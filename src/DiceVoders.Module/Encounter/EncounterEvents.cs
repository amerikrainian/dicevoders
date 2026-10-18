using System;
using System.Collections.Generic;
using BreakInfinity;
using StarVaders;
using DiceVoders.Core;
using DiceVoders.Core.Encounter;
using DiceVoders.Module.Interop;
using DiceVoders.Module.UI;

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
            if (_last != null)
            {
                var lines = EncounterDiff.Lines(_last, now);
                if (lines.Count > 0) LogRaw();
                foreach (var line in lines) Speech.Say(line);
            }
            _last = now;
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
                };
                foreach (var die in Encounter.Dice()) s.Dice.Add(die.isRolling ? 0 : die.Value);
                ColumnAttack(model, s.ColumnAttack);
                return s;
            }
            catch (Exception e)
            {
                CoreLog.Warning("EncounterEvents: taking the snapshot failed: " + e.Message);
                return null;
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
