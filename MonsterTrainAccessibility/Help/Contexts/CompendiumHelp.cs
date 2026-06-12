namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for the logbook/compendium screen (meta progression,
    /// card and artifact collections, leaderboards, lifetime stats)
    /// </summary>
    public class CompendiumHelp : IHelpContext
    {
        public string ContextId => "compendium";
        public string ContextName => "Logbook";
        public int Priority => 65;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.Compendium;
        }

        public string GetHelpText()
        {
            return "Page Up and Page Down: Switch sections: Checklist, Cards, Champion Upgrades, Artifacts, Card Frames, Statistics. " +
                   "Left and Right arrows: Turn pages within a section. " +
                   "Arrow keys: Move between items on the page. " +
                   "T: Read a full summary of the Checklist or Statistics section. " +
                   "F: On the Checklist, switch between standard progress and The Last Divinity page. On Statistics, switch between Stats Leaderboard and Personal Records. " +
                   "On the Checklist: clan rows announce name and level; their XP, champion unlocks, victories per allied clan, and card collection go to the review buffer. " +
                   "C: Re-read the focused item with full details. " +
                   "Ctrl plus Up and Down: Step through the focused item's details. " +
                   "Ctrl plus Left and Right: Switch review buffers. " +
                   "On the Statistics section: Covenant Rank, Score, Wins, and Win Streak buttons change the leaderboard sort. Clan buttons filter by clan. " +
                   "Escape: Close the logbook.";
        }
    }
}
