using System;
using System.Collections.Generic;
using BreakInfinity;
using StarVaders;
using DiceVoders.Core;
using DiceVoders.Core.Encounter;
using DiceVoders.Module.Interop;
using DiceVoders.Module.UI;
using HarmonyLib;

namespace DiceVoders.Module.Encounter
{
    /// <summary>
    /// The encounter's narration, as starvaders' fight's: no patches, a plain snapshot of the model
    /// on each settled frame, compared with the last (<see cref="EncounterDiff"/>), each line queued
    /// (never interrupting). Settled: the game waits on the player (a tip that waits for a click), or
    /// the task engine has been idle with no die rolling for <see cref="SettleFrames"/> frames.
    /// Ticked by the module before the screens, so an event is heard before the focus landing it
    /// causes. A new controller (a new encounter) is a fresh baseline, said nothing of.
    /// </summary>
    internal static class EncounterEvents
    {
        private const int SettleFrames = 3;

        private static IntPtr _controller;
        private static EncounterSnapshot _last;
        private static int _idle;

        /// <summary>Whether this frame is settled (see the class).</summary>
        public static bool Settled { get; private set; }

        public static void Tick()
        {
            Settled = false;
            var c = Encounter.Controller;
            if (c == null) { _controller = IntPtr.Zero; _last = null; _idle = 0; return; }
            if (c.Pointer != _controller) { _controller = c.Pointer; _last = null; _idle = 0; }

            var tip = Tips.Current;
            bool waiting = tip != null && Tips.WaitsForClick(tip);
            if (!waiting && (Encounter.Busy || Rolling())) { _idle = 0; return; }
            if (!waiting && ++_idle < SettleFrames) return;
            Settled = true;

            var now = Take();
            if (now == null) return;
            _miniDice.Clear();
            if (_last != null)
            {
                var lines = EncounterDiff.Lines(_last, now);
                if (lines.Count > 0) LogRaw();
                foreach (var line in lines) Speech.Say(line);
            }
            _last = now;
        }

        // The mini-dice launched since the last snapshot: their faces are the columns they go to,
        // caught as the game sends each (they roll and land mid-scoring, never on a settled frame).
        private static readonly List<int> _miniDice = new List<int>();

        [HarmonyPatch(typeof(MiniDiceSpawner), nameof(MiniDiceSpawner.LaunchDiceToColumn))]
        internal static class MiniDiceLaunch
        {
            [HarmonyPostfix]
            private static void Postfix(GridColumn gridColumn)
            {
                try
                {
                    if (gridColumn == null) return;
                    for (int x = 0; x < Encounter.Columns; x++)
                    {
                        var column = Encounter.Column(x);
                        if (column != null && column.Pointer == gridColumn.Pointer) { _miniDice.Add(x + 1); return; }
                    }
                    CoreLog.Warning("EncounterEvents: a mini-die went to a column not on the grid");
                }
                catch (Exception e) { CoreLog.Warning("EncounterEvents: reading a mini-die failed: " + e.Message); }
            }
        }

        /// <summary>Take the encounter as it is now as the baseline, so what a key has just said
        /// itself (a budge's new face) is not said again.</summary>
        public static void Rebase()
        {
            var now = Take();
            if (now != null) _last = now;
        }

        private static bool Rolling()
        {
            foreach (var die in Encounter.Dice())
                if (die.isRolling) return true;
            return false;
        }

        private static EncounterSnapshot Take()
        {
            var model = Encounter.Model;
            if (model == null) return null;
            try
            {
                var s = new EncounterSnapshot
                {
                    Phase = Encounter.Phase.ToString(),
                    TurnsLeft = Encounter.Int(EncounterValue.TurnsRemaining) ?? 0,
                    TurnsText = Screens.EncounterScreen.Turns(),
                    Hearts = Encounter.Int(EncounterValue.Hearts) ?? 0,
                    Total = model.GetBigDoubleValue(EncounterValue.TotalScore).ToDouble(),
                    Target = model.GetBigDoubleValue(EncounterValue.TargetTributeScore).ToDouble(),
                    TotalText = Encounter.Big(EncounterValue.TotalScore),
                    TargetText = Encounter.Big(EncounterValue.TargetTributeScore),
                    TargetReachedText = GameLoc.Get("TargetReached_Misc"),
                    TargetName = Screens.EncounterScreen.Target(),
                    Act = GameText.Of(Encounter.View?.MapPanel?.actText),
                    Mult = model.GetBigDoubleValue(EncounterValue.GlobalMult).ToDouble(),
                    MultText = Encounter.Big(EncounterValue.GlobalMult),
                    MultLabel = GameLoc.Get("GlobalMult_Keyword") ?? "Mult",
                    CloudLabel = GameLoc.Get("Cloudy_TooltipTitle") ?? "Cloudy",
                };
                foreach (var die in Encounter.Dice()) s.Dice.Add(die.isRolling ? 0 : die.Value);
                s.MiniDice.AddRange(_miniDice);
                s.MiniDiceLabel = GameLoc.Get("MiniDice_Keyword") ?? "Mini-Dice";
                ColumnAttack(model, s.ColumnAttack);
                Tokens(s.Tokens);
                Units(model, s.Units);
                for (int slot = 0; slot < (model.ArtifactSlots?.Count ?? 0); slot++)
                {
                    var artifact = Encounter.Artifact(slot);
                    if (artifact != null)
                        s.Artifacts.Add(new ArtifactSnap { Id = artifact.ArtifactID.Number, Name = Encounter.ArtifactName(artifact), Slot = slot, Level = artifact.Level });
                }
                for (int x = 0; x < Encounter.Columns; x++)
                {
                    if (Encounter.Honey(x)) s.Honey.Add(x);
                    for (int y = 0; y < Encounter.Rows; y++)
                    {
                        if (!Encounter.Built(x, y)) continue;
                        s.Built.Add((x, y));
                        if (Encounter.Cloud(x, y)) s.Clouds.Add((x, y));
                    }
                }
                if (Encounter.Phase == Phase.Draft)
                    foreach (var choice in Encounter.Choices()) s.Shop.Add(Screens.EncounterScreen.ChoiceName(choice));
                return s;
            }
            catch (Exception e)
            {
                CoreLog.Warning("EncounterEvents: taking the snapshot failed: " + e.Message);
                return null;
            }
        }

