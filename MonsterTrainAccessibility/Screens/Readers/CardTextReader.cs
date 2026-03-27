using MonsterTrainAccessibility.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Extracted reader for Card UI elements.
    /// </summary>
    internal static class CardTextReader
    {

        /// <summary>
        /// Get full card details when arrowing over a card in the hand (CardUI component)
        /// </summary>
        internal static string GetCardUIText(GameObject go)
        {
            try
            {
                // Find CardUI component on this object or in hierarchy
                Component cardUIComponent = null;

                // Check this object and its children first
                foreach (var component in go.GetComponentsInChildren<Component>())
                {
                    if (component == null) continue;
                    if (component.GetType().Name == "CardUI")
                    {
                        cardUIComponent = component;
                        break;
                    }
                }

                // If not found, check parents
                if (cardUIComponent == null)
                {
                    Transform current = go.transform;
                    while (current != null && cardUIComponent == null)
                    {
                        foreach (var component in current.GetComponents<Component>())
                        {
                            if (component == null) continue;
                            if (component.GetType().Name == "CardUI")
                            {
                                cardUIComponent = component;
                                break;
                            }
                        }
                        current = current.parent;
                    }
                }

                if (cardUIComponent == null)
                    return null;

                // Get CardState from CardUI
                var cardUIType = cardUIComponent.GetType();
                object cardState = null;

                // Try common field/property names for the card state reference
                var cardStateField = cardUIType.GetField("cardState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (cardStateField != null)
                {
                    cardState = cardStateField.GetValue(cardUIComponent);
                }

                // Try _cardState
                if (cardState == null)
                {
                    cardStateField = cardUIType.GetField("_cardState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (cardStateField != null)
                    {
                        cardState = cardStateField.GetValue(cardUIComponent);
                    }
                }

                // Try CardState property
                if (cardState == null)
                {
                    var cardStateProp = cardUIType.GetProperty("CardState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (cardStateProp != null)
                    {
                        cardState = cardStateProp.GetValue(cardUIComponent);
                    }
                }

                // Try GetCard or GetCardState method
                if (cardState == null)
                {
                    var getCardMethod = cardUIType.GetMethod("GetCard", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (getCardMethod != null && getCardMethod.GetParameters().Length == 0)
                    {
                        cardState = getCardMethod.Invoke(cardUIComponent, null);
                    }
                }

                if (cardState == null)
                {
                    var getCardStateMethod = cardUIType.GetMethod("GetCardState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (getCardStateMethod != null && getCardStateMethod.GetParameters().Length == 0)
                    {
                        cardState = getCardStateMethod.Invoke(cardUIComponent, null);
                    }
                }

                if (cardState == null)
                {
                    MonsterTrainAccessibility.LogInfo("CardUI found but couldn't get CardState");
                    return null;
                }

                // Format the card details
                string cardDetails = FormatCardDetails(cardState);

                var currentScreen = Help.ScreenStateTracker.CurrentScreen;

                // Look for an upgrade path name/title above the card (e.g., "Wrathful", "Brawler")
                // Only on champion upgrade and clan selection screens
                if (currentScreen == Help.GameScreen.ChampionUpgrade ||
                    currentScreen == Help.GameScreen.ClanSelection)
                {
                    string upgradePath = FindUpgradePathName(cardUIComponent);

                    // Prepend upgrade path if found
                    if (!string.IsNullOrEmpty(upgradePath) && !string.IsNullOrEmpty(cardDetails))
                    {
                        return upgradePath + ": " + cardDetails;
                    }
                }

                // On deck view, add card position (e.g., "Card 3 of 20")
                if (currentScreen == Help.GameScreen.DeckView && !string.IsNullOrEmpty(cardDetails))
                {
                    try
                    {
                        Transform cardTransform = cardUIComponent.transform;
                        Transform parent = cardTransform.parent;
                        if (parent != null)
                        {
                            int siblingIndex = cardTransform.GetSiblingIndex();
                            // Count only active children (visible cards)
                            int totalCards = 0;
                            for (int i = 0; i < parent.childCount; i++)
                            {
                                if (parent.GetChild(i).gameObject.activeSelf)
                                    totalCards++;
                            }
                            // Calculate position among active siblings
                            int position = 0;
                            for (int i = 0; i <= siblingIndex && i < parent.childCount; i++)
                            {
                                if (parent.GetChild(i).gameObject.activeSelf)
                                    position++;
                            }
                            if (totalCards > 0)
                            {
                                cardDetails = $"{cardDetails} Card {position} of {totalCards}.";
                            }
                        }
                    }
                    catch { }
                }

                return cardDetails;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting card UI text: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Find upgrade path name (like "Wrathful", "Brawler") for champion upgrade screens
        /// Looks for title text elements above or near the CardUI
        /// </summary>
        internal static string FindUpgradePathName(Component cardUIComponent)
        {
            try
            {
                if (cardUIComponent == null) return null;

                Transform cardTransform = cardUIComponent.transform;

                // Look for a parent that might contain the title
                // Common patterns: the card is in a container with a sibling title element
                Transform parent = cardTransform.parent;
                while (parent != null)
                {
                    // Look for TMP_Text components in siblings or parent's direct children
                    foreach (Transform sibling in parent)
                    {
                        // Skip the card itself and its descendants
                        if (sibling == cardTransform || sibling.IsChildOf(cardTransform))
                            continue;

                        // Look for text components
                        foreach (var component in sibling.GetComponentsInChildren<Component>())
                        {
                            if (component == null) continue;
                            string typeName = component.GetType().Name;

                            if (typeName == "TextMeshProUGUI" || typeName == "ExtendedTextMeshProUGUI" ||
                                typeName == "TextMeshPro" || typeName == "Text")
                            {
                                // Get the text property
                                var textProp = component.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                                if (textProp != null)
                                {
                                    string text = textProp.GetValue(component) as string;
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        text = text.Trim();
                                        // Upgrade path names are typically short (1-2 words)
                                        // and don't contain numbers, colons, or card-like content
                                        if (text.Length > 0 && text.Length < 30 &&
                                            !text.Contains(":") && !text.Contains(".") &&
                                            !System.Text.RegularExpressions.Regex.IsMatch(text, @"\d") &&
                                            !text.ToLower().Contains("champion") &&
                                            !text.ToLower().Contains("cost") &&
                                            !text.ToLower().Contains("ember") &&
                                            !DialogTextReader.IsGarbageText(text))
                                        {
                                            // Check if the text element is positioned above the card
                                            // by comparing Y positions (higher Y = above in UI)
                                            RectTransform textRect = component.transform as RectTransform;
                                            RectTransform cardRect = cardTransform as RectTransform;

                                            if (textRect != null && cardRect != null)
                                            {
                                                // If text is above the card (higher world position Y)
                                                if (textRect.position.y > cardRect.position.y)
                                                {
                                                    MonsterTrainAccessibility.LogInfo($"Found upgrade path name: '{text}'");
                                                    return text;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Move up the hierarchy
                    parent = parent.parent;

                    // Don't go too far up
                    if (parent != null && (parent.name.Contains("Canvas") || parent.name.Contains("Screen")))
                        break;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error finding upgrade path name: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Format card details into a readable string (name, type, clan, cost, description)
        /// </summary>
        internal static string FormatCardDetails(object cardState)
        {
            try
            {
                var sb = new StringBuilder();
                var type = cardState.GetType();

                MonsterTrainAccessibility.LogInfo($"FormatCardDetails called for type: {type.Name}");

                // Get card name - CardState uses GetTitle(), CardData uses GetName()
                string name = "Unknown Card";
                var getTitleMethod = type.GetMethod("GetTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    name = getTitleMethod.Invoke(cardState, null) as string ?? name;
                }
                if (name == "Unknown Card")
                {
                    var getNameMethod = type.GetMethod("GetName", Type.EmptyTypes);
                    if (getNameMethod != null)
                    {
                        name = getNameMethod.Invoke(cardState, null) as string ?? name;
                    }
                }
                MonsterTrainAccessibility.LogInfo($"Card name: {name}");

                // Get card type
                string cardType = null;
                var getCardTypeMethod = type.GetMethod("GetCardType");
                if (getCardTypeMethod != null)
                {
                    var cardTypeObj = getCardTypeMethod.Invoke(cardState, null);
                    if (cardTypeObj != null)
                    {
                        cardType = cardTypeObj.ToString();
                        if (cardType == "Monster") cardType = "Unit";
                    }
                }

                // Get ember cost (GetCostWithoutTraits includes upgrade cost reductions)
                int cost = 0;
                var getCostMethod = type.GetMethod("GetCostWithoutTraits", Type.EmptyTypes)
                                  ?? type.GetMethod("GetCostWithoutAnyModifications", Type.EmptyTypes)
                                  ?? type.GetMethod("GetCost", Type.EmptyTypes);
                if (getCostMethod != null)
                {
                    var costResult = getCostMethod.Invoke(cardState, null);
                    if (costResult is int c) cost = c;
                }

                // Get CardData to access linked class (clan) and better descriptions
                object cardData = null;
                string clanName = null;
                string description = null;

                var getCardDataMethod = type.GetMethod("GetCardDataRead", Type.EmptyTypes)
                                     ?? type.GetMethod("GetCardData", Type.EmptyTypes);
                MonsterTrainAccessibility.LogInfo($"GetCardData method: {(getCardDataMethod != null ? getCardDataMethod.Name : "NOT FOUND")}");

                // Log all methods that might be related to card data
                var cardDataMethods = type.GetMethods()
                    .Where(m => m.Name.Contains("CardData") || m.Name.Contains("Data"))
                    .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
                    .Distinct()
                    .ToArray();
                MonsterTrainAccessibility.LogInfo($"CardState data methods: {string.Join(", ", cardDataMethods)}");

                if (getCardDataMethod != null)
                {
                    cardData = getCardDataMethod.Invoke(cardState, null);
                    MonsterTrainAccessibility.LogInfo($"CardData result: {(cardData != null ? cardData.GetType().Name : "null")}");
                }

                // If we couldn't get CardData via method, the input might already be a CardData
                if (cardData == null && type.Name == "CardData")
                {
                    cardData = cardState;
                    MonsterTrainAccessibility.LogInfo("Input is already CardData, using directly");
                }

                if (cardData != null)
                {
                    var cardDataType = cardData.GetType();

                    // Get linked class (clan) from CardData
                    var linkedClassField = cardDataType.GetField("linkedClass", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (linkedClassField != null)
                    {
                        var linkedClass = linkedClassField.GetValue(cardData);
                        if (linkedClass != null)
                        {
                            var classType = linkedClass.GetType();
                            // Try GetTitle() for localized name
                            var getClassTitleMethod = classType.GetMethod("GetTitle", Type.EmptyTypes);
                            if (getClassTitleMethod != null)
                            {
                                clanName = getClassTitleMethod.Invoke(linkedClass, null) as string;
                            }
                            // Fallback to GetName()
                            if (string.IsNullOrEmpty(clanName))
                            {
                                var getClassNameMethod = classType.GetMethod("GetName", Type.EmptyTypes);
                                if (getClassNameMethod != null)
                                {
                                    clanName = getClassNameMethod.Invoke(linkedClass, null) as string;
                                }
                            }
                        }
                    }

                    // Try GetDescription from CardData for effect text
                    var getDescMethod = cardDataType.GetMethod("GetDescription", Type.EmptyTypes);
                    if (getDescMethod != null)
                    {
                        description = getDescMethod.Invoke(cardData, null) as string;
                    }

                    // If no parameterless GetDescription, try with RelicManager parameter
                    if (string.IsNullOrEmpty(description))
                    {
                        var allDescMethods = cardDataType.GetMethods().Where(m => m.Name.Contains("Description")).ToArray();
                        foreach (var descMethod in allDescMethods)
                        {
                            var ps = descMethod.GetParameters();
                            // Log available description methods for debugging
                            MonsterTrainAccessibility.LogInfo($"CardData has description method: {descMethod.Name}({string.Join(", ", ps.Select(p => p.ParameterType.Name))})");
                        }
                    }
                }

                // Try GetCardText on CardState - this is the main method for card effect text
                if (string.IsNullOrEmpty(description))
                {
                    // Log all GetCardText methods for debugging
                    var cardTextMethods = type.GetMethods().Where(m => m.Name == "GetCardText").ToArray();
                    MonsterTrainAccessibility.LogInfo($"Found {cardTextMethods.Length} GetCardText methods");
                    foreach (var method in cardTextMethods)
                    {
                        var ps = method.GetParameters();
                        MonsterTrainAccessibility.LogInfo($"  GetCardText({string.Join(", ", ps.Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
                    }

                    // Try GetCardText with no parameters first
                    var getCardTextMethod = type.GetMethod("GetCardText", Type.EmptyTypes);
                    if (getCardTextMethod != null)
                    {
                        description = getCardTextMethod.Invoke(cardState, null) as string;
                        MonsterTrainAccessibility.LogInfo($"GetCardText() returned: '{description}'");
                    }

                    // If no parameterless version, try with parameters
                    if (string.IsNullOrEmpty(description))
                    {
                        foreach (var method in cardTextMethods)
                        {
                            var ps = method.GetParameters();
                            try
                            {
                                var args = new object[ps.Length];
                                for (int i = 0; i < ps.Length; i++)
                                {
                                    if (ps[i].ParameterType == typeof(bool))
                                        args[i] = true;
                                    else if (ps[i].ParameterType.IsValueType)
                                        args[i] = Activator.CreateInstance(ps[i].ParameterType);
                                    else
                                        args[i] = null;
                                }
                                description = method.Invoke(cardState, args) as string;
                                MonsterTrainAccessibility.LogInfo($"GetCardText with {ps.Length} params returned: '{description}'");
                                if (!string.IsNullOrEmpty(description)) break;
                            }
                            catch (Exception ex)
                            {
                                MonsterTrainAccessibility.LogInfo($"GetCardText failed: {ex.Message}");
                            }
                        }
                    }
                }

                // Fallback: try GetAssetDescription
                if (string.IsNullOrEmpty(description))
                {
                    var getAssetDescMethod = type.GetMethod("GetAssetDescription", Type.EmptyTypes);
                    if (getAssetDescMethod != null)
                    {
                        description = getAssetDescMethod.Invoke(cardState, null) as string;
                    }
                }

                // Log if we still have no description
                if (string.IsNullOrEmpty(description))
                {
                    var cardTextMethods = type.GetMethods().Where(m => m.Name == "GetCardText").ToArray();
                    MonsterTrainAccessibility.LogInfo($"GetCardText methods: {string.Join(", ", cardTextMethods.Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})"))}");
                }

                // Get rarity from CardState
                string rarity = null;
                var getRarityMethod = type.GetMethod("GetRarityType", Type.EmptyTypes);
                if (getRarityMethod != null)
                {
                    var rarityResult = getRarityMethod.Invoke(cardState, null);
                    if (rarityResult != null)
                    {
                        rarity = rarityResult.ToString();
                    }
                }

                // Check if card has upgrades applied (CardState only, not CardData)
                // Upgrades are in CardState.cardModifiers.GetCardUpgrades()
                bool hasUpgrades = false;
                if (type.Name == "CardState")
                {
                    var modifiersField = type.GetField("cardModifiers", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (modifiersField != null)
                    {
                        var modifiers = modifiersField.GetValue(cardState);
                        if (modifiers != null)
                        {
                            var getCardUpgradesMethod = modifiers.GetType().GetMethod("GetCardUpgrades", Type.EmptyTypes);
                            if (getCardUpgradesMethod != null)
                            {
                                var upgrades = getCardUpgradesMethod.Invoke(modifiers, null) as System.Collections.IList;
                                if (upgrades != null && upgrades.Count > 0)
                                    hasUpgrades = true;
                            }
                        }
                    }
                }

                // Get unit subtype (e.g., "Imp", "Hollow", "Channeler") for monster cards
                string unitSubtype = null;
                if (cardType == "Unit" || cardType == "Monster")
                {
                    try
                    {
                        // Try CardState.GetSpawnCharacterData() first, then CardData's
                        object charDataForSubtype = null;
                        var getSpawnMethod = type.GetMethod("GetSpawnCharacterData", Type.EmptyTypes);
                        if (getSpawnMethod != null)
                            charDataForSubtype = getSpawnMethod.Invoke(cardState, null);

                        if (charDataForSubtype == null && cardData != null)
                        {
                            getSpawnMethod = cardData.GetType().GetMethod("GetSpawnCharacterData", Type.EmptyTypes);
                            if (getSpawnMethod != null)
                                charDataForSubtype = getSpawnMethod.Invoke(cardData, null);
                        }

                        if (charDataForSubtype != null)
                        {
                            var getSubtypeMethod = charDataForSubtype.GetType().GetMethod("GetLocalizedSubtype", Type.EmptyTypes);
                            if (getSubtypeMethod != null)
                            {
                                unitSubtype = getSubtypeMethod.Invoke(charDataForSubtype, null) as string;
                            }
                        }
                    }
                    catch (Exception subtypeEx)
                    {
                        MonsterTrainAccessibility.LogError($"Error getting unit subtype: {subtypeEx.Message}");
                    }
                }

                // Build announcement: [Upgraded] Name (Rarity [Subtype] Type), Clan, Cost. Effect.
                if (hasUpgrades)
                    sb.Append("Upgraded ");
                sb.Append(name);
                if (!string.IsNullOrEmpty(cardType) || !string.IsNullOrEmpty(rarity))
                {
                    sb.Append(" (");
                    if (!string.IsNullOrEmpty(rarity))
                    {
                        sb.Append(rarity);
                        if (!string.IsNullOrEmpty(unitSubtype) || !string.IsNullOrEmpty(cardType))
                            sb.Append(" ");
                    }
                    if (!string.IsNullOrEmpty(unitSubtype))
                    {
                        sb.Append(unitSubtype);
                        if (!string.IsNullOrEmpty(cardType))
                            sb.Append(" ");
                    }
                    if (!string.IsNullOrEmpty(cardType))
                        sb.Append(cardType);
                    sb.Append(")");
                }
                if (!string.IsNullOrEmpty(clanName))
                {
                    sb.Append($", {clanName}");
                }
                sb.Append($", {cost} ember");

                if (!string.IsNullOrEmpty(description))
                {
                    // Strip rich text tags for screen reader output
                    description = TextUtilities.StripRichTextTags(description);
                    // Resolve any remaining effect placeholders
                    description = ResolveCardEffectPlaceholders(description, cardState, cardData);
                    // Add context to standalone numbers (e.g., "+2" -> "+2 ember" if it's the only content)
                    description = AddContextToEffectNumbers(description);
                    sb.Append($". {description}");
                }

                // For unit cards, try to get attack and health stats
                if (cardType == "Unit" || cardType == "Monster")
                {
                    MonsterTrainAccessibility.LogInfo($"Unit card detected, looking for stats. cardData is {(cardData != null ? "not null" : "NULL")}");
                    int attack = -1;
                    int health = -1;

                    // Try to get stats from CardState
                    // CardState has GetTotalAttackDamage() (not GetAttackDamage) which includes upgrades
                    var getAttackMethod = type.GetMethod("GetTotalAttackDamage", Type.EmptyTypes)
                                       ?? type.GetMethod("GetAttackDamage", Type.EmptyTypes);
                    MonsterTrainAccessibility.LogInfo($"GetAttack on CardState: {(getAttackMethod != null ? getAttackMethod.Name : "not found")}");
                    if (getAttackMethod != null)
                    {
                        var attackResult = getAttackMethod.Invoke(cardState, null);
                        if (attackResult is int a) attack = a;
                        MonsterTrainAccessibility.LogInfo($"Attack from CardState: {attack}");
                    }

                    // CardState.GetHealth() returns float (includes upgrades), not int
                    var getHPMethod = type.GetMethod("GetHealth", Type.EmptyTypes)
                                   ?? type.GetMethod("GetHP", Type.EmptyTypes)
                                   ?? type.GetMethod("GetMaxHP", Type.EmptyTypes);
                    MonsterTrainAccessibility.LogInfo($"GetHP/Health on CardState: {(getHPMethod != null ? getHPMethod.Name : "not found")}");
                    if (getHPMethod != null)
                    {
                        var hpResult = getHPMethod.Invoke(cardState, null);
                        if (hpResult is int h) health = h;
                        else if (hpResult is float f) health = (int)f;
                        MonsterTrainAccessibility.LogInfo($"Health from CardState: {health}");
                    }

                    // If not found on CardState, try GetSpawnCharacterData directly on CardState
                    MonsterTrainAccessibility.LogInfo($"Stats after CardState check: attack={attack}, health={health}");
                    if (attack < 0 || health < 0)
                    {
                        // GetSpawnCharacterData is directly on CardState, not CardData
                        var getSpawnCharMethod = type.GetMethod("GetSpawnCharacterData", Type.EmptyTypes);
                        MonsterTrainAccessibility.LogInfo($"GetSpawnCharacterData on CardState: {(getSpawnCharMethod != null ? "found" : "not found")}");
                        if (getSpawnCharMethod != null)
                        {
                            var charData = getSpawnCharMethod.Invoke(cardState, null);
                            MonsterTrainAccessibility.LogInfo($"SpawnCharacterData result: {(charData != null ? charData.GetType().Name : "null")}");
                            if (charData != null)
                            {
                                var charDataType = charData.GetType();

                                // Log all methods on character data
                                var charMethods = charDataType.GetMethods()
                                    .Where(m => m.Name.Contains("Attack") || m.Name.Contains("Damage") || m.Name.Contains("HP") || m.Name.Contains("Health") || m.Name.Contains("Size"))
                                    .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
                                    .Distinct()
                                    .ToArray();
                                MonsterTrainAccessibility.LogInfo($"CharacterData stat methods available: {string.Join(", ", charMethods)}");

                                if (attack < 0)
                                {
                                    var charAttackMethod = charDataType.GetMethod("GetAttackDamage", Type.EmptyTypes);
                                    if (charAttackMethod != null)
                                    {
                                        var attackResult = charAttackMethod.Invoke(charData, null);
                                        if (attackResult is int a) attack = a;
                                        MonsterTrainAccessibility.LogInfo($"Attack from CharacterData: {attack}");
                                    }
                                }

                                if (health < 0)
                                {
                                    var charHPMethod = charDataType.GetMethod("GetHealth", Type.EmptyTypes)
                                                   ?? charDataType.GetMethod("GetHP", Type.EmptyTypes)
                                                   ?? charDataType.GetMethod("GetMaxHP", Type.EmptyTypes);
                                    if (charHPMethod != null)
                                    {
                                        var hpResult = charHPMethod.Invoke(charData, null);
                                        if (hpResult is int h) health = h;
                                        MonsterTrainAccessibility.LogInfo($"Health from CharacterData: {health}");
                                    }
                                }

                                // Log what methods are available if still not found
                                if (attack < 0 || health < 0)
                                {
                                    var statMethods = charDataType.GetMethods()
                                        .Where(m => m.Name.Contains("Attack") || m.Name.Contains("Damage") || m.Name.Contains("HP") || m.Name.Contains("Health") || m.Name.Contains("Size"))
                                        .Select(m => m.Name)
                                        .Distinct()
                                        .ToArray();
                                    MonsterTrainAccessibility.LogInfo($"CharacterData stat methods: {string.Join(", ", statMethods)}");
                                }
                            }
                        }
                    }

                    // Append unit stats
                    if (attack >= 0 || health >= 0)
                    {
                        var stats = new List<string>();
                        if (attack >= 0) stats.Add($"{attack} attack");
                        if (health >= 0) stats.Add($"{health} health");
                        sb.Append($". {string.Join(", ", stats)}");
                    }
                }

                // Get keyword tooltips (Permafrost, Frozen, Regen, etc.)
                // Pass the description we already have to avoid re-fetching
                string keywordTooltips = GetCardKeywordTooltips(cardState, cardData, description);
                if (!string.IsNullOrEmpty(keywordTooltips))
                {
                    sb.Append($". Keywords: {keywordTooltips}");
                }

                var result = sb.ToString();
                MonsterTrainAccessibility.LogInfo($"FormatCardDetails result: {result}");
                return result;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error formatting card details: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Resolve placeholders like {[effect0.power]} in card descriptions
        /// </summary>
        internal static string ResolveCardEffectPlaceholders(string text, object cardState, object cardData)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains("{[effect"))
                return text;

            try
            {
                // Get effects from CardData
                object[] effects = null;

                if (cardData != null)
                {
                    var cardDataType = cardData.GetType();
                    var getEffectsMethod = cardDataType.GetMethod("GetEffects", Type.EmptyTypes);
                    if (getEffectsMethod != null)
                    {
                        var result = getEffectsMethod.Invoke(cardData, null);
                        if (result is System.Collections.IList list)
                        {
                            effects = new object[list.Count];
                            list.CopyTo(effects, 0);
                        }
                    }
                }

                if (effects == null || effects.Length == 0)
                    return text;

                // Match patterns like {[effect0.power]} or {[effect0.status0.power]}
                var regex = new System.Text.RegularExpressions.Regex(@"\{\[effect(\d+)\.(?:status(\d+)\.)?(\w+)\]\}");
                text = regex.Replace(text, match =>
                {
                    int effectIndex = int.Parse(match.Groups[1].Value);
                    string property = match.Groups[3].Value;
                    int statusIndex = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : -1;

                    if (effectIndex < effects.Length)
                    {
                        var effect = effects[effectIndex];
                        if (effect != null)
                        {
                            var effectType = effect.GetType();

                            if (statusIndex >= 0)
                            {
                                // Handle status effect references
                                var statusesField = effectType.GetField("paramStatusEffects", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (statusesField != null)
                                {
                                    var statuses = statusesField.GetValue(effect) as System.Collections.IList;
                                    if (statuses != null && statusIndex < statuses.Count)
                                    {
                                        var status = statuses[statusIndex];
                                        if (status != null)
                                        {
                                            var statusType = status.GetType();
                                            var propField = statusType.GetField("count", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                                         ?? statusType.GetField("_count", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                            if (propField != null)
                                            {
                                                var val = propField.GetValue(status);
                                                return val?.ToString() ?? match.Value;
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Map property names to field names
                                string fieldName = property.ToLower() == "power" ? "paramInt" : "param" + char.ToUpper(property[0]) + property.Substring(1);
                                var field = effectType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (field != null)
                                {
                                    var val = field.GetValue(effect);
                                    return val?.ToString() ?? match.Value;
                                }
                            }
                        }
                    }
                    return match.Value;
                });
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error resolving card effect placeholders: {ex.Message}");
            }

            return text;
        }


        /// <summary>
        /// Add context to standalone effect numbers (e.g., "+2" might mean "+2 ember")
        /// </summary>
        internal static string AddContextToEffectNumbers(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Don't modify text that already has clear context
            if (text.Contains("ember") || text.Contains("gold") || text.Contains("damage") ||
                text.Contains("health") || text.Contains("attack") || text.Contains("armor"))
                return text;

            // Pattern: standalone numbers like "+2" or "-1" at word boundaries
            var standaloneNumber = new System.Text.RegularExpressions.Regex(@"^\s*([+-]?\d+)\s*$");
            var match = standaloneNumber.Match(text);
            if (match.Success)
            {
                // Just a number - this might be ember gain, gold, etc.
                // Make it clearer
                return $"Effect value: {match.Groups[1].Value}";
            }

            return text;
        }


        /// <summary>
        /// Get keyword tooltip definitions from a card (Permafrost, Frozen, Regen, etc.)
        /// Returns formatted string of keyword definitions
        /// </summary>
        internal static string GetCardKeywordTooltips(object cardState, object cardData, string cardDescription = null)
        {
            try
            {
                var tooltips = new List<string>();

                // Method 1: Try to get linked tooltips directly from CardState
                if (cardState != null)
                {
                    var stateType = cardState.GetType();

                    // Try GetEffectTooltipData or similar methods
                    var getTooltipsMethod = stateType.GetMethods()
                        .FirstOrDefault(m => m.Name.Contains("Tooltip") && m.GetParameters().Length == 0);
                    if (getTooltipsMethod != null)
                    {
                        var tooltipResult = getTooltipsMethod.Invoke(cardState, null);
                        if (tooltipResult is System.Collections.IList tooltipList)
                        {
                            foreach (var tooltip in tooltipList)
                            {
                                string tooltipText = ExtractTooltipText(tooltip);
                                if (!string.IsNullOrEmpty(tooltipText))
                                    tooltips.Add(tooltipText);
                            }
                        }
                    }
                }

                // Method 2: Get tooltips from CardData's effects
                if (cardData != null && tooltips.Count == 0)
                {
                    var dataType = cardData.GetType();

                    // Get card effects - each effect can have additionalTooltips
                    var getEffectsMethod = dataType.GetMethod("GetEffects", Type.EmptyTypes);
                    if (getEffectsMethod != null)
                    {
                        var effects = getEffectsMethod.Invoke(cardData, null) as System.Collections.IList;
                        if (effects != null)
                        {
                            foreach (var effect in effects)
                            {
                                // Get additionalTooltips from each effect
                                ExtractTooltipsFromEffect(effect, tooltips);

                                // Also get status effect tooltips from paramStatusEffects
                                ExtractStatusEffectTooltips(effect, tooltips);
                            }
                        }
                    }

                    // Also check card traits for tooltips
                    var getTraitsMethod = dataType.GetMethod("GetTraits", Type.EmptyTypes);
                    if (getTraitsMethod != null)
                    {
                        var traits = getTraitsMethod.Invoke(cardData, null) as System.Collections.IList;
                        if (traits != null)
                        {
                            foreach (var trait in traits)
                            {
                                ExtractTraitTooltip(trait, tooltips);
                            }
                        }
                    }
                }

                // Method 3: Parse keywords from card description and look up definitions
                // Use the passed description if available, otherwise try to fetch it
                if (tooltips.Count == 0)
                {
                    string desc = cardDescription;
                    if (string.IsNullOrEmpty(desc))
                    {
                        desc = GetCardDescriptionForKeywordParsing(cardState, cardData);
                    }
                    if (!string.IsNullOrEmpty(desc))
                    {
                        ExtractKeywordsFromDescription(desc, tooltips);
                    }
                }

                if (tooltips.Count > 0)
                {
                    return string.Join(". ", tooltips.Distinct());
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting keyword tooltips: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Extract tooltip text from a tooltip data object
        /// </summary>
        internal static string ExtractTooltipText(object tooltip)
        {
            if (tooltip == null) return null;

            try
            {
                var tooltipType = tooltip.GetType();
                string title = null;
                string body = null;

                // Try GetTitle/GetBody methods
                var getTitleMethod = tooltipType.GetMethod("GetTitle", Type.EmptyTypes);
                var getBodyMethod = tooltipType.GetMethod("GetBody", Type.EmptyTypes)
                                 ?? tooltipType.GetMethod("GetDescription", Type.EmptyTypes);

                if (getTitleMethod != null)
                    title = getTitleMethod.Invoke(tooltip, null) as string;
                if (getBodyMethod != null)
                    body = getBodyMethod.Invoke(tooltip, null) as string;

                // Try title/body fields
                if (string.IsNullOrEmpty(title))
                {
                    var titleField = tooltipType.GetField("title", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                  ?? tooltipType.GetField("_title", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (titleField != null)
                        title = titleField.GetValue(tooltip) as string;
                }

                if (string.IsNullOrEmpty(body))
                {
                    var bodyField = tooltipType.GetField("body", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                 ?? tooltipType.GetField("description", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (bodyField != null)
                        body = bodyField.GetValue(tooltip) as string;
                }

                // Localize if needed
                title = LocalizationHelper.Localize(title);
                body = LocalizationHelper.Localize(body);

                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(body))
                {
                    return $"{TextUtilities.StripRichTextTags(title)}: {TextUtilities.StripRichTextTags(body)}";
                }
                else if (!string.IsNullOrEmpty(title))
                {
                    return TextUtilities.StripRichTextTags(title);
                }
            }
            catch { }
            return null;
        }


        /// <summary>
        /// Extract tooltips from a card effect (CardEffectData)
        /// </summary>
        internal static void ExtractTooltipsFromEffect(object effect, List<string> tooltips)
        {
            if (effect == null) return;

            try
            {
                var effectType = effect.GetType();

                // Get additionalTooltips field
                var tooltipsField = effectType.GetField("additionalTooltips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (tooltipsField != null)
                {
                    var additionalTooltips = tooltipsField.GetValue(effect) as System.Collections.IList;
                    if (additionalTooltips != null)
                    {
                        foreach (var tooltip in additionalTooltips)
                        {
                            string text = ExtractAdditionalTooltipData(tooltip);
                            if (!string.IsNullOrEmpty(text) && !tooltips.Contains(text))
                                tooltips.Add(text);
                        }
                    }
                }
            }
            catch { }
        }


        /// <summary>
        /// Extract tooltip from AdditionalTooltipData
        /// </summary>
        internal static string ExtractAdditionalTooltipData(object tooltipData)
        {
            if (tooltipData == null) return null;

            try
            {
                var type = tooltipData.GetType();

                // AdditionalTooltipData has titleKey, descriptionKey, or title/description
                string title = null;
                string description = null;

                // Try titleKey/descriptionKey first
                var titleKeyField = type.GetField("titleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var descKeyField = type.GetField("descriptionKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (titleKeyField != null)
                {
                    string key = titleKeyField.GetValue(tooltipData) as string;
                    title = LocalizationHelper.LocalizeOrNull(key);
                }

                if (descKeyField != null)
                {
                    string key = descKeyField.GetValue(tooltipData) as string;
                    description = LocalizationHelper.LocalizeOrNull(key);
                }

                // Also try direct title/description
                if (string.IsNullOrEmpty(title))
                {
                    var titleField = type.GetField("title", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (titleField != null)
                        title = titleField.GetValue(tooltipData) as string;
                }

                if (string.IsNullOrEmpty(description))
                {
                    var descField = type.GetField("description", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (descField != null)
                        description = descField.GetValue(tooltipData) as string;
                }

                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(description))
                {
                    return $"{TextUtilities.StripRichTextTags(title)}: {TextUtilities.StripRichTextTags(description)}";
                }
            }
            catch { }
            return null;
        }


        /// <summary>
        /// Extract status effect tooltips from a card effect's paramStatusEffects
        /// </summary>
        internal static void ExtractStatusEffectTooltips(object effect, List<string> tooltips)
        {
            if (effect == null) return;

            try
            {
                var effectType = effect.GetType();

                // Get paramStatusEffects field
                var statusField = effectType.GetField("paramStatusEffects", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (statusField != null)
                {
                    var statusEffects = statusField.GetValue(effect) as System.Collections.IList;
                    if (statusEffects != null)
                    {
                        foreach (var statusEffect in statusEffects)
                        {
                            string tooltip = GetStatusEffectTooltip(statusEffect);
                            if (!string.IsNullOrEmpty(tooltip) && !tooltips.Contains(tooltip))
                                tooltips.Add(tooltip);
                        }
                    }
                }
            }
            catch { }
        }


        /// <summary>
        /// Get tooltip for a status effect stack/application
        /// </summary>
        internal static string GetStatusEffectTooltip(object statusEffectParam)
        {
            if (statusEffectParam == null) return null;

            try
            {
                var type = statusEffectParam.GetType();

                // Get the statusId
                string statusId = null;
                var statusIdField = type.GetField("statusId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (statusIdField != null)
                {
                    statusId = statusIdField.GetValue(statusEffectParam) as string;
                }

                if (!string.IsNullOrEmpty(statusId))
                {
                    // Look up the status effect data
                    return GetStatusEffectDefinition(statusId);
                }
            }
            catch { }
            return null;
        }


        /// <summary>
        /// Get the definition of a status effect by its ID
        /// </summary>
        internal static string GetStatusEffectDefinition(string statusId)
        {
            if (string.IsNullOrEmpty(statusId)) return null;

            try
            {
                // Try to get from StatusEffectManager
                var managerType = ReflectionHelper.FindType("StatusEffectManager");
                if (managerType != null)
                {
                    var instanceProp = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProp != null)
                    {
                        var manager = instanceProp.GetValue(null);
                        if (manager != null)
                        {
                            var getAllMethod = managerType.GetMethod("GetAllStatusEffectsData", Type.EmptyTypes);
                            if (getAllMethod != null)
                            {
                                var allData = getAllMethod.Invoke(manager, null);
                                if (allData != null)
                                {
                                    var getDataMethod = allData.GetType().GetMethod("GetStatusEffectData", Type.EmptyTypes);
                                    if (getDataMethod != null)
                                    {
                                        var dataList = getDataMethod.Invoke(allData, null) as System.Collections.IList;
                                        if (dataList != null)
                                        {
                                            foreach (var data in dataList)
                                            {
                                                var dataType = data.GetType();
                                                var getIdMethod = dataType.GetMethod("GetStatusId", Type.EmptyTypes);
                                                if (getIdMethod != null)
                                                {
                                                    string id = getIdMethod.Invoke(data, null) as string;
                                                    if (id == statusId)
                                                    {
                                                        // Found it - get name and description
                                                        return GetStatusEffectNameAndDescription(data, dataType);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Fallback: try localization directly
                string locKey = GetStatusEffectLocKey(statusId);
                string name = LocalizationHelper.LocalizeOrNull($"{locKey}_CardText");
                string desc = LocalizationHelper.LocalizeOrNull($"{locKey}_CardTooltipText");

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(desc) && name != $"{locKey}_CardText")
                {
                    return $"{TextUtilities.StripRichTextTags(name)}: {TextUtilities.StripRichTextTags(desc)}";
                }
            }
            catch { }
            return null;
        }


        /// <summary>
        /// Get name and description from StatusEffectData
        /// </summary>
        internal static string GetStatusEffectNameAndDescription(object statusData, Type dataType)
        {
            try
            {
                string name = null;
                string description = null;

                // Try GetDisplayName or similar
                var getNameMethod = dataType.GetMethod("GetDisplayName", Type.EmptyTypes)
                                 ?? dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    name = getNameMethod.Invoke(statusData, null) as string;
                }

                // Try GetDescription
                var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes)
                                 ?? dataType.GetMethod("GetTooltipText", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    description = getDescMethod.Invoke(statusData, null) as string;
                }

                // Fallback to localization
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(description))
                {
                    var getIdMethod = dataType.GetMethod("GetStatusId", Type.EmptyTypes);
                    if (getIdMethod != null)
                    {
                        string statusId = getIdMethod.Invoke(statusData, null) as string;
                        string locKey = GetStatusEffectLocKey(statusId);

                        if (string.IsNullOrEmpty(name))
                            name = LocalizationHelper.LocalizeOrNull($"{locKey}_CardText");
                        if (string.IsNullOrEmpty(description))
                            description = LocalizationHelper.LocalizeOrNull($"{locKey}_CardTooltipText");
                    }
                }

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(description))
                {
                    return $"{TextUtilities.StripRichTextTags(name)}: {TextUtilities.StripRichTextTags(description)}";
                }
            }
            catch { }
            return null;
        }


        /// <summary>
        /// Get localization key prefix for a status effect ID
        /// </summary>
        internal static string GetStatusEffectLocKey(string statusId)
        {
            if (string.IsNullOrEmpty(statusId)) return null;

            // Standard format: StatusEffect_[StatusId] with first letter capitalized
            if (statusId.Length == 1)
                return "StatusEffect_" + char.ToUpper(statusId[0]);
            else if (statusId.Length > 1)
                return "StatusEffect_" + char.ToUpper(statusId[0]) + statusId.Substring(1);

            return null;
        }


        /// <summary>
        /// Extract trait tooltips from CardTraitData
        /// </summary>
        internal static void ExtractTraitTooltip(object trait, List<string> tooltips)
        {
            if (trait == null) return;

            try
            {
                var traitType = trait.GetType();

                // Get trait name
                var getNameMethod = traitType.GetMethod("GetTraitStateName", Type.EmptyTypes)
                                 ?? traitType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    string traitName = getNameMethod.Invoke(trait, null) as string;
                    if (!string.IsNullOrEmpty(traitName))
                    {
                        // Look up trait definition
                        string tooltip = GetTraitDefinition(traitName);
                        if (!string.IsNullOrEmpty(tooltip) && !tooltips.Contains(tooltip))
                            tooltips.Add(tooltip);
                    }
                }
            }
            catch { }
        }


        /// <summary>
        /// Get tooltip definition for a card trait
        /// </summary>
        internal static string GetTraitDefinition(string traitName)
        {
            // Look up in shared keyword dictionary
            var keywords = Core.KeywordManager.GetKeywords();
            if (keywords.TryGetValue(traitName, out string definition))
            {
                return definition;
            }

            // Also try formatted name (e.g. "SelfPurge" -> "Self Purge")
            string formatted = FormatTraitName(traitName);
            if (formatted != traitName && keywords.TryGetValue(formatted, out definition))
            {
                return definition;
            }

            // Try localization as last resort
            string key = $"CardTrait_{traitName}_Tooltip";
            string localized = LocalizationHelper.LocalizeOrNull(key);
            if (!string.IsNullOrEmpty(localized) && localized != key)
            {
                return $"{FormatTraitName(traitName)}: {TextUtilities.StripRichTextTags(localized)}";
            }

            return null;
        }


        /// <summary>
        /// Format a trait name for display
        /// </summary>
        internal static string FormatTraitName(string traitName)
        {
            if (string.IsNullOrEmpty(traitName)) return traitName;

            // Remove "State" suffix and format
            traitName = traitName.Replace("State", "");
            return System.Text.RegularExpressions.Regex.Replace(traitName, "([a-z])([A-Z])", "$1 $2");
        }


        /// <summary>
        /// Get card description for keyword parsing
        /// </summary>
        internal static string GetCardDescriptionForKeywordParsing(object cardState, object cardData)
        {
            string desc = null;

            try
            {
                if (cardState != null)
                {
                    var type = cardState.GetType();
                    var getCardTextMethod = type.GetMethod("GetCardText", Type.EmptyTypes);
                    if (getCardTextMethod != null)
                    {
                        desc = getCardTextMethod.Invoke(cardState, null) as string;
                    }
                }

                if (string.IsNullOrEmpty(desc) && cardData != null)
                {
                    var dataType = cardData.GetType();
                    var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes);
                    if (getDescMethod != null)
                    {
                        desc = getDescMethod.Invoke(cardData, null) as string;
                    }
                }
            }
            catch { }

            return desc;
        }


        /// <summary>
        /// Extract keywords from card description text and look up their definitions
        /// </summary>
        internal static void ExtractKeywordsFromDescription(string description, List<string> tooltips)
        {
            if (string.IsNullOrEmpty(description)) return;

            // Keywords loaded from game localization + fallbacks
            var knownKeywords = Core.KeywordManager.GetKeywords();

            foreach (var keyword in knownKeywords)
            {
                // Check if keyword appears in description (as whole word)
                if (System.Text.RegularExpressions.Regex.IsMatch(description,
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(keyword.Key)}\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    if (!tooltips.Contains(keyword.Value))
                    {
                        tooltips.Add(keyword.Value);
                    }
                }
            }
        }

    }
}
