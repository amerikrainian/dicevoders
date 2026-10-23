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
    /// The constellation scene (<see cref="ConstellationController"/>, in the encounter: the run's
    /// random modifiers, shown at a run's start once constellations are unlocked, and again from the
    /// encounter's Constellations button). One stop: the three constellations left to right, each one
    /// node of its drawn texts ("FAE MINOR, Your Pixie units are Rare and have a regenerating
    /// Shield."; a locked one "Complete 3 CHALLENGES to unlock... (0/3)"); one still to reveal is a
    /// button, its caption "Reveal !" last, and Enter reveals it (<see cref="Constellation.OnPressReveal"/>,
    /// the button's own click), its texts said again once revealed. Backspace reads what the
    /// constellation changes: its artifacts' names and descriptions, or its units, with the keywords
    /// the game explains beside them. First the scene's title hover ("Constellation System",
    /// Backspace its explanation); then Proceed
    /// (<see cref="ConstellationController.OnContinuePressed"/>), once the game shows it.
    ///
    /// The first time, the unlock notice (<c>UnlockedConstellations</c>, "CONSTELLATIONS UNLOCKED! From
    /// now on...") covers the scene: it is the one node then, and Enter is its click
    /// (<see cref="ConstellationController.OnPressUnlockConstellation"/>, which records it as seen).
    ///
    /// The scene fades in, so the screen declares nothing until its canvas is opaque, 3 s at most.
    /// Read only from the prefab so far (this save has no constellations): its texts are placeholders.
    /// </summary>
    public sealed class ConstellationScreen : Screen
    {
        public override string Key => "constellations";
        public override int Layer => 32;
        public override bool Exclusive => true;
        public override bool AllowsTypeahead => false;

        private const float Settled = 0.99f, MaxWait = 3f;
        private float _pushedAt;

        private static ConstellationController Controller()
        {
            var c = Encounter.Encounter.Controller?.ConstellationController;
            return c != null && c.gameObject.activeInHierarchy ? c : null;
        }

        public override bool IsActive() => Controller() != null;

        public override string ScreenName => GameLoc.Get("Constellations_Misc");

        public override void OnPush() => _pushedAt = Time.unscaledTime;

        public override bool HoldsArrival => !IsSettled(Controller());

        private bool IsSettled(ConstellationController c)
        {
            if (c == null) return false;
            if (Time.unscaledTime - _pushedAt >= MaxWait) return true;
            return c.Canvas == null || c.Canvas.alpha >= Settled;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller();
            if (c == null || !IsSettled(c)) return;
            var notice = c.UnlockConstellationObject;
            if (notice != null && notice.activeInHierarchy)
            {
                BuildNotice(b, notice.transform);
                return;
            }
            b.BeginStop("constellations:list");
            // The scene's title hover ("Constellation System", Backspace its explanation), first.
            var system = c.TooltipViewItem;
            if (ViewNodes.IsShown(system) && Keywords.Trigger(system.transform).Count > 0)
                b.AddItem(ControlId.Structural("constellations:system"), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => SystemTitle(system)) },
                    SearchText = () => SystemTitle(system),
                    Details = () => Keywords.Trigger(system.transform),
                    OnSecondary = () => SayDetails(Keywords.Trigger(system.transform)),
                });
            var shown = new List<Constellation>();
            foreach (var con in c.Constellations)
                if (con != null && con.gameObject.activeInHierarchy) shown.Add(con);
            shown.Sort((l, r) => l.transform.position.x.CompareTo(r.transform.position.x));
            for (int i = 0; i < shown.Count; i++)
            {
                var con = shown[i];
                b.AddItem(ControlId.Structural("constellations:" + i), Node(con));
            }
            var proceed = c.ContinueButton != null ? c.ContinueButton.GetComponentInChildren<ButtonView>(false) : null;
            if (ViewNodes.IsShown(proceed))
                b.AddItem(ControlId.Structural("constellations:proceed"),
                    ViewNodes.Button(proceed, () => GameText.Of(c.ContinueButton.Find("Top")?.GetComponentInChildren<TMPro.TMP_Text>(false)) ?? GameLoc.Get("Proceed_Misc") ?? "Proceed"));
        }

        private static string SystemTitle(AViewItem system)
        {
            var view = system.TryCast<TooltipTriggerView>();
            var tip = view != null && view.Tooltip != null ? view.Tooltip.GetComponentInChildren<Tooltip>(true) : null;
            return tip != null ? GameText.Of(tip.TitleText) : null;
        }

        // The notice: its text (its "to continue" line left out) as one node; Enter is its click.
        private static void BuildNotice(GraphBuilder b, Transform notice)
        {
            var box = notice.GetComponentInChildren<ButtonView>(false);
            if (box == null) return;
            // The box's first text is the notice; its "to continue" line is a text nested under it.
            Func<string> text = () => GameText.Of(box.GetComponentInChildren<TMPro.TMP_Text>(false));
            b.AddItem(ControlId.Structural("constellations:notice"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { new NodeAnnouncement(text, live: true, kind: AnnouncementKinds.Label) },
                SearchText = text,
                OnFocus = () => ViewNodes.Hover(box),
                OnActivate = () => ViewNodes.Click(box),
            });
        }

        // A constellation: its drawn texts, the reveal button's caption last while it shows (Enter is
        // then its click). One kind of node either way, so a reveal rewords it in place.
        internal static NodeVtable Node(Constellation con)
        {
            var reveal = con.transform.Find("Parallaxer/RevealObject")?.GetComponent<ButtonView>();
            Func<string> label = () => Label(con, reveal);
            Func<IEnumerable<string>> details = () => Details(con);
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { new NodeAnnouncement(label, live: true, kind: AnnouncementKinds.Label) },
                SearchText = label,
                OnFocus = () => { if (ViewNodes.IsShown(reveal)) ViewNodes.Hover(reveal); },
                OnActivate = () => { if (ViewNodes.IsShown(reveal)) ViewNodes.Click(reveal); },
                Details = details,
                OnSecondary = () => SayDetails(details()),
            };
        }

        private static string Label(Constellation con, ButtonView reveal)
        {
            var parts = new List<string>();
            try
            {
                Add(parts, con.TitleText);
                Add(parts, con.LockedText);
                if (con.PanelTransform != null && con.PanelTransform.gameObject.activeInHierarchy) Add(parts, con.PanelDescription);
                if (ViewNodes.IsShown(reveal)) Add(parts, reveal.GetComponentInChildren<TMPro.TMP_Text>(false));
            }
            catch (Exception e) { CoreLog.Warning("ConstellationScreen: reading " + con.name + " failed: " + e.Message); }
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        private static void Add(List<string> parts, TMPro.TMP_Text tmp)
        {
            if (tmp == null || !tmp.gameObject.activeInHierarchy) return;
            string text = GameText.Of(tmp);
            if (text != null) parts.Add(System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim());
        }

        // What the constellation changes, once revealed: its artifacts ("Meteor: After you score a pair
        // ..."), or its units, the base one then the changed one, as their tooltips read.
        private static IEnumerable<string> Details(Constellation con)
        {
            var lines = new List<string>();
            try
            {
                if (con.PanelTransform == null || !con.PanelTransform.gameObject.activeInHierarchy) return lines;
                if (con.ArtifactSetObject != null && con.ArtifactSetObject.activeInHierarchy)
                {
                    foreach (var view in con.ArtifactSetObject.GetComponentsInChildren<ArtifactView>(false))
                    {
                        var artifact = view.ArtifactModel;
                        if (artifact == null || artifact.ArtifactName == ArtifactName.None) continue;
                        var (title, desc, keywords) = Encounter.Encounter.ArtifactTooltip(artifact, view);
                        title = title ?? Encounter.Encounter.ArtifactName(artifact);
                        lines.Add(desc.Count > 0 ? title + ": " + desc[0] : title);
                        foreach (var line in keywords) if (!lines.Contains(line)) lines.Add(line);
                    }
                }
                if (con.UnitSetObject != null && con.UnitSetObject.activeInHierarchy)
                {
                    foreach (var entry in con.UnitSetObject.GetComponentsInChildren<InvaderCompendiumEntry>(false))
                    {
                        var unit = entry.entityView != null ? entry.entityView.EntityModel : null;
                        if (unit == null) continue;
                        var (desc, rarity, keywords) = Encounter.Encounter.Tooltip(unit, entry.EntityTooltip);
                        string name = Encounter.Encounter.Name(unit);
                        lines.Add(desc != null ? name + ": " + desc : name);
                        if (rarity != null) lines.Add(rarity);
                        foreach (var line in keywords) if (!lines.Contains(line)) lines.Add(line);
                    }
                }
            }
            catch (Exception e) { CoreLog.Warning("ConstellationScreen: reading " + con.name + "'s set failed: " + e.Message); }
            return lines;
        }

        private static void SayDetails(IEnumerable<string> lines)
        {
            var list = new List<string>(lines ?? Array.Empty<string>());
            Speech.Say(list.Count > 0 ? string.Join(" ", list) : Strings.NoDetails, interrupt: true);
        }
    }
}
