using HarmonyLib;
using MonsterTrainAccessibility.Core;
using MonsterTrainAccessibility.Help;
using System;
using System.Linq;
using UnityEngine;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Patches for detecting screen transitions and notifying accessibility handlers.
    /// These patches hook into game screen managers to detect when players enter different screens.
    /// </summary>

    /// <summary>
    /// Detect when main menu is shown
    /// </summary>
    public static class MainMenuScreenPatch
    {
        // Target: MainMenuScreen.Initialize
        // This will be resolved at runtime when the game DLLs are available

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("MainMenuScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(MainMenuScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched MainMenuScreen.Initialize");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch MainMenuScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.MainMenu);
                MonsterTrainAccessibility.MenuHandler?.OnMainMenuEntered(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in MainMenuScreen patch: {ex.Message}");
            }
        }
    }

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
                var sb = new System.Text.StringBuilder();

                // Get battle/boss name from SaveManager's scenario data (more reliable than UI label)
                string battleName = GetBossNameFromScreen(battleIntroScreen, screenType);

                // Get battle description
                string battleDescription = null;
                var descField = screenType.GetField("battleDescriptionLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
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
                var trialDataField = screenType.GetField("trialData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                object trialData = trialDataField?.GetValue(battleIntroScreen);

                if (trialData != null)
                {
                    var trialEnabledField = screenType.GetField("trialEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
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
                    var sinField = trialType.GetField("sin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
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

                            // Get the rule description - try GetDescriptionKey() and localize
                            var getDescKeyMethod = sinType.GetMethod("GetDescriptionKey");
                            if (getDescKeyMethod != null && getDescKeyMethod.GetParameters().Length == 0)
                            {
                                var descKey = getDescKeyMethod.Invoke(sinData, null) as string;
                                if (!string.IsNullOrEmpty(descKey))
                                {
                                    ruleDescription = TryLocalizeKey(descKey);

                                    // Resolve placeholders like {[effect0.status0.power]}
                                    if (!string.IsNullOrEmpty(ruleDescription) && ruleDescription.Contains("{["))
                                    {
                                        ruleDescription = ResolveEffectPlaceholders(ruleDescription, sinData, sinType);
                                    }
                                }
                            }
                        }
                    }

                    // Get reward from trial data
                    var rewardField = trialType.GetField("reward", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
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
                var saveManagerField = screenType.GetField("saveManager", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (saveManagerField != null)
                {
                    var saveManager = saveManagerField.GetValue(screen);
                    if (saveManager != null)
                    {
                        var saveManagerType = saveManager.GetType();

                        // Try GetCurrentScenarioData method
                        var getCurrentScenarioMethod = saveManagerType.GetMethod("GetCurrentScenarioData", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
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
                        var getScenarioMethod = saveManagerType.GetMethod("GetScenario", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
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
                var battleNameField = screenType.GetField("battleNameLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
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
                var getBattleNameMethod = dataType.GetMethod("GetBattleName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (getBattleNameMethod != null && getBattleNameMethod.GetParameters().Length == 0)
                {
                    var result = getBattleNameMethod.Invoke(scenarioData, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                        return name;
                }

                // Try GetName method
                var getNameMethod = dataType.GetMethod("GetName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
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
                    var field = dataType.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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
                    var titleKeyField = rewardType.GetField("_rewardTitleKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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
        private static string ResolveCodeIntPlaceholders(string text, object rewardData, Type rewardType)
        {
            if (string.IsNullOrEmpty(text) || rewardData == null) return text;

            MonsterTrainAccessibility.LogInfo($"ResolveCodeIntPlaceholders called for: {text}");
            MonsterTrainAccessibility.LogInfo($"RewardData type: {rewardType.Name}");

            try
            {
                // Log all fields on the reward data to find the right one
                var allFields = rewardType.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
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
                var codeIntsField = rewardType.GetField("codeInts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (codeIntsField == null)
                {
                    codeIntsField = rewardType.GetField("_codeInts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
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
                    var amountField = rewardType.GetField("_amount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
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

        private static System.Reflection.MethodInfo _localizeMethod = null;
        private static bool _localizeMethodSearched = false;

        /// <summary>
        /// Try to localize a key using the game's localization system
        /// </summary>
        private static string TryLocalizeKey(string key)
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

                                var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
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
        private static string ResolveEffectPlaceholders(string text, object relicData, Type relicType)
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

                                    // If status index specified, get from paramStatusEffects array
                                    if (statusIndex >= 0 && property.ToLower() == "power")
                                    {
                                        var statusEffectsField = effectType.GetField("paramStatusEffects",
                                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
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
                                                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
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
                                        // Try to get the property directly from effect
                                        var propField = effectType.GetField("param" + char.ToUpper(property[0]) + property.Substring(1),
                                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
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
        private static string GetRewardTypeDisplayName(Type rewardType)
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

    /// <summary>
    /// Detect when combat starts
    /// </summary>
    public static class CombatStartPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CombatManager");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "StartCombat");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(CombatStartPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched CombatManager.StartCombat");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CombatManager: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Battle);
                MonsterTrainAccessibility.BattleHandler?.OnBattleEntered();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CombatStart patch: {ex.Message}");
            }
        }
    }

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
                // Extract draft cards from __instance and call handler
                // This would parse the actual CardDraftScreen to get card data
                MonsterTrainAccessibility.LogInfo("Card draft screen detected");

                // For now, announce generic draft entry
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Card Draft. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CardDraftScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect clan/class selection screen
    /// </summary>
    public static class ClassSelectionScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("ClassSelectionScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(ClassSelectionScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched ClassSelectionScreen.Initialize");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch ClassSelectionScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.ClanSelection);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Clan Selection. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ClassSelectionScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect map screen
    /// </summary>
    public static class MapScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("MapScreen");
                if (targetType == null)
                {
                    targetType = AccessTools.TypeByName("MapNodeScreen");
                }

                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(MapScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched {targetType.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch MapScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Map);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Map. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in MapScreen patch: {ex.Message}");
            }
        }
    }

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

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Shop);

                // Announce gold when entering shop
                int gold = InputInterceptor.GetCurrentGold();
                string goldText = gold >= 0 ? $"You have {gold} gold." : "";

                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen($"Shop. {goldText} Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in MerchantScreen patch: {ex.Message}");
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
                var getCardsMethod = instanceType.GetMethod("GetCards", System.Type.EmptyTypes) ??
                                     instanceType.GetMethod("GetCardList", System.Type.EmptyTypes);
                if (getCardsMethod != null)
                {
                    var cards = getCardsMethod.Invoke(__instance, null) as System.Collections.IList;
                    if (cards != null)
                        cardCount = cards.Count;
                }

                // Also try cards field
                if (cardCount == 0)
                {
                    var fields = instanceType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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

    /// <summary>
    /// Detect game over / run summary screen (victory or defeat)
    /// </summary>
    public static class GameOverScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try different possible class names for the game over screen
                var targetNames = new[] {
                    "GameOverScreen",
                    "RunSummaryScreen",
                    "VictoryScreen",
                    "DefeatScreen",
                    "RunEndScreen",
                    "EndRunScreen"
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
                            var postfix = new HarmonyMethod(typeof(GameOverScreenPatch).GetMethod(nameof(Postfix)));
                            harmony.Patch(method, postfix: postfix);
                            MonsterTrainAccessibility.LogInfo($"Patched {name}.{method.Name}");
                            return;
                        }
                    }
                }

                MonsterTrainAccessibility.LogInfo("GameOverScreen not found - will use alternative detection");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch GameOverScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Game over screen entered");

                // Auto-read the game over screen
                AutoReadGameOverScreen(__instance);

                // Also call the menu handler for additional processing
                MonsterTrainAccessibility.MenuHandler?.OnGameOverScreenEntered(__instance);

                // Fix navigation by selecting the first button after a short delay
                if (__instance is MonoBehaviour screenBehaviour)
                {
                    screenBehaviour.StartCoroutine(SelectFirstButtonDelayed(__instance));
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in GameOverScreen patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Select the first selectable button on the game over screen after a delay
        /// </summary>
        private static System.Collections.IEnumerator SelectFirstButtonDelayed(object screen)
        {
            // Wait for UI to fully load
            yield return new WaitForSecondsRealtime(0.5f);

            GameObject screenGO = null;
            UnityEngine.UI.Selectable firstSelectable = null;
            bool success = false;

            try
            {
                if (screen is Component comp)
                {
                    screenGO = comp.gameObject;
                }
                else if (screen is GameObject go)
                {
                    screenGO = go;
                }

                if (screenGO != null)
                {
                    // Find all selectable/button elements on the screen
                    var allSelectables = screenGO.GetComponentsInChildren<UnityEngine.UI.Selectable>(false);

                    // Filter to only active, interactable selectables
                    foreach (var sel in allSelectables)
                    {
                        if (sel != null && sel.gameObject.activeInHierarchy && sel.interactable)
                        {
                            firstSelectable = sel;
                            break;
                        }
                    }

                    if (firstSelectable != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Selecting first button on game over screen: {firstSelectable.name}");
                        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(firstSelectable.gameObject);
                        success = true;
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("No selectable buttons found on game over screen");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error selecting first button: {ex.Message}");
            }

            // Announce navigation help if we found a button
            if (success)
            {
                yield return new WaitForSecondsRealtime(0.3f);
                MonsterTrainAccessibility.ScreenReader?.Speak("Use arrow keys to navigate, Enter to select.", false);
            }
        }

        private static void AutoReadGameOverScreen(object screen)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                var screenType = screen?.GetType();

                // Log fields for debugging
                MonsterTrainAccessibility.LogInfo($"=== GameOverScreen fields ===");
                if (screenType != null)
                {
                    foreach (var field in screenType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                    {
                        try
                        {
                            var value = field.GetValue(screen);
                            MonsterTrainAccessibility.LogInfo($"  {field.Name} = {value?.GetType().Name ?? "null"}");
                        }
                        catch { }
                    }
                }

                // Try to get victory/defeat status from SaveManager
                bool isVictory = false;
                int score = 0;
                int battlesWon = 0;
                int ring = 0;

                var saveManagerType = AccessTools.TypeByName("SaveManager");
                if (saveManagerType != null)
                {
                    var saveManager = UnityEngine.Object.FindObjectOfType(saveManagerType) as UnityEngine.Object;
                    if (saveManager != null)
                    {
                        // Check if battle was won/lost
                        var battleCompleteMethod = saveManagerType.GetMethod("BattleComplete");
                        if (battleCompleteMethod != null)
                        {
                            var result = battleCompleteMethod.Invoke(saveManager, null);
                            if (result is bool bc) isVictory = bc;
                        }

                        // Get ring/covenant level
                        var getCovenantMethod = saveManagerType.GetMethod("GetAscensionLevel") ??
                                               saveManagerType.GetMethod("GetCovenantLevel");
                        if (getCovenantMethod != null)
                        {
                            var result = getCovenantMethod.Invoke(saveManager, null);
                            if (result is int r) ring = r;
                        }

                        // Get battles won
                        var getBattlesMethod = saveManagerType.GetMethod("GetNumBattlesWon");
                        if (getBattlesMethod != null)
                        {
                            var result = getBattlesMethod.Invoke(saveManager, null);
                            if (result is int b) battlesWon = b;
                        }

                        // Get score
                        var getScoreMethod = saveManagerType.GetMethod("GetRunScore") ??
                                            saveManagerType.GetMethod("GetScore");
                        if (getScoreMethod != null)
                        {
                            var result = getScoreMethod.Invoke(saveManager, null);
                            if (result is int s) score = s;
                        }
                    }
                }

                // Try to get specific labels from the screen
                string resultTitle = null;
                string runType = null;

                if (screenType != null)
                {
                    // Look for result/victory/defeat label
                    var resultField = screenType.GetField("resultLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) ??
                                     screenType.GetField("titleLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) ??
                                     screenType.GetField("victoryDefeatLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (resultField != null)
                    {
                        var labelObj = resultField.GetValue(screen);
                        if (labelObj != null)
                        {
                            var textProp = labelObj.GetType().GetProperty("text");
                            if (textProp != null)
                            {
                                resultTitle = textProp.GetValue(labelObj) as string;
                            }
                        }
                    }

                    // Look for run type label
                    var runTypeField = screenType.GetField("runTypeLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) ??
                                      screenType.GetField("runNameLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (runTypeField != null)
                    {
                        var labelObj = runTypeField.GetValue(screen);
                        if (labelObj != null)
                        {
                            var textProp = labelObj.GetType().GetProperty("text");
                            if (textProp != null)
                            {
                                runType = textProp.GetValue(labelObj) as string;
                            }
                        }
                    }

                    // Look for score label
                    var scoreField = screenType.GetField("scoreLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) ??
                                    screenType.GetField("totalScoreLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (scoreField != null && score == 0)
                    {
                        var labelObj = scoreField.GetValue(screen);
                        if (labelObj != null)
                        {
                            var textProp = labelObj.GetType().GetProperty("text");
                            if (textProp != null)
                            {
                                var scoreText = textProp.GetValue(labelObj) as string;
                                if (!string.IsNullOrEmpty(scoreText))
                                {
                                    // Parse score from text like "4,254"
                                    scoreText = System.Text.RegularExpressions.Regex.Replace(scoreText, "[^0-9]", "");
                                    int.TryParse(scoreText, out score);
                                }
                            }
                        }
                    }
                }

                // Build announcement
                // Result title (Victory/Defeat)
                if (!string.IsNullOrEmpty(resultTitle))
                {
                    sb.Append($"{resultTitle}. ");
                }
                else
                {
                    sb.Append(isVictory ? "Victory. " : "Defeat. ");
                }

                // Run type and ring
                if (!string.IsNullOrEmpty(runType))
                {
                    sb.Append($"{runType}. ");
                }
                if (ring > 0)
                {
                    sb.Append($"Covenant {ring}. ");
                }

                // Score
                if (score > 0)
                {
                    sb.Append($"Score: {score:N0}. ");
                }

                // Battles
                if (battlesWon > 0)
                {
                    sb.Append($"Battles won: {battlesWon}. ");
                }

                // Announce
                string announcement = sb.ToString().Trim();
                if (!string.IsNullOrEmpty(announcement))
                {
                    MonsterTrainAccessibility.LogInfo($"Game over auto-read: {announcement}");
                    MonsterTrainAccessibility.ScreenReader?.Speak(announcement, false);
                }
                else
                {
                    // Fallback
                    MonsterTrainAccessibility.ScreenReader?.Speak("Run complete. Press T to read stats.", false);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in AutoReadGameOverScreen: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Run complete.", false);
            }
        }
    }

    /// <summary>
    /// Detect settings screen
    /// </summary>
    public static class SettingsScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("SettingsScreen");
                if (targetType == null)
                {
                    targetType = AccessTools.TypeByName("OptionsScreen");
                }

                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Show") ??
                                 AccessTools.Method(targetType, "Open");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(SettingsScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched {targetType.Name}.{method.Name}");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("SettingsScreen methods not found");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch SettingsScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Settings. Press Tab to switch between tabs.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in SettingsScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Generic screen manager patch to catch all screen transitions
    /// </summary>
    public static class ScreenManagerPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("ScreenManager");
                if (targetType != null)
                {
                    // Try to find the method that handles screen changes
                    var method = AccessTools.Method(targetType, "ChangeScreen") ??
                                 AccessTools.Method(targetType, "LoadScreen") ??
                                 AccessTools.Method(targetType, "ShowScreen");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(ScreenManagerPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched ScreenManager.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch ScreenManager: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                // Could log screen transitions for debugging
                MonsterTrainAccessibility.LogInfo("Screen transition detected");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ScreenManager patch: {ex.Message}");
            }
        }
    }
}
