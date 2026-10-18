using System;
using System.Collections.Generic;
using StarVaders;
using DiceVoders.Core;
using DiceVoders.Core.Graph;
using DiceVoders.Core.Input;
using DiceVoders.Core.Strings;
using DiceVoders.Core.UI;
using DiceVoders.Module.Encounter;
using DiceVoders.Module.Interop;
using DiceVoders.Module.UI;
using Screen = DiceVoders.Core.Screens.Screen;
using EncounterState = DiceVoders.Module.Encounter.Encounter;

namespace DiceVoders.Module.Screens
{
    /// <summary>
    /// The encounter (<c>EncounterScene</c>), named by its target ("Defeat the P.O.G. (Pluto Outer
    /// Guards)"). Two stops. The board: a header row, one per column ("column 3, Dice Power 2", then
    /// its honey and the column's artifact), the grid top row first ("2, 1, Migo, 10 strength"), then
    /// a strip under the bottom row with what the phase puts there: in the player phase the dice in
    /// the tray's order ("die 3, frozen") and Launch. All rows share a key, so Up and Down keep the
    /// column; the strip is strict (item n under column n). Focus first lands on the first die. The
    /// status: the target, Target Defense, Total Attack, turns left, hearts, the tokens, Mult, and
    /// the artifacts outside the columns. Units and artifacts give their tooltips on Backspace and in
    /// the ui buffer.
    /// </summary>
    public sealed class EncounterScreen : Screen
    {
        public override string Key => "encounter";
        public override bool AllowsTypeahead => false;
        public override object InitialFocusStop => BoardStop;

        private static readonly InputCategory[] Categories = { InputCategory.Game, InputCategory.UI };
        public override IReadOnlyList<InputCategory> InputCategories => Categories;

        public override bool IsActive()
        {
            var c = EncounterState.Controller;
            return c != null && !c.EncounterEnded;
        }

        public override string ScreenName => Target();

        // The name and the landing wait while the encounter moves or a tip is on its way: the
        // tutorial's tips come one after another over the board.
        public override bool HoldsArrival => EncounterState.Busy || Tips.Coming;

        internal const string BoardStop = "enc:board";

        public static ControlId TileId(int x, int y) => ControlId.Structural("enc:tile:" + x + "," + y);
        public static ControlId ColumnId(int x) => ControlId.Structural("enc:col:" + x);
        private static ControlId DieId(Dice die) => ControlId.Structural("enc:die:" + die.GetInstanceID());

        /// <summary>The 0-based tile the focus is on, or null off the grid.</summary>
        public static (int X, int Y)? FocusedTile()
        {
            string id = FocusedKey();
            if (id == null || !id.StartsWith("enc:tile:")) return null;
            var parts = id.Substring("enc:tile:".Length).Split(',');
            return (int.Parse(parts[0]), int.Parse(parts[1]));
        }

        /// <summary>The 0-based column the focus is in: a tile's or a header's; null elsewhere.</summary>
        public static int? FocusedColumn()
        {
            var tile = FocusedTile();
            if (tile.HasValue) return tile.Value.X;
            string id = FocusedKey();
            return id != null && id.StartsWith("enc:col:") ? int.Parse(id.Substring("enc:col:".Length)) : (int?)null;
        }

        private static string FocusedKey() =>
            (Navigation.Active as GraphNavigator)?.FocusedNode?.Id?.StructuralKey as string;

        public override void Build(GraphBuilder b)
        {
            if (EncounterState.Controller == null) return;
            BuildBoard(b);
            BuildStatus(b);
        }

        // A tip that waits for an action instead of a click ("Press the LAUNCH button...") is no
        // modal (TipScreen takes the others): it is said once as it appears, and heads the status
        // while it shows. By native pointer: a proxy is a fresh object per read.
        private IntPtr _tipSaid;

        public override void OnUpdate()
        {
            var tip = ActionTip();
            if (tip == null) { _tipSaid = IntPtr.Zero; return; }
            if (tip.Pointer == _tipSaid) return;
            _tipSaid = tip.Pointer;
            string text = Tips.Text(tip);
            if (text != null) Speech.Say(text);
        }

        private static Tip ActionTip()
        {
            var tip = Tips.Current;
            return tip != null && !Tips.WaitsForClick(tip) && Tips.Text(tip) != null ? tip : null;
        }