        private static void Tokens(List<Figure> into)
        {
            void Add(string key, string label, EncounterValue value)
            {
                int? n = Encounter.Int(value);
                if (n.HasValue) into.Add(new Figure { Key = key, Label = label, Value = n.Value });
            }
            Add("budge", GameLoc.Get("Budge_Keyword") ?? "Budge", EncounterValue.BudgeToken);
            Add("chrono", GameLoc.Get("Chrono_Keyword") ?? "Chrono", EncounterValue.ChronoToken);
            Add("swap", Core.Strings.Strings.EncSwap, EncounterValue.SwapToken);
            Add("minidice", GameLoc.Get("MiniDice_Keyword") ?? "Mini-Dice", EncounterValue.MiniDice);
            Add("cash", GameLoc.Get("ShopToken_TooltipTitle") ?? "Cash", EncounterValue.ShopToken);
        }

        // The units on the grid, by the game's id (name and number: stable while the unit lives).
        private static void Units(EncounterModel model, List<UnitSnap> into)
        {
            foreach (var kv in model.ModelItemDict)
            {
                var unit = kv.Value?.TryCast<EntityModel>();
                if (unit == null) continue;
                var id = unit.EntityID;
                if (!model.GridModel.IsEntityOnGrid(id)) continue;
                var c = model.GridModel.GetEntityCoord(id, false);
                var strength = unit.GetCurrentValue(model);
                into.Add(new UnitSnap
                {
                    Id = id.EntityName + "#" + id.Number,
                    Name = Encounter.Name(unit),
                    X = c.x,
                    Y = c.y,
                    Strength = strength.ToDouble(),
                    StrengthText = strength.ToString(),
                    States = Screens.EncounterScreen.UnitStates(unit),
                    Charge = unit.Charge,
                    MaxCharge = unit.MaxCharge,
                });
            }
        }

        // What each column's units gained this turn, summed: AttackGainedThisTurn while the scoring
        // runs, AttackGainedLastTurn once it is over (the turn's figures move there as the shop opens;
        // the model's AtkPerCol reads all 0 by then).
        private static void ColumnAttack(EncounterModel model, List<string> into)
        {
            int columns = Encounter.Columns;
            var sums = new BigDouble[columns];
            var any = new bool[columns];
            bool scoring = Encounter.Phase == Phase.Scoring;
            foreach (var kv in model.ModelItemDict)
            {
                var unit = kv.Value?.TryCast<EntityModel>();
                if (unit == null || !model.GridModel.IsEntityOnGrid(unit.EntityID)) continue;
                int x = model.GridModel.GetEntityCoord(unit.EntityID, false).x;
                if (x < 0 || x >= columns) continue;
                var gained = scoring ? unit.GetAttackGainedThisTurn() : unit.GetAttackGainedLastTurn();
                if (gained.ToDouble() <= 0) continue;
                sums[x] = any[x] ? sums[x] + gained : gained;
                any[x] = true;
            }
            for (int x = 0; x < columns; x++) into.Add(any[x] ? sums[x].ToString() : "0");
        }

        // The model's turn figures beside what was said, while the scoring summary's sources are
        // being checked against the game.
        private static void LogRaw()
        {
            var model = Encounter.Model;
            if (model == null) return;
            try
            {
                var cols = new List<string>();
                if (model.AtkPerCol != null) foreach (var a in model.AtkPerCol) cols.Add(a.ToString());
                var mults = new List<string>();
                if (model.MultPerCol != null) foreach (var m in model.MultPerCol) mults.Add(m.ToString());
                CoreLog.Info("EncounterEvents: phase " + Encounter.Phase + ", CurrentScore " + Encounter.Big(EncounterValue.CurrentScore)
                    + ", TotalScore " + Encounter.Big(EncounterValue.TotalScore) + ", BestTurnScore " + model.BestTurnScore.ToString()
                    + ", GlobalMult " + Encounter.Big(EncounterValue.GlobalMult) + ", AtkPerCol [" + string.Join(" ", cols)
                    + "], MultPerCol [" + string.Join(" ", mults) + "]");
            }
            catch (Exception e) { CoreLog.Warning("EncounterEvents: logging the turn figures failed: " + e.Message); }
        }
    }
}
