using System;
using System.Collections.Generic;
using StarVaders;
using DiceVoders.Core;
using DiceVoders.Core.Graph;
using DiceVoders.Core.Strings;
using DiceVoders.Core.UI;
using DiceVoders.Module.Interop;
using DiceVoders.Module.UI;
using UnityEngine;
using Screen = DiceVoders.Core.Screens.Screen;

namespace DiceVoders.Module.Screens
{
    /// <summary>
    /// The strategy guide (<see cref="StrategyGuideController"/>, StrategyGuideSceneAdditive, from the
    /// main menu or the game menu), named "Strategy Guide". One stop: its pages as a row of tabs, each
    /// by its button as drawn (<see cref="StrategyGuideController.GuideButtons"/>: a caption with
    /// icons pictured around it, read in order as their words, "Budge + Chrono"; locked, New!),
    /// landing turns to it (<see cref="StrategyGuideController.SetPage"/>); then the page, group by
    /// group (a paragraph and the example drawn beside it): the paragraph, the example's texts
    /// (one-letter key prompts left out), its artifacts as a row (Backspace their tooltips); Exit,
    /// Escape too.
    /// </summary>
    public sealed class GuideScreen : Screen
    {
        public override string Key => "guide";
        public override int Layer => 36;
        public override bool Exclusive => true;

        private static StrategyGuideController Controller()
        {
            var g = GameScenes.Controller<StrategyGuideController>();
            if (g == null || g.Pages == null || g.Pages.Count == 0 || g.Pages[0] == null) return null;
            var pages = g.Pages[0].transform.parent;
            var group = pages != null && pages.parent != null ? pages.parent.GetComponent<CanvasGroup>() : null;
            if (pages == null || !pages.gameObject.activeInHierarchy) return null;
            return group == null || group.alpha > 0.01f ? g : null;
        }

        public override bool IsActive() => Controller() != null;

        public override string ScreenName => GameLoc.Get("StrategyGuide_Misc");

