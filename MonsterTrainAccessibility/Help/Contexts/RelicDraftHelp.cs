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
                   "C: Read the focused artifact's full details. " +
                   "Ctrl plus Up: Step through the artifact's description and keywords in the Artifact buffer. " +
                   "Ctrl plus Down: Move back toward the top of the buffer. " +
                   "Ctrl plus Left/Right: Switch review buffers. " +
                   "T: Read all available artifacts. " +
                   "Escape: Skip reward (if allowed).";
        }
    }
}
