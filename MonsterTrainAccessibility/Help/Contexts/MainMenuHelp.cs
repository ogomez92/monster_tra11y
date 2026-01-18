namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for the main menu screen
    /// </summary>
    public class MainMenuHelp : IHelpContext
    {
        public string ContextId => "main_menu";
        public string ContextName => "Main Menu";
        public int Priority => 40;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.MainMenu;
        }

        public string GetHelpText()
        {
            return "Up and Down arrows: Navigate menu options. " +
                   "Enter: Select highlighted option. " +
                   "C: Re-read current option. " +
                   "T: Read all menu options. " +
                   "Escape: Exit game or go back.";
        }
    }
}
