using System;
using System.Collections.Generic;
using StarVaders;
using StarVaders.PauseMenu;
using DiceVoders.Core;
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
            BuildDetails(b, menu);
        }

        // ---- the stats panel: the board's figures for a view ----

        private static readonly DetailType[] Views = { DetailType.AllUnitSTR, DetailType.AttackMultScoredLastTurn, DetailType.TriggeredLastTurn };

        // Its views as a row of radio buttons named by the game's titles, entered at the one shown;
        // choosing one is the game's SetCurrentDetail (the overlay switched on first, as the game's Tab
        // does, if it is off). Then each figure the overlay draws, tiles top row first, then the
        // artifacts: "3, 2, Migo, 810"; an artifact's Mult with its word ("Trail Pot, Mult 9.5").
        private static void BuildDetails(GraphBuilder b, PauseMenuController menu)
        {
            b.BeginStop("pause:details");
            b.PushContext(null);
            b.StartRow("pause:details", entry: () => Math.Max(0, Array.IndexOf(Views, menu.CurrentDetailType)));
            foreach (var view in Views)
            {
                var v = view;
                b.AddItem(ControlId.Structural("pause:view:" + v), new NodeVtable
                {
                    ControlType = ControlTypes.RadioButton,
                    Announcements = new List<NodeAnnouncement>
                    {
                        GameNodes.LabelPart(() => GameLoc.Get(v + "_Misc") ?? v.ToString()),
                        GameNodes.SelectedPart(() => menu.IsDetailsVisible && menu.CurrentDetailType == v),
                    },
                    SearchText = () => GameLoc.Get(v + "_Misc") ?? v.ToString(),
                    OnActivate = () => ShowDetail(menu, v),
                });
            }
            b.EndRow();
            if (menu.IsDetailsVisible)
            {
                int i = 0;
                foreach (var line in DetailLines(menu.CurrentDetailType))
                {
                    string text = line;
                    b.AddItem(ControlId.Structural("pause:detail:" + i++), GameNodes.Text(() => text));
                }
            }
            b.PopContext();
        }

        private static void ShowDetail(PauseMenuController menu, DetailType view)
        {
            try
            {
                if (!menu.IsDetailsVisible) menu.OnPressTab();
                menu.SetCurrentDetail(Array.IndexOf(Views, view));
            }
            catch (Exception e) { CoreLog.Warning("PauseScreen: showing " + view + " failed: " + e.Message); }
        }

        private static List<string> DetailLines(DetailType view)
        {
            var lines = new List<string>();
            try
            {
                for (int y = Encounter.Encounter.Rows - 1; y >= 0; y--)
                    for (int x = 0; x < Encounter.Encounter.Columns; x++)
                    {
                        var tile = Encounter.Encounter.Tile(x, y);
                        var unit = Encounter.Encounter.At(x, y);
                        if (tile == null || unit == null || tile.DetailObject == null || !tile.DetailObject.activeInHierarchy) continue;
                        var parts = new List<string> { Strings.EncCoord(x + 1, y + 1), Encounter.Encounter.Name(unit) };
                        Figure(parts, tile.AttackDetailText, null);
                        Figure(parts, tile.MultDetailText, MultWord());
                        Figure(parts, tile.ScoredDetailText, null);
                        if (parts.Count > 2) lines.Add(string.Join(", ", parts));
                    }
                var model = Encounter.Encounter.Model;
                int slots = model?.ArtifactSlots?.Count ?? 0;
                for (int slot = 0; slot < slots; slot++)
                {
                    var artifact = Encounter.Encounter.Artifact(slot);
                    var av = Encounter.Encounter.ArtifactView(artifact);
                    if (av == null || av.DetailObject == null || !av.DetailObject.activeInHierarchy) continue;
                    var parts = new List<string> { Encounter.Encounter.ArtifactName(artifact) };
                    Figure(parts, av.MultDetailText, MultWord());
                    Figure(parts, av.TriggerDetailText, null);
                    if (parts.Count > 1) lines.Add(string.Join(", ", parts));
                }
            }
            catch (Exception e) { CoreLog.Warning("PauseScreen: reading the " + view + " figures failed: " + e.Message); }
            return lines;
        }

        private static string MultWord() => GameLoc.Get("GlobalMult_Keyword") ?? "Mult";

        // A drawn figure, with its word where the view draws two kinds.
        private static void Figure(List<string> parts, TMPro.TMP_Text tmp, string word)
        {
            if (tmp == null || !tmp.gameObject.activeInHierarchy) return;
            string text = GameText.Of(tmp);
            if (text == null) return;
            parts.Add(word != null ? Strings.EncFigure(word, text) : text);
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
