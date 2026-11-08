using System;
using System.Collections.Generic;
using DiceVoders.Core;
using DiceVoders.Core.Strings;
using DiceVoders.Module.Screens;
using StarVaders;

namespace DiceVoders.Module.Encounter
{
    /// <summary>
    /// The lines of the buffers that read the encounter whatever has focus: the units, each as its
    /// cell reads it with its place after ("Migo, 10 strength, 2, 1"), top row first; the perks, the
    /// columns' first by column ("column 4, Meteor, 0/10 XP: After you score..."), then the starter
    /// perk, then the others in slot order, as the status stop has them, each with its description
    /// (a pilot's artifact, the target's, left out). Read live on every buffer keypress; empty outside
    /// a run.
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

        public static IEnumerable<string> Perks()
        {
            var lines = new List<string>();
            try
            {
                foreach (int slot in PerkSlots())
                    lines.Add(PerkLine(Encounter.Artifact(slot), slot < Encounter.Columns ? Strings.EncColumn(slot + 1) : null));
            }
            catch (Exception e) { CoreLog.Warning("EncounterBuffers: reading the perks failed: " + e.Message); }
            return lines;
        }

        /// <summary>The artifacts' names alone ("Meteor", "4D Printer"), in the perks' order, locked
        /// slots left out.</summary>
        public static List<string> ArtifactNames()
        {
            var names = new List<string>();
            try
            {
                foreach (int slot in PerkSlots())
                {
                    var artifact = Encounter.Artifact(slot);
                    if (!Encounter.LockedSlot(artifact)) names.Add(Encounter.ArtifactName(artifact));
                }
            }
            catch (Exception e) { CoreLog.Warning("EncounterBuffers: reading the artifacts' names failed: " + e.Message); }
            return names;
        }

        // The slots holding an artifact: the columns', the starter perk, then the others in slot
        // order; a pilot's left out.
        private static List<int> PerkSlots()
        {
            var columns = new List<int>();
            var starters = new List<int>();
            var perks = new List<int>();
            var model = Encounter.Model;
            if (Encounter.Controller == null || model == null) return columns;
            int slots = model.ArtifactSlots?.Count ?? 0;
            for (int slot = 0; slot < slots; slot++)
            {
                var artifact = Encounter.Artifact(slot);
                if (artifact == null) continue;
                if (slot < Encounter.Columns) { columns.Add(slot); continue; }
                if (Encounter.PilotName(artifact) != null) continue;
                if (artifact.ArtifactType == ArtifactType.Starter) starters.Add(slot);
                else perks.Add(slot);
            }
            columns.AddRange(starters);
            columns.AddRange(perks);
            return columns;
        }

        // "column 4, Meteor, 0/10 XP: After you score a pair...": the artifact as the board reads it,
        // then its description.
        private static string PerkLine(ArtifactModel artifact, string column)
        {
            string label = EncounterScreen.ArtifactLabel(artifact);
            if (label == null) return null;
            if (column != null) label = column + ", " + label;
            var description = Encounter.ArtifactLines(artifact);
            return description.Count > 0 ? label + ": " + description[0] : label;
        }
    }
}
