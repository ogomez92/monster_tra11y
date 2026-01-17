using System;
using System.Collections.Generic;

namespace MonsterTrainAccessibility.Core
{
    /// <summary>
    /// Manages the virtual focus system for keyboard navigation.
    /// Tracks current focus context and handles navigation between items.
    /// </summary>
    public class VirtualFocusManager
    {
        /// <summary>
        /// The current navigation context (determines available items and navigation rules)
        /// </summary>
        public FocusContext CurrentContext { get; private set; }

        /// <summary>
        /// The currently focused item within the current context
        /// </summary>
        public FocusableItem CurrentFocus { get; private set; }

        /// <summary>
        /// Stack of previous contexts for back navigation
        /// </summary>
        private Stack<FocusContext> _contextStack = new Stack<FocusContext>();

        /// <summary>
        /// Event fired when focus changes to a new item
        /// </summary>
        public event Action<FocusableItem, FocusableItem> OnFocusChanged;

        /// <summary>
        /// Event fired when context changes
        /// </summary>
        public event Action<FocusContext, FocusContext> OnContextChanged;

        /// <summary>
        /// Whether the focus manager is active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Set a new focus context, pushing the current one onto the stack
        /// </summary>
        public void SetContext(FocusContext newContext, bool pushToStack = true)
        {
            if (newContext == null)
                return;

            var oldContext = CurrentContext;

            // Save current context to stack
            if (pushToStack && oldContext != null)
            {
                oldContext.OnExit();
                _contextStack.Push(oldContext);
            }

            // Set new context
            CurrentContext = newContext;
            CurrentContext.OnEnter();

            // Focus the default item
            CurrentFocus = newContext.GetDefaultFocus();

            // Fire events
            OnContextChanged?.Invoke(oldContext, newContext);

            // Announce the new context and focus
            AnnounceContextChange();
        }

        /// <summary>
        /// Replace the current context without pushing to stack
        /// </summary>
        public void ReplaceContext(FocusContext newContext)
        {
            if (newContext == null)
                return;

            var oldContext = CurrentContext;
            oldContext?.OnExit();

            CurrentContext = newContext;
            CurrentContext.OnEnter();
            CurrentFocus = newContext.GetDefaultFocus();

            OnContextChanged?.Invoke(oldContext, newContext);
            AnnounceContextChange();
        }

        /// <summary>
        /// Navigate up
        /// </summary>
        public void NavigateUp()
        {
            if (!IsActive || CurrentContext == null)
                return;

            var newFocus = CurrentContext.GetItemAbove(CurrentFocus);
            SetFocus(newFocus);
        }

        /// <summary>
        /// Navigate down
        /// </summary>
        public void NavigateDown()
        {
            if (!IsActive || CurrentContext == null)
                return;

            var newFocus = CurrentContext.GetItemBelow(CurrentFocus);
            SetFocus(newFocus);
        }

        /// <summary>
        /// Navigate left
        /// </summary>
        public void NavigateLeft()
        {
            if (!IsActive || CurrentContext == null)
                return;

            var newFocus = CurrentContext.GetItemLeft(CurrentFocus);
            SetFocus(newFocus);
        }

        /// <summary>
        /// Navigate right
        /// </summary>
        public void NavigateRight()
        {
            if (!IsActive || CurrentContext == null)
                return;

            var newFocus = CurrentContext.GetItemRight(CurrentFocus);
            SetFocus(newFocus);
        }

        /// <summary>
        /// Activate the currently focused item (Enter key)
        /// </summary>
        public void Activate()
        {
            if (!IsActive || CurrentFocus == null)
                return;

            try
            {
                CurrentFocus.Activate();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error activating item: {ex.Message}");
            }
        }

