namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for battle/combat (when not targeting)
    /// </summary>
    public class BattleHelp : IHelpContext
    {
        public string ContextId => "battle";
        public string ContextName => "Battle";
        public int Priority => 90;

        public bool IsActive()
        {
            // Active during battle, but not when floor targeting is active
            if (ScreenStateTracker.CurrentScreen != GameScreen.Battle)
                return false;

            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle == null || !battle.IsInBattle)
                return false;

            // Check if floor targeting is NOT active
            var targeting = MonsterTrainAccessibility.FloorTargeting;
            return targeting == null || !targeting.IsTargeting;
        }

        public string GetHelpText()
        {
            return "H: Read hand (cards available). " +
                   "F: Read floors (units on each floor). " +
                   "E: Read enemies (enemy positions and health). " +
                   "R: Read resources (ember, pyre health, cards). " +
                   "1 through 9: Select card by position in hand. " +
                   "Enter: Play selected card (may require floor selection). " +
                   "End Turn button or Tab: End your turn. " +
                   "C: Re-read current selection. " +
                   "V: Cycle verbosity level.";
        }
    }
}
