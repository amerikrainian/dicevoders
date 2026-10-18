using System;
using StarVaders;
using DiceVoders.Core;
using DiceVoders.Module.UI;

namespace DiceVoders.Module.Interop
{
    /// <summary>
    /// The game's localized names and descriptions, by the keys its own <see cref="LocalizationUtils"/>
    /// helpers build (<c>LocalizeEntityTitle</c> is <c>name + "_EntityTitle"</c> and so on). Those
    /// helpers answer through a callback; <see cref="LocalizationUtils.LocalizeSynchronous"/> answers
    /// at once from the same table, and says "No translation found for '...' in Everything" for a
    /// missing key, which reads as null here.
    /// </summary>
    internal static class GameLoc
    {
        private static readonly System.Text.RegularExpressions.Regex KeywordTag =
            new System.Text.RegularExpressions.Regex(@"\{f\.keyword\}(.*?)\{f\.end\}");

        /// <summary>The first keyword a string marks, in the current language: "Purge" from
        /// <c>Purge_InfoLine</c>'s "{f.keyword}Purge{f.end}: Temporarily remove...", for the words the
        /// game has no string of their own for. Read from the raw table entry, since
        /// <see cref="LocalizationUtils.LocalizeSynchronous"/> expands the tags into rich text.</summary>
        public static string Keyword(string key)
        {
            try
            {
                var entry = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.GetTable("Everything")?.GetEntry(key);
                var match = entry != null && entry.Value != null ? KeywordTag.Match(entry.Value) : null;
                if (match != null && match.Success) return GameText.Clean(match.Groups[1].Value);
                CoreLog.Warning("GameLoc: no keyword in " + key);
            }
            catch (Exception e) { CoreLog.Warning("GameLoc: reading the keyword of " + key + " failed: " + e.Message); }
            return null;
        }

        public static string Get(string key)
        {
            try
            {
                string text = LocalizationUtils.LocalizeSynchronous(key);
                if (string.IsNullOrWhiteSpace(text) || GameText.IsMissing(text)) return null;
                return GameText.Clean(text);
            }
            catch (Exception e)
            {
                CoreLog.Warning("GameLoc: localizing " + key + " failed: " + e.Message);
                return null;
            }
        }
    }
}
