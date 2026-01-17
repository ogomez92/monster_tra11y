using MonsterTrainAccessibility.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Handles accessibility for the map/path selection screen.
    /// Players choose their route through different node types.
    /// </summary>
    public class MapAccessibility
    {
        private ListFocusContext _mapContext;

        /// <summary>
        /// Called when the map screen is entered
        /// </summary>
        public void OnMapScreenEntered(List<MapNodeInfo> availableNodes, int currentRing)
        {
            MonsterTrainAccessibility.LogInfo($"Map screen entered, ring {currentRing}");

            _mapContext = new ListFocusContext("Map", OnMapBack);

            foreach (var node in availableNodes)
            {
                _mapContext.AddItem(new FocusableMapNode
                {
                    Id = node.Id,
                    NodeType = node.Type,
                    Rewards = node.Rewards,
                    Description = node.Description,
                    MapNode = node.GameMapNode,
                    OnSelect = () => SelectNode(node)
                });
            }

            MonsterTrainAccessibility.FocusManager.SetContext(_mapContext);

            // Announce map info
            string ringInfo = currentRing > 0 ? $"Ring {currentRing}. " : "";
            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen($"Map. {ringInfo}Choose your path.");

            if (availableNodes.Count > 1)
            {
                MonsterTrainAccessibility.ScreenReader?.Queue(
                    $"{availableNodes.Count} paths available. Use arrows to browse, Enter to select.");
            }
            else if (availableNodes.Count == 1)
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("One path available. Press Enter to continue.");
            }
        }

        /// <summary>
        /// Called when entering a shop
        /// </summary>
        public void OnShopEntered(ShopInfo shop)
        {
            MonsterTrainAccessibility.LogInfo("Shop entered");

            var context = new ListFocusContext("Merchant Shop", OnShopBack);

            // Add buyable items
            foreach (var item in shop.Items)
            {
                bool canAfford = shop.PlayerGold >= item.Cost;
                string affordText = canAfford ? "" : " (cannot afford)";

                context.AddItem(new FocusableMenuItem
                {
                    Id = item.Id,
                    Label = $"{item.Name}, {item.Cost} gold{affordText}",
                    Description = item.Description,
                    OnActivate = canAfford ? (Action)(() => BuyItem(item, shop)) : null
                });
            }

            // Add leave option
            context.AddItem(new FocusableMenuItem
            {
                Id = "leave",
                Label = "Leave Shop",
                OnActivate = () => LeaveShop()
            });

            MonsterTrainAccessibility.FocusManager.SetContext(context);

            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Merchant Shop");
            MonsterTrainAccessibility.ScreenReader?.Queue($"You have {shop.PlayerGold} gold. {shop.Items.Count} items for sale.");
        }

        /// <summary>
        /// Called when entering an event
        /// </summary>
        public void OnEventEntered(EventInfo eventInfo)
        {
            MonsterTrainAccessibility.LogInfo($"Event entered: {eventInfo.Name}");

            var context = new ListFocusContext("Event", OnEventBack);

            // Add event choices
            foreach (var choice in eventInfo.Choices)
            {
                context.AddItem(new FocusableMenuItem
                {
                    Id = choice.Id,
                    Label = choice.Name,
                    Description = choice.Description,
                    OnActivate = () => SelectEventChoice(choice)
                });
            }

            MonsterTrainAccessibility.FocusManager.SetContext(context);

            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen(eventInfo.Name);
            MonsterTrainAccessibility.ScreenReader?.Queue(eventInfo.Description);
            MonsterTrainAccessibility.ScreenReader?.Queue($"{eventInfo.Choices.Count} choices available.");
        }

        /// <summary>
        /// Called when reaching the boss node
        /// </summary>
        public void OnBossNodeEntered(string bossName, string bossDescription)
        {
            MonsterTrainAccessibility.LogInfo($"Boss node: {bossName}");

            var context = new ListFocusContext("Boss Battle", OnBossBack);

            context.AddItem(new FocusableMenuItem
            {
                Id = "fight",
                Label = "Fight Boss",
                Description = $"Face {bossName}",
                OnActivate = () => StartBossBattle()
            });

            MonsterTrainAccessibility.FocusManager.SetContext(context);

            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen($"Boss: {bossName}");
            MonsterTrainAccessibility.ScreenReader?.Queue(bossDescription);
            MonsterTrainAccessibility.ScreenReader?.Queue("Press Enter to begin the boss battle.");
        }

        #region Selection Actions

        private void SelectNode(MapNodeInfo node)
        {
            MonsterTrainAccessibility.LogInfo($"Selected map node: {node.Type}");
            MonsterTrainAccessibility.ScreenReader?.Speak($"Selected {node.Type}", true);

            // Actual node selection would be handled by game API
        }

        private void BuyItem(ShopItemInfo item, ShopInfo shop)
        {
            MonsterTrainAccessibility.LogInfo($"Buying: {item.Name}");
            MonsterTrainAccessibility.ScreenReader?.Speak($"Purchased {item.Name} for {item.Cost} gold", true);

            // Update gold display
            shop.PlayerGold -= item.Cost;

            // Actual purchase would be handled by game API
        }

        private void LeaveShop()
        {
            MonsterTrainAccessibility.ScreenReader?.Speak("Leaving shop", true);
            MonsterTrainAccessibility.FocusManager.GoBack();
        }

        private void SelectEventChoice(EventChoiceInfo choice)
        {
            MonsterTrainAccessibility.LogInfo($"Selected event choice: {choice.Name}");
            MonsterTrainAccessibility.ScreenReader?.Speak($"Selected: {choice.Name}", true);

            // Actual choice selection would be handled by game API
        }

        private void StartBossBattle()
        {
            MonsterTrainAccessibility.ScreenReader?.Speak("Starting boss battle", true);
            // Actual battle start would be handled by game API
        }

        #endregion

        #region Back Handlers

        private void OnMapBack()
        {
            // At map, might open pause menu or similar
            MonsterTrainAccessibility.ScreenReader?.Speak("Press Escape to open menu", true);
        }

        private void OnShopBack()
        {
            LeaveShop();
        }

        private void OnEventBack()
        {
            MonsterTrainAccessibility.ScreenReader?.Speak("You must choose an option", true);
        }

        private void OnBossBack()
        {
            MonsterTrainAccessibility.ScreenReader?.Speak("You must face the boss", true);
        }

        #endregion
    }

    #region Data Classes

    /// <summary>
    /// Information about a map node
    /// </summary>
    public class MapNodeInfo
    {
        public string Id { get; set; }
        public string Type { get; set; } // Battle, Shop, Event, Boss, etc.
        public string Rewards { get; set; }
        public string Description { get; set; }
        public object GameMapNode { get; set; }
    }

    /// <summary>
    /// Information about a shop
    /// </summary>
    public class ShopInfo
    {
        public int PlayerGold { get; set; }
        public List<ShopItemInfo> Items { get; set; } = new List<ShopItemInfo>();
    }

    /// <summary>
    /// Information about a shop item
    /// </summary>
    public class ShopItemInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Cost { get; set; }
        public string ItemType { get; set; } // Card, Artifact, Upgrade, etc.
    }

    /// <summary>
    /// Information about an event
    /// </summary>
    public class EventInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<EventChoiceInfo> Choices { get; set; } = new List<EventChoiceInfo>();
    }

    /// <summary>
    /// Information about an event choice
    /// </summary>
    public class EventChoiceInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    #endregion
}
