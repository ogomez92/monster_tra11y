using System;
using System.Collections.Generic;

namespace MonsterTrainAccessibility.Core.Buffers
{
    /// <summary>
    /// Manages the set of review buffers and handles the buffer hotkeys:
    /// Ctrl+Up/Down move through the current buffer's items (Up reads the
    /// current/top item first, then deeper into older events / further detail
    /// lines; Down moves back toward the top), Ctrl+Left/Right switch between
    /// available buffers.
    /// Modeled on the Monster Train 2 accessibility mod's buffer system.
    ///
    /// Buffers are cycled in registration order; buffers whose refresher
    /// reports no content are skipped.
    /// </summary>
    public class BufferManager
    {
        private const int EVENTS_BUFFER_CAP = 200;

        private readonly List<AnnouncementBuffer> _buffers = new List<AnnouncementBuffer>();
        private int _currentIndex = -1;

        /// <summary>
        /// History of game events (combat log). Created here so events can be
        /// recorded from the start; registered into the cycle order by
        /// FocusBuffers.Register.
        /// </summary>
        public AnnouncementBuffer Events { get; }

        public BufferManager()
        {
            Events = new AnnouncementBuffer("Events", EVENTS_BUFFER_CAP)
            {
                FollowLatest = true
            };
        }

        /// <summary>
        /// Register a contextual buffer. The refresher rebuilds its items when
        /// the buffer is used; returning null marks it unavailable.
        /// </summary>
        public AnnouncementBuffer Register(string name, Func<List<string>> refresher = null)
        {
            var buffer = new AnnouncementBuffer(name) { Refresher = refresher };
            _buffers.Add(buffer);
            return buffer;
        }

        /// <summary>
        /// Register an existing buffer at the next cycle position.
        /// </summary>
        public void Register(AnnouncementBuffer buffer)
        {
            if (buffer != null && !_buffers.Contains(buffer))
                _buffers.Add(buffer);
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
        /// Ctrl+Up: move deeper into the buffer (older events, further detail
        /// lines). From the starting point this reads the buffer's current/top item.
        /// </summary>
        public void PreviousItem()
        {
            var buffer = EnsureCurrentBuffer();
            if (buffer == null)
                return;

            if (buffer.Count == 0)
            {
                Speak($"{buffer.Name}: empty");
                return;
            }

            // At the end, re-read the last item instead of a boundary message
            buffer.MoveDeeper();
            SpeakItem(buffer);
        }

        /// <summary>
        /// Ctrl+Down: move back toward the buffer's top item.
        /// </summary>
        public void NextItem()
        {
            var buffer = EnsureCurrentBuffer();
            if (buffer == null)
                return;

            if (buffer.Count == 0)
            {
                Speak($"{buffer.Name}: empty");
                return;
            }

            // At the top, re-read the top item instead of a boundary message
            buffer.MoveTowardTop();
            SpeakItem(buffer);
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
        /// Returns null (after announcing) if nothing has content, or after
        /// announcing the newly focused buffer so the user starts oriented.
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
                    _buffers[i].OnFocused();
                    AnnounceCurrentBuffer();
                    return null; // Swallow the first move so the user starts oriented
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

            string count = buffer.Count == 1 ? "1 item" : $"{buffer.Count} items";
            Speak($"{buffer.Name}, {count}");
        }

        private static void SpeakItem(AnnouncementBuffer buffer)
        {
            Speak(buffer.CurrentItem);
        }

        private static void Speak(string text)
        {
            MonsterTrainAccessibility.ScreenReader?.Speak(text, false);
        }
    }
}
