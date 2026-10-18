using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using StarVaders;
using DiceVoders.Core;
using DiceVoders.Core.Graph;
using DiceVoders.Core.Strings;
using DiceVoders.Core.UI;
using DiceVoders.Module.Interop;
using DiceVoders.Module.UI;
using TMPro;
using UnityEngine;
using Screen = DiceVoders.Core.Screens.Screen;

namespace DiceVoders.Module.Screens
{
    /// <summary>
    /// The credits (<c>CreditsScene</c>, <see cref="CreditsController"/>, from the main menu): six
    /// pages, one shown at a time (<c>PageList[currentPageIndex]</c>), turned by the up and down
    /// arrow buttons (drawn with only their key prompts), Exit on the last. One stop: the page's
    /// lines under a "Page 2/6" context, then Previous page, Next page and Exit while each is
    /// shown. A turn lands on the new page's first line. Lines in hierarchy order: a heading with
    /// a <c>Name</c> child reads "Lead Design: Eddie Cai (Axolotl)"; a name with
    /// a name nested in it (two to a row in the team lists) reads both, comma-separated; the
    /// localization page's icon legend ("[Budge] : Translator") is not read as lines but names the
    /// icons in its entries ("Español (es), Translator, LQA: Tamara Morales Gómez"). Escape leaves, as
    /// the game's cancel does, from any page.
    /// </summary>
    public sealed class CreditsScreen : Screen
    {
        public override string Key => "credits";
        public override string ScreenName => GameLoc.Get("Credits_Misc") ?? Strings.MainCredits;

        private static CreditsController Controller => GameScenes.Controller<CreditsController>();

        public override bool IsActive() => Controller != null;

        private static readonly Regex SpriteTag = new Regex(@"<sprite[^>]*?name=""?([^"">\s]+)""?[^>]*>", RegexOptions.IgnoreCase);
        private static readonly Regex Legend = new Regex(@"^\s*<sprite[^>]*?name=""?([^"">\s]+)""?[^>]*>\s*:\s*(.+?)\s*$", RegexOptions.IgnoreCase);

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            var pages = c != null ? c.PageList : null;
            if (pages == null || pages.Count == 0) return;
            int index = c.currentPageIndex;
            var page = index >= 0 && index < pages.Count ? pages[index] : null;

            b.BeginStop("credits");
            b.PushContext(Strings.CreditsPage(index + 1, pages.Count), positions: false);
            if (page != null)
            {
                var icons = Icons(page);
                var lines = new List<string>();
                Walk(page, lines, icons);
                for (int i = 0; i < lines.Count; i++)
                {
                    string text = lines[i];
                    b.AddItem(LineId(index, i), GameNodes.Text(() => text));
                }
            }
            b.PopContext();
            AddTurn(b, c, c.UpButton, "credits:prev", () => Strings.CreditsPrevious, () => c.OnPressPreviousPageButton());
            AddTurn(b, c, c.DownButton, "credits:next", () => Strings.CreditsNext, () => c.OnPressNextPageButton());
            if (c.ExitButton != null && c.ExitButton.activeInHierarchy)
                b.AddItem(ControlId.Structural("credits:exit"),
                    GameNodes.Button(() => GameLoc.Get("Exit_Misc") ?? Strings.Get("bind.ui.back"), Leave));
        }

        private static ControlId LineId(int page, int line) => ControlId.Structural("credits:p" + page + ":" + line);

        // A page arrow: its click turns the page (ignored by the game while a turn still moves), then
        // focus goes to the new page's first line.
        private static void AddTurn(GraphBuilder b, CreditsController c, GameObject button, string id, Func<string> label, Action turn)
        {
            if (button == null || !button.activeInHierarchy) return;
            b.AddItem(ControlId.Structural(id), GameNodes.Button(label, () =>
            {
                int before = c.currentPageIndex;
                try { turn(); }
                catch (Exception e) { CoreLog.Warning("Credits: turning the page failed: " + e.Message); return; }
                if (c.currentPageIndex != before) Navigation.FocusNode(LineId(c.currentPageIndex, 0));
            }));
        }

        private static void Leave()
        {
            var c = Controller;
            if (c == null || c.IsTransitioning) return;
            try { c.OnPressExitButton(); }
            catch (Exception e) { CoreLog.Warning("Credits: leaving failed: " + e.Message); }
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Leave());
        }

        // The page's icon legend: icon name to the role it stands for ("Budge" to "Translator").
        private static Dictionary<string, string> Icons(Transform page)
        {
            var icons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tmp in page.GetComponentsInChildren<TMP_Text>(true))
            {
                var m = tmp != null && tmp.text != null ? Legend.Match(tmp.text) : null;
                if (m != null && m.Success) icons[m.Groups[1].Value] = GameText.Clean(m.Groups[2].Value)?.Trim();
            }
            return icons;
        }

        // Depth first in hierarchy order. A heading with a Name child (a role, "OTKO Animations BY")
        // takes it as "heading: name"; a name reads alone, its nested name after it (two to a row in
        // the team lists), and so does any other text, its children after it (a section's roles).
        private static void Walk(Transform t, List<string> lines, Dictionary<string, string> icons)
        {
            var own = t.GetComponent<TMP_Text>();
            Transform name = null;
            if (own != null && !Legend.IsMatch(own.text ?? ""))
            {
                if (!t.name.StartsWith("Name")) name = t.Find("Name");
                string title = Text(own, icons);
                string named = name != null ? Text(name.GetComponent<TMP_Text>(), icons) : null;
                string line = title != null && named != null ? title + ": " + named : title ?? named;
                if (line != null) lines.Add(line);
            }
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (child != name && child.gameObject.activeSelf) Walk(child, lines, icons);
            }
        }

        // The text's parts (its lines, and the " | " the game draws between a language and its
        // icons) cleaned and joined by commas, each icon read as the role the legend gives it:
        // "Español (es) | [Budge] [Chrono]" reads "Español (es), Translator, LQA".
        private static string Text(TMP_Text tmp, Dictionary<string, string> icons)
        {
            if (tmp == null || string.IsNullOrEmpty(tmp.text)) return null;
            string raw = SpriteTag.Replace(tmp.text, m => icons.TryGetValue(m.Groups[1].Value, out var role) ? "\n" + role + "\n" : m.Value);
            var parts = new List<string>();
            foreach (var piece in raw.Split('\n', '|'))
            {
                string line = GameText.Clean(piece)?.Trim().Trim(',', ' ');
                if (!string.IsNullOrEmpty(line) && !parts.Contains(line)) parts.Add(line);
            }
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }
    }
}
