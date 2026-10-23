using System;
using System.Collections.Generic;
using DiceVoders.Core;
using DiceVoders.Core.Graph;
using DiceVoders.Core.Input;
using DiceVoders.Core.Strings;
using DiceVoders.Core.UI;
using DiceVoders.Module.Input;
using DiceVoders.Module.Interop;
using DiceVoders.Module.Screens;
using DiceVoders.Module.UI;
using StarVaders;
using UnityEngine;

namespace DiceVoders.Module.Encounter
{
    /// <summary>
    /// The encounter's keys (the Game category, live while the encounter screen is). Read keys speak
    /// one thing and are silent where there is nothing to read: C the focused tile's coordinates (a
    /// header's column); in the Run category (<see cref="RunKeys"/>: over the game menu, the result
    /// screen and the other overlays too) Ctrl+A the attack against the target ("30/100 attack"), Ctrl+T
    /// turns left and hearts, Ctrl+R the tokens, Ctrl+D the dice, Ctrl+O the target, Ctrl+M Mult. 1 to
    /// 6 read that column whole (its header, then its cells top first), from anywhere in the encounter;
    /// Shift with them goes there (the same row from a cell, else the header). Period and comma step
    /// to the next and previous target while the game waits on one, else unit, in reading order. Alt+D
    /// goes to the strip under the grid (the dice, the shop, a pilot's rewards), Alt+Backspace back to
    /// where the last of these jumps left from. On the grid and its header row, Home and End go
    /// to the row's ends, Page Up and Page Down to the grid's top and bottom rows in the same column;
    /// elsewhere Home and End are the navigator's own. The game's own keys are its actions' keys, read
    /// from the game (<see cref="GameKeyBinding"/>, so its rebinding carries over), and run its own
    /// handlers (<see cref="EncounterKeyInputController"/>, which read nothing of the key press), with
    /// the focused item hovered first, as the game's would be: S and A budge the focused die up and
    /// down, F freezes it, R rerolls with Chrono, E proceeds (launch, leave the shop), Q cancels (undoes
    /// a budge). A budge or a freeze says the die after; one refused says so. Ctrl+M reads Mult;
    /// Ctrl+S opens the game menu on its stats panel.
    /// </summary>
    internal static class EncounterKeys
    {
        public static void Register()
        {
            Read("enc.coords", "Coordinates", KeyCode.C, Coords);
            // 1 to 6: a column read whole, where the focus is: a die's face names the column it scores.
            for (int n = 1; n <= 6; n++)
            {
                int x = n - 1;
                Read("enc.column" + n, "Read column " + n, KeyCode.Alpha0 + n, () => ColumnLine(x));
                InputManager.Register("enc.go_column" + n, "Go to column " + n, InputCategory.Game, () => GoToColumn(x))
                    .AddBinding(new KeyboardBinding(KeyCode.Alpha0 + n, shift: true));
            }
            // The run's figures (RunKeys): live over the game menu and the other overlays too.
            Read("enc.attack", "Attack", KeyCode.A, EncounterScreen.Attack, ctrl: true, category: InputCategory.Run);
            Read("enc.turns", "Turns and hearts", KeyCode.T, () => Join(EncounterScreen.Turns(), EncounterScreen.Hearts()), ctrl: true, category: InputCategory.Run);
            Read("enc.tokens", "Tokens", KeyCode.R, () =>
            {
                var parts = new List<string>();
                foreach (var token in EncounterScreen.TokenLines()) parts.Add(token.Value());
                return Join(parts.ToArray());
            }, ctrl: true, category: InputCategory.Run);
            Read("enc.dice", "Dice", KeyCode.D, DiceLine, ctrl: true, category: InputCategory.Run);
            Read("enc.target", "Target", KeyCode.O, EncounterScreen.TargetLine, ctrl: true, category: InputCategory.Run);
            InputManager.Register("enc.stats", "Stats", InputCategory.Game, Stats)
                .AddBinding(new KeyboardBinding(KeyCode.S, ctrl: true));
            Read("enc.mult", "Mult", KeyCode.M, EncounterScreen.Mult, ctrl: true, category: InputCategory.Run);

            InputManager.Register("enc.budge_up", "Budge up", InputCategory.Game, () => Budge(up: true))
                .AddBinding(GameKey("Increment", KeyCode.S));
            InputManager.Register("enc.budge_down", "Budge down", InputCategory.Game, () => Budge(up: false))
                .AddBinding(GameKey("Decrement", KeyCode.A));
            InputManager.Register("enc.freeze", "Freeze", InputCategory.Game, Freeze)
                .AddBinding(GameKey("Freeze", KeyCode.F));
            InputManager.Register("enc.reroll", "Reroll", InputCategory.Game, Reroll)
                .AddBinding(GameKey("Reroll", KeyCode.R));
            InputManager.Register("enc.proceed", "Proceed", InputCategory.Game, () => Run("proceed", k => k.OnProceed(Context())))
                .AddBinding(GameKey("Proceed", KeyCode.E));
            InputManager.Register("enc.cancel", "Cancel", InputCategory.Game, Cancel)
                .AddBinding(GameKey("Cancel", KeyCode.Q));

            // Period and comma: the next and previous target in a choice, else unit, in reading order.
            InputManager.Register("enc.next", "Next unit or target", InputCategory.Game, () => Step(1))
                .AddBinding(new KeyboardBinding(KeyCode.Period)).Repeating();
            InputManager.Register("enc.prev", "Previous unit or target", InputCategory.Game, () => Step(-1))
                .AddBinding(new KeyboardBinding(KeyCode.Comma)).Repeating();

            // Alt+D: the dice, the shop or the rewards under the grid; Alt+Backspace: back where a jump left.
            InputManager.Register("enc.strip", "Go to the dice or the shop", InputCategory.Game, () => JumpTo(EncounterScreen.StripFirst()))
                .AddBinding(new KeyboardBinding(KeyCode.D, alt: true));
            InputManager.Register("enc.jump_back", "Jump back", InputCategory.Game, JumpBack)
                .AddBinding(new KeyboardBinding(KeyCode.Backspace, alt: true));

            InputManager.Register("enc.first_column", "First column", InputCategory.Game, () => Edge(UiActions.Home, (x, y) => (0, y)))
                .AddBinding(new KeyboardBinding(KeyCode.Home));
            InputManager.Register("enc.last_column", "Last column", InputCategory.Game, () => Edge(UiActions.End, (x, y) => (Encounter.Columns - 1, y)))
                .AddBinding(new KeyboardBinding(KeyCode.End));
            InputManager.Register("enc.top_row", "Top row", InputCategory.Game, () => Edge(null, (x, y) => (x, Encounter.Rows - 1)))
                .AddBinding(new KeyboardBinding(KeyCode.PageUp));
            InputManager.Register("enc.bottom_row", "Bottom row", InputCategory.Game, () => Edge(null, (x, y) => (x, 0)))
                .AddBinding(new KeyboardBinding(KeyCode.PageDown));
        }

