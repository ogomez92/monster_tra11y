namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for the map/route selection screen
    /// </summary>
    public class MapHelp : IHelpContext
    {
        public string ContextId => "map";
        public string ContextName => "Map";
        public int Priority => 60;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.Map;
        }

        public string GetHelpText()
        {
            return "Arrow keys: Navigate between available paths. " +
                   "Enter: Select path and proceed to next encounter. " +
                   "C: Re-read current node type. " +
                   "T: Read all available paths. " +
                   "Escape: Open pause menu.";
        }
    }
}
