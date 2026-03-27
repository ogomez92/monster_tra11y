using HarmonyLib;
using MonsterTrainAccessibility.Core;
using MonsterTrainAccessibility.Help;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Patch for merchant/shop screen
    /// </summary>
    public static class MerchantScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("MerchantScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Open") ??
                                 AccessTools.Method(targetType, "Show");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(MerchantScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched MerchantScreen.{method.Name}");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("MerchantScreen methods not found");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch MerchantScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Shop);
                AutoReadShop(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in MerchantScreen patch: {ex.Message}");
            }
        }

        private static void AutoReadShop(object screen)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("Shop. ");

                // Announce gold
                int gold = InputInterceptor.GetCurrentGold();
                if (gold >= 0)
                {
                    sb.Append($"You have {gold} gold. ");
                }

                // Try to count shop items by category
                if (screen != null)
                {
                    var screenType = screen.GetType();
                    var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                    // Try to get merchant goods list
                    var goodsField = screenType.GetField("merchantGoods", bindingFlags) ??
                                     screenType.GetField("_merchantGoods", bindingFlags) ??
                                     screenType.GetField("goods", bindingFlags);

                    if (goodsField != null)
                    {
                        var goods = goodsField.GetValue(screen) as System.Collections.IList;
                        if (goods != null && goods.Count > 0)
                        {
                            int cards = 0, relics = 0, upgrades = 0, other = 0;
                            foreach (var good in goods)
                            {
                                if (good == null) continue;
                                string typeName = good.GetType().Name.ToLower();
                                if (typeName.Contains("card")) cards++;
                                else if (typeName.Contains("relic")) relics++;
                                else if (typeName.Contains("enhancer") || typeName.Contains("upgrade")) upgrades++;
                                else other++;
                            }

                            var itemParts = new List<string>();
                            if (cards > 0) itemParts.Add($"{cards} cards");
                            if (relics > 0) itemParts.Add($"{relics} artifacts");
                            if (upgrades > 0) itemParts.Add($"{upgrades} upgrades");
                            if (other > 0) itemParts.Add($"{other} other items");

                            if (itemParts.Count > 0)
                            {
                                sb.Append(string.Join(", ", itemParts));
                                sb.Append(" available. ");
                            }
                        }
                    }
                }

                sb.Append("Press F1 for help.");
                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error auto-reading shop: {ex.Message}");
                int gold = InputInterceptor.GetCurrentGold();
                string goldText = gold >= 0 ? $"You have {gold} gold." : "";
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen($"Shop. {goldText} Press F1 for help.");
            }
        }
    }

    /// <summary>
    /// Patch for enhancer/upgrade card selection screen
    /// </summary>
    public static class EnhancerSelectionScreenPatch
    {
        private static string _lastEnhancerName = null;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try different possible class names for the upgrade card selection screen
                var targetNames = new[] {
                    "UpgradeSelectionScreen",
                    "EnhancerSelectionScreen",
                    "CardUpgradeSelectionScreen",
                    "UpgradeScreen",
                    "EnhancerScreen"
                };

                foreach (var name in targetNames)
                {
                    var targetType = AccessTools.TypeByName(name);
                    if (targetType != null)
                    {
                        var method = AccessTools.Method(targetType, "Initialize") ??
                                     AccessTools.Method(targetType, "Setup") ??
                                     AccessTools.Method(targetType, "Show") ??
                                     AccessTools.Method(targetType, "Open");

                        if (method != null)
                        {
                            var postfix = new HarmonyMethod(typeof(EnhancerSelectionScreenPatch).GetMethod(nameof(Postfix)));
                            harmony.Patch(method, postfix: postfix);
                            MonsterTrainAccessibility.LogInfo($"Patched {name}.{method.Name}");
                            return;
                        }
                    }
                }

                MonsterTrainAccessibility.LogInfo("EnhancerSelectionScreen not found - will use alternative detection");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch EnhancerSelectionScreen: {ex.Message}");
            }
        }

        public static void SetEnhancerName(string name)
        {
            _lastEnhancerName = name;
        }

        public static void Postfix(object __instance)
        {
            try
            {
                // Try to get the card count from the screen
                int cardCount = 0;
                var instanceType = __instance.GetType();

                // Look for cards list or count
                var getCardsMethod = instanceType.GetMethod("GetCards", Type.EmptyTypes) ??
                                     instanceType.GetMethod("GetCardList", Type.EmptyTypes);
                if (getCardsMethod != null)
                {
                    var cards = getCardsMethod.Invoke(__instance, null) as System.Collections.IList;
                    if (cards != null)
                        cardCount = cards.Count;
                }

                // Also try cards field
                if (cardCount == 0)
                {
                    var fields = instanceType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var field in fields)
                    {
                        if (field.Name.ToLower().Contains("card"))
                        {
                            var value = field.GetValue(__instance);
                            if (value is System.Collections.IList list)
                            {
                                cardCount = list.Count;
                                break;
                            }
                        }
                    }
                }

                MonsterTrainAccessibility.DraftHandler?.OnEnhancerCardSelectionEntered(_lastEnhancerName, cardCount);
                _lastEnhancerName = null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in EnhancerSelectionScreen patch: {ex.Message}");
            }
        }
    }
}
