using System;
using DiceVoders.Core.Strings;

namespace DiceVoders.Core.Buffers
{
    /// <summary>
    /// The four buffer review commands, composed and spoken here so every screen shares one
    /// behavior: switching a buffer speaks its name and current line; stepping speaks just the
    /// line; the edges re-read the current line rather than going silent. A buffer that follows its
    /// latest line (an event log, oldest first, opened on the newest) steps the other way, so Next
    /// goes to older lines as it goes on in every other buffer. Speech is injected so
    /// this stays unit-testable. Ported from Harkest Dungeon (dd2a11y).
    /// </summary>
    public sealed class BufferControls
    {
        private readonly BufferManager _buffers;
        private readonly Action<string, bool> _speak;

        public BufferControls(BufferManager buffers, Action<string, bool> speak)
        {
            _buffers = buffers;
            _speak = speak;
        }

        public void NextBuffer()
        {
            _buffers.MoveBuffer(1);
            ReportBuffer();
        }

        public void PreviousBuffer()
        {
            _buffers.MoveBuffer(-1);
            ReportBuffer();
        }

        public void NextLine()
        {
            var buffer = _buffers.Current;
            if (buffer == null) { _speak(Strings.Strings.BufferNone, true); return; }
            if (buffer.FollowLatest) buffer.MovePrevious();
            else buffer.MoveNext();
            ReportLine(buffer);
        }

        public void PreviousLine()
        {
            var buffer = _buffers.Current;
            if (buffer == null) { _speak(Strings.Strings.BufferNone, true); return; }
            if (buffer.FollowLatest) buffer.MoveNext();
            else buffer.MovePrevious();
            ReportLine(buffer);
        }

        private void ReportBuffer()
        {
            var buffer = _buffers.Current;
            if (buffer == null) { _speak(Strings.Strings.BufferNone, true); return; }
            _speak(Strings.Strings.BufferLine(buffer.Label, buffer.CurrentLine ?? ""), true);
        }

        private void ReportLine(Buffer buffer)
        {
            var line = buffer.CurrentLine;
            _speak(line ?? Strings.Strings.BufferNone, true);
        }
    }
}
