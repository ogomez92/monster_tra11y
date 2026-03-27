using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;
using System.Reflection;
using System.Text;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Detect card draft screen
    /// </summary>
    public static class CardDraftScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CardDraftScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Setup");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(CardDraftScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched CardDraftScreen.Setup");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CardDraftScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.CardDraft);
                MonsterTrainAccessibility.LogInfo("Card draft screen detected");
                AutoReadCardDraft(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CardDraftScreen patch: {ex.Message}");
            }
        }

        private static void AutoReadCardDraft(object screen)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("Card Draft. ");

                var screenType = screen.GetType();
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Try to get draft card items or card data list
                System.Collections.IList cards = null;

                // Look for draftItems, cardChoices, etc.
                string[] fieldNames = { "draftItems", "cardChoiceItems", "choices", "cardChoices", "_draftItems", "cards", "_cards" };
                foreach (var fieldName in fieldNames)
                {
                    var field = screenType.GetField(fieldName, bindingFlags);
                    if (field != null)
                    {
                        cards = field.GetValue(screen) as System.Collections.IList;
                        if (cards != null && cards.Count > 0) break;
                    }
                }

                if (cards != null && cards.Count > 0)
                {
                    sb.Append($"{cards.Count} cards: ");
                    foreach (var card in cards)
                    {
                        if (card == null) continue;
                        string name = GetCardName(card);
                        if (!string.IsNullOrEmpty(name))
                        {
                            sb.Append($"{name}, ");
                        }
                    }
                }

                sb.Append("Left and Right to browse. Enter to select. Press F1 for help.");
                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error auto-reading card draft: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Card Draft. Left and Right to browse. Enter to select. Press F1 for help.");
            }
        }

        /// <summary>
        /// Get card name from a CardData, CardState, or CardChoiceItem
        /// </summary>
        private static string GetCardName(object card)
        {
            if (card == null) return null;
            try
            {
                var cardType = card.GetType();

                // Try GetName first
                var getNameMethod = cardType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    string name = getNameMethod.Invoke(card, null) as string;
                    if (!string.IsNullOrEmpty(name)) return Screens.BattleAccessibility.StripRichTextTags(name);
                }

                // Try GetTitle
                var getTitleMethod = cardType.GetMethod("GetTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    string title = getTitleMethod.Invoke(card, null) as string;
                    if (!string.IsNullOrEmpty(title)) return Screens.BattleAccessibility.StripRichTextTags(title);
                }

                // If it's a CardChoiceItem, get the CardData from it
                var cardDataField = cardType.GetField("cardData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                                    cardType.GetField("_cardData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (cardDataField != null)
                {
                    var cardData = cardDataField.GetValue(card);
                    if (cardData != null)
                    {
                        return GetCardName(cardData);
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
