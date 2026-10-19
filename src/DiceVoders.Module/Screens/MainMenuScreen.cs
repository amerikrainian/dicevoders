using System.Collections.Generic;
using StarVaders;
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
    /// The main menu (<see cref="MainMenuController"/>). Its scene also holds the welcome popup
    /// (<see cref="WelcomePopupScreen"/>) and the language picker (<see cref="LanguageScreen"/>),
    /// whose items are left to those screens. The rest reads in the scene's order: the column
    /// (Resume Game and its delete icon, Start Game, Options, Exit, the Strategy Guide), then the
    /// icon buttons along the edges (News, Join Discord, Credits, Change Language, Compendium,
    /// Wishlist), whichever are shown, named by their hover tooltips. The run's delete icon has only
    /// its key prompt ("Q"), so it reads by the mod's words.
    /// </summary>
    public sealed class MainMenuScreen : Screen
    {
        public override string Key => "mainmenu";
        public override string ScreenName => Strings.ScreenMainMenu;

        internal static MainMenuController Controller => GameScenes.Controller<MainMenuController>();

        public override bool IsActive() => Controller != null;

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            var items = GameScenes.Items(c);
            if (items == null) return;
            var edge = new List<AViewItem>();
            b.PushContext(null);
            foreach (var item in items)
            {
                if (!ViewNodes.IsShown(item) || item.TryCast<IClickable>() == null || IsOverlay(c, item.transform)) continue;
                if (IsEdge(item)) { edge.Add(item); continue; }
                var label = item.gameObject.name == "DeleteRunButton" ? (System.Func<string>)(() => Strings.MainDeleteRun) : null;
                b.AddItem(ControlId.Structural("main:" + item.gameObject.name), ViewNodes.Button(item, label));
            }
            b.PopContext();
            b.PushContext(null);
            foreach (var item in edge)
            {
                var icon = item;
                b.AddItem(ControlId.Structural("main:" + item.gameObject.name), ViewNodes.Button(item, () => EdgeLabel(icon)));
            }
            b.PopContext();
        }

        // An icon by its hover tooltip's name, with a figure it draws ("Compendium, 12%", its unlocked
        // share): the icon's caption alone would be the figure.
        private static string EdgeLabel(AViewItem item)
        {
            string label = ViewNodes.Label(item);
            var button = item.TryCast<ButtonView>();
            var tip = button != null ? button.Tooltip : null;
            string name = null;
            if (tip != null)
                foreach (var tmp in tip.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    if (name == null) name = GameText.Of(tmp);
            if (name == null || name == label) return label;
            return label != null && label != item.gameObject.name ? name + ", " + label : name;
        }

        // The popup and the language picker are screens of their own over this one.
        private static bool IsOverlay(MainMenuController c, Transform t)
            => (c.PopupCanvas != null && t.IsChildOf(c.PopupCanvas))
            || (c.LanguageSelector != null && t.IsChildOf(c.LanguageSelector.transform));

        // The icon buttons: ExtraButtonsLeft, ExtraButtonsRight and the wishlist frame.
        private static bool IsEdge(AViewItem item)
        {
            var parent = item.transform.parent;
            string name = parent != null ? parent.name : "";
            return name.StartsWith("ExtraButtons") || name.StartsWith("WishlistButtonFrame");
        }

        public override IEnumerable<ElementAction> GetActions() { yield break; }
    }

    /// <summary>
    /// The main menu's welcome popup (<see cref="MainMenuController.PopupCanvas"/>, "Welcome to
    /// Dicevaders!"): a dialog named by the shown panel's title, one stop of its prose, one line per
    /// drawn line (focus lands on the first), with its buttons (Join Discord, Got It) at the bottom.
    /// Escape is Got It.
    /// </summary>
    public sealed class WelcomePopupScreen : Screen
    {
        public override string Key => "welcome";
        public override int Layer => 5;
        public override bool Exclusive => true;
        public override object InitialFocusStop => "text";

        private static Transform Popup()
        {
            var c = MainMenuScreen.Controller;
            var popup = c != null ? c.PopupCanvas : null;
            return popup != null && popup.gameObject.activeInHierarchy ? popup : null;
        }

        public override bool IsActive() => Popup() != null;

        public override void Build(GraphBuilder b)
        {
            var popup = Popup();
            if (popup == null) return;
            var title = Title(popup);
            b.PushContext(GameText.Of(title), Strings.RoleDialog, positions: false);
            b.BeginStop("text");
            var lines = ViewNodes.Prose(popup, title);
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                b.AddItem(ControlId.Structural("welcome:line:" + i), GameNodes.Text(() => line));
            }
            foreach (var item in popup.GetComponentsInChildren<AViewItem>(false))
                if (item.TryCast<IClickable>() != null)
                    b.AddItem(ControlId.Structural("welcome:" + item.transform.parent.name), ViewNodes.Button(item));
            b.PopContext();
        }

        // The shown panel's heading: the first visible text named "Title".
        private static TMPro.TMP_Text Title(Transform popup)
        {
            foreach (var tmp in popup.GetComponentsInChildren<TMPro.TMP_Text>(false))
                if (tmp != null && tmp.gameObject.name == "Title" && !string.IsNullOrWhiteSpace(tmp.text)) return tmp;
            return null;
        }

        private static AViewItem GotIt(Transform popup)
        {
            var t = popup.Find("PanelBG/ContinueFrame/ExitButton");
            return t != null ? t.GetComponent<AViewItem>() : null;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var popup = Popup();
                var gotIt = popup != null ? GotIt(popup) : null;
                if (ViewNodes.IsShown(gotIt)) ViewNodes.Click(gotIt);
            });
        }
    }

    /// <summary>
    /// The language picker over the main menu (<see cref="MainMenuController.LanguageSelector"/>:
    /// up on the first launch, and from the Change Language icon): a dialog named by its heading
    /// ("Select Language"), the languages as radio buttons by their own names ("Français (fr)"), the
    /// selected one marked by its <c>Selected</c> child, then Apply &amp; Exit. Enter picks a language as
    /// the game's click does; landing on one picks nothing. Escape is Apply &amp; Exit, the picker's
    /// only way out.
    /// </summary>
    public sealed class LanguageScreen : Screen
    {
        public override string Key => "language";
        public override int Layer => 10;
        public override bool Exclusive => true;
        public override object InitialFocusStop => "languages";

        private static LanguageSelector Selector()
        {
            var c = MainMenuScreen.Controller;
            var selector = c != null ? c.LanguageSelector : null;
            return selector != null && selector.gameObject.activeInHierarchy ? selector : null;
        }

        public override bool IsActive() => Selector() != null;

        public override void Build(GraphBuilder b)
        {
            var selector = Selector();
            if (selector == null) return;
            var page = selector.transform.Find("Page");
            var heading = page != null ? page.Find("Text (TMP)") : null;
            b.PushContext(heading != null ? GameText.Of(heading.GetComponent<TMPro.TMP_Text>()) : Strings.MainLanguage,
                Strings.RoleDialog, positions: false);
            b.BeginStop("languages");
            var grid = page != null ? page.Find("Grid") : null;
            if (grid != null)
                foreach (var item in grid.GetComponentsInChildren<AViewItem>(false))
                {
                    var mark = item.transform.Find("Selected");
                    b.AddItem(ControlId.Structural("language:" + item.gameObject.name),
                        ViewNodes.Radio(item, () => mark != null && mark.gameObject.activeInHierarchy));
                }
            b.BeginStop("apply");
            var apply = Apply(selector);
            if (ViewNodes.IsShown(apply)) b.AddItem(ControlId.Structural("language:apply"), ViewNodes.Button(apply));
            b.PopContext();
        }

        private static AViewItem Apply(LanguageSelector selector)
        {
            var t = selector.transform.Find("Page/ApplyButton");
            return t != null ? t.GetComponentInChildren<AViewItem>(false) : null;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var selector = Selector();
                var apply = selector != null ? Apply(selector) : null;
                if (ViewNodes.IsShown(apply)) ViewNodes.Click(apply);
            });
        }
    }
}
