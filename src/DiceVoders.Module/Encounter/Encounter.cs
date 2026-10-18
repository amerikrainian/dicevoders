using System;
using System.Collections.Generic;
using StarVaders;
using DiceVoders.Core;
using DiceVoders.Module.Interop;
using DiceVoders.Module.UI;
using UnityEngine;

namespace DiceVoders.Module.Encounter
{
    /// <summary>
    /// The encounter (<c>EncounterScene</c>): its controller and model, the board, the dice, the
    /// figures, and the game's own click. Everything is read from the model when it is asked for
    /// (nothing kept between renders); the panels animate towards the model and a hover previews on
    /// them. The board is <see cref="GridModel.GridShape"/> (6 by 3), its <see cref="Coord"/> 0-based
    /// with y growing upward (each column's layout is reversed: y 0 is drawn at the bottom). A die's
    /// value is the column it scores, 1-based.
    /// </summary>
    internal static class Encounter
    {
        public static EncounterController Controller
        {
            get
            {
                var c = GameScenes.Controller<EncounterController>();
                return c != null && c.EncounterModel != null ? c : null;
            }
        }

        public static EncounterModel Model => Controller?.EncounterModel;
        public static EncounterView View => Controller?.EncounterView;

        public static int Columns => GridModel.GridShape.x;
        public static int Rows => GridModel.GridShape.y;

        /// <summary>Hover the item as the game's keys do: its highlight and tooltip follow.</summary>
        public static void Hover(AViewItem item)
        {
            var handler = Controller?.EncounterInputHandler;
            if (handler == null || item == null) return;
            try { handler.OnHoverViewItemByKey(item); }
            catch (Exception e) { CoreLog.Warning("Encounter: hovering " + item.gameObject.name + " failed: " + e.Message); }
        }

        /// <summary>The game's click on the item: hover it, then click what is hovered.</summary>
        public static void Click(AViewItem item)
        {
            var handler = Controller?.EncounterInputHandler;
            if (handler == null || item == null) return;
            try
            {
                handler.OnHoverViewItemByKey(item);
                handler.ClickViewItems();
            }
            catch (Exception e) { CoreLog.Warning("Encounter: clicking " + item.gameObject.name + " failed: " + e.Message); }
        }

        /// <summary>Whether the task engine is running something (scoring, a roll, the shop being set up).</summary>
        public static bool Busy
        {
            get
            {
                try { return Controller?.TaskEngine?.IsBusy() == true; }
                catch (Exception e) { CoreLog.Warning("Encounter: reading the task engine failed: " + e.Message); return false; }
            }
        }

        public static Phase Phase => Controller?.CurrentPhase ?? Phase.None;

        // ---- figures ----

        public static int? Int(EncounterValue value)
        {
            var model = Model;
            if (model == null) return null;
            try { return model.GetIntValue(value); }
            catch (Exception e) { CoreLog.Warning("Encounter: reading " + value + " failed: " + e.Message); return null; }
        }

        /// <summary>A big figure as the game prints it ("100", "1.03e12").</summary>
        public static string Big(EncounterValue value)
        {
            var model = Model;
            if (model == null) return null;
            try { return model.GetBigDoubleValue(value).ToString(); }
            catch (Exception e) { CoreLog.Warning("Encounter: reading " + value + " failed: " + e.Message); return null; }
        }

        // ---- the board ----

        public static Coord MakeCoord(int x, int y)
        {
            var c = new Coord();
            c.x = x;
            c.y = y;
            return c;
        }

        /// <summary>The unit on the tile, or null.</summary>
        public static EntityModel At(int x, int y)
        {
            var model = Model;
            if (model == null) return null;
            try
            {
                foreach (var kv in model.ModelItemDict)
                {
                    var entity = kv.Value?.TryCast<EntityModel>();
                    if (entity == null) continue;
                    var id = entity.EntityID;
                    if (!model.GridModel.IsEntityOnGrid(id)) continue;
                    var c = model.GridModel.GetEntityCoord(id, false);
                    if (c.x == x && c.y == y) return entity;
                }
            }
            catch (Exception e) { CoreLog.Warning("Encounter: reading the unit at " + x + "," + y + " failed: " + e.Message); }
            return null;
        }

        public static TileView Tile(int x, int y)
        {
            var grid = View?.GridView;
            if (grid == null) return null;
            foreach (var tile in grid.GetComponentsInChildren<TileView>())
            {
                var model = tile.TileModel;
                if (model != null && model.Coord.x == x && model.Coord.y == y) return tile;
            }
            return null;
        }

