using System;
using System.Collections.Generic;
using DiceVoders.Core;
using DiceVoders.Core.Input;
using DiceVoders.Core.Strings;
using DiceVoders.Core.UI;
using DiceVoders.Module.Input;
using DiceVoders.Module.Screens;
using UnityEngine;

namespace DiceVoders.Module.Encounter
{
    /// <summary>
    /// The encounter's keys (the Game category, live while the encounter screen is). Read keys speak
    /// one thing and are silent where there is nothing to read: C the focused tile's coordinates (a
    /// header's column), Ctrl+A Total Attack and Target Defense, Ctrl+T turns left and hearts, Ctrl+R
    /// the tokens, Ctrl+D the dice, Ctrl+O the target. On the grid and its header row, Home and End go
    /// to the row's ends, Page Up and Page Down to the grid's top and bottom rows in the same column;
    /// elsewhere Home and End are the navigator's own. The game's own keys (A and S budge, F freeze,
    /// R reroll, E proceed) are not taken yet.
    /// </summary>
    internal static class EncounterKeys
    {
        public static void Register()
        {
            Read("enc.coords", "Coordinates", KeyCode.C, Coords);
            Read("enc.attack", "Attack and defense", KeyCode.A, () => Join(EncounterScreen.Attack(), EncounterScreen.Defense()), ctrl: true);
            Read("enc.turns", "Turns and hearts", KeyCode.T, () => Join(EncounterScreen.Turns(), EncounterScreen.Hearts()), ctrl: true);
            Read("enc.tokens", "Tokens", KeyCode.R, () =>
            {
                var parts = new List<string>();
                foreach (var token in EncounterScreen.TokenLines()) parts.Add(token.Value());
                return Join(parts.ToArray());
            }, ctrl: true);
            Read("enc.dice", "Dice", KeyCode.D, () =>
            {
                var parts = new List<string>();
                foreach (var die in Encounter.Dice()) parts.Add(EncounterScreen.DieText(die));
                return Join(parts.ToArray());
            }, ctrl: true);
            Read("enc.target", "Target", KeyCode.O, EncounterScreen.TargetLine, ctrl: true);

            InputManager.Register("enc.first_column", "First column", InputCategory.Game, () => Edge(UiActions.Home, (x, y) => (0, y)))
                .AddBinding(new KeyboardBinding(KeyCode.Home));
            InputManager.Register("enc.last_column", "Last column", InputCategory.Game, () => Edge(UiActions.End, (x, y) => (Encounter.Columns - 1, y)))
                .AddBinding(new KeyboardBinding(KeyCode.End));
            InputManager.Register("enc.top_row", "Top row", InputCategory.Game, () => Edge(null, (x, y) => (x, Encounter.Rows - 1)))
                .AddBinding(new KeyboardBinding(KeyCode.PageUp));
            InputManager.Register("enc.bottom_row", "Bottom row", InputCategory.Game, () => Edge(null, (x, y) => (x, 0)))
                .AddBinding(new KeyboardBinding(KeyCode.PageDown));
        }

        // A read key: speaks its line, interrupting; nothing when the line is null.
        private static void Read(string key, string label, KeyCode code, Func<string> line, bool ctrl = false)
        {
            InputManager.Register(key, label, InputCategory.Game, () =>
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
