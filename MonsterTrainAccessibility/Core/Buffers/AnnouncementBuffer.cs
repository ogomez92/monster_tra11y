using System;
using System.Collections.Generic;

namespace MonsterTrainAccessibility.Core.Buffers
{
    /// <summary>
    /// A reviewable list of announcements or information items, navigated with
    /// Ctrl+Up/Down (modeled on Say the Spire's buffer system).
    /// Buffers either accumulate items over time (events buffer) or rebuild
    /// their contents from game state via a Refresher when focused (hand,
    /// floors, units, resources).
    /// </summary>
    public class AnnouncementBuffer
    {
        public string Name { get; }

        /// <summary>
        /// When true, focusing this buffer jumps to the newest item instead of
        /// staying at the saved position. Used by the events buffer.
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
        /// 1-based position for "x of y" announcements
        /// </summary>
        public int Position => _position + 1;

        /// <summary>
        /// Append an item, trimming the oldest entries past the cap.
        /// Keeps the review position pointing at the same item while reviewing.
        /// </summary>
        public void Add(string item)
        {
            if (string.IsNullOrEmpty(item))
                return;

            _items.Add(item);

            if (_maxItems > 0 && _items.Count > _maxItems)
            {
                int removeCount = _items.Count - _maxItems;
                _items.RemoveRange(0, removeCount);
                if (_position >= 0)
                    _position = Math.Max(0, _position - removeCount);
            }
        }

        public void Clear()
        {
            _items.Clear();
            _position = -1;
        }

        /// <summary>
        /// Rebuild contents from the Refresher (if any), preserving position.
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
                _items.Clear();
                _position = -1;
                return false;
            }

            _items.Clear();
            _items.AddRange(fresh);

            if (_items.Count == 0)
                _position = -1;
            else if (_position >= _items.Count)
                _position = _items.Count - 1;

            return _items.Count > 0;
        }

        /// <summary>
        /// Called when this buffer becomes the current one.
        /// </summary>
        public void OnFocused()
        {
            if (_items.Count == 0)
            {
                _position = -1;
                return;
            }

            if (FollowLatest)
                _position = _items.Count - 1;
            else if (_position < 0 || _position >= _items.Count)
                _position = 0;
        }

        /// <summary>
        /// Move toward the newest/last item. Returns false at the end (no wrap).
        /// </summary>
        public bool MoveNext()
        {
            if (_items.Count == 0)
                return false;

            if (_position < 0)
            {
                _position = 0;
                return true;
            }

            if (_position >= _items.Count - 1)
                return false;

            _position++;
            return true;
        }

        /// <summary>
        /// Move toward the oldest/first item. Returns false at the start (no wrap).
        /// </summary>
        public bool MovePrevious()
        {
            if (_items.Count == 0)
                return false;

            if (_position < 0)
            {
                _position = _items.Count - 1;
                return true;
            }

            if (_position <= 0)
                return false;

            _position--;
            return true;
        }
    }
}