        public static GridColumn Column(int x)
        {
            var grid = View?.GridView;
            if (grid == null) return null;
            var columns = grid.GetComponentsInChildren<GridColumn>();
            return x >= 0 && x < columns.Length ? columns[x] : null;
        }

        /// <summary>Whether the tile holds a cloud (<see cref="TileStatus.Cloud"/>).</summary>
        public static bool Cloud(int x, int y)
        {
            try
            {
                var status = Model?.GridModel?.TryGetTileStatusAtCoord(MakeCoord(x, y));
                return status.HasValue && status.Value == TileStatus.Cloud;
            }
            catch (Exception e) { CoreLog.Warning("Encounter: reading the tile status at " + x + "," + y + " failed: " + e.Message); return false; }
        }

        /// <summary>Whether the column (0-based) holds honey (<see cref="EncounterModel.HoneySlots"/>).</summary>
        public static bool Honey(int x)
        {
            try
            {
                var slots = Model?.HoneySlots;
                return slots != null && x < slots.Count && slots[x];
            }
            catch (Exception e) { CoreLog.Warning("Encounter: reading the honey failed: " + e.Message); return false; }
        }

        /// <summary>The Dice Power each column (0-based) gets from the dice now, as the game counts it
        /// (<see cref="DicePanel.GetAllDicePower"/>, keyed by the 1-based column).</summary>
        public static int DicePower(int x)
        {
            try
            {
                var power = Controller?.DicePanel?.GetAllDicePower(true);
                return power != null && power.ContainsKey(x + 1) ? power[x + 1] : 0;
            }
            catch (Exception e) { CoreLog.Warning("Encounter: reading the dice power failed: " + e.Message); return 0; }
        }

        // ---- units ----

        /// <summary>The unit's name ("Migo").</summary>
        public static string Name(EntityModel entity)
        {
            if (entity == null) return null;
            return GameLoc.Get(entity.EntityName + "_Entity_Title") ?? entity.EntityName.ToString();
        }

        /// <summary>The unit's strength now, as the game prints it.</summary>
        public static string Strength(EntityModel entity)
        {
            try { return entity?.GetCurrentValue(Model).ToString(); }
            catch (Exception e) { CoreLog.Warning("Encounter: reading a unit's strength failed: " + e.Message); return null; }
        }

        /// <summary>The unit's tooltip as the game fills it on hover (<see cref="EntityTooltip.UpdateTooltip"/>,
        /// run here on the tile's own tooltip: the hidden one holds prefab placeholder text until then):
        /// its description and its rarity line ("Unit - Common").</summary>
        public static (string Description, string Rarity) Tooltip(EntityModel entity, TileView tile)
        {
            var tooltip = tile != null ? tile.EntityTooltip : null;
            if (entity == null || tooltip == null) return (null, null);
            try
            {
                tooltip.UpdateTooltip(entity, Model);
                return (GameText.Of(tooltip.DescriptionText), GameText.Of(tooltip.RarityText));
            }
            catch (Exception e)
            {
                CoreLog.Warning("Encounter: filling a unit's tooltip failed: " + e.Message);
                return (GameLoc.Get(entity.EntityName + "_Entity_Desc"), null);
            }
        }

        // ---- artifacts ----

        /// <summary>The artifact in the slot (the first 6 are the columns', <see cref="EncounterModel.GetColumnArtifacts"/>),
        /// or null for an empty slot.</summary>
        public static ArtifactModel Artifact(int slot)
        {
            var model = Model;
            if (model == null) return null;
            try
            {
                var slots = model.ArtifactSlots;
                if (slots == null || slot >= slots.Count) return null;
                // The dictionary is keyed by the boxed ID: matched by number instead.
                int number = slots[slot].Number;
                foreach (var kv in model.ModelItemDict)
                {
                    var artifact = kv.Value?.TryCast<ArtifactModel>();
                    if (artifact == null || artifact.ArtifactID.Number != number) continue;
                    return artifact.ArtifactName != StarVaders.ArtifactName.None && !artifact.IsDestroyed ? artifact : null;
                }
                return null;
            }
            catch (Exception e) { CoreLog.Warning("Encounter: reading artifact slot " + slot + " failed: " + e.Message); return null; }
        }

        /// <summary>"Meteor"; a pilot's ability leads with the pilot ("Roxy, Trailblazer").</summary>
        public static string ArtifactName(ArtifactModel artifact)
        {
            if (artifact == null) return null;
            string name = GameLoc.Get(artifact.ArtifactName + "_Arti_Title") ?? artifact.ArtifactName.ToString();
            return WithPilot(artifact, name);
        }

