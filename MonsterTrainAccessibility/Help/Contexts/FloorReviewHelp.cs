namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context while the battle floor review cursor is open
    /// </summary>
    public class FloorReviewHelp : IHelpContext
    {
        public string ContextId => "floor_review";
        public string ContextName => "Floor review";
        public int Priority => 96; // Above battle (90); targeting closes the review

        public bool IsActive()
        {
            var review = MonsterTrainAccessibility.FloorReview;
            return review != null && review.IsActive;
        }

        public string GetHelpText()
        {
            return "Floor review: a virtual cursor over the train's floors. The real game selection does not move. " +
                   "Up and Down arrows: Move between floors, from bottom floor up to the pyre room. " +
                   "Right arrow: Step through the units on the floor in their positions, " +
                   "your units back to front, then enemies front to back. " +
                   "Left arrow: Step back; left of the first unit returns to the floor overview. " +
                   "Enter: Read the focused floor or unit with full details. " +
                   "Ctrl plus Up/Down: Review the focused item's details in the UI buffer, " +
                   "including keyword explanations; units also fill the Creature buffer. " +
                   "Escape: Close floor review.";
        }
    }
}
