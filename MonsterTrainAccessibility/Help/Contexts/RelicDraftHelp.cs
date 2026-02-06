namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for the artifact/relic draft screen
    /// </summary>
    public class RelicDraftHelp : IHelpContext
    {
        public string ContextId => "relic_draft";
        public string ContextName => "Artifact Draft";
        public int Priority => 80;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.RelicDraft;
        }

        public string GetHelpText()
        {
            return "Left and Right arrows: Browse available artifacts. " +
                   "Enter: Select artifact and add to collection. " +
                   "C: Re-read current artifact details. " +
                   "T: Read all available artifacts. " +
                   "Escape: Skip reward (if allowed).";
        }
    }
}
