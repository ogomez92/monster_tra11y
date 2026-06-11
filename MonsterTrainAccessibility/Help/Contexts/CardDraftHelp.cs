namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for the card draft/reward screen
    /// </summary>
    public class CardDraftHelp : IHelpContext
    {
        public string ContextId => "card_draft";
        public string ContextName => "Card Draft";
        public int Priority => 80;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.CardDraft;
        }

        public string GetHelpText()
        {
            return "Left and Right arrows: Browse available cards. " +
                   "Enter: Select and add card to deck. " +
                   "C: Read the focused card's full details. " +
                   "Ctrl plus Up: Step through the card's details in the Card buffer, one piece at a time. " +
                   "Ctrl plus Down: Move back toward the top of the buffer. " +
                   "Ctrl plus Left/Right: Switch review buffers. " +
                   "T: Read all available cards. " +
                   "Escape: Skip reward (if allowed).";
        }
    }
}
