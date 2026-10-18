using System.Collections.Generic;
using System.Text.RegularExpressions;
using DiceVoders.Core;
using DiceVoders.Core.Text;

namespace DiceVoders.Module.UI
{
    /// <summary>
    /// Game text made speakable. Besides TMP rich text the game uses Text Animator, whose effect tags
    /// come in two forms (<c>&lt;shake a=0.2&gt;</c>, <c>{rot}</c>) and stay in a text until the
    /// animator parses them, and inline sprite icons (<c>&lt;sprite="TextIcons" name="Reroll"&gt;</c>)
    /// that carry meaning ("3 [damage icon]"), so an icon reads as its word, unless it is a
    /// keyword's decoration.
    /// </summary>
    internal static class GameText
    {
        private static readonly Regex AnimatorTag = new Regex(@"\{/?[a-z]+[^}]*\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Sprite = new Regex(@"<sprite[^>]*?name=""?([^"">]+)""?[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // A keyword is an icon and its word, which the game wraps as one ("<nobr><sprite name=Chrono>
        // <b>Chrono</b></nobr>"): there the icon is decoration and dropped. Elsewhere an icon reads as
        // its word ("2 <sprite name=Attack>" is "2 STR"; "Unspent <sprite name=ShopToken> is
        // converted" keeps it), unless its word or its name follows it (Charge's title "<Charge>
        // Charge", the stats panel's "<Attack> ATTACK"). An icon's word is its keyword's
        // (Attack_Keyword is "<Attack> STR"); ShopToken's is Cash (ShopToken_TooltipTitle), and the
        // scoring line's Target is "When scored" (the strategy guide's Guide_Scoring_1: 'The following
        // icon means "When scored":'), with a colon where it opens a line, as the scoring line's
        // marker ("When scored: Spend 2 Charge Arrow Get +1 Budge"). Arrow, between a trigger
        // condition and its effect (Guide_Triggers_2), and React keep their names, which the game's
        // own explanations quote ('everything after the "Arrow"'). A run of one icon is a count
        // ("Spend <Charge><Charge>" is "2 Charge"). Icons are marked while the tags between them and
        // their words are stripped.
        private const char IconStart = '\u0001', IconEnd = '\u0002', DecorStart = '\u0003';
        private const string LineTarget = "Target:";
        private static readonly Regex Nobr = new Regex(@"<nobr>(.*?)</nobr>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex OpeningTarget = new Regex(@"(^|\n)((?:\s|<[^>]*>)*)<sprite[^>]*?name=""?Target""?[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex DecorThenWord = new Regex(@"\u0003([^\u0002]+)\u0002\s*(?=\p{L})", RegexOptions.Compiled);
        private static readonly Regex IconRun = new Regex(@"\u0001([^\u0002]+)\u0002(?:\s*\u0001\1\u0002)+", RegexOptions.Compiled);
        private static readonly Regex IconThenWord = new Regex(@"\u0001([^\u0002]+)\u0002\s*(?=(\p{L}[\p{L}\-]*))", RegexOptions.Compiled);
        private static readonly Regex Icon = new Regex(@"\u0001([^\u0002]+)\u0002", RegexOptions.Compiled);
        private static readonly Regex SpaceBeforeStop = new Regex(@"\s+([.,])", RegexOptions.Compiled);
        private static readonly Regex Quoted = new Regex(@"[""“”«»「」„]\s*([^""“”«»「」„]+?)\s*[""“”«»「」„]", RegexOptions.Compiled);
        private static readonly Regex PaddedQuote = new Regex(@"""\s+([^""]*?)\s+""", RegexOptions.Compiled);

        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = OpeningTarget.Replace(text, m => m.Groups[1].Value + m.Groups[2].Value + "<sprite name=\"" + LineTarget + "\">");
            text = Nobr.Replace(text, m => Sprite.Replace(m.Groups[1].Value, s => DecorStart + s.Groups[1].Value + IconEnd));
            text = Sprite.Replace(text, m => IconStart + m.Groups[1].Value + IconEnd);
            text = AnimatorTag.Replace(text, "");
            text = TextUtil.StripRichText(text);
            text = DecorThenWord.Replace(text, "");
            text = text.Replace(DecorStart, IconStart);
            text = IconRun.Replace(text, m => " " + Count(m.Value) + " " + IconWord(m.Groups[1].Value) + " ");
            text = IconThenWord.Replace(text, m => SameWord(IconWord(m.Groups[1].Value), m.Groups[2].Value) || SameWord(m.Groups[1].Value, m.Groups[2].Value) ? "" : m.Value);
            text = Icon.Replace(text, m => " " + IconWord(m.Groups[1].Value) + " ");
            text = PaddedQuote.Replace(text, "\"$1\"");
            text = TextUtil.StripRichText(text);
            return SpaceBeforeStop.Replace(text, "$1");
        }

        private static int Count(string run)
        {
            int n = 0;
            foreach (char c in run) if (c == IconStart) n++;
            return n;
        }

        // Letters alike, case and hyphens aside ("MiniDice" is "Mini-Dice").
        private static bool SameWord(string a, string b)
        {
            string Norm(string w)
            {
                var sb = new System.Text.StringBuilder();
                foreach (char c in w ?? "") if (char.IsLetter(c)) sb.Append(char.ToLowerInvariant(c));
                return sb.ToString();
            }
            return Norm(a).Length > 0 && Norm(a) == Norm(b);
        }

        // An icon's word: its name, but for the two named otherwise.
        // An icon's word: its keyword's (<name>_Keyword is the icon and its word: Attack's is "STR"),
        // else its name; Target and ShopToken, whose keywords are the bare icon, have theirs from the
        // guide and the Cash tooltip. Per language, cached.
        private static string IconWord(string name)
        {
            switch (name)
            {
                case "Target": return WhenScored() ?? name;
                case LineTarget: { string words = WhenScored(); return words != null ? words + ":" : "Target"; }
                case "ShopToken": return Plain("ShopToken_TooltipTitle") ?? name;
            }
            string locale = CurrentLocale();
            if (locale != _wordsLocale) { _words.Clear(); _wordsLocale = locale; }
            if (!_words.TryGetValue(name, out var word))
            {
                word = Plain(name + "_Keyword") ?? name;
                _words[name] = word;
            }
            return word;
        }

        private static readonly Dictionary<string, string> _words = new Dictionary<string, string>();
        private static string _wordsLocale;

        private static string CurrentLocale()
        {
            try { return UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale?.Identifier.Code; }
            catch { return null; }
        }

        // The quoted words of the guide's 'The following icon means "When scored":', in the current
        // language; null when it has none.
        private static string WhenScored()
        {
            string guide = Plain("Guide_Scoring_1");
            if (guide == null) return null;
            var m = Quoted.Match(guide);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        // A string's words with its icons and tags dropped (not through Clean, which asks for these).
        private static string Plain(string key)
        {
            try
            {
                string text = StarVaders.LocalizationUtils.LocalizeSynchronous(key);
                if (string.IsNullOrWhiteSpace(text) || IsMissing(text)) return null;
                text = Sprite.Replace(text, "");
                text = AnimatorTag.Replace(text, "");
                text = TextUtil.StripRichText(text);
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch (System.Exception e) { CoreLog.Warning("GameText: localizing " + key + " failed: " + e.Message); return null; }
        }

        /// <summary>A TMP text's clean text, or null when it is missing or blank, or holds a missing
        /// key's placeholder (logged once per text).</summary>
        public static string Of(TMPro.TMP_Text tmp)
        {
            if (tmp == null) return null;
            string text = Clean(tmp.text);
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (!IsMissing(text)) return text;
            if (_missingSeen.Add(text)) CoreLog.Warning("GameText: " + tmp.gameObject.name + " holds a missing key: " + text);
            return null;
        }

        private static readonly HashSet<string> _missingSeen = new HashSet<string>();

        /// <summary>Whether a text is, or holds, Unity Localization's placeholder for a key the table
        /// lacks ("No translation found for 'None_Misc' in Everything"; the game builds some keys
        /// that do not exist, a pilot's rarity, a pilot fight's target).</summary>
        public static bool IsMissing(string text) =>
            text != null && text.IndexOf("No translation found for '", System.StringComparison.Ordinal) >= 0;
    }
}
