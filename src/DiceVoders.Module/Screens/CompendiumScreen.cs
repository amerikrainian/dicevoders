using System;
using System.Collections.Generic;
using StarVaders;
using DiceVoders.Core;
using DiceVoders.Core.Graph;
using DiceVoders.Core.Strings;
using DiceVoders.Core.UI;
using DiceVoders.Module.Interop;
using DiceVoders.Module.UI;
using UnityEngine;
using Screen = DiceVoders.Core.Screens.Screen;

namespace DiceVoders.Module.Screens
{
    /// <summary>
    /// The compendium (<see cref="CompendiumController"/>, CompendiumSceneAdditive over the main menu,
    /// from its Compendium button), named by its title. One stop: "Unlocked: 12%", "Completion: 0%";
    /// its pages as a row named by their subtitles ("Profile Stats", "Perks (1-24)", "Common
    /// Artifacts", "Units"...), landing on one turns to it (<see cref="CompendiumController.SetPage"/>);
    /// then the page: the stats page's figures, each label with its value ("Runs played: 1"), its
    /// chapters, challenges and commanders as rows; any other page's entries as drawn, rows by
    /// height: an artifact or perk as the board reads it (Backspace its tooltip), a unit by name
    /// (Backspace its tooltip), a locked slot "locked" (the game draws a bare lock). Exit last, Escape
    /// too. The constellations panel is not read yet.
    /// </summary>
    public sealed class CompendiumScreen : Screen
    {
        public override string Key => "compendium";
        public override int Layer => 10;
        public override bool Exclusive => true;

        private static CompendiumController Controller()
        {
            var c = GameScenes.Controller<CompendiumController>();
            return c != null && c.Canvas != null && c.Canvas.gameObject.activeInHierarchy && c.Canvas.alpha > 0.01f ? c : null;
        }

        public override bool IsActive() => Controller() != null;

        public override string ScreenName
        {
            get
            {
                var c = Controller();
                var title = c != null ? c.Canvas.transform.Find("Pages/Title") : null;
                return title != null ? GameText.Of(title.GetComponent<TMPro.TMP_Text>()) : null;
            }
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller();
            if (c == null) return;
            if (c.IsConstellationPanelActive()) { BuildConstellations(b, c); return; }
            var pages = c.Canvas.transform.Find("Pages");
            if (pages == null) return;
            b.PushContext(null);
            foreach (var name in new[] { "Unlocked", "Completion" })
            {
                var t = pages.Find(name);
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                var tr = t;
                b.AddItem(ControlId.Structural("comp:" + name), GameNodes.Text(() => Paired(tr)));
            }

            // The pages, a row; landing on one turns to it.
            b.StartRow("comp:pages", entry: () => Math.Max(0, c.currentIndex));
            for (int i = 0; i < c.Pages.Count; i++)
            {
                int page = i;
                Func<string> label = () => PageName(c, page);
                b.AddItem(ControlId.Structural("comp:page:" + i), new NodeVtable
                {
                    ControlType = ControlTypes.Tab,
                    Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label), GameNodes.SelectedPart(() => c.currentIndex == page) },
                    SearchText = label,
                    OnFocus = () => { if (c.currentIndex != page) Turn(c, page); },
                });
            }
            b.EndRow();

            var shown = c.currentIndex >= 0 && c.currentIndex < c.Pages.Count ? c.Pages[c.currentIndex] : null;
            if (shown != null && shown.activeInHierarchy)
            {
                if (shown.name == "StatsPage") BuildStats(b, c, shown.transform);
                else BuildEntries(b, shown.transform);
            }

