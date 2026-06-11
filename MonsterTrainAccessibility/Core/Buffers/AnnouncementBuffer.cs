using System;
using System.Collections.Generic;

namespace MonsterTrainAccessibility.Core.Buffers
{
    /// <summary>
    /// A reviewable list of announcements or information items, navigated with
    /// Ctrl+Up/Down (modeled on the Monster Train 2 accessibility mod's buffers).
    ///
    /// Items are stored in review order: index 0 is the current/top item (the
    /// newest event, or the first detail line). The cursor starts at -1, one
    /// step before the top, so the first Ctrl+Up reads the top item.
    /// Ctrl+Up (MoveDeeper) walks deeper into the buffer (older events,
    /// further detail lines); Ctrl+Down (MoveTowardTop) walks back toward
    /// the top, matching the MT2 mod's direction convention.
    ///
    /// Buffers either accumulate items over time (events buffer) or rebuild
    /// their contents from game state via a Refresher when used (UI, card,
    /// hand, floors, and so on).
    /// </summary>
    public class AnnouncementBuffer
    {
        public string Name { get; }

        /// <summary>
        /// When true, focusing this buffer moves the cursor back to the
        /// starting point so the first Ctrl+Up reads the newest item.
        /// Used by the events buffer.
        /// </summary>
        public bool FollowLatest { get; set; }

        /// <summary>
        /// Optional callback that rebuilds the buffer contents from game state.
        /// Return null to mark the buffer unavailable (it is skipped when
        /// cycling buffers). Buffers without a refresher keep accumulated items.
        /// </summary>
        public Func<List<string>> Refresher { get; set; }

        private readonly List<string> _items = new List<string>();
        private readonly int _maxItems;
        private int _position = -1;

        public AnnouncementBuffer(string name, int maxItems = 0)
        {
            Name = name;
            _maxItems = maxItems;
        }

        public int Count => _items.Count;

        public string CurrentItem =>
            (_position >= 0 && _position < _items.Count) ? _items[_position] : null;

        /// <summary>
        /// Add a new item at the top of the buffer, trimming the oldest entries
        /// past the cap. Keeps the cursor pointing at the same item while reviewing.
        /// </summary>
        public void Add(string item)
        {
            if (string.IsNullOrEmpty(item))
                return;

            _items.Insert(0, item);
            if (_position >= 0)
                _position++;

            if (_maxItems > 0 && _items.Count > _maxItems)
            {
                _items.RemoveRange(_maxItems, _items.Count - _maxItems);
                if (_position >= _maxItems)
                    _position = _maxItems - 1;
            }
        }

        public void Clear()
        {
            _items.Clear();
            _position = -1;
        }

        /// <summary>
        /// Rebuild contents from the Refresher (if any). When the content
        /// actually changed (focus moved to a new element, hand changed),
        /// the cursor returns to the starting point above the top item.
        /// Returns false if the refresher reports the buffer unavailable.
        /// </summary>
        public bool Refresh()
        {
            if (Refresher == null)
                return _items.Count > 0;

            List<string> fresh;
            try
            {
                fresh = Refresher();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error refreshing buffer {Name}: {ex.Message}");
                return false;
            }

            if (fresh == null)
            {
                // Context gone (e.g. battle ended) - drop stale items
                Clear();
                return false;
            }

            if (!ContentEquals(fresh))
            {
                _items.Clear();
                _items.AddRange(fresh);
                _position = -1;
            }

            return _items.Count > 0;
        }

        /// <summary>
        /// Called when this buffer becomes the current one.
        /// </summary>
        public void OnFocused()
        {
            if (FollowLatest)
                _position = -1;
            else if (_position >= _items.Count)
                _position = _items.Count - 1;
        }

        /// <summary>
        /// Ctrl+Up: move deeper into the buffer (older events, further detail
        /// lines). From the starting point this reads the top (current/newest)
        /// item. Returns false at the end (no wrap).
        /// </summary>
        public bool MoveDeeper()
        {
            if (_position >= _items.Count - 1)
                return false;

            _position++;
            return true;
        }

        /// <summary>
        /// Ctrl+Down: move back toward the top item. From the starting point
        /// this settles on the top item. Returns false at the top (no wrap).
        /// </summary>
        public bool MoveTowardTop()
        {
            if (_position < 0)
            {
                if (_items.Count == 0)
                    return false;

                _position = 0;
                return true;
            }

            if (_position == 0)
                return false;

            _position--;
            return true;
        }

        private bool ContentEquals(List<string> other)
        {
            if (other.Count != _items.Count)
                return false;

            for (int i = 0; i < _items.Count; i++)
            {
                if (!string.Equals(_items[i], other[i], StringComparison.Ordinal))
                    return false;
            }
            return true;
        }
    }
}
