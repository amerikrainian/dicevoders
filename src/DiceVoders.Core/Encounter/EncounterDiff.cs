using System.Collections.Generic;
using S = DiceVoders.Core.Strings.Strings;

namespace DiceVoders.Core.Encounter
{
    /// <summary>
    /// What changed between two settled snapshots, as lines to say, in this order: the turn's scoring
    /// once the player phase is left with more attack, one line ("column 3: 20, column 5: 10, 30/100
    /// attack"), the target reached, a heart lost, the shop opening, a new turn
    /// with its roll ("3 Turns, rolled 2, 4, 6"), and dice rerolled within the turn ("rolled 1, 5, 6").
    /// </summary>
    public static class EncounterDiff
    {
        public const string PlayerPhase = "Player", DraftPhase = "Draft";

        public static List<string> Lines(EncounterSnapshot prev, EncounterSnapshot now)
        {
            var lines = new List<string>();
            if (prev == null || now == null) return lines;
            bool wasPlayer = prev.Phase == PlayerPhase, isPlayer = now.Phase == PlayerPhase;

            if (wasPlayer && !isPlayer && now.Total > prev.Total)
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
            if (prev.Total < prev.Target && now.Total >= now.Target && now.Target > 0 && !string.IsNullOrEmpty(now.TargetReachedText))
                lines.Add(now.TargetReachedText);
            if (now.Hearts < prev.Hearts)
                lines.Add(S.EncFigure(S.EncHearts, now.Hearts.ToString()));
            if (now.Phase == DraftPhase && prev.Phase != DraftPhase)
                lines.Add(S.EncShop);
            if (isPlayer && !now.DiceRolling)
            {
                if (!wasPlayer)
                    lines.Add(string.IsNullOrEmpty(now.TurnsText) ? Rolled(now) : now.TurnsText + ", " + Rolled(now));
                else if (!SameDice(prev, now))
                    lines.Add(Rolled(now));
            }
            return lines;
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
