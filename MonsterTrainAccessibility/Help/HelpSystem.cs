using System.Collections.Generic;
using System.Linq;
using MonsterTrainAccessibility.Utilities;

namespace MonsterTrainAccessibility.Help
{
    /// <summary>
    /// Coordinates context-sensitive help by managing multiple help contexts
    /// and selecting the appropriate one based on current game state.
    ///
    /// Like the Monster Train 2 accessibility mod, F1 opens a browsable help
    /// list: Up/Down read one entry at a time, and F1, Enter, or Escape close
    /// it. While help is open, those keys are kept away from the game (see
    /// InputSuppressionPatch).
    /// </summary>
    public class HelpSystem
    {
        private readonly List<IHelpContext> _contexts = new List<IHelpContext>();
        private readonly List<string> _entries = new List<string>();
        private int _entryIndex = -1;
        private int _closedFrame = -1;

        /// <summary>
        /// True while the help list is open and Up/Down browse its entries.
        /// </summary>
        public bool IsBrowsing { get; private set; }

        /// <summary>
        /// True for the rest of the frame in which help was closed. The key
        /// that closed help (Enter/Escape/Space) reads as pressed for the whole
        /// frame, so consumers that run later in the same frame must keep
        /// treating it as claimed or it would also confirm/cancel in the game.
        /// </summary>
        public bool ClosedThisFrame => _closedFrame == UnityEngine.Time.frameCount;

        /// <summary>
        /// Register a help context
        /// </summary>
        public void RegisterContext(IHelpContext context)
        {
            _contexts.Add(context);
            // Keep sorted by priority (highest first)
            _contexts.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            MonsterTrainAccessibility.LogInfo($"Registered help context: {context.ContextId} (priority {context.Priority})");
        }

        /// <summary>
        /// Register multiple contexts at once
        /// </summary>
        public void RegisterContexts(params IHelpContext[] contexts)
        {
            foreach (var context in contexts)
            {
                RegisterContext(context);
            }
        }

        /// <summary>
        /// Toggle the help list for the current context. Opening loads one
        /// entry per available action; the user browses them with Up/Down.
        /// </summary>
        public void ShowHelp()
        {
            if (IsBrowsing)
            {
                CloseHelp();
                return;
            }

            var activeContext = GetActiveContext();
            string contextName = activeContext?.ContextName ?? "General";
            string helpText = activeContext?.GetHelpText() ?? GetFallbackHelp();
            MonsterTrainAccessibility.LogInfo($"Showing help for context: {activeContext?.ContextId ?? "fallback"}");

            _entries.Clear();
            _entries.AddRange(TextUtilities.SplitIntoSpeechItems(helpText));
            _entryIndex = -1;
            IsBrowsing = _entries.Count > 0;

            if (!IsBrowsing)
            {
                Speak($"{contextName} help. No entries available.");
                return;
            }

            Speak($"{contextName} help, {_entries.Count} entries. " +
                  "Down arrow to read entries. Press F1, Enter, or Escape to close.");
        }

        /// <summary>
        /// Down arrow: read the next help entry.
        /// </summary>
        public void NextEntry()
        {
            if (!IsBrowsing)
                return;

            // At the end, re-read the last entry instead of a boundary message
            if (_entryIndex < _entries.Count - 1)
                _entryIndex++;
            AnnounceEntry();
        }

        /// <summary>
        /// Up arrow: read the previous help entry.
        /// </summary>
        public void PreviousEntry()
        {
            if (!IsBrowsing)
                return;

            // At the top, re-read the first entry instead of a boundary message
            if (_entryIndex > 0)
                _entryIndex--;
            else
                _entryIndex = 0;
            AnnounceEntry();
        }

        /// <summary>
        /// Close the help list (F1 again, Enter, or Escape).
        /// </summary>
        public void CloseHelp()
        {
            if (!IsBrowsing)
                return;

            IsBrowsing = false;
            _closedFrame = UnityEngine.Time.frameCount;
            _entries.Clear();
            _entryIndex = -1;
            Speak("Help closed");
        }

        /// <summary>
        /// Get the currently active context (highest priority that returns IsActive = true)
        /// </summary>
        public IHelpContext GetActiveContext()
        {
            return _contexts.FirstOrDefault(c => c.IsActive());
        }

        /// <summary>
        /// Get the name of the current context (for status announcements)
        /// </summary>
        public string GetCurrentContextName()
        {
            return GetActiveContext()?.ContextName ?? "Unknown";
        }

        private void AnnounceEntry()
        {
            Speak(_entries[_entryIndex]);
        }

        /// <summary>
        /// Fallback help text when no context matches
        /// </summary>
        private string GetFallbackHelp()
        {
            return "F1: Help. " +
                   "C: Re-read current item. " +
                   "T: Read all text on screen. " +
                   "V: Cycle verbosity. " +
                   "Arrow keys: Navigate. " +
                   "Enter or Space: Activate. " +
                   "Escape: Back or cancel.";
        }

        private static void Speak(string text)
        {
            MonsterTrainAccessibility.ScreenReader?.Speak(text, false);
        }
    }
}
