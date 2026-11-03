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
    /// causes. A new controller (a new encounter) is a fresh baseline, said nothing of. Every line
    /// said is appended to <see cref="Log"/> (the events buffer), cleared by a new controller (a run
    /// begun or resumed, a turn rewound).
    /// </summary>
    internal static class EncounterEvents
    {
        private const int SettleFrames = 3;

        /// <summary>The scoring summary setting's key in the host's store.</summary>
        public const string SummaryKey = "scoring_summary";

        /// <summary>Whether each turn's scoring is said as one line (<see cref="EncounterDiff.TurnSummary"/>)
        /// instead of column by column; the rest of what the turn changed is said once, as the shop
        /// settles. Off by default.</summary>
        public static bool Summary
        {
            get
            {
                try { return ModuleMain.Host?.Settings?.Store.GetBool(SummaryKey, false) == true; }
                catch (Exception e) { CoreLog.Warning("EncounterEvents: reading the summary setting failed: " + e.Message); return false; }
            }
        }

        /// <summary>Every line said in the current encounter, oldest first.</summary>
        public static readonly List<string> Log = new List<string>();

        private static IntPtr _controller;
        private static EncounterSnapshot _last;
        private static int _idle;

        /// <summary>Whether this frame is settled (see the class).</summary>
        public static bool Settled { get; private set; }

        // Frames the narration waits out for a key that says its own result (Q's undo).
        private static int _held;

        /// <summary>Hold the narration for some frames: a key that says what it changed, then
        /// <see cref="Rebase"/>s, is not said twice.</summary>
        public static void Hold(int frames) => _held = Math.Max(_held, frames);

        public static void Tick()
        {
            Settled = false;
            if (_held > 0) { _held--; return; }
            var c = Encounter.Controller;
            if (c == null) { _controller = IntPtr.Zero; _last = null; _idle = 0; return; }
            if (c.Pointer != _controller) { _controller = c.Pointer; _last = null; _idle = 0; _column = -1; Log.Clear(); }
            TurnBoard();

            var tip = Tips.Current;
            bool waiting = tip != null && Tips.WaitsForClick(tip);
            if (!waiting && (Encounter.Busy || Rolling())) { _idle = 0; return; }
            if (!waiting && ++_idle < SettleFrames) return;
            Settled = true;

            var now = Take();
            if (now == null) return;
            if (_last != null) Say(EncounterDiff.Lines(_last, now, columns: !Summary));
            else
            {
                // A new encounter (a run begun, a turn rewound, a run loaded) met on the player's turn:
                // where it stands. Otherwise its first change says it.
                string turn = EncounterDiff.TurnLine(now);
                if (turn != null) Say(new List<string> { turn });
            }
            _last = now;
        }

        // The scoring board under the grid (EncounterView.CurrentScoreBoard) sums the turn: its attack
        // counting up, its Mult beside it ("X 2.0") once there is any, then the two combined
        // (CombineAndScore: CurrentScore times GlobalMult, written to CenterTotalScoreText, then shown,
        // then counted down into the score panel). Said once per scoring as the total shows, and only
        // with a Mult: without one the total is the columns' attack, already said.
        private static bool _turnSaid;

        // The Mult as the player's turn last stood: the turn's Mult is news only if the scoring changed
        // it (a Mult held from before the launch was there all turn).
        private static double _multBefore = 1;

        private static void TurnBoard()
        {
            var phase = Encounter.Phase;
            if (phase != Phase.Scoring)
            {
                _turnSaid = false;
                if (phase == Phase.Player) _multBefore = ModelMult() ?? 1;
                // A summarized turn whose board never showed its total: its columns at least, as the
                // turn ends.
                if (_summary.Open && phase != Phase.Player) SaySummary(null, null);
                return;
            }
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
                string label = GameLoc.Get("GlobalMult_Keyword") ?? "Mult";
                // The Mult only when the scoring changed it; then the board's, else the model's (the
                // board draws it only once it rises: the total is multiplied by the model's).
                double? now = ModelMult();
                bool changed = now.HasValue && Math.Abs(now.Value - _multBefore) > 1e-9;
                if (changed && string.IsNullOrEmpty(mult)) mult = Encounter.Mult();
                CoreLog.Info("EncounterEvents: turn total " + sum + ", Mult " + _multBefore + " -> " + now + (changed ? ", said" : ", unchanged"));
                if (_summary.Open)
                {
                    SaySummary(changed && !string.IsNullOrEmpty(mult) ? Core.Strings.Strings.EncFigure(label, mult) : null, sum ?? "");
                    return;
                }
                // Column by column, the total is news only with a Mult the scoring changed.
                if (!changed || string.IsNullOrEmpty(mult) || attack == null || sum == null) return;
                Say(new List<string> { Core.Strings.Strings.EncTurnScore(attack, Core.Strings.Strings.EncFigure(label, mult), sum) });
            }
            catch (Exception e) { CoreLog.Warning("EncounterEvents: reading the scoring board failed: " + e.Message); }
        }

        // The summarized turn, gathered as its columns score (ColumnSteps): each column's attack over
        // every pass, the Mini-Dice scored, and the combos they landed in, by the game's label, in
        // order of size (a pass's column with several Mini-Dice: "Pair" for 2, "Quintuple!" for 5
        // and up). Opened by the turn's first column, closed as the line is said.
        private sealed class TurnTally
        {
            public bool Open;
            public BigDouble[] Columns = new BigDouble[0];
            public int MiniDice;
            public readonly List<KeyValuePair<string, int>> Combos = new List<KeyValuePair<string, int>>();
            private readonly List<int> _sizes = new List<int>();

            public void Start(int columns)
            {
                Open = true;
                Columns = new BigDouble[columns];
                for (int x = 0; x < columns; x++) Columns[x] = new BigDouble(0);
                MiniDice = 0;
                Combos.Clear();
                _sizes.Clear();
            }

            public void AddCombo(int dice, string label)
            {
                if (string.IsNullOrEmpty(label)) return;
                int i = Combos.FindIndex(c => c.Key == label);
                if (i >= 0) { Combos[i] = new KeyValuePair<string, int>(label, Combos[i].Value + 1); return; }
                int at = _sizes.FindIndex(size => size > dice);
                if (at < 0) at = Combos.Count;
                Combos.Insert(at, new KeyValuePair<string, int>(label, 1));
                _sizes.Insert(at, dice);
            }
        }

        private static readonly TurnTally _summary = new TurnTally();

        private static double? ModelMult()
        {
            try { return Encounter.Model?.GetBigDoubleValue(EncounterValue.GlobalMult).ToDouble(); }
            catch (Exception e) { CoreLog.Warning("EncounterEvents: reading the Mult failed: " + e.Message); return null; }
        }

        // mult ("Mult 2"): null when the scoring left it as it was; total: the board's, null (with
        // mult) when it never showed it.
        private static void SaySummary(string mult, string total)
        {
            _summary.Open = false;
            try
            {
                var columns = new List<string>();
                foreach (var c in _summary.Columns) columns.Add(c.ToDouble() > 0 ? c.ToString() : "0");
                string line = total != null
                    ? EncounterDiff.TurnSummary(columns, mult, total, _summary.MiniDice, GameLoc.Get("MiniDice_Keyword") ?? "Mini-Dice", _summary.Combos)
                    : string.Join(", ", columns);
                Say(new List<string> { line });
            }
            catch (Exception e) { CoreLog.Warning("EncounterEvents: saying the turn's summary failed: " + e.Message); }
        }

        private static void Say(List<string> lines)
        {
            if (lines.Count > 0) LogRaw();
            foreach (var line in lines) Speech.Say(line);
            Log.AddRange(lines);
        }

        // The turn's scoring is one busy stretch (the dice launched, each column scored in turn, the
        // end of the turn) that settles only as the shop opens. It is said column by column instead:
        // ScoreDiceTask scores the columns holding dice in ascending order, each a ScoreColumnTask
        // (the mini-dice through a second ScoreDiceTask), whose coroutine is hooked. As a column
        // starts, what came before it is said; as it ends, the column with the attack it gained and
        // what its units set off. The end of the turn is then the shop's settled snapshot.
        private static int _column = -1, _columnDice, _columnRift;
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
                    // Summarized: nothing said in the scoring, and the baseline kept from before it, so
                    // the shop's settled snapshot says what the whole turn changed, once.
                    if (Summary && Encounter.Phase == Phase.Scoring)
                    {
                        if (!_summary.Open) _summary.Start(Encounter.Columns);
                        _stepStart = ColumnSums(Encounter.Model, true);
                        return;
                    }
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
                        // The Chrono Rift faces in the tray count as dice in every column with dice,
                        // for the base dice only (as the game's label and its pair conditions count).
                        _columnRift = _columnMini ? 0 : Encounter.RiftFaces();
                    }
                    if (__result || _column < 0 || _last == null || _stepStart == null) return;
                    int column = _column;
                    _column = -1;
                    if (_summary.Open) { Tally(column); return; }
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
                    Say(EncounterDiff.Step(_last, now, column, _columnDice, gains, mini, Combo(column - 1, _columnDice + _columnRift)));
                    _last = now;
                }
                catch (Exception e) { CoreLog.Warning("EncounterEvents: a column's end failed: " + e.Message); }
            }
        }

        // A summarized turn's column: the attack every column gained in it, and a Mini-Dice pass's
        // dice and combo.
        private static void Tally(int column)
        {
            var end = ColumnSums(Encounter.Model, true);
            if (end == null) return;
            for (int x = 0; x < end.Length && x < _summary.Columns.Length; x++)
            {
                var gain = x < _stepStart.Length ? end[x] - _stepStart[x] : end[x];
                if (gain.ToDouble() > 0) _summary.Columns[x] = _summary.Columns[x] + gain;
            }
            if (_columnMini)
            {
                _summary.MiniDice += _columnDice;
                if (_columnDice >= 2) _summary.AddCombo(_columnDice, Combo(column - 1, _columnDice));
            }
            CoreLog.Info("EncounterEvents: summarizing column " + column + ", " + _columnDice + (_columnMini ? " mini" : "") + " dice");
        }

        // The label the game draws over a column scored with several dice (GridColumn.PairTextObjects:
        // "Pair", "Triple!", ... for 2, 3, ... dice, a Chrono Rift face counting as one), its
        // localized string (Encounter.ComboText). Monocle and Broken Lens change what the game's pair
        // and triple triggers count, not the label: as drawn.
        private static string Combo(int x, int dice)
        {
            if (dice < 2) return null;
            try
            {
                var labels = Encounter.Column(x)?.PairTextObjects;
                if (labels == null || dice - 2 >= labels.Count) return null;
                return Encounter.ComboText(labels[dice - 2]);
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
                    RiftLabel = Encounter.RiftName(),
                };
                foreach (var die in Encounter.Dice()) (Encounter.IsKraken(die) ? s.Kraken : s.Dice).Add(die.isRolling ? 0 : die.Value);
                var pilotDie = Encounter.PilotDie();
                if (pilotDie != null && !pilotDie.isRolling) s.PilotDie = pilotDie.Value;
                s.Tippy = Screens.EncounterScreen.TippyLine();
                s.BossHp = Encounter.BossHp() ?? -1;
                foreach (var card in Encounter.StarCards())
                    s.StarCards.Add((card.CardTooltip != null ? GameText.Of(card.CardTooltip.TitleText) : null) ?? card.artifactName.ToString());
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
                if (s.Shop.Count > 0) s.LastOffer.AddRange(s.Shop);
                else if (_last != null && _last.Phase == s.Phase) s.LastOffer.AddRange(_last.LastOffer);
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
