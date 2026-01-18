namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Fallback help context that is always active.
    /// Provides basic navigation help that applies to all screens.
    /// </summary>
    public class GlobalHelp : IHelpContext
    {
        public string ContextId => "global";
        public string ContextName => "General";
        public int Priority => 0; // Lowest priority - only used as fallback

        public bool IsActive()
        {
            // Always active as fallback
            return true;
        }

        public string GetHelpText()
        {
            return "F1: Context-sensitive help. " +
                   "C: Re-read current item. " +
                   "T: Read all text on screen. " +
                   "V: Cycle verbosity level. " +
                   "Arrow keys: Navigate menus. " +
                   "Enter or Space: Activate selected item. " +
                   "Escape: Go back or cancel.";
        }
    }
}
