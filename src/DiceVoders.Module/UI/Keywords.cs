using System;
using System.Collections.Generic;
using StarVaders;
using DiceVoders.Core;
using DiceVoders.Module.Interop;

namespace DiceVoders.Module.UI
{
    /// <summary>
    /// What the game draws beside a tooltip: its keyword explanations
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

        /// <summary>What an artifact's or spell's tooltip shows beside it, once it is filled: its
        /// keywords, then the unit it creates (<see cref="Nested"/>).</summary>
        public static List<string> Of(StarVaders.Tooltip tooltip)
        {
            if (tooltip == null) return new List<string>();
            var lines = Lines(tooltip._nestedTooltips);
            Nested(lines, tooltip.NestedEntityTooltipObject, tooltip.NestedEntityToolTip);
            return lines;
        }

        /// <summary>What a unit's tooltip shows beside it, once it is filled: its keywords, then the
        /// unit it creates (<see cref="Nested"/>).</summary>
        public static List<string> Of(EntityTooltip tooltip)
        {
            if (tooltip == null) return new List<string>();
            var lines = Lines(tooltip._nestedTooltips);
            Nested(lines, tooltip.NestedEntityTooltipObject, tooltip.NestedEntityToolTip);
            return lines;
        }

        // The unit a unit or artifact creates (its definition's NestedEntity: the Weaver's Web), whose
        // whole tooltip the game draws beside the other while it is switched on: "Web: When scored:
        // ...", its rarity, its strength, its own keywords.
        private static void Nested(List<string> lines, UnityEngine.GameObject shown, EntityTooltip nested)
        {
            if (shown == null || nested == null || !shown.activeSelf) return;
            try
            {
                string title = null;
                foreach (var tmp in new TMPro.TMP_Text[] { nested.TitleLongerText, nested.TitleText })
                    if (title == null && tmp != null && tmp.gameObject.activeSelf) title = GameText.Of(tmp);
                string desc = nested.DescriptionText != null && nested.DescriptionText.gameObject.activeSelf ? GameText.Of(nested.DescriptionText) : null;
                if (title == null && desc == null) return;
                lines.Add(title != null && desc != null ? title + ": " + desc : title ?? desc);
                if (nested.RarityText != null && nested.RarityText.gameObject.activeSelf)
                {
                    string rarity = GameText.Of(nested.RarityText);
                    if (rarity != null) lines.Add(rarity);
                }
                if (nested.AttackScoreText != null && nested.AttackScoreText.gameObject.activeInHierarchy)
                {
                    string strength = GameText.Of(nested.AttackScoreText);
                    if (strength != null) lines.Add(DiceVoders.Core.Strings.Strings.EncStrength(strength));
                }
                foreach (var line in Lines(nested._nestedTooltips)) if (!lines.Contains(line)) lines.Add(line);
            }
            catch (Exception e) { CoreLog.Warning("Keywords: reading a nested unit's tooltip failed: " + e.Message); }
        }

        private static readonly System.Text.RegularExpressions.Regex Coloured =
            new System.Text.RegularExpressions.Regex(@"<color=#([0-9A-Fa-f]{6})>([^<]*)</color>");
        private static readonly System.Text.RegularExpressions.Regex Bar =
            new System.Text.RegularExpressions.Regex(@"\s*\|\s*");
        private static readonly Rarity[] Rarities = { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Legendary };

        // A line whose figures the game tells apart by rarity colour alone
        // (<see cref="ContentGetter.GetRarityTextColor"/>): each named ("Common 84%"), its bars commas.
        private static string RarityFigures(string line)
        {
            bool named = false;
            string text = Coloured.Replace(line, m =>
            {
                string name = RarityOfColour(m.Groups[1].Value);
                if (name == null) return m.Value;
                named = true;
                return name + " " + m.Groups[2].Value.Trim();
            });
            return named ? Bar.Replace(text, ", ") : line;
        }

        private static string RarityOfColour(string hex)
        {
            try
            {
                foreach (var rarity in Rarities)
                {
                    var c = (UnityEngine.Color32)ContentGetter.GetRarityTextColor(rarity);
                    if (string.Equals(c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2"), hex, StringComparison.OrdinalIgnoreCase))
                        return GameLoc.Get(rarity + "_Misc") ?? rarity.ToString();
                }
            }
            catch (Exception e) { CoreLog.Warning("Keywords: matching a rarity colour failed: " + e.Message); }
            return null;
        }

        /// <summary>A hover tooltip on the board as drawn: "title: description" (each further line of
        /// the description its own), then its keywords. Empty when it has none. Figures coloured as a
        /// rarity read with its name (shop rarity's "Current chances: Common 84%, Uncommon 13%, ...").</summary>
        public static List<string> Trigger(UnityEngine.Transform trigger)
        {
            var view = trigger != null ? trigger.GetComponent<TooltipTriggerView>() : null;
            return view != null ? Tooltip(view.Tooltip) : new List<string>();
        }

        /// <summary>A hover tooltip object's <see cref="Tooltip"/> as drawn, as <see cref="Trigger"/>
        /// reads it (a button's own, <see cref="ButtonView.Tooltip"/>). <paramref name="localized"/>:
        /// the title and description are their <c>LocalizeStringEvent</c>s' strings, for a tooltip whose
        /// texts only those fill: hidden, they keep the prefab's placeholder ("4D-принтер") until the
        /// game first shows them, and a disabled event refreshes nothing.</summary>
        public static List<string> Tooltip(UnityEngine.GameObject tooltip, bool localized = false)
        {
            var lines = new List<string>();
            if (tooltip == null) return lines;
            string name = tooltip.name;
            try
            {
                var tip = tooltip.GetComponentInChildren<StarVaders.Tooltip>(true);
                if (tip == null) return lines;
                string title = localized ? GameText.Clean(Localized(tip.TitleText)) : GameText.Of(tip.TitleText);
                var desc = new List<string>();
                string raw = localized ? Localized(tip.DescriptionText) : tip.DescriptionText != null ? tip.DescriptionText.text : null;
                if (raw != null)
                    foreach (var part in raw.Split('\n'))
                    {
                        string line = GameText.Clean(RarityFigures(part))?.Trim();
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
            catch (Exception e) { CoreLog.Warning("Keywords: reading the tooltip " + name + " failed: " + e.Message); }
            return lines;
        }

        /// <summary>A tooltip object's title as its localization fills it (a hidden one keeps the
        /// prefab's placeholder until first shown).</summary>
        public static string LocalizedTitle(UnityEngine.GameObject tooltip)
        {
            var tip = tooltip != null ? tooltip.GetComponentInChildren<StarVaders.Tooltip>(true) : null;
            return tip != null ? GameText.Clean(Localized(tip.TitleText)) : null;
        }

        // The text's localized string, else its text.
        private static string Localized(TMPro.TMP_Text tmp)
        {
            if (tmp == null) return null;
            var ev = tmp.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
            if (ev == null || ev.StringReference == null || ev.StringReference.IsEmpty) return tmp.text;
            try { return ev.StringReference.GetLocalizedString(); }
            catch (Exception e) { CoreLog.Warning("Keywords: localizing " + tmp.name + " failed: " + e.Message); return tmp.text; }
        }
    }
}
