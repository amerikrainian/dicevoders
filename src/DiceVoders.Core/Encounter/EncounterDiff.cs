using System;
using System.Collections.Generic;
using System.Globalization;
using S = DiceVoders.Core.Strings.Strings;

namespace DiceVoders.Core.Encounter
{
    /// <summary>
    /// What changed between two settled snapshots, as lines to say, in this order:
    /// <list type="number">
    /// <item>a new target ("Defeat the N.B.S. (Neptune Base Security), Act 1-2"), after which the
    /// round's resets (attack, turns, cash) go unsaid;</item>
    /// <item>the turn's scoring once the player phase is left with more attack, one line ("column 3:
    /// 20, column 5: 10, 30/100 attack"), when no <see cref="Step"/> said it column by column; attack
    /// changing otherwise ("40/100 attack");</item>
    /// <item>the target reached, a heart lost;</item>
    /// <item>units: destroyed ("Time Angel at 6, 1 destroyed"), added ("Migo added at 4, 1"), moved
    /// ("Migo moved to 3, 2"), strength ("all units +5 strength" when every one that stayed changed
    /// alike, else "Migo at 2, 1: 20 strength"), states gained and lost, charge;</item>
    /// <item>artifacts: added ("Meteor added, column 5"), removed, moved, levelled, XP ("Meteor: 9/10 XP"),
    /// blocked and freed ("Meteor: blocked", "Meteor: blocked lost");</item>
    /// <item>tiles: new ones, clouds coming and going, honey;</item>
    /// <item>the tokens that changed ("Chrono 4"), Mult rising;</item>
    /// <item>the shop opening, its offer changing other than by a purchase ("shop: Comet, Migo");</item>
    /// <item>a new turn with its roll ("3 Turns, rolled 2, 4, 6"), turns left changing within a phase, and
    /// dice rerolled within the turn ("rolled 1, 5, 6").</item>
    /// </list>
    /// </summary>
    public static class EncounterDiff
    {
        public const string PlayerPhase = "Player", DraftPhase = "Draft", ScoringPhase = "Scoring";

        public static List<string> Lines(EncounterSnapshot prev, EncounterSnapshot now)
        {
            var lines = new List<string>();
            if (prev == null || now == null) return lines;
            bool wasPlayer = prev.Phase == PlayerPhase, isPlayer = now.Phase == PlayerPhase;
            bool newTarget = !string.IsNullOrEmpty(now.TargetName) && now.TargetName != prev.TargetName;

            if (newTarget)
                lines.Add(string.IsNullOrEmpty(now.Act) ? now.TargetName : now.TargetName + ", " + now.Act);

            bool scored = wasPlayer && !isPlayer && now.Total > prev.Total;
            if (scored)
            {
                var parts = new List<string>();
                for (int i = 0; i < now.ColumnAttack.Count; i++)
                {
                    string attack = now.ColumnAttack[i];
                    if (!string.IsNullOrEmpty(attack) && attack != "0") parts.Add(S.EncColumnAttack(i + 1, attack));
                }
                parts.Add(S.EncAttack(now.TotalText, now.TargetText));
                lines.Add(string.Join(", ", parts));
            }
            else if (!newTarget && now.Total != prev.Total)
                lines.Add(S.EncAttack(now.TotalText, now.TargetText));
            if (!newTarget && prev.Total < prev.Target && now.Total >= now.Target && now.Target > 0 && !string.IsNullOrEmpty(now.TargetReachedText))
                lines.Add(now.TargetReachedText);
            if (now.Hearts < prev.Hearts)
                lines.Add(S.EncFigure(S.EncHearts, now.Hearts.ToString()));

            Units(prev, now, lines);
            Artifacts(prev, now, lines);
            Tiles(prev, now, lines);

            foreach (var token in now.Tokens)
            {
                var before = prev.Tokens.Find(t => t.Key == token.Key);
                if (before == null || before.Value == token.Value) continue;
                // A new round's fresh Cash is no news; nor the Mini-Dice spent as they are rolled in the
                // scoring, where each one's column is said (Step).
                if (newTarget && token.Key == "cash") continue;
                if (token.Key == "minidice" && token.Value < before.Value && now.Phase == ScoringPhase) continue;
                lines.Add(S.EncFigure(token.Label, token.Value.ToString()));
            }
            if (now.Mult > prev.Mult && now.Mult > 1 && !string.IsNullOrEmpty(now.MultLabel))
                lines.Add(S.EncFigure(now.MultLabel, now.MultText));

            if (now.Phase == DraftPhase && prev.Phase != DraftPhase)
                lines.Add(S.EncShop);
            // The offer arrives a moment after the shop opens: filled from empty is the opening, not
            // a change.
            else if (now.Phase == DraftPhase && prev.Phase == DraftPhase && prev.Shop.Count > 0 && now.Shop.Count > 0 && !Bought(prev.Shop, now.Shop))
                lines.Add(S.EncShopItems(string.Join(", ", now.Shop)));

            if (isPlayer && !now.DiceRolling)
            {
                if (!wasPlayer)
                    lines.Add(string.IsNullOrEmpty(now.TurnsText) ? Rolled(now) : now.TurnsText + ", " + Rolled(now));
                else if (!SameDice(prev, now))
                    lines.Add(Rolled(now));
            }
            // Turns left change with the phase as a matter of course (the new turn's line says them):
            // within a phase, they are news (an extra turn, a turn taken away).
            if (!newTarget && now.TurnsLeft != prev.TurnsLeft && now.Phase == prev.Phase && !string.IsNullOrEmpty(now.TurnsText))
                lines.Add(now.TurnsText);
            return lines;
        }

