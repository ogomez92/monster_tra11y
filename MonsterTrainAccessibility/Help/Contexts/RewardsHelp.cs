namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for the rewards screen (post-battle)
    /// </summary>
    public class RewardsHelp : IHelpContext
    {
        public string ContextId => "rewards";
        public string ContextName => "Rewards";
        public int Priority => 75;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.Rewards;
        }

        public string GetHelpText()
        {
            return "Arrow keys: Browse rewards. " +
                   "Enter: Collect selected reward. " +
                   "C: Re-read current reward. " +
                   "T: Read all rewards. " +
                   "Escape: Skip remaining rewards (if allowed).";
        }
    }
}