        // ---- the board ----

        private static void BuildBoard(GraphBuilder b)
        {
            int columns = EncounterState.Columns, rows = EncounterState.Rows;
            b.BeginStop(BoardStop);
            b.StartRow(BoardStop);
            for (int x = 0; x < columns; x++)
                b.AddItem(ColumnId(x), Header(x));
            b.EndRow();
            for (int y = rows - 1; y >= 0; y--)
            {
                b.StartRow(BoardStop);
                for (int x = 0; x < columns; x++)
                    b.AddItem(TileId(x, y), Cell(x, y));
                b.EndRow();
            }
            BuildStrip(b);
        }

        // The strip under the grid: the dice and Launch while the player rolls; nothing in the other
        // phases yet (the shop comes next).
        private static void BuildStrip(GraphBuilder b)
        {
            if (EncounterState.Phase != Phase.Player) return;
            var dice = EncounterState.Dice();
            var launch = EncounterState.LaunchButton();
            if (dice.Count == 0 && launch == null) return;
            b.PushContext(null);
            b.StartRow(BoardStop, strictColumns: true);
            for (int i = 0; i < dice.Count; i++)
                b.AddItem(DieId(dice[i]), Die(dice[i], i == 0));
            if (launch != null)
                b.AddItem(ControlId.Structural("enc:launch"), Launch(launch));
            b.EndRow();
            b.PopContext();
        }