            // Constellations (once unlocked), beside Exit.
            var constellations = c.CompendiumButton != null && c.CompendiumButton.activeInHierarchy ? c.CompendiumButton.GetComponentInChildren<ButtonView>(false) : null;
            if (ViewNodes.IsShown(constellations))
                b.AddItem(ControlId.Structural("comp:constellations"), ViewNodes.Button(constellations, () => GameLoc.Get("Constellations_Misc") ?? "Constellations"));
            var exit = pages.Find("BG/Exit/ExitButton");
            var exitItem = exit != null ? exit.GetComponent<AViewItem>() : null;
            if (ViewNodes.IsShown(exitItem)) b.AddItem(ControlId.Structural("comp:exit"), ViewNodes.Button(exitItem));
            b.PopContext();
        }

        // The constellations panel (OnConstellationPressed): "1-3 / 30", the shown three as the
        // encounter's scene reads them (title and description, Backspace the set), the page arrows
        // (drawn with no caption: the credits' "Previous page"/"Next page"), Exit; Escape too.
        private static void BuildConstellations(GraphBuilder b, CompendiumController c)
        {
            b.PushContext(GameLoc.Get("Constellations_Misc"));
            var page = c.ConstellationsPageNumber;
            if (page != null && page.gameObject.activeInHierarchy)
                b.AddItem(ControlId.Structural("comp:cons:page"), GameNodes.Text(() => GameText.Of(page)));
            int i = 0;
            if (c.Constellations != null)
                foreach (var con in c.Constellations)
                    if (con != null && con.gameObject.activeInHierarchy)
                        b.AddItem(ControlId.Structural("comp:cons:" + i++), ConstellationScreen.Node(con));
            var arrows = page != null ? page.transform.parent : null;
            foreach (var (name, key) in new[] { ("LeftArrow", "credits.prev"), ("RightArrow", "credits.next") })
            {
                var t = arrows != null ? arrows.Find(name) : null;
                var item = t != null ? t.GetComponent<AViewItem>() : null;
                if (!ViewNodes.IsShown(item)) continue;
                var vt = ViewNodes.Button(item, () => Strings.Get(key));
                var arrow = item;
                // A turn lands on the page line, the new page's figure ("4-6 / 30").
                vt.OnActivate = () => { ViewNodes.Click(arrow); Navigation.MoveTo(ControlId.Structural("comp:cons:page")); };
                b.AddItem(ControlId.Structural("comp:cons:" + name), vt);
            }
            var exit = c.ConstellationExitButton;
            // Its caption is drawn beside it (Exit/Mover/Top): the game's "Exit".
            if (ViewNodes.IsShown(exit)) b.AddItem(ControlId.Structural("comp:cons:exit"), ViewNodes.Button(exit, () => GameLoc.Get("Exit_Misc") ?? "Exit"));
            b.PopContext();
        }

        private static void Turn(CompendiumController c, int page)
        {
            try { c.SetPage(page); }
            catch (Exception e) { CoreLog.Warning("CompendiumScreen: turning to page " + page + " failed: " + e.Message); }
        }

        // A page's subtitle by its key: a page not yet shown still holds its prefab's text ("Powers
        // (1-24)" on the second and third perks pages).
        private static readonly Dictionary<string, string> PageKeys = new Dictionary<string, string>
        {
            ["StatsPage"] = "GameStats_Misc", ["PowersPage"] = "Powers_Subtitle_1", ["PowersPage_2"] = "Powers_Subtitle_2",
            ["PowersPage_3"] = "Powers_Subtitle_3", ["CommonArtifactsPage"] = "CommonArti_Subtitle",
            ["UncommonArtifactsPage"] = "UncommonArti_Subtitle", ["RareArtifactsPage"] = "RareArti_Subtitle",
            ["LegendaryArtifactsPage"] = "LegendaryArti_Subtitle", ["UnitsPage"] = "Units_Subtitle", ["WarpedUnitsPage"] = "WarpedUnits_Subtitle",
        };

        private static string PageName(CompendiumController c, int page)
        {
            var go = c.Pages[page];
            if (go == null) return null;
            if (PageKeys.TryGetValue(go.name, out var key)) { string name = GameLoc.Get(key)?.Trim(); if (!string.IsNullOrEmpty(name)) return name; }
            var subtitle = go.transform.Find("Subtitle");
            return (subtitle != null ? GameText.Of(subtitle.GetComponent<TMPro.TMP_Text>())?.Trim() : null) ?? go.name;
        }

        // A label text with its value drawn as a child text: "Unlocked: 12%", "Runs played: 1".
        private static string Paired(Transform label)
        {
            var parts = new List<string>();
            foreach (var tmp in label.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                string text = GameText.Of(tmp);
                if (text != null) parts.Add(text.Trim());
            }
            return string.Join(" ", parts);
        }

        // The stats page: its subtitle is the page's name; each figure, a label with its value; the
        // chapters, the challenges and the commanders, each a row after its heading line.
        private static void BuildStats(GraphBuilder b, CompendiumController c, Transform page)
        {
            var general = page.Find("General");
            if (general != null)
                for (int i = 0; i < general.childCount; i++)
                {
                    var line = general.GetChild(i);
                    if (!line.gameObject.activeInHierarchy || line.GetComponent<TMPro.TMP_Text>() == null) continue;
                    b.AddItem(ControlId.Structural("comp:stat:" + line.name), GameNodes.Text(() => Paired(line)));
                }

            var challenges = page.Find("Challenges");
            Heading(b, challenges, "comp:challenges");
            var chapters = new List<DifficultyNode>();
            if (c.DifficultyNodes != null)
                foreach (var n in c.DifficultyNodes) if (n != null && n.gameObject.activeInHierarchy) chapters.Add(n);
            chapters.Sort((l, r) => l._index.CompareTo(r._index));
            if (chapters.Count > 0)
            {
                b.StartRow("comp:chapters");
                foreach (var n in chapters)
                {
                    var node = n;
                    Func<string> label = () => NodeLabel(GameLoc.Get("Chapter" + (node._index + 1) + "_Title") ?? ("Chapter " + (node._index + 1)), node);
                    b.AddItem(ControlId.Structural("comp:chapter:" + node._index), Text(label, null, node));
                }
                b.EndRow();
            }
            var challengeNodes = new List<DifficultyNode>();
            if (c.ChallengeNodes != null)
                foreach (var n in c.ChallengeNodes) if (n != null && n.gameObject.activeInHierarchy) challengeNodes.Add(n);
            challengeNodes.Sort((l, r) => l._index.CompareTo(r._index));
            if (challengeNodes.Count > 0)
            {
                b.PushContext(Strings.Get("cs.challenges"));
                b.StartRow("comp:challengerow");
                foreach (var n in challengeNodes)
                {
                    var node = n;
                    string key = node.ChallengeName.ToString();
                    Func<string> label = () => NodeLabel(GameLoc.Get(key + "_Title") ?? key, node);
                    Func<IEnumerable<string>> details = () => GameNodes.Lines(GameLoc.Get(key + "_Desc"), GameLoc.Get(key + "Mod_Title"), GameLoc.Get(key + "Mod_Desc"));
                    b.AddItem(ControlId.Structural("comp:challenge:" + key), Text(label, details, node));
                }
                b.EndRow();
                b.PopContext();
            }

            var commanders = page.Find("Commanders");
            Heading(b, commanders, "comp:commanders");
            var icons = new List<CommanderIconView>();
            if (c.CommanderIconViews != null)
                foreach (var icon in c.CommanderIconViews) if (icon != null && icon.gameObject.activeInHierarchy) icons.Add(icon);
            if (icons.Count > 0)
            {
                b.StartRow("comp:commanderrow");
                // A display (the icons' click acts only on the commander select): each by name, or its
                // lock, "completed" while its star (won with it) shows.
                foreach (var icon in icons)
                {
                    var ic = icon;
                    Func<string> label = () =>
                    {
                        string name = CharacterSelectScreen.CommanderLabel(ic);
                        bool won = false;
                        try { won = ic.Star != null && ic.Star.activeInHierarchy; }
                        catch (Exception e) { CoreLog.Warning("CompendiumScreen: reading a commander's star failed: " + e.Message); }
                        return won ? name + ", " + Strings.StateCompleted : name;
                    };
                    b.AddItem(ControlId.Structural("comp:commander:" + icon.CommanderName), Text(label, () => CharacterSelectScreen.CommanderDetails(ic), ic));
                }
                b.EndRow();
            }
        }

        // A section's own line ("0 / 19 Challenges Completed"), its first shown text.
        private static void Heading(GraphBuilder b, Transform section, string id)
        {
            if (section == null || !section.gameObject.activeInHierarchy) return;
            var tmp = section.Find("Text (TMP)");
            if (tmp == null || !tmp.gameObject.activeInHierarchy) return;
            var text = tmp.GetComponent<TMPro.TMP_Text>();
            b.AddItem(ControlId.Structural(id), GameNodes.Text(() => GameText.Of(text)));
        }

        private static string NodeLabel(string name, DifficultyNode node)
        {
            var parts = new List<string> { name };
            try
            {
                if (node.IsLocked) parts.Add(Strings.StateLocked);
                bool done = false;
                if (node.CompletedObjects != null)
                    foreach (var o in node.CompletedObjects) if (o != null && o.activeInHierarchy) done = true;
                if (done) parts.Add(Strings.StateCompleted);
            }
            catch (Exception e) { CoreLog.Warning("CompendiumScreen: reading a node failed: " + e.Message); }
            return string.Join(", ", parts);
        }

        private static NodeVtable Text(Func<string> label, Func<IEnumerable<string>> details, AViewItem hover) => new NodeVtable
        {
            ControlType = ControlTypes.Text,
            Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label) },
            SearchText = label,
            OnFocus = () => { if (hover != null) ViewNodes.Hover(hover); },
            Details = details,
            OnSecondary = details != null ? () => SayDetails(details()) : (Action)null,
        };

        // A grid page: its entries in rows by height, top first, left to right.
        // A grid page: each holder's entries in hierarchy order, in rows of the holder's grid
        // (GridLayoutGroup, FixedColumnCount: 8 to a row on the perks pages), else one row.
        private static void BuildEntries(GraphBuilder b, Transform page)
        {
            int id = 0;
            foreach (var grid in page.GetComponentsInChildren<UnityEngine.UI.GridLayoutGroup>(false))
            {
                var holder = grid.transform;
                var nodes = new List<NodeVtable>();
                for (int i = 0; i < holder.childCount; i++)
                {
                    var t = holder.GetChild(i);
                    if (!t.gameObject.activeSelf) continue;
                    var artifact = t.GetComponent<ArtifactView>();
                    var unit = t.GetComponent<InvaderCompendiumEntry>();
                    if (artifact != null && artifact.ArtifactModel != null && artifact.ArtifactModel.ArtifactName != ArtifactName.None) nodes.Add(Artifact(artifact));
                    else if (unit != null) nodes.Add(Unit(unit));
                    else if (t.name.StartsWith("Locked")) nodes.Add(Text(() => Strings.StateLocked, null, null));
                }
                int perRow = grid.constraint == UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount && grid.constraintCount > 0 ? grid.constraintCount : Math.Max(1, nodes.Count);
                for (int k = 0; k < nodes.Count; k += perRow)
                {
                    b.StartRow("comp:grid");
                    for (int j = k; j < Math.Min(nodes.Count, k + perRow); j++) b.AddItem(ControlId.Structural("comp:e:" + id++), nodes[j]);
                    b.EndRow();
                }
            }
        }

        private static NodeVtable Artifact(ArtifactView view)
        {
            Func<string> label = () => Encounter.Encounter.ArtifactName(view.ArtifactModel);
            Func<IEnumerable<string>> details = () => EncounterScreen.ArtifactDetails(view.ArtifactModel, view);
            return Text(label, details, view);
        }

        private static NodeVtable Unit(InvaderCompendiumEntry entry)
        {
            Func<string> label = () => GameLoc.Get(entry.EntityName + "_Entity_Title") ?? entry.EntityName.ToString();
            Func<IEnumerable<string>> details = () =>
            {
                var unit = entry.entityView != null ? entry.entityView.EntityModel : null;
                if (unit == null) return GameNodes.Lines(GameLoc.Get(entry.EntityName + "_Entity_Desc"));
                var (desc, rarity, keywords) = Encounter.Encounter.Tooltip(unit, entry.EntityTooltip);
                var lines = new List<string>();
                if (desc != null) lines.Add(desc);
                if (rarity != null) lines.Add(rarity);
                lines.AddRange(keywords);
                return lines;
            };
            return Text(label, details, entry);
        }

        private static void SayDetails(IEnumerable<string> lines)
        {
            var list = new List<string>(lines ?? Array.Empty<string>());
            Speech.Say(list.Count > 0 ? string.Join(" ", list) : Strings.NoDetails, interrupt: true);
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var c = Controller();
                if (c != null && c.IsConstellationPanelActive())
                {
                    if (ViewNodes.IsShown(c.ConstellationExitButton)) ViewNodes.Click(c.ConstellationExitButton);
                    return;
                }
                var exit = c != null ? c.Canvas.transform.Find("Pages/BG/Exit/ExitButton") : null;
                var item = exit != null ? exit.GetComponent<AViewItem>() : null;
                if (ViewNodes.IsShown(item)) ViewNodes.Click(item);
            });
        }
    }
}
