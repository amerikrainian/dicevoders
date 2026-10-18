using System;
using System.Collections.Generic;
using System.Globalization;
using StarVaders;
using DiceVoders.Core.Graph;
using DiceVoders.Core.Strings;
using DiceVoders.Core.UI;
using TMPro;
using UnityEngine;

namespace DiceVoders.Module.UI
{
    /// <summary>
    /// Node factories over the game's option rows (<see cref="OptionView"/> and its kinds), the
    /// settings screen's widgets. Every change goes through the row's own handlers
    /// (<c>OnClick</c>, <c>OnLeftPressed</c>, <c>OnRightPressed</c>), as the game's keyboard does, so
    /// its settings logic (applying volumes, resolution choices) runs unchanged. A row's caption is
    /// its <c>Description</c> text (the game's name for the row's label); its tooltip, the text of
    /// the "?" beside it, is read only where the row shows that "?": every row carries one, and the
    /// hidden ones hold the prefab's placeholder text (an artifact's description in Korean).
    /// </summary>
    internal static class OptionNodes
    {
        /// <summary>The row's caption.</summary>
        public static string Label(OptionView row)
        {
            var d = row != null ? row.Description : null;
            return d != null && !string.IsNullOrWhiteSpace(d.text) ? d.text : ViewNodes.Label(row);
        }

        /// <summary>The row's tooltip body (its <c>Description (1)</c> text), or null when the row
        /// shows no "?".</summary>
        public static string Tooltip(OptionView row)
        {
            var trigger = row != null ? row.GetComponentInChildren<TooltipTriggerView>(true) : null;
            if (trigger == null || !trigger.gameObject.activeInHierarchy || trigger.Tooltip == null) return null;
            var body = trigger.Tooltip.transform.Find("Description (1)");
            var tmp = body != null ? body.GetComponent<TMP_Text>() : null;
            return tmp != null ? GameText.Of(tmp) : null;
        }

