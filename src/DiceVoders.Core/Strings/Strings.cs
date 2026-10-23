using System;
using System.Collections.Generic;
using System.Text;

namespace DiceVoders.Core.Strings
{
    /// <summary>
    /// Central table for text the MOD itself authors and speaks (never game content, which is read live
    /// and already localized). Every authored word lives in <see cref="Defaults"/> as a key and its
    /// English value; typed accessors read through the loaded translation, so a lang/&lt;language&gt;.txt
    /// file overrides any value at speak time. Word order lives in "{0}"-style templates, never in code.
    /// Values are spoken by a screen reader: terse, lowercase unless shown otherwise, no decorative
    /// punctuation. Keys follow the wotr-access locale vocabulary (role.*, state.*, nav.*, screen.*).
    /// </summary>
    public static class Strings
    {
        private static KeyValuePair<string, string> D(string key, string value)
            => new KeyValuePair<string, string>(key, value);

        /// <summary>Every authored string, in template order: the key a translation file addresses and
        /// its English default.</summary>
        internal static readonly KeyValuePair<string, string>[] Defaults =
        {
            // Spoken once when the mod finishes loading; {0} = the mod version.
            D("app.loaded", "DiceVoders {0} loaded"),
            // Spoken at launch instead when the feature module fails to load (the mod is then dead).
            D("app.module_failed", "DiceVoders features failed to load"),
            // Follows the loaded line when the newest release outranks the running build; {0} = that
            // newer version. Up to date (or ahead, a dev build) stays silent.
            D("app.update_available", "update {0} available"),
            // Spoken when the focus-mode toggle engages / releases our keyboard navigation.
            D("app.focus_on", "navigation on"),
            D("app.focus_off", "navigation off"),

            // Control role words, spoken after the label ("Continue, button").
            D("role.button", "button"),
            D("role.toggle", "toggle"),
            D("role.slider", "slider"),
            D("role.radio button", "radio button"),
            D("role.combo box", "combo box"),
            D("role.tab", "tab"),
            D("role.item", "item"),
            D("role.heading", "heading"),
            D("role.table", "table"),
            D("role.link", "link"),
            D("role.edit", "edit"),

            // Control states.
            D("state.disabled", "disabled"),
            D("state.locked", "locked"),
            D("state.completed", "completed"),
            D("cs.random", "Random commander"),
            D("cs.challenges", "Challenges"),
            D("state.selected", "selected"),
            D("state.on", "on"),
            D("state.off", "off"),
            D("state.expanded", "expanded"),
            D("state.collapsed", "collapsed"),
            D("value.blank", "blank"),
            D("value.space", "space"),
            // A 0 to 1 slider's value (the volumes). {0} = the whole percent.
            D("value.percent", "{0} percent"),
            D("edit.selected", "{0} selected"),
            D("edit.unselected", "{0} unselected"),
            // The mod's own number entry: the decimal point as typed, a character erased with Backspace.
            D("edit.point", "point"),
            D("edit.deleted", "{0} deleted"),

            // Navigation feedback. {0} = index, {1} = count.
            D("nav.position", "{0}/{1}"),
            D("nav.no_tooltip", "no description"),
            D("nav.no_details", "no details"),
            // Buffer review (Ctrl plus arrows): the buffer names, the "name: line" readout on switching,
            // and the words when every buffer is empty.
            D("buffer.ui", "control"),
            D("buffer.none", "no buffer lines"),
            // {0} = the buffer's name, {1} = its current line.
            D("buffer.line", "{0}: {1}"),
            D("drag.no_target", "nothing to drag"),
            D("delete.no_target", "nothing to remove"),
            D("nav.minimum", "minimum"),
            D("nav.maximum", "maximum"),

            // Type-ahead search. {0} = the typed text.
            D("search.no_match", "no match for {0}"),
            D("search.cleared", "search cleared"),

            // Input action labels (for a key-help reader). Short imperative phrases.
            D("bind.ui.up", "Navigate up"),
            D("bind.ui.down", "Navigate down"),
            D("bind.ui.left", "Navigate left"),
            D("bind.ui.right", "Navigate right"),
            D("bind.ui.next", "Next control group"),
            D("bind.ui.prev", "Previous control group"),
            D("bind.ui.activate", "Activate"),
            D("bind.ui.secondary", "Secondary action"),
            D("bind.ui.back", "Back"),
            D("bind.ui.home", "Jump to first"),
            D("bind.ui.end", "Jump to last"),
            D("bind.ui.tooltip", "Read description"),
            D("bind.ui.regionPrev", "Previous section"),
            D("bind.ui.regionNext", "Next section"),
            D("bind.ui.readFocus", "Read current control"),
            // Buffer review (Ctrl plus arrows): switch between the review lists, step through one.
            D("bind.buffer.next", "Next buffer"),
            D("bind.buffer.prev", "Previous buffer"),
            D("bind.buffer.line.next", "Next buffer line"),
            D("bind.buffer.line.prev", "Previous buffer line"),
            D("bind.mod.focus", "Toggle navigation"),
            D("bind.mod.reload", "Reload mod code"),

            // Screen names, spoken on entry.
            D("screen.main_menu", "Main menu"),
            // The options, when its own title cannot be read.
            D("screen.settings", "Options"),
            D("screen.confirm", "Confirm"),
            // The boot scene's title screen (the arcade machine), before its own prompt.
            D("screen.title", "Title screen"),
            // The main menu's controls that carry no caption of their own (icons, a bare dropdown).
            D("main.language", "Language"),
            D("main.news", "News"),
            D("main.discord", "Discord"),
            D("main.credits", "Credits"),
            // The run's delete icon beside Resume Game (the game draws only its key prompt).
            D("main.delete_run", "Delete run"),
            // The credits: which page of how many ({0} of {1}), and the page arrows (drawn with only their key prompts).
            D("credits.page", "Page {0}/{1}"),
            D("credits.prev", "Previous page"),
            D("credits.next", "Next page"),
            // The role word for a modal dialog context.
            D("role.dialog", "dialog"),

            // The encounter. A tile: {0} = column, {1} = row, both 1-based, row 1 at the bottom.
            D("enc.coord", "{0}, {1}"),
            // A column's header above the grid; {0} = its 1-based number (a die showing it scores there).
            D("enc.column", "column {0}"),
            // A label and its figure ("Dice Power 2", "Budge 3"): {0} = the game's word, {1} = the number.
            D("enc.figure", "{0} {1}"),
            // A unit's strength (the game's STR); {0} = the figure.
            D("enc.strength", "{0} strength"),
            D("enc.empty", "empty"),
            D("enc.honey", "honey"),
            D("enc.negative", "negative"),
            // A unit's charges; {0} = left, {1} = the most it holds.
            D("enc.charge", "charge {0}/{1}"),
            D("enc.rolling", "rolling"),
            D("enc.frozen", "frozen"),
            D("enc.permafrozen", "permafrozen"),
            D("enc.cracked", "cracked"),
            // A die a pilot rolls against the player.
            D("enc.pilot_die", "pilot die"),
            // Dice Power a die adds on top of its own; {0} = the amount.
            D("enc.added_power", "plus {0} power"),
            // The attack gathered against the target: {0} = Total Attack, {1} = Target Defense.
            D("enc.attack", "{0}/{1} attack"),
            D("enc.hearts", "Hearts"),
            D("enc.swap", "Swap"),
            // The game refused a budge (no Budge left, the face at its end, already budged this roll).
            D("enc.cant_budge", "can't budge"),
            // A reward the game refuses (the Heart at full Hearts, under Scarcity).
            D("enc.cant_take", "can't take"),
            // The Budge button while a budge can be undone (the game draws an undo arrow on it), and
            // Enter on it when none can.
            D("enc.undo", "undo"),
            D("enc.nothing_to_undo", "nothing to undo"),
            D("enc.cant_freeze", "can't freeze"),
            D("enc.cant_reroll", "can't reroll"),
            // The dice after a roll; {0} = the faces in the tray's order.
            D("enc.rolled", "rolled {0}"),
            // What a column scored this turn; {0} = its 1-based number, {1} = the attack.
            D("enc.column_attack", "column {0}: {1}"),
            // Several dice scoring one column together; {0} = how many, {1} = their name ("3 Mini-Dice").
            D("enc.dice_count", "{0} {1}"),
            // The scoring board's sum once a turn's attack is multiplied; {0} = the attack, {1} = the
            // Mult ("Mult 2.0"), {2} = what the turn adds.
            D("enc.turn_score", "turn: {0}, {1}, {2}"),
            // What the game lights on the board for the hovered item; {0} = the units, tiles and
            // artifacts, comma-separated.
            D("enc.affects", "affects: {0}"),
            // The direction a unit scores in (the Oculus); {0} = one of the enc.dir_* words below.
            D("enc.looking", "looking {0}"),
            D("enc.dir_up", "up"),
            D("enc.dir_down", "down"),
            D("enc.dir_left", "left"),
            D("enc.dir_right", "right"),
            D("enc.dir_up_left", "up-left"),
            D("enc.dir_up_right", "up-right"),
            D("enc.dir_down_left", "down-left"),
            D("enc.dir_down_right", "down-right"),
            // An artifact or unit a pilot keeps from triggering or scoring (Xenn, Tippy).
            D("enc.blocked", "blocked"),
            // A column holding a pilot's trap (Kaia's Booby Trap: "When trapped column is scored...").
            D("enc.trapped", "trapped"),
            // A tile under a pilot's danger zone (Garu's Turret strikes it as the turn ends).
            D("enc.danger", "danger"),
            // What Tippy covers this turn, after its name ("Tippy: right tiles"): the two halves' artifact
            // slots, the two halves' tiles, the dice buttons.
            D("enc.area_left_artifacts", "left artifacts"),
            D("enc.area_right_artifacts", "right artifacts"),
            D("enc.area_left_tiles", "left tiles"),
            D("enc.area_right_tiles", "right tiles"),
            D("enc.area_buttons", "buttons"),
            // Blood Beast's Rage ("Rage 2") and the mode it brings on, as its perk's text names them.
            D("enc.rage", "Rage"),
            D("enc.beast_mode", "Beast Mode"),
            // The final boss's HP, drawn only as its panic bar ("HP 3").
            D("enc.boss_hp", "HP"),
            // The shop phase opening after a turn.
            D("enc.shop", "Shop"),
            // A tile the grid does not have yet (the shop's Build Tile adds them).
            D("enc.no_tile", "no tile"),
            // A cell or column the game is asking for (a unit to place, a tile to build).
            D("enc.target", "target"),
            // A shop item's price; {0} = the shop tokens (the game's Cash).
            D("enc.cost", "{0} cash"),
            D("enc.cursed", "cursed"),
            D("enc.cant_afford", "can't afford"),
            // Events. {0} = a unit's name, {1} = its tile ("2, 1").
            D("enc.unit_removed", "{0} at {1} destroyed"),
            D("enc.unit_added", "{0} added at {1}"),
            // {0} = a unit or artifact, {1} = where it went (a tile, a column).
            D("enc.moved", "{0} moved to {1}"),
            // Every unit's strength changed alike; {0} = the change, signed ("+5").
            D("enc.all_strength", "all units {0} strength"),
            // Something and what happened to it ("Migo at 2, 1: Shielded"): {0} = the thing, {1} = the news.
            D("enc.at", "{0}: {1}"),
            D("enc.unit_at", "{0} at {1}"),
            // A state or thing gone; {0} = it ("Shielded lost").
            D("enc.lost", "{0} lost"),
            D("enc.artifact_added", "{0} added"),
            D("enc.artifact_removed", "{0} removed"),
            // {0} = an artifact, {1} = its new level.
            D("enc.level", "{0} level {1}"),
            // {0} = the tile.
            D("enc.tile_built", "new tile at {0}"),
            // A tile's cloud gone.
            D("enc.clear", "clear"),
            // The shop's offer after a reroll; {0} = the items.
            D("enc.shop_items", "Shop: {0}"),
            // An artifact's XP; {0} = what it has, {1} = what its next level takes.
            D("enc.xp", "{0}/{1} XP"),
            // A game speed in the pause menu (its arrows draw no caption); {0} = 1 to 4.
            D("enc.speed", "speed {0}"),
            D("bind.enc.budge_up", "Budge up"),
            D("bind.enc.stats", "Stats"),
            D("bind.enc.mult", "Mult"),
            D("bind.enc.budge_down", "Budge down"),
            D("bind.enc.freeze", "Freeze"),
            D("bind.enc.reroll", "Reroll"),
            D("bind.enc.proceed", "Proceed"),
            D("bind.enc.cancel", "Cancel"),
            D("bind.enc.coords", "Coordinates"),
            D("bind.enc.attack", "Attack"),
            D("bind.enc.turns", "Turns and hearts"),
            D("bind.enc.tokens", "Tokens"),
            D("bind.enc.dice", "Dice"),
            D("bind.enc.target", "Target"),
            D("bind.enc.first_column", "First column"),
            D("bind.enc.last_column", "Last column"),
            D("bind.enc.top_row", "Top row"),
            D("bind.enc.bottom_row", "Bottom row"),

            D("screen.mod_menu", "DiceVoders"),
            D("mod.settings", "Settings"),
            // The mod menu's list of every key the mod binds, whatever the screen.
            D("mod.key_help", "All keys"),
            D("mod.close", "Close"),
            D("mod.speak_positions", "Speak list positions"),
            D("mod.focus_on_launch", "Keyboard navigation on at launch"),
            // {0} = the key category, one of the three names below.
            D("mod.key_category", "{0} keys"),
            // The key categories of the "All keys" list: live everywhere, on the focused screen, in a run.
            D("mod.category.global", "Global"),
            D("mod.category.ui", "UI"),
            D("mod.category.game", "Game"),
            D("bind.mod.menu", "Mod menu"),
            // The context-sensitive key help (F1): the keys that do something on this screen, on this
            // control. Enter on a row runs it.
            D("bind.mod.help", "Keys here"),
            D("screen.help", "Keys here"),
            D("help.none", "no keys here"),
            // {0} = what the key does, {1} = the keys bound to it.
            D("help.row", "{0}: {1}"),
            // A key binding row waiting for its new key (the game's rebind); {0} = the action.
            D("rebind.prompt", "Press a key for {0}, Escape cancels"),
            // The new key is also one of the mod's: {0} = the key, {1} = what the mod does with it.
            D("rebind.conflict", "{0} is also {1}"),
        };

