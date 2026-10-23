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
    /// The encounter's narration, as starvaders' fight's: a plain snapshot of the model on each
    /// settled frame, compared with the last (<see cref="EncounterDiff"/>), each line queued (never
    /// interrupting). Settled: the game waits on the player (a tip that waits for a click), or the
    /// task engine has been idle with no die rolling for <see cref="SettleFrames"/> frames. The
    /// scoring, which never settles, adds a snapshot as each column starts and ends (<see cref="ColumnSteps"/>).
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
            if (c.Pointer != _controller) { _controller = c.Pointer; _last = null; _idle = 0; _column = -1; }
            TurnBoard();

            var tip = Tips.Current;
            bool waiting = tip != null && Tips.WaitsForClick(tip);
            if (!waiting && (Encounter.Busy || Rolling())) { _idle = 0; return; }
            if (!waiting && ++_idle < SettleFrames) return;
            Settled = true;

            var now = Take();
            if (now == null) return;
            if (_last != null) Say(EncounterDiff.Lines(_last, now));
            _last = now;
        }

        // The scoring board under the grid (EncounterView.CurrentScoreBoard) sums the turn: its attack
        // counting up, its Mult beside it ("X 2.0") once there is any, then the two combined
        // (CombineAndScore: CurrentScore times GlobalMult, written to CenterTotalScoreText, then shown,
        // then counted down into the score panel). Said once per scoring as the total shows, and only
        // with a Mult: without one the total is the columns' attack, already said.
        private static bool _turnSaid;

        private static void TurnBoard()
        {
            if (Encounter.Phase != Phase.Scoring) { _turnSaid = false; return; }
            if (_turnSaid) return;
            try
            {
                var board = Encounter.View?.CurrentScoreBoard;
                var total = board?.CenterTotalScoreText;
                if (total == null || !total.gameObject.activeInHierarchy) return;
                _turnSaid = true;
                // Read whether shown or not: the attack and Mult are hidden as the total shows.
                // "X 2.0" reads as "2", as the status's Mult.
                string mult = Encounter.WholeMult(GameText.Of(board.CurrentMultText)?.TrimStart('X', 'x').Trim());
                string attack = GameText.Of(board.CurrentAttackScoreText);
                string sum = GameText.Of(total);
                if (string.IsNullOrEmpty(mult) || attack == null || sum == null) return;
                string label = GameLoc.Get("GlobalMult_Keyword") ?? "Mult";
                Say(new List<string> { Core.Strings.Strings.EncTurnScore(attack, Core.Strings.Strings.EncFigure(label, mult), sum) });
            }
            catch (Exception e) { CoreLog.Warning("EncounterEvents: reading the scoring board failed: " + e.Message); }
        }

        private static void Say(List<string> lines)
        {
            if (lines.Count > 0) LogRaw();
            foreach (var line in lines) Speech.Say(line);
        }

        // The turn's scoring is one busy stretch (the dice launched, each column scored in turn, the
        // end of the turn) that settles only as the shop opens. It is said column by column instead:
        // ScoreDiceTask scores the columns holding dice in ascending order, each a ScoreColumnTask
        // (the mini-dice through a second ScoreDiceTask), whose coroutine is hooked. As a column
        // starts, what came before it is said; as it ends, the column with the attack it gained and
        // what its units set off. The end of the turn is then the shop's settled snapshot.
        private static int _column = -1, _columnDice;
        private static bool _columnMini;
        private static BigDouble[] _stepStart;

        [HarmonyPatch(typeof(ScoreColumnTask._Execute_d__1), nameof(ScoreColumnTask._Execute_d__1.MoveNext))]
        internal static class ColumnSteps
        {
            [HarmonyPrefix]
            private static void Prefix(ScoreColumnTask._Execute_d__1 __instance, out bool __state)
            {
                __state = false;
                try
                {
                    if (__instance.__1__state != 0 || _last == null || !Live(__instance.__4__this)) return;
                    __state = true;
                    var now = Take();
                    if (now == null) return;
                    Say(EncounterDiff.Lines(_last, now));
                    _last = now;
                    _stepStart = ColumnSums(Encounter.Model, true);
                }
                catch (Exception e) { CoreLog.Warning("EncounterEvents: a column's start failed: " + e.Message); }
            }

            [HarmonyPostfix]
            private static void Postfix(ScoreColumnTask._Execute_d__1 __instance, bool __result, bool __state)
            {
                try
                {
                    // The column and whether it is a mini-die's are read from the args on the first step;
                    // how many dice it scores with stays in the args (Amount).
                    if (__state)
                    {
                        _column = __instance._i_5__2;
                        _columnMini = __instance._isMini_5__3;
                        _columnDice = DiceCount(__instance.__4__this);
                    }
                    if (__result || _column < 0 || _last == null || _stepStart == null) return;
                    int column = _column;
                    _column = -1;
                    var now = Take();
                    var end = ColumnSums(Encounter.Model, true);
                    if (now == null || end == null) return;
                    var gains = new List<string>();
                    for (int x = 0; x < end.Length; x++)
                    {
                        var gain = x < _stepStart.Length ? end[x] - _stepStart[x] : end[x];
                        gains.Add(gain.ToDouble() > 0 ? gain.ToString() : null);
                    }
                    string mini = _columnMini ? GameLoc.Get("MiniDice_Keyword") ?? "Mini-Dice" : null;
                    CoreLog.Info("EncounterEvents: column " + column + " scored with " + _columnDice + (_columnMini ? " mini" : "") + " dice, gains [" + string.Join(" ", gains.ConvertAll(g => g ?? "-")) + "]");
                    Say(EncounterDiff.Step(_last, now, column, _columnDice, gains, mini, Combo(column - 1, _columnDice)));
                    _last = now;
                }
                catch (Exception e) { CoreLog.Warning("EncounterEvents: a column's end failed: " + e.Message); }
            }
        }

        // The label the game draws over a column scored with several dice (GridColumn.PairTextObjects:
        // "Pair", "Triple!", ... for 2, 3, ... dice), its localized string: the texts are filled by
        // LocalizeStringEvents only while drawn, so a hidden one holds the prefab's.
        private static string Combo(int x, int dice)
        {
            if (dice < 2) return null;
            try
            {
                var labels = Encounter.Column(x)?.PairTextObjects;
                if (labels == null || dice - 2 >= labels.Count) return null;
                var tmp = labels[dice - 2]?.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (tmp == null) return null;
                var ev = tmp.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
                string text = ev != null && ev.StringReference != null && !ev.StringReference.IsEmpty ? ev.StringReference.GetLocalizedString() : tmp.text;
                return GameText.Clean(text);
            }
            catch (Exception e) { CoreLog.Warning("EncounterEvents: reading column " + (x + 1) + "'s combo label failed: " + e.Message); return null; }
        }

        private static int DiceCount(ScoreColumnTask task)
        {
            try
            {
                if (task?.Args != null && task.Args.TryGetValue(ArgKey.Amount, out var amount) && amount != null) return amount.Unbox<int>();
            }
            catch (Exception e) { CoreLog.Warning("EncounterEvents: reading a column's dice count failed: " + e.Message); }
            return 1;
        }

        // The encounter's own task engine, not a preview's.
        private static bool Live(ATask task)
        {
            var engine = task?.TaskEngine;
            var live = Encounter.Controller?.TaskEngine;
            return engine != null && live != null && engine.Pointer == live.Pointer && !engine.IsPreviewMode;
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
                    MultText = Encounter.Mult(),
                    MultLabel = GameLoc.Get("GlobalMult_Keyword") ?? "Mult",
                    CloudLabel = GameLoc.Get("Cloudy_TooltipTitle") ?? "Cloudy",
                };
                foreach (var die in Encounter.Dice()) s.Dice.Add(die.isRolling ? 0 : die.Value);
                var pilotDie = Encounter.PilotDie();
                if (pilotDie != null && !pilotDie.isRolling) s.PilotDie = pilotDie.Value;
                s.Tippy = Screens.EncounterScreen.TippyLine();
                if (Screens.EncounterScreen.HasRage())
                {
                    s.Rage = Encounter.Int(EncounterValue.Rage) ?? 0;
                    s.BeastTurns = Encounter.Int(EncounterValue.BeastTurn) ?? 0;
                }
                s.ShopRarity = Encounter.Int(EncounterValue.ShopRarity) ?? 0;
                s.ShopRarityLabel = Screens.EncounterScreen.ShopRarityTitle();
                ColumnAttack(model, s.ColumnAttack);
                Tokens(s.Tokens);
                Units(model, s.Units);
                for (int slot = 0; slot < (model.ArtifactSlots?.Count ?? 0); slot++)
                {
                    var artifact = Encounter.Artifact(slot);
                    // A pilot is an artifact while it is the target: its arrival is the new target's
                    // line, its going the result's, never "added" or "removed".
                    if (artifact == null || Encounter.PilotName(artifact) != null) continue;
                    var xp = Encounter.ArtifactXp(artifact);
                    s.Artifacts.Add(new ArtifactSnap
                    {
                        Id = artifact.ArtifactID.Number, Name = Encounter.ArtifactName(artifact), Slot = slot, Level = artifact.Level,
                        Xp = xp?.Xp ?? -1, XpNeeded = xp?.Needed ?? 0, Blocked = Encounter.Blocked(artifact),
                    });
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
            var sums = ColumnSums(model, Encounter.Phase == Phase.Scoring);
            if (sums == null) return;
            foreach (var sum in sums) into.Add(sum.ToDouble() > 0 ? sum.ToString() : "0");
        }

        private static BigDouble[] ColumnSums(EncounterModel model, bool thisTurn)
        {
            if (model == null) return null;
            int columns = Encounter.Columns;
            var sums = new BigDouble[columns];
            var any = new bool[columns];
            foreach (var kv in model.ModelItemDict)
            {
                var unit = kv.Value?.TryCast<EntityModel>();
                if (unit == null || !model.GridModel.IsEntityOnGrid(unit.EntityID)) continue;
                int x = model.GridModel.GetEntityCoord(unit.EntityID, false).x;
                if (x < 0 || x >= columns) continue;
                var gained = thisTurn ? unit.GetAttackGainedThisTurn() : unit.GetAttackGainedLastTurn();
                if (gained.ToDouble() <= 0) continue;
                sums[x] = any[x] ? sums[x] + gained : gained;
                any[x] = true;
            }
            for (int x = 0; x < columns; x++) if (!any[x]) sums[x] = new BigDouble(0);
            return sums;
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