        /// <summary>
        /// One column's scoring, said as it ends: the column (1-based) with the attack it gained
        /// ("column 3: 20", a mini-die's "Mini-Dice, column 6: 30", several "3 Mini-Dice, column 1: 0"), any other column's gain in the same
        /// step, then everything else that changed in it, as <see cref="Lines"/> says it. A base die's
        /// column that changed nothing is not said (the player chose it); a mini-die's always is, as
        /// its roll. <paramref name="combo"/> is the label the game draws over a column scored with several
        /// dice ("column 4: 80, Pair"). <paramref name="dice"/> is how many dice the column scored with; <paramref name="gains"/> holds each column's gain as printed, left to right, null
        /// for none.
        /// </summary>
        public static List<string> Step(EncounterSnapshot prev, EncounterSnapshot now, int column, int dice, IList<string> gains, string miniLabel, string combo = null)
        {
            var rest = Lines(prev, now);
            bool any = !string.IsNullOrEmpty(miniLabel);
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(miniLabel)) parts.Add(dice > 1 ? S.EncDiceCount(dice, miniLabel) : miniLabel);
            string own = column >= 1 && column <= gains.Count ? gains[column - 1] : null;
            any |= own != null;
            parts.Add(S.EncColumnAttack(column, own ?? "0"));
            if (!string.IsNullOrEmpty(combo)) parts.Add(combo);
            for (int i = 0; i < gains.Count; i++)
                if (i != column - 1 && gains[i] != null) { parts.Add(S.EncColumnAttack(i + 1, gains[i])); any = true; }
            if (!any && rest.Count == 0) return rest;
            rest.Insert(0, string.Join(", ", parts));
            return rest;
        }

        private static string Place(int x, int y) => S.EncCoord(x + 1, y + 1);

        private static void Units(EncounterSnapshot prev, EncounterSnapshot now, List<string> lines)
        {
            var before = new Dictionary<string, UnitSnap>();
            foreach (var u in prev.Units) before[u.Id] = u;
            var after = new Dictionary<string, UnitSnap>();
            foreach (var u in now.Units) after[u.Id] = u;

            foreach (var u in prev.Units)
                if (!after.ContainsKey(u.Id)) lines.Add(S.EncUnitRemoved(u.Name, Place(u.X, u.Y)));
            foreach (var u in now.Units)
                if (!before.ContainsKey(u.Id)) lines.Add(S.EncUnitAdded(u.Name, Place(u.X, u.Y)));

            var stayed = new List<(UnitSnap Was, UnitSnap Is)>();
            foreach (var u in now.Units)
                if (before.TryGetValue(u.Id, out var was)) stayed.Add((was, u));

            foreach (var (was, u) in stayed)
                if (was.X != u.X || was.Y != u.Y) lines.Add(S.EncMoved(u.Name, Place(u.X, u.Y)));

            // Strength: one line when every unit that stayed changed by the same amount.
            var changed = stayed.FindAll(p => p.Was.Strength != p.Is.Strength);
            double first = changed.Count > 0 ? changed[0].Is.Strength - changed[0].Was.Strength : 0;
            if (changed.Count > 1 && changed.Count == stayed.Count
                && changed.TrueForAll(p => Math.Abs((p.Is.Strength - p.Was.Strength) - first) < 1e-9))
                lines.Add(S.EncAllStrength(Delta(first)));
            else
                foreach (var (was, u) in changed)
                    lines.Add(S.EncAt(S.EncUnitAt(u.Name, Place(u.X, u.Y)), S.EncStrength(u.StrengthText)));

            foreach (var (was, u) in stayed)
            {
                string who = S.EncUnitAt(u.Name, Place(u.X, u.Y));
                foreach (var state in u.States)
                    if (!was.States.Contains(state)) lines.Add(S.EncAt(who, state));
                foreach (var state in was.States)
                    if (!u.States.Contains(state)) lines.Add(S.EncAt(who, S.EncLost(state)));
                if ((was.Charge != u.Charge || was.MaxCharge != u.MaxCharge) && u.MaxCharge > 0)
                    lines.Add(S.EncAt(who, S.EncCharge(u.Charge, u.MaxCharge)));
            }
        }

