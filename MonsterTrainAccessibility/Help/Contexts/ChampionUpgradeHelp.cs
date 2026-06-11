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
                   "C: Read the focused upgrade's full details. " +
                   "Ctrl plus Up: Step through the upgrade's details in the review buffer. " +
                   "Ctrl plus Left/Right: Switch review buffers. " +
                   "T: Read all upgrade options.";
        }
    }
}
