using System;
using System.Collections.Generic;
using StarVaders;
using DiceVoders.Core;

namespace DiceVoders.Module.UI
{
    /// <summary>
    /// The tutorial's tip box (<see cref="TipManager.CurrentTip"/>): one at a time, its text drawn at
    /// once (no typing out, unlike StarVaders'), and on some a "[click] to continue" line
    /// (<c>ClickToContinue</c>): those wait for a click, which the mod gives through
    /// <see cref="Continue"/>.
    /// </summary>
    internal static class Tips
    {
        private const string ContinueLine = "ClickToContinue";

        /// <summary>The tip showing now, or null.</summary>
        public static Tip Current
        {
            get
            {
                var tip = TipManager.CurrentTip;
                return tip != null && tip._isShowing ? tip : null;
            }
        }

        private static TipBox Box(Tip tip) => tip != null ? tip.GetComponentInChildren<TipBox>() : null;

        /// <summary>The tip box's shown texts outside the continue line, joined, or null.</summary>
        public static string Text(Tip tip)
        {
            var box = Box(tip);
            if (box == null) return null;
            var parts = new List<string>();
            foreach (var tmp in box.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
            {
                if (InContinueLine(tmp.transform, box.transform)) continue;
                string text = GameText.Of(tmp);
                if (text != null) parts.Add(text);
            }
            return parts.Count == 0 ? null : string.Join(" ", parts);
        }

        private static bool InContinueLine(UnityEngine.Transform t, UnityEngine.Transform box)
        {
            for (; t != null && t.Pointer != box.Pointer; t = t.parent)
                if (t.name == ContinueLine) return true;
            return false;
        }

        /// <summary>Whether the tip waits for a click: its continue line shows. A tip that waits for
        /// an action instead has none.</summary>
        public static bool WaitsForClick(Tip tip)
        {
            var box = Box(tip);
            if (box == null) return false;
            foreach (var tmp in box.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
                if (tmp.name == ContinueLine) return true;
            return false;
        }

        /// <summary>Click the tip as the game's click does (<see cref="TipManager.ClickTipBox"/>, which
        /// destroys the tip once its box has finished popping in, <see cref="TipBox._isFinished"/>;
        /// before then the click is dropped, so it is said).</summary>
        public static void Continue(Tip tip)
        {
            var box = Box(tip);
            if (box == null)
            {
                CoreLog.Warning("Tips: no tip box to click");
                return;
            }
            try
            {
                if (!box._isFinished)
                {
                    CoreLog.Info("Tips: the tip box is still opening, click ignored");
                    return;
                }
                TipManager.ClickTipBox();
            }
            catch (Exception e) { CoreLog.Warning("Tips: continuing the tip failed: " + e.Message); }
        }
    }
}
