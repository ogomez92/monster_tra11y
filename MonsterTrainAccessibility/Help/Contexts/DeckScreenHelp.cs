namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for the deck/card list screen (deck view, purge, upgrade, draw pile, discard pile)
    /// </summary>
    public class DeckScreenHelp : IHelpContext
    {
        public string ContextId => "deck_screen";
        public string ContextName => "Deck";
        public int Priority => 75;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.DeckView;
        }

        public string GetHelpText()
        {
            return "Arrow keys: Browse cards. " +
                   "Enter: Select card (for purge or upgrade). " +
                   "C: Re-read current card details. " +
                   "T: Read all cards. " +
                   "Escape: Close deck view or cancel.";
        }
    }
}
