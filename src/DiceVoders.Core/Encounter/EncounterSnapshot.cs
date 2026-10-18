using System.Collections.Generic;

namespace DiceVoders.Core.Encounter
{
    /// <summary>
    /// The encounter as plain data, taken by the module on a settled frame: what
    /// <see cref="EncounterDiff"/> compares. Figures come as the game prints them, with their values
    /// alongside for comparing; labels are the game's words, filled by the module.
    /// </summary>
    public sealed class EncounterSnapshot
    {
        /// <summary>The game's phase name: "Player", "Scoring", "Draft"...</summary>
        public string Phase = "";
        public int TurnsLeft;
        /// <summary>"4 Turns", the game's words.</summary>
        public string TurnsText;
        public int Hearts;
        public double Total, Target;
        public string TotalText, TargetText;
        /// <summary>"TARGET REACHED!", the game's words.</summary>
        public string TargetReachedText;
        /// <summary>The base dice faces in the tray's order; 0 for one still rolling.</summary>
        public List<int> Dice = new List<int>();
        /// <summary>The attack each column gained this turn, as printed ("0" for none), left to right.</summary>
        public List<string> ColumnAttack = new List<string>();

        public bool DiceRolling => Dice.Contains(0);
    }
}
