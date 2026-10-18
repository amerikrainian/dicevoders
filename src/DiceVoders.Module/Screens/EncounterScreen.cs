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
    /// the tray's order ("3, frozen") and Launch; in the shop its choices ("Meteor, Artifact, 2 cash")
    /// and Proceed. All rows share a key, so Up and Down keep the column; the strip is strict (item n
    /// under column n). A tile the grid does not have yet reads "no tile". While the game waits on a
    /// target (a unit to place, a tile to build, a column for an artifact), the cells and headers it
    /// takes read "target" first and Enter gives them; focus goes to the first; Escape cancels. A new
    /// turn puts focus on the first die (silently: the roll is said), the shop on its first choice. The
    /// status: the target, the attack against the target ("0/100 attack"), turns left, hearts, the tokens, Mult, and
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

        // Escape cancels a target choice, as the game's Cancel; otherwise it is the navigator's.
        public override IEnumerable<ElementAction> GetActions()
        {
            if (EncounterState.Selecting)
                yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => EncounterKeys.CancelSelection());
        }

        // The name and the landing wait while the encounter moves or a tip is on its way: the
        // tutorial's tips come one after another over the board.
        public override bool HoldsArrival => !EncounterEvents.Settled || Tips.Coming;

        internal const string BoardStop = "enc:board";

        public static ControlId TileId(int x, int y) => ControlId.Structural("enc:tile:" + x + "," + y);
        public static ControlId ColumnId(int x) => ControlId.Structural("enc:col:" + x);
        private static ControlId DieId(Dice die) => ControlId.Structural("enc:die:" + die.GetInstanceID());
        private static ControlId ChoiceId(DraftChoice choice) => ControlId.Structural("enc:choice:" + choice.GetInstanceID());

        /// <summary>The shop choice the focus is on, or null.</summary>
        public static DraftChoice FocusedChoice()
        {
            string id = FocusedKey();
            if (id == null || !id.StartsWith("enc:choice:")) return null;
            int instance = int.Parse(id.Substring("enc:choice:".Length));
            foreach (var choice in EncounterState.Choices())
                if (choice.GetInstanceID() == instance) return choice;
            return null;
        }

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

        /// <summary>The die the focus is on, or null.</summary>
        public static Dice FocusedDie()
        {
            string id = FocusedKey();
            if (id == null || !id.StartsWith("enc:die:")) return null;
            int instance = int.Parse(id.Substring("enc:die:".Length));
            foreach (var die in EncounterState.Dice())
                if (die.GetInstanceID() == instance) return die;
            return null;
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

        // The phase and selection last seen, for the landings they cause; a landing is retried each
        // frame until the node it wants is in the render.
        private Phase _phase = Phase.None;
        private bool _selecting;
        private ControlId _pendingLanding;
        private bool _pendingSpeak;

        public override void OnUpdate()
        {
            Landings();
            SayActionTip();
        }

        private void Landings()
        {
            var phase = EncounterState.Phase;
            bool selecting = EncounterState.Selecting;
            if (selecting && !_selecting)
            {
                var first = FirstTarget();
                if (first != null) { _pendingLanding = first; _pendingSpeak = true; }
            }
            else if (!selecting && phase != _phase && EncounterEvents.Settled)
            {
                if (phase == Phase.Draft)
                {
                    var choices = EncounterState.Choices();
                    if (choices.Count > 0) { _pendingLanding = ChoiceId(choices[0]); _pendingSpeak = true; }
                }
                else if (phase == Phase.Player)
                {
                    var dice = EncounterState.Dice();
                    if (dice.Count > 0) { _pendingLanding = DieId(dice[0]); _pendingSpeak = false; }
                }
            }
            _selecting = selecting;
            if (EncounterEvents.Settled || selecting) _phase = phase;
            if (_pendingLanding != null && Navigation.Active?.MoveTo(_pendingLanding, _pendingSpeak) == true) _pendingLanding = null;
        }

        // The first cell or header the running selection takes, reading order (headers, then the
        // grid top row first).
        private static ControlId FirstTarget()
        {
            for (int x = 0; x < EncounterState.Columns; x++)
                if (EncounterState.SlotTarget(x) != null) return ColumnId(x);
            for (int y = EncounterState.Rows - 1; y >= 0; y--)
                for (int x = 0; x < EncounterState.Columns; x++)
                    if (EncounterState.TargetAt(x, y) != null) return TileId(x, y);
            return null;
        }

        private void SayActionTip()
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

        // The strip under the grid: the dice and Launch while the player rolls, the shop's choices and
        // its buttons in the shop. Its nodes vanish quietly: launching takes the dice away, and the
        // landing that causes is no news (the scoring is said).
        private static void BuildStrip(GraphBuilder b)
        {
            var phase = EncounterState.Phase;
            if (phase == Phase.Player) BuildDice(b);
            else if (phase == Phase.Draft) BuildShop(b);
        }

        private static void BuildDice(GraphBuilder b)
        {
            var dice = EncounterState.Dice();
            var launch = EncounterState.LaunchButton();
            if (dice.Count == 0 && launch == null) return;
            b.PushContext(null);
            b.StartRow(BoardStop, strictColumns: true);
            for (int i = 0; i < dice.Count; i++)
                b.AddItem(DieId(dice[i]), Die(dice[i], i == 0));
            if (launch != null)
                b.AddItem(ControlId.Structural("enc:launch"), StripButton(launch, () => ViewNodes.Label(launch) ?? GameLoc.Get("Launch_Misc")));
            b.EndRow();
            b.PopContext();
        }

        private static void BuildShop(GraphBuilder b)
        {
            var choices = EncounterState.Choices();
            var proceed = EncounterState.ShopButton("BG/ProceedButton");
            var skip = EncounterState.ShopButton("SkipTurns/ProceedLine/SkipButton");
            if (choices.Count == 0 && proceed == null && skip == null) return;
            b.PushContext(null);
            b.StartRow(BoardStop, strictColumns: true);
            for (int i = 0; i < choices.Count; i++)
                b.AddItem(ChoiceId(choices[i]), Choice(choices[i], i == 0));
            if (proceed != null)
                b.AddItem(ControlId.Structural("enc:proceed"), StripButton(proceed, () => GameLoc.Get("Proceed_Misc") ?? "Proceed"));
            if (skip != null)
                b.AddItem(ControlId.Structural("enc:skip"), StripButton(skip, () => (GameLoc.Get("SkipTurns_Misc") ?? "Skip remaining turns").TrimEnd(':', ' ')));
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
                OnFocus = () =>
                {
                    var target = EncounterState.SlotTarget(x);
                    var view = target ?? (AViewItem)EncounterState.ArtifactView(EncounterState.Artifact(x));
                    if (view != null) EncounterState.Hover(view);
                },
                OnActivate = () => { var target = EncounterState.SlotTarget(x); if (target != null) EncounterState.Click(target); },
                Details = () => ArtifactDetails(EncounterState.Artifact(x)),
                OnSecondary = () => SayDetails(ArtifactDetails(EncounterState.Artifact(x))),
            };
        }

        internal static string HeaderText(int x)
        {
            var parts = new List<string>();
            if (EncounterState.SlotTarget(x) != null) parts.Add(Strings.EncTarget);
            parts.Add(Strings.EncColumn(x + 1));
            int power = EncounterState.DicePower(x);
            if (power > 0) parts.Add(Strings.EncFigure(GameLoc.Get("DicePower_Misc") ?? "Dice Power", power.ToString()));
            if (EncounterState.Honey(x)) parts.Add(Strings.EncHoney);
            string artifact = ArtifactLabel(EncounterState.Artifact(x));
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
                OnFocus = () =>
                {
                    var view = EncounterState.TargetAt(x, y) ?? (AViewItem)EncounterState.Tile(x, y);
                    if (view != null) EncounterState.Hover(view);
                },
                // Enter gives the cell to a running selection when it takes it; otherwise nothing.
                OnActivate = () => { var target = EncounterState.TargetAt(x, y); if (target != null) EncounterState.Click(target); },
                Details = () => CellDetails(x, y),
                OnSecondary = () => SayDetails(CellDetails(x, y)),
            };
        }

        internal static string CellText(int x, int y)
        {
            string text = Strings.EncCoord(x + 1, y + 1) + ", " + CellContents(x, y);
            return EncounterState.TargetAt(x, y) != null ? Strings.EncTarget + ", " + text : text;
        }

        /// <summary>"Migo, 10 strength, shielded", "empty", or "no tile" where the grid has none yet,
        /// then a cloud.</summary>
        internal static string CellContents(int x, int y)
        {
            if (!EncounterState.Built(x, y)) return Strings.EncNoTile;
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
            parts.AddRange(UnitStates(unit));
            try { if (unit.MaxCharge > 0) parts.Add(Strings.EncCharge(unit.Charge, unit.MaxCharge)); }
            catch (Exception e) { CoreLog.Warning("EncounterScreen: reading a unit's charge failed: " + e.Message); }
            parts.RemoveAll(string.IsNullOrEmpty);
            return string.Join(", ", parts);
        }

        /// <summary>A unit's named states, charge apart: negative, Shielded, Shock.</summary>
        internal static List<string> UnitStates(EntityModel unit)
        {
            var states = new List<string>();
            try
            {
                if (unit.IsNegative) states.Add(Strings.EncNegative);
                if (unit.IsShielded) states.Add(GameLoc.Get("Shielded_Keyword") ?? "Shielded");
                if (unit.IsShocked) states.Add(GameLoc.Get("Shock_Keyword") ?? "Shock");
            }
            catch (Exception e) { CoreLog.Warning("EncounterScreen: reading a unit's state failed: " + e.Message); }
            states.RemoveAll(string.IsNullOrEmpty);
            return states;
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

        /// <summary>"Meteor, LV. 2, 9/20 XP": the artifact's name, its level past the first, its XP;
        /// null for none.</summary>
        internal static string ArtifactLabel(ArtifactModel artifact)
        {
            string name = EncounterState.ArtifactName(artifact);
            if (name == null) return null;
            var parts = new List<string> { name };
            var xp = EncounterState.ArtifactXp(artifact);
            if (xp.HasValue)
            {
                if (xp.Value.Level > 1)
                    parts.Add(Strings.EncFigure((GameLoc.Get("LevelShortened_Misc") ?? "LV.").Trim(), xp.Value.Level.ToString()));
                parts.Add(Strings.EncXp(xp.Value.Xp, xp.Value.Needed));
            }
            return string.Join(", ", parts);
        }

        // "Meteor [9/10 XP]: After you score a pair...", the game's title (its XP) leading, then the
        // rarity and scaling.
        private static IEnumerable<string> ArtifactDetails(ArtifactModel artifact)
        {
            if (artifact == null) yield break;
            var (title, lines) = EncounterState.ArtifactTooltip(artifact);
            title = title ?? EncounterState.ArtifactName(artifact);
            if (lines.Count == 0) { yield return title; yield break; }
            yield return title + ": " + lines[0];
            for (int i = 1; i < lines.Count; i++) yield return lines[i];
        }

        // ---- the dice ----

        // "3, frozen": the face (the column it scores) and the die's state.
        private static NodeVtable Die(Dice die, bool first)
        {
            Func<string> label = () => DieText(die);
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label) },
                SearchText = label,
                IsSelected = () => first,
                QuietVanish = true,
                OnFocus = () => EncounterState.Hover(die),
            };
        }

        internal static string DieText(Dice die)
        {
            var parts = new List<string>();
            try
            {
                parts.Add(die.isRolling ? Strings.EncRolling : die.Value.ToString());
                if (die.IsDangerDice) parts.Add(Strings.EncPilotDie);
                if (die.IsPermaFrozen) parts.Add(Strings.EncPermafrozen);
                else if (die.IsLockedIn) parts.Add(Strings.EncFrozen);
                if (die.IsCracked) parts.Add(Strings.EncCracked);
                if (die.AddedPower > 0) parts.Add(Strings.EncAddedPower(die.AddedPower));
            }
            catch (Exception e) { CoreLog.Warning("EncounterScreen: reading a die failed: " + e.Message); }
            return string.Join(", ", parts);
        }

        private static NodeVtable StripButton(AViewItem item, Func<string> label)
        {
            var vt = GameNodes.Button(label, () => EncounterState.Click(item));
            vt.OnFocus = () => EncounterState.Hover(item);
            vt.QuietVanish = true;
            return vt;
        }

        // ---- the shop ----

        // "Meteor, Artifact, 2 cash, frozen": the name, the kind, the price, then what stands out.
        // Enter buys it (the game's click; a unit, a tile or a swap then asks for its target on the
        // board); Backspace reads its tooltip.
        private static NodeVtable Choice(DraftChoice choice, bool first)
        {
            Func<string> label = () => ChoiceText(choice);
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label) },
                SearchText = () => ChoiceName(choice),
                IsSelected = () => first,
                QuietVanish = true,
                OnFocus = () => EncounterState.Hover(choice),
                OnActivate = () => EncounterState.Click(choice),
                Details = () => ChoiceDetails(choice),
                OnSecondary = () => SayDetails(ChoiceDetails(choice)),
            };
        }

        internal static string ChoiceName(DraftChoice choice)
        {
            try
            {
                switch (choice.DraftType)
                {
                    case DraftType.Entity:
                        return GameLoc.Get(choice.EntityName + "_Entity_Title") ?? choice.EntityName.ToString();
                    case DraftType.Artifact:
                        return GameLoc.Get(choice.ArtifactName + "_Arti_Title") ?? choice.ArtifactName.ToString();
                    default:
                        return GameText.Of(choice.Tooltip != null ? choice.Tooltip.TitleText : null) ?? choice.DraftType.ToString();
                }
            }
            catch (Exception e) { CoreLog.Warning("EncounterScreen: naming a shop choice failed: " + e.Message); return null; }
        }

        internal static string ChoiceText(DraftChoice choice)
        {
            var parts = new List<string> { ChoiceName(choice) };
            try
            {
                if (choice.DraftType == DraftType.Entity) parts.Add(GameLoc.Get("Unit_Misc"));
                else if (choice.DraftType == DraftType.Artifact) parts.Add(GameLoc.Get("Artifact_Misc"));
                parts.Add(Strings.EncCost(choice.DraftCost));
                if (choice.tempRarity != Rarity.Common) parts.Add(GameLoc.Get(choice.tempRarity + "_Misc") ?? choice.tempRarity.ToString());
                if (choice.isCursed) parts.Add(Strings.EncCursed);
                if (choice.IsLocked) parts.Add(Strings.EncFrozen);
                if (choice.CantBuyCostIcon != null && choice.CantBuyCostIcon.activeInHierarchy) parts.Add(Strings.EncCantAfford);
            }
            catch (Exception e) { CoreLog.Warning("EncounterScreen: reading a shop choice failed: " + e.Message); }
            parts.RemoveAll(string.IsNullOrEmpty);
            return string.Join(", ", parts);
        }

        // A unit's tooltip is its EntityTooltip; anything else's its Tooltip (the other holds the
        // prefab's placeholder text): "title: description" (an artifact's title with its XP, a unit's
        // the longer one it draws), then the rarity and the scaling, each only where switched on.
        private static IEnumerable<string> ChoiceDetails(DraftChoice choice)
        {
            var lines = new List<string>();
            try
            {
                bool unit = choice.DraftType == DraftType.Entity && choice.EntityTooltip != null;
                if (!unit && choice.Tooltip == null) return lines;
                var titles = unit ? new TMPro.TMP_Text[] { choice.EntityTooltip.TitleText, choice.EntityTooltip.TitleLongerText } : new TMPro.TMP_Text[] { choice.Tooltip.TitleText };
                var rest = unit ? new TMPro.TMP_Text[] { choice.EntityTooltip.RarityText } : new TMPro.TMP_Text[] { choice.Tooltip.RarityText, choice.Tooltip.ScalingText };
                string title = null;
                foreach (var tmp in titles) if (title == null) title = Shown(tmp);
                string description = Shown(unit ? choice.EntityTooltip.DescriptionText : choice.Tooltip.DescriptionText);
                title = title ?? ChoiceName(choice);
                lines.Add(description != null ? title + ": " + description : title);
                foreach (var tmp in rest) { string text = Shown(tmp); if (text != null) lines.Add(text); }
            }
            catch (Exception e) { CoreLog.Warning("EncounterScreen: reading a shop choice's tooltip failed: " + e.Message); }
            return lines;
        }

        // A tooltip text switched on for this kind (a spell has no scaling line): the tooltip itself
        // is hidden, so by its own switch.
        private static string Shown(TMPro.TMP_Text tmp) => tmp != null && tmp.gameObject.activeSelf ? GameText.Of(tmp) : null;

        // ---- the status ----

        private static void BuildStatus(GraphBuilder b)
        {
            b.BeginStop("enc:status");
            b.PushContext(null, positions: false);
            if (ActionTip() != null)
                b.AddItem(ControlId.Structural("enc:tip"), Stat(() => { var tip = ActionTip(); return tip != null ? Tips.Text(tip) : null; }));
            b.AddItem(ControlId.Structural("enc:target"), Stat(TargetLine));
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
                    Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => ArtifactLabel(EncounterState.Artifact(s))) },
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

        /// <summary>The target as the score panel names it ("Defeat the P.O.G. (Pluto Outer Guards)";
        /// a pilot, "Roxy, Trailblazer").</summary>
        public static string Target()
        {
            string pilot = EncounterState.PilotTarget();
            if (pilot != null) return pilot;
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

        /// <summary>"30/100 attack": Total Attack against Target Defense, the score panel's two.</summary>
        public static string Attack() =>
            Strings.EncAttack(EncounterState.Big(EncounterValue.TotalScore), EncounterState.Big(EncounterValue.TargetTributeScore));

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
            new KeyValuePair<string, Func<string>>("budge", Budge),
            new KeyValuePair<string, Func<string>>("chrono", Chrono),
            new KeyValuePair<string, Func<string>>("swap", () => Figure(Strings.EncSwap, EncounterValue.SwapToken)),
            new KeyValuePair<string, Func<string>>("minidice", () => Figure(GameLoc.Get("MiniDice_Keyword") ?? "Mini-Dice", EncounterValue.MiniDice)),
            new KeyValuePair<string, Func<string>>("cash", () => Figure(GameLoc.Get("ShopToken_TooltipTitle") ?? "Cash", EncounterValue.ShopToken)),
        };

        public static string Budge() => Figure(GameLoc.Get("Budge_Keyword") ?? "Budge", EncounterValue.BudgeToken);
        public static string Chrono() => Figure(GameLoc.Get("Chrono_Keyword") ?? "Chrono", EncounterValue.ChronoToken);

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
