using MonsterTrainAccessibility.Core;
using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Reads hand/card information from the battle state.
    /// </summary>
    internal static class HandReader
    {
        /// <summary>
        /// Announce all cards in hand
        /// </summary>
        internal static void AnnounceHand(BattleManagerCache cache)
        {
            try
            {
                var hand = GetHandCards(cache);
                if (hand == null || hand.Count == 0)
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak("Hand is empty", false);
                    return;
                }

                var sb = new StringBuilder();
                sb.Append($"Hand contains {hand.Count} cards. ");

                int currentEnergy = ResourceReader.GetCurrentEnergy(cache);
                var verbosity = MonsterTrainAccessibility.AccessibilitySettings.VerbosityLevel.Value;

                for (int i = 0; i < hand.Count; i++)
                {
                    var card = hand[i];
                    string name = GetCardTitle(cache, card);
                    int cost = GetCardCost(cache, card);
                    string cardType = GetCardType(card);
                    string clanName = GetCardClan(card);
                    string description = GetCardDescription(cache, card);
                    bool upgraded = HasCardUpgrades(card);

                    string playable = (currentEnergy >= 0 && cost > currentEnergy) ? ", unplayable" : "";
                    string upgradePrefix = upgraded ? "Upgraded " : "";

                    // Build card announcement based on verbosity
                    if (verbosity == VerbosityLevel.Minimal)
                    {
                        sb.Append($"{i + 1}: {upgradePrefix}{name}, {cost} ember{playable}. ");
                    }
                    else
                    {
                        // Normal and Verbose include type, clan, and description
                        string typeStr = !string.IsNullOrEmpty(cardType) ? $" ({cardType})" : "";
                        string clanStr = !string.IsNullOrEmpty(clanName) ? $", {clanName}" : "";
                        sb.Append($"{i + 1}: {upgradePrefix}{name}{typeStr}{clanStr}, {cost} ember{playable}. ");

                        if (!string.IsNullOrEmpty(description))
                        {
                            sb.Append($"{description} ");
                        }

                        // At Verbose level, include keyword tooltips
                        if (verbosity == VerbosityLevel.Verbose)
                        {
                            string keywords = GetCardKeywords(description);
                            if (!string.IsNullOrEmpty(keywords))
                            {
                                sb.Append($"Keywords: {keywords} ");
                            }
                        }
                    }
                }

                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error announcing hand: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read hand", false);
            }
        }

        internal static List<object> GetHandCards(BattleManagerCache cache)
        {
            if (cache.CardManager == null || cache.GetHandMethod == null)
            {
                cache.FindManagers();
                if (cache.CardManager == null) return null;
            }

            try
            {
                var result = cache.GetHandMethod.Invoke(cache.CardManager, new object[] { false });
                if (result is System.Collections.IList list)
                {
                    var cards = new List<object>();
                    foreach (var card in list)
                    {
                        cards.Add(card);
                    }
                    return cards;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting hand: {ex.Message}");
            }
            return null;
        }

        internal static string GetCardTitle(BattleManagerCache cache, object cardState)
        {
            try
            {
                if (cache.GetTitleMethod == null)
                {
                    var type = cardState.GetType();
                    cache.GetTitleMethod = type.GetMethod("GetTitle");
                }
                var title = cache.GetTitleMethod?.Invoke(cardState, null) as string ?? "Unknown Card";
                return TextUtilities.StripRichTextTags(title);
            }
            catch
            {
                return "Unknown Card";
            }
        }

        internal static int GetCardCost(BattleManagerCache cache, object cardState)
        {
            try
            {
                if (cache.GetCostMethod == null)
                {
                    var type = cardState.GetType();
                    cache.GetCostMethod = type.GetMethod("GetCostWithoutTraits")
                                  ?? type.GetMethod("GetCostWithoutAnyModifications");
                }
                var result = cache.GetCostMethod?.Invoke(cardState, null);
                if (result is int cost) return cost;
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Check if a card has any upgrades applied
        /// </summary>
        private static bool HasCardUpgrades(object cardState)
        {
            try
            {
                if (cardState == null) return false;
                var type = cardState.GetType();
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // CardState stores upgrades in cardModifiers.GetCardUpgrades()
                var modifiersField = type.GetField("cardModifiers", bindingFlags);
                if (modifiersField != null)
                {
                    var modifiers = modifiersField.GetValue(cardState);
                    if (modifiers != null)
                    {
                        var getCardUpgradesMethod = modifiers.GetType().GetMethod("GetCardUpgrades", Type.EmptyTypes);
                        if (getCardUpgradesMethod != null)
                        {
                            var upgrades = getCardUpgradesMethod.Invoke(modifiers, null) as System.Collections.IList;
                            if (upgrades != null && upgrades.Count > 0) return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private static string GetCardDescription(BattleManagerCache cache, object cardState)
        {
            try
            {
                var type = cardState.GetType();
                string result = null;

                // Try GetDescription first
                var getDescMethod = type.GetMethod("GetDescription");
                if (getDescMethod != null)
                {
                    var desc = getDescMethod.Invoke(cardState, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                        result = desc;
                }

                // Try GetEffectDescription
                if (result == null)
                {
                    var getEffectDescMethod = type.GetMethod("GetEffectDescription");
                    if (getEffectDescMethod != null)
                    {
                        var desc = getEffectDescMethod.Invoke(cardState, null) as string;
                        if (!string.IsNullOrEmpty(desc))
                            result = desc;
                    }
                }

                // Try getting from CardData
                if (result == null)
                {
                    var getCardDataMethod = type.GetMethod("GetCardDataID") ?? type.GetMethod("GetCardData");
                    if (getCardDataMethod != null)
                    {
                        var cardData = getCardDataMethod.Invoke(cardState, null);
                        if (cardData != null)
                        {
                            var dataType = cardData.GetType();
                            var dataDescMethod = dataType.GetMethod("GetDescription");
                            if (dataDescMethod != null)
                            {
                                var desc = dataDescMethod.Invoke(cardData, null) as string;
                                if (!string.IsNullOrEmpty(desc))
                                    result = desc;
                            }
                        }
                    }
                }

                // Strip rich text tags before returning
                return TextUtilities.StripRichTextTags(result);
            }
            catch { }
            return null;
        }

        private static string GetCardType(object cardState)
        {
            try
            {
                var type = cardState.GetType();
                var getCardTypeMethod = type.GetMethod("GetCardType");
                if (getCardTypeMethod != null)
                {
                    var cardType = getCardTypeMethod.Invoke(cardState, null);
                    if (cardType != null)
                    {
                        string typeName = cardType.ToString();
                        // Convert enum value to readable name
                        if (typeName == "Monster") return "Unit";
                        if (typeName == "Spell") return "Spell";
                        if (typeName == "Blight") return "Blight";
                        return typeName;
                    }
                }
            }
            catch { }
            return null;
        }

        private static string GetCardClan(object cardState)
        {
            try
            {
                var type = cardState.GetType();

                // Get CardData from CardState
                var getCardDataMethod = type.GetMethod("GetCardDataRead", Type.EmptyTypes)
                                     ?? type.GetMethod("GetCardData", Type.EmptyTypes);
                if (getCardDataMethod == null) return null;

                var cardData = getCardDataMethod.Invoke(cardState, null);
                if (cardData == null) return null;

                var cardDataType = cardData.GetType();

                // Get linkedClass field from CardData
                var linkedClassField = cardDataType.GetField("linkedClass",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (linkedClassField == null) return null;

                var linkedClass = linkedClassField.GetValue(cardData);
                if (linkedClass == null) return null;

                var classType = linkedClass.GetType();

                // Try GetTitle() for localized name
                var getTitleMethod = classType.GetMethod("GetTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    var clanName = getTitleMethod.Invoke(linkedClass, null) as string;
                    if (!string.IsNullOrEmpty(clanName)) return clanName;
                }

                // Fallback to GetName()
                var getNameMethod = classType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    return getNameMethod.Invoke(linkedClass, null) as string;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Extract keyword definitions from a card's description
        /// </summary>
        private static string GetCardKeywords(string description)
        {
            if (string.IsNullOrEmpty(description)) return null;

            try
            {
                var keywords = new List<string>();

                // Keywords loaded from game localization + fallbacks
                var knownKeywords = KeywordManager.GetKeywords();

                foreach (var keyword in knownKeywords)
                {
                    // Check if keyword appears in description (as whole word)
                    if (Regex.IsMatch(description,
                        $@"\b{Regex.Escape(keyword.Key)}\b",
                        RegexOptions.IgnoreCase))
                    {
                        if (!keywords.Contains(keyword.Value))
                        {
                            keywords.Add(keyword.Value);
                        }
                    }
                }

                if (keywords.Count > 0)
                {
                    return string.Join(". ", keywords);
                }
            }
            catch { }
            return null;
        }
    }
}