        private static StarVaders.InputHandler Handler() => Encounter.Controller?.EncounterInputHandler;

        private static GameKeyBinding GameKey(string action, KeyCode defaultKey) => new GameKeyBinding(action, defaultKey, Handler);

        private static EncounterKeyInputController KeyController()
        {
            foreach (var k in GameScenes.Components<EncounterKeyInputController>())
                if (k != null) return k;
            return null;
        }

        // The handlers' callback context, which they never read: a fresh one (the interop's
        // CallbackContext is a reference type, so default is null and the call throws).
        private static UnityEngine.InputSystem.InputAction.CallbackContext Context() => new UnityEngine.InputSystem.InputAction.CallbackContext();

        // Run one of the game's key handlers, as its own key press would.
        private static bool Run(string what, Action<EncounterKeyInputController> handler)
        {
            var keys = KeyController();
            if (keys == null) { CoreLog.Warning("EncounterKeys: no key controller for " + what); return false; }
            try { handler(keys); return true; }
            catch (Exception e) { CoreLog.Warning("EncounterKeys: " + what + " failed: " + e.Message); return false; }
        }

        // ---- the dice ----

        // S and A: the focused die one up or down, as the game's Increment and Decrement (they budge
        // the hovered die). The new face is said once the die stops (with the Budge left), and taken
        // as the events' baseline, so it is not said again as a roll.
        private static void Budge(bool up)
        {
            var die = EncounterScreen.FocusedDie();
            if (die == null) return;
            bool can;
            try { can = up ? die.CanBudgeUp() : die.CanBudgeDown(); }
            catch (Exception e) { CoreLog.Warning("EncounterKeys: asking the die failed: " + e.Message); can = false; }
            if (!can) { Speech.Say(Strings.EncCantBudge, interrupt: true); return; }
            int before = die.Value;
            Encounter.Hover(die);
            if (!Run("budge", k => { if (up) k.OnIncrement(Context()); else k.OnDecrement(Context()); })) return;
            AfterDie(die, 0, () =>
            {
                if (die.Value == before) { Speech.Say(Strings.EncCantBudge, interrupt: true); return; }
                EncounterEvents.Rebase();
                Speech.Say(EncounterScreen.DieText(die) + ", " + EncounterScreen.Budge(), interrupt: true);
            });
        }

