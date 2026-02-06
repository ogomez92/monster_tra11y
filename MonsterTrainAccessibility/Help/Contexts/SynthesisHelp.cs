namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for the unit synthesis screen (DLC)
    /// </summary>
    public class SynthesisHelp : IHelpContext
    {
        public string ContextId => "synthesis";
        public string ContextName => "Unit Synthesis";
        public int Priority => 70;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.Synthesis;
        }

        public string GetHelpText()
        {
            return "Arrow keys: Browse available units. " +
                   "Enter: Select unit for synthesis. " +
                   "C: Re-read current unit details. " +
                   "T: Read all available units. " +
                   "Escape: Cancel synthesis.";
        }
    }
}
