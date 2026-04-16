using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Detect battle intro screen (pre-battle, showing enemy info and Fight button)
    /// </summary>
    public static class BattleIntroScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("BattleIntroScreen");
                if (targetType != null)
                {
                    // Try Initialize or Setup method
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(BattleIntroScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched BattleIntroScreen.{method.Name}");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("BattleIntroScreen methods not found - will use alternative detection");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch BattleIntroScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.BattleIntro);
                MonsterTrainAccessibility.LogInfo("Battle intro screen entered");

                // Auto-read the battle intro content
                AutoReadBattleIntro(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in BattleIntroScreen patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Automatically read the battle intro screen content
        /// </summary>
        private static void AutoReadBattleIntro(object battleIntroScreen)
        {
            try
            {
                if (battleIntroScreen == null) return;

                var screenType = battleIntroScreen.GetType();
                var sb = new StringBuilder();

                // Get battle/boss name from SaveManager's scenario data (more reliable than UI label)
                string battleName = GetBossNameFromScreen(battleIntroScreen, screenType);

                // Get battle description
                string battleDescription = null;
                var descField = screenType.GetField("battleDescriptionLabel", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (descField != null)
                {
                    var descLabel = descField.GetValue(battleIntroScreen);
                    if (descLabel != null)
                    {
                        // MultilineTextFitter might have a text property or GetText method
                        var textProp = descLabel.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            battleDescription = textProp.GetValue(descLabel) as string;
                        }
                        else
                        {
                            // Try GetText method
                            var getTextMethod = descLabel.GetType().GetMethod("GetText");
                            if (getTextMethod != null)
                            {
                                battleDescription = getTextMethod.Invoke(descLabel, null) as string;
                            }
                        }
                    }
                }

                // Build the announcement
                sb.Append("Battle intro. ");
                if (!string.IsNullOrEmpty(battleName))
                {
                    sb.Append("Boss: ");
                    sb.Append(battleName);
                    sb.Append(". ");
                }
                if (!string.IsNullOrEmpty(battleDescription))
                {
                    sb.Append(Screens.BattleAccessibility.StripRichTextTags(battleDescription));
                    sb.Append(" ");
                }

                // Check for trial
                var trialDataField = screenType.GetField("trialData", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                object trialData = trialDataField?.GetValue(battleIntroScreen);

                if (trialData != null)
                {
                    var trialEnabledField = screenType.GetField("trialEnabled", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    bool trialEnabled = false;
                    if (trialEnabledField != null)
                    {
                        var val = trialEnabledField.GetValue(battleIntroScreen);
                        if (val is bool b) trialEnabled = b;
                    }

                    var trialType = trialData.GetType();
                    string ruleName = null;
                    string ruleDescription = null;
                    string rewardName = null;

                    // The rule comes from the 'sin' field (SinsData), which is a RelicData subclass
                    var sinField = trialType.GetField("sin", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    if (sinField != null)
                    {
                        var sinData = sinField.GetValue(trialData);
                        if (sinData != null)
                        {
                            var sinType = sinData.GetType();

                            // Get the rule name from sin
                            var getNameMethod = sinType.GetMethod("GetName");
                            if (getNameMethod != null)
                            {
                                ruleName = getNameMethod.Invoke(sinData, null) as string;
                            }

                            // Get the description by creating a RelicState from the SinsData.
                            // RelicState.GetDescription() fills in numeric parameters (e.g., attack values)
                            // that raw localization of the description key would miss.
                            ruleDescription = Screens.SettingsTextReader.GetRelicDescription(sinData);
                        }
                    }

                    // Get reward from trial data
                    var rewardField = trialType.GetField("reward", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    if (rewardField != null)
                    {
                        var rewardData = rewardField.GetValue(trialData);
                        if (rewardData != null)
                        {
                            rewardName = GetRewardName(rewardData);
                        }
                    }

                    // Build a clear, descriptive trial announcement
                    sb.Append("Trial available! ");

                    if (trialEnabled)
                    {
                        sb.Append("Trial is ON. ");
                        if (!string.IsNullOrEmpty(ruleName))
                        {
                            sb.Append("Additional rule: ");
                            sb.Append(ruleName);
                            sb.Append(". ");
                        }
                        if (!string.IsNullOrEmpty(ruleDescription))
                        {
                            sb.Append(Screens.BattleAccessibility.StripRichTextTags(ruleDescription));
                            sb.Append(" ");
                        }
                        if (!string.IsNullOrEmpty(rewardName))
                        {
                            sb.Append("You will gain an additional reward: ");
                            sb.Append(rewardName);
                            sb.Append(". ");
                        }
                    }
                    else
                    {
                        sb.Append("Trial is OFF. ");
                        if (!string.IsNullOrEmpty(ruleName))
                        {
                            sb.Append("If enabled, additional rule: ");
                            sb.Append(ruleName);
                            sb.Append(". ");
                        }
                        if (!string.IsNullOrEmpty(ruleDescription))
                        {
                            sb.Append(Screens.BattleAccessibility.StripRichTextTags(ruleDescription));
                            sb.Append(" ");
                        }
                        if (!string.IsNullOrEmpty(rewardName))
                        {
                            sb.Append("Enable trial to gain additional reward: ");
                            sb.Append(rewardName);
                            sb.Append(". ");
                        }
                    }

                    sb.Append("Press F to toggle trial. ");
                }

                sb.Append("Press Enter to fight. Press F1 for help.");

                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error auto-reading battle intro: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the boss/battle name from the screen's SaveManager scenario data
        /// </summary>
        private static string GetBossNameFromScreen(object screen, Type screenType)
        {
            try
            {
                // Try to get SaveManager from the screen
                var saveManagerField = screenType.GetField("saveManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (saveManagerField != null)
                {
                    var saveManager = saveManagerField.GetValue(screen);
                    if (saveManager != null)
                    {
                        var saveManagerType = saveManager.GetType();

                        // Try GetCurrentScenarioData method
                        var getCurrentScenarioMethod = saveManagerType.GetMethod("GetCurrentScenarioData", BindingFlags.Public | BindingFlags.Instance);
                        if (getCurrentScenarioMethod != null && getCurrentScenarioMethod.GetParameters().Length == 0)
                        {
                            var scenarioData = getCurrentScenarioMethod.Invoke(saveManager, null);
                            if (scenarioData != null)
                            {
                                string name = GetBattleNameFromScenario(scenarioData);
                                if (!string.IsNullOrEmpty(name))
                                    return name;
                            }
                        }

                        // Try GetScenario method
                        var getScenarioMethod = saveManagerType.GetMethod("GetScenario", BindingFlags.Public | BindingFlags.Instance);
                        if (getScenarioMethod != null && getScenarioMethod.GetParameters().Length == 0)
                        {
                            var scenarioData = getScenarioMethod.Invoke(saveManager, null);
                            if (scenarioData != null)
                            {
                                string name = GetBattleNameFromScenario(scenarioData);
                                if (!string.IsNullOrEmpty(name))
                                    return name;
                            }
                        }
                    }
                }

                // Fallback: try to read from the UI label
                var battleNameField = screenType.GetField("battleNameLabel", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (battleNameField != null)
                {
                    var label = battleNameField.GetValue(screen);
                    if (label != null)
                    {
                        var textProp = label.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            return textProp.GetValue(label) as string;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting boss name from screen: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the battle name from a ScenarioData object
        /// </summary>
        private static string GetBattleNameFromScenario(object scenarioData)
        {
            if (scenarioData == null) return null;

            try
            {
                var dataType = scenarioData.GetType();

                // Try GetBattleName method first
                var getBattleNameMethod = dataType.GetMethod("GetBattleName", BindingFlags.Public | BindingFlags.Instance);
                if (getBattleNameMethod != null && getBattleNameMethod.GetParameters().Length == 0)
                {
                    var result = getBattleNameMethod.Invoke(scenarioData, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                        return name;
                }

                // Try GetName method
                var getNameMethod = dataType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                {
                    var result = getNameMethod.Invoke(scenarioData, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                        return name;
                }

                // Try battleNameKey field with localization
                string[] nameFieldNames = { "battleNameKey", "_battleNameKey", "nameKey", "_nameKey" };
                foreach (var fieldName in nameFieldNames)
                {
                    var field = dataType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var key = field.GetValue(scenarioData) as string;
                        if (!string.IsNullOrEmpty(key))
                        {
                            string localized = TryLocalizeKey(key);
                            if (!string.IsNullOrEmpty(localized) && localized != key)
                                return localized;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle name from scenario: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract the name from a RewardData object
        /// </summary>
        private static string GetRewardName(object rewardData)
        {
            if (rewardData == null) return null;

            try
            {
                var rewardType = rewardData.GetType();
                string result = null;

                // Try GetTitle method first (if it exists)
                var getTitleMethod = rewardType.GetMethod("GetTitle");
                if (getTitleMethod != null)
                {
                    var title = getTitleMethod.Invoke(rewardData, null) as string;
                    if (!string.IsNullOrEmpty(title) && !title.Contains("_") && !title.Contains("-"))
                        result = title;
                }

                // Try to get the title key and localize it using the game's Localize method
                if (result == null)
                {
                    var titleKeyField = rewardType.GetField("_rewardTitleKey", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (titleKeyField != null)
                    {
                        var titleKey = titleKeyField.GetValue(rewardData) as string;
                        if (!string.IsNullOrEmpty(titleKey))
                        {
                            // Try to find and use the Localize extension method
                            string localized = TryLocalizeKey(titleKey);
                            if (!string.IsNullOrEmpty(localized) && localized != titleKey && !localized.Contains("-"))
                                result = localized;
                        }
                    }
                }

                // Fall back to type name - this is the most reliable approach
                if (result == null)
                {
                    result = GetRewardTypeDisplayName(rewardType);
                }

                // Resolve {[codeint0]} placeholders in the result
                if (!string.IsNullOrEmpty(result) && result.Contains("{[codeint"))
                {
                    result = ResolveCodeIntPlaceholders(result, rewardData, rewardType);
                }

                // Clean sprite tags like <sprite name="Gold"> -> "gold"
                if (!string.IsNullOrEmpty(result) && result.Contains("sprite"))
                {
                    result = CleanSpriteTagsForReward(result);
                }

                return result;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting reward name: {ex.Message}");
                return "Reward";
            }
        }

        /// <summary>
        /// Resolve {[codeint0]} style placeholders in reward text
        /// </summary>
        internal static string ResolveCodeIntPlaceholders(string text, object rewardData, Type rewardType)
        {
            if (string.IsNullOrEmpty(text) || rewardData == null) return text;

            MonsterTrainAccessibility.LogInfo($"ResolveCodeIntPlaceholders called for: {text}");
            MonsterTrainAccessibility.LogInfo($"RewardData type: {rewardType.Name}");

            try
            {
                // Log all fields on the reward data to find the right one
                var allFields = rewardType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                foreach (var f in allFields)
                {
                    try
                    {
                        var val = f.GetValue(rewardData);
                        string valStr = val?.ToString() ?? "null";
                        if (val is Array arr)
                            valStr = $"Array[{arr.Length}]: {string.Join(", ", arr.Cast<object>().Take(5))}";
                        MonsterTrainAccessibility.LogInfo($"  Field: {f.Name} = {valStr}");
                    }
                    catch { }
                }

                // Look for codeInts field (array of ints for placeholder values)
                var codeIntsField = rewardType.GetField("codeInts", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (codeIntsField == null)
                {
                    codeIntsField = rewardType.GetField("_codeInts", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                }

                int[] codeInts = null;
                if (codeIntsField != null)
                {
                    codeInts = codeIntsField.GetValue(rewardData) as int[];
                    MonsterTrainAccessibility.LogInfo($"Found codeInts field, value: {(codeInts != null ? string.Join(", ", codeInts) : "null")}");
                }

                // For GoldRewardData and similar, use _amount field for codeint0
                if (codeInts == null || codeInts.Length == 0)
                {
                    var amountField = rewardType.GetField("_amount", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    if (amountField != null)
                    {
                        var amountVal = amountField.GetValue(rewardData);
                        if (amountVal is int amount)
                        {
                            codeInts = new int[] { amount };
                            MonsterTrainAccessibility.LogInfo($"Using _amount field for codeint0: {amount}");
                        }
                    }
                }

                if (codeInts != null && codeInts.Length > 0)
                {
                    // Replace {[codeint0]}, {[codeint1]}, etc.
                    var regex = new System.Text.RegularExpressions.Regex(@"\{\[codeint(\d+)\]\}");
                    text = regex.Replace(text, match =>
                    {
                        int index = int.Parse(match.Groups[1].Value);
                        if (index < codeInts.Length)
                        {
                            MonsterTrainAccessibility.LogInfo($"Resolved {match.Value} -> {codeInts[index]}");
                            return codeInts[index].ToString();
                        }
                        return match.Value;
                    });
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"ResolveCodeIntPlaceholders error: {ex.Message}");
            }

            return text;
        }

        /// <summary>
        /// Clean sprite tags like <sprite name="Gold"> to readable text like "gold"
        /// </summary>
        private static string CleanSpriteTagsForReward(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Convert <sprite name="Gold"> or <sprite name=Gold> to "gold"
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<sprite\s+name\s*=\s*[""']?([^""'>\s]+)[""']?\s*/?>",
                match => " " + match.Groups[1].Value.ToLower() + " ",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Also handle <sprite=X> format
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<sprite\s*=\s*[""']?([^""'>\s]+)[""']?\s*/?>",
                match => " " + match.Groups[1].Value.ToLower() + " ",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Clean up double spaces and trim
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

            return text;
        }

        private static MethodInfo _localizeMethod = null;
        private static bool _localizeMethodSearched = false;

        /// <summary>
        /// Try to localize a key using the game's localization system
        /// </summary>
        internal static string TryLocalizeKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            try
            {
                // Cache the Localize method on first use
                if (!_localizeMethodSearched)
                {
                    _localizeMethodSearched = true;

                    // Search in Assembly-CSharp for LocalizationExtensions.Localize
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var assemblyName = assembly.GetName().Name;
                        if (!assemblyName.Contains("Assembly-CSharp"))
                            continue;

                        try
                        {
                            var types = assembly.GetTypes();
                            foreach (var type in types)
                            {
                                // Look for static classes that contain extension methods
                                if (!type.IsClass || !type.IsAbstract || !type.IsSealed)
                                    continue;

                                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
                                foreach (var method in methods)
                                {
                                    if (method.Name == "Localize" && method.ReturnType == typeof(string))
                                    {
                                        var parameters = method.GetParameters();
                                        if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(string))
                                        {
                                            _localizeMethod = method;
                                            break;
                                        }
                                    }
                                }
                                if (_localizeMethod != null) break;
                            }
                        }
                        catch { }
                        if (_localizeMethod != null) break;
                    }
                }

                // Use cached method
                if (_localizeMethod != null)
                {
                    var parameters = _localizeMethod.GetParameters();
                    object[] args;
                    if (parameters.Length == 1)
                    {
                        args = new object[] { key };
                    }
                    else
                    {
                        // Fill additional params with defaults
                        args = new object[parameters.Length];
                        args[0] = key;
                        for (int i = 1; i < parameters.Length; i++)
                        {
                            args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                        }
                    }

                    var result = _localizeMethod.Invoke(null, args);
                    if (result is string localized && !string.IsNullOrEmpty(localized))
                    {
                        return localized;
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"TryLocalizeKey error: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Resolve placeholders like {[effect0.status0.power]} in localized text
        /// </summary>
        internal static string ResolveEffectPlaceholders(string text, object relicData, Type relicType)
        {
            if (string.IsNullOrEmpty(text) || relicData == null) return text;

            try
            {
                // Get effects from the relic data (SinsData inherits from RelicData)
                var getEffectsMethod = relicType.GetMethod("GetEffects", Type.EmptyTypes);
                if (getEffectsMethod == null)
                {
                    // Try the base RelicData type
                    var baseType = relicType.BaseType;
                    while (baseType != null && getEffectsMethod == null)
                    {
                        getEffectsMethod = baseType.GetMethod("GetEffects", Type.EmptyTypes);
                        baseType = baseType.BaseType;
                    }
                }

                if (getEffectsMethod != null)
                {
                    var effects = getEffectsMethod.Invoke(relicData, null) as System.Collections.IList;
                    if (effects != null && effects.Count > 0)
                    {
                        // Look for patterns like {[effect0.status0.power]} or {[effect0.power]}
                        var regex = new System.Text.RegularExpressions.Regex(@"\{\[effect(\d+)\.(?:status(\d+)\.)?(\w+)\]\}");
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
                                    var bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

                                    // If status index specified, get from paramStatusEffects array
                                    if (statusIndex >= 0 && property.ToLower() == "power")
                                    {
                                        var statusEffectsField = effectType.GetField("paramStatusEffects", bindingFlags);
                                        if (statusEffectsField != null)
                                        {
                                            var statusEffects = statusEffectsField.GetValue(effect) as Array;
                                            if (statusEffects != null && statusIndex < statusEffects.Length)
                                            {
                                                var statusEffect = statusEffects.GetValue(statusIndex);
                                                if (statusEffect != null)
                                                {
                                                    // StatusEffectStackData has 'count' field for the power
                                                    var countField = statusEffect.GetType().GetField("count",
                                                        BindingFlags.Public | BindingFlags.Instance);
                                                    if (countField != null)
                                                    {
                                                        var count = countField.GetValue(statusEffect);
                                                        return count?.ToString() ?? match.Value;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    // Handle "power" without status index - common for simple effects
                                    else if (statusIndex < 0 && property.ToLower() == "power")
                                    {
                                        // First try paramInt (most common for simple integer values like +X attack)
                                        var paramIntField = effectType.GetField("paramInt", bindingFlags);
                                        if (paramIntField != null)
                                        {
                                            var value = paramIntField.GetValue(effect);
                                            if (value != null)
                                            {
                                                return value.ToString();
                                            }
                                        }

                                        // Try first status effect's count as fallback
                                        var statusEffectsField = effectType.GetField("paramStatusEffects", bindingFlags);
                                        if (statusEffectsField != null)
                                        {
                                            var statusEffects = statusEffectsField.GetValue(effect) as Array;
                                            if (statusEffects != null && statusEffects.Length > 0)
                                            {
                                                var statusEffect = statusEffects.GetValue(0);
                                                if (statusEffect != null)
                                                {
                                                    var countField = statusEffect.GetType().GetField("count",
                                                        BindingFlags.Public | BindingFlags.Instance);
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
                                        // Try to get the property directly from effect (e.g., paramInt, paramFloat)
                                        var propField = effectType.GetField("param" + char.ToUpper(property[0]) + property.Substring(1), bindingFlags);
                                        if (propField != null)
                                        {
                                            var value = propField.GetValue(effect);
                                            return value?.ToString() ?? match.Value;
                                        }
                                    }
                                }
                            }
                            return match.Value;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"ResolveEffectPlaceholders error: {ex.Message}");
            }

            return text;
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
