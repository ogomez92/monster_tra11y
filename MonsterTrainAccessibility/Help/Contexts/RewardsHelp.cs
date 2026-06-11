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
                   "C: Read the focused reward's full details. " +
                   "Ctrl plus Up: Step through the reward's details in the review buffer, one piece at a time. " +
                   "Ctrl plus Down: Move back toward the top of the buffer. " +
                   "Ctrl plus Left/Right: Switch review buffers, such as Card or Reward. " +
                   "T: Read all rewards. " +
                   "Escape: Skip remaining rewards (if allowed).";
        }
    }
}
