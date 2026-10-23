using System.Collections.Generic;
using DiceVoders.Core.Input;

namespace DiceVoders.Module.UI
{
    /// <summary>
    /// The Run key category: keys that read the run itself (Ctrl+A attack, Ctrl+T turns and hearts,
    /// Ctrl+R tokens, Ctrl+D dice, Ctrl+O target, Ctrl+M Mult), live on the encounter and on the
    /// overlays above it (the game menu, its options and strategy guide, the result and victory
    /// screens, constellations, tips, a confirmation) while the encounter is underneath; not on the
    /// main menu's own guide or options.
    /// </summary>
    internal static class RunKeys
    {
        private static readonly InputCategory[] RunUi = { InputCategory.Run, InputCategory.UI };
        private static readonly InputCategory[] UiOnly = { InputCategory.UI };

        /// <summary>The encounter scene is loaded: a run is under way.</summary>
        public static bool InRun => Encounter.Encounter.Controller != null;

        /// <summary>A screen's categories: the Run keys and UI navigation in a run, else UI alone.</summary>
        public static IReadOnlyList<InputCategory> Categories => InRun ? RunUi : UiOnly;
    }
}
