using System.Collections.Generic;
using StarVaders;
using StarVaders.PauseMenu;
using DiceVoders.Core.Graph;
using DiceVoders.Core.Strings;
using DiceVoders.Core.UI;
using DiceVoders.Module.Encounter;
using DiceVoders.Module.Interop;
using DiceVoders.Module.UI;
using Screen = DiceVoders.Core.Screens.Screen;

namespace DiceVoders.Module.Screens
{
    /// <summary>
    /// The encounter's pause menu (<see cref="PauseMenuController"/>, in the encounter, not a scene of
    /// its own: <see cref="EncounterController.PausePanel"/>), opened by Escape on the encounter (the
    /// navigator's back fallback, the game's Menu key). Named by the game's "Game Menu". One stop, as
    /// drawn: Resume, Options, Abandon Run, Save and Exit, Rewind Turn, the game speeds as a row of
    /// radio buttons ("speed 2", selected by <see cref="PauseMenuController.SelectedSpeedObjects"/>: the
    /// arrows draw no caption), Strategy Guide. Each is the game's click; focus lands on Resume. Escape resumes, as the Menu key does. Not
    /// read: the stats panel's per-unit overlays (what each unit scored last turn), drawn over the board.
    /// </summary>
    public sealed class PauseScreen : Screen
    {
        public override string Key => "pause";
        public override int Layer => 33;
        public override bool Exclusive => true;
        public override bool AllowsTypeahead => false;

        private static PauseMenuController Menu()
        {
            var c = Encounter.Encounter.Controller;
            var menu = c?.PauseMenuController;
            return menu != null && c.PausePanel != null && c.PausePanel.activeInHierarchy && menu.IsPaused ? menu : null;
        }

        /// <summary>Whether the pause menu is up.</summary>
        public static bool Open => Menu() != null;

        public override bool IsActive() => Menu() != null;

        public override string ScreenName => GameLoc.Get("PauseTitle_Misc");

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => EncounterKeys.Menu());
        }

        private static readonly string[] Buttons = { "ResumeButton", "Options", "AbandonRun", "SaveAndExit", "RestartTurn" };
        private static readonly string[] Speeds = { "PlayArrow", "Speed2Arrow", "Speed3Arrow", "Speed4Arrow" };

        public override void Build(GraphBuilder b)
        {
            var menu = Menu();
            if (menu == null) return;
            var frame = menu.transform.Find("BottomPanel/Mover/Frame");
            if (frame == null) return;
            b.BeginStop("pause:menu");
            b.PushContext(null);
            foreach (var name in Buttons)
            {
                var item = Item(frame, name);
                if (!ViewNodes.IsShown(item)) continue;
                var vt = ViewNodes.Button(item);
                // Landing is on Resume: a stop lands on its first node that reads selected, and a speed does.
                if (name == "ResumeButton") vt.IsSelected = () => true;
                b.AddItem(ControlId.Structural("pause:" + name), vt);
            }
            // The speeds, a row where the game draws them, between Rewind Turn and Strategy Guide;
            // coming into it lands on the speed in use.
            var shown = new List<int>();
            for (int i = 0; i < Speeds.Length; i++)
                if (ViewNodes.IsShown(Item(frame, "SpeedGroup/" + Speeds[i]))) shown.Add(i);
            b.StartRow("pause:speed", entry: () => System.Math.Max(0, shown.FindIndex(i => SpeedSelected(menu, i))));
            foreach (int i in shown)
            {
                var item = Item(frame, "SpeedGroup/" + Speeds[i]);
                int speed = i;
                b.AddItem(ControlId.Structural("pause:speed" + i), ViewNodes.Radio(item, () => SpeedSelected(menu, speed), () => Strings.EncSpeed(speed + 1)));
            }
            b.EndRow();
            var guide = Item(frame, "StrategyGuideButton");
            if (ViewNodes.IsShown(guide)) b.AddItem(ControlId.Structural("pause:guide"), ViewNodes.Button(guide));
            b.PopContext();
        }

        private static AViewItem Item(UnityEngine.Transform frame, string path)
        {
            var t = frame.Find(path);
            return t != null ? t.GetComponent<AViewItem>() : null;
        }

        private static bool SpeedSelected(PauseMenuController menu, int speed)
        {
            var selected = menu.SelectedSpeedObjects;
            return selected != null && speed < selected.Count && selected[speed] != null && selected[speed].activeSelf;
        }
    }
}
