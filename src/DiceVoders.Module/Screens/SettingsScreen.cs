using System.Collections.Generic;
using StarVaders;
using DiceVoders.Core.Graph;
using DiceVoders.Core.Input;
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
    /// The options (<see cref="SettingsSceneController"/>, the SettingsMenuScene over the main menu
    /// or a run, named by its title "Options &amp; Extras"): the tab strip (Audio, Video, Control,
    /// Profile, Sandbox) as one Tab-stop, the shown page's rows as the next, and Reset to Default and
    /// Apply &amp; Exit as the last. A page is <c>Panels[_currentTab]</c>; it reads in the game's order:
    /// its rows (the view items under it that sit in no other view item: a chooser's arrows and the
    /// "?" are parts of their row, a chooser's own Apply follows it) and the texts that belong to no
    /// row ("Music by plasterbrain..."). Escape is Apply &amp; Exit.
    /// </summary>
    public sealed class SettingsScreen : Screen
    {
        public override string Key => "settings";
        public override string ScreenName
        {
            get
            {
                var title = Root(Controller)?.Find("BackgroundPanel/TitleText");
                var tmp = title != null ? title.GetComponent<TMP_Text>() : null;
                return GameText.Of(tmp) ?? Strings.ScreenSettings;
            }
        }
        public override int Layer => 35;
        public override bool Exclusive => true;
        public override IReadOnlyList<InputCategory> InputCategories => RunKeys.Categories;
        public override object InitialFocusStop => "tabs";

        private static SettingsSceneController Controller => GameScenes.Controller<SettingsSceneController>();

        // The panel everything sits on (Canvas/CanvasGroup), above the pages' InnerBG.
        private static Transform Root(SettingsSceneController c)
        {
            var panels = c != null ? c.Panels : null;
            if (panels == null || panels.Count == 0 || panels[0] == null) return null;
            var inner = panels[0].transform.parent;
            return inner != null ? inner.parent : null;
        }

        public override bool IsActive()
        {
            var root = Root(Controller);
            return root != null && root.gameObject.activeInHierarchy;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            var root = Root(c);
            if (root == null) return;

            b.BeginStop("tabs");
            b.PushContext(null);
            b.StartRow(entry: () => c._currentTab);
            var strip = root.Find("CategoryButtons");
            if (strip != null)
                for (int i = 0; i < strip.childCount; i++)
                {
                    int index = i;
                    var holder = strip.GetChild(i);
                    var tab = holder.Find("Button")?.GetComponent<AViewItem>();
                    if (!ViewNodes.IsShown(tab)) continue;
                    var caption = holder.Find("Text (TMP)")?.GetComponent<TMP_Text>();
                    b.AddItem(ControlId.Structural("settings:tab" + i),
                        OptionNodes.Tab(tab, () => GameText.Of(caption) ?? ViewNodes.Label(tab),
                            () => c._currentTab == index, () => ViewNodes.Click(tab)));
                }
            b.EndRow();
            b.PopContext();

            b.BeginStop("page");
            b.PushContext(null);
            var panels = c.Panels;
            int current = c._currentTab;
            var page = panels != null && current >= 0 && current < panels.Count ? panels[current] : null;
            if (page != null && page.activeInHierarchy) AddPage(b, page.transform, 0);
            b.PopContext();

            b.BeginStop("actions");
            b.PushContext(null);
            var inner = page != null ? page.transform.parent : null;
            foreach (var name in new[] { "ResetToDefaultButton", "ApplyButton" })
            {
                var item = Bottom(inner, name);
                if (ViewNodes.IsShown(item))
                    b.AddItem(ControlId.Structural("settings:" + name), ViewNodes.Button(item, () => HolderLabel(item)));
            }
            b.PopContext();
        }

        // The page in hierarchy order: a row is added whole (its arrows and "?" are parts of it), a
        // text outside every row is a line of its own.
        private static void AddPage(GraphBuilder b, Transform t, int depth)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                var item = child.GetComponent<AViewItem>();
                if (item != null && item.TryCast<BlockerViewItem>() == null && item.TryCast<TooltipTriggerView>() == null)
                {
                    AddRow(b, item);
                    continue;
                }
                // The key bindings' column headers ("Game Action", "Binding"): each row names both.
                if (child.name == "Labels") continue;
                var tmp = child.GetComponent<TMP_Text>();
                string text = GameText.Of(tmp);
                if (text != null && !IsCaption(child))
                    b.AddItem(ControlId.Structural("settings:text:" + Path(child)), GameNodes.Text(() => GameText.Of(tmp)));
                AddPage(b, child, depth + 1);
            }
        }

        // A stable name for a text: its path under the page (several share the name "Text (TMP)").
        private static string Path(Transform t)
        {
            string path = t.name;
            for (var p = t.parent; p != null && p.GetComponent<OptionView>() == null && !p.name.EndsWith("Page"); p = p.parent)
                path = p.name + "/" + path;
            return path;
        }

        private static void AddRow(GraphBuilder b, AViewItem item)
        {
            var id = ControlId.Structural("settings:row:" + Path(item.transform));
            var toggle = item.TryCast<OptionToggle>();
            if (toggle != null) { b.AddItem(id, OptionNodes.Toggle(toggle)); return; }
            var slider = item.TryCast<OptionSlider>();
            if (slider != null) { b.AddItem(id, OptionNodes.Slider(slider)); return; }
            var chooser = item.TryCast<OptionChooser>();
            if (chooser != null)
            {
                b.AddItem(id, OptionNodes.Chooser(chooser));
                // The screen type's own Apply, inside its row: a control of its own after it.
                var apply = chooser.transform.Find("ApplyButton");
                var applyItem = apply != null ? apply.GetComponentInChildren<AViewItem>(false) : null;
                if (ViewNodes.IsShown(applyItem))
                    b.AddItem(ControlId.Structural("settings:row:" + Path(item.transform) + "/apply"),
                        ViewNodes.Button(applyItem, () => HolderLabel(applyItem)));
                return;
            }
            var keybind = item.TryCast<OptionKeybind>();
            if (keybind != null) { b.AddItem(id, OptionNodes.Keybind(keybind)); return; }
            var button = item.TryCast<OptionButton>();
            if (button != null) { b.AddItem(id, OptionNodes.Button(button)); return; }
            var selected = item.transform.Find("Selected");
            var existed = item.transform.Find("Existed");
            if (selected != null && existed != null) { b.AddItem(id, ProfileSlot(item, selected, existed)); return; }
            b.AddItem(id, ViewNodes.Button(item, () => HolderLabel(item)));
        }

        // A save profile slot: its name ("P-1") once it holds a profile, else "Create New Profile",
        // drawn over each other; the current profile shows its Selected frame. Enter is the game's
        // click (switches to the profile, or creates one).
        private static NodeVtable ProfileSlot(AViewItem item, Transform selected, Transform existed)
        {
            var unselected = item.transform.Find("Unselected");
            return ViewNodes.Radio(item, () => selected.gameObject.activeInHierarchy, () =>
            {
                var shown = existed.gameObject.activeInHierarchy ? existed : unselected;
                var tmp = shown != null ? shown.GetComponentInChildren<TMP_Text>(false) : null;
                return GameText.Of(tmp) ?? ViewNodes.Label(item);
            });
        }

        // A text that is the caption of a captionless button beside it (Delete Current Profile's):
        // read as that button's name, not as a line of its own.
        private static bool IsCaption(Transform text)
        {
            var parent = text.parent;
            if (parent == null) return false;
            for (int i = 0; i < parent.childCount; i++)
            {
                var item = parent.GetChild(i).GetComponent<AViewItem>();
                if (item != null && item.gameObject.activeInHierarchy && item.TryCast<BlockerViewItem>() == null
                    && item.GetComponentInChildren<TMP_Text>(true) == null)
                    return true;
            }
            return false;
        }

        // A bottom button by its holder's name under InnerBG (the button itself is "Button").
        private static AViewItem Bottom(Transform inner, string name)
        {
            var t = inner != null ? inner.Find(name + "/Button") : null;
            return t != null ? t.GetComponent<AViewItem>() : null;
        }

        // A button's own caption, else (a button drawn as an image) the caption its holder carries
        // beside it.
        private static string HolderLabel(AViewItem item)
        {
            foreach (var own in item.GetComponentsInChildren<TMP_Text>(false))
            {
                string text = GameText.Of(own);
                if (text != null && text.Length > 1) return text;
            }
            var holder = item.transform.parent;
            if (holder != null)
                foreach (var tmp in holder.GetComponentsInChildren<TMP_Text>(true))
                {
                    string text = GameText.Of(tmp);
                    if (text != null && text.Length > 1) return text;
                }
            return ViewNodes.Label(item);
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var c = Controller;
                var panels = c != null ? c.Panels : null;
                var inner = panels != null && panels.Count > 0 && panels[0] != null ? panels[0].transform.parent : null;
                var apply = Bottom(inner, "ApplyButton");
                if (ViewNodes.IsShown(apply)) ViewNodes.Click(apply);
            });
        }
    }
}