        /// <summary>The pilot an artifact stands for (its <see cref="ArtifactType.Pilot"/> entry is named
        /// for them: <c>ArtifactName.Roxy</c>), null for any other artifact. The game draws pilots'
        /// names only as images and has no text for them.</summary>
        public static string PilotName(ArtifactModel artifact)
        {
            try { return artifact != null && artifact.ArtifactType == ArtifactType.Pilot ? artifact.ArtifactName.ToString() : null; }
            catch (Exception e) { CoreLog.Warning("Encounter: reading an artifact's type failed: " + e.Message); return null; }
        }

        private static string WithPilot(ArtifactModel artifact, string text)
        {
            string pilot = PilotName(artifact);
            return pilot == null || text == null || text == pilot ? text : pilot + ", " + text;
        }

        public static ArtifactView ArtifactView(ArtifactModel artifact)
        {
            var dict = View?.ArtifactViewDict;
            if (artifact == null || dict == null) return null;
            try
            {
                int number = artifact.ArtifactID.Number;
                foreach (var kv in dict)
                    if (kv.Value != null && kv.Value.ArtifactModel != null && kv.Value.ArtifactModel.ArtifactID.Number == number) return kv.Value;
                return null;
            }
            catch (Exception e) { CoreLog.Warning("Encounter: finding an artifact's view failed: " + e.Message); return null; }
        }

        /// <summary>The artifact's tooltip as the game fills it (<see cref="StarVaders.Tooltip.SetArtifactText"/>
        /// on its view's tooltip): the description, the rarity and the scaling lines.</summary>
        public static List<string> ArtifactLines(ArtifactModel artifact) => ArtifactTooltip(artifact).Lines;

        /// <summary>The artifact's tooltip as the game fills it: its title ("Meteor [9/10 XP]", null when
        /// it has none) and the lines after it.</summary>
        public static (string Title, List<string> Lines) ArtifactTooltip(ArtifactModel artifact)
        {
            var lines = new List<string>();
            string title = null;
            if (artifact == null) return (null, lines);
            var tooltip = ArtifactView(artifact)?.Tooltip;
            try
            {
                if (tooltip != null)
                {
                    tooltip.SetArtifactText(artifact, false, Model);
                    if (tooltip.TitleText != null && tooltip.TitleText.gameObject.activeSelf) title = WithPilot(artifact, GameText.Of(tooltip.TitleText));
                    // A pilot's rarity is None, which the game has no text for: its line reads Unity's
                    // "No translation found for 'None_Misc'".
                    var rarity = artifact.Rarity == Rarity.None ? null : tooltip.RarityText;
                    foreach (var tmp in new TMPro.TMP_Text[] { tooltip.DescriptionText, rarity, tooltip.ScalingText })
                    {
                        if (tmp == null || !tmp.gameObject.activeSelf) continue;
                        string text = GameText.Of(tmp);
                        if (text != null) lines.Add(text);
                    }
                }
            }
            catch (Exception e) { CoreLog.Warning("Encounter: filling an artifact's tooltip failed: " + e.Message); }
            if (lines.Count == 0)
            {
                string desc = GameLoc.Get(artifact.ArtifactName + "_Arti_Desc");
                if (desc != null) lines.Add(desc);
            }
            return (title, lines);
        }

        /// <summary>The artifact's level, XP and the XP its next level takes
        /// (<see cref="ArtifactModel.GetLevelXPRequirement"/>); null for one that gains none (the
        /// starter perk's XP reads -1).</summary>
        public static (int Level, int Xp, int Needed)? ArtifactXp(ArtifactModel artifact)
        {
            if (artifact == null) return null;
            try
            {
                if (artifact.CurrentXP < 0) return null;
                return (artifact.Level, artifact.CurrentXP, ArtifactModel.GetLevelXPRequirement(artifact.Level, artifact.ArtifactType));
            }
            catch (Exception e) { CoreLog.Warning("Encounter: reading an artifact's XP failed: " + e.Message); return null; }
        }

        // ---- the dice ----

        /// <summary>The base dice in play, in the tray's drawn order.</summary>
        public static List<Dice> Dice()
        {
            var dice = new List<Dice>();
            var panel = Controller?.DicePanel;
            if (panel == null) return dice;
            try
            {
                var order = panel.DiceVisualOrder;
                if (order != null)
                    foreach (var d in order)
                        if (d != null && d.gameObject.activeInHierarchy) dice.Add(d);
            }
            catch (Exception e) { CoreLog.Warning("Encounter: reading the dice failed: " + e.Message); }
            return dice;
        }

        // ---- the grid's tiles ----

