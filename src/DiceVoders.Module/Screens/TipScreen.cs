using System;
using System.Collections.Generic;
using DiceVoders.Core;
using DiceVoders.Core.Graph;
using DiceVoders.Core.Input;
using DiceVoders.Core.UI;
using DiceVoders.Module.UI;
using Screen = DiceVoders.Core.Screens.Screen;

namespace DiceVoders.Module.Screens
{
    /// <summary>
    /// A tutorial tip that waits for a click ("[click] to continue"), as a modal over whatever scene
    /// it shows in: its text is the one node, Enter continues as the game's click does. Tips that do
    /// not wait are the encounter's to read. Once up for a tip, the screen stays up while that tip
    /// shows (by native pointer), whatever its continue line does meanwhile: Tip10 was heard four
    /// times over, most likely the screen dropping and coming back as the tip animated, each return
    /// a fresh landing. Each time it comes up is logged.
    /// </summary>
    public sealed class TipScreen : Screen
    {
        public override string Key => "tip";
        public override int Layer => 40;
        public override bool Exclusive => true;
        public override IReadOnlyList<InputCategory> InputCategories => RunKeys.Categories;
        public override bool AllowsTypeahead => false;

        private IntPtr _tip;

        public override bool IsActive()
        {
            var tip = Tips.Current;
            if (tip == null) { _tip = IntPtr.Zero; return false; }
            if (tip.Pointer == _tip) return Tips.Text(tip) != null;
            if (!Tips.WaitsForClick(tip) || Tips.Text(tip) == null) return false;
            _tip = tip.Pointer;
            CoreLog.Info("TipScreen: up for " + tip.name);
            return true;
        }

        public override void Build(GraphBuilder b)
        {
            var tip = Tips.Current;
            if (tip == null) return;
            b.AddItem(ControlId.Structural("tip:" + tip.GetInstanceID()), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => Tips.Text(tip)) },
                SearchText = () => Tips.Text(tip),
                OnActivate = () => Tips.Continue(tip),
            });
        }
    }
}
