using System;
using System.Collections.Generic;
using DiceVoders.Core;
using DiceVoders.Core.Strings;
using DiceVoders.Module.Screens;

namespace DiceVoders.Module.Encounter
{
    /// <summary>
    /// The lines of the buffers that read the encounter whatever has focus: the units, each as its
    /// cell reads it with its place after ("Migo, 10 strength, 2, 1"), top row first. Read live on
    /// every buffer keypress; empty outside a run.
    /// </summary>
    internal static class EncounterBuffers
    {
        public static IEnumerable<string> Units()
        {
            var lines = new List<string>();
            if (Encounter.Controller == null) return lines;
            try
            {
                for (int y = Encounter.Rows - 1; y >= 0; y--)
                    for (int x = 0; x < Encounter.Columns; x++)
                    {
                        var unit = Encounter.At(x, y);
                        if (unit == null) continue;
                        string line = EncounterScreen.Describe(unit) + ", " + Strings.EncCoord(x + 1, y + 1);
                        // Blocked leads, as on the cell: the unit will not score.
                        if (unit.IsBlocked || Encounter.TippyCovers(x)) line = Strings.EncBlocked + ", " + line;
                        lines.Add(line);
                    }
            }
            catch (Exception e) { CoreLog.Warning("EncounterBuffers: reading the units failed: " + e.Message); }
            return lines;
        }
    }
}
