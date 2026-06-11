namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for the shop/merchant screen
    /// </summary>
    public class ShopHelp : IHelpContext
    {
        public string ContextId => "shop";
        public string ContextName => "Shop";
        public int Priority => 70;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.Shop;
        }

        public bool PassesAdditionalChecks()
        {
            return true;
        }

        public string GetHelpText()
        {
            return @"Shop Controls:
Arrow keys: Browse shop items
Enter: Purchase selected item
C: Read the focused item's full details and price
Ctrl plus Up: Step through the item's details in the review buffer
Ctrl plus Down: Move back toward the top of the buffer
Ctrl plus Left/Right: Switch review buffers
Ctrl plus G: Read current gold
T: Read all text on screen
Escape: Leave shop";
        }
    }
}
