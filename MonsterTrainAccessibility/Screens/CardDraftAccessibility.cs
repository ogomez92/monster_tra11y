using MonsterTrainAccessibility.Core;
using System.Collections.Generic;
using System.Linq;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Handles accessibility for card draft/selection screens.
    /// Used for card rewards, upgrade selections, and similar card choice UI.
    /// </summary>
    public class CardDraftAccessibility
    {
        private GridFocusContext _draftContext;
        private string _draftType;
        private bool _canSkip;

        /// <summary>
        /// Called when a card draft screen is entered
        /// </summary>
        public void OnDraftScreenEntered(string draftType, List<CardInfo> cards, bool canSkip = true)
        {
            _draftType = draftType;
            _canSkip = canSkip;

            MonsterTrainAccessibility.LogInfo($"Card draft entered: {draftType}");

            _draftContext = new GridFocusContext($"{draftType} Draft", cards.Count, OnDraftBack);

            var items = cards.Select((card, i) => new FocusableCard
            {
                Id = card.Id,
                Index = i,
                CardName = card.Name,
                Cost = card.Cost,
                CardType = card.Type,
                BodyText = card.BodyText,
                IsPlayable = true, // All draft cards are selectable
                CardState = card.GameCardState,
                OnPlay = () => SelectCard(card)
            }).Cast<FocusableItem>().ToList();

            _draftContext.SetItems(items);

            MonsterTrainAccessibility.FocusManager.SetContext(_draftContext);

            // Announce draft
            string skipInfo = canSkip ? " Press Escape to skip." : "";
            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen($"{draftType}. Choose 1 of {cards.Count} cards.");
            MonsterTrainAccessibility.ScreenReader?.Queue($"Use Left and Right arrows to browse, Enter to select.{skipInfo}");
        }

        /// <summary>
        /// Called when an upgrade selection screen is entered
        /// </summary>
        public void OnUpgradeScreenEntered(CardInfo cardToUpgrade, List<UpgradeInfo> upgrades)
        {
            MonsterTrainAccessibility.LogInfo($"Upgrade selection for {cardToUpgrade.Name}");

            var context = new ListFocusContext($"Upgrade {cardToUpgrade.Name}", OnUpgradeBack);

            foreach (var upgrade in upgrades)
            {
                context.AddItem(new FocusableMenuItem
                {
                    Id = upgrade.Id,
                    Label = upgrade.Name,
                    Description = upgrade.Description,
                    OnActivate = () => SelectUpgrade(cardToUpgrade, upgrade)
                });
            }

            MonsterTrainAccessibility.FocusManager.SetContext(context);

            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen($"Select upgrade for {cardToUpgrade.Name}");
            MonsterTrainAccessibility.ScreenReader?.Queue($"Choose from {upgrades.Count} upgrades. Use arrows to browse, Enter to select.");
        }

        /// <summary>
        /// Called when a card removal screen is entered
        /// </summary>
        public void OnCardRemovalEntered(List<CardInfo> deckCards)
        {
            MonsterTrainAccessibility.LogInfo("Card removal entered");

            var context = new GridFocusContext("Remove a Card", 5, OnRemovalBack);

            var items = deckCards.Select((card, i) => new FocusableCard
            {
                Id = card.Id,
                Index = i,
                CardName = card.Name,
                Cost = card.Cost,
                CardType = card.Type,
                BodyText = card.BodyText,
                IsPlayable = true,
                CardState = card.GameCardState,
                OnPlay = () => RemoveCard(card)
            }).Cast<FocusableItem>().ToList();

            context.SetItems(items);

            MonsterTrainAccessibility.FocusManager.SetContext(context);

            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Remove a Card");
            MonsterTrainAccessibility.ScreenReader?.Queue($"Select a card to remove from your deck. {deckCards.Count} cards in deck.");
        }

        /// <summary>
        /// Called when a card duplication screen is entered
        /// </summary>
        public void OnCardDuplicationEntered(List<CardInfo> deckCards)
        {
            MonsterTrainAccessibility.LogInfo("Card duplication entered");

            var context = new GridFocusContext("Duplicate a Card", 5, OnDuplicationBack);

            var items = deckCards.Select((card, i) => new FocusableCard
            {
                Id = card.Id,
                Index = i,
                CardName = card.Name,
                Cost = card.Cost,
                CardType = card.Type,
                BodyText = card.BodyText,
                IsPlayable = true,
                CardState = card.GameCardState,
                OnPlay = () => DuplicateCard(card)
            }).Cast<FocusableItem>().ToList();

            context.SetItems(items);

            MonsterTrainAccessibility.FocusManager.SetContext(context);

            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Duplicate a Card");
            MonsterTrainAccessibility.ScreenReader?.Queue("Select a card to add a copy to your deck.");
        }

        #region Selection Actions

        private void SelectCard(CardInfo card)
        {
            MonsterTrainAccessibility.LogInfo($"Selected card: {card.Name}");
            MonsterTrainAccessibility.ScreenReader?.Speak($"Selected {card.Name}", true);

            // Actual selection would be handled by game API
            // The game will typically close the draft screen automatically
        }

        private void SelectUpgrade(CardInfo card, UpgradeInfo upgrade)
        {
            MonsterTrainAccessibility.LogInfo($"Selected upgrade {upgrade.Name} for {card.Name}");
            MonsterTrainAccessibility.ScreenReader?.Speak($"Applied {upgrade.Name} to {card.Name}", true);

            // Actual upgrade would be handled by game API
        }

        private void RemoveCard(CardInfo card)
        {
            MonsterTrainAccessibility.LogInfo($"Removed card: {card.Name}");
            MonsterTrainAccessibility.ScreenReader?.Speak($"Removed {card.Name} from deck", true);

            // Actual removal would be handled by game API
        }

        private void DuplicateCard(CardInfo card)
        {
            MonsterTrainAccessibility.LogInfo($"Duplicated card: {card.Name}");
            MonsterTrainAccessibility.ScreenReader?.Speak($"Added copy of {card.Name} to deck", true);

            // Actual duplication would be handled by game API
        }

        #endregion

        #region Back Handlers

        private void OnDraftBack()
        {
            if (_canSkip)
            {
                MonsterTrainAccessibility.ScreenReader?.Speak($"Skipping {_draftType}", true);
                // Actual skip would be handled by game API
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Speak("You must select a card", true);
            }
        }

        private void OnUpgradeBack()
        {
            MonsterTrainAccessibility.ScreenReader?.Speak("Canceling upgrade", true);
            MonsterTrainAccessibility.FocusManager.GoBack();
        }

        private void OnRemovalBack()
        {
            MonsterTrainAccessibility.ScreenReader?.Speak("Canceling card removal", true);
            MonsterTrainAccessibility.FocusManager.GoBack();
        }

        private void OnDuplicationBack()
        {
            MonsterTrainAccessibility.ScreenReader?.Speak("Canceling duplication", true);
            MonsterTrainAccessibility.FocusManager.GoBack();
        }

        #endregion
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
