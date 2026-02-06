using HarmonyLib;
using MonsterTrainAccessibility.Core;
using MonsterTrainAccessibility.Help;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
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
                                    var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;

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
                var sb = new System.Text.StringBuilder();
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

                // Try to get map progress from the MapScreen instance
                string progressInfo = GetMapProgress(__instance);
                if (!string.IsNullOrEmpty(progressInfo))
                {
                    MonsterTrainAccessibility.ScreenReader?.AnnounceScreen($"Map. {progressInfo} Press F1 for help.");
                }
                else
                {
                    MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Map. Press F1 for help.");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in MapScreen patch: {ex.Message}");
            }
        }

        private static string GetMapProgress(object mapScreen)
        {
            try
            {
                var type = mapScreen.GetType();

                // Try to get saveManager field
                var saveManagerField = type.GetField("saveManager", BindingFlags.NonPublic | BindingFlags.Instance);
                if (saveManagerField == null) return null;

                var saveManager = saveManagerField.GetValue(mapScreen);
                if (saveManager == null) return null;

                var saveManagerType = saveManager.GetType();

                // Get current distance (ring/section)
                var getCurrentDistanceMethod = saveManagerType.GetMethod("GetCurrentDistance");
                var getRunLengthMethod = saveManagerType.GetMethod("GetRunLength");

                if (getCurrentDistanceMethod == null || getRunLengthMethod == null)
                    return null;

                int currentDistance = (int)getCurrentDistanceMethod.Invoke(saveManager, null);
                int runLength = (int)getRunLengthMethod.Invoke(saveManager, null);

                // Ring is 1-indexed for user display
                int currentRing = currentDistance + 1;
                int totalRings = runLength;

                // Check victory state
                var getVictorySectionStateMethod = saveManagerType.GetMethod("GetVictorySectionState");
                if (getVictorySectionStateMethod != null)
                {
                    var victoryState = getVictorySectionStateMethod.Invoke(saveManager, null);
                    string victoryStateName = victoryState?.ToString() ?? "";

                    if (victoryStateName == "Victory")
                    {
                        return "Victory!";
                    }
                    else if (victoryStateName == "PreHellforgedBoss")
                    {
                        return $"Ring {currentRing} of {totalRings}. Final boss ahead.";
                    }
                }

                // Try to get available map nodes/paths
                string pathInfo = GetAvailablePaths(mapScreen, type);
                if (!string.IsNullOrEmpty(pathInfo))
                {
                    return $"Ring {currentRing} of {totalRings}. {pathInfo}";
                }

                return $"Ring {currentRing} of {totalRings}.";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting map progress: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get descriptions of available map paths/nodes
        /// </summary>
        private static string GetAvailablePaths(object mapScreen, Type screenType)
        {
            try
            {
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Try to get available nodes
                var nodesField = screenType.GetField("availableNodes", bindingFlags) ??
                                 screenType.GetField("selectableNodes", bindingFlags) ??
                                 screenType.GetField("currentNodes", bindingFlags) ??
                                 screenType.GetField("_availableNodes", bindingFlags);

                if (nodesField != null)
                {
                    var nodes = nodesField.GetValue(mapScreen) as System.Collections.IList;
                    if (nodes != null && nodes.Count > 0)
                    {
                        var nodeNames = new List<string>();
                        foreach (var node in nodes)
                        {
                            if (node == null) continue;
                            string nodeName = GetMapNodeName(node);
                            if (!string.IsNullOrEmpty(nodeName))
                            {
                                nodeNames.Add(nodeName);
                            }
                        }

                        if (nodeNames.Count > 0)
                        {
                            return $"{nodeNames.Count} paths: {string.Join(", ", nodeNames)}.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting map paths: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get a readable name for a map node
        /// </summary>
        private static string GetMapNodeName(object node)
        {
            try
            {
                var nodeType = node.GetType();

                // Try GetNodeData or similar
                var getDataMethod = nodeType.GetMethod("GetMapNodeData", Type.EmptyTypes) ??
                                    nodeType.GetMethod("GetNodeData", Type.EmptyTypes);

                object nodeData = getDataMethod?.Invoke(node, null) ?? node;
                var dataType = nodeData.GetType();
                string typeName = dataType.Name;

                // Map known node data types to readable names
                if (typeName.Contains("Battle") || typeName.Contains("Combat")) return "Battle";
                if (typeName.Contains("Merchant") || typeName.Contains("Shop")) return "Shop";
                if (typeName.Contains("Event") || typeName.Contains("Story")) return "Event";
                if (typeName.Contains("Relic") || typeName.Contains("Artifact")) return "Artifact";
                if (typeName.Contains("Upgrade") || typeName.Contains("Enhancer")) return "Upgrade";
                if (typeName.Contains("Purge")) return "Purge";
                if (typeName.Contains("Pact") || typeName.Contains("Divine")) return "Divine";
                if (typeName.Contains("Reward")) return "Reward";

                // Try to get the name from the node data
                var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    string name = getNameMethod.Invoke(nodeData, null) as string;
                    if (!string.IsNullOrEmpty(name) && !name.Contains("_"))
                        return Screens.BattleAccessibility.StripRichTextTags(name);
                }

                return typeName.Replace("MapNodeData", "").Replace("Data", "");
            }
            catch { }
            return null;
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
                var sb = new System.Text.StringBuilder();
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

                            var itemParts = new System.Collections.Generic.List<string>();
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

                // Auto-read the game over screen with full details
                AutoReadGameOverScreen(__instance);

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
                var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

                // Get key data from the screen object and its base classes
                string victoryTypeStr = null;
                int finalScore = 0;
                string mainClassName = null;
                string subClassName = null;
                int covenantLevel = 0;
                int battlesWon = 0;
                double runTimeSeconds = 0;
                int gold = 0;

                // Check all types in inheritance chain (GameOverScreen extends GameOverScreenBase)
                var currentType = screenType;
                while (currentType != null)
                {
                    // Get victoryType field (SaveManager.VictoryType enum)
                    var victoryTypeField = currentType.GetField("victoryType", bindingFlags);
                    if (victoryTypeField != null && victoryTypeStr == null)
                    {
                        var vt = victoryTypeField.GetValue(screen);
                        if (vt != null)
                        {
                            victoryTypeStr = vt.ToString();
                        }
                    }

                    // Get finalScore field
                    var finalScoreField = currentType.GetField("finalScore", bindingFlags);
                    if (finalScoreField != null && finalScore == 0)
                    {
                        var fs = finalScoreField.GetValue(screen);
                        if (fs is int score) finalScore = score;
                    }

                    // Get currentRunData (RunAggregateData) for detailed info
                    var runDataField = currentType.GetField("currentRunData", bindingFlags);
                    if (runDataField != null)
                    {
                        var runData = runDataField.GetValue(screen);
                        if (runData != null)
                        {
                            var runDataType = runData.GetType();

                            // Get battles won
                            var getBattlesMethod = runDataType.GetMethod("GetNumBattlesWon");
                            if (getBattlesMethod != null)
                            {
                                var result = getBattlesMethod.Invoke(runData, null);
                                if (result is int b) battlesWon = b;
                            }

                            // Get run time
                            var getRunTimeMethod = runDataType.GetMethod("GetRunTime");
                            if (getRunTimeMethod != null)
                            {
                                var result = getRunTimeMethod.Invoke(runData, null);
                                if (result is TimeSpan ts) runTimeSeconds = ts.TotalSeconds;
                            }

                            // Get gold
                            var getGoldMethod = runDataType.GetMethod("GetFinalGold");
                            if (getGoldMethod != null)
                            {
                                var result = getGoldMethod.Invoke(runData, null);
                                if (result is int g) gold = g;
                            }

                            // Get covenant/ascension level
                            var getAscensionMethod = runDataType.GetMethod("GetAscensionLevel");
                            if (getAscensionMethod != null)
                            {
                                var result = getAscensionMethod.Invoke(runData, null);
                                if (result is int a) covenantLevel = a;
                            }

                            // Get score from run data if not found elsewhere
                            if (finalScore == 0)
                            {
                                var getScoreMethod = runDataType.GetMethod("GetScore");
                                if (getScoreMethod != null)
                                {
                                    var result = getScoreMethod.Invoke(runData, null);
                                    if (result is int s) finalScore = s;
                                }
                            }
                        }
                    }

                    // Get saveManager for clan names
                    var saveManagerField = currentType.GetField("saveManager", bindingFlags);
                    if (saveManagerField != null && mainClassName == null)
                    {
                        var saveManager = saveManagerField.GetValue(screen);
                        if (saveManager != null)
                        {
                            var smType = saveManager.GetType();

                            // Get main class
                            var getMainClassMethod = smType.GetMethod("GetMainClass");
                            if (getMainClassMethod != null)
                            {
                                var mainClass = getMainClassMethod.Invoke(saveManager, null);
                                if (mainClass != null)
                                {
                                    var getTitleMethod = mainClass.GetType().GetMethod("GetTitle");
                                    if (getTitleMethod != null)
                                    {
                                        mainClassName = getTitleMethod.Invoke(mainClass, null) as string;
                                    }
                                }
                            }

                            // Get sub class
                            var getSubClassMethod = smType.GetMethod("GetSubClass");
                            if (getSubClassMethod != null)
                            {
                                var subClass = getSubClassMethod.Invoke(saveManager, null);
                                if (subClass != null)
                                {
                                    var getTitleMethod = subClass.GetType().GetMethod("GetTitle");
                                    if (getTitleMethod != null)
                                    {
                                        subClassName = getTitleMethod.Invoke(subClass, null) as string;
                                    }
                                }
                            }

                            // Get covenant level from saveManager if not found
                            if (covenantLevel == 0)
                            {
                                var getAscensionMethod = smType.GetMethod("GetAscensionLevel");
                                if (getAscensionMethod != null)
                                {
                                    var result = getAscensionMethod.Invoke(saveManager, null);
                                    if (result is int a) covenantLevel = a;
                                }
                            }

                            // Get battles won from saveManager if not found
                            if (battlesWon == 0)
                            {
                                var getBattlesMethod = smType.GetMethod("GetNumBattlesWon");
                                if (getBattlesMethod != null)
                                {
                                    var result = getBattlesMethod.Invoke(saveManager, null);
                                    if (result is int b) battlesWon = b;
                                }
                            }
                        }
                    }

                    currentType = currentType.BaseType;
                }

                // Also try to get the title label text directly
                string titleText = null;
                var titleField = screenType?.GetField("titleLabel", bindingFlags);
                if (titleField != null)
                {
                    var labelObj = titleField.GetValue(screen);
                    if (labelObj != null)
                    {
                        var textProp = labelObj.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            titleText = textProp.GetValue(labelObj) as string;
                        }
                    }
                }

                // Build announcement
                // Victory/Defeat - use title text if available, otherwise infer from victoryType
                if (!string.IsNullOrEmpty(titleText))
                {
                    sb.Append($"{titleText}! ");
                }
                else if (!string.IsNullOrEmpty(victoryTypeStr))
                {
                    if (victoryTypeStr == "Hellforged")
                        sb.Append("Hellforged Victory! ");
                    else if (victoryTypeStr == "Standard")
                        sb.Append("Victory! ");
                    else
                        sb.Append("Defeat. ");
                }
                else
                {
                    sb.Append("Run complete. ");
                }

                // Clans
                if (!string.IsNullOrEmpty(mainClassName) && !string.IsNullOrEmpty(subClassName))
                {
                    sb.Append($"{mainClassName} and {subClassName}. ");
                }
                else if (!string.IsNullOrEmpty(mainClassName))
                {
                    sb.Append($"{mainClassName}. ");
                }

                // Covenant
                if (covenantLevel > 0)
                {
                    sb.Append($"Covenant {covenantLevel}. ");
                }

                // Battles won
                if (battlesWon > 0)
                {
                    sb.Append($"{battlesWon} battles won. ");
                }

                // Score
                if (finalScore > 0)
                {
                    sb.Append($"Score: {finalScore:N0}. ");
                }

                // Gold
                if (gold > 0)
                {
                    sb.Append($"Gold: {gold:N0}. ");
                }

                // Run time
                if (runTimeSeconds > 0)
                {
                    var runTime = TimeSpan.FromSeconds(runTimeSeconds);
                    if (runTime.TotalHours >= 1)
                    {
                        sb.Append($"Time: {(int)runTime.TotalHours} hours {runTime.Minutes} minutes. ");
                    }
                    else
                    {
                        sb.Append($"Time: {runTime.Minutes} minutes {runTime.Seconds} seconds. ");
                    }
                }

                sb.Append("Press R for run summary. ");

                // Announce
                string announcement = sb.ToString().Trim();
                MonsterTrainAccessibility.LogInfo($"Game over auto-read: {announcement}");
                MonsterTrainAccessibility.ScreenReader?.Speak(announcement, false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in AutoReadGameOverScreen: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Run complete. Press R for run summary.", false);
            }
        }
    }

    /// <summary>
    /// Announce stat highlights as they appear on the game over screen.
    /// Hooks StatHighlightUI.AnimateInCoroutine to read the stat when it animates in.
    /// </summary>
    public static class StatHighlightPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("StatHighlightUI");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("StatHighlightUI not found");
                    return;
                }

                // Hook AnimateInCoroutine - this is called when the stat becomes visible
                var animateMethod = AccessTools.Method(targetType, "AnimateInCoroutine");
                if (animateMethod != null)
                {
                    var prefix = new HarmonyMethod(typeof(StatHighlightPatch).GetMethod(nameof(AnimatePrefix)));
                    harmony.Patch(animateMethod, prefix: prefix);
                    MonsterTrainAccessibility.LogInfo("Patched StatHighlightUI.AnimateInCoroutine");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch StatHighlightUI: {ex.Message}");
            }
        }

        // Prefix runs before AnimateInCoroutine - at this point the labels are already set
        public static void AnimatePrefix(object __instance)
        {
            try
            {
                if (__instance == null) return;

                var instanceType = __instance.GetType();
                var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

                string headerText = null;
                string statText = null;

                // Get headerLabel text
                var headerField = instanceType.GetField("headerLabel", bindingFlags);
                if (headerField != null)
                {
                    var label = headerField.GetValue(__instance);
                    if (label != null)
                    {
                        var textProp = label.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            headerText = textProp.GetValue(label) as string;
                        }
                    }
                }

                // Get statLabel text
                var statField = instanceType.GetField("statLabel", bindingFlags);
                if (statField != null)
                {
                    var label = statField.GetValue(__instance);
                    if (label != null)
                    {
                        var textProp = label.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            statText = textProp.GetValue(label) as string;
                        }
                    }
                }

                // Build and announce
                if (!string.IsNullOrEmpty(headerText) || !string.IsNullOrEmpty(statText))
                {
                    // Clean rich text tags
                    headerText = Screens.BattleAccessibility.StripRichTextTags(headerText ?? "");
                    statText = Screens.BattleAccessibility.StripRichTextTags(statText ?? "");

                    // Remove newlines for cleaner speech
                    statText = statText.Replace("\n", " ").Replace("\r", "");

                    string announcement = $"{headerText}: {statText}".Trim();
                    MonsterTrainAccessibility.LogInfo($"Stat highlight: {announcement}");
                    MonsterTrainAccessibility.ScreenReader?.Queue(announcement);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in StatHighlightPatch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Announce win streak when it appears on the game over screen.
    /// </summary>
    public static class WinStreakPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("WinStreakIncreaseUI");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("WinStreakIncreaseUI not found");
                    return;
                }

                // Hook AnimateInCoroutine
                var animateMethod = AccessTools.Method(targetType, "AnimateInCoroutine");
                if (animateMethod != null)
                {
                    var prefix = new HarmonyMethod(typeof(WinStreakPatch).GetMethod(nameof(AnimatePrefix)));
                    harmony.Patch(animateMethod, prefix: prefix);
                    MonsterTrainAccessibility.LogInfo("Patched WinStreakIncreaseUI.AnimateInCoroutine");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch WinStreakIncreaseUI: {ex.Message}");
            }
        }

        public static void AnimatePrefix(object __instance)
        {
            try
            {
                if (__instance == null) return;

                var instanceType = __instance.GetType();
                var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;

                string countText = null;
                string previousText = null;

                // Get countLabel from base WinStreakUI class
                var countField = instanceType.GetField("countLabel", bindingFlags);
                if (countField == null)
                {
                    // Try base type
                    countField = instanceType.BaseType?.GetField("countLabel", bindingFlags);
                }
                if (countField != null)
                {
                    var label = countField.GetValue(__instance);
                    if (label != null)
                    {
                        var textProp = label.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            countText = textProp.GetValue(label) as string;
                        }
                    }
                }

                // Get previousWinStreakLabel
                var prevField = instanceType.GetField("previousWinStreakLabel", bindingFlags);
                if (prevField != null)
                {
                    var label = prevField.GetValue(__instance);
                    if (label != null)
                    {
                        var textProp = label.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            previousText = textProp.GetValue(label) as string;
                        }
                    }
                }

                // Build announcement
                if (!string.IsNullOrEmpty(countText))
                {
                    countText = Screens.BattleAccessibility.StripRichTextTags(countText);
                    string announcement;
                    if (!string.IsNullOrEmpty(previousText))
                    {
                        previousText = Screens.BattleAccessibility.StripRichTextTags(previousText);
                        announcement = $"Win streak increased! {previousText} to {countText}";
                    }
                    else
                    {
                        announcement = $"Win streak: {countText}";
                    }
                    MonsterTrainAccessibility.LogInfo($"Win streak: {announcement}");
                    MonsterTrainAccessibility.ScreenReader?.Queue(announcement);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in WinStreakPatch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Announce unlocks (level ups, new cards/relics, clan unlocks, etc.) as they appear.
    /// </summary>
    public static class UnlockScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("UnlockScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("UnlockScreen not found");
                    return;
                }

                // Hook ShowNextUnlock - this is called each time an unlock is shown
                var showMethod = AccessTools.Method(targetType, "ShowNextUnlock");
                if (showMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(UnlockScreenPatch).GetMethod(nameof(ShowNextUnlockPostfix)));
                    harmony.Patch(showMethod, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched UnlockScreen.ShowNextUnlock");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch UnlockScreen: {ex.Message}");
            }
        }

        public static void ShowNextUnlockPostfix(object __instance)
        {
            try
            {
                if (__instance == null) return;

                var instanceType = __instance.GetType();
                var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

                // Get currentItem (UnlockDisplayData) via the property
                var currentItemProp = instanceType.GetProperty("currentItem", bindingFlags);
                if (currentItemProp == null) return;

                var currentItem = currentItemProp.GetValue(__instance);
                if (currentItem == null) return;

                var itemType = currentItem.GetType();

                // Get source (UnlockSource enum)
                var sourceField = itemType.GetField("source");
                string source = sourceField?.GetValue(currentItem)?.ToString() ?? "";

                // Get headerTextContent (clan name, etc.)
                var headerContentField = itemType.GetField("headerTextContent");
                string headerContent = headerContentField?.GetValue(currentItem) as string ?? "";

                // Get headerLevel
                var headerLevelField = itemType.GetField("headerLevel");
                int headerLevel = -1;
                if (headerLevelField != null)
                {
                    var lvl = headerLevelField.GetValue(currentItem);
                    if (lvl is int l) headerLevel = l;
                }

                // Get unlocked card name
                string unlockedCardName = null;
                var cardDataField = itemType.GetField("unlockedCardData");
                if (cardDataField != null)
                {
                    var cardData = cardDataField.GetValue(currentItem);
                    if (cardData != null)
                    {
                        var getNameMethod = cardData.GetType().GetMethod("GetName");
                        if (getNameMethod != null)
                        {
                            unlockedCardName = getNameMethod.Invoke(cardData, null) as string;
                        }
                    }
                }

                // Get unlocked relic name
                string unlockedRelicName = null;
                var relicDataField = itemType.GetField("unlockedRelicData");
                if (relicDataField != null)
                {
                    var relicData = relicDataField.GetValue(currentItem);
                    if (relicData != null)
                    {
                        var getNameMethod = relicData.GetType().GetMethod("GetName");
                        if (getNameMethod != null)
                        {
                            unlockedRelicName = getNameMethod.Invoke(relicData, null) as string;
                        }
                    }
                }

                // Get unlocked feature data (title)
                string featureTitle = null;
                var featureDataField = itemType.GetField("unlockedFeatureData");
                if (featureDataField != null)
                {
                    var featureData = featureDataField.GetValue(currentItem);
                    if (featureData != null)
                    {
                        var titleField = featureData.GetType().GetField("title");
                        if (titleField != null)
                        {
                            featureTitle = titleField.GetValue(featureData) as string;
                        }
                    }
                }

                // Build announcement based on source type
                string announcement = BuildUnlockAnnouncement(source, headerContent, headerLevel,
                    unlockedCardName, unlockedRelicName, featureTitle);

                if (!string.IsNullOrEmpty(announcement))
                {
                    MonsterTrainAccessibility.LogInfo($"Unlock: {announcement}");
                    MonsterTrainAccessibility.ScreenReader?.Speak(announcement, false);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in UnlockScreenPatch: {ex.Message}");
            }
        }

        private static string BuildUnlockAnnouncement(string source, string headerContent, int headerLevel,
            string cardName, string relicName, string featureTitle)
        {
            var sb = new System.Text.StringBuilder();

            switch (source)
            {
                case "ClanLevelUp":
                    sb.Append($"{headerContent} reached level {headerLevel}! ");
                    if (!string.IsNullOrEmpty(cardName))
                        sb.Append($"Unlocked card: {cardName}. ");
                    if (!string.IsNullOrEmpty(relicName))
                        sb.Append($"Unlocked artifact: {relicName}. ");
                    if (!string.IsNullOrEmpty(featureTitle))
                        sb.Append($"Unlocked: {featureTitle}. ");
                    break;

                case "NewClan":
                    sb.Append($"New clan unlocked: {featureTitle ?? headerContent}! ");
                    break;

                case "CovenantUnlocked":
                    sb.Append($"Covenant mode unlocked! {featureTitle}");
                    break;

                case "ChallengeLevelUp":
                    sb.Append($"New covenant level unlocked! {featureTitle}");
                    break;

                case "CardMastery":
                    sb.Append("Card mastery achieved! ");
                    break;

                case "DivineCardMastery":
                    sb.Append("Divine card mastery achieved! ");
                    break;

                case "FeatureUnlocked":
                    sb.Append($"Feature unlocked: {featureTitle}! ");
                    break;

                case "MasteryCardFrameUnlocked":
                    sb.Append("Mastery card frame unlocked! ");
                    break;

                default:
                    if (!string.IsNullOrEmpty(cardName))
                        sb.Append($"Unlocked card: {cardName}. ");
                    else if (!string.IsNullOrEmpty(relicName))
                        sb.Append($"Unlocked artifact: {relicName}. ");
                    else if (!string.IsNullOrEmpty(featureTitle))
                        sb.Append($"Unlocked: {featureTitle}. ");
                    break;
            }

            sb.Append("Press Enter to continue.");
            return sb.ToString().Trim();
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
    /// Detect when compendium/logbook screen is shown
    /// </summary>
    public static class CompendiumScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CompendiumScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(CompendiumScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched CompendiumScreen.Initialize");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CompendiumScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Compendium);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Logbook. Use Page Up and Page Down to switch sections. Left and Right arrows to turn pages.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CompendiumScreen patch: {ex.Message}");
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

    /// <summary>
    /// Detect reward screen (post-battle rewards)
    /// </summary>
    public static class RewardScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("RewardScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(RewardScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched RewardScreen.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch RewardScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Rewards);
                AutoReadRewards(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RewardScreen patch: {ex.Message}");
            }
        }

        private static void AutoReadRewards(object screen)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("Rewards. ");

                var screenType = screen.GetType();
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Try to get reward list
                var rewardsField = screenType.GetField("rewards", bindingFlags) ??
                                   screenType.GetField("_rewards", bindingFlags) ??
                                   screenType.GetField("rewardStates", bindingFlags);

                if (rewardsField != null)
                {
                    var rewards = rewardsField.GetValue(screen) as System.Collections.IList;
                    if (rewards != null && rewards.Count > 0)
                    {
                        sb.Append($"{rewards.Count} rewards: ");
                        foreach (var reward in rewards)
                        {
                            string rewardName = GetRewardDisplayName(reward);
                            if (!string.IsNullOrEmpty(rewardName))
                            {
                                sb.Append($"{rewardName}, ");
                            }
                        }
                    }
                }

                sb.Append("Navigate with arrows. Enter to collect.");
                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error auto-reading rewards: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Rewards. Navigate with arrows. Enter to collect.", false);
            }
        }

        private static string GetRewardDisplayName(object reward)
        {
            if (reward == null) return null;

            try
            {
                var rewardType = reward.GetType();

                // Try to get reward data from RewardState
                var getDataMethod = rewardType.GetMethod("GetRewardData") ??
                                    rewardType.GetMethod("GetData");
                object rewardData = getDataMethod?.Invoke(reward, null) ?? reward;

                var dataType = rewardData.GetType();
                string typeName = dataType.Name;

                // Map type names to readable names
                if (typeName.Contains("Gold")) return "Gold";
                if (typeName.Contains("Health")) return "Pyre Health";
                if (typeName.Contains("Crystal")) return "Crystals";
                if (typeName.Contains("RelicDraft") || typeName.Contains("RelicPool")) return "Artifact Choice";
                if (typeName.Contains("Relic")) return "Artifact";
                if (typeName.Contains("CardPool") || typeName.Contains("Draft")) return "Card Draft";
                if (typeName.Contains("Card")) return "Card";
                if (typeName.Contains("Enhancer") || typeName.Contains("Upgrade")) return "Upgrade";
                if (typeName.Contains("Purge")) return "Card Purge";
                if (typeName.Contains("Synthesis")) return "Unit Synthesis";
                if (typeName.Contains("ChampionUpgrade")) return "Champion Upgrade";
                if (typeName.Contains("Merchant")) return "Shop";
                if (typeName.Contains("MapSkip")) return "Map Skip";

                // Try GetTitle for any other type
                var getTitleMethod = dataType.GetMethod("GetTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    string title = getTitleMethod.Invoke(rewardData, null) as string;
                    if (!string.IsNullOrEmpty(title) && !title.Contains("_"))
                        return title;
                }

                // Fallback: clean up type name
                return typeName.Replace("RewardData", "").Replace("RewardState", "").Replace("Data", "");
            }
            catch
            {
                return "Reward";
            }
        }
    }

    /// <summary>
    /// Detect artifact/relic draft screen
    /// </summary>
    public static class RelicDraftScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("RelicDraftScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(RelicDraftScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched RelicDraftScreen.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch RelicDraftScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.RelicDraft);
                AutoReadRelicDraft(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RelicDraftScreen patch: {ex.Message}");
            }
        }

        private static void AutoReadRelicDraft(object screen)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("Artifact Draft. ");

                var screenType = screen.GetType();
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Try to get relic choices
                var relicsField = screenType.GetField("relicChoices", bindingFlags) ??
                                  screenType.GetField("relics", bindingFlags) ??
                                  screenType.GetField("_relics", bindingFlags) ??
                                  screenType.GetField("draftRelics", bindingFlags);

                if (relicsField != null)
                {
                    var relics = relicsField.GetValue(screen) as System.Collections.IList;
                    if (relics != null && relics.Count > 0)
                    {
                        sb.Append($"{relics.Count} artifacts: ");
                        foreach (var relic in relics)
                        {
                            string name = GetRelicName(relic);
                            if (!string.IsNullOrEmpty(name))
                            {
                                sb.Append($"{name}, ");
                            }
                        }
                    }
                }

                sb.Append("Left and Right to browse. Enter to select. Escape to skip.");
                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error auto-reading relic draft: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Artifact Draft. Left and Right to browse. Enter to select.", false);
            }
        }

        private static string GetRelicName(object relic)
        {
            if (relic == null) return null;
            try
            {
                var relicType = relic.GetType();
                var getNameMethod = relicType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    return getNameMethod.Invoke(relic, null) as string;
                }

                // Try name field
                var nameField = relicType.GetField("name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return nameField?.GetValue(relic) as string;
            }
            catch { return null; }
        }
    }

    /// <summary>
    /// Detect story event screen
    /// </summary>
    public static class StoryEventScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("StoryEventScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(StoryEventScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched StoryEventScreen.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch StoryEventScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Event);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Event. Navigate choices with arrows. Enter to select. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in StoryEventScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect champion upgrade screen
    /// </summary>
    public static class ChampionUpgradeScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("ChampionUpgradeScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(ChampionUpgradeScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched ChampionUpgradeScreen.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch ChampionUpgradeScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.ChampionUpgrade);
                AutoReadChampionUpgrade(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ChampionUpgradeScreen patch: {ex.Message}");
            }
        }

        private static void AutoReadChampionUpgrade(object screen)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("Champion Upgrade. ");

                var screenType = screen.GetType();
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Try to get upgrade tree data
                var treeField = screenType.GetField("upgradeTree", bindingFlags) ??
                                screenType.GetField("upgradeTreeData", bindingFlags) ??
                                screenType.GetField("_upgradeTreeData", bindingFlags);

                if (treeField != null)
                {
                    var treeData = treeField.GetValue(screen);
                    if (treeData != null)
                    {
                        // Try to get champion name
                        var getNameMethod = treeData.GetType().GetMethod("GetName", Type.EmptyTypes);
                        if (getNameMethod != null)
                        {
                            string name = getNameMethod.Invoke(treeData, null) as string;
                            if (!string.IsNullOrEmpty(name))
                            {
                                sb.Append($"Champion: {name}. ");
                            }
                        }
                    }
                }

                // Try to get upgrade choices count
                var choicesField = screenType.GetField("upgradeChoices", bindingFlags) ??
                                   screenType.GetField("choices", bindingFlags) ??
                                   screenType.GetField("_choices", bindingFlags);

                if (choicesField != null)
                {
                    var choices = choicesField.GetValue(screen) as System.Collections.IList;
                    if (choices != null)
                    {
                        sb.Append($"{choices.Count} upgrade paths available. ");
                    }
                }

                sb.Append("Left and Right to browse. Enter to select. Press F1 for help.");
                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error auto-reading champion upgrade: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Champion Upgrade. Left and Right to browse. Enter to select.", false);
            }
        }
    }

    /// <summary>
    /// Detect deck/card list screen (deck view, purge, upgrade, draw pile, discard pile)
    /// </summary>
    public static class DeckScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("DeckScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(DeckScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched DeckScreen.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch DeckScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.DeckView);
                AutoReadDeckScreen(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in DeckScreen patch: {ex.Message}");
            }
        }

        private static void AutoReadDeckScreen(object screen)
        {
            try
            {
                var screenType = screen.GetType();
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Try to determine the mode/source
                string modeStr = null;
                var modeField = screenType.GetField("mode", bindingFlags) ??
                                screenType.GetField("_mode", bindingFlags) ??
                                screenType.GetField("source", bindingFlags) ??
                                screenType.GetField("_source", bindingFlags);

                if (modeField != null)
                {
                    var modeValue = modeField.GetValue(screen);
                    modeStr = modeValue?.ToString();
                }

                // Get card count
                int cardCount = 0;
                var cardsField = screenType.GetField("cards", bindingFlags) ??
                                 screenType.GetField("_cards", bindingFlags) ??
                                 screenType.GetField("cardStates", bindingFlags);

                if (cardsField != null)
                {
                    var cards = cardsField.GetValue(screen) as System.Collections.IList;
                    if (cards != null)
                        cardCount = cards.Count;
                }

                // Build context-dependent announcement
                string announcement;
                if (!string.IsNullOrEmpty(modeStr))
                {
                    string modeLower = modeStr.ToLower();
                    if (modeLower.Contains("purge") || modeLower.Contains("remove"))
                        announcement = $"Card Purge. Select a card to remove. {cardCount} cards.";
                    else if (modeLower.Contains("upgrade") || modeLower.Contains("enhance"))
                        announcement = $"Card Upgrade. Select a card to enhance. {cardCount} cards.";
                    else if (modeLower.Contains("draw"))
                        announcement = $"Draw Pile. {cardCount} cards.";
                    else if (modeLower.Contains("discard"))
                        announcement = $"Discard Pile. {cardCount} cards.";
                    else
                        announcement = cardCount > 0 ? $"Deck. {cardCount} cards." : "Deck.";
                }
                else
                {
                    announcement = cardCount > 0 ? $"Deck. {cardCount} cards." : "Deck.";
                }

                announcement += " Arrow keys to browse cards. Press F1 for help.";
                MonsterTrainAccessibility.ScreenReader?.Speak(announcement, false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error auto-reading deck screen: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Deck. Arrow keys to browse cards.", false);
            }
        }
    }

    /// <summary>
    /// Detect unit synthesis screen (DLC)
    /// </summary>
    public static class SynthesisScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("SynthesisScreen");
                if (targetType == null)
                {
                    // DLC type may not be present
                    MonsterTrainAccessibility.LogInfo("SynthesisScreen not found (DLC may not be installed)");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");
                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(SynthesisScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched SynthesisScreen.{method.Name}");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch SynthesisScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Synthesis);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Unit Synthesis. Arrow keys to browse units. Enter to select. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in SynthesisScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect dialog/popup screen
    /// </summary>
    public static class DialogScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("DialogScreen");
                if (targetType == null)
                {
                    targetType = AccessTools.TypeByName("DialogPopup");
                }

                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "ShowDialog") ??
                                 AccessTools.Method(targetType, "Show") ??
                                 AccessTools.Method(targetType, "Initialize");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(DialogScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched {targetType.Name}.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch DialogScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Dialog);
                AutoReadDialog(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in DialogScreen patch: {ex.Message}");
            }
        }

        private static void AutoReadDialog(object screen)
        {
            try
            {
                var screenType = screen.GetType();
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                var sb = new StringBuilder();
                sb.Append("Dialog. ");

                // Try to get dialog content text
                string[] textFieldNames = { "contentText", "messageText", "bodyText", "dialogText", "content", "_content", "message", "_message" };
                foreach (var fieldName in textFieldNames)
                {
                    var field = screenType.GetField(fieldName, bindingFlags);
                    if (field != null)
                    {
                        var value = field.GetValue(screen);
                        if (value != null)
                        {
                            // Could be a TMP_Text component or a string
                            string text = null;
                            if (value is string str)
                            {
                                text = str;
                            }
                            else
                            {
                                var textProp = value.GetType().GetProperty("text");
                                if (textProp != null)
                                {
                                    text = textProp.GetValue(value) as string;
                                }
                            }

                            if (!string.IsNullOrEmpty(text))
                            {
                                sb.Append(Screens.BattleAccessibility.StripRichTextTags(text));
                                sb.Append(" ");
                                break;
                            }
                        }
                    }
                }

                sb.Append("Press Enter to confirm or Escape to cancel.");
                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error auto-reading dialog: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Dialog. Press Enter to confirm or Escape to cancel.", false);
            }
        }
    }

    /// <summary>
    /// Detect run history screen
    /// </summary>
    public static class RunHistoryScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetNames = new[] { "RunHistoryScreen", "RunLogScreen", "PastRunsScreen" };
                foreach (var name in targetNames)
                {
                    var targetType = AccessTools.TypeByName(name);
                    if (targetType != null)
                    {
                        var method = AccessTools.Method(targetType, "Setup") ??
                                     AccessTools.Method(targetType, "Show");
                        if (method != null)
                        {
                            var postfix = new HarmonyMethod(typeof(RunHistoryScreenPatch).GetMethod(nameof(Postfix)));
                            harmony.Patch(method, postfix: postfix);
                            MonsterTrainAccessibility.LogInfo($"Patched {name}.{method.Name}");
                            return;
                        }
                    }
                }
                MonsterTrainAccessibility.LogInfo("RunHistoryScreen not found");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch RunHistoryScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.RunHistory);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Run History. Use arrows to browse runs.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RunHistoryScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect challenge details screen
    /// </summary>
    public static class ChallengeDetailsScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("ChallengeDetailsScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(ChallengeDetailsScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched ChallengeDetailsScreen.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch ChallengeDetailsScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.ChallengeDetails);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Challenge Details.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ChallengeDetailsScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect challenge overview screen
    /// </summary>
    public static class ChallengeOverviewScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("ChallengeOverviewScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(ChallengeOverviewScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched ChallengeOverviewScreen.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch ChallengeOverviewScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.ChallengeOverview);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Challenges.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ChallengeOverviewScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect minimap screen
    /// </summary>
    public static class MinimapScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("MinimapScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(MinimapScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched MinimapScreen.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch MinimapScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Minimap);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Minimap.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in MinimapScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect credits screen
    /// </summary>
    public static class CreditsScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CreditsScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(CreditsScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched CreditsScreen.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CreditsScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Credits);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Credits. Press Escape to return.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CreditsScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect key mapping screen
    /// </summary>
    public static class KeyMappingScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetNames = new[] { "KeyMappingScreen", "KeyBindingsScreen", "ControlsScreen", "InputMappingScreen" };
                foreach (var name in targetNames)
                {
                    var targetType = AccessTools.TypeByName(name);
                    if (targetType != null)
                    {
                        var method = AccessTools.Method(targetType, "Initialize") ??
                                     AccessTools.Method(targetType, "Show") ??
                                     AccessTools.Method(targetType, "Setup");
                        if (method != null)
                        {
                            var postfix = new HarmonyMethod(typeof(KeyMappingScreenPatch).GetMethod(nameof(Postfix)));
                            harmony.Patch(method, postfix: postfix);
                            MonsterTrainAccessibility.LogInfo($"Patched {name}.{method.Name}");
                            return;
                        }
                    }
                }
                MonsterTrainAccessibility.LogInfo("KeyMappingScreen not found");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch KeyMappingScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.KeyMapping);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Key Mapping. Use arrows to navigate.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in KeyMappingScreen patch: {ex.Message}");
            }
        }
    }
}
