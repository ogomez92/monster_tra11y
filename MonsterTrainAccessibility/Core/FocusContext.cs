using System;
using System.Collections.Generic;
using System.Linq;

namespace MonsterTrainAccessibility.Core
{
    /// <summary>
    /// Base class for navigation contexts.
    /// Each screen or mode has its own FocusContext that defines how navigation works.
    /// </summary>
    public abstract class FocusContext
    {
        /// <summary>
        /// Name of this context (e.g., "Main Menu", "Hand", "Floor Selection")
        /// </summary>
        public string ContextName { get; protected set; }

        /// <summary>
        /// The last focused item in this context (for restoring focus when returning)
        /// </summary>
        public FocusableItem LastFocus { get; set; }

        /// <summary>
        /// All focusable items in this context
        /// </summary>
        protected List<FocusableItem> Items { get; set; } = new List<FocusableItem>();

        /// <summary>
        /// Number of rows for grid navigation
        /// </summary>
        protected int Rows { get; set; } = 1;

        /// <summary>
        /// Number of columns for grid navigation
        /// </summary>
        protected int Columns { get; set; } = 1;

        /// <summary>
        /// Whether navigation wraps at boundaries
        /// </summary>
        protected bool WrapNavigation { get; set; } = true;

        /// <summary>
        /// Get the item that should be focused by default when entering this context
        /// </summary>
        public virtual FocusableItem GetDefaultFocus()
        {
            // Return last focus if available, otherwise first item
            if (LastFocus != null && Items.Contains(LastFocus) && LastFocus.CanFocus)
            {
                return LastFocus;
            }
            return Items.FirstOrDefault(i => i.CanFocus);
        }

        /// <summary>
        /// Get the item above the current item
        /// </summary>
        public virtual FocusableItem GetItemAbove(FocusableItem current)
        {
            if (current == null || Items.Count == 0)
                return null;

            int newRow = current.Row - 1;
            if (newRow < 0)
            {
                newRow = WrapNavigation ? Rows - 1 : 0;
            }

            return GetItemAt(newRow, current.Column) ?? current;
        }

        /// <summary>
        /// Get the item below the current item
        /// </summary>
        public virtual FocusableItem GetItemBelow(FocusableItem current)
        {
            if (current == null || Items.Count == 0)
                return null;

            int newRow = current.Row + 1;
            if (newRow >= Rows)
            {
                newRow = WrapNavigation ? 0 : Rows - 1;
            }

            return GetItemAt(newRow, current.Column) ?? current;
        }

        /// <summary>
        /// Get the item to the left of the current item
        /// </summary>
        public virtual FocusableItem GetItemLeft(FocusableItem current)
        {
            if (current == null || Items.Count == 0)
                return null;

            int newCol = current.Column - 1;
            if (newCol < 0)
            {
                newCol = WrapNavigation ? Columns - 1 : 0;
            }

            return GetItemAt(current.Row, newCol) ?? current;
        }

        /// <summary>
        /// Get the item to the right of the current item
        /// </summary>
        public virtual FocusableItem GetItemRight(FocusableItem current)
        {
            if (current == null || Items.Count == 0)
                return null;

            int newCol = current.Column + 1;
            if (newCol >= Columns)
            {
                newCol = WrapNavigation ? 0 : Columns - 1;
            }

            return GetItemAt(current.Row, newCol) ?? current;
        }

        /// <summary>
        /// Get item at specific grid position
        /// </summary>
        protected virtual FocusableItem GetItemAt(int row, int col)
        {
            return Items.FirstOrDefault(i => i.Row == row && i.Column == col && i.CanFocus);
        }

        /// <summary>
        /// Get item by linear index
        /// </summary>
        public virtual FocusableItem GetItemByIndex(int index)
        {
            if (index >= 0 && index < Items.Count)
            {
                var item = Items[index];
                return item.CanFocus ? item : null;
            }
            return null;
        }

        /// <summary>
        /// Handle the back/escape action
        /// </summary>
        public abstract void HandleBack();

        /// <summary>
        /// Refresh the items list from current game state
        /// </summary>
        public abstract void Refresh();

        /// <summary>
        /// Get a summary of all items (for "read all" functionality)
        /// </summary>
        public virtual string GetSummary()
        {
            if (Items.Count == 0)
            {
                return $"{ContextName} is empty";
            }

            var descriptions = Items
                .Where(i => i.CanFocus)
                .Select((item, index) => $"{index + 1}: {item.GetBriefLabel()}");

            return $"{ContextName} contains {Items.Count} items. " + string.Join(". ", descriptions);
        }

        /// <summary>
        /// Get the total number of focusable items
        /// </summary>
        public int ItemCount => Items.Count(i => i.CanFocus);

        /// <summary>
        /// Get the position of an item (1-based for announcements)
        /// </summary>
        public int GetItemPosition(FocusableItem item)
        {
            var focusable = Items.Where(i => i.CanFocus).ToList();
            int index = focusable.IndexOf(item);
            return index >= 0 ? index + 1 : -1;
        }

        /// <summary>
        /// Called when this context becomes active
        /// </summary>
        public virtual void OnEnter()
        {
            Refresh();
        }

        /// <summary>
        /// Called when this context is deactivated
        /// </summary>
        public virtual void OnExit()
        {
            // Override in subclasses if needed
        }
    }

    /// <summary>
    /// A simple list-based context for vertical menus
    /// </summary>
    public class ListFocusContext : FocusContext
    {
        private Action _onBack;

        public ListFocusContext(string name, Action onBack = null)
        {
            ContextName = name;
            Columns = 1;
            _onBack = onBack;
        }

        public void AddItem(FocusableItem item)
        {
            item.Index = Items.Count;
            item.Row = Items.Count;
            item.Column = 0;
            Items.Add(item);
            Rows = Items.Count;
        }

        public void ClearItems()
        {
            Items.Clear();
            Rows = 0;
        }

        public override void HandleBack()
        {
            _onBack?.Invoke();
        }

        public override void Refresh()
        {
            // Override in subclasses to rebuild items from game state
        }
    }

    /// <summary>
    /// A grid-based context for card selection, etc.
    /// </summary>
    public class GridFocusContext : FocusContext
    {
        private Action _onBack;
        private int _itemsPerRow;

        public GridFocusContext(string name, int itemsPerRow, Action onBack = null)
        {
            ContextName = name;
            _itemsPerRow = itemsPerRow;
            Columns = itemsPerRow;
            _onBack = onBack;
        }

        public void SetItems(List<FocusableItem> items)
        {
            Items = items;

            // Assign grid positions
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].Index = i;
                Items[i].Row = i / _itemsPerRow;
                Items[i].Column = i % _itemsPerRow;
            }

            Rows = (Items.Count + _itemsPerRow - 1) / _itemsPerRow;
            Columns = Math.Min(_itemsPerRow, Items.Count);
        }

        public override void HandleBack()
        {
            _onBack?.Invoke();
        }

        public override void Refresh()
        {
            // Override in subclasses to rebuild items from game state
        }
    }
}
