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
    /// The rewards after a pilot (<see cref="SpecialRewardPanel"/>, the encounter's bottom panel in
    /// <c>Phase.LegendaryDraft</c>), over the board: a dialog named by its title ("Level Up!"), one
    /// stop of whichever of its screens shows, as drawn: the starter perk levelled
    /// (<c>LevelUpScreen</c>: the perk, then Proceed, <see cref="SpecialRewardPanel.OnPressLevelUpProceed"/>),
    /// the pilot's loot (<c>PilotDraftScreen</c>, "You Receive": Budge, Chrono and Heart, all received
    /// by Proceed, <see cref="SpecialRewardPanel.OnPressReceiveAllButton"/>, so they read as text;
    /// under the Scarcity run mod each is a button that receives it alone), the legendary
    /// draft (<c>LegendaryDraftScreen</c>: three artifacts, skip for money, proceed), the overseer's
    /// choice (<c>OverseerLevelupScreen</c>). An artifact reads as on the board ("The Tutorial, LV.
    /// 2"), Backspace its tooltip; a button by its hover tooltip's title, Backspace the tooltip; one
    /// with neither, the game's "Proceed". Every button is the game's click. Only the level-up seen
    /// live so far: the others are read from their prefabs.
    /// </summary>
    public sealed class SpecialRewardScreen : Screen
    {
        public override string Key => "specialreward";
        public override int Layer => 31;
        public override bool Exclusive => true;
        public override bool AllowsTypeahead => false;

        private static SpecialRewardPanel Panel()
        {
            var panel = Encounter.Encounter.Controller?.specialRewardPanel;
            return panel != null && panel.IsActive && panel.gameObject.activeInHierarchy ? panel : null;
        }

        private static GameObject Shown(SpecialRewardPanel panel)
        {
            if (panel == null) return null;
            foreach (var screen in new[] { panel.LevelUpScreen, panel.PilotDraftScreen, panel.LegendaryDraftScreen, panel.OverseerLevelupScreen })
                if (screen != null && screen.activeInHierarchy) return screen;
            return null;
        }

        public override bool IsActive() => Shown(Panel()) != null;

        public override void Build(GraphBuilder b)
        {
            var panel = Panel();
            var screen = Shown(panel);
            if (screen == null) return;
            b.PushContext(GameText.Of(panel.TitleText), Strings.RoleDialog, positions: false);
            bool loot = screen.Pointer == panel.PilotDraftScreen.Pointer;
            // The pilot's loot is all received by Proceed; its rewards take a click only under
            // Scarcity (OnPressReceive* return at once otherwise), each then received alone.
            bool pickOne = loot && Scarcity();
            var nodes = new List<NodeVtable>();
            var last = new List<NodeVtable>();
            foreach (var item in screen.GetComponentsInChildren<AViewItem>(false))
            {
                var button = item.TryCast<ButtonView>();
                bool reward = loot && button != null && button.Tooltip != null;
                var node = reward && !pickOne ? Reward(button) : Node(item, screen.transform);
                if (node == null) continue;
                // The buttons with nothing to say for themselves (Proceed) come after what they take.
                if (button != null && !reward && Keywords.Tooltip(button.Tooltip).Count == 0 && button.GetComponentInChildren<ArtifactView>(false) == null) last.Add(node);
                else nodes.Add(node);
            }
            nodes.AddRange(last);
            for (int i = 0; i < nodes.Count; i++)
                b.AddItem(ControlId.Structural("specialreward:" + screen.name + ":" + i), nodes[i]);
            b.PopContext();
        }

        private static bool Scarcity()
        {
            try { return Encounter.Encounter.Model?.HasRunMod(RunModName.Scarcity) == true; }
            catch (Exception e) { CoreLog.Warning("SpecialRewardScreen: reading the run mods failed: " + e.Message); return false; }
        }

        // A reward shown, not chosen: its tooltip's title as text, Backspace the tooltip.
        private static NodeVtable Reward(ButtonView button)
        {
            Func<string> label = () => ButtonLabel(button);
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label) },
                SearchText = label,
                OnFocus = () => ViewNodes.Hover(button),
                Details = () => Keywords.Tooltip(button.Tooltip),
                OnSecondary = () => SayDetails(Keywords.Tooltip(button.Tooltip)),
            };
        }

        // A button (with the artifact it shows, if any), or an artifact outside any button; null for
        // the rest (an artifact inside a button is that button's).
        private static NodeVtable Node(AViewItem item, Transform screen)
        {
            var artifactView = item.TryCast<ArtifactView>();
            if (artifactView != null)
            {
                if (InButton(item.transform, screen)) return null;
                return Artifact(artifactView, null);
            }
            var button = item.TryCast<ButtonView>();
            if (button == null) return null;
            var shown = button.GetComponentInChildren<ArtifactView>(false);
            if (shown != null && Model(shown) != null) return Artifact(shown, button);
            Func<string> label = () => ButtonLabel(button);
            var vt = ViewNodes.Button(button, label);
            vt.Details = () => Keywords.Tooltip(button.Tooltip);
            vt.OnSecondary = () => SayDetails(Keywords.Tooltip(button.Tooltip));
            return vt;
        }

        private static ArtifactModel Model(ArtifactView view)
        {
            var model = view.ArtifactModel;
            return model != null && model.ArtifactName != ArtifactName.None ? model : null;
        }

        private static bool InButton(Transform t, Transform screen)
        {
            for (var p = t.parent; p != null && p != screen; p = p.parent)
                if (p.GetComponent<ButtonView>() != null) return true;
            return false;
        }

        // An artifact as the board reads it; Enter is its button's click where it sits in one.
        private static NodeVtable Artifact(ArtifactView view, ButtonView button)
        {
            Func<string> label = () => EncounterScreen.ArtifactLabel(Model(view));
            Func<IEnumerable<string>> details = () => EncounterScreen.ArtifactDetails(Model(view), view);
            return new NodeVtable
            {
                ControlType = button != null ? ControlTypes.Button : ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label) },
                SearchText = label,
                OnFocus = () => ViewNodes.Hover((AViewItem)button ?? view),
                OnActivate = () => { if (button != null) ViewNodes.Click(button); },
                Details = details,
                OnSecondary = () => SayDetails(details()),
            };
        }

        // The button's hover tooltip's title, else its caption, else the game's "Proceed" (the
        // proceed buttons draw only their key prompt).
        private static string ButtonLabel(ButtonView button)
        {
            var lines = Keywords.Tooltip(button.Tooltip);
            if (lines.Count > 0)
            {
                string first = lines[0];
                int colon = first.IndexOf(": ", StringComparison.Ordinal);
                return colon > 0 ? first.Substring(0, colon) : first;
            }
            string label = ViewNodes.Label(button);
            return label != null && label != button.gameObject.name && label.Length > 1 ? label : GameLoc.Get("Proceed_Misc") ?? "Proceed";
        }

        private static void SayDetails(IEnumerable<string> lines)
        {
            var list = new List<string>(lines ?? Array.Empty<string>());
            Speech.Say(list.Count > 0 ? string.Join(" ", list) : Strings.NoDetails, interrupt: true);
        }
    }
}