        /// <summary>Whether the tile exists (<see cref="GridModel.BuiltCoords"/>): the grid grows by
        /// the shop's Build Tile, a column at a time from the bottom.</summary>
        public static bool Built(int x, int y)
        {
            try
            {
                var built = Model?.GridModel?.BuiltCoords;
                if (built == null) return true;
                foreach (var c in built)
                    if (c.x == x && c.y == y) return true;
                return false;
            }
            catch (Exception e) { CoreLog.Warning("Encounter: reading the built tiles failed: " + e.Message); return true; }
        }

        // ---- the shop ----

        /// <summary>The shop's choices still on offer (a bought one goes inactive), in its order.</summary>
        public static List<DraftChoice> Choices()
        {
            var choices = new List<DraftChoice>();
            var panel = Controller?.DraftPanel;
            if (panel == null || panel.DraftChoices == null) return choices;
            try
            {
                foreach (var choice in panel.DraftChoices)
                    if (choice != null && choice.IsActive && choice.gameObject.activeInHierarchy) choices.Add(choice);
            }
            catch (Exception e) { CoreLog.Warning("Encounter: reading the shop failed: " + e.Message); }
            return choices;
        }

        public static AViewItem ShopButton(string path)
        {
            var panel = Controller?.DraftPanel;
            var t = panel != null ? panel.transform.Find(path) : null;
            var item = t != null ? t.GetComponent<AViewItem>() : null;
            return ViewNodes.IsShown(item) ? item : null;
        }

        // ---- selections ----

        /// <summary>Whether the game waits on a choice of target (a unit to place, a tile to build).</summary>
        public static bool Selecting
        {
            get
            {
                try { return Controller?.SelectionController?.IsSelecting == true; }
                catch (Exception e) { CoreLog.Warning("Encounter: reading the selection failed: " + e.Message); return false; }
            }
        }

        private static int _targetsFrame = -1;
        private static readonly List<AEncounterViewItem> _targets = new List<AEncounterViewItem>();

        /// <summary>The view items the running selection takes, by the game's own test
        /// (<see cref="SelectionController.IsSatisfied"/>), read once a frame.</summary>
        public static List<AEncounterViewItem> Targets()
        {
            if (_targetsFrame == Time.frameCount) return _targets;
            _targetsFrame = Time.frameCount;
            _targets.Clear();
            var c = Controller;
            var sc = c?.SelectionController;
            if (sc == null || !Selecting) return _targets;
            try
            {
                foreach (var item in c.EncounterView.ViewsItems)
                {
                    if (item == null || !item.gameObject.activeInHierarchy) continue;
                    bool ok;
                    try { ok = sc.IsSatisfied(item); } catch { ok = false; }
                    if (ok) _targets.Add(item);
                }
            }
            catch (Exception e) { CoreLog.Warning("Encounter: listing the targets failed: " + e.Message); }
            return _targets;
        }

        /// <summary>The target the selection takes at the tile: the tile itself or the unit on it; null.</summary>
        public static AEncounterViewItem TargetAt(int x, int y)
        {
            foreach (var item in Targets())
            {
                var tile = item.TryCast<TileView>();
                if (tile != null && tile.TileModel != null && tile.TileModel.Coord.x == x && tile.TileModel.Coord.y == y) return item;
                var unit = item.TryCast<EntityView>();
                if (unit != null && unit.EntityModel != null)
                {
                    try
                    {
                        var grid = Model.GridModel;
                        if (!grid.IsEntityOnGrid(unit.EntityModel.EntityID)) continue;
                        var c = grid.GetEntityCoord(unit.EntityModel.EntityID, false);
                        if (c.x == x && c.y == y) return item;
                    }
                    catch (Exception e) { CoreLog.Warning("Encounter: placing a target failed: " + e.Message); }
                }
            }
            return null;
        }

        /// <summary>The column's artifact slot (<see cref="EncounterView.ArtifactViewObjects"/>, the
        /// first 6 being the columns'), when the selection takes it; null.</summary>
        public static AEncounterViewItem SlotTarget(int x)
        {
            var slot = SlotView(x);
            if (slot == null) return null;
            foreach (var item in Targets())
                if (item.Pointer == slot.Pointer) return item;
            return null;
        }

        public static ArtifactView SlotView(int x)
        {
            var slots = View?.ArtifactViewObjects;
            return slots != null && x < slots.Count ? slots[x] : null;
        }

        /// <summary>The tray's Launch button, when drawn.</summary>
        public static AViewItem LaunchButton()
        {
            var panel = Controller?.DicePanel;
            var t = panel != null ? panel.transform.Find("ProceedKnob/ProceedLine/LaunchPanel/LaunchButton") : null;
            var item = t != null ? t.GetComponent<AViewItem>() : null;
            return ViewNodes.IsShown(item) ? item : null;
        }
    }
}
