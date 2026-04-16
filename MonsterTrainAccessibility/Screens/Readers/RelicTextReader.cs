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
    /// Extracted reader for Relic UI elements.
    /// </summary>
    internal static class RelicTextReader
    {

        /// <summary>
        /// Get text for RelicInfoUI (artifact selection on RelicDraftScreen)
        /// </summary>
        internal static string GetRelicInfoText(GameObject go)
        {
            try
            {
                // Check if this has a RelicInfoUI component
                Component relicInfoUI = null;
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    if (component.GetType().Name == "RelicInfoUI")
                    {
                        relicInfoUI = component;
                        break;
                    }
                }

                if (relicInfoUI == null)
                    return null;

                var relicType = relicInfoUI.GetType();
                MonsterTrainAccessibility.LogInfo($"Found RelicInfoUI, extracting relic data...");

                // Log all fields first to see what's available
                var sbFields = new StringBuilder();
                sbFields.AppendLine($"=== Fields on RelicInfoUI ===");
                foreach (var field in relicType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    try
                    {
                        var value = field.GetValue(relicInfoUI);
                        string valueStr = value?.ToString() ?? "null";
                        if (valueStr.Length > 80) valueStr = valueStr.Substring(0, 80) + "...";
                        sbFields.AppendLine($"  {field.FieldType.Name} {field.Name} = {valueStr}");
                    }
                    catch { }
                }
                MonsterTrainAccessibility.LogInfo(sbFields.ToString());

                // Try to get RelicData from the backing field (C# auto-property)
                string relicName = null;
                string relicDescription = null;

                // Access <relicData>k__BackingField - the backing field for the relicData property
                var backingField = relicType.GetField("<relicData>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField != null)
                {
                    var relicData = backingField.GetValue(relicInfoUI);
                    if (relicData != null)
                    {
                        var dataType = relicData.GetType();
                        MonsterTrainAccessibility.LogInfo($"Found RelicData: {dataType.Name}");

                        // Log available methods to find description
                        var methods = dataType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                        var descMethods = methods.Where(m => m.Name.ToLower().Contains("desc") || m.Name.ToLower().Contains("effect") || m.Name.ToLower().Contains("text")).ToList();
                        MonsterTrainAccessibility.LogInfo($"Potential description methods: {string.Join(", ", descMethods.Select(m => m.Name))}");

                        // Try GetName()
                        var getNameMethod = dataType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                        if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                        {
                            relicName = getNameMethod.Invoke(relicData, null) as string;
                            MonsterTrainAccessibility.LogInfo($"GetName() returned: '{relicName}'");
                        }

                        // Try various description method names
                        string[] descMethodNames = { "GetDescription", "GetEffectText", "GetDescriptionText", "GetRelicEffectText", "GetEffectDescription" };
                        foreach (var methodName in descMethodNames)
                        {
                            var method = dataType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                            if (method != null && method.GetParameters().Length == 0)
                            {
                                relicDescription = method.Invoke(relicData, null) as string;
                                if (!string.IsNullOrEmpty(relicDescription))
                                {
                                    MonsterTrainAccessibility.LogInfo($"{methodName}() returned: '{relicDescription}'");
                                    break;
                                }
                            }
                        }

                        // If still no description, create a RelicState to get the description
                        // with numeric parameters filled in (raw localization misses these)
                        if (string.IsNullOrEmpty(relicDescription))
                        {
                            relicDescription = SettingsTextReader.GetRelicDescription(relicData);
                            MonsterTrainAccessibility.LogInfo($"GetRelicDescription returned: '{relicDescription}'");
                        }

                        // Resolve effect placeholders like {[effect0.power]} or {[#effect0.power]}
                        if (!string.IsNullOrEmpty(relicDescription) && relicDescription.Contains("{["))
                        {
                            relicDescription = ResolveRelicEffectPlaceholders(relicDescription, relicData, dataType);
                        }
                    }
                }

                // If description looks like a localization key, try getting it from RelicState instead
                if (!string.IsNullOrEmpty(relicDescription) && relicDescription.Contains("_descriptionKey"))
                {
                    MonsterTrainAccessibility.LogInfo("Description is a loc key, trying RelicState...");
                    relicDescription = null; // Clear it, will try relicState
                }

                // Try relicState for name and/or description if we don't have them yet
                if (string.IsNullOrEmpty(relicName) || string.IsNullOrEmpty(relicDescription))
                {
                    var relicStateField = relicType.GetField("relicState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (relicStateField != null)
                    {
                        var relicState = relicStateField.GetValue(relicInfoUI);
                        if (relicState != null)
                        {
                            var stateType = relicState.GetType();

                            // Log available methods on RelicState
                            var stateMethods = stateType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                            var descStateMethods = stateMethods.Where(m => m.Name.ToLower().Contains("desc") || m.Name.ToLower().Contains("effect") || m.Name.ToLower().Contains("text") || m.Name.ToLower().Contains("name")).ToList();
                            MonsterTrainAccessibility.LogInfo($"RelicState methods: {string.Join(", ", descStateMethods.Select(m => m.Name))}");

                            // Try to get name from RelicState if we don't have it
                            if (string.IsNullOrEmpty(relicName))
                            {
                                // Try GetName method
                                var getNameMethod = stateType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                                if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                                {
                                    relicName = getNameMethod.Invoke(relicState, null) as string;
                                    MonsterTrainAccessibility.LogInfo($"RelicState.GetName() returned: '{relicName}'");
                                }

                                // Try to get RelicData from RelicState and then get name
                                if (string.IsNullOrEmpty(relicName))
                                {
                                    var getDataMethod = stateType.GetMethod("GetRelicData", BindingFlags.Public | BindingFlags.Instance);
                                    if (getDataMethod == null)
                                        getDataMethod = stateType.GetProperty("RelicData", BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod();

                                    if (getDataMethod != null)
                                    {
                                        var stateRelicData = getDataMethod.Invoke(relicState, null);
                                        if (stateRelicData != null)
                                        {
                                            var dataType = stateRelicData.GetType();
                                            var nameMethod = dataType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                                            if (nameMethod != null && nameMethod.GetParameters().Length == 0)
                                            {
                                                relicName = nameMethod.Invoke(stateRelicData, null) as string;
                                                MonsterTrainAccessibility.LogInfo($"RelicState.RelicData.GetName() returned: '{relicName}'");
                                            }
                                        }
                                    }
                                }
                            }

                            // Try GetDescription on RelicState
                            foreach (var methodName in new[] { "GetDescription", "GetEffectText", "GetDescriptionText" })
                            {
                                var method = stateType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                                if (method != null)
                                {
                                    var parameters = method.GetParameters();
                                    var paramCount = parameters.Length;
                                    MonsterTrainAccessibility.LogInfo($"Found {methodName} with {paramCount} params");

                                    try
                                    {
                                        if (paramCount == 0)
                                        {
                                            relicDescription = method.Invoke(relicState, null) as string;
                                        }
                                        else if (paramCount == 1)
                                        {
                                            // Try calling with null or default value
                                            var paramType = parameters[0].ParameterType;
                                            MonsterTrainAccessibility.LogInfo($"  Param type: {paramType.Name}");

                                            object arg = null;
                                            if (paramType.IsValueType)
                                            {
                                                arg = Activator.CreateInstance(paramType);
                                            }
                                            relicDescription = method.Invoke(relicState, new[] { arg }) as string;
                                        }

                                        MonsterTrainAccessibility.LogInfo($"RelicState.{methodName}() returned: '{relicDescription ?? "null"}'");
                                        if (!string.IsNullOrEmpty(relicDescription) && !relicDescription.Contains("_descriptionKey"))
                                        {
                                            break;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        MonsterTrainAccessibility.LogError($"Error calling {methodName}: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                }

                // Build result
                if (!string.IsNullOrEmpty(relicName))
                {
                    var sb = new StringBuilder();
                    sb.Append("Artifact: ");
                    sb.Append(relicName);

                    if (!string.IsNullOrEmpty(relicDescription) && !relicDescription.Contains("_descriptionKey"))
                    {
                        // Clean up sprite tags like <sprite name=Gold> -> "gold"
                        string cleanDesc = TextUtilities.CleanSpriteTagsForSpeech(relicDescription);
                        sb.Append(". ");
                        sb.Append(cleanDesc);

                        // Extract and append keyword explanations
                        var keywords = new List<string>();
                        CardTextReader.ExtractKeywordsFromDescription(relicDescription, keywords);
                        if (keywords.Count > 0)
                        {
                            sb.Append(" Keywords: ");
                            sb.Append(string.Join(". ", keywords));
                            sb.Append(".");
                        }
                    }

                    string result = sb.ToString();
                    MonsterTrainAccessibility.LogInfo($"Relic text: {result}");
                    return result;
                }

                MonsterTrainAccessibility.LogInfo("Could not extract relic info");
                return null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting relic info text: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Format relic/enhancer details from a RelicData or EnhancerData object.
        /// Returns name and description.
        /// </summary>
        internal static string FormatRelicDetails(object relicData)
        {
            try
            {
                var type = relicData.GetType();
                var sb = new StringBuilder();

                var getNameMethod = type.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(relicData, null) as string;
                    if (!string.IsNullOrEmpty(name))
                        sb.Append(name);
                }

                var getDescMethod = type.GetMethod("GetDescription", BindingFlags.Public | BindingFlags.Instance);
                if (getDescMethod != null)
                {
                    var desc = getDescMethod.Invoke(relicData, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                    {
                        string cleanDesc = TextUtilities.StripRichTextTags(desc);
                        if (sb.Length > 0) sb.Append(". ");
                        sb.Append(cleanDesc);
                    }
                }
                else
                {
                    // Create a RelicState to get the description with numeric parameters filled in
                    string desc = SettingsTextReader.GetRelicDescription(relicData);
                    if (!string.IsNullOrEmpty(desc))
                    {
                        string cleanDesc = TextUtilities.StripRichTextTags(desc);
                        if (sb.Length > 0) sb.Append(". ");
                        sb.Append(cleanDesc);
                    }
                }

                return sb.Length > 0 ? sb.ToString() : null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error formatting relic details: {ex.Message}");
                return null;
            }
        }


        /// <summary>
        /// Look up reward data by type and key, returning a readable description.
        /// PreviewType values: None=0, Card=1, Relic=2, Upgrade=3, Reward=4, Coin=5, DeckReward=6, Relic_Name=7
        /// </summary>
        internal static string GetRewardTextFromData(int previewType, string dataKey, object saveManager, object relicManager = null)
        {
            try
            {
                if (saveManager == null)
                {
                    MonsterTrainAccessibility.LogInfo("No SaveManager available for reward lookup");
                    return null;
                }

                // Get AllGameData from SaveManager
                var getAllGameDataMethod = saveManager.GetType().GetMethod("GetAllGameData", BindingFlags.Public | BindingFlags.Instance);
                if (getAllGameDataMethod == null) return null;

                var allGameData = getAllGameDataMethod.Invoke(saveManager, null);
                if (allGameData == null) return null;

                var gameDataType = allGameData.GetType();

                switch (previewType)
                {
                    case 1: // Card
                    {
                        var findMethod = gameDataType.GetMethod("FindCardDataByName", BindingFlags.Public | BindingFlags.Instance);
                        if (findMethod == null) return null;
                        var cardData = findMethod.Invoke(allGameData, new object[] { dataKey });
                        if (cardData == null) return null;

                        // Create a CardState from CardData to get full card text
                        // (CardData alone has no GetDescription/GetCardText - those are on CardState)
                        try
                        {
                            var cardStateType = cardData.GetType().Assembly.GetType("CardState");
                            if (cardStateType != null)
                            {
                                // CardState(CardData, RelicManager, SaveManager, bool setupStartingUpgrades = true)
                                var ctors = cardStateType.GetConstructors();
                                foreach (var ctor in ctors)
                                {
                                    var ps = ctor.GetParameters();
                                    if (ps.Length >= 3 && ps[0].ParameterType.Name == "CardData")
                                    {
                                        var args = new object[ps.Length];
                                        args[0] = cardData;
                                        args[1] = relicManager; // may be null, that's ok
                                        args[2] = saveManager;
                                        for (int i = 3; i < ps.Length; i++)
                                        {
                                            args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : (ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null);
                                        }
                                        var cardState = ctor.Invoke(args);
                                        if (cardState != null)
                                        {
                                            MonsterTrainAccessibility.LogInfo($"Created CardState from CardData for reward '{dataKey}'");
                                            return CardTextReader.FormatCardDetails(cardState);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MonsterTrainAccessibility.LogInfo($"Failed to create CardState from CardData: {ex.Message}");
                        }

                        // Fallback: use CardData directly (limited info)
                        return CardTextReader.FormatCardDetails(cardData);
                    }
                    case 2: // Relic
                    case 7: // Relic_Name
                    {
                        var findMethod = gameDataType.GetMethod("FindCollectableRelicDataByName", BindingFlags.Public | BindingFlags.Instance);
                        if (findMethod == null) return null;
                        var relicData = findMethod.Invoke(allGameData, new object[] { dataKey });
                        if (relicData == null) return null;
                        return FormatRelicDetails(relicData);
                    }
                    case 3: // Upgrade (Enhancer)
                    {
                        var findMethod = gameDataType.GetMethod("FindEnhancerDataByName", BindingFlags.Public | BindingFlags.Instance);
                        if (findMethod == null) return null;
                        var enhancerData = findMethod.Invoke(allGameData, new object[] { dataKey });
                        if (enhancerData == null) return null;
                        return FormatRelicDetails(enhancerData);
                    }
                    case 4: // Reward (GrantableRewardData - could be upgraded card)
                    {
                        var findMethod = gameDataType.GetMethod("FindRewardDataByName", BindingFlags.Public | BindingFlags.Instance);
                        if (findMethod == null) return null;
                        var rewardData = findMethod.Invoke(allGameData, new object[] { dataKey });
                        if (rewardData == null) return null;

                        var rewardDataType = rewardData.GetType();
                        // Try to get card name from various reward types
                        if (rewardDataType.Name == "GrantUpgradedCachedCardRewardData")
                        {
                            var getCardStateMethod = rewardDataType.GetMethod("GetCardState", BindingFlags.Public | BindingFlags.Instance);
                            if (getCardStateMethod != null)
                            {
                                var cardState = getCardStateMethod.Invoke(rewardData, new object[] { saveManager });
                                if (cardState != null) return CardTextReader.FormatCardDetails(cardState);
                            }
                        }
                        else if (rewardDataType.Name == "BuildCardRewardData")
                        {
                            var getCardDataMethod = rewardDataType.GetMethod("GetCardData", BindingFlags.Public | BindingFlags.Instance);
                            if (getCardDataMethod != null)
                            {
                                var cardData = getCardDataMethod.Invoke(rewardData, null);
                                if (cardData != null) return CardTextReader.FormatCardDetails(cardData);
                            }
                        }

                        // Generic fallback - try to get name
                        var getNameMethod = rewardDataType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                        if (getNameMethod != null)
                        {
                            var name = getNameMethod.Invoke(rewardData, null) as string;
                            if (!string.IsNullOrEmpty(name)) return name;
                        }
                        return null;
                    }
                    case 5: // Coin
                    {
                        return $"{dataKey} gold";
                    }
                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error looking up reward data (type={previewType}, key={dataKey}): {ex.Message}");
                return null;
            }
        }


        /// <summary>
        /// Resolve placeholders like {[effect0.power]} or {[effect0.status0.power]} in relic descriptions
        /// </summary>
        internal static string ResolveRelicEffectPlaceholders(string text, object relicData, Type relicType)
        {
            if (string.IsNullOrEmpty(text) || relicData == null) return text;

            MonsterTrainAccessibility.LogInfo($"ResolveRelicEffectPlaceholders called with text: {text}");

            try
            {
                // Get effects from the relic data
                var getEffectsMethod = relicType.GetMethod("GetEffects", Type.EmptyTypes);
                if (getEffectsMethod == null)
                {
                    // Try base types
                    var baseType = relicType.BaseType;
                    while (baseType != null && getEffectsMethod == null)
                    {
                        getEffectsMethod = baseType.GetMethod("GetEffects", Type.EmptyTypes);
                        baseType = baseType.BaseType;
                    }
                }

                MonsterTrainAccessibility.LogInfo($"GetEffects method found: {getEffectsMethod != null}");

                if (getEffectsMethod != null)
                {
                    var effects = getEffectsMethod.Invoke(relicData, null) as System.Collections.IList;
                    MonsterTrainAccessibility.LogInfo($"Effects count: {effects?.Count ?? 0}");
                    if (effects != null && effects.Count > 0)
                    {
                        // Log first effect's fields for debugging
                        var firstEffect = effects[0];
                        if (firstEffect != null)
                        {
                            var effectType = firstEffect.GetType();
                            MonsterTrainAccessibility.LogInfo($"Effect[0] type: {effectType.Name}");
                            foreach (var field in effectType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                            {
                                if (field.Name.ToLower().Contains("param") || field.Name.ToLower().Contains("power"))
                                {
                                    try
                                    {
                                        var val = field.GetValue(firstEffect);
                                        MonsterTrainAccessibility.LogInfo($"  {field.Name} = {val}");
                                    }
                                    catch { }
                                }
                            }
                        }
                        // Match patterns like {[effect0.power]}, {[effect0.status0.power]}, {[#effect0.power]}
                        var regex = new System.Text.RegularExpressions.Regex(@"\{\[#?effect(\d+)\.(?:status(\d+)\.)?(\w+)\]\}");
                        text = regex.Replace(text, match =>
                        {
                            int effectIndex = int.Parse(match.Groups[1].Value);
                            string property = match.Groups[3].Value;
                            int statusIndex = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : -1;

                            if (effectIndex < effects.Count)
                            {
                                var effect = effects[effectIndex];
                                if (effect != null)
                                {
                                    var effectType = effect.GetType();

                                    // If status index specified, get from paramStatusEffects array
                                    if (statusIndex >= 0 && property.ToLower() == "power")
                                    {
                                        var statusEffectsField = effectType.GetField("paramStatusEffects",
                                            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                                        if (statusEffectsField != null)
                                        {
                                            var statusEffects = statusEffectsField.GetValue(effect) as Array;
                                            if (statusEffects != null && statusIndex < statusEffects.Length)
                                            {
                                                var statusEffect = statusEffects.GetValue(statusIndex);
                                                if (statusEffect != null)
                                                {
                                                    // StatusEffectStackData has public 'count' field for the power
                                                    var countField = statusEffect.GetType().GetField("count",
                                                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                                    if (countField != null)
                                                    {
                                                        var count = countField.GetValue(statusEffect);
                                                        return count?.ToString() ?? match.Value;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Map placeholder property names to actual field names
                                        string fieldName;
                                        switch (property.ToLower())
                                        {
                                            case "power": fieldName = "paramInt"; break;
                                            case "powerabs": fieldName = "paramInt"; break;
                                            case "minpower": fieldName = "paramMinInt"; break;
                                            case "maxpower": fieldName = "paramMaxInt"; break;
                                            default: fieldName = "param" + char.ToUpper(property[0]) + property.Substring(1); break;
                                        }

                                        var propField = effectType.GetField(fieldName,
                                            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                                        if (propField != null)
                                        {
                                            var value = propField.GetValue(effect);
                                            if (property.ToLower() == "powerabs" && value is int intVal)
                                                return Math.Abs(intVal).ToString();
                                            MonsterTrainAccessibility.LogInfo($"Resolved {match.Value} -> {value} (field: {fieldName})");
                                            return value?.ToString() ?? match.Value;
                                        }
                                    }
                                }
                            }
                            return match.Value;
                        });

                        // Strip any remaining unresolved placeholders to avoid reading raw variables
                        var unresolvedRegex = new System.Text.RegularExpressions.Regex(@"\{\[[^\]]*\]\}");
                        text = unresolvedRegex.Replace(text, "");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"ResolveRelicEffectPlaceholders error: {ex.Message}");
            }

            return text;
        }


        /// <summary>
        /// Append keyword explanations to text containing known game keywords
        /// </summary>
        internal static string AppendKeywordExplanations(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var keywords = new List<string>();
            CardTextReader.ExtractKeywordsFromDescription(text, keywords);

            if (keywords.Count > 0)
            {
                return text + " Keywords: " + string.Join(". ", keywords) + ".";
            }

            return text;
        }

    }
}
