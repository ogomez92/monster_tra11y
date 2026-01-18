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

        public string GetHelpText()
        {
            return "Arrow keys: Browse shop items. " +
                   "Enter: Purchase selected item. " +
                   "C: Re-read current item and price. " +
                   "T: Read all shop items. " +
                   "R: Read current gold. " +
                   "Escape: Leave shop.";
        }
    }
}
