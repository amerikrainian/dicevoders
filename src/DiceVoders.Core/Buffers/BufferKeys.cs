namespace DiceVoders.Core.Buffers
{
    /// <summary>
    /// The registered buffer keys, in cycling order. The ui buffer carries the focused control's own
    /// lines (<see cref="NodeLines"/>); the units and perks buffers read the encounter whatever has
    /// focus; the events buffer is the encounter's narration log.
    /// <see cref="Graph.NodeVtable.SideLines"/> answers by key.
    /// </summary>
    public static class BufferKeys
    {
        public const string Ui = "ui";
        public const string Units = "units";
        public const string Perks = "perks";
        public const string Events = "events";
    }
}