        private static readonly Dictionary<string, string> _defaults = BuildDefaults();
        private static Dictionary<string, string> _overrides = new Dictionary<string, string>();

        private static Dictionary<string, string> BuildDefaults()
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in Defaults) d[kv.Key] = kv.Value;
            return d;
        }

        /// <summary>The value for a key: the loaded translation, else the English default, else the
        /// key itself (a missing key is then audible rather than silent).</summary>
        public static string Get(string key)
        {
            if (_overrides.TryGetValue(key, out var o)) return o;
            if (_defaults.TryGetValue(key, out var v)) return v;
            CoreLog.Warning("Strings: missing key " + key);
            return key;
        }

        /// <summary>Whether a key exists in the defaults (for optional lookups like role words).</summary>
        public static bool Has(string key) => _defaults.ContainsKey(key) || _overrides.ContainsKey(key);

        /// <summary>The value for a key with "{0}"-style slots filled.</summary>
        public static string F(string key, params object[] args)
        {
            string template = Get(key);
            try { return string.Format(template, args); }
            catch (FormatException) { return template; }
        }

        /// <summary>Install a translation, replacing any previous one whole (a language switch never
        /// blends two files): "key=value" lines split at the first '=' ("#" comments and blanks
        /// ignored, a written-out "\n" a line break); missing keys fall back to English. Pass null to
        /// return to the defaults. What the file got wrong is set aside, never applied, and comes
        /// back in the report for the caller to log: a line without a key, a key this table does not
        /// define (a typo, a file for a newer mod), an empty value (it would silence information), a
        /// value with a slot its English has not ("{2}" where the code fills two: the line would be
        /// spoken as the raw template).</summary>
        public static TranslationReport LoadTranslation(IEnumerable<string> lines)
        {
            var report = new TranslationReport();
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (lines != null)
            {
                int n = 0;
                foreach (var raw in lines)
                {
                    n++;
                    if (raw == null) continue;
                    string line = raw.Trim().TrimStart('\uFEFF');
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) { report.Malformed.Add(n); continue; }
                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim().Replace("\\n", "\n");
                    if (!_defaults.TryGetValue(key, out var english)) { report.UnknownKeys.Add(key); continue; }
                    if (value.Length == 0) { report.EmptyKeys.Add(key); continue; }
                    if (!SlotsFit(value, english)) { report.BadSlots.Add(key); continue; }
                    map[key] = value;
                }
            }
            _overrides = map;
            report.Applied = map.Count;
            return report;
        }

        // Whether a translated template formats with the arguments its English takes: every "{n}" of
        // it one the English has, and the braces well formed.
        public static bool SlotsFit(string value, string english)
        {
            int max = -1;
            foreach (int slot in Slots(english)) if (slot > max) max = slot;
            try { string.Format(value, new object[Math.Max(max + 1, 0)]); }
            catch (FormatException) { return false; }
            return true;
        }

        /// <summary>The "{n}" slots a template uses, for the translation checks.</summary>
        public static HashSet<int> Slots(string template)
        {
            var slots = new HashSet<int>();
            if (template == null) return slots;
            for (int i = 0; i < template.Length - 2; i++)
            {
                if (template[i] != '{' || !char.IsDigit(template[i + 1])) continue;
                int j = i + 1, slot = 0;
                while (j < template.Length && char.IsDigit(template[j])) slot = slot * 10 + (template[j++] - '0');
                if (j < template.Length && (template[j] == '}' || template[j] == ':' || template[j] == ',')) slots.Add(slot);
            }
            return slots;
        }

        /// <summary>Every key of the table, in template order (the translation tests walk it).</summary>
        public static IEnumerable<string> Keys
        {
            get { foreach (var kv in Defaults) yield return kv.Key; }
        }

        /// <summary>A key's English default, or null.</summary>
        public static string Default(string key) => _defaults.TryGetValue(key, out var v) ? v : null;

        private const string TemplateHeader =
            "# DiceVoders: the words the mod itself speaks (the game's own text is the game's).\n" +
            "# This file is the template, generated from the mod's strings table: copy it to\n" +
            "# <language>.txt, named for the code of the game's selected locale (de, fr, es, ja, ko,\n" +
            "# ru, tr, pt-BR, ...), and translate the values. key=value, one per line; keep every {0} slot (their\n" +
            "# order is free); a missing key is spoken in English. The mod follows the game's language.\n";

        /// <summary>The translator template (lang/en.txt): every key with its English default, one per
        /// line, under a header that tells a translator what to do with it.</summary>
        public static string DumpTemplate()
        {
            var sb = new StringBuilder();
            sb.Append(TemplateHeader);
            foreach (var kv in Defaults)
                sb.Append(kv.Key).Append('=').Append(kv.Value.Replace("\n", "\\n")).Append('\n');
            return sb.ToString();
        }

        // ---- typed accessors ----

        public static string ModLoaded(string version) => F("app.loaded", version);
        public static string ModuleFailed => Get("app.module_failed");
        public static string UpdateAvailable(string version) => F("app.update_available", version);
        public static string FocusOn => Get("app.focus_on");
        public static string FocusOff => Get("app.focus_off");

        /// <summary>The role word for a control type key ("button" -> "button"); null when none.</summary>
        public static string Role(string word) => Has("role." + word) ? Get("role." + word) : null;
        public static string RoleTable => Get("role.table");
        public static string RoleHeading => Get("role.heading");

        public static string StateDisabled => Get("state.disabled");
        public static string StateLocked => Get("state.locked");
        public static string StateCompleted => Get("state.completed");
        public static string StateSelected => Get("state.selected");
        public static string StateOn => Get("state.on");
        public static string StateOff => Get("state.off");
        public static string ExpandedState(bool expanded) => Get(expanded ? "state.expanded" : "state.collapsed");
        public static string ValueBlank => Get("value.blank");
        public static string ValueSpace => Get("value.space");
        public static string Percent(int percent) => F("value.percent", percent);
        public static string EditSelected(string text) => F("edit.selected", text);
        public static string EditUnselected(string text) => F("edit.unselected", text);
        public static string EditPoint => Get("edit.point");
        public static string EditDeleted(string text) => F("edit.deleted", text);

        public static string Position(int index, int count) => F("nav.position", index, count);
        public static string NoTooltip => Get("nav.no_tooltip");
        public static string NoDetails => Get("nav.no_details");
        public static string BufferUi => Get("buffer.ui");
        public static string BufferNone => Get("buffer.none");
        public static string BufferLine(string name, string line) => F("buffer.line", name, line);
        public static string NoDragTarget => Get("drag.no_target");
        public static string NoDeleteTarget => Get("delete.no_target");
        public static string Minimum => Get("nav.minimum");
        public static string Maximum => Get("nav.maximum");

        public static string SearchNoMatch(string text) => F("search.no_match", text);
        public static string SearchCleared => Get("search.cleared");

        /// <summary>The spoken label of an input action ("bind.ui.down" -> "Navigate down"); the
        /// registration label when no entry exists.</summary>
        public static string BindLabel(string actionKey, string fallback)
            => Has("bind." + actionKey) ? Get("bind." + actionKey) : fallback;

        public static string ScreenMainMenu => Get("screen.main_menu");
        public static string ScreenSettings => Get("screen.settings");
        public static string ScreenConfirm => Get("screen.confirm");
        public static string ScreenTitle => Get("screen.title");
        public static string MainLanguage => Get("main.language");
        public static string MainNews => Get("main.news");
        public static string MainDiscord => Get("main.discord");
        public static string MainCredits => Get("main.credits");
        public static string MainDeleteRun => Get("main.delete_run");
        public static string CreditsPage(int page, int count) => F("credits.page", page, count);
        public static string CreditsPrevious => Get("credits.prev");
        public static string CreditsNext => Get("credits.next");
        public static string RoleDialog => Get("role.dialog");

        public static string EncCoord(int column, int row) => F("enc.coord", column, row);
        public static string EncColumn(int column) => F("enc.column", column);
        public static string EncFigure(string label, string figure) => F("enc.figure", label, figure);
        public static string EncStrength(string figure) => F("enc.strength", figure);
        public static string EncEmpty => Get("enc.empty");
        public static string EncHoney => Get("enc.honey");
        public static string EncNegative => Get("enc.negative");
        public static string EncCharge(int left, int max) => F("enc.charge", left, max);
        public static string EncRolling => Get("enc.rolling");
        public static string EncFrozen => Get("enc.frozen");
        public static string EncPermafrozen => Get("enc.permafrozen");
        public static string EncCracked => Get("enc.cracked");
        public static string EncPilotDie => Get("enc.pilot_die");
        public static string EncAddedPower(int amount) => F("enc.added_power", amount);
        public static string EncAttack(string total, string target) => F("enc.attack", total, target);
        public static string EncHearts => Get("enc.hearts");
        public static string EncSwap => Get("enc.swap");
        public static string EncCantBudge => Get("enc.cant_budge");
        public static string EncCantTake => Get("enc.cant_take");
        public static string EncUndo => Get("enc.undo");
        public static string EncNothingToUndo => Get("enc.nothing_to_undo");
        public static string EncCantFreeze => Get("enc.cant_freeze");
        public static string EncCantReroll => Get("enc.cant_reroll");
        public static string EncRolled(string faces) => F("enc.rolled", faces);
        public static string EncColumnAttack(int column, string attack) => F("enc.column_attack", column, attack);
        public static string EncDiceCount(int count, string dice) => F("enc.dice_count", count, dice);
        public static string EncTurnScore(string attack, string mult, string total) => F("enc.turn_score", attack, mult, total);
        public static string EncAffects(string what) => F("enc.affects", what);
        public static string EncLooking(string direction) => F("enc.looking", direction);
        public static string EncDirUp => Get("enc.dir_up");
        public static string EncDirDown => Get("enc.dir_down");
        public static string EncDirLeft => Get("enc.dir_left");
        public static string EncDirRight => Get("enc.dir_right");
        public static string EncDirUpLeft => Get("enc.dir_up_left");
        public static string EncDirUpRight => Get("enc.dir_up_right");
        public static string EncDirDownLeft => Get("enc.dir_down_left");
        public static string EncDirDownRight => Get("enc.dir_down_right");
        public static string EncBlocked => Get("enc.blocked");
        public static string EncTrapped => Get("enc.trapped");
        public static string EncDanger => Get("enc.danger");
        public static string EncAreaLeftArtifacts => Get("enc.area_left_artifacts");
        public static string EncAreaRightArtifacts => Get("enc.area_right_artifacts");
        public static string EncAreaLeftTiles => Get("enc.area_left_tiles");
        public static string EncAreaRightTiles => Get("enc.area_right_tiles");
        public static string EncAreaButtons => Get("enc.area_buttons");
        public static string EncRage => Get("enc.rage");
        public static string EncBeastMode => Get("enc.beast_mode");
        public static string EncBossHp => Get("enc.boss_hp");
        public static string EncShop => Get("enc.shop");
        public static string EncNoTile => Get("enc.no_tile");
        public static string EncTarget => Get("enc.target");
        public static string EncCost(int cost) => F("enc.cost", cost);
        public static string EncCursed => Get("enc.cursed");
        public static string EncCantAfford => Get("enc.cant_afford");
        public static string EncUnitRemoved(string name, string place) => F("enc.unit_removed", name, place);
        public static string EncUnitAdded(string name, string place) => F("enc.unit_added", name, place);
        public static string EncMoved(string what, string where) => F("enc.moved", what, where);
        public static string EncAllStrength(string delta) => F("enc.all_strength", delta);
        public static string EncAt(string what, string news) => F("enc.at", what, news);
        public static string EncUnitAt(string name, string place) => F("enc.unit_at", name, place);
        public static string EncLost(string what) => F("enc.lost", what);
        public static string EncArtifactAdded(string name) => F("enc.artifact_added", name);
        public static string EncArtifactRemoved(string name) => F("enc.artifact_removed", name);
        public static string EncLevel(string name, int level) => F("enc.level", name, level);
        public static string EncTileBuilt(string place) => F("enc.tile_built", place);
        public static string EncClear => Get("enc.clear");
        public static string EncShopItems(string items) => F("enc.shop_items", items);
        public static string EncXp(int xp, int needed) => F("enc.xp", xp, needed);
        public static string EncSpeed(int speed) => F("enc.speed", speed);

        public static string ScreenModMenu => Get("screen.mod_menu");
        public static string ModSettings => Get("mod.settings");
        public static string ModKeyHelp => Get("mod.key_help");
        public static string ScreenHelp => Get("screen.help");
        public static string HelpNone => Get("help.none");
        public static string HelpRow(string label, string keys) => F("help.row", label, keys);
        public static string RebindPrompt(string action) => F("rebind.prompt", action);
        public static string RebindConflict(string key, string what) => F("rebind.conflict", key, what);
        public static string ModClose => Get("mod.close");
        public static string ModSpeakPositions => Get("mod.speak_positions");
        public static string ModFocusOnLaunch => Get("mod.focus_on_launch");
        public static string ModKeyCategory(string category)
        {
            string name = "mod.category." + (category ?? "").ToLowerInvariant();
            return F("mod.key_category", Has(name) ? Get(name) : category);
        }
    }
}

