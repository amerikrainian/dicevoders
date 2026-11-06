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
    /// unlocked one by name; the chosen commander's panel (with the Overseer, the two commanders it
    /// borrows from, a row a side); then every other button shown, in hierarchy order, by caption; Back
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
            // The run's modifiers, the icon line at the panel's top (ModifierLine, drawn open or shut):
            // the chosen chapter's or challenge's icon, then each modifier in play.
            BuildModifiers(b, c);
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
            BuildNewMechanic(b, c);
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

            // The mission panel, whichever of its kinds shows (the tutorial's "Mission Complete!", a
            // chapter's mission, CH6's two missions, the game finished, the demo's end, Unlock All's
            // "There are no missions... enjoy!"): its lines, then its buttons.
            Transform panel = null;
            foreach (var kind in new[] { c.TutorialCompletePanel, c.MissionPanel, c.Chapter6MissionPanel, c.EndOfContentPanel, c.NoNewMissionThisVersion, c.UnlockAllMissionPanel })
                if (panel == null && kind != null && kind.activeInHierarchy) panel = kind.transform;
            if (panel != null)
            {
                var lines = ViewNodes.Prose(panel);
                var done = CheckedLines(panel);
                // Proxies are fresh objects per cast: compared by native pointer.
                string mission = c.MissionPanel != null && panel.Pointer == c.MissionPanel.transform.Pointer ? GameText.Of(c.MissionDesc) : null;
                for (int i = 0; i < lines.Count; i++)
                {
                    string line = lines[i];
                    if (mission != null && line == mission)
                    {
                        var winners = MissionWinners(c);
                        if (winners.Count > 0) line += ", " + string.Join(", ", winners);
                    }
                    if (done.Contains(line)) line += ", " + Strings.StateCompleted;
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
            BuildOverseer(b, c, taken);

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

        // CH6's missions ("Complete CHAPTER 6.") each carry a check mark or a red cross (their
        // CheckMark and Cross children): the texts whose check mark shows. A cross is unsaid, as an
        // unfilled commander slot is.
        private static HashSet<string> CheckedLines(Transform panel)
        {
            var done = new HashSet<string>();
            try
            {
                foreach (var tmp in panel.GetComponentsInChildren<TMPro.TMP_Text>(false))
                {
                    var mark = tmp.transform.Find("CheckMark");
                    if (mark == null || !mark.gameObject.activeInHierarchy) continue;
                    string text = GameText.Of(tmp);
                    if (text != null) done.Add(text);
                }
            }
            catch (Exception e) { CoreLog.Warning("CharacterSelectScreen: reading the missions' check marks failed: " + e.Message); }
            return done;
        }

        // The mission's commanders ("Win CH2 with 2 commanders" over one slot each, MissionImages): a
        // slot with its check mark shown holds the portrait of a commander who won the mission's
        // chapter (the game's CommanderSpriteMapping._square), named here by matching that portrait;
        // an unfilled slot (a question mark, a cross) is left out, the count being in the mission.
        private static List<string> MissionWinners(CharacterSelectionController c)
        {
            var names = new List<string>();
            try
            {
                var images = c.MissionImages;
                var checks = c.MissionCheckBoxes;
                if (images == null || checks == null) return names;
                var data = c._commanderDataSO;
                for (int i = 0; i < images.Count && i < checks.Count; i++)
                {
                    var image = images[i];
                    if (image == null || !image.gameObject.activeInHierarchy || checks[i] == null || !checks[i].activeInHierarchy) continue;
                    var sprite = image.sprite;
                    string name = null;
                    if (sprite != null && data != null)
                        foreach (CommanderName commander in Enum.GetValues(typeof(CommanderName)))
                        {
                            CommanderSpriteMapping mapping = null;
                            try { mapping = data.GetCommanderData(commander); }
                            catch (Exception e) { CoreLog.Warning("CharacterSelectScreen: no portrait data for " + commander + ": " + e.Message); }
                            if (mapping == null || mapping._square == null || mapping._square.Pointer != sprite.Pointer) continue;
                            name = GameLoc.Get(commander + "_Misc") ?? commander.ToString();
                            break;
                        }
                    if (name != null) names.Add(name);
                    else CoreLog.Warning("CharacterSelectScreen: no commander for the mission portrait " + (sprite != null ? sprite.name : "null"));
                }
            }
            catch (Exception e) { CoreLog.Warning("CharacterSelectScreen: reading the mission's commanders failed: " + e.Message); }
            return names;
        }

        // The modifier line (RunModIconHolder, filled by SetDifficultyToIndex and SetChallenge): one
        // RunModIcon each, an image whose name and rules are only in its hover tooltip. A row under
        // "Modifiers", in the drawn order; each by its tooltip's title, Backspace the tooltip. The
        // replaced icons are destroyed at the frame's end: a build in that frame reads them too, the
        // next does not.
        private static void BuildModifiers(GraphBuilder b, CharacterSelectionController c)
        {
            var icons = new List<RunModIcon>();
            try
            {
                var holder = c.RunModIconHolder;
                if (holder == null || !holder.gameObject.activeInHierarchy) return;
                for (int i = 0; i < holder.childCount; i++)
                {
                    var icon = holder.GetChild(i).GetComponent<RunModIcon>();
                    if (icon != null && icon.gameObject.activeInHierarchy) icons.Add(icon);
                }
            }
            catch (Exception e) { CoreLog.Warning("CharacterSelectScreen: reading the modifier line failed: " + e.Message); return; }
            if (icons.Count == 0) return;
            b.PushContext(Strings.Get("cs.modifiers"));
            b.StartRow("cs:modifiers");
            for (int i = 0; i < icons.Count; i++) b.AddItem(ControlId.Structural("cs:modifier:" + i), ModifierNode(icons[i]));
            b.EndRow();
            b.PopContext();
        }

        // The chapter panel's line under the description: "New Mechanic: Locked Artifact Slots"
        // (NewModObject: its caption, the modifier's name and its icon, PanelRunModIcon), Backspace the
        // icon's tooltip; or, for a daily challenge, its "Daily Mods (Resets in 12h 27m)" and the day's
        // modifiers as a row (DailyRunModIcons shown: the hidden ones hold the prefab's placeholders).
        private static void BuildNewMechanic(GraphBuilder b, CharacterSelectionController c)
        {
            try
            {
                if (c.NewModObject != null && c.NewModObject.activeInHierarchy)
                {
                    var icon = c.PanelRunModIcon;
                    var parts = new List<TMPro.TMP_Text>();
                    foreach (var tmp in c.NewModObject.GetComponentsInChildren<TMPro.TMP_Text>(false))
                        if (icon == null || !tmp.transform.IsChildOf(icon.transform)) parts.Add(tmp);
                    Func<string> label = () =>
                    {
                        var words = new List<string>();
                        foreach (var tmp in parts)
                        {
                            string text = tmp != null && tmp.gameObject.activeInHierarchy ? GameText.Of(tmp) : null;
                            if (text != null) words.Add(text);
                        }
                        return string.Join(" ", words);
                    };
                    Func<IEnumerable<string>> details = () => icon != null ? (IEnumerable<string>)ModifierDetails(icon) : Array.Empty<string>();
                    b.AddItem(ControlId.Structural("cs:newmechanic"), new NodeVtable
                    {
                        ControlType = ControlTypes.Text,
                        Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label) },
                        SearchText = label,
                        Details = details,
                        OnSecondary = () => SayDetails(details()),
                    });
                }
                if (c.DailyRunModsObject != null && c.DailyRunModsObject.activeInHierarchy)
                {
                    var text = c.DailyRunModsText;
                    if (text != null && text.gameObject.activeInHierarchy)
                        b.AddItem(ControlId.Structural("cs:dailymods"), GameNodes.Text(() => GameText.Of(text)));
                    // The modifier line holds the day's modifiers too: those already read there are
                    // left out here.
                    var onLine = new HashSet<string>();
                    var holder = c.RunModIconHolder;
                    if (holder != null && holder.gameObject.activeInHierarchy)
                        for (int i = 0; i < holder.childCount; i++)
                        {
                            var shown = holder.GetChild(i).GetComponent<RunModIcon>();
                            string title = shown != null && shown.Tooltip != null ? GameText.Of(shown.Tooltip.TitleText) : null;
                            if (title != null) onLine.Add(title);
                        }
                    var icons = new List<RunModIcon>();
                    if (c.DailyRunModIcons != null)
                        foreach (var icon in c.DailyRunModIcons)
                        {
                            if (icon == null || !icon.gameObject.activeInHierarchy) continue;
                            string title = icon.Tooltip != null ? GameText.Of(icon.Tooltip.TitleText) : null;
                            if (title == null || !onLine.Contains(title)) icons.Add(icon);
                        }
                    if (icons.Count > 0)
                    {
                        b.StartRow("cs:dailymodicons");
                        for (int i = 0; i < icons.Count; i++) b.AddItem(ControlId.Structural("cs:dailymod:" + i), ModifierNode(icons[i]));
                        b.EndRow();
                    }
                }
            }
            catch (Exception e) { CoreLog.Warning("CharacterSelectScreen: reading the new mechanic failed: " + e.Message); }
        }

        private static NodeVtable ModifierNode(RunModIcon icon)
        {
            Func<string> label = () =>
            {
                try { return icon.Tooltip != null ? GameText.Of(icon.Tooltip.TitleText) : null; }
                catch (Exception e) { CoreLog.Warning("CharacterSelectScreen: reading a modifier failed: " + e.Message); return null; }
            };
            Func<IEnumerable<string>> details = () => ModifierDetails(icon);
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label) },
                SearchText = label,
                OnFocus = () => ViewNodes.Hover(icon),
                Details = details,
                OnSecondary = () => SayDetails(details()),
            };
        }

        private static List<string> ModifierDetails(RunModIcon icon)
        {
            try { return icon != null && icon.Tooltip != null ? Keywords.Tooltip(icon.Tooltip.gameObject) : new List<string>(); }
            catch (Exception e) { CoreLog.Warning("CharacterSelectScreen: reading a modifier's tooltip failed: " + e.Message); return new List<string>(); }
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
        // starting perk as the board reads it, Backspace its tooltip; the unique artifacts one after
        // another, in unlock order (LV.2 to LV.7): an unlocked one by name (Backspace its tooltip),
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
            // One list, Up and Down through it (rows of the drawn grid made it a grid to walk), in the
            // game's order: slot i unlocks at level i + 2 (LockedArtifactItemView.Initialize). The
            // drawing is two columns of three around the portrait, filled bottom up, left then right
            // (0 bottom left, 1 bottom right, ... 5 top right): read top first it went LV.6, LV.7,
            // LV.4, LV.5, LV.2, LV.3.
            for (int i = 0; i < (c.LockedArtifactItems?.Count ?? 0); i++)
            {
                var locked = c.LockedArtifactItems[i];
                var unlocked = c.UnlockedArtifactObjects != null && i < c.UnlockedArtifactObjects.Count ? c.UnlockedArtifactObjects[i] : null;
                var view = c.UnlockedArtifactViews != null && i < c.UnlockedArtifactViews.Count ? c.UnlockedArtifactViews[i] : null;
                if (locked != null && locked.gameObject.activeInHierarchy)
                {
                    taken.Add(locked.Pointer);
                    b.AddItem(ControlId.Structural("cs:unique:" + i), LockedNode(locked, unlocked, view));
                }
                else if (unlocked != null && unlocked.activeInHierarchy && view != null && Model(view) != null)
                    b.AddItem(ControlId.Structural("cs:unique:" + i), ArtifactNode(view, true));
            }
        }

        // The Overseer's frame (OverseerFrame, shown while the Overseer is chosen): the two commanders
        // whose starting perks it combines, each side three icons drawn top to bottom around that
        // side's perk (OverseerCommanderIcons: 0 to 2 the left, OverseerCommander1; 3 to 5 the right),
        // one chosen; a row each, as the commanders are read, then the chosen one's perk. Enter is the
        // game's click (OnPressOverseerIndex: the side's commander and perk change; no save written).
        private static void BuildOverseer(GraphBuilder b, CharacterSelectionController c, HashSet<IntPtr> taken)
        {
            var all = c.OverseerCommanderIcons;
            if (all == null) return;
            var perks = new[] { c.OverseerCommander1ArtifactView, c.OverseerCommander2ArtifactView };
            for (int side = 0; side < 2; side++)
            {
                var icons = new List<CommanderIconView>();
                foreach (var icon in all)
                    if (icon != null && icon.gameObject.activeInHierarchy && icon._index / 3 == side) icons.Add(icon);
                icons.Sort((l, r) => r.transform.position.y.CompareTo(l.transform.position.y));
                if (icons.Count > 0)
                {
                    b.StartRow("cs:overseer:" + side, entry: () => Math.Max(0, icons.FindIndex(i => Selected(i))));
                    foreach (var icon in icons)
                    {
                        taken.Add(icon.Pointer);
                        b.AddItem(ControlId.Structural("cs:overseer:" + side + ":" + icon.CommanderName), Commander(icon));
                    }
                    b.EndRow();
                }
                var perk = perks[side];
                if (perk != null && perk.gameObject.activeInHierarchy && Model(perk) != null)
                {
                    taken.Add(perk.Pointer);
                    b.AddItem(ControlId.Structural("cs:overseer:perk:" + side), ArtifactNode(perk, false));
                }
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

        // unlocked, view: the slot's revealed side, said once a ready unique's click reveals it.
        private static NodeVtable LockedNode(LockedArtifactItemView item, GameObject unlocked, ArtifactView view)
        {
            // By its condition (the slot draws a bare lock, its title only "Locked LV.6 Unique"):
            // the title for Backspace.
            Func<string> label = () => LockedText(item, false) ?? LockedText(item, true) ?? ViewNodes.Label(item);
            Func<IEnumerable<string>> details = () => GameNodes.Lines(LockedText(item, true));
            var vt = ViewNodes.Button(item, label);
            vt.Details = details;
            vt.OnSecondary = () => SayDetails(details());
            // A ready unique ("Press to unlock!") turns into its artifact under the same place: the
            // reveal is said, the slot being another kind of node by then.
            var click = vt.OnActivate;
            vt.OnActivate = () =>
            {
                click?.Invoke();
                if (unlocked != null && view != null) SayReveal(unlocked, view, 0);
            };
            return vt;
        }

        // Watches the slot for its artifact, two seconds at most (the reveal animates).
        private static void SayReveal(GameObject unlocked, ArtifactView view, int waited)
        {
            Later.Frames(6, () =>
            {
                if (unlocked != null && unlocked.activeInHierarchy && Model(view) != null)
                {
                    string label = EncounterScreen.ArtifactLabel(Model(view));
                    if (label != null) Speech.Say(label, interrupt: true);
                    return;
                }
                if (waited < 20) SayReveal(unlocked, view, waited + 1);
            }, "unique reveal");
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
