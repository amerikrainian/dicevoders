using System;
using System.Collections.Generic;
using StarVaders;
using DiceVoders.Core;
using DiceVoders.Core.Graph;
using DiceVoders.Core.Strings;
using DiceVoders.Module.Interop;
using DiceVoders.Module.UI;
using UnityEngine;
using Screen = DiceVoders.Core.Screens.Screen;

namespace DiceVoders.Module.Screens
{
    /// <summary>
    /// The run won (<see cref="VictoryPanel"/>, over the encounter): a dialog named by the shown
    /// ending's title ("True End...?"; the panel's normal, true and ultimate ending objects are its
    /// title's variants, one shown), one
    /// stop: the pilots defeated (drawn only as portraits: the run's <see cref="EncounterModel.DefeatedPilots"/>
    /// by name), the best turn ("Best Turn: 40, Mult 1"), then its buttons (Endless Mode, Exit to
    /// Menu, Hide). Hide puts the panel away over the board; it is up while <c>isShowing</c>.
    /// </summary>
    public sealed class VictoryScreen : Screen
    {
        public override string Key => "victory";
        public override int Layer => 31;
        public override bool Exclusive => true;
        public override bool AllowsTypeahead => false;

        private static GameObject Shown()
        {
            var panel = Encounter.Encounter.Controller?.VictoryPanel;
            if (panel == null || !panel.isShowing || !panel.gameObject.activeInHierarchy) return null;
            foreach (var ending in new[] { panel.UltimateEndingObject, panel.TrueEndingObject, panel.NormalEndingObject })
                if (ending != null && ending.activeInHierarchy) return ending;
            return null;
        }

        public override bool IsActive() => Shown() != null;

        public override void Build(GraphBuilder b)
        {
            var ending = Shown();
            var panel = Encounter.Encounter.Controller?.VictoryPanel;
            if (ending == null || panel == null) return;
            b.PushContext(Title(ending.transform), Strings.RoleDialog, positions: false);
            int i = 0;
            foreach (var line in Lines(panel, panel.transform))
            {
                string text = line;
                b.AddItem(ControlId.Structural("victory:line:" + i++), GameNodes.Text(() => text));
            }
            foreach (var item in panel.GetComponentsInChildren<AViewItem>(false))
                if (item.TryCast<IClickable>() != null && item.TryCast<ButtonView>() != null)
                    b.AddItem(ControlId.Structural("victory:" + item.Pointer), ViewNodes.Button(item, () => ViewNodes.Label(item)));
            b.PopContext();
        }

        // The ending's heading: the shown title variant's text ("True End...?").
        private static string Title(Transform ending)
        {
            foreach (var tmp in ending.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                string text = GameText.Of(tmp);
                if (text != null) return text;
            }
            return null;
        }

        private static List<string> Lines(VictoryPanel panel, Transform root)
        {
            var lines = new List<string>();
            try
            {
                Transform details = null;
                foreach (var t in root.GetComponentsInChildren<Transform>(false))
                    if (t.name == "DetailsPanel") { details = t; break; }
                // "Pilots Defeated: Roxy, Shun": the label, the portraits by the run's record.
                var pilotsLabel = details != null ? details.Find("PilotsDefeated")?.GetComponent<TMPro.TMP_Text>() : null;
                var defeated = new List<string>();
                var model = Encounter.Encounter.Model;
                if (model?.DefeatedPilots != null)
                    foreach (var pilot in model.DefeatedPilots) defeated.Add(pilot.ToString());
                string label = GameText.Of(pilotsLabel);
                if (label != null && defeated.Count > 0) lines.Add(label + " " + string.Join(", ", defeated));
                // "Best Turn: 40, Mult 1": the label, its score and its Mult ("X 1" as the status's Mult).
                var best = details != null ? details.Find("BestTurn:") : null;
                if (best != null && best.gameObject.activeInHierarchy)
                {
                    string bestLabel = GameText.Of(best.GetComponent<TMPro.TMP_Text>());
                    string score = GameText.Of(panel.BestTurnScoreText);
                    string mult = Encounter.Encounter.WholeMult(GameText.Of(panel.BestTurnMultText)?.TrimStart('X', 'x').Trim());
                    var parts = new List<string>();
                    if (score != null) parts.Add(bestLabel != null ? bestLabel + " " + score : score);
                    if (mult != null) parts.Add(Strings.EncFigure(GameLoc.Get("GlobalMult_Keyword") ?? "Mult", mult));
                    if (parts.Count > 0) lines.Add(string.Join(", ", parts));
                }
            }
            catch (Exception e) { CoreLog.Warning("VictoryScreen: reading the details failed: " + e.Message); }
            return lines;
        }
    }
}
