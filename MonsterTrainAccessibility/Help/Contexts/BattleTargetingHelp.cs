namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for floor targeting during battle.
    /// Active when a card requires selecting a floor to play on.
    /// </summary>
    public class BattleTargetingHelp : IHelpContext
    {
        public string ContextId => "battle_targeting";
        public string ContextName => "Floor Targeting";
        public int Priority => 100; // Highest - takes precedence over battle

        public bool IsActive()
        {
            var targeting = MonsterTrainAccessibility.FloorTargeting;
            return targeting != null && targeting.IsTargeting;
        }

        public string GetHelpText()
        {
            return "Select a floor to play the card. " +
                   "1: Select floor 1 (bottom). " +
                   "2: Select floor 2 (middle). " +
                   "3: Select floor 3 (top). " +
                   "Up and Down arrows: Cycle between floors. " +
                   "Enter: Confirm and play card on selected floor. " +
                   "Escape: Cancel card play.";
        }
    }
}
