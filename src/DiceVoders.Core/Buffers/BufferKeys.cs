namespace DiceVoders.Core.Buffers
{
    /// <summary>
    /// The registered buffer keys, in cycling order. The ui buffer carries the focused control's own
    /// lines (<see cref="NodeLines"/>). <see cref="Graph.NodeVtable.SideLines"/> answers by key.
    /// </summary>
    public static class BufferKeys
    {
        public const string Ui = "ui";
    }
}
