namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for the battle intro screen (pre-battle, showing enemy info and Fight button)
    /// </summary>
    public class BattleIntroHelp : IHelpContext
    {
        public string ContextId => "battle_intro";
        public string ContextName => "Battle Intro";
        public int Priority => 85; // Higher than shop/event, lower than battle

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.BattleIntro;
        }

        public string GetHelpText()
        {
            return "Battle Intro screen. Preview enemies before fighting. " +
                   "Left/Right arrows: Navigate between enemy previews and Fight button. " +
                   "Enter: On Fight button, start the battle. On enemy, hear details. " +
                   "C: Re-read current selection. " +
                   "T: Read all screen text including battle name and enemies. " +
                   "This screen shows which enemies will appear on each floor. " +
                   "Boss battles display a larger boss preview. " +
                   "Escape: Return to map without fighting.";
        }
    }
}
