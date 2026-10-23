using System.Collections.Generic;

namespace DiceVoders.Core.Encounter
{
    /// <summary>
    /// The encounter as plain data, taken by the module on a settled frame: what
    /// <see cref="EncounterDiff"/> compares. Figures come as the game prints them, with their values
    /// alongside for comparing; labels and names are the game's words, filled by the module.
    /// Coordinates are 0-based, y up, as the game's.
    /// </summary>
    public sealed class EncounterSnapshot
    {
        /// <summary>The game's phase name: "Player", "Scoring", "Draft"...</summary>
        public string Phase = "";
        /// <summary>The target as the score panel names it, and the act ("Act 1-2").</summary>
        public string TargetName, Act;
        public int TurnsLeft;
        /// <summary>"4 Turns", the game's words.</summary>
        public string TurnsText;
        public int Hearts;
        public double Total, Target;
        public string TotalText, TargetText;
        /// <summary>"TARGET REACHED!", the game's words.</summary>
        public string TargetReachedText;
        public double Mult;
        public string MultText, MultLabel;
        /// <summary>The tokens in a fixed order (Budge, Chrono, Swap, Mini-Dice, Cash), each its label and count.</summary>
        public List<Figure> Tokens = new List<Figure>();
        /// <summary>The base dice faces in the tray's order; 0 for one still rolling.</summary>
        public List<int> Dice = new List<int>();
        /// <summary>The attack each column gained this turn, as printed ("0" for none), left to right.</summary>
        public List<string> ColumnAttack = new List<string>();
        public List<UnitSnap> Units = new List<UnitSnap>();
        public List<ArtifactSnap> Artifacts = new List<ArtifactSnap>();
        /// <summary>The tiles the grid has, and those with a cloud, as (x, y).</summary>
        public HashSet<(int X, int Y)> Built = new HashSet<(int X, int Y)>(), Clouds = new HashSet<(int X, int Y)>();
        /// <summary>The columns (0-based) holding honey.</summary>
        public HashSet<int> Honey = new HashSet<int>();
        /// <summary>The shop's choices on offer, by name, in its order (empty outside the shop).</summary>
        public List<string> Shop = new List<string>();
        /// <summary>The cloud's name ("Cloudy"), the game's.</summary>
        public string CloudLabel;

        public bool DiceRolling => Dice.Contains(0);
    }

    public sealed class Figure
    {
        public string Key, Label;
        public int Value;
    }

    public sealed class UnitSnap
    {
        /// <summary>The game's id for the unit, stable while it lives.</summary>
        public string Id;
        public string Name;
        public int X, Y;
        public double Strength;
        public string StrengthText;
        /// <summary>Its named states ("Shielded", "Shock", "negative"), charge apart.</summary>
        public List<string> States = new List<string>();
        public int Charge, MaxCharge;
    }

    public sealed class ArtifactSnap
    {
        /// <summary>The game's id for the artifact, stable while it is held.</summary>
        public int Id;
        public string Name;
        /// <summary>Its slot: 0 to 5 are the columns'.</summary>
        public int Slot;
        public int Level;
        /// <summary>Its XP and what its next level takes; -1 for one that gains none.</summary>
        public int Xp, XpNeeded;
        /// <summary>Blocked from triggering (a pilot's: Xenn's Sol and Luna, Tippy).</summary>
        public bool Blocked;
    }
}