        // F: freeze or unfreeze the focused die or shop choice, as the game's Freeze on the hovered one.
        private static void Freeze()
        {
            var choice = EncounterScreen.FocusedChoice();
            if (choice != null) { FreezeChoice(choice); return; }
            var die = EncounterScreen.FocusedDie();
            if (die == null) return;
            bool before = die.IsLockedIn;
            Encounter.Hover(die);
            if (!Run("freeze", k => k.OnFreeze(Context()))) return;
            AfterDie(die, 0, () =>
            {
                if (die.IsLockedIn == before) { Speech.Say(Strings.EncCantFreeze, interrupt: true); return; }
                Speech.Say(EncounterScreen.DieText(die), interrupt: true);
            });
        }

        private static void FreezeChoice(DraftChoice choice)
        {
            bool before = choice.IsLocked;
            Encounter.Hover(choice);
            if (!Run("freeze", k => k.OnFreeze(Context()))) return;
            Later.Frames(3, () =>
            {
                if (choice.IsLocked == before) { Speech.Say(Strings.EncCantFreeze, interrupt: true); return; }
                Speech.Say(EncounterScreen.ChoiceText(choice), interrupt: true);
            }, "freeze");
        }

        /// <summary>The game's Menu key: opens the pause menu, or resumes from it (ignored while a tip shows).</summary>
        public static bool Menu() => Encounter.Controller != null && Run("menu", k => k.OnMenu(Context()));

        // Ctrl+S: the game menu opened (the Menu key) on its stats panel.
        private static void Stats()
        {
            PauseScreen.LandOnStats();
            if (!Menu()) PauseScreen.CancelStatsLanding();
        }

        /// <summary>Escape in a target choice: the game's Cancel, which drops it.</summary>
        public static void CancelSelection() => Run("cancel", k => k.OnCancel(Context()));

        // R: reroll the dice that are not frozen with Chrono; the roll is said by the events. With no
        // Chrono left, says so (the game ignores the key); refused otherwise, says that.
        internal static void Reroll()
        {
            if ((Encounter.Int(EncounterValue.ChronoToken) ?? 0) <= 0)
            {
                Speech.Say(EncounterScreen.Chrono(), interrupt: true);
                return;
            }
            int before = Encounter.Int(EncounterValue.ChronoToken) ?? 0;
            if (!Run("reroll", k => k.OnReroll(Context()))) return;
            // Refused (a tip up, the wrong phase): nothing spent.
            Later.Frames(10, () =>
            {
                if ((Encounter.Int(EncounterValue.ChronoToken) ?? 0) == before) Speech.Say(Strings.EncCantReroll, interrupt: true);
            }, "reroll");
        }

        // Q: the game's cancel (undoes a budge, leaves a choice); dice it changes are said and taken
        // as the baseline.
        internal static void Cancel()
        {
            string before = Faces();
            EncounterEvents.Hold(10);
            if (!Run("cancel", k => k.OnCancel(Context()))) return;
            Later.Frames(5, () =>
            {
                if (Faces() == before) return;
                EncounterEvents.Rebase();
                Speech.Say(DiceLine() + ", " + EncounterScreen.Budge(), interrupt: true);
            }, "cancel");
        }

