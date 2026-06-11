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
            return "Map screen. Navigate your path through each ring. " +
                   "Map browsing with the virtual map cursor: " +
                   "Ctrl plus Up: Move forward one ring toward the final boss. " +
                   "Ctrl plus Down: Move back one ring. " +
                   "Ctrl plus Left/Right: Step through the stops on that ring, " +
                   "including which path they are on: left, right, or both. " +
                   "Each ring announces its number, whether it is your current position, " +
                   "the battle waiting at its end, and how many stops it has. " +
                   "The cursor is for review only; it does not move your real selection. " +
                   "Moving and selecting: " +
                   "Up/Down arrows: Move between rings. " +
                   "Left/Right arrows: Choose between left path, center battle, or right path. " +
                   "Enter: Select and go to the focused node. " +
                   "M: Re-read current map node with coordinates. " +
                   "C: Re-read current node details. " +
                   "T: Read all available choices for this ring. " +
                   "Node types: Battle (required fight), Merchant (buy/sell cards), " +
                   "Artifact (gain relic), Upgrade (enhance cards), Event (random encounter), " +
                   "Concealed Caverns (mystery reward), Pyre Remains (restore pyre health), " +
                   "Hellvent (remove cards). " +
                   "Escape: Open pause menu.";
        }
    }
}