        /// <summary>A tab of the strip: "label, tab"; landing on it shows its page, as a tab does, so
        /// no "selected" is spoken. Its <see cref="NodeVtable.IsSelected"/> is what Tab-stop entry
        /// lands on when the strip has no remembered position (a fresh render, a reload): landing
        /// elsewhere would switch the page.</summary>
        public static NodeVtable Tab(AViewItem tab, Func<string> label, Func<bool> selected, Action select)
        {
            Action pick = () => { if (!selected()) select(); };
            return new NodeVtable
            {
                ControlType = ControlTypes.Tab,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label) },
                IsSelected = selected,
                SearchText = label,
                OnActivate = pick,
                OnFocus = () => { ViewNodes.Hover(tab); pick(); },
            };
        }

        /// <summary>A toggle row: "label, toggle, on/off[, disabled]"; Enter is the row's click. A
        /// locked row has no state.</summary>
        public static NodeVtable Toggle(OptionToggle row)
        {
            Func<string> label = () => Label(row);
            Func<string> state = () =>
            {
                if (row._isLocked) return null;
                return row.Toggle != null && row.Toggle.isOn ? Strings.StateOn : Strings.StateOff;
            };
            return Row(row, ControlTypes.Toggle, label, state, () => !row._isLocked, onActivate: () => row.OnClick());
        }

        /// <summary>A slider row: "label, slider, value"; Left and Right are the row's own steps
        /// (five at a time with the large step). A 0 to 1 slider (the volumes) reads as a percent,
        /// a whole-number one as its number.</summary>
        public static NodeVtable Slider(OptionSlider row)
        {
            Func<string> label = () => Label(row);
            Func<string> value = () => SliderText(row);
            var vt = Row(row, ControlTypes.Slider, label, value, null, onActivate: null);
            vt.OnAdjust = (sign, large) => Step(sign, large, row.OnRightPressed, row.OnLeftPressed);
            return vt;
        }

        /// <summary>A chooser row (resolution, screen type) as a combo box: "label, combo box,
        /// current"; Enter opens the choices as a list landing on the current one, where Enter picks
        /// and Escape leaves the value alone (guildrun's dropdown). Left and Right navigate. The
        /// game's arrows only move <c>_currentOptionIndex</c> (wrapping) and redraw the text with
        /// <c>UpdateText</c>; a pick does the same, and the settings read the index on Apply.</summary>
        public static NodeVtable Chooser(OptionChooser row)
        {
            Func<string> label = () => Label(row);
            Func<string> value = () => row.CurrentOptionText != null ? GameText.Of(row.CurrentOptionText) : null;
            var vt = Row(row, ControlTypes.ComboBox, label, value, null, onActivate: () => OpenChoices(row, label));
            vt.StateText = null; // Enter opens the list, which is its own feedback: no value echo first
            return vt;
        }

        private static void OpenChoices(OptionChooser row, Func<string> label)
        {
            var keys = row.OptionKeys;
            int count = keys != null ? keys.Count : 0;
            if (count == 0) return;
            int current = row._currentOptionIndex;
            var choices = new List<Core.Screens.ChoiceOption>();
            for (int i = 0; i < count; i++)
            {
                int index = i;
                choices.Add(new Core.Screens.ChoiceOption(ChoiceText(row, i, current), () =>
                {
                    if (row._currentOptionIndex == index) return;
                    row._currentOptionIndex = index;
                    row.UpdateText();
                }));
            }
            // The combo box just read its own label: the list speaks only the option landed on.
            Core.Screens.ChoiceSubmenuScreen.Open(label(), choices, current, speakTitle: false);
        }

        // An option as the game draws it: the shown text for the current one, else the key, through
        // the game's "<key>_Misc" localization when the chooser is localized (UpdateText's).
        private static string ChoiceText(OptionChooser row, int index, int current)
        {
            if (index == current && row.CurrentOptionText != null && !string.IsNullOrWhiteSpace(row.CurrentOptionText.text))
                return GameText.Of(row.CurrentOptionText);
            string key = row.OptionKeys[index];
            if (!row.IsLocalized || string.IsNullOrEmpty(key)) return key;
            string text = Interop.GameLoc.Get(key + "_Misc");
            return string.IsNullOrEmpty(text) ? key : text;
        }

        /// <summary>A button row: "label, button"; Enter is the row's click.</summary>
        public static NodeVtable Button(OptionButton row)
        {
            Func<string> label = () => Label(row);
            return Row(row, ControlTypes.Button, label, null, null, onActivate: () => row.OnClick());
        }

        /// <summary>The key a binding row draws ("W"), or null. Some keys (Enter) are drawn as a picture
        /// with the prompt's text hidden and stale, so then the key is named from the binding.</summary>
        public static string KeyText(OptionKeybind row)
        {
            var prompt = row.MainKeyprompt;
            var text = prompt != null ? prompt._keyText : null;
            if (text != null && text.isActiveAndEnabled && !string.IsNullOrWhiteSpace(text.text)) return text.text;
            var action = row._rebindActionBehaviour != null ? row._rebindActionBehaviour.focusedInputAction : null;
            if (action == null) return null;
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var key = Input.GameKeyBinding.KeyOf(bindings[i].effectivePath);
                if (key.HasValue) return Input.KeyboardBinding.KeyName(key.Value);
            }
            return null;
        }

        /// <summary>A key binding row: "action, key binding, key" ("Reroll, R"). Enter rebinds it,
        /// through the game's own rebind with the keyboard lent to it
        /// (<see cref="Screens.KeyCaptureScreen"/>).</summary>
        public static NodeVtable Keybind(OptionKeybind row)
        {
            Func<string> label = () => Label(row);
            Func<string> key = () => KeyText(row);
            var vt = Row(row, ControlTypes.KeyBinding, label, key, null,
                onActivate: () => Core.Screens.ScreenManager.Current?.PushChild(new Screens.KeyCaptureScreen(row, label())));
            vt.StateText = null; // Enter's echo would be the old key: the prompt speaks instead
            return vt;
        }

        private static NodeVtable Row(OptionView row, ControlType type, Func<string> label, Func<string> value,
            Func<bool> enabled, Action onActivate)
        {
            var parts = new List<NodeAnnouncement> { GameNodes.LabelPart(label) };
            if (value != null) parts.Add(new NodeAnnouncement(value, live: true, kind: AnnouncementKinds.Value));
            parts.Add(GameNodes.DisabledPart(enabled));
            parts.Add(GameNodes.TooltipPart(() => Tooltip(row)));
            return new NodeVtable
            {
                ControlType = type,
                Announcements = parts,
                SearchText = label,
                StateText = value,
                OnFocus = () => ViewNodes.Hover(row),
                OnActivate = onActivate == null ? null : () =>
                {
                    if (enabled != null && !enabled()) return;
                    onActivate();
                },
            };
        }

        private static void Step(int sign, bool large, Action right, Action left)
        {
            int n = large ? 5 : 1;
            for (int i = 0; i < n; i++) (sign > 0 ? right : left)();
        }

        private static string SliderText(OptionSlider row)
        {
            var s = row.Slider;
            if (s == null) return null;
            if (s.wholeNumbers || row.isIntIncrement) return ((int)Math.Round(s.value)).ToString(CultureInfo.InvariantCulture);
            if (s.minValue == 0f && s.maxValue == 1f) return Strings.Percent((int)Math.Round(s.value * 100f));
            return Math.Round(s.value, 2).ToString(CultureInfo.InvariantCulture);
        }
    }
}
