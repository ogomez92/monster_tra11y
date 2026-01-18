using System.Collections.Generic;
using System.Linq;

namespace MonsterTrainAccessibility.Help
{
    /// <summary>
    /// Coordinates context-sensitive help by managing multiple help contexts
    /// and selecting the appropriate one based on current game state.
    /// </summary>
    public class HelpSystem
    {
        private readonly List<IHelpContext> _contexts = new List<IHelpContext>();

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
        /// Show help for the current context.
        /// Finds the first active context (by priority) and speaks its help text.
        /// </summary>
        public void ShowHelp()
        {
            var activeContext = GetActiveContext();
            if (activeContext != null)
            {
                string helpText = activeContext.GetHelpText();
                MonsterTrainAccessibility.LogInfo($"Showing help for context: {activeContext.ContextId}");
                MonsterTrainAccessibility.ScreenReader?.Speak($"{activeContext.ContextName} help. {helpText}", true);
            }
            else
            {
                // Fallback if no context is active
                MonsterTrainAccessibility.ScreenReader?.Speak(GetFallbackHelp(), true);
            }
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
    }
}