        /// <summary>
        /// Go back to previous context (Escape key)
        /// </summary>
        public void GoBack()
        {
            if (!IsActive)
                return;

            if (_contextStack.Count > 0)
            {
                // Pop previous context
                CurrentContext?.OnExit();
                var previousContext = _contextStack.Pop();

                var oldContext = CurrentContext;
                CurrentContext = previousContext;
                CurrentContext.OnEnter();

                // Restore previous focus
                CurrentFocus = previousContext.LastFocus ?? previousContext.GetDefaultFocus();

                OnContextChanged?.Invoke(oldContext, previousContext);
                AnnounceContextChange();
            }
            else
            {
                // Let the current context handle back
                CurrentContext?.HandleBack();
            }
        }

        /// <summary>
        /// Set focus to a specific item
        /// </summary>
        public void SetFocus(FocusableItem newFocus)
        {
            if (newFocus == null || newFocus == CurrentFocus || !newFocus.CanFocus)
                return;

            var oldFocus = CurrentFocus;
            CurrentFocus = newFocus;

            // Remember last focus in context
            if (CurrentContext != null)
            {
                CurrentContext.LastFocus = newFocus;
            }

            OnFocusChanged?.Invoke(oldFocus, newFocus);
            AnnounceFocusChange();
        }

        /// <summary>
        /// Set focus by index
        /// </summary>
        public void SetFocusByIndex(int index)
        {
            if (CurrentContext == null)
                return;

            var item = CurrentContext.GetItemByIndex(index);
            if (item != null)
            {
                SetFocus(item);
            }
        }

        /// <summary>
        /// Re-read the current focus
        /// </summary>
        public void RereadCurrentFocus()
        {
            if (CurrentFocus != null)
            {
                string description = CurrentFocus.GetAccessibleDescription();
                MonsterTrainAccessibility.ScreenReader?.Speak(description, true);
            }
            else if (CurrentContext != null)
            {
                MonsterTrainAccessibility.ScreenReader?.Speak($"{CurrentContext.ContextName}. No items.", true);
            }
        }

        /// <summary>
        /// Read detailed description of current item
        /// </summary>
        public void ReadDetailedDescription()
        {
            if (CurrentFocus != null)
            {
                string description = CurrentFocus.GetDetailedDescription();
                MonsterTrainAccessibility.ScreenReader?.Speak(description, true);
            }
        }

        /// <summary>
        /// Read summary of all items in current context
        /// </summary>
        public void ReadAllItems()
        {
            if (CurrentContext != null)
            {
                string summary = CurrentContext.GetSummary();
                MonsterTrainAccessibility.ScreenReader?.Speak(summary, true);
            }
        }

        /// <summary>
        /// Clear the context stack
        /// </summary>
        public void ClearStack()
        {
            while (_contextStack.Count > 0)
            {
                _contextStack.Pop().OnExit();
            }
        }

        /// <summary>
        /// Refresh the current context
        /// </summary>
        public void RefreshContext()
        {
            CurrentContext?.Refresh();

            // Re-acquire focus if current item is no longer valid
            if (CurrentFocus == null || !CurrentFocus.CanFocus)
            {
                CurrentFocus = CurrentContext?.GetDefaultFocus();
            }
        }

        private void AnnounceContextChange()
        {
            if (CurrentContext == null)
                return;

            // Announce the new context name
            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen(CurrentContext.ContextName);

            // Then announce the focused item with position
            AnnounceFocusChange();
        }

        private void AnnounceFocusChange()
        {
            if (CurrentFocus == null)
            {
                if (CurrentContext != null && CurrentContext.ItemCount == 0)
                {
                    MonsterTrainAccessibility.ScreenReader?.Queue("No items");
                }
                return;
            }

            // Get position info
            int position = CurrentContext?.GetItemPosition(CurrentFocus) ?? -1;
            int total = CurrentContext?.ItemCount ?? 0;

            string description = CurrentFocus.GetAccessibleDescription();

            if (position > 0 && total > 1)
            {
                description = $"{description}. {position} of {total}";
            }

            MonsterTrainAccessibility.ScreenReader?.AnnounceFocus(description);
        }
    }
}
