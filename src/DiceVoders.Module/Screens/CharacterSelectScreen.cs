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
    /// The commander select (<see cref="CharacterSelectionController"/>, CharacterSelectionScene), the
    /// run's hub, where the game shows its unlocks. One stop, as drawn: the top right panel (the
    /// tutorial's "Mission Complete!", later the chapter's mission) as its lines, then its buttons
    /// ("New Chapter Unlocked!": <see cref="CharacterSelectionController.OnPressCompleteTutorial"/>);
    /// the commanders as a row (<see cref="CharacterSelectionController.CommanderIcons"/> shown, left
    /// to right): a locked one as its tooltip shows it, the lock's title ("Locked - The Invasion
    /// Begins!") with its condition for Backspace, as a sighted player sees only a locked square; an
    /// unlocked one by name; then every other button shown, in hierarchy order, by caption; Back
    /// (<see cref="CharacterSelectionController.OnExitPressed"/>) last, Escape too. Seen live only with
    /// the tutorial's panel up and after it: see CLAUDE.md for what has been read.
    /// </summary>
    public sealed class CharacterSelectScreen : Screen
    {
        public override string Key => "charselect";

        internal static CharacterSelectionController Controller => GameScenes.Controller<CharacterSelectionController>();

        public override bool IsActive() => Controller != null;

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            var items = GameScenes.Items(c);
            if (c == null || items == null) return;
            var taken = new HashSet<IntPtr>();
            b.PushContext(null);
            try { BuildAll(b, c, items, taken); }
            catch (Exception e) { CoreLog.Warning("CharacterSelectScreen: building failed: " + e); }
            b.PopContext();
        }

        private static void BuildAll(GraphBuilder b, CharacterSelectionController c, IReadOnlyList<AViewItem> items, HashSet<IntPtr> taken)
        {
            // The chapter: Select Challenge, the toggle of the chapter panel (expanded while it is
            // open), with the chosen chapter's name while it is shut; open, the chapters as a row
            // ("CH1 - First Contact", locked, completed) entered at the chosen one, and its
            // description. The panel's headings repeat the chosen chapter's name and its arrows are
            // the row's neighbours: both left out.
            foreach (var item in items)
            {
                // Items gathered with the scene can be destroyed since (a unique's lock, once unlocked).
                if (item == null) continue;
                if (item.transform.parent != null && item.transform.parent.name == "Title" && item.TryCast<ButtonView>() != null)
                    taken.Add(item.Pointer);
                if (item.gameObject.name != "DifficultyDropdownButton" || !ViewNodes.IsShown(item)) continue;
                taken.Add(item.Pointer);
                var toggle = item;
                var vt = ViewNodes.Button(toggle, () => ChallengeLabel(c, toggle));
                vt.Announcements = new List<NodeAnnouncement>(vt.Announcements)
                {
                    new NodeAnnouncement(() => Strings.Get(c.IsDifficultyPanelActive ? "state.expanded" : "state.collapsed"), live: true),
                };
                b.AddItem(ControlId.Structural("cs:challenge"), vt);
            }
            var nodes = new List<DifficultyNode>();
            if (c.DifficultyNodes != null)
                foreach (var node in c.DifficultyNodes)
                    if (node != null && node.gameObject.activeInHierarchy) { nodes.Add(node); taken.Add(node.Pointer); }
            nodes.Sort((l, r) => l._index.CompareTo(r._index));
            if (nodes.Count > 0)
            {
                b.StartRow("cs:chapters", entry: () => Math.Max(0, nodes.FindIndex(n => n.IsSelected)));
                foreach (var node in nodes) b.AddItem(ControlId.Structural("cs:chapter:" + node._index), Chapter(c, node));
                b.EndRow();
            }
            foreach (var tmp in new[] { c.DifficultyPanelFlavor, c.Chapter1TutObject != null ? c.Chapter1TutObject.GetComponent<TMPro.TMP_Text>() : null })
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;
                var text = tmp;
                b.AddItem(ControlId.Structural("cs:chapterline:" + tmp.gameObject.name), GameNodes.Text(() => GameText.Of(text)));
            }
            // The challenges (once unlocked, on the chapter panel's map): a row, by name ("Strange
            // World"), locked, completed, selected as chosen; Backspace its description and rules.
            var challenges = new List<DifficultyNode>();
            if (c.ChallengeNodes != null)
                foreach (var node in c.ChallengeNodes)
                    if (node != null && node.gameObject.activeInHierarchy) { challenges.Add(node); taken.Add(node.Pointer); }
            challenges.Sort((l, r) => l._index.CompareTo(r._index));
            if (challenges.Count > 0)
            {
                b.PushContext(Strings.Get("cs.challenges"));
                b.StartRow("cs:challenges", entry: () => Math.Max(0, challenges.FindIndex(n => n.IsSelected)));
                foreach (var node in challenges) b.AddItem(ControlId.Structural("cs:challenge:" + node.ChallengeName), Challenge(node));
                b.EndRow();
                b.PopContext();
            }

            // The mission panel (the tutorial's "Mission Complete!", later the chapter's): its lines,
            // then its buttons.
            var panel = c.TutorialCompletePanel != null && c.TutorialCompletePanel.activeInHierarchy ? c.TutorialCompletePanel.transform
                : c.MissionPanel != null && c.MissionPanel.activeInHierarchy ? c.MissionPanel.transform : null;
            if (panel != null)
            {
                var lines = ViewNodes.Prose(panel);
                for (int i = 0; i < lines.Count; i++)
                {
                    string line = lines[i];
                    b.AddItem(ControlId.Structural("cs:panel:" + i), GameNodes.Text(() => line));
                }
                foreach (var item in panel.GetComponentsInChildren<AViewItem>(false))
                {
                    if (item.TryCast<IClickable>() == null || !taken.Add(item.Pointer)) continue;
                    b.AddItem(ControlId.Structural("cs:panelbutton:" + item.gameObject.name), ViewNodes.Button(item));
                }
            }

            // The commanders, a row.
            var icons = new List<CommanderIconView>();
            if (c.CommanderIcons != null)
                foreach (var icon in c.CommanderIcons)
                    if (icon != null && icon.gameObject.activeInHierarchy) icons.Add(icon);
            icons.Sort((l, r) => l.transform.position.x.CompareTo(r.transform.position.x));
            if (icons.Count > 0)
            {
                b.StartRow("cs:commanders", entry: () => Math.Max(0, icons.FindIndex(i => Selected(i))));
                foreach (var icon in icons)
                {
                    taken.Add(icon.Pointer);
                    b.AddItem(ControlId.Structural("cs:commander:" + icon.CommanderName), Commander(icon));
                }
                // The random commander, at the list's end: it draws only its key prompt.
                foreach (var item in items)
                {
                    if (item == null || item.gameObject.name != "RandomIcon" || !ViewNodes.IsShown(item)) continue;
                    taken.Add(item.Pointer);
                    b.AddItem(ControlId.Structural("cs:random"), ViewNodes.Button(item, () => Strings.Get("cs.random")));
                }
                b.EndRow();
            }

            // The chosen commander's panel: name and level, the starting perk, its unique artifacts.
            BuildCommander(b, c, taken);

            // Whatever else is shown and clickable (Launch!), Back last.
            AViewItem back = null;
            foreach (var item in items)
            {
                if (item == null || !ViewNodes.IsShown(item) || item.TryCast<IClickable>() == null || !taken.Add(item.Pointer)) continue;
                if (item.gameObject.name == "BackButton") { back = item; continue; }
                b.AddItem(ControlId.Structural("cs:" + item.gameObject.name + ":" + item.GetInstanceID()), ViewNodes.Button(item));
            }
            if (back != null)
                b.AddItem(ControlId.Structural("cs:back"), ViewNodes.Button(back, () => GameLoc.Get("Back_Misc") ?? "Back"));
        }

        private static NodeVtable Challenge(DifficultyNode node)
        {
            string key = node.ChallengeName.ToString();
            Func<string> label = () =>
            {
                var parts = new List<string> { GameLoc.Get(key + "_Title") ?? key };
                if (node.IsLocked) parts.Add(Strings.StateLocked);
                bool done = false;
                try { if (node.CompletedObjects != null) foreach (var o in node.CompletedObjects) if (o != null && o.activeInHierarchy) done = true; }
                catch (Exception e) { CoreLog.Warning("CharacterSelectScreen: reading a challenge failed: " + e.Message); }
                if (done) parts.Add(Strings.StateCompleted);
                return string.Join(", ", parts);
            };
            Func<IEnumerable<string>> details = () => GameNodes.Lines(GameLoc.Get(key + "_Desc"), GameLoc.Get(key + "Mod_Title"), GameLoc.Get(key + "Mod_Desc"));
            return new NodeVtable
            {
                ControlType = ControlTypes.RadioButton,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label), GameNodes.SelectedPart(() => node.IsSelected) },
                SearchText = label,
                OnFocus = () => ViewNodes.Hover(node),
                OnActivate = () => ViewNodes.Click(node),
                Details = details,
                OnSecondary = () => SayDetails(details()),
            };
        }

        // "Select Challenge, CH1 - First Contact": the caption, and the chosen chapter's name drawn
        // above it while the chapter panel is shut.
        private static string ChallengeLabel(CharacterSelectionController c, AViewItem toggle)
        {
            string caption = ViewNodes.Label(toggle);
            if (c.IsDifficultyPanelActive) return caption;
            var panel = toggle.transform.parent != null ? toggle.transform.parent.parent : null;
            var header = panel != null ? panel.Find("Image/Text (TMP)") : null;
            string chapter = header != null && header.gameObject.activeInHierarchy ? GameText.Of(header.GetComponent<TMPro.TMP_Text>()) : null;
            return chapter != null ? caption + ", " + chapter : caption;
        }

        // The commander panel (Frame, shown once a commander is chosen): "The Captain, LV. 2"; the
        // starting perk as the board reads it, Backspace its tooltip; the unique artifacts as drawn,
        // three rows top to bottom of left and right: an unlocked one by name (Backspace its tooltip),
        // a locked one by its tooltip's condition ("Defeat 4 pilots with this commander to level up
        // and unlock this! (2/4)"), Backspace its title ("Locked LV.3 Unique"); Enter is the game's
        // click (an unlockable one's reveal).
        private static void BuildCommander(GraphBuilder b, CharacterSelectionController c, HashSet<IntPtr> taken)
        {
            if (c.Frame == null || !c.Frame.gameObject.activeInHierarchy) return;
            b.AddItem(ControlId.Structural("cs:commandername"), GameNodes.Text(() => CommanderTitle(c)));
            var starting = c.CommanderArtifactView;
            if (starting != null && starting.gameObject.activeInHierarchy && Model(starting) != null)
                b.AddItem(ControlId.Structural("cs:starting"), ArtifactNode(starting, false));
            var slots = new List<(Transform At, NodeVtable Node, string Id)>();
            for (int i = 0; i < (c.LockedArtifactItems?.Count ?? 0); i++)
            {
                var locked = c.LockedArtifactItems[i];
                var unlocked = c.UnlockedArtifactObjects != null && i < c.UnlockedArtifactObjects.Count ? c.UnlockedArtifactObjects[i] : null;
                var view = c.UnlockedArtifactViews != null && i < c.UnlockedArtifactViews.Count ? c.UnlockedArtifactViews[i] : null;
                if (locked != null && locked.gameObject.activeInHierarchy)
                {
                    taken.Add(locked.Pointer);
                    slots.Add((locked.transform, LockedNode(locked), "cs:unique:" + i));
                }
                else if (unlocked != null && unlocked.activeInHierarchy && view != null && Model(view) != null)
                    slots.Add((view.transform, ArtifactNode(view, true), "cs:unique:" + i));
            }
            // Rows by height, top first; left to right within one.
            slots.Sort((l, r) => Math.Abs(l.At.position.y - r.At.position.y) > 5f ? r.At.position.y.CompareTo(l.At.position.y) : l.At.position.x.CompareTo(r.At.position.x));
            int k = 0;
            while (k < slots.Count)
            {
                float y = slots[k].At.position.y;
                b.StartRow("cs:uniques");
                while (k < slots.Count && Math.Abs(slots[k].At.position.y - y) <= 5f) { b.AddItem(ControlId.Structural(slots[k].Id), slots[k].Node); k++; }
                b.EndRow();
            }
        }

        private static string CommanderTitle(CharacterSelectionController c)
        {
            var parts = new List<string>();
            string name = GameText.Of(c.CommanderText);
            if (name != null) parts.Add(name);
            var level = c.LevelTextObjectChinese != null && c.LevelTextObjectChinese.activeInHierarchy ? c.LevelTextObjectChinese : c.LevelTextObject;
            if (level != null && level.activeInHierarchy)
            {
                var words = new List<string>();
                foreach (var tmp in level.GetComponentsInChildren<TMPro.TMP_Text>(false)) { string w = GameText.Of(tmp); if (w != null) words.Add(w.Trim()); }
                if (words.Count > 0) parts.Add(string.Join(" ", words));
            }
            return string.Join(", ", parts);
        }

        private static ArtifactModel Model(ArtifactView view)
        {
            var model = view.ArtifactModel;
            return model != null && model.ArtifactName != ArtifactName.None ? model : null;
        }

        // An unlocked unique's tooltip carries the game's unlock text ("You now have a chance to find
        // this perk during a run with this commander!"), as the game sets it (SetModel(model, true)).
        private static NodeVtable ArtifactNode(ArtifactView view, bool withUnlockText)
        {
            Func<string> label = () => EncounterScreen.ArtifactLabel(Model(view));
            Func<IEnumerable<string>> details = () => EncounterScreen.ArtifactDetails(Model(view), view, withUnlockText);
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label) },
                SearchText = label,
                OnFocus = () => ViewNodes.Hover(view),
                Details = details,
                OnSecondary = () => SayDetails(details()),
            };
        }

        private static NodeVtable LockedNode(LockedArtifactItemView item)
        {
            // By its condition (the slot draws a bare lock, its title only "Locked LV.6 Unique"):
            // the title for Backspace.
            Func<string> label = () => LockedText(item, false) ?? LockedText(item, true) ?? ViewNodes.Label(item);
            Func<IEnumerable<string>> details = () => GameNodes.Lines(LockedText(item, true));
            var vt = ViewNodes.Button(item, label);
            vt.Details = details;
            vt.OnSecondary = () => SayDetails(details());
            return vt;
        }

        private static string LockedText(LockedArtifactItemView item, bool title)
        {
            try
            {
                var tip = item.Tooltip;
                var tmp = tip != null ? (title ? tip.TitleText : tip.DescriptionText) : null;
                return tmp != null && tmp.gameObject.activeSelf ? GameText.Of(tmp) : null;
            }
            catch (Exception e) { CoreLog.Warning("CharacterSelectScreen: reading a locked artifact failed: " + e.Message); return null; }
        }

        // A chapter: "CH1 - First Contact" (<DifficultyName>_Title), locked, completed; selected as
        // chosen. Enter is the game's click.
        private static NodeVtable Chapter(CharacterSelectionController c, DifficultyNode node)
        {
            Func<string> label = () => ChapterLabel(c, node);
            return new NodeVtable
            {
                ControlType = ControlTypes.RadioButton,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(label),
                    GameNodes.SelectedPart(() => node.IsSelected),
                },
                SearchText = label,
                OnFocus = () => ViewNodes.Hover(node),
                OnActivate = () => ViewNodes.Click(node),
            };
        }

        private static string ChapterLabel(CharacterSelectionController c, DifficultyNode node)
        {
            var parts = new List<string>();
            try
            {
                var names = c.DifficultyNames;
                string key = names != null && node._index < names.Count ? names[node._index].ToString() : null;
                parts.Add((key != null ? GameLoc.Get(key + "_Title") : null) ?? key ?? node.gameObject.name);
                if (node.IsLocked) parts.Add(Strings.StateLocked);
                bool done = false;
                if (node.CompletedObjects != null)
                    foreach (var o in node.CompletedObjects) if (o != null && o.activeInHierarchy) done = true;
                if (done) parts.Add(Strings.StateCompleted);
            }
            catch (Exception e) { CoreLog.Warning("CharacterSelectScreen: reading a chapter failed: " + e.Message); }
            return string.Join(", ", parts);
        }

        private static bool Selected(CommanderIconView icon)
        {
            try { return icon.SelectedObject != null && icon.SelectedObject.activeInHierarchy; }
            catch (Exception e) { CoreLog.Warning("CharacterSelectScreen: reading a commander's selection failed: " + e.Message); return false; }
        }

        // A commander: locked, its tooltip's lock title ("Locked - The Invasion Begins!"), Backspace the
        // condition; unlocked, its name ("Mothership", the game's <c>_Arti_Title</c>), selected by the
        // game's highlight. Enter is the game's click.
        internal static NodeVtable Commander(CommanderIconView icon)
        {
            Func<string> label = () => CommanderLabel(icon);
            Func<IEnumerable<string>> details = () => CommanderDetails(icon);
            return new NodeVtable
            {
                ControlType = ControlTypes.RadioButton,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(label),
                    GameNodes.SelectedPart(() => Selected(icon)),
                },
                SearchText = label,
                OnFocus = () => ViewNodes.Hover(icon),
                OnActivate = () => ViewNodes.Click(icon),
                Details = details,
                OnSecondary = () => SayDetails(details()),
            };
        }

        // "The Captain, New!": the commander's name (<c>_Misc</c>; <c>_Arti_Title</c> is its perk's),
        // and the game's New! while it shows.
        internal static string CommanderLabel(CommanderIconView icon)
        {
            if (icon.IsLocked)
            {
                string title = TooltipText(icon, t => t.TitleText);
                if (title != null) return title;
            }
            string name = GameLoc.Get(icon.CommanderName + "_Misc") ?? icon.CommanderName.ToString();
            try
            {
                if (icon.NewText != null && icon.NewText.activeInHierarchy)
                {
                    // The field is the UnlockIcon; its NewText child draws the word.
                    string isNew = GameText.Of(icon.NewText.GetComponentInChildren<TMPro.TMP_Text>(false));
                    if (isNew != null) return name + ", " + isNew;
                }
            }
            catch (Exception e) { CoreLog.Warning("CharacterSelectScreen: reading a commander's New! failed: " + e.Message); }
            return name;
        }

        internal static IEnumerable<string> CommanderDetails(CommanderIconView icon)
        {
            var lines = new List<string>();
            if (icon.IsLocked)
            {
                string desc = TooltipText(icon, t => t.DescriptionText);
                if (desc != null) lines.Add(desc);
                return lines;
            }
            // Its starting perk by name: the perk itself (its figures by level) is the details panel's.
            string perk = GameLoc.Get(icon.CommanderName + "_Arti_Title");
            if (perk != null) lines.Add(perk);
            return lines;
        }

        private static string TooltipText(CommanderIconView icon, Func<Tooltip, TMPro.TMP_Text> pick)
        {
            try
            {
                var tip = icon.Tooltip;
                var tmp = tip != null ? pick(tip) : null;
                return tmp != null && tmp.gameObject.activeSelf ? GameText.Of(tmp) : null;
            }
            catch (Exception e) { CoreLog.Warning("CharacterSelectScreen: reading a commander's tooltip failed: " + e.Message); return null; }
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
                var c = Controller;
                if (c == null) return;
                foreach (var item in GameScenes.Items(c) ?? new List<AViewItem>())
                    if (item.gameObject.name == "BackButton" && ViewNodes.IsShown(item)) { ViewNodes.Click(item); return; }
            });
        }
    }
}
