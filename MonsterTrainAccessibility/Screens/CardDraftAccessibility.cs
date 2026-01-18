using MonsterTrainAccessibility.Core;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Handles accessibility for card draft/selection screens.
    /// UI navigation is handled by MenuAccessibility via EventSystem.
    /// This class handles screen-specific announcements.
    /// </summary>
    public class CardDraftAccessibility
    {
        /// <summary>
        /// Called when a card draft screen is entered
        /// </summary>
        public void OnDraftScreenEntered(string draftType, int cardCount)
        {
            MonsterTrainAccessibility.LogInfo($"Card draft entered: {draftType}");

            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen($"{draftType}");
            MonsterTrainAccessibility.ScreenReader?.Queue($"Choose 1 of {cardCount} cards. Use arrows to browse, Enter to select.");
        }

        /// <summary>
        /// Called when an upgrade selection screen is entered
        /// </summary>
        public void OnUpgradeScreenEntered(string cardName, int upgradeCount)
        {
            MonsterTrainAccessibility.LogInfo($"Upgrade selection for {cardName}");

            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen($"Upgrade {cardName}");
            MonsterTrainAccessibility.ScreenReader?.Queue($"Choose from {upgradeCount} upgrades.");
        }

        /// <summary>
        /// Called when a card removal screen is entered
        /// </summary>
        public void OnCardRemovalEntered(int deckSize)
        {
            MonsterTrainAccessibility.LogInfo("Card removal entered");

            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Remove a Card");
            MonsterTrainAccessibility.ScreenReader?.Queue($"Select a card to remove. {deckSize} cards in deck.");
        }

        /// <summary>
        /// Called when a card duplication screen is entered
        /// </summary>
        public void OnCardDuplicationEntered()
        {
            MonsterTrainAccessibility.LogInfo("Card duplication entered");

            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Duplicate a Card");
            MonsterTrainAccessibility.ScreenReader?.Queue("Select a card to add a copy to your deck.");
        }
    }

    /// <summary>
    /// Information about a card upgrade
    /// </summary>
    public class UpgradeInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public object GameUpgradeState { get; set; }
    }
}
