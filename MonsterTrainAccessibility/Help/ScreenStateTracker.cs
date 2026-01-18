namespace MonsterTrainAccessibility.Help
{
    /// <summary>
    /// Tracks the current game screen for context-sensitive help.
    /// Updated by screen transition patches.
    /// </summary>
    public enum GameScreen
    {
        Unknown,
        MainMenu,
        ClanSelection,
        Map,
        BattleIntro,
        Battle,
        CardDraft,
        Shop,
        Event,
        Rewards,
        Settings,
        Compendium,
        Victory,
        Defeat
    }

    /// <summary>
    /// Static tracker for current game screen state.
    /// </summary>
    public static class ScreenStateTracker
    {
        /// <summary>
        /// The current game screen
        /// </summary>
        public static GameScreen CurrentScreen { get; private set; } = GameScreen.Unknown;

        /// <summary>
        /// Previous screen (for back navigation tracking)
        /// </summary>
        public static GameScreen PreviousScreen { get; private set; } = GameScreen.Unknown;

        /// <summary>
        /// Set the current screen state
        /// </summary>
        public static void SetScreen(GameScreen screen)
        {
            if (screen != CurrentScreen)
            {
                PreviousScreen = CurrentScreen;
                CurrentScreen = screen;
                MonsterTrainAccessibility.LogInfo($"Screen changed: {PreviousScreen} -> {CurrentScreen}");
            }
        }

        /// <summary>
        /// Reset to unknown state (e.g., on game exit)
        /// </summary>
        public static void Reset()
        {
            PreviousScreen = GameScreen.Unknown;
            CurrentScreen = GameScreen.Unknown;
        }
    }
}
