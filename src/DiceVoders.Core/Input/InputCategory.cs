namespace DiceVoders.Core.Input
{
    /// <summary>
    /// The input layer an action belongs to. Screens declare which categories they use
    /// (<see cref="Screens.Screen.InputCategories"/>, in priority order); the live set each frame is the
    /// union of every active screen's declaration walked focus-first, plus <see cref="Global"/>, which is
    /// always on. Within the live set, an identical chord bound in two categories resolves to the
    /// higher-priority (deeper) one (shadowing), so the same arrows can navigate a menu when focused and
    /// drive something else when not. Conflict prevention therefore only applies WITHIN a category.
    /// </summary>
    public enum InputCategory
    {
        /// <summary>Always live, even when focus mode is off (focus toggle, mod menu, reload).</summary>
        Global,
        /// <summary>Screen/menu navigation: live when the focused screen declares it.</summary>
        UI,
        /// <summary>The encounter's gameplay keys (the board, the dice, the shop), declared by the encounter screen.</summary>
        Game,
        /// <summary>Keys that read the run itself (attack, turns and hearts, tokens...), live anywhere in a
        /// run: declared by the encounter and the overlays above it while it is underneath.</summary>
        Run,
    }
}
