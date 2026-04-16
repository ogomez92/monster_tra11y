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
    /// Extracted reader for Shop UI elements.
    /// </summary>
    internal static class ShopTextReader
    {

        /// <summary>
        /// Get text for shop items (cards, relics, services/upgrades)
        /// </summary>
        internal static string GetShopItemText(GameObject go)
        {
            try
            {
                // Look for MerchantGoodDetailsUI (cards/relics for sale)
                Component goodDetailsUI = UITextHelper.FindComponentInHierarchy(go, "MerchantGoodDetailsUI");
                if (goodDetailsUI != null)
                {
                    string goodText = ExtractMerchantGoodInfo(goodDetailsUI);
                    if (!string.IsNullOrEmpty(goodText))
                    {
                        return goodText;
                    }
                }

                // Fallback: try to find relic info in the hierarchy (for artifact shops)
                string relicText = RelicTextReader.GetRelicInfoText(go);
                if (!string.IsNullOrEmpty(relicText))
                {
                    string price = GetPriceFromBuyButton(go);
                    if (!string.IsNullOrEmpty(price))
                        return InsertPriceAfterName(relicText, price);
                    return relicText;
                }

                // Look for MerchantServiceUI (services/upgrades)
                Component serviceUI = UITextHelper.FindComponentInHierarchy(go, "MerchantServiceUI");
                if (serviceUI != null)
                {
                    string serviceText = ExtractMerchantServiceInfo(serviceUI);
                    if (!string.IsNullOrEmpty(serviceText))
                    {
                        return serviceText;
                    }
                }

                // Look for BuyButton component to get price
                Component buyButton = UITextHelper.FindComponentInHierarchy(go, "BuyButton");
                if (buyButton != null)
                {
                    // Try to find the associated good or service
                    var buyType = buyButton.GetType();
                    var buyFields = buyType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    MonsterTrainAccessibility.LogInfo($"BuyButton fields: {string.Join(", ", buyFields.Select(f => f.Name))}");

                    // Look for good/service reference
                    foreach (var field in buyFields)
                    {
                        var value = field.GetValue(buyButton);
                        if (value == null) continue;

                        string typeName = value.GetType().Name;
                        if (typeName.Contains("Good") || typeName.Contains("Service") ||
                            typeName.Contains("Card") || typeName.Contains("Relic"))
                        {
                            MonsterTrainAccessibility.LogInfo($"Found {field.Name}: {typeName}");
                            string info = ExtractShopItemInfo(value);
                            if (!string.IsNullOrEmpty(info))
                            {
                                return info;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting shop item text: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Extract info from MerchantGoodDetailsUI (card/relic for sale)
        /// </summary>
        internal static string ExtractMerchantGoodInfo(Component goodUI)
        {
            try
            {
                var uiType = goodUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                MonsterTrainAccessibility.LogInfo($"MerchantGoodDetailsUI fields: {string.Join(", ", fields.Select(f => f.Name).Take(15))}");

                // Look for rewardUI field - this contains the actual reward data
                var rewardUIField = fields.FirstOrDefault(f => f.Name == "rewardUI");
                if (rewardUIField != null)
                {
                    var rewardUI = rewardUIField.GetValue(goodUI);
                    if (rewardUI != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found rewardUI: {rewardUI.GetType().Name}");
                        string rewardInfo = ExtractRewardUIInfo(rewardUI);
                        if (!string.IsNullOrEmpty(rewardInfo))
                        {
                            // Insert price after the first sentence (item name) so it's heard early
                            string price = GetPriceFromBuyButton(goodUI.gameObject);
                            return InsertPriceAfterName(rewardInfo, price);
                        }
                    }
                }

                // Fallback: look for card data directly
                foreach (var field in fields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("card") || fieldName.Contains("good") || fieldName.Contains("data"))
                    {
                        var value = field.GetValue(goodUI);
                        if (value != null)
                        {
                            string cardInfo = ExtractCardInfo(value);
                            if (!string.IsNullOrEmpty(cardInfo))
                            {
                                string price = GetPriceFromBuyButton(goodUI.gameObject);
                                return InsertPriceAfterName(cardInfo, price);
                            }
                        }
                    }
                }

                // Fallback: look for relic data fields
                foreach (var field in fields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("relic") || fieldName.Contains("artifact"))
                    {
                        var value = field.GetValue(goodUI);
                        if (value != null)
                        {
                            string relicInfo = ExtractRewardDataInfo(value);
                            if (!string.IsNullOrEmpty(relicInfo))
                            {
                                string price = GetPriceFromBuyButton(goodUI.gameObject);
                                return InsertPriceAfterName(relicInfo, price);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting merchant good info: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Insert price after the first sentence (the item name) of a shop item description.
        /// e.g. "Strengthstone. Upgrade. +10 Magic Power" + "20 gold"
        ///   -> "Strengthstone. 20 gold. Upgrade. +10 Magic Power"
        /// </summary>
        internal static string InsertPriceAfterName(string itemText, string price)
        {
            if (string.IsNullOrEmpty(price))
                return itemText;

            int firstPeriod = itemText.IndexOf('.');
            if (firstPeriod >= 0 && firstPeriod < itemText.Length - 1)
            {
                // Insert price after the first sentence
                return $"{itemText.Substring(0, firstPeriod + 1)} {price}{itemText.Substring(firstPeriod + 1)}";
            }

            // No period found or it's at the end - just append
            return $"{itemText}. {price}";
        }


        /// <summary>
        /// Extract info from a RewardUI component
        /// </summary>
        internal static string ExtractRewardUIInfo(object rewardUI)
        {
            if (rewardUI == null) return null;

            try
            {
                var uiType = rewardUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var methods = uiType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                MonsterTrainAccessibility.LogInfo($"RewardUI type: {uiType.Name}");

                // Priority 1: Check rewardData backing field first - this has the actual data
                var rewardDataField = fields.FirstOrDefault(f =>
                    f.Name == "<rewardData>k__BackingField" || f.Name == "rewardData");
                if (rewardDataField != null)
                {
                    var rewardData = rewardDataField.GetValue(rewardUI);
                    if (rewardData != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found rewardData: {rewardData.GetType().Name}");
                        string info = ExtractRewardDataInfo(rewardData);
                        if (!string.IsNullOrEmpty(info))
                            return info;
                    }
                }

                // Priority 2: Try GetRewardData method
                var getDataMethod = methods.FirstOrDefault(m =>
                    (m.Name == "GetRewardData" || m.Name == "GetData" || m.Name == "GetReward") &&
                    m.GetParameters().Length == 0);
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(rewardUI, null);
                    if (data != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"GetRewardData returned: {data.GetType().Name}");
                        string info = ExtractRewardDataInfo(data);
                        if (!string.IsNullOrEmpty(info))
                            return info;
                    }
                }

                // Priority 3: Check cardUI field for card rewards
                var cardUIField = fields.FirstOrDefault(f => f.Name == "cardUI");
                if (cardUIField != null)
                {
                    var cardUI = cardUIField.GetValue(rewardUI);
                    if (cardUI != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found cardUI: {cardUI.GetType().Name}");
                        string cardInfo = ExtractCardUIInfo(cardUI);
                        if (!string.IsNullOrEmpty(cardInfo))
                            return cardInfo;
                    }
                }

                // Priority 4: Check relicUI field
                var relicUIField = fields.FirstOrDefault(f => f.Name == "relicUI");
                if (relicUIField != null)
                {
                    var relicUI = relicUIField.GetValue(rewardUI);
                    if (relicUI != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found relicUI: {relicUI.GetType().Name}");
                        string relicInfo = ExtractRelicUIInfo(relicUI);
                        if (!string.IsNullOrEmpty(relicInfo))
                            return relicInfo;
                    }
                }

                // Priority 5: Check genericRewardUI field
                var genericField = fields.FirstOrDefault(f => f.Name == "genericRewardUI");
                if (genericField != null)
                {
                    var genericUI = genericField.GetValue(rewardUI);
                    if (genericUI != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found genericRewardUI: {genericUI.GetType().Name}");
                        string genericInfo = ExtractGenericRewardUIInfo(genericUI);
                        if (!string.IsNullOrEmpty(genericInfo))
                            return genericInfo;
                    }
                }

                // Fallback: If rewardUI is a Component, check its GameObject for text
                if (rewardUI is Component comp)
                {
                    string textInfo = MapTextReader.GetFirstMeaningfulChildText(comp.gameObject);
                    if (!string.IsNullOrEmpty(textInfo))
                        return textInfo;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting reward UI info: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Extract info from a CardUI component
        /// </summary>
        internal static string ExtractCardUIInfo(object cardUI)
        {
            if (cardUI == null) return null;

            try
            {
                var uiType = cardUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // Look for cardState field
                var cardStateField = fields.FirstOrDefault(f =>
                    f.Name.ToLower().Contains("cardstate") || f.Name.ToLower().Contains("card"));
                if (cardStateField != null)
                {
                    var cardState = cardStateField.GetValue(cardUI);
                    if (cardState != null)
                    {
                        return ExtractCardInfo(cardState);
                    }
                }

                // Try GetCardState method
                var getCardMethod = uiType.GetMethod("GetCardState", Type.EmptyTypes);
                if (getCardMethod != null)
                {
                    var cardState = getCardMethod.Invoke(cardUI, null);
                    if (cardState != null)
                    {
                        return ExtractCardInfo(cardState);
                    }
                }
            }
            catch { }

            return null;
        }


        /// <summary>
        /// Extract info from a RelicUI component
        /// </summary>
        internal static string ExtractRelicUIInfo(object relicUI)
        {
            if (relicUI == null) return null;

            try
            {
                var uiType = relicUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // Look for relicData field
                foreach (var field in fields)
                {
                    if (field.Name.ToLower().Contains("data") || field.Name.ToLower().Contains("relic"))
                    {
                        var data = field.GetValue(relicUI);
                        if (data != null)
                        {
                            string info = ExtractRewardDataInfo(data);
                            if (!string.IsNullOrEmpty(info))
                                return info;
                        }
                    }
                }

                // Try GetRelicData method
                var getDataMethod = uiType.GetMethod("GetRelicData", Type.EmptyTypes) ??
                                   uiType.GetMethod("GetData", Type.EmptyTypes);
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(relicUI, null);
                    if (data != null)
                    {
                        return ExtractRewardDataInfo(data);
                    }
                }
            }
            catch { }

            return null;
        }


        /// <summary>
        /// Extract info from a generic reward UI (RewardIconUI)
        /// </summary>
        internal static string ExtractGenericRewardUIInfo(object genericUI)
        {
            if (genericUI == null) return null;

            try
            {
                var uiType = genericUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // Look for data field
                foreach (var field in fields)
                {
                    if (field.Name.ToLower().Contains("data") || field.Name.ToLower().Contains("reward"))
                    {
                        var data = field.GetValue(genericUI);
                        if (data != null && !data.GetType().Name.Contains("Transform"))
                        {
                            string info = ExtractRewardDataInfo(data);
                            if (!string.IsNullOrEmpty(info))
                                return info;
                        }
                    }
                }
            }
            catch { }

            return null;
        }


        /// <summary>
        /// Extract info from reward data (CardData, RelicData, etc.)
        /// </summary>
        internal static string ExtractRewardDataInfo(object data)
        {
            if (data == null) return null;

            try
            {
                var dataType = data.GetType();
                string typeName = dataType.Name;

                MonsterTrainAccessibility.LogInfo($"Extracting reward data from: {typeName}");

                // Special handling for EnhancerRewardData (upgrade stones like Surgestone)
                if (typeName == "EnhancerRewardData")
                {
                    string enhancerInfo = ExtractEnhancerInfo(data);
                    if (!string.IsNullOrEmpty(enhancerInfo))
                        return enhancerInfo;
                }

                // Check if this is RelicData or a type that contains relic info
                bool isRelicType = typeName.Contains("Relic") || typeName.Contains("Artifact");

                // Try GetName method
                var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(data, null) as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        MonsterTrainAccessibility.LogInfo($"GetName returned: {name}");

                        // Try to get description too
                        string desc = null;
                        var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes);
                        if (getDescMethod != null)
                        {
                            desc = getDescMethod.Invoke(data, null) as string;
                        }

                        // RelicState.GetDescription requires RelicManager param - try with null
                        if (string.IsNullOrEmpty(desc))
                        {
                            var descMethods = dataType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                .Where(m => m.Name == "GetDescription" && m.GetParameters().Length == 1);
                            foreach (var dm in descMethods)
                            {
                                try
                                {
                                    desc = dm.Invoke(data, new object[] { null }) as string;
                                    if (!string.IsNullOrEmpty(desc)) break;
                                }
                                catch { }
                            }
                        }

                        // If description looks like a key or is empty, create a RelicState to get
                        // the description with numeric parameters filled in
                        if (string.IsNullOrEmpty(desc) || desc.Contains("_descriptionKey"))
                        {
                            if (isRelicType)
                            {
                                desc = SettingsTextReader.GetRelicDescription(data);
                                MonsterTrainAccessibility.LogInfo($"GetRelicDescription returned: {desc}");
                            }

                            // Fallback to raw localization for non-relic types
                            if (string.IsNullOrEmpty(desc))
                            {
                                var descKeyMethod = dataType.GetMethod("GetDescriptionKey", Type.EmptyTypes);
                                if (descKeyMethod != null)
                                {
                                    var key = descKeyMethod.Invoke(data, null) as string;
                                    if (!string.IsNullOrEmpty(key))
                                    {
                                        desc = LocalizationHelper.Localize(key);
                                        MonsterTrainAccessibility.LogInfo($"Localized description: {desc}");
                                    }
                                }
                            }
                        }

                        // For relics, resolve effect placeholders like {[effect0.power]}
                        if (isRelicType && !string.IsNullOrEmpty(desc) && desc.Contains("{[effect"))
                        {
                            desc = RelicTextReader.ResolveRelicEffectPlaceholders(desc, data, dataType);
                            MonsterTrainAccessibility.LogInfo($"Resolved relic description: {desc}");
                        }

                        // Try to get cost for cards
                        int cost = -1;
                        var getCostMethod = dataType.GetMethod("GetCost", Type.EmptyTypes);
                        if (getCostMethod != null)
                        {
                            var costResult = getCostMethod.Invoke(data, null);
                            if (costResult is int c)
                                cost = c;
                        }

                        List<string> parts = new List<string>();
                        if (isRelicType)
                        {
                            parts.Add($"Artifact: {TextUtilities.StripRichTextTags(name)}");
                        }
                        else
                        {
                            parts.Add(TextUtilities.StripRichTextTags(name));
                        }

                        if (cost >= 0)
                            parts.Add($"{cost} ember");

                        if (!string.IsNullOrEmpty(desc))
                        {
                            parts.Add(TextUtilities.StripRichTextTags(desc));

                            // Extract and add keyword explanations for relics
                            if (isRelicType)
                            {
                                var keywords = new List<string>();
                                CardTextReader.ExtractKeywordsFromDescription(desc, keywords);
                                if (keywords.Count > 0)
                                {
                                    parts.Add("Keywords: " + string.Join(". ", keywords));
                                }
                            }
                        }

                        return string.Join(". ", parts);
                    }
                }

                // For CardState, get CardData first
                if (typeName == "CardState")
                {
                    return ExtractCardInfo(data);
                }

                // Try fields
                var fields = dataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var nameField = fields.FirstOrDefault(f => f.Name.ToLower().Contains("name"));
                if (nameField != null)
                {
                    var name = nameField.GetValue(data) as string;
                    if (!string.IsNullOrEmpty(name))
                        return TextUtilities.StripRichTextTags(name);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting reward data: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Extract info from EnhancerRewardData (upgrade stones like Surgestone, Emberstone, etc.)
        /// </summary>
        internal static string ExtractEnhancerInfo(object enhancerRewardData)
        {
            if (enhancerRewardData == null) return null;

            try
            {
                var rewardType = enhancerRewardData.GetType();
                var fields = rewardType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // Look for the enhancerData field (the actual EnhancerData object)
                object enhancerData = null;
                var enhancerDataField = fields.FirstOrDefault(f =>
                    f.Name.ToLower().Contains("enhancerdata") ||
                    f.Name == "enhancer" ||
                    f.Name == "_enhancerData");

                if (enhancerDataField != null)
                {
                    enhancerData = enhancerDataField.GetValue(enhancerRewardData);
                }

                // Try GetEnhancerData method
                if (enhancerData == null)
                {
                    var getEnhancerMethod = rewardType.GetMethod("GetEnhancerData", Type.EmptyTypes)
                                         ?? rewardType.GetMethod("GetEnhancer", Type.EmptyTypes);
                    if (getEnhancerMethod != null)
                    {
                        enhancerData = getEnhancerMethod.Invoke(enhancerRewardData, null);
                    }
                }

                // Check backing field
                if (enhancerData == null)
                {
                    var backingField = fields.FirstOrDefault(f => f.Name == "<enhancerData>k__BackingField");
                    if (backingField != null)
                    {
                        enhancerData = backingField.GetValue(enhancerRewardData);
                    }
                }

                if (enhancerData != null)
                {
                    MonsterTrainAccessibility.LogInfo($"Found EnhancerData: {enhancerData.GetType().Name}");
                    return ExtractEnhancerDataInfo(enhancerData);
                }

                // If no enhancerData found, log available fields for debugging
                MonsterTrainAccessibility.LogInfo($"EnhancerRewardData fields: {string.Join(", ", fields.Select(f => f.Name).Take(15))}");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting enhancer info: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Extract info from EnhancerData (the actual upgrade stone data)
        /// </summary>
        internal static string ExtractEnhancerDataInfo(object enhancerData)
        {
            if (enhancerData == null) return null;

            try
            {
                var dataType = enhancerData.GetType();
                var fields = dataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var methods = dataType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                string name = null;
                string description = null;

                // Try GetName method
                var getNameMethod = methods.FirstOrDefault(m => m.Name == "GetName" && m.GetParameters().Length == 0);
                if (getNameMethod != null)
                {
                    name = getNameMethod.Invoke(enhancerData, null) as string;
                }

                // Try GetDescription method
                var getDescMethod = methods.FirstOrDefault(m => m.Name == "GetDescription" && m.GetParameters().Length == 0);
                if (getDescMethod != null)
                {
                    description = getDescMethod.Invoke(enhancerData, null) as string;
                }

                // Try GetID and localize
                if (string.IsNullOrEmpty(name))
                {
                    var getIdMethod = methods.FirstOrDefault(m => m.Name == "GetID" && m.GetParameters().Length == 0);
                    if (getIdMethod != null)
                    {
                        string id = getIdMethod.Invoke(enhancerData, null) as string;
                        if (!string.IsNullOrEmpty(id))
                        {
                            // Try standard localization keys
                            name = LocalizationHelper.LocalizeOrNull($"{id}_EnhancerData_NameKey")
                                ?? LocalizationHelper.LocalizeOrNull($"EnhancerData_{id}_Name");
                            if (string.IsNullOrEmpty(description))
                            {
                                description = LocalizationHelper.LocalizeOrNull($"{id}_EnhancerData_DescriptionKey")
                                           ?? LocalizationHelper.LocalizeOrNull($"EnhancerData_{id}_Description");
                            }
                        }
                    }
                }

                // Try to get upgrade info from the CardUpgradeData
                if (string.IsNullOrEmpty(description))
                {
                    // EnhancerData stores upgrade in effects[0].GetParamCardUpgradeData()
                    var getEffectsMethod = methods.FirstOrDefault(m => m.Name == "GetEffects" && m.GetParameters().Length == 0);
                    if (getEffectsMethod != null)
                    {
                        var effects = getEffectsMethod.Invoke(enhancerData, null) as System.Collections.IList;
                        if (effects != null && effects.Count > 0)
                        {
                            var effect = effects[0];
                            var effectType = effect.GetType();
                            var getUpgradeMethod = effectType.GetMethod("GetParamCardUpgradeData", Type.EmptyTypes);
                            if (getUpgradeMethod != null)
                            {
                                var upgradeData = getUpgradeMethod.Invoke(effect, null);
                                if (upgradeData != null)
                                {
                                    description = ExtractCardUpgradeDescription(upgradeData);
                                }
                            }
                        }
                    }
                }

                // Build result
                if (!string.IsNullOrEmpty(name))
                {
                    var parts = new List<string>();
                    parts.Add(TextUtilities.StripRichTextTags(name));
                    parts.Add("Upgrade");

                    if (!string.IsNullOrEmpty(description))
                    {
                        parts.Add(TextUtilities.StripRichTextTags(description));
                    }

                    // Add helper instruction
                    parts.Add("After selecting a card, press Enter to apply the upgrade");

                    // Extract and append keyword explanations
                    string fullText = string.Join(". ", parts);
                    var keywords = new List<string>();
                    CardTextReader.ExtractKeywordsFromDescription(fullText, keywords);
                    if (keywords.Count > 0)
                    {
                        parts.AddRange(keywords);
                    }

                    MonsterTrainAccessibility.LogInfo($"Enhancer result: {string.Join(". ", parts)}");
                    return string.Join(". ", parts);
                }

                // Fallback: try name field
                var nameField = fields.FirstOrDefault(f => f.Name == "name");
                if (nameField != null)
                {
                    name = nameField.GetValue(enhancerData) as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        return TextUtilities.StripRichTextTags(name) + " (Upgrade)";
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting enhancer data: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Extract description from CardUpgradeData
        /// </summary>
        internal static string ExtractCardUpgradeDescription(object upgradeData)
        {
            if (upgradeData == null) return null;

            try
            {
                var dataType = upgradeData.GetType();
                var parts = new List<string>();

                // Get upgrade title/name
                var getTitleMethod = dataType.GetMethod("GetUpgradeTitleForCardText", Type.EmptyTypes)
                                  ?? dataType.GetMethod("GetUpgradeTitle", Type.EmptyTypes)
                                  ?? dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    var title = getTitleMethod.Invoke(upgradeData, null) as string;
                    if (!string.IsNullOrEmpty(title))
                    {
                        parts.Add(TextUtilities.StripRichTextTags(title));
                    }
                }

                // Get upgrade description
                var getDescMethod = dataType.GetMethod("GetUpgradeDescription", Type.EmptyTypes)
                                 ?? dataType.GetMethod("GetDescription", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    var desc = getDescMethod.Invoke(upgradeData, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                    {
                        parts.Add(TextUtilities.StripRichTextTags(desc));
                    }
                }

                // If no description, try to extract stat bonuses
                if (parts.Count <= 1)
                {
                    var bonuses = ExtractUpgradeBonuses(upgradeData);
                    if (!string.IsNullOrEmpty(bonuses))
                    {
                        parts.Add(bonuses);
                    }
                }

                if (parts.Count > 0)
                {
                    return string.Join(". ", parts);
                }
            }
            catch { }

            return null;
        }


        /// <summary>
        /// Extract stat bonuses from CardUpgradeData
        /// </summary>
        internal static string ExtractUpgradeBonuses(object upgradeData)
        {
            if (upgradeData == null) return null;

            try
            {
                var dataType = upgradeData.GetType();
                var bonuses = new List<string>();

                // Determine if this upgrade targets units or spells via filters
                bool isUnitUpgrade = false;
                var getFiltersMethod = dataType.GetMethod("GetFilters", Type.EmptyTypes);
                if (getFiltersMethod != null)
                {
                    var filters = getFiltersMethod.Invoke(upgradeData, null) as System.Collections.IList;
                    if (filters != null)
                    {
                        foreach (var filter in filters)
                        {
                            if (filter == null) continue;
                            var cardTypeField = filter.GetType().GetField("cardType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (cardTypeField != null)
                            {
                                var cardTypeVal = cardTypeField.GetValue(filter);
                                // CardType.Monster = 0 in the game enum
                                if (cardTypeVal != null && cardTypeVal.ToString() == "Monster")
                                {
                                    isUnitUpgrade = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                // Also check HasUnitStatUpgrade() and bonusHP/bonusSize as heuristic
                if (!isUnitUpgrade)
                {
                    var hasUnitStatMethod = dataType.GetMethod("HasUnitStatUpgrade", Type.EmptyTypes);
                    if (hasUnitStatMethod != null)
                    {
                        var result = hasUnitStatMethod.Invoke(upgradeData, null);
                        if (result is bool hasUnit && hasUnit)
                        {
                            // If it has HP or size bonuses, it's likely a unit upgrade
                            var hpMethod = dataType.GetMethod("GetBonusHP", Type.EmptyTypes);
                            var sizeMethod = dataType.GetMethod("GetBonusSize", Type.EmptyTypes);
                            bool hasHP = hpMethod != null && hpMethod.Invoke(upgradeData, null) is int hp && hp != 0;
                            bool hasSize = sizeMethod != null && sizeMethod.Invoke(upgradeData, null) is int sz && sz != 0;
                            if (hasHP || hasSize)
                                isUnitUpgrade = true;
                        }
                    }
                }

                // Check common bonus methods/fields
                var getBonusDamageMethod = dataType.GetMethod("GetBonusDamage", Type.EmptyTypes);
                if (getBonusDamageMethod != null)
                {
                    var damage = getBonusDamageMethod.Invoke(upgradeData, null);
                    if (damage is int d && d != 0)
                    {
                        string label = isUnitUpgrade ? "Attack" : "Magic Power";
                        bonuses.Add($"{(d > 0 ? "+" : "")}{d} {label}");
                    }
                }

                var getBonusHPMethod = dataType.GetMethod("GetBonusHP", Type.EmptyTypes);
                if (getBonusHPMethod != null)
                {
                    var hp = getBonusHPMethod.Invoke(upgradeData, null);
                    if (hp is int h && h != 0)
                    {
                        bonuses.Add($"{(h > 0 ? "+" : "")}{h} Health");
                    }
                }

                var getCostReductionMethod = dataType.GetMethod("GetCostReduction", Type.EmptyTypes);
                if (getCostReductionMethod != null)
                {
                    var reduction = getCostReductionMethod.Invoke(upgradeData, null);
                    if (reduction is int r && r != 0)
                    {
                        // Positive costReduction = cost goes down, negative = cost goes up
                        if (r > 0)
                            bonuses.Add($"-{r} Ember cost");
                        else
                            bonuses.Add($"+{-r} Ember cost");
                    }
                }

                // Check for status effect additions (like Spikes, Armor, etc.)
                var getStatusEffectsMethod = dataType.GetMethod("GetStatusEffectUpgrades", Type.EmptyTypes);
                if (getStatusEffectsMethod != null)
                {
                    var statusEffects = getStatusEffectsMethod.Invoke(upgradeData, null) as System.Collections.IList;
                    if (statusEffects != null && statusEffects.Count > 0)
                    {
                        foreach (var statusEffect in statusEffects)
                        {
                            string effectInfo = ExtractStatusEffectUpgradeInfo(statusEffect);
                            if (!string.IsNullOrEmpty(effectInfo))
                            {
                                bonuses.Add(effectInfo);
                            }
                        }
                    }
                }

                // Also check for trigger effects that add status effects
                var getTriggerUpgradesMethod = dataType.GetMethod("GetTriggerUpgrades", Type.EmptyTypes);
                if (getTriggerUpgradesMethod != null)
                {
                    var triggerUpgrades = getTriggerUpgradesMethod.Invoke(upgradeData, null) as System.Collections.IList;
                    if (triggerUpgrades != null && triggerUpgrades.Count > 0)
                    {
                        foreach (var trigger in triggerUpgrades)
                        {
                            string triggerInfo = ExtractTriggerUpgradeInfo(trigger);
                            if (!string.IsNullOrEmpty(triggerInfo))
                            {
                                bonuses.Add(triggerInfo);
                            }
                        }
                    }
                }

                // Check for added traits
                var getTraitsMethod = dataType.GetMethod("GetTraitDataUpgradeList", Type.EmptyTypes)
                                   ?? dataType.GetMethod("GetTraitDataUpgrades", Type.EmptyTypes);
                if (getTraitsMethod != null)
                {
                    var traits = getTraitsMethod.Invoke(upgradeData, null) as System.Collections.IList;
                    if (traits != null && traits.Count > 0)
                    {
                        foreach (var trait in traits)
                        {
                            var traitType = trait.GetType();

                            // Log trait type methods once for debugging
                            MonsterTrainAccessibility.LogInfo($"Trait type: {traitType.Name}");
                            foreach (var m in traitType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                            {
                                if (m.GetParameters().Length == 0 && m.ReturnType == typeof(string))
                                {
                                    var result = m.Invoke(trait, null) as string;
                                    MonsterTrainAccessibility.LogInfo($"  {m.Name}() = {result}");
                                }
                            }

                            // Try to get localized trait name first
                            string traitName = null;

                            // Try GetTraitStateName which might return localized name
                            var getLocalizedMethod = traitType.GetMethod("GetTraitStateName", Type.EmptyTypes);
                            if (getLocalizedMethod != null)
                            {
                                traitName = getLocalizedMethod.Invoke(trait, null) as string;
                            }

                            // Fallback to GetName
                            if (string.IsNullOrEmpty(traitName))
                            {
                                var getNameMethod = traitType.GetMethod("GetName", Type.EmptyTypes);
                                if (getNameMethod != null)
                                {
                                    traitName = getNameMethod.Invoke(trait, null) as string;
                                }
                            }

                            if (!string.IsNullOrEmpty(traitName))
                            {
                                // Format trait name - strip internal prefixes
                                traitName = traitName.Replace("CardTraitState", "").Replace("CardTrait", "").Replace("State", "");
                                // Map internal names to display names
                                traitName = MapTraitToDisplayName(traitName);
                                bonuses.Add($"Gain {traitName}");
                            }
                        }
                    }
                }

                // Check for removed traits (e.g., Eternalstone removes Consume)
                var getRemoveTraitsMethod = dataType.GetMethod("GetRemoveTraitUpgrades", Type.EmptyTypes);
                if (getRemoveTraitsMethod != null)
                {
                    var removeTraits = getRemoveTraitsMethod.Invoke(upgradeData, null) as System.Collections.IList;
                    if (removeTraits != null && removeTraits.Count > 0)
                    {
                        foreach (var traitName in removeTraits)
                        {
                            string name = traitName as string;
                            if (!string.IsNullOrEmpty(name))
                            {
                                // Strip internal prefixes to get display name
                                string displayName = name.Replace("CardTrait", "").Replace("State", "");
                                displayName = MapTraitToDisplayName(displayName);
                                bonuses.Add($"Remove {displayName}");
                            }
                        }
                    }
                }

                if (bonuses.Count > 0)
                {
                    return string.Join(" and ", bonuses);
                }
            }
            catch { }

            return null;
        }


        /// <summary>
        /// Map internal trait names to player-facing display names
        /// </summary>
        internal static string MapTraitToDisplayName(string internalName)
        {
            if (string.IsNullOrEmpty(internalName)) return internalName;

            // Dictionary of internal trait names to display names
            var traitMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Juice", "Doublestack" },
                { "DoubleStack", "Doublestack" },
                { "Exhaust", "Consume" },
                { "Intrinsic", "Innate" },
                { "Retain", "Holdover" },
                { "SelfPurge", "Purge" },
                { "Freeze", "Permafrost" },
            };

            if (traitMappings.TryGetValue(internalName, out string displayName))
            {
                return displayName;
            }

            // If no mapping, return the cleaned-up internal name
            return internalName;
        }


        /// <summary>
        /// Extract info from a status effect upgrade (like +X Spikes, +X Armor)
        /// </summary>
        internal static string ExtractStatusEffectUpgradeInfo(object statusEffectUpgrade)
        {
            if (statusEffectUpgrade == null) return null;

            try
            {
                var seType = statusEffectUpgrade.GetType();
                string statusId = null;
                int stacks = 0;

                // Try to get status effect ID/name
                var getStatusIdMethod = seType.GetMethod("GetStatusId", Type.EmptyTypes);
                if (getStatusIdMethod != null)
                {
                    statusId = getStatusIdMethod.Invoke(statusEffectUpgrade, null) as string;
                }

                // Try field access
                if (string.IsNullOrEmpty(statusId))
                {
                    var statusIdField = seType.GetField("statusId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                     ?? seType.GetField("_statusId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (statusIdField != null)
                    {
                        statusId = statusIdField.GetValue(statusEffectUpgrade) as string;
                    }
                }

                // Get stack count
                var getCountMethod = seType.GetMethod("GetCount", Type.EmptyTypes)
                                  ?? seType.GetMethod("GetStacks", Type.EmptyTypes);
                if (getCountMethod != null)
                {
                    var result = getCountMethod.Invoke(statusEffectUpgrade, null);
                    if (result is int s) stacks = s;
                }

                // Try field access for stacks
                if (stacks == 0)
                {
                    var countField = seType.GetField("count", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                  ?? seType.GetField("_count", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (countField != null)
                    {
                        var result = countField.GetValue(statusEffectUpgrade);
                        if (result is int s) stacks = s;
                    }
                }

                if (!string.IsNullOrEmpty(statusId))
                {
                    // Format status name
                    string displayName = FormatStatusEffectName(statusId);
                    if (stacks > 0)
                    {
                        return $"+{stacks} {displayName}";
                    }
                    return $"Gain {displayName}";
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting status effect upgrade info: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Extract info from a trigger upgrade (abilities added to the unit)
        /// </summary>
        internal static string ExtractTriggerUpgradeInfo(object triggerUpgrade)
        {
            if (triggerUpgrade == null) return null;

            try
            {
                var triggerType = triggerUpgrade.GetType();

                // Try to get description
                var getDescMethod = triggerType.GetMethod("GetDescription", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    var desc = getDescMethod.Invoke(triggerUpgrade, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                    {
                        return TextUtilities.StripRichTextTags(desc);
                    }
                }

                // Try to get trigger type and effects
                var getTriggerTypeMethod = triggerType.GetMethod("GetTrigger", Type.EmptyTypes);
                if (getTriggerTypeMethod != null)
                {
                    var triggerTypeVal = getTriggerTypeMethod.Invoke(triggerUpgrade, null);
                    if (triggerTypeVal != null)
                    {
                        string triggerName = triggerTypeVal.ToString();
                        // Convert trigger type to readable format
                        triggerName = System.Text.RegularExpressions.Regex.Replace(triggerName, "([a-z])([A-Z])", "$1 $2");
                        return $"Gain trigger: {triggerName}";
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting trigger upgrade info: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Format a status effect ID into a readable display name
        /// </summary>
        internal static string FormatStatusEffectName(string statusId)
        {
            if (string.IsNullOrEmpty(statusId)) return statusId;

            // Common status effect mappings
            var statusMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "armor", "Armor" },
                { "damageshield", "Damage Shield" },
                { "damage_shield", "Damage Shield" },
                { "rage", "Rage" },
                { "quick", "Quick" },
                { "multistrike", "Multistrike" },
                { "regen", "Regen" },
                { "sap", "Sap" },
                { "dazed", "Dazed" },
                { "rooted", "Rooted" },
                { "frostbite", "Frostbite" },
                { "spikes", "Spikes" },
                { "spike", "Spikes" },
                { "lifesteal", "Lifesteal" },
                { "stealth", "Stealth" },
                { "burnout", "Burnout" },
                { "endless", "Endless" },
                { "fragile", "Fragile" },
                { "heartless", "Heartless" },
                { "spellweakness", "Spell Weakness" },
                { "spell_weakness", "Spell Weakness" },
                { "meleeweakness", "Melee Weakness" },
                { "melee_weakness", "Melee Weakness" },
            };

            if (statusMappings.TryGetValue(statusId, out string displayName))
            {
                return displayName;
            }

            // Convert camelCase or snake_case to Title Case
            statusId = statusId.Replace("_", " ");
            statusId = System.Text.RegularExpressions.Regex.Replace(statusId, "([a-z])([A-Z])", "$1 $2");

            // Capitalize first letter of each word
            var words = statusId.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }

            return string.Join(" ", words);
        }


        /// <summary>
        /// Get price from the BuyButton component.
        /// Searches parents (for when go is BuyButton's child), children (for when go is MerchantGoodUI parent),
        /// and the MerchantGoodUIBase.buyButton field directly.
        /// </summary>
        internal static string GetPriceFromBuyButton(GameObject go)
        {
            try
            {
                // Find BuyButton in parents (handles case where focused object is a child of BuyButton)
                Component buyButton = UITextHelper.FindComponentInHierarchy(go, "BuyButton");

                // If not found in parents, search children (handles case where go is MerchantGoodDetailsUI)
                if (buyButton == null)
                {
                    foreach (var comp in go.GetComponentsInChildren<Component>())
                    {
                        if (comp != null && comp.GetType().Name == "BuyButton")
                        {
                            buyButton = comp;
                            break;
                        }
                    }
                }

                // Also try reading buyButton field from MerchantGoodUIBase in parents
                if (buyButton == null)
                {
                    var merchantGood = UITextHelper.FindComponentInHierarchy(go, "MerchantGoodDetailsUI")
                                   ?? UITextHelper.FindComponentInHierarchy(go, "MerchantGoodUIBase");
                    if (merchantGood != null)
                    {
                        var buyField = merchantGood.GetType().GetField("buyButton",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        buyButton = buyField?.GetValue(merchantGood) as Component;
                    }
                }

                if (buyButton != null)
                {
                    var btnType = buyButton.GetType();
                    var fields = btnType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    // Check for crystal currency (alternateText is set when currency is crystals)
                    var altTextField = fields.FirstOrDefault(f => f.Name == "alternateText");
                    if (altTextField != null)
                    {
                        var altText = altTextField.GetValue(buyButton) as string;
                        if (!string.IsNullOrEmpty(altText))
                        {
                            // alternateText contains the localized crystal cost string
                            string cleanText = TextUtilities.StripRichTextTags(altText);
                            if (!string.IsNullOrEmpty(cleanText))
                                return cleanText;
                        }
                    }

                    // Look for gold cost field
                    var costField = fields.FirstOrDefault(f => f.Name == "cost");
                    if (costField != null)
                    {
                        var costValue = costField.GetValue(buyButton);
                        if (costValue is int cost && cost > 0)
                        {
                            return $"{cost} gold";
                        }
                    }

                    // Fallback: read the label TMP text directly (has the formatted price)
                    var labelField = fields.FirstOrDefault(f => f.Name == "label");
                    if (labelField != null)
                    {
                        var label = labelField.GetValue(buyButton);
                        if (label != null)
                        {
                            var textProp = label.GetType().GetProperty("text");
                            if (textProp != null)
                            {
                                string labelText = textProp.GetValue(label) as string;
                                if (!string.IsNullOrEmpty(labelText))
                                {
                                    string cleanLabel = TextUtilities.StripRichTextTags(labelText);
                                    if (!string.IsNullOrEmpty(cleanLabel))
                                        return cleanLabel;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }


        /// <summary>
        /// Extract info from MerchantServiceUI (upgrade/service)
        /// </summary>
        internal static string ExtractMerchantServiceInfo(Component serviceUI)
        {
            try
            {
                var uiType = serviceUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var methods = uiType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                // Log the GameObject hierarchy - the name often contains the service type
                var go = serviceUI.gameObject;
                string hierarchyPath = go.name;
                var parent = go.transform.parent;
                while (parent != null)
                {
                    hierarchyPath = parent.name + "/" + hierarchyPath;
                    parent = parent.parent;
                    if (hierarchyPath.Length > 200) break; // Safety limit
                }
                MonsterTrainAccessibility.LogInfo($"MerchantServiceUI hierarchy: {hierarchyPath}");
                MonsterTrainAccessibility.LogInfo($"MerchantServiceUI fields: {string.Join(", ", fields.Select(f => f.Name).Take(20))}");

                // Log all components on this GameObject
                var components = go.GetComponents<Component>();
                MonsterTrainAccessibility.LogInfo($"Components on GO: {string.Join(", ", components.Select(c => c?.GetType().Name ?? "null"))}");

                string serviceName = null;
                string serviceDesc = null;

                // Priority 0: Check if GameObject name contains service type
                string goName = go.name.ToLower();
                if (goName.Contains("reroll"))
                {
                    serviceName = "Reroll";
                }
                else if (goName.Contains("purge") || goName.Contains("remove"))
                {
                    serviceName = "Purge Card";
                }
                else if (goName.Contains("duplicate") || goName.Contains("copy"))
                {
                    serviceName = "Duplicate Card";
                }
                else if (goName.Contains("upgrade") || goName.Contains("enhance"))
                {
                    serviceName = "Upgrade Card";
                }
                else if (goName.Contains("heal") || goName.Contains("repair"))
                {
                    serviceName = "Heal";
                }
                else if (goName.Contains("unleash"))
                {
                    serviceName = "Unleash";
                }

                if (!string.IsNullOrEmpty(serviceName))
                {
                    MonsterTrainAccessibility.LogInfo($"Got service name from GO name: {serviceName}");
                }

                // Priority 1: Extract service index from GO name and get data from MerchantScreen
                if (string.IsNullOrEmpty(serviceName))
                {
                    // Parse service sign index from name like "Service sign 1", "Service sign 2"
                    int serviceIndex = -1;
                    var match = System.Text.RegularExpressions.Regex.Match(go.name, @"Service sign (\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        serviceIndex = int.Parse(match.Groups[1].Value) - 1; // Convert to 0-based
                        MonsterTrainAccessibility.LogInfo($"Service sign index: {serviceIndex}");
                    }

                    // Find MerchantScreen or MerchantScreenContent parent and get services list
                    var parentTransform = go.transform.parent;
                    while (parentTransform != null && string.IsNullOrEmpty(serviceName))
                    {
                        var parentGO = parentTransform.gameObject;
                        var parentComponents = parentGO.GetComponents<Component>();

                        foreach (var comp in parentComponents)
                        {
                            if (comp == null) continue;
                            var compType = comp.GetType();
                            var compName = compType.Name;

                            // Look for MerchantScreen or MerchantScreenContent
                            if (compName == "MerchantScreen" || compName == "MerchantScreenContent")
                            {
                                MonsterTrainAccessibility.LogInfo($"Found parent component: {compType.Name}");

                                // Look for services list/array field
                                var compFields = compType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                MonsterTrainAccessibility.LogInfo($"{compName} fields: {string.Join(", ", compFields.Select(f => f.Name).Take(20))}");

                                // First check sourceMerchantData which should contain the actual service definitions
                                var merchantDataField = compFields.FirstOrDefault(f => f.Name == "sourceMerchantData");
                                if (merchantDataField != null)
                                {
                                    var merchantData = merchantDataField.GetValue(comp);
                                    if (merchantData != null)
                                    {
                                        var mdType = merchantData.GetType();
                                        MonsterTrainAccessibility.LogInfo($"sourceMerchantData type: {mdType.Name}");

                                        // Log all fields on merchant data
                                        var mdFields = mdType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                        MonsterTrainAccessibility.LogInfo($"MerchantData fields: {string.Join(", ", mdFields.Select(f => f.Name).Take(20))}");

                                        // Look for services list in merchant data
                                        foreach (var mdField in mdFields)
                                        {
                                            string mdFieldName = mdField.Name.ToLower();
                                            if (mdFieldName.Contains("service"))
                                            {
                                                var servicesValue = mdField.GetValue(merchantData);
                                                if (servicesValue != null)
                                                {
                                                    MonsterTrainAccessibility.LogInfo($"Found {mdField.Name}: {servicesValue.GetType().Name}");

                                                    if (servicesValue is System.Collections.IList servicesList && serviceIndex >= 0 && serviceIndex < servicesList.Count)
                                                    {
                                                        var svcData = servicesList[serviceIndex];
                                                        if (svcData != null)
                                                        {
                                                            var svcType = svcData.GetType();
                                                            MonsterTrainAccessibility.LogInfo($"Service[{serviceIndex}] type: {svcType.Name}");

                                                            // Log service data fields
                                                            var svcFields = svcType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                                            MonsterTrainAccessibility.LogInfo($"Service fields: {string.Join(", ", svcFields.Select(f => f.Name).Take(15))}");

                                                            var getNameMethod = svcType.GetMethod("GetName", Type.EmptyTypes);
                                                            if (getNameMethod != null)
                                                            {
                                                                serviceName = getNameMethod.Invoke(svcData, null) as string;
                                                                MonsterTrainAccessibility.LogInfo($"Service name from GetName(): {serviceName}");
                                                            }

                                                            // Try GetDescription
                                                            var getDescMethod = svcType.GetMethod("GetDescription", Type.EmptyTypes) ??
                                                                               svcType.GetMethod("GetTooltipDescription", Type.EmptyTypes);
                                                            if (getDescMethod != null)
                                                            {
                                                                serviceDesc = getDescMethod.Invoke(svcData, null) as string;
                                                            }

                                                            if (!string.IsNullOrEmpty(serviceName)) break;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                if (!string.IsNullOrEmpty(serviceName)) break;

                                foreach (var field in compFields)
                                {
                                    string fieldName = field.Name.ToLower();
                                    var value = field.GetValue(comp);
                                    if (value == null) continue;

                                    // Look for services list
                                    if (fieldName.Contains("service"))
                                    {
                                        MonsterTrainAccessibility.LogInfo($"Found field {field.Name}: {value.GetType().Name}");

                                        // If it's a list/array, try to get item by index
                                        if (value is System.Collections.IList list && serviceIndex >= 0 && serviceIndex < list.Count)
                                        {
                                            var serviceData = list[serviceIndex];
                                            if (serviceData != null)
                                            {
                                                var dataType = serviceData.GetType();
                                                MonsterTrainAccessibility.LogInfo($"Service data type: {dataType.Name}");

                                                var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                                                if (getNameMethod != null)
                                                {
                                                    serviceName = getNameMethod.Invoke(serviceData, null) as string;
                                                    MonsterTrainAccessibility.LogInfo($"Service name from list[{serviceIndex}]: {serviceName}");
                                                }

                                                var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes) ??
                                                                   dataType.GetMethod("GetTooltipDescription", Type.EmptyTypes);
                                                if (getDescMethod != null)
                                                {
                                                    serviceDesc = getDescMethod.Invoke(serviceData, null) as string;
                                                }

                                                if (!string.IsNullOrEmpty(serviceName)) break;
                                            }
                                        }

                                        // If it's a single service data, try GetName
                                        var valueType = value.GetType();
                                        var nameMethod = valueType.GetMethod("GetName", Type.EmptyTypes);
                                        if (nameMethod != null)
                                        {
                                            serviceName = nameMethod.Invoke(value, null) as string;
                                            if (!string.IsNullOrEmpty(serviceName))
                                            {
                                                MonsterTrainAccessibility.LogInfo($"Service name from {field.Name}: {serviceName}");
                                                break;
                                            }
                                        }
                                    }
                                }

                                if (!string.IsNullOrEmpty(serviceName)) break;
                            }
                        }

                        parentTransform = parentTransform.parent;
                    }
                }

                // Priority 2: Look for service data via properties on MerchantServiceUI
                if (string.IsNullOrEmpty(serviceName))
                {
                    // Check all properties on MerchantServiceUI
                    var props = uiType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    MonsterTrainAccessibility.LogInfo($"MerchantServiceUI properties: {string.Join(", ", props.Select(p => p.Name).Take(15))}");

                    // Check GoodState property specifically - this likely contains the service data
                    var goodStateProp = props.FirstOrDefault(p => p.Name == "GoodState");
                    if (goodStateProp != null)
                    {
                        try
                        {
                            var goodState = goodStateProp.GetValue(serviceUI);
                            if (goodState != null)
                            {
                                var gsType = goodState.GetType();
                                MonsterTrainAccessibility.LogInfo($"GoodState type: {gsType.Name}");

                                // Log GoodState fields
                                var gsFields = gsType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                MonsterTrainAccessibility.LogInfo($"GoodState fields: {string.Join(", ", gsFields.Select(f => f.Name).Take(15))}");

                                // Log GoodState properties
                                var gsProps = gsType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                MonsterTrainAccessibility.LogInfo($"GoodState properties: {string.Join(", ", gsProps.Select(p => p.Name).Take(15))}");

                                // Check RewardData property - this should have the actual service info
                                var rewardDataProp = gsProps.FirstOrDefault(p => p.Name == "RewardData");
                                if (rewardDataProp != null)
                                {
                                    try
                                    {
                                        var rewardData = rewardDataProp.GetValue(goodState);
                                        if (rewardData != null)
                                        {
                                            var rdType = rewardData.GetType();
                                            MonsterTrainAccessibility.LogInfo($"RewardData type: {rdType.Name}");

                                            // Map RewardData type name to friendly service name and description
                                            (serviceName, serviceDesc) = rdType.Name switch
                                            {
                                                "PurgeRewardData" => ("Purge Card", "Remove a card from your deck"),
                                                "RerollMerchantRewardData" => ("Re-roll", "Randomize and refresh the offered goods"),
                                                "DuplicateRewardData" => ("Duplicate Card", "Create a copy of a card in your deck"),
                                                "HealRewardData" => ("Heal", "Restore health to your Pyre"),
                                                "TrainRepairRewardData" => ("Train Repair", "Repair your train"),
                                                "UnleashRewardData" => ("Unleash", "Choose a Branded unit and unleash its power"),
                                                "UpgradeRewardData" => ("Upgrade", "Upgrade a card"),
                                                "EnhancerRewardData" => ("Upgrade Stone", null),
                                                _ => (null, null)
                                            };

                                            if (!string.IsNullOrEmpty(serviceName))
                                            {
                                                MonsterTrainAccessibility.LogInfo($"Service name from type mapping: {serviceName}");
                                            }

                                            // If mapping didn't work, try GetName method
                                            if (string.IsNullOrEmpty(serviceName))
                                            {
                                                var getNameMethod = rdType.GetMethod("GetName", Type.EmptyTypes);
                                                if (getNameMethod != null)
                                                {
                                                    serviceName = getNameMethod.Invoke(rewardData, null) as string;
                                                    MonsterTrainAccessibility.LogInfo($"Service name from RewardData.GetName(): {serviceName}");
                                                }
                                            }

                                            // Only try to get description from game if we don't have one from mapping
                                            if (string.IsNullOrEmpty(serviceDesc))
                                            {
                                                // Try various methods for getting the description
                                                var rdMethods = rdType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                                                var descMethodNames = new[] { "GetDescription", "GetTooltipDescription", "GetRewardDescription", "GetLocalizedDescription" };

                                                foreach (var methodName in descMethodNames)
                                                {
                                                    var descMethod = rdMethods.FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == 0);
                                                    if (descMethod != null)
                                                    {
                                                        try
                                                        {
                                                            var desc = descMethod.Invoke(rewardData, null) as string;
                                                            if (!string.IsNullOrEmpty(desc) && !desc.Contains("__") && !desc.Contains("-v2"))
                                                            {
                                                                serviceDesc = desc;
                                                                MonsterTrainAccessibility.LogInfo($"Got description from {methodName}: {serviceDesc}");
                                                                break;
                                                            }
                                                        }
                                                        catch { }
                                                    }
                                                }
                                            }

                                            // Don't use description if it looks like a raw localization key
                                            if (!string.IsNullOrEmpty(serviceDesc) && (serviceDesc.Contains("__") || serviceDesc.Contains("-v2") || serviceDesc.StartsWith("$")))
                                            {
                                                serviceDesc = null;
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        MonsterTrainAccessibility.LogError($"Error reading RewardData: {ex.Message}");
                                    }
                                }

                                // Try GetName method on GoodState itself
                                if (string.IsNullOrEmpty(serviceName))
                                {
                                    var getNameMethod = gsType.GetMethod("GetName", Type.EmptyTypes);
                                    if (getNameMethod != null)
                                    {
                                        serviceName = getNameMethod.Invoke(goodState, null) as string;
                                        MonsterTrainAccessibility.LogInfo($"Service name from GoodState.GetName(): {serviceName}");
                                    }
                                }

                                // Try GetDescription method
                                if (string.IsNullOrEmpty(serviceDesc))
                                {
                                    var getDescMethod = gsType.GetMethod("GetDescription", Type.EmptyTypes) ??
                                                       gsType.GetMethod("GetTooltipDescription", Type.EmptyTypes);
                                    if (getDescMethod != null)
                                    {
                                        serviceDesc = getDescMethod.Invoke(goodState, null) as string;
                                        MonsterTrainAccessibility.LogInfo($"Service desc from GoodState: {serviceDesc}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MonsterTrainAccessibility.LogError($"Error reading GoodState: {ex.Message}");
                        }
                    }

                    foreach (var prop in props)
                    {
                        string propName = prop.Name.ToLower();
                        if (propName.Contains("service") || propName.Contains("data"))
                        {
                            try
                            {
                                var value = prop.GetValue(serviceUI);
                                if (value != null)
                                {
                                    MonsterTrainAccessibility.LogInfo($"Property {prop.Name}: {value.GetType().Name}");

                                    var valueType = value.GetType();
                                    var getNameMethod = valueType.GetMethod("GetName", Type.EmptyTypes);
                                    if (getNameMethod != null)
                                    {
                                        serviceName = getNameMethod.Invoke(value, null) as string;
                                        MonsterTrainAccessibility.LogInfo($"Service name from property: {serviceName}");
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    // Check all methods for GetServiceData or similar
                    var allMethods = uiType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var getDataMethods = allMethods.Where(m =>
                        (m.Name.Contains("Service") || m.Name.Contains("Data")) &&
                        m.GetParameters().Length == 0 &&
                        m.ReturnType != typeof(void)).Take(5);

                    foreach (var method in getDataMethods)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found method: {method.Name} returns {method.ReturnType.Name}");
                        try
                        {
                            var result = method.Invoke(serviceUI, null);
                            if (result != null)
                            {
                                MonsterTrainAccessibility.LogInfo($"Method {method.Name} returned: {result.GetType().Name}");

                                var resultType = result.GetType();
                                var getNameMethod = resultType.GetMethod("GetName", Type.EmptyTypes);
                                if (getNameMethod != null)
                                {
                                    serviceName = getNameMethod.Invoke(result, null) as string;
                                    MonsterTrainAccessibility.LogInfo($"Service name from method result: {serviceName}");
                                    if (!string.IsNullOrEmpty(serviceName)) break;
                                }
                            }
                        }
                        catch { }
                    }
                }

                // Priority 2: Search all fields for data objects
                if (string.IsNullOrEmpty(serviceName))
                {
                    foreach (var field in fields)
                    {
                        string fieldName = field.Name.ToLower();
                        var value = field.GetValue(serviceUI);
                        if (value == null) continue;

                        // Check for service/data objects
                        if (fieldName.Contains("service") || fieldName.Contains("data"))
                        {
                            MonsterTrainAccessibility.LogInfo($"Checking field {field.Name}: {value.GetType().Name}");

                            // Try to get name/description from data object
                            var dataType = value.GetType();

                            var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                            if (getNameMethod != null)
                            {
                                serviceName = getNameMethod.Invoke(value, null) as string;
                                MonsterTrainAccessibility.LogInfo($"Service name from {field.Name}: {serviceName}");
                            }

                            var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes) ??
                                               dataType.GetMethod("GetTooltipDescription", Type.EmptyTypes);
                            if (getDescMethod != null)
                            {
                                serviceDesc = getDescMethod.Invoke(value, null) as string;
                            }

                            if (!string.IsNullOrEmpty(serviceName))
                                break;
                        }
                    }
                }

                // Priority 3: Try methods on the UI component itself
                if (string.IsNullOrEmpty(serviceName))
                {
                    var getNameMethod = methods.FirstOrDefault(m =>
                        m.Name == "GetServiceName" || m.Name == "GetName");
                    if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                    {
                        serviceName = getNameMethod.Invoke(serviceUI, null) as string;
                        MonsterTrainAccessibility.LogInfo($"Service name from method: {serviceName}");
                    }
                }

                // Priority 4: Search child transforms directly for text
                if (string.IsNullOrEmpty(serviceName))
                {
                    var serviceGO = serviceUI.gameObject;

                    // Log all immediate children
                    var childNames = new List<string>();
                    for (int i = 0; i < serviceGO.transform.childCount; i++)
                    {
                        var child = serviceGO.transform.GetChild(i);
                        childNames.Add(child.name);

                        // Try to get text from each immediate child
                        string childText = UITextHelper.GetTMPTextDirect(child.gameObject);
                        if (!string.IsNullOrEmpty(childText))
                        {
                            MonsterTrainAccessibility.LogInfo($"Found text in child '{child.name}': {childText}");
                        }

                        // Also check grandchildren
                        for (int j = 0; j < child.childCount; j++)
                        {
                            var grandchild = child.GetChild(j);
                            string gcText = UITextHelper.GetTMPTextDirect(grandchild.gameObject);
                            if (!string.IsNullOrEmpty(gcText))
                            {
                                MonsterTrainAccessibility.LogInfo($"Found text in grandchild '{child.name}/{grandchild.name}': {gcText}");
                            }
                        }
                    }
                    MonsterTrainAccessibility.LogInfo($"MerchantServiceUI children: {string.Join(", ", childNames)}");

                    // Look for specific named children that might contain the title
                    var titleChildNames = new[] { "Title", "TitleLabel", "Name", "ServiceName", "TitleText", "Label", "Text" };
                    foreach (var childName in titleChildNames)
                    {
                        var titleChild = serviceGO.transform.Find(childName);
                        if (titleChild != null)
                        {
                            serviceName = UITextHelper.GetTMPTextDirect(titleChild.gameObject);
                            if (!string.IsNullOrEmpty(serviceName))
                            {
                                MonsterTrainAccessibility.LogInfo($"Got service name from child '{childName}': {serviceName}");
                                break;
                            }
                        }
                    }

                    // If still not found, get all text from children
                    if (string.IsNullOrEmpty(serviceName))
                    {
                        var childTexts = BattleIntroTextReader.GetAllTextFromChildren(serviceGO);
                        MonsterTrainAccessibility.LogInfo($"Child texts found: {string.Join(", ", childTexts.Take(5))}");

                        if (childTexts.Count > 0)
                        {
                            serviceName = childTexts[0];
                            MonsterTrainAccessibility.LogInfo($"Got service name from children: {serviceName}");

                            if (childTexts.Count > 1)
                            {
                                serviceDesc = childTexts[1];
                            }
                        }
                    }
                }

                // Priority 5: Try titleLabel and descriptionLabel fields as last resort
                if (string.IsNullOrEmpty(serviceName))
                {
                    var titleLabelField = fields.FirstOrDefault(f => f.Name == "titleLabel");
                    var descLabelField = fields.FirstOrDefault(f => f.Name == "descriptionLabel");

                    if (titleLabelField != null)
                    {
                        var titleLabel = titleLabelField.GetValue(serviceUI);
                        if (titleLabel != null)
                        {
                            serviceName = BattleIntroTextReader.GetTextFromComponent(titleLabel);
                            MonsterTrainAccessibility.LogInfo($"Got title from titleLabel field: {serviceName}");
                        }
                    }

                    if (descLabelField != null)
                    {
                        var descLabel = descLabelField.GetValue(serviceUI);
                        if (descLabel != null)
                        {
                            serviceDesc = BattleIntroTextReader.GetTextFromComponent(descLabel);
                        }
                    }
                }

                // Priority 6: Check for text/label fields (as strings)
                if (string.IsNullOrEmpty(serviceName))
                {
                    foreach (var field in fields)
                    {
                        string fieldName = field.Name.ToLower();
                        var value = field.GetValue(serviceUI);

                        if (value is string str && !string.IsNullOrEmpty(str))
                        {
                            if (fieldName.Contains("name") || fieldName.Contains("title"))
                            {
                                serviceName = str;
                                MonsterTrainAccessibility.LogInfo($"Got service name from string field {field.Name}: {serviceName}");
                            }
                            else if (fieldName.Contains("desc"))
                            {
                                serviceDesc = str;
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(serviceName))
                {
                    serviceName = TextUtilities.StripRichTextTags(serviceName);
                    string price = GetShopItemPrice(serviceUI);

                    List<string> parts = new List<string> { serviceName };

                    // Price right after name so it's heard early
                    if (!string.IsNullOrEmpty(price))
                    {
                        parts.Add(price);
                    }

                    if (!string.IsNullOrEmpty(serviceDesc) && serviceDesc != serviceName)
                    {
                        parts.Add(TextUtilities.StripRichTextTags(serviceDesc));
                    }

                    return string.Join(". ", parts);
                }

                MonsterTrainAccessibility.LogWarning("Could not extract service name from MerchantServiceUI");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting merchant service info: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Get price from a shop item component
        /// </summary>
        internal static string GetShopItemPrice(Component shopItem)
        {
            try
            {
                var itemType = shopItem.GetType();

                // Try GoodState.Cost property first (works for MerchantServiceUI and MerchantGoodDetailsUI)
                var goodStateProp = itemType.GetProperty("GoodState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (goodStateProp != null)
                {
                    var goodState = goodStateProp.GetValue(shopItem);
                    if (goodState != null)
                    {
                        var costProp = goodState.GetType().GetProperty("Cost", BindingFlags.Public | BindingFlags.Instance);
                        if (costProp != null)
                        {
                            var costVal = costProp.GetValue(goodState);
                            if (costVal is int cost && cost > 0)
                            {
                                return $"{cost} gold";
                            }
                        }
                    }
                }

                var fields = itemType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (var field in fields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("price") || fieldName.Contains("cost") || fieldName.Contains("gold"))
                    {
                        var value = field.GetValue(shopItem);
                        if (value is int intPrice && intPrice > 0)
                        {
                            return $"{intPrice} gold";
                        }
                    }
                }

                // Try methods
                var methods = itemType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                var getPriceMethod = methods.FirstOrDefault(m =>
                    (m.Name == "GetPrice" || m.Name == "GetCost" || m.Name == "GetGoldCost") &&
                    m.GetParameters().Length == 0);

                if (getPriceMethod != null)
                {
                    var result = getPriceMethod.Invoke(shopItem, null);
                    if (result is int price && price > 0)
                    {
                        return $"{price} gold";
                    }
                }
            }
            catch { }

            return null;
        }


        /// <summary>
        /// Extract card info from CardState or CardData
        /// </summary>
        internal static string ExtractCardInfo(object cardObj)
        {
            if (cardObj == null) return null;

            try
            {
                var cardType = cardObj.GetType();

                // If this is CardState, get CardData first
                object cardData = cardObj;
                if (cardType.Name == "CardState")
                {
                    var getDataMethod = cardType.GetMethod("GetCardDataRead", Type.EmptyTypes);
                    if (getDataMethod != null)
                    {
                        cardData = getDataMethod.Invoke(cardObj, null);
                        if (cardData == null) return null;
                    }
                }

                var dataType = cardData.GetType();
                string name = null;
                string description = null;
                int cost = -1;

                // Get name
                var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    name = getNameMethod.Invoke(cardData, null) as string;
                }

                // Get description
                var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    description = getDescMethod.Invoke(cardData, null) as string;
                }

                // Get cost
                var getCostMethod = dataType.GetMethod("GetCost", Type.EmptyTypes);
                if (getCostMethod != null)
                {
                    var costResult = getCostMethod.Invoke(cardData, null);
                    if (costResult is int c)
                        cost = c;
                }

                if (!string.IsNullOrEmpty(name))
                {
                    List<string> parts = new List<string>();
                    parts.Add(TextUtilities.StripRichTextTags(name));

                    if (cost >= 0)
                    {
                        parts.Add($"{cost} ember");
                    }

                    if (!string.IsNullOrEmpty(description))
                    {
                        parts.Add(TextUtilities.StripRichTextTags(description));
                    }

                    // Add keyword definitions
                    string keywords = CardTextReader.GetCardKeywordTooltips(cardObj, cardData, description);
                    if (!string.IsNullOrEmpty(keywords))
                    {
                        parts.Add($"Keywords: {keywords}");
                    }

                    return string.Join(". ", parts);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting card info: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Extract info from a generic shop item
        /// </summary>
        internal static string ExtractShopItemInfo(object item)
        {
            if (item == null) return null;

            try
            {
                var itemType = item.GetType();

                // Try GetName
                var getNameMethod = itemType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(item, null) as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        return TextUtilities.StripRichTextTags(name);
                    }
                }

                // Try to find card data inside
                var fields = itemType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("card") || fieldName.Contains("data"))
                    {
                        var value = field.GetValue(item);
                        if (value != null)
                        {
                            string info = ExtractCardInfo(value);
                            if (!string.IsNullOrEmpty(info))
                                return info;
                        }
                    }
                }
            }
            catch { }

            return null;
        }


        /// <summary>
        /// Extract the name from a RewardData object
        /// </summary>
        internal static string GetRewardName(object rewardData)
        {
            if (rewardData == null) return null;

            try
            {
                var rewardType = rewardData.GetType();
                string typeName = rewardType.Name;

                // Special handling for GoldRewardData - extract the gold amount
                if (typeName == "GoldRewardData" || typeName.Contains("Gold"))
                {
                    var amountField = rewardType.GetField("_amount", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    if (amountField != null)
                    {
                        var amount = amountField.GetValue(rewardData);
                        if (amount is int goldAmount && goldAmount > 0)
                        {
                            return $"{goldAmount} Gold";
                        }
                    }
                    // Try GetAmount method
                    var getAmountMethod = rewardType.GetMethod("GetAmount", Type.EmptyTypes);
                    if (getAmountMethod != null)
                    {
                        var amount = getAmountMethod.Invoke(rewardData, null);
                        if (amount is int goldAmount && goldAmount > 0)
                        {
                            return $"{goldAmount} Gold";
                        }
                    }
                }

                // Try GetTitle method first (if it exists)
                var getTitleMethod = rewardType.GetMethod("GetTitle");
                if (getTitleMethod != null)
                {
                    var title = getTitleMethod.Invoke(rewardData, null) as string;
                    // Only use if it looks like a real name (not a key)
                    if (!string.IsNullOrEmpty(title) && !title.Contains("_") && !title.Contains("-"))
                        return title;
                }

                // Try to get the title key and localize it
                var titleKeyField = rewardType.GetField("_rewardTitleKey", BindingFlags.NonPublic | BindingFlags.Instance);
                if (titleKeyField != null)
                {
                    var titleKey = titleKeyField.GetValue(rewardData) as string;
                    if (!string.IsNullOrEmpty(titleKey))
                    {
                        // Try to localize the key
                        string localized = LocalizationHelper.LocalizeOrNull(titleKey);
                        // Only use if localization succeeded (not same as key and looks like real text)
                        if (!string.IsNullOrEmpty(localized) && localized != titleKey && !localized.Contains("-"))
                            return localized;
                    }
                }

                // Fall back to type name - this is the most reliable approach
                return GetRewardTypeDisplayName(rewardType);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting reward name: {ex.Message}");
                return "Reward";
            }
        }


        /// <summary>
        /// Get a human-readable display name from the reward type
        /// </summary>
        internal static string GetRewardTypeDisplayName(Type rewardType)
        {
            string typeName = rewardType.Name;
            if (typeName.EndsWith("RewardData"))
                typeName = typeName.Substring(0, typeName.Length - "RewardData".Length);

            // Convert type name to readable format
            switch (typeName)
            {
                case "RelicPool": return "Random Artifact";
                case "Relic": return "Artifact";
                case "CardPool": return "Random Card";
                case "Card": return "Card";
                case "Gold": return "Gold";
                case "Health": return "Pyre Health";
                case "Crystal": return "Crystal";
                case "EnhancerPool": return "Random Upgrade";
                case "Enhancer": return "Upgrade";
                case "Draft": return "Card Draft";
                case "RelicDraft": return "Artifact Choice";
                default: return typeName;
            }
        }

    }
}