        // "+5", "-2.5": a strength change as a signed number.
        private static string Delta(double d)
        {
            string n = Math.Abs(d) < 1e15 && d == Math.Floor(d) ? ((long)d).ToString(CultureInfo.InvariantCulture) : d.ToString("G4", CultureInfo.InvariantCulture);
            return d > 0 ? "+" + n : n;
        }

        private static void Artifacts(EncounterSnapshot prev, EncounterSnapshot now, List<string> lines)
        {
            var before = new Dictionary<int, ArtifactSnap>();
            foreach (var a in prev.Artifacts) before[a.Id] = a;
            var after = new Dictionary<int, ArtifactSnap>();
            foreach (var a in now.Artifacts) after[a.Id] = a;
            foreach (var a in prev.Artifacts)
                if (!after.ContainsKey(a.Id)) lines.Add(S.EncArtifactRemoved(a.Name));
            foreach (var a in now.Artifacts)
            {
                if (!before.TryGetValue(a.Id, out var was))
                {
                    lines.Add(a.Slot < 6 ? S.EncArtifactAdded(a.Name) + ", " + S.EncColumn(a.Slot + 1) : S.EncArtifactAdded(a.Name));
                    continue;
                }
                if (was.Slot != a.Slot && a.Slot < 6) lines.Add(S.EncMoved(a.Name, S.EncColumn(a.Slot + 1)));
                if (a.Level > was.Level) lines.Add(S.EncLevel(a.Name, a.Level));
                if (a.Xp >= 0 && (a.Xp != was.Xp || a.XpNeeded != was.XpNeeded)) lines.Add(S.EncAt(a.Name, S.EncXp(a.Xp, a.XpNeeded)));
                if (a.Blocked != was.Blocked) lines.Add(S.EncAt(a.Name, a.Blocked ? S.EncBlocked : S.EncLost(S.EncBlocked)));
            }
        }

        private static void Tiles(EncounterSnapshot prev, EncounterSnapshot now, List<string> lines)
        {
            foreach (var t in Sorted(now.Built))
                if (!prev.Built.Contains(t)) lines.Add(S.EncTileBuilt(Place(t.X, t.Y)));
            foreach (var t in Sorted(now.Clouds))
                if (!prev.Clouds.Contains(t) && !string.IsNullOrEmpty(now.CloudLabel)) lines.Add(S.EncAt(Place(t.X, t.Y), now.CloudLabel));
            foreach (var t in Sorted(prev.Clouds))
                if (!now.Clouds.Contains(t)) lines.Add(S.EncAt(Place(t.X, t.Y), S.EncClear));
            for (int x = 0; x < 6; x++)
            {
                if (now.Honey.Contains(x) && !prev.Honey.Contains(x)) lines.Add(S.EncAt(S.EncColumn(x + 1), S.EncHoney));
                if (prev.Honey.Contains(x) && !now.Honey.Contains(x)) lines.Add(S.EncAt(S.EncColumn(x + 1), S.EncLost(S.EncHoney)));
            }
        }

        // Reading order: top row first, left to right.
        private static List<(int X, int Y)> Sorted(HashSet<(int X, int Y)> tiles)
        {
            var list = new List<(int X, int Y)>(tiles);
            list.Sort((a, b) => a.Y != b.Y ? b.Y.CompareTo(a.Y) : a.X.CompareTo(b.X));
            return list;
        }

        // The offer lost items and gained none: something bought, not a reroll.
        private static bool Bought(List<string> before, List<string> after)
        {
            var left = new List<string>(before);
            foreach (var item in after)
                if (!left.Remove(item)) return false;
            return true;
        }

        private static string Rolled(EncounterSnapshot s) => S.EncRolled(string.Join(", ", s.Dice));

        // The faces as a set: the tray re-sorts its dice a moment after a budge, which is no roll.
        private static bool SameDice(EncounterSnapshot a, EncounterSnapshot b)
        {
            var x = new List<int>(a.Dice);
            var y = new List<int>(b.Dice);
            x.Sort();
            y.Sort();
            if (x.Count != y.Count) return false;
            for (int i = 0; i < x.Count; i++)
                if (x[i] != y[i]) return false;
            return true;
        }
    }
}
