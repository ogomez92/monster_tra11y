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
        Defeat,
        ChampionUpgrade,
        DeckView,
        RelicDraft,
        Synthesis,
        Dialog,
        RunHistory,
        ChallengeDetails,
        ChallengeOverview,
        Minimap,
        Credits,
        KeyMapping,
        RunSummary,
        Loading
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

                // Contextual buffer content belongs to the screen it was read on
                Core.Buffers.FocusBuffers.Clear();

                // The floor review belongs to the battle screen; any overlay
                // (pile view, dialog) takes the arrows back the moment it opens
                if (screen != GameScreen.Battle)
                    MonsterTrainAccessibility.FloorReview?.Close(announce: false);
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
