using DiceVoders.Core.Buffers;
using DiceVoders.Core.Graph;
using DiceVoders.Core.Strings;
using DiceVoders.Core.UI;
using Buffer = DiceVoders.Core.Buffers.Buffer;

namespace DiceVoders.Module.UI
{
    /// <summary>
    /// The mod's buffer roster and its review keys (Ctrl plus arrows), the Harkest Dungeon pattern:
    /// review lists for the information a focus announcement leaves out: the focused control's own
    /// lines (its head, then one line per description part and detail); the units on the encounter's
    /// grid and the run's perks (<see cref="Encounter.EncounterBuffers"/>); the encounter's events (every
    /// line the narration said, uncapped, oldest first, opening on the newest). Every buffer
    /// reads live on each keypress; a focus change rewinds every buffer (one that follows its latest
    /// line to its end) and leaves review on whichever buffer is being read.
    /// </summary>
    internal static class Buffers
    {
        public static BufferManager Manager { get; private set; }
        public static BufferControls Controls { get; private set; }

        private static ControlId _homed;

        public static void Init()
        {
            Manager = new BufferManager();
            Controls = new BufferControls(Manager, (text, interrupt) => Core.Speech.Say(text, interrupt: interrupt));
            Manager.Add(new Buffer(BufferKeys.Ui, () => Strings.BufferUi)).SetSource(() => NodeLines.Lines(Navigation.FocusedNode));
            Manager.Add(new Buffer(BufferKeys.Units, Screens.EncounterScreen.Units)).SetSource(Encounter.EncounterBuffers.Units);
            Manager.Add(new Buffer(BufferKeys.Perks, Screens.EncounterScreen.Perks)).SetSource(Encounter.EncounterBuffers.Perks);
            // The encounter's narration, oldest first, opening on the newest; empty outside a run.
            var events = Manager.Add(new Buffer(BufferKeys.Events, () => Strings.BufferEvents) { FollowLatest = true });
            events.SetSource(() => Encounter.Encounter.Controller != null ? Encounter.EncounterEvents.Log : null);
        }

        /// <summary>Per frame: a focus change (by key or by the game moving things) rewinds every
        /// buffer (one that follows its latest line to its end).</summary>
        public static void Tick()
        {
            if (Manager == null) return;
            var id = Navigation.FocusedNode?.Id;
            if (Equals(id, _homed)) return;
            _homed = id;
            foreach (var buffer in Manager.Buffers)
            {
                if (buffer.FollowLatest) buffer.MoveToEnd();
                else buffer.Reset();
            }
            // Review stays on the buffer being read: only the control buffer's content follows focus.
        }
    }
}
