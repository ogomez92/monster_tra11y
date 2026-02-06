namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for the champion upgrade screen
    /// </summary>
    public class ChampionUpgradeHelp : IHelpContext
    {
        public string ContextId => "champion_upgrade";
        public string ContextName => "Champion Upgrade";
        public int Priority => 80;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.ChampionUpgrade;
        }

        public string GetHelpText()
        {
            return "Left and Right arrows: Browse upgrade paths. " +
                   "Enter: Select upgrade. " +
                   "C: Re-read current upgrade details. " +
                   "T: Read all upgrade options.";
        }
    }
}
