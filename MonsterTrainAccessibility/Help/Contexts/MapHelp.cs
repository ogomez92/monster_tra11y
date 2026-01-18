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
                   "Up/Down arrows: Move between rings. " +
                   "Left/Right arrows: Choose between left path, center battle, or right path. " +
                   "Enter: Select the current node. " +
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