        private static string Faces()
        {
            var faces = new List<string>();
            foreach (var die in Encounter.Dice()) faces.Add(die.Value.ToString());
            return string.Join(",", faces);
        }

        private static string DiceLine()
        {
            var parts = new List<string>();
            foreach (var die in Encounter.Dice()) parts.Add(EncounterScreen.DieText(die));
            var pilotDie = Encounter.PilotDie();
            if (pilotDie != null) parts.Add(EncounterScreen.DieText(pilotDie));
            return Join(parts.ToArray());
        }

        // Runs the action once the die has stopped rolling (a budge spins it), a few frames on at
        // least, giving up after a second or so.
        private static void AfterDie(Dice die, int waited, Action action)
        {
            Later.Frames(3, () =>
            {
                if (die != null && die.isRolling && waited < 60) { AfterDie(die, waited + 3, action); return; }
                action();
            }, "die");
        }

        // A read key: speaks its line, interrupting; nothing when the line is null.
        private static void Read(string key, string label, KeyCode code, Func<string> line, bool ctrl = false,
            InputCategory category = InputCategory.Game)
        {
            InputManager.Register(key, label, category, () =>
            {
                string text = null;
                try { text = line(); }
                catch (Exception e) { CoreLog.Warning("EncounterKeys: " + key + " failed: " + e.Message); }
                if (!string.IsNullOrEmpty(text)) Speech.Say(text, interrupt: true);
            }).AddBinding(new KeyboardBinding(code, ctrl: ctrl));
        }

        private static string Join(params string[] parts)
        {
            var list = new List<string>();
            foreach (var p in parts) if (!string.IsNullOrEmpty(p)) list.Add(p);
            return list.Count > 0 ? string.Join(", ", list) : null;
        }

        // ---- jumps ----

        private static ControlId _back;

        /// <summary>Move the focus to the node, spoken as a move, remembering where it was; on the
        /// node already, it is read again.</summary>
        private static void JumpTo(ControlId id)
        {
            var nav = Navigation.Active as GraphNavigator;
            var from = nav?.FocusedNode?.Id;
            if (id == null || nav == null) return;
            // Already there: read again, interrupting, as a move would (AnnounceCurrent queues).
            if (Equals(from, id)) { Speech.Say(GraphAnnouncer.ComposeFull(nav.FocusedNode), interrupt: true); return; }
            if (nav.MoveTo(id)) _back = from;
        }

        // Alt+Backspace: where the last jump left from (a jump itself, so pressed again it returns).
        private static void JumpBack()
        {
            if (_back != null) JumpTo(_back);
        }

        // Shift+1 to 6: that column, on the same row from a cell, else its header.
        private static void GoToColumn(int x)
        {
            if (Encounter.Controller == null || x >= Encounter.Columns) return;
            var tile = EncounterScreen.FocusedTile();
            JumpTo(tile.HasValue ? EncounterScreen.TileId(x, tile.Value.Y) : EncounterScreen.ColumnId(x));
        }

        // ---- stepping ----

        // The board's places in reading order: the headers, then the cells top row first. While the
        // game waits on a target, the headers and cells that take it; otherwise the cells with a unit.
        private static List<ControlId> Members()
        {
            var members = new List<ControlId>();
            bool selecting = Encounter.Selecting;
            if (selecting)
                for (int x = 0; x < Encounter.Columns; x++)
                    if (Encounter.SlotTarget(x) != null) members.Add(EncounterScreen.ColumnId(x));
            for (int y = Encounter.Rows - 1; y >= 0; y--)
                for (int x = 0; x < Encounter.Columns; x++)
                    if (selecting ? Encounter.TargetAt(x, y) != null : Encounter.At(x, y) != null)
                        members.Add(EncounterScreen.TileId(x, y));
            return members;
        }

