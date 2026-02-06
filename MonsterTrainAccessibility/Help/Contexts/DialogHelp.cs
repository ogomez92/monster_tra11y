namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for dialog/popup screens
    /// </summary>
    public class DialogHelp : IHelpContext
    {
        public string ContextId => "dialog";
        public string ContextName => "Dialog";
        public int Priority => 110;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.Dialog;
        }

        public string GetHelpText()
        {
            return "Enter: Confirm or accept. " +
                   "Escape: Cancel or dismiss. " +
                   "C: Re-read dialog text. " +
                   "Arrow keys: Navigate between buttons.";
        }
    }
}
