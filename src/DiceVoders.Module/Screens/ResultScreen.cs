using System.Collections.Generic;
using StarVaders;
using DiceVoders.Core.Graph;
using DiceVoders.Core.Strings;
using DiceVoders.Module.Interop;
using DiceVoders.Module.UI;
using UnityEngine;
using Screen = DiceVoders.Core.Screens.Screen;

namespace DiceVoders.Module.Screens
{
    /// <summary>
    /// The encounter's result (<see cref="ResultPanel"/>), over the board: whichever of its end
    /// screens shows, a dialog named by its Title ("INVASION SUCCESSFUL", "SIMULATION FAILED",
    /// "GAME OVER", the one-turn KO's "SUCCESS!"), one stop: its other lines ("NEXT TARGET : 500",
    /// "LOSE 1 HEART : EXTRA TURN"), then its buttons, by caption or else the game's "Proceed".
    /// Focus lands on the first line. The panel's simulation (<see cref="ResultPanel.SimScreen"/>,
    /// the skipped turns played out) is no dialog: nothing to choose there.
    /// </summary>
    public sealed class ResultScreen : Screen
    {
        public override string Key => "result";
        public override int Layer => 30;
        public override bool Exclusive => true;
        public override bool AllowsTypeahead => false;

        private static GameObject Shown()
        {
            var panel = Encounter.Encounter.Controller?.ResultPanel;
            // Dismissed, the panel slides away with its end screen still switched on: its own flag says.
            if (panel == null || !panel.IsActive || !panel.gameObject.activeInHierarchy) return null;
            foreach (var screen in new[] { panel.GreenScreen, panel.RedScreen, panel.OTKScreen, panel.GameOverScreen })
                if (screen != null && screen.activeInHierarchy) return screen;
            return null;
        }

        public override bool IsActive() => Shown() != null;

        public override void Build(GraphBuilder b)
        {
            var screen = Shown();
            if (screen == null) return;
            var title = Title(screen.transform);
            b.PushContext(GameText.Of(title), Strings.RoleDialog, positions: false);
            var lines = ViewNodes.Prose(screen.transform, title);
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                b.AddItem(ControlId.Structural("result:line:" + i), GameNodes.Text(() => line));
            }
            foreach (var item in screen.GetComponentsInChildren<AViewItem>(false))
                if (item.TryCast<IClickable>() != null)
                    b.AddItem(ControlId.Structural("result:" + item.gameObject.name), ViewNodes.Button(item, () => ButtonLabel(item)));
            b.PopContext();
        }

        // A caption, else the game's "Proceed" (the success screen's button draws only its key prompt;
        // Label then falls back to the object's name).
        private static string ButtonLabel(AViewItem item)
        {
            string label = ViewNodes.Label(item);
            return label != null && label != item.gameObject.name ? label : GameLoc.Get("Proceed_Misc") ?? "Proceed";
        }

        // The shown screen's heading: its first visible text named "Title".
        private static TMPro.TMP_Text Title(Transform screen)
        {
            foreach (var tmp in screen.GetComponentsInChildren<TMPro.TMP_Text>(false))
                if (tmp != null && tmp.gameObject.name == "Title" && !string.IsNullOrWhiteSpace(tmp.text)) return tmp;
            return null;
        }
    }
}
