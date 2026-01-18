namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for event/choice screens
    /// </summary>
    public class EventHelp : IHelpContext
    {
        public string ContextId => "event";
        public string ContextName => "Event";
        public int Priority => 70;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.Event;
        }

        public string GetHelpText()
        {
            return "Up and Down arrows: Browse event choices. " +
                   "Enter: Select choice. " +
                   "C: Re-read current choice and effects. " +
                   "T: Read full event text and all choices.";
        }
    }
}
