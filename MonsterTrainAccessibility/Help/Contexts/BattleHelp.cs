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
            return "Battle screen. Play cards to defeat enemies and protect your Pyre. " +
                   "H: Read hand (all cards with costs and effects). " +
                   "L: Read floors (floor capacity and units). " +
                   "U: Read all units (your monsters front-to-back, then enemies). " +
                   "R: Read resources (ember, pyre health, deck/discard counts). " +
                   "Enter: Play selected card. Some cards require floor or unit selection. " +
                   "Left/Right arrows: Select targets when playing spells. " +
                   "E: End your turn and start combat phase. " +
                   "C: Re-read current selection. " +
                   "V: Cycle verbosity level. " +
                   "Enemies ascend each turn. Protect your Pyre on top floor!";
        }
    }
}
