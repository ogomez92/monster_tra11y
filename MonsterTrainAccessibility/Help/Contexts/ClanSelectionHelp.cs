namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for the clan/class selection screen
    /// </summary>
    public class ClanSelectionHelp : IHelpContext
    {
        public string ContextId => "clan_selection";
        public string ContextName => "Clan Selection";
        public int Priority => 50;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.ClanSelection;
        }

        public string GetHelpText()
        {
            return "Left and Right arrows: Browse clans. " +
                   "Enter: Select highlighted clan. " +
                   "C: Re-read current clan name and description. " +
                   "T: Read all available clans. " +
                   "Escape: Return to main menu.";
        }
    }
}