        // "column 3, Dice Power 2, honey, Twin Engines": the column and what bears on all of it.
        private static NodeVtable Header(int x)
        {
            Func<string> label = () => HeaderText(x);
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                SpeaksOwnPosition = true,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label) },
                SearchText = label,
                OnFocus = () => { var view = EncounterState.ArtifactView(EncounterState.Artifact(x)); if (view != null) EncounterState.Hover(view); },
                Details = () => ArtifactDetails(EncounterState.Artifact(x)),
                OnSecondary = () => SayDetails(ArtifactDetails(EncounterState.Artifact(x))),
            };
        }

        internal static string HeaderText(int x)
        {
            var parts = new List<string> { Strings.EncColumn(x + 1) };
            int power = EncounterState.DicePower(x);
            if (power > 0) parts.Add(Strings.EncFigure(GameLoc.Get("DicePower_Misc") ?? "Dice Power", power.ToString()));
            if (EncounterState.Honey(x)) parts.Add(Strings.EncHoney);
            string artifact = EncounterState.ArtifactName(EncounterState.Artifact(x));
            if (artifact != null) parts.Add(artifact);
            return string.Join(", ", parts);
        }

        private static NodeVtable Cell(int x, int y)
        {
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                SpeaksOwnPosition = true,
                // One part: the coordinates, then what is there.
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => CellText(x, y)) },
                SearchText = () => Strings.EncCoord(x + 1, y + 1),
                OnFocus = () => { var tile = EncounterState.Tile(x, y); if (tile != null) EncounterState.Hover(tile); },
                Details = () => CellDetails(x, y),
                OnSecondary = () => SayDetails(CellDetails(x, y)),
            };
        }

        internal static string CellText(int x, int y) => Strings.EncCoord(x + 1, y + 1) + ", " + CellContents(x, y);

        /// <summary>"Migo, 10 strength, shielded" or "empty", then a cloud.</summary>
        internal static string CellContents(int x, int y)
        {
            var parts = new List<string>();
            var unit = EncounterState.At(x, y);
            parts.Add(unit != null ? Describe(unit) : Strings.EncEmpty);
            if (EncounterState.Cloud(x, y)) parts.Add(GameLoc.Get("Cloudy_TooltipTitle") ?? "Cloudy");
            return string.Join(", ", parts);
        }

        /// <summary>"Migo, 10 strength, shielded, charge 1/2": the unit's name, strength and state.</summary>
        internal static string Describe(EntityModel unit)
        {
            var parts = new List<string> { EncounterState.Name(unit) };
            string strength = EncounterState.Strength(unit);
            if (strength != null) parts.Add(Strings.EncStrength(strength));
            try
            {
                if (unit.IsNegative) parts.Add(Strings.EncNegative);
                if (unit.IsShielded) parts.Add(GameLoc.Get("Shielded_Keyword") ?? "Shielded");
                if (unit.IsShocked) parts.Add(GameLoc.Get("Shock_Keyword") ?? "Shock");
                if (unit.MaxCharge > 0) parts.Add(Strings.EncCharge(unit.Charge, unit.MaxCharge));
            }
            catch (Exception e) { CoreLog.Warning("EncounterScreen: reading a unit's state failed: " + e.Message); }
            parts.RemoveAll(string.IsNullOrEmpty);
            return string.Join(", ", parts);
        }

        private static IEnumerable<string> CellDetails(int x, int y)
        {
            var unit = EncounterState.At(x, y);
            if (unit == null) yield break;
            var (description, rarity) = EncounterState.Tooltip(unit, EncounterState.Tile(x, y));
            string name = EncounterState.Name(unit);
            yield return description != null ? name + ": " + description : name;
            if (rarity != null) yield return rarity;
        }

        private static IEnumerable<string> ArtifactDetails(ArtifactModel artifact)
        {
            if (artifact == null) yield break;
            string name = EncounterState.ArtifactName(artifact);
            var lines = EncounterState.ArtifactLines(artifact);
            if (lines.Count == 0) { yield return name; yield break; }
            yield return name + ": " + lines[0];
            for (int i = 1; i < lines.Count; i++) yield return lines[i];
        }

        // ---- the dice ----

        // "die 3, frozen": the face (the column it scores) and the die's state.
        private static NodeVtable Die(Dice die, bool first)
        {
            Func<string> label = () => DieText(die);
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label) },
                SearchText = label,
                IsSelected = () => first,
                OnFocus = () => EncounterState.Hover(die),
            };
        }

        internal static string DieText(Dice die)
        {
            var parts = new List<string>();
            try
            {
                parts.Add(die.isRolling ? Strings.EncRolling : Strings.EncDie(die.Value));
                if (die.IsDangerDice) parts.Add(Strings.EncPilotDie);
                if (die.IsPermaFrozen) parts.Add(Strings.EncPermafrozen);
                else if (die.IsLockedIn) parts.Add(Strings.EncFrozen);
                if (die.IsCracked) parts.Add(Strings.EncCracked);
                if (die.AddedPower > 0) parts.Add(Strings.EncAddedPower(die.AddedPower));
            }
            catch (Exception e) { CoreLog.Warning("EncounterScreen: reading a die failed: " + e.Message); }
            return string.Join(", ", parts);
        }

        private static NodeVtable Launch(AViewItem launch)
        {
            var vt = GameNodes.Button(() => ViewNodes.Label(launch) ?? GameLoc.Get("Launch_Misc"), () => EncounterState.Click(launch));
            vt.OnFocus = () => EncounterState.Hover(launch);
            return vt;
        }

        // ---- the status ----

        private static void BuildStatus(GraphBuilder b)
        {
            b.BeginStop("enc:status");
            b.PushContext(null, positions: false);
            if (ActionTip() != null)
                b.AddItem(ControlId.Structural("enc:tip"), Stat(() => { var tip = ActionTip(); return tip != null ? Tips.Text(tip) : null; }));
            b.AddItem(ControlId.Structural("enc:target"), Stat(TargetLine));
            b.AddItem(ControlId.Structural("enc:defense"), Stat(Defense));
            b.AddItem(ControlId.Structural("enc:attack"), Stat(Attack));
            b.AddItem(ControlId.Structural("enc:turns"), Stat(Turns));
            b.AddItem(ControlId.Structural("enc:hearts"), Stat(Hearts));
            foreach (var token in TokenLines())
            {
                var line = token;
                b.AddItem(ControlId.Structural("enc:token:" + line.Key), Stat(line.Value));
            }
            b.AddItem(ControlId.Structural("enc:mult"), Stat(Mult));
            // The artifacts outside the columns' slots.
            var model = EncounterState.Model;
            int slots = model?.ArtifactSlots?.Count ?? 0;
            for (int slot = EncounterState.Columns; slot < slots; slot++)
            {
                var artifact = EncounterState.Artifact(slot);
                if (artifact == null) continue;
                int s = slot;
                b.AddItem(ControlId.Structural("enc:artifact:" + slot), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => EncounterState.ArtifactName(EncounterState.Artifact(s))) },
                    SearchText = () => EncounterState.ArtifactName(EncounterState.Artifact(s)),
                    OnFocus = () => { var view = EncounterState.ArtifactView(EncounterState.Artifact(s)); if (view != null) EncounterState.Hover(view); },
                    Details = () => ArtifactDetails(EncounterState.Artifact(s)),
                    OnSecondary = () => SayDetails(ArtifactDetails(EncounterState.Artifact(s))),
                });
            }
            b.PopContext();
        }

        private static NodeVtable Stat(Func<string> text) => new NodeVtable
        {
            ControlType = ControlTypes.Text,
            Announcements = new List<NodeAnnouncement> { new NodeAnnouncement(text, live: true, kind: AnnouncementKinds.Label) },
            SearchText = text,
        };

        /// <summary>The target as the score panel names it ("Defeat the P.O.G. (Pluto Outer Guards)").</summary>
        public static string Target()
        {
            string text = GameText.Of(EncounterState.View?.TargetText);
            return text != null ? System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim() : null;
        }

        /// <summary>The target and the act ("Defeat the P.O.G. (Pluto Outer Guards), Act 1-1").</summary>
        public static string TargetLine()
        {
            string target = Target();
            string act = GameText.Of(EncounterState.View?.MapPanel?.actText);
            return target != null && act != null ? target + ", " + act : target ?? act;
        }

        public static string Defense() =>
            Strings.EncFigure(GameLoc.Get("TargetDefense_Misc") ?? "Target Defense", EncounterState.Big(EncounterValue.TargetTributeScore));

        public static string Attack() =>
            Strings.EncFigure(GameLoc.Get("TotalAttack_Misc") ?? "Total Attack", EncounterState.Big(EncounterValue.TotalScore));

        /// <summary>"4 Turns", the game's words.</summary>
        public static string Turns()
        {
            int? left = EncounterState.Int(EncounterValue.TurnsRemaining);
            if (left == null) return null;
            string text = GameLoc.Get("TurnsLeft_Misc");
            return text != null ? text.Replace("[#]", left.Value.ToString()) : left.Value.ToString();
        }

        public static string Hearts() => Figure(Strings.EncHearts, EncounterValue.Hearts);

        public static string Mult() => Strings.EncFigure(GameLoc.Get("GlobalMult_Keyword") ?? "Mult", EncounterState.Big(EncounterValue.GlobalMult));

        /// <summary>The tokens, in the game's words: Budge, Chrono, Swap, Mini-Dice, the shop's Cash.</summary>
        public static List<KeyValuePair<string, Func<string>>> TokenLines() => new List<KeyValuePair<string, Func<string>>>
        {
            new KeyValuePair<string, Func<string>>("budge", () => Figure(GameLoc.Get("Budge_Keyword") ?? "Budge", EncounterValue.BudgeToken)),
            new KeyValuePair<string, Func<string>>("chrono", () => Figure(GameLoc.Get("Chrono_Keyword") ?? "Chrono", EncounterValue.ChronoToken)),
            new KeyValuePair<string, Func<string>>("swap", () => Figure(Strings.EncSwap, EncounterValue.SwapToken)),
            new KeyValuePair<string, Func<string>>("minidice", () => Figure(GameLoc.Get("MiniDice_Keyword") ?? "Mini-Dice", EncounterValue.MiniDice)),
            new KeyValuePair<string, Func<string>>("cash", () => Figure(GameLoc.Get("ShopToken_TooltipTitle") ?? "Cash", EncounterValue.ShopToken)),
        };

        private static string Figure(string label, EncounterValue value)
        {
            int? n = EncounterState.Int(value);
            return n.HasValue ? Strings.EncFigure(label, n.Value.ToString()) : null;
        }

        /// <summary>Backspace, the game's more info: the focused thing's details, read out at once,
        /// the ones the ui buffer steps through.</summary>
        private static void SayDetails(IEnumerable<string> lines)
        {
            var list = new List<string>(lines ?? Array.Empty<string>());
            Speech.Say(list.Count > 0 ? string.Join(" ", list) : Strings.NoDetails, interrupt: true);
        }
    }
}
