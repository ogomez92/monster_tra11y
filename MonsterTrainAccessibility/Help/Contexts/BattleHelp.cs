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
                   "K: Read the selected floor. Shift plus K or L: Read all floors. " +
                   "U: Read all units (your monsters front-to-back, then enemies). " +
                   "Up arrow: Open floor review, a cursor to walk the floors and their units with the arrow keys. " +
                   "R: Read ember. " +
                   "Ctrl plus G: Read gold. " +
                   "Ctrl plus H: Read pyre health. " +
                   "Ctrl plus R: Read pact shards. " +
                   "Enter: Play selected card. Some cards require floor or unit selection. " +
                   "Left/Right arrows: Select targets when playing spells. " +
                   "F: End your turn and start combat phase. " +
                   "C: Re-read current selection; for cards this reads the full details. " +
                   "V: Cycle verbosity level. " +
                   "Ctrl plus Left/Right: Switch review buffers: UI, Events, Card, Creature, Artifact, Reward, Hand, Floors, Units, Resources. " +
                   "Ctrl plus Up: Read the next item in the current buffer, " +
                   "for example each combat event or each card in hand. " +
                   "Ctrl plus Down: Move back toward the top of the buffer. " +
                   "Enemies ascend each turn. Protect your Pyre on top floor!";
        }
    }
}
