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
    /// the tutorial's panel up (no commander unlocked, no Start): the rest is generic until read.
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

            // The chapter: the chapters as a row ("CH1 - First Contact", locked, completed), entered
            // at the one chosen; the chosen one's description; Select Challenge. The panel's own
            // headings repeat the chosen chapter's name and are left out; so are its arrows, the
            // row's neighbours.
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
            foreach (var item in items)
            {
                if (!ViewNodes.IsShown(item)) continue;
                if (item.gameObject.name == "DifficultyDropdownButton")
                {
                    taken.Add(item.Pointer);
                    b.AddItem(ControlId.Structural("cs:challenge"), ViewNodes.Button(item));
                }
                else if (item.transform.parent != null && item.transform.parent.name == "Title" && item.TryCast<ButtonView>() != null)
                    taken.Add(item.Pointer);
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
                b.EndRow();
            }

            // Whatever else is shown and clickable, Back last.
            AViewItem back = null;
            foreach (var item in items)
            {
                if (!ViewNodes.IsShown(item) || item.TryCast<IClickable>() == null || !taken.Add(item.Pointer)) continue;
                if (item.gameObject.name == "BackButton") { back = item; continue; }
                b.AddItem(ControlId.Structural("cs:" + item.gameObject.name + ":" + item.GetInstanceID()), ViewNodes.Button(item));
            }
            if (back != null)
                b.AddItem(ControlId.Structural("cs:back"), ViewNodes.Button(back, () => GameLoc.Get("Back_Misc") ?? "Back"));
            b.PopContext();
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
        private static NodeVtable Commander(CommanderIconView icon)
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
        private static string CommanderLabel(CommanderIconView icon)
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

        private static IEnumerable<string> CommanderDetails(CommanderIconView icon)
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
