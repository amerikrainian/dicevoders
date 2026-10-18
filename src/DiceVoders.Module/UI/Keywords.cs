using System;
using System.Collections.Generic;
using StarVaders;
using DiceVoders.Core;
using DiceVoders.Module.Interop;

namespace DiceVoders.Module.UI
{
    /// <summary>
    /// The game's keyword explanations, as it draws them beside a tooltip
    /// (<see cref="NestedTooltipController"/>): one box per <see cref="NestedTooltipName"/>, titled and
    /// described by <c>&lt;Name&gt;_TooltipTitle</c> and <c>_TooltipDesc</c> (<see cref="NestedTooltip.SetText"/>).
    /// Which ones a tooltip gets is the list its fill keeps (<c>_nestedTooltips</c>, set by
    /// <see cref="Tooltip.SetArtifactText"/>, <see cref="Tooltip.SetTextKey"/> and
    /// <see cref="EntityTooltip.UpdateTooltip"/> from the definition's <c>NestedTooltips</c>), shown
    /// with the tooltip by its <c>Show</c>. Also the board's own hover tooltips
    /// (<see cref="TooltipTriggerView"/>: hearts, the score, the pilot, shop rarity).
    /// </summary>
    internal static class Keywords
    {
        /// <summary>"Mini-Dice: Rolled and scored automatically each turn."</summary>
        public static string Line(NestedTooltipName name)
        {
            string title = GameLoc.Get(name + "_TooltipTitle")?.Trim();
            string desc = GameLoc.Get(name + "_TooltipDesc")?.Trim();
            if (desc == null) return title;
            return title != null ? title + ": " + desc : desc;
        }

        /// <summary>The lines of a tooltip's keyword list, in the game's order, each once.</summary>
        public static List<string> Lines(Il2CppSystem.Collections.Generic.List<NestedTooltipName> names)
        {
            var lines = new List<string>();
            if (names == null) return lines;
            try
            {
                foreach (var name in names)
                {
                    string line = Line(name);
                    if (line != null && !lines.Contains(line)) lines.Add(line);
                }
            }
            catch (Exception e) { CoreLog.Warning("Keywords: reading a keyword list failed: " + e.Message); }
            return lines;
        }

        /// <summary>The keywords an artifact's or spell's tooltip shows, once it is filled.</summary>
        public static List<string> Of(Tooltip tooltip) => tooltip != null ? Lines(tooltip._nestedTooltips) : new List<string>();

        /// <summary>The keywords a unit's tooltip shows, once it is filled.</summary>
        public static List<string> Of(EntityTooltip tooltip) => tooltip != null ? Lines(tooltip._nestedTooltips) : new List<string>();

        /// <summary>A hover tooltip on the board as drawn: "title: description" (each further line of
        /// the description its own), then its keywords. Empty when it has none.</summary>
        public static List<string> Trigger(UnityEngine.Transform trigger)
        {
            var lines = new List<string>();
            var view = trigger != null ? trigger.GetComponent<TooltipTriggerView>() : null;
            if (view == null || view.Tooltip == null) return lines;
            try
            {
                var tip = view.Tooltip.GetComponentInChildren<Tooltip>(true);
                if (tip == null) return lines;
                string title = GameText.Of(tip.TitleText);
                var desc = new List<string>();
                string raw = tip.DescriptionText != null ? tip.DescriptionText.text : null;
                if (raw != null)
                    foreach (var part in raw.Split('\n'))
                    {
                        string line = GameText.Clean(part)?.Trim();
                        if (!string.IsNullOrEmpty(line) && !GameText.IsMissing(line) && System.Linq.Enumerable.Any(line, char.IsLetterOrDigit)) desc.Add(line);
                    }
                if (desc.Count == 0) { if (title != null) lines.Add(title); }
                else
                {
                    lines.Add(title != null ? title + ": " + desc[0] : desc[0]);
                    for (int i = 1; i < desc.Count; i++) lines.Add(desc[i]);
                }
                foreach (var line in Of(tip)) if (!lines.Contains(line)) lines.Add(line);
            }
            catch (Exception e) { CoreLog.Warning("Keywords: reading " + trigger.name + "'s tooltip failed: " + e.Message); }
            return lines;
        }
    }
}