        public override void Build(GraphBuilder b)
        {
            var g = Controller();
            if (g == null) return;
            b.PushContext(null);
            try
            {
                var buttons = new List<GuidePageButton>();
                foreach (var button in g.GuideButtons) if (button != null && button.gameObject.activeInHierarchy) buttons.Add(button);
                if (buttons.Count > 0)
                {
                    b.StartRow("guide:pages", entry: () => Math.Max(0, buttons.FindIndex(x => x.index == g.currentIndex)));
                    foreach (var button in buttons)
                    {
                        var page = button;
                        Func<string> label = () => PageLabel(page);
                        b.AddItem(ControlId.Structural("guide:page:" + page.index), new NodeVtable
                        {
                            ControlType = ControlTypes.Tab,
                            Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label), GameNodes.SelectedPart(() => g.currentIndex == page.index) },
                            SearchText = label,
                            OnFocus = () => { if (g.currentIndex != page.index && !page.IsLocked) Turn(g, page.index); },
                        });
                    }
                    b.EndRow();
                }
                var shown = g.currentIndex >= 0 && g.currentIndex < g.Pages.Count ? g.Pages[g.currentIndex] : null;
                if (shown != null && shown.activeInHierarchy)
                {
                    BuildPage(b, shown.transform);
                    // The page's own buttons (the last page's Discord), by caption.
                    foreach (var item in shown.GetComponentsInChildren<ButtonView>(false))
                        b.AddItem(ControlId.Structural("guide:button:" + item.gameObject.name), ViewNodes.Button(item));
                    if (g.DiscordButton != null && g.DiscordButton.activeInHierarchy && !g.DiscordButton.transform.IsChildOf(shown.transform))
                    {
                        var discord = g.DiscordButton.GetComponentInChildren<ButtonView>(false);
                        if (discord != null) b.AddItem(ControlId.Structural("guide:discord"), ViewNodes.Button(discord));
                    }
                }
                var exit = g.Pages[0].transform.parent.Find("BG/Exit/ExitButton");
                var exitItem = exit != null ? exit.GetComponent<AViewItem>() : null;
                if (ViewNodes.IsShown(exitItem)) b.AddItem(ControlId.Structural("guide:exit"), ViewNodes.Button(exitItem));
            }
            catch (Exception e) { CoreLog.Warning("GuideScreen: building failed: " + e); }
            b.PopContext();
        }

        private static void Turn(StrategyGuideController g, int page)
        {
            try { g.SetPage(page); }
            catch (Exception e) { CoreLog.Warning("GuideScreen: turning to page " + page + " failed: " + e.Message); }
        }

        // The icons pictured on the page buttons, by sprite, as the icons' names in game text.
        private static readonly Dictionary<string, string> Icons = new Dictionary<string, string>
        {
            ["T_RB_budgeicon"] = "Budge", ["T_RB_rerollicon"] = "Chrono", ["T_RB_minidiceicon"] = "MiniDice",
            ["Arrow"] = "Arrow", ["Target"] = "Target",
        };

        // "Budge + Chrono": the caption's text and the icons pictured around it, left to right; then
        // locked, New!.
        private static string PageLabel(GuidePageButton button)
        {
            var parts = new List<(float X, string Text)>();
            try
            {
                foreach (var tmp in button.GetComponentsInChildren<TMPro.TMP_Text>(false))
                {
                    if (button.NewText != null && tmp.transform.IsChildOf(button.NewText.transform)) continue;
                    string text = GameText.Of(tmp);
                    if (text != null) parts.Add((tmp.transform.position.x, text.Trim()));
                }
                foreach (var img in button.GetComponentsInChildren<UnityEngine.UI.Image>(false))
                {
                    string sprite = img.sprite != null ? img.sprite.name : null;
                    if (sprite == null || !Icons.TryGetValue(sprite, out var icon)) continue;
                    string word = GameText.Clean(" <sprite name=\"" + icon + "\"> ")?.Trim().TrimEnd(':');
                    if (!string.IsNullOrEmpty(word)) parts.Add((img.transform.position.x, word));
                }
            }
            catch (Exception e) { CoreLog.Warning("GuideScreen: reading a page button failed: " + e.Message); }
            parts.Sort((l, r) => l.X.CompareTo(r.X));
            var words = new List<string>();
            foreach (var p in parts) words.Add(p.Text);
            string label = string.Join(" ", words);
            var states = new List<string> { label };
            if (button.IsLocked) states.Add(Strings.StateLocked);
            else if (button.NewText != null && button.NewText.activeInHierarchy)
            {
                string isNew = GameText.Of(button.NewText.GetComponentInChildren<TMPro.TMP_Text>(false) ?? button.NewText.GetComponent<TMPro.TMP_Text>());
                if (isNew != null) states.Add(isNew);
            }
            return string.Join(", ", states);
        }

        // The page: its groups (the page's children: a paragraph with an example drawn beside it) top
        // to bottom; in each, its paragraph first, each text one line (its line breaks are layout),
        // then the example's texts top to bottom (a text with figures drawn in it read as one line:
        // "11 / 11 pages discovered!") and its artifacts as a row.
        private static void BuildPage(GraphBuilder b, Transform page)
        {
            var groups = new List<(float Top, List<(int Rank, float Y, string Line, List<ArtifactView> Row)> Items)>();
            for (int g = 0; g < page.childCount; g++)
            {
                var group = page.GetChild(g);
                if (!group.gameObject.activeInHierarchy) continue;
                var items = new List<(int Rank, float Y, string Line, List<ArtifactView> Row)>();
                var parts = new HashSet<IntPtr>();
                foreach (var tmp in group.GetComponentsInChildren<TMPro.TMP_Text>(false))
                {
                    if (tmp == null || parts.Contains(tmp.Pointer) || string.IsNullOrWhiteSpace(tmp.text)) continue;
                    if (tmp.GetComponentInParent<AViewItem>() != null) continue;
                    var rt = tmp.rectTransform;
                    float top = rt.position.y + rt.rect.yMax * rt.lossyScale.y;
                    // The paragraph: a text that is the group, or its direct child.
                    int rank = tmp.transform == group || tmp.transform.parent == group ? 0 : 1;
                    string line;
                    var pieces = tmp.GetComponentsInChildren<TMPro.TMP_Text>(false);
                    if (pieces.Length > 1)
                    {
                        var ordered = new List<TMPro.TMP_Text>(pieces);
                        ordered.Sort((l, r) => l.transform.position.x.CompareTo(r.transform.position.x));
                        var words = new List<string>();
                        foreach (var piece in ordered)
                        {
                            parts.Add(piece.Pointer);
                            string w = GameText.Clean(piece.text)?.Trim();
                            if (!string.IsNullOrEmpty(w)) words.Add(w);
                        }
                        line = string.Join(" ", words);
                    }
                    else line = GameText.Clean(tmp.text.Replace('\n', ' '))?.Trim();
                    if (string.IsNullOrEmpty(line) || line.Length < 2 || !System.Linq.Enumerable.Any(line, char.IsLetterOrDigit)) continue;
                    items.Add((rank, top, line, null));
                }
                var rows = new Dictionary<IntPtr, List<ArtifactView>>();
                var rowY = new Dictionary<IntPtr, float>();
                foreach (var view in group.GetComponentsInChildren<ArtifactView>(false))
                {
                    var model = view.ArtifactModel;
                    if (model == null || model.ArtifactName == ArtifactName.None || view.transform.parent == null) continue;
                    var key = view.transform.parent.Pointer;
                    if (!rows.TryGetValue(key, out var row)) { row = new List<ArtifactView>(); rows[key] = row; rowY[key] = view.transform.position.y; }
                    row.Add(view);
                    rowY[key] = Math.Max(rowY[key], view.transform.position.y);
                }
                foreach (var kv in rows)
                {
                    kv.Value.Sort((l, r) => l.transform.position.x.CompareTo(r.transform.position.x));
                    items.Add((1, rowY[kv.Key], null, kv.Value));
                }
                if (items.Count == 0) continue;
                items.Sort((l, r) => l.Rank != r.Rank ? l.Rank.CompareTo(r.Rank) : r.Y.CompareTo(l.Y));
                float groupTop = float.MinValue;
                foreach (var item in items) groupTop = Math.Max(groupTop, item.Y);
                groups.Add((groupTop, items));
            }
            groups.Sort((l, r) => r.Top.CompareTo(l.Top));
            int i = 0;
            foreach (var group in groups)
                foreach (var item in group.Items)
                {
                    if (item.Line != null)
                    {
                        string text = item.Line;
                        b.AddItem(ControlId.Structural("guide:line:" + i++), GameNodes.Text(() => text));
                        continue;
                    }
                    b.StartRow("guide:artifacts");
                    foreach (var view in item.Row)
                    {
                        var v = view;
                        Func<string> label = () => Encounter.Encounter.ArtifactName(v.ArtifactModel);
                        Func<IEnumerable<string>> details = () => EncounterScreen.ArtifactDetails(v.ArtifactModel, v);
                        b.AddItem(ControlId.Structural("guide:artifact:" + i++), new NodeVtable
                        {
                            ControlType = ControlTypes.Text,
                            Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label) },
                            SearchText = label,
                            OnFocus = () => ViewNodes.Hover(v),
                            Details = details,
                            OnSecondary = () => SayDetails(details()),
                        });
                    }
                    b.EndRow();
                }
        }

        private static void SayDetails(IEnumerable<string> lines)
        {
            var list = new List<string>(lines ?? Array.Empty<string>());
            Speech.Say(list.Count > 0 ? string.Join(" ", list) : Strings.NoDetails, interrupt: true);
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var g = Controller();
                var exit = g != null ? g.Pages[0].transform.parent.Find("BG/Exit/ExitButton") : null;
                var item = exit != null ? exit.GetComponent<AViewItem>() : null;
                if (ViewNodes.IsShown(item)) ViewNodes.Click(item);
            });
        }
    }
}
