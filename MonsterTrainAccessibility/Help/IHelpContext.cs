namespace MonsterTrainAccessibility.Help
{
    /// <summary>
    /// Interface for context-sensitive help providers.
    /// Each context represents a different screen or mode where different keys are available.
    /// </summary>
    public interface IHelpContext
    {
        /// <summary>
        /// Unique identifier for this context
        /// </summary>
        string ContextId { get; }

        /// <summary>
        /// Human-readable name of this context
        /// </summary>
        string ContextName { get; }

        /// <summary>
        /// Priority for context detection. Higher values are checked first.
        /// This allows more specific contexts (like floor targeting during battle)
        /// to take precedence over general contexts (like battle).
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Check if this context is currently active.
        /// Should return true if the user is currently in this context.
        /// </summary>
        bool IsActive();

        /// <summary>
        /// Get the help text to announce for this context.
        /// Should include available keys and their functions.
        /// </summary>
        string GetHelpText();
    }
}
