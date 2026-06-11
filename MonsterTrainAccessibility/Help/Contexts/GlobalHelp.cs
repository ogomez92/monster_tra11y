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
                   "C: Re-read current item; for cards, shop items and artifacts this reads the full details. " +
                   "T: Read all text on screen. " +
                   "V: Cycle verbosity level. " +
                   "Arrow keys: Navigate menus. " +
                   "Enter or Space: Activate selected item. " +
                   "Escape: Go back or cancel. " +
                   "Ctrl plus G: Read gold. " +
                   "Ctrl plus H: Read pyre health. " +
                   "Ctrl plus R: Read pact shards. " +
                   "Ctrl plus Up: Read the next item in the current review buffer. " +
                   "Ctrl plus Down: Move back toward the top of the buffer. " +
                   "Ctrl plus Left/Right: Switch between buffers, such as the focused element or the events history.";
        }
    }
}
