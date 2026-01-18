namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for unit targeting during battle (when targeting a spell at a unit).
    /// Highest priority (101) to override all other contexts.
    /// </summary>
    public class UnitTargetingHelp : IHelpContext
    {
        public string ContextId => "unit_targeting";
        public string ContextName => "Unit Targeting";
        public int Priority => 101; // Higher than floor targeting

        public bool IsActive()
        {
            var targeting = MonsterTrainAccessibility.UnitTargeting;
            return targeting != null && targeting.IsTargeting;
        }

        public bool PassesAdditionalChecks()
        {
            return true;
        }

        public string GetHelpText()
        {
            return @"Unit Targeting Mode:
Arrow keys or Left/Right: Cycle through targets
Number keys 1-5: Select target directly
Enter: Confirm target
Escape: Cancel spell";
        }
    }
}