        // The focus's place in that order: a header's index, a cell's after the headers; -1 off the board.
        private static int Place()
        {
            var tile = EncounterScreen.FocusedTile();
            if (tile.HasValue) return Encounter.Columns + (Encounter.Rows - 1 - tile.Value.Y) * Encounter.Columns + tile.Value.X;
            return EncounterScreen.FocusedColumn() ?? -1;
        }

        private static int Place(ControlId id)
        {
            string key = id.StructuralKey as string;
            if (key.StartsWith("enc:col:")) return int.Parse(key.Substring("enc:col:".Length));
            var parts = key.Substring("enc:tile:".Length).Split(',');
            return Encounter.Columns + (Encounter.Rows - 1 - int.Parse(parts[1])) * Encounter.Columns + int.Parse(parts[0]);
        }

        // Period and comma: the next or previous member after the focus, wrapping; from off the board,
        // the first or the last. "nothing" when there is none.
        private static void Step(int step)
        {
            if (Encounter.Controller == null) return;
            List<ControlId> members;
            try { members = Members(); }
            catch (Exception e) { CoreLog.Warning("EncounterKeys: reading the board's members failed: " + e.Message); return; }
            if (members.Count == 0) { Speech.Say(Strings.EncNothing, interrupt: true); return; }
            int at = Place();
            ControlId to = null;
            if (step > 0)
            {
                foreach (var m in members) if (at < 0 || Place(m) > at) { to = m; break; }
                to = to ?? members[0];
            }
            else
            {
                for (int i = members.Count - 1; i >= 0; i--) if (at < 0 || Place(members[i]) < at) { to = members[i]; break; }
                to = to ?? members[members.Count - 1];
            }
            JumpTo(to);
        }

        // "column 4, Dice Power 2, Meteor, 0/10 XP, 4, 3, Migo, 10 strength, 4, 2, empty, 4, 1, no
        // tile": the header, then the cells top first, each as focusing it reads it.
        private static string ColumnLine(int x)
        {
            if (Encounter.Controller == null || x >= Encounter.Columns) return null;
            var parts = new List<string> { EncounterScreen.HeaderText(x) };
            for (int y = Encounter.Rows - 1; y >= 0; y--) parts.Add(EncounterScreen.CellText(x, y));
            return Join(parts.ToArray());
        }

        private static string Coords()
        {
            var tile = EncounterScreen.FocusedTile();
            if (tile.HasValue) return Strings.EncCoord(tile.Value.X + 1, tile.Value.Y + 1);
            var column = EncounterScreen.FocusedColumn();
            return column.HasValue ? Strings.EncColumn(column.Value + 1) : null;
        }

        // Home and End: the row's first and last column (on the header row, its first and last
        // header); Page Up and Page Down: the grid's top and bottom row, from a header too. Silent
        // when already there. Off the grid, Home and End are the navigator's own (these keys shadow them).
        private static void Edge(string uiKey, Func<int, int, (int X, int Y)> to)
        {
            var tile = EncounterScreen.FocusedTile();
            var column = EncounterScreen.FocusedColumn();
            if (column == null)
            {
                // A row off the grid (the dice, the shop, a reward screen): along it.
                if (uiKey != null && (Navigation.Active as GraphNavigator)?.JumpRowEdge(uiKey == UiActions.Home) == true) return;
                var ui = uiKey != null ? InputManager.Find(uiKey) : null;
                if (ui != null) Navigation.DispatchJustPressed(ui);
                return;
            }
            if (tile == null)
            {
                // A header: along the header row, or down into the grid.
                if (uiKey != null)
                {
                    var (hx, _) = to(column.Value, 0);
                    if (hx != column.Value) Navigation.Active?.MoveTo(EncounterScreen.ColumnId(hx));
                    return;
                }
                var (cx, cy) = to(column.Value, -1);
                if (cy >= 0) Navigation.Active?.MoveTo(EncounterScreen.TileId(cx, cy));
                return;
            }
            var target = to(tile.Value.X, tile.Value.Y);
            if (target.Equals(tile.Value)) return;
            Navigation.Active?.MoveTo(EncounterScreen.TileId(target.X, target.Y));
        }
    }
}
