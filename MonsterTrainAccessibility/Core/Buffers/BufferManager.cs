using System;
using System.Collections.Generic;

namespace MonsterTrainAccessibility.Core.Buffers
{
    /// <summary>
    /// Manages the set of review buffers and handles the buffer hotkeys:
    /// Ctrl+Up/Down move through the current buffer's items,
    /// Ctrl+Left/Right switch between available buffers.
    /// Modeled on Say the Spire's buffer system.
    /// </summary>
    public class BufferManager
    {
        private const int EVENTS_BUFFER_CAP = 200;

        private readonly List<AnnouncementBuffer> _buffers = new List<AnnouncementBuffer>();
        private int _currentIndex = -1;

        /// <summary>
        /// History of game events (combat log). Always registered first.
        /// </summary>
        public AnnouncementBuffer Events { get; }

        public BufferManager()
        {
            Events = new AnnouncementBuffer("Events", EVENTS_BUFFER_CAP)
            {
                FollowLatest = true
            };
            _buffers.Add(Events);
        }

        /// <summary>
        /// Register a contextual buffer. The refresher rebuilds its items when
        /// the buffer is focused; returning null marks it unavailable.
        /// </summary>
        public AnnouncementBuffer Register(string name, Func<List<string>> refresher = null)
        {
            var buffer = new AnnouncementBuffer(name) { Refresher = refresher };
            _buffers.Add(buffer);
            return buffer;
        }

        /// <summary>
        /// Append a game event to the events buffer.
        /// </summary>
        public void AddEvent(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            Events.Add(text.Trim());
        }

        public AnnouncementBuffer CurrentBuffer =>
            (_currentIndex >= 0 && _currentIndex < _buffers.Count) ? _buffers[_currentIndex] : null;

        /// <summary>
        /// Ctrl+Up: move forward through the current buffer
        /// </summary>
        public void NextItem()
        {
            var buffer = EnsureCurrentBuffer();
            if (buffer == null)
                return;

            if (buffer.MoveNext())
                Speak(buffer.CurrentItem);
            else
                Speak(buffer.Count == 0 ? $"{buffer.Name}: empty" : $"End of {buffer.Name}");
        }

        /// <summary>
        /// Ctrl+Down: move backward through the current buffer
        /// </summary>
        public void PreviousItem()
        {
            var buffer = EnsureCurrentBuffer();
            if (buffer == null)
                return;

            if (buffer.MovePrevious())
                Speak(buffer.CurrentItem);
            else
                Speak(buffer.Count == 0 ? $"{buffer.Name}: empty" : $"Start of {buffer.Name}");
        }

        /// <summary>
        /// Ctrl+Right: switch to the next available buffer
        /// </summary>
        public void NextBuffer()
        {
            SwitchBuffer(1);
        }

        /// <summary>
        /// Ctrl+Left: switch to the previous available buffer
        /// </summary>
        public void PreviousBuffer()
        {
            SwitchBuffer(-1);
        }

        private void SwitchBuffer(int direction)
        {
            if (_buffers.Count == 0)
                return;

            // With nothing focused yet, start from outside the list so the
            // first candidate is the first (or last) buffer rather than the second
            int start = _currentIndex >= 0 ? _currentIndex : (direction > 0 ? -1 : _buffers.Count);
            // Walk the buffer list once, skipping buffers with no content
            for (int step = 1; step <= _buffers.Count; step++)
            {
                int index = ((start + direction * step) % _buffers.Count + _buffers.Count) % _buffers.Count;
                var candidate = _buffers[index];
                if (!candidate.Refresh())
                    continue;

                _currentIndex = index;
                candidate.OnFocused();
                AnnounceCurrentBuffer();
                return;
            }

            Speak("No information available");
        }

        /// <summary>
        /// Make sure some buffer is focused before item navigation.
        /// Returns null (after announcing) if nothing has content.
        /// </summary>
        private AnnouncementBuffer EnsureCurrentBuffer()
        {
            var current = CurrentBuffer;
            if (current != null)
            {
                // Refresh contextual buffers so positions stay in range, but
                // keep the buffer focused even if it just became empty.
                current.Refresh();
                return current;
            }

            for (int i = 0; i < _buffers.Count; i++)
            {
                if (_buffers[i].Refresh())
                {
                    _currentIndex = i;
                    var buffer = _buffers[i];
                    buffer.OnFocused();
                    AnnounceCurrentBuffer();
                    return null; // The focus announcement already read the current item
                }
            }

            Speak("No information available");
            return null;
        }

        private void AnnounceCurrentBuffer()
        {
            var buffer = CurrentBuffer;
            if (buffer == null)
                return;

            string item = buffer.CurrentItem;
            if (string.IsNullOrEmpty(item))
                Speak($"{buffer.Name}: empty");
            else
                Speak($"{buffer.Name}, {buffer.Position} of {buffer.Count}: {item}");
        }

        private static void Speak(string text)
        {
            MonsterTrainAccessibility.ScreenReader?.Speak(text, false);
        }
    }
}
