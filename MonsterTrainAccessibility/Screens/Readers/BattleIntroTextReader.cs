using MonsterTrainAccessibility.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System;
using UnityEngine.UI;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Extracted reader for BattleIntro UI elements.
    /// </summary>
    internal static class BattleIntroTextReader
    {

        /// <summary>
        /// Get battle info when on the Fight button of BattleIntro screen
        /// </summary>
        internal static string GetBattleIntroText(GameObject go)
        {
            try
            {
                // Check if this is the Fight button
                string goName = go.name.ToLower();
                if (!goName.Contains("fight"))
                    return null;

                // Look for BattleIntroScreen component in parents
                Component battleIntroScreen = null;
                Transform current = go.transform;

                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;
                        if (typeName == "BattleIntroScreen")
                        {
                            battleIntroScreen = component;
                            break;
                        }
                    }
                    if (battleIntroScreen != null) break;
                    current = current.parent;
                }

                if (battleIntroScreen == null)
                    return null;

                // Try to get the scenario/battle info from BattleIntroScreen
                var screenType = battleIntroScreen.GetType();

                // Log all fields on BattleIntroScreen to find scenario data
                LogScreenFields(screenType, battleIntroScreen);

                // Try to find scenario-specific text - look for labels that might contain wave info
                string scenarioName = null;
                string scenarioDescription = null;
                string battleMetadata = null;

                // Try to get ScenarioData from BattleIntroScreen
                scenarioName = GetScenarioNameFromScreen(battleIntroScreen, screenType);
                if (!string.IsNullOrEmpty(scenarioName))
                {
                    // Also try to get description and metadata from ScenarioData
                    scenarioDescription = GetScenarioDescriptionFromScreen(battleIntroScreen, screenType);
                    battleMetadata = GetBattleMetadataFromScreen(battleIntroScreen, screenType);
                }

                // If we found a scenario name, use it
                if (!string.IsNullOrEmpty(scenarioName))
                {
                    var sb = new StringBuilder();
                    sb.Append("Fight: ");

                    // Add battle type/metadata first if available
                    if (!string.IsNullOrEmpty(battleMetadata))
                    {
                        sb.Append($"{battleMetadata} - ");
                    }

                    sb.Append(scenarioName);

                    if (!string.IsNullOrEmpty(scenarioDescription))
                    {
                        sb.Append($". {scenarioDescription}");
                    }

                    return sb.ToString();
                }

                // Fallback to battleNameLabel if no scenario-specific name found
                string battleName = null;
                var nameField = screenType.GetField("battleNameLabel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (nameField != null)
                {
                    var nameLabel = nameField.GetValue(battleIntroScreen);
                    if (nameLabel != null)
                    {
                        battleName = GetTextFromComponent(nameLabel);
                    }
                }

                if (!string.IsNullOrEmpty(battleName))
                {
                    return $"Fight: {battleName}";
                }

                // Fallback to enemy names if we couldn't get scenario info
                string enemyNames = GetEnemyNamesFromSiblings(go);
                if (!string.IsNullOrEmpty(enemyNames))
                {
                    return $"Fight: {enemyNames}";
                }

                // Fallback - at least indicate it's a battle
                return "Fight: Start Battle";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle intro text: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Get text for RunOpeningScreen (Boss Battles screen shown at start of run)
        /// </summary>
        internal static string GetRunOpeningScreenText(GameObject go)
        {
            try
            {
                // Look for RunOpeningScreen component in parents
                Component runOpeningScreen = null;
                Transform current = go.transform;

                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;
                        if (typeName == "RunOpeningScreen")
                        {
                            runOpeningScreen = component;
                            break;
                        }
                    }
                    if (runOpeningScreen != null) break;
                    current = current.parent;
                }

                if (runOpeningScreen == null)
                    return null;

                var screenType = runOpeningScreen.GetType();
                MonsterTrainAccessibility.LogInfo($"Found RunOpeningScreen component");

                // Build the boss battles text from bossDetailsUIs
                var sb = new StringBuilder();
                sb.Append("Boss Battles. ");

                // Get bossDetailsUIs field - List<BossDetailsUI>
                var bossDetailsField = screenType.GetField("bossDetailsUIs", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MonsterTrainAccessibility.LogInfo($"bossDetailsField found: {bossDetailsField != null}");
                if (bossDetailsField != null)
                {
                    var bossDetailsList = bossDetailsField.GetValue(runOpeningScreen) as System.Collections.IList;
                    MonsterTrainAccessibility.LogInfo($"bossDetailsList count: {bossDetailsList?.Count ?? 0}");
                    if (bossDetailsList != null && bossDetailsList.Count > 0)
                    {
                        for (int i = 0; i < bossDetailsList.Count; i++)
                        {
                            var bossDetailsUI = bossDetailsList[i];
                            if (bossDetailsUI == null) continue;

                            // Add ring label: "Ring 1:", "Ring 2:", ..., last one is "Final Boss:"
                            if (i < bossDetailsList.Count - 1)
                                sb.Append($"Ring {i + 1}: ");
                            else
                                sb.Append("Final Boss: ");

                            string bossInfo = GetBossDetailsUIText(bossDetailsUI);
                            MonsterTrainAccessibility.LogInfo($"BossDetailsUI[{i}] text: '{bossInfo}'");
                            if (!string.IsNullOrEmpty(bossInfo))
                            {
                                sb.Append(bossInfo);
                                if (i < bossDetailsList.Count - 1)
                                    sb.Append(". ");
                            }
                        }

                        string result = sb.ToString().Trim();
                        MonsterTrainAccessibility.LogInfo($"Final boss battles text: '{result}'");
                        if (result.Length > 15) // More than just "Boss Battles. "
                        {
                            // Add button hint
                            sb.Append(". Press Enter to confirm.");
                            return sb.ToString();
                        }
                    }
                }

                // Fallback - try to get text from children
                var screenGo = (runOpeningScreen as MonoBehaviour)?.gameObject;
                if (screenGo != null)
                {
                    var texts = GetAllTextFromChildren(screenGo);
                    if (texts != null && texts.Count > 0)
                    {
                        var meaningfulTexts = texts.Where(t =>
                            !string.IsNullOrWhiteSpace(t) &&
                            t.Length > 2 &&
                            !t.Equals("OK", StringComparison.OrdinalIgnoreCase) &&
                            !t.Equals("Confirm", StringComparison.OrdinalIgnoreCase) &&
                            !t.ToLower().Contains("placeholder")
                        ).ToList();

                        if (meaningfulTexts.Count > 0)
                        {
                            return "Boss Battles. " + string.Join(". ", meaningfulTexts) + ". Press Enter to confirm.";
                        }
                    }
                }

                return "Boss Battles. Press Enter to confirm.";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting run opening screen text: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Extract text from a BossDetailsUI component
        /// </summary>
        internal static string GetBossDetailsUIText(object bossDetailsUI)
        {
            if (bossDetailsUI == null) return null;

            try
            {
                var uiType = bossDetailsUI.GetType();
                var sb = new StringBuilder();

                // Get the title (Ring X: or Final Boss:) from titleLabel
                var titleField = uiType.GetField("titleLabel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (titleField != null)
                {
                    var labelObj = titleField.GetValue(bossDetailsUI);
                    if (labelObj != null)
                    {
                        string titleText = GetTMPTextFromObject(labelObj);
                        if (!string.IsNullOrEmpty(titleText) && !titleText.ToLower().Contains("placeholder"))
                        {
                            sb.Append(titleText);
                        }
                    }
                }

                // Get the boss name from tooltipProvider
                var tooltipField = uiType.GetField("tooltipProvider", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (tooltipField != null)
                {
                    var tooltipProvider = tooltipField.GetValue(bossDetailsUI);
                    if (tooltipProvider != null)
                    {
                        string bossName = GetBossNameFromTooltip(tooltipProvider);
                        MonsterTrainAccessibility.LogInfo($"Boss name from tooltip: '{bossName}'");
                        if (!string.IsNullOrEmpty(bossName) && !bossName.ToLower().Contains("placeholder"))
                        {
                            if (sb.Length > 0) sb.Append(" ");
                            sb.Append(bossName);
                        }
                    }
                }

                string result = sb.ToString();
                MonsterTrainAccessibility.LogInfo($"BossDetailsUI final text: '{result}'");
                return !string.IsNullOrEmpty(result) ? result : null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting boss details UI text: {ex.Message}");
                return null;
            }
        }


        /// <summary>
        /// Extract boss info (name and description) from TooltipProviderComponent
        /// </summary>
        internal static string GetBossNameFromTooltip(object tooltipProvider)
        {
            if (tooltipProvider == null) return null;

            try
            {
                var tooltipType = tooltipProvider.GetType();
                var sb = new StringBuilder();

                // Try to get tooltips list/array - this contains all the boss info
                var tooltipsField = tooltipType.GetField("_tooltips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                                   tooltipType.GetField("tooltips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (tooltipsField != null)
                {
                    var tooltips = tooltipsField.GetValue(tooltipProvider);
                    if (tooltips is System.Collections.IList list && list.Count > 0)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found {list.Count} tooltip entries");

                        foreach (var tooltip in list)
                        {
                            if (tooltip == null) continue;

                            var ttType = tooltip.GetType();

                            // Try to get title
                            string title = null;
                            string[] titleFieldNames = { "title", "titleKey", "_title", "_titleKey", "TitleKey" };
                            foreach (var fieldName in titleFieldNames)
                            {
                                var titleField = ttType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (titleField != null)
                                {
                                    var titleVal = titleField.GetValue(tooltip) as string;
                                    if (!string.IsNullOrEmpty(titleVal))
                                    {
                                        title = TextUtilities.ResolveInlineKeys(titleVal);
                                        title = LocalizationHelper.Localize(title);
                                        if (string.IsNullOrEmpty(title) || (title.Contains("_") && !title.Contains(" ")))
                                            title = titleVal;
                                        title = TextUtilities.ResolveInlineKeys(title);
                                        break;
                                    }
                                }
                            }

                            // Try to get description/body
                            string description = null;
                            string[] descFieldNames = { "body", "description", "descriptionKey", "_description", "_descriptionKey", "bodyKey", "text", "textKey", "DescriptionKey" };
                            foreach (var fieldName in descFieldNames)
                            {
                                var descField = ttType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (descField != null)
                                {
                                    var descVal = descField.GetValue(tooltip) as string;
                                    if (!string.IsNullOrEmpty(descVal))
                                    {
                                        // Resolve KEY>>...<<  patterns first
                                        description = TextUtilities.ResolveInlineKeys(descVal);
                                        // Then try full localization if it still looks like a key
                                        if (description.Contains("_") && !description.Contains(" "))
                                            description = LocalizationHelper.Localize(description) ?? description;
                                        // Clean up description
                                        if (!string.IsNullOrEmpty(description))
                                        {
                                            description = TextUtilities.StripRichTextTags(description);
                                        }
                                        break;
                                    }
                                }
                            }

                            // Also try properties if fields didn't work
                            if (string.IsNullOrEmpty(title))
                            {
                                var titleProp = ttType.GetProperty("Title") ?? ttType.GetProperty("TitleKey");
                                if (titleProp != null)
                                {
                                    var titleVal = titleProp.GetValue(tooltip) as string;
                                    if (!string.IsNullOrEmpty(titleVal))
                                    {
                                        title = LocalizationHelper.Localize(titleVal);
                                        if (string.IsNullOrEmpty(title) || title.Contains("_"))
                                            title = titleVal;
                                    }
                                }
                            }

                            if (string.IsNullOrEmpty(description))
                            {
                                var descProp = ttType.GetProperty("Description") ?? ttType.GetProperty("DescriptionKey") ?? ttType.GetProperty("Body");
                                if (descProp != null)
                                {
                                    var descVal = descProp.GetValue(tooltip) as string;
                                    if (!string.IsNullOrEmpty(descVal))
                                    {
                                        description = LocalizationHelper.Localize(descVal);
                                        if (string.IsNullOrEmpty(description) || description.Contains("_"))
                                            description = descVal;
                                        if (!string.IsNullOrEmpty(description))
                                        {
                                            description = TextUtilities.StripRichTextTags(description);
                                        }
                                    }
                                }
                            }

                            // Build the tooltip text
                            if (!string.IsNullOrEmpty(title) && !title.ToLower().Contains("placeholder"))
                            {
                                if (sb.Length > 0) sb.Append(". ");
                                sb.Append(title);

                                if (!string.IsNullOrEmpty(description) && !description.ToLower().Contains("placeholder"))
                                {
                                    sb.Append(": ");
                                    sb.Append(description);
                                }
                            }
                            else if (!string.IsNullOrEmpty(description) && !description.ToLower().Contains("placeholder"))
                            {
                                if (sb.Length > 0) sb.Append(". ");
                                sb.Append(description);
                            }
                        }
                    }
                }

                // Fallback: Try common tooltip title field names on the provider itself
                if (sb.Length == 0)
                {
                    string[] titleFieldNames = { "tooltipTitleKey", "_tooltipTitleKey", "titleKey", "title", "tooltipTitle" };
                    foreach (var fieldName in titleFieldNames)
                    {
                        var field = tooltipType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            var val = field.GetValue(tooltipProvider);
                            if (val is string key && !string.IsNullOrEmpty(key))
                            {
                                // Try to localize the key
                                string localized = LocalizationHelper.Localize(key);
                                if (!string.IsNullOrEmpty(localized) && !localized.Contains("_") && !localized.Contains("-"))
                                {
                                    return localized;
                                }
                                // If localization fails, return the key if it looks like a name
                                if (!key.Contains("_") && !key.Contains("-"))
                                {
                                    return key;
                                }
                            }
                        }
                    }
                }

                return sb.Length > 0 ? sb.ToString() : null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting boss name from tooltip: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Get text from a TMP label object
        /// </summary>
        internal static string GetTMPTextFromObject(object labelObj)
        {
            if (labelObj == null) return null;

            try
            {
                var labelType = labelObj.GetType();

                // Try text property
                var textProp = labelType.GetProperty("text");
                if (textProp != null)
                {
                    var text = textProp.GetValue(labelObj) as string;
                    if (!string.IsNullOrEmpty(text))
                        return TextUtilities.StripRichTextTags(text);
                }

                // Try GetText method
                var getTextMethod = labelType.GetMethod("GetText", Type.EmptyTypes);
                if (getTextMethod != null)
                {
                    var text = getTextMethod.Invoke(labelObj, null) as string;
                    if (!string.IsNullOrEmpty(text))
                        return TextUtilities.StripRichTextTags(text);
                }
            }
            catch { }

            return null;
        }


        /// <summary>
        /// Find the scenario/wave name in children of BattleIntroScreen
        /// The battleNameLabel shows the boss name, but we want the wave/scenario name
        /// </summary>
        internal static string FindScenarioTextInChildren(Transform root)
        {
            try
            {
                // Look for common patterns that might contain the scenario name
                // Often there's a "waveText", "scenarioText", "encounterText" or similar
                string[] namePatternsToFind = { "wave", "scenario", "encounter", "mission", "stage", "title" };
                string[] namePatternsToExclude = { "boss", "champion" };

                // Collect all text labels with their names
                var textLabels = new Dictionary<string, string>();
                CollectTextLabels(root, textLabels);

                // Log what we found
                foreach (var kvp in textLabels)
                {
                    MonsterTrainAccessibility.LogInfo($"Label [{kvp.Key}]: \"{kvp.Value}\"");
                }

                // First, try to find by label name patterns
                foreach (var pattern in namePatternsToFind)
                {
                    foreach (var kvp in textLabels)
                    {
                        string labelName = kvp.Key.ToLower();
                        if (labelName.Contains(pattern))
                        {
                            // Make sure it's not an excluded pattern
                            bool excluded = false;
                            foreach (var excludePattern in namePatternsToExclude)
                            {
                                if (labelName.Contains(excludePattern))
                                {
                                    excluded = true;
                                    break;
                                }
                            }

                            if (!excluded && !string.IsNullOrEmpty(kvp.Value))
                            {
                                MonsterTrainAccessibility.LogInfo($"Found scenario name via pattern '{pattern}': {kvp.Value}");
                                return kvp.Value;
                            }
                        }
                    }
                }

                // If no pattern match, try to find by looking for text that's NOT the boss name
                // The boss name typically appears in "battleNameLabel" or similar
                // Look for another substantial text that might be the wave name
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error finding scenario text: {ex.Message}");
            }

            return null;
        }


        internal static void CollectTextLabels(Transform node, Dictionary<string, string> labels)
        {
            if (node == null) return;

            // Get text from this node
            string text = UITextHelper.GetTMPTextDirect(node.gameObject);
            if (string.IsNullOrEmpty(text))
            {
                var uiText = node.GetComponent<Text>();
                text = uiText?.text;
            }

            if (!string.IsNullOrEmpty(text) && text.Trim().Length > 0)
            {
                labels[node.name] = text.Trim();
            }

            // Recurse to children
            foreach (Transform child in node)
            {
                if (child.gameObject.activeInHierarchy)
                {
                    CollectTextLabels(child, labels);
                }
            }
        }


        /// <summary>
        /// Log all fields on a screen type for debugging
        /// </summary>
        internal static void LogScreenFields(Type screenType, object screen)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"=== Fields on {screenType.Name} ===");

                var fields = screenType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    try
                    {
                        var value = field.GetValue(screen);
                        string valueStr = value?.ToString() ?? "null";
                        // Truncate long values
                        if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                        sb.AppendLine($"  {field.FieldType.Name} {field.Name} = {valueStr}");
                    }
                    catch { }
                }

                MonsterTrainAccessibility.LogInfo(sb.ToString());
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error logging screen fields: {ex.Message}");
            }
        }


        /// <summary>
        /// Get the scenario name from BattleIntroScreen's ScenarioData or SaveManager
        /// </summary>
        internal static string GetScenarioNameFromScreen(object screen, Type screenType)
        {
            try
            {
                // First try to get SaveManager and get scenario from there
                var saveManagerField = screenType.GetField("saveManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (saveManagerField != null)
                {
                    var saveManager = saveManagerField.GetValue(screen);
                    if (saveManager != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found SaveManager of type {saveManager.GetType().Name}");
                        string name = GetScenarioNameFromSaveManager(saveManager);
                        if (!string.IsNullOrEmpty(name))
                            return name;
                    }
                }

                // Look for scenario-related fields
                string[] scenarioFieldNames = { "scenarioData", "_scenarioData", "scenario", "_scenario",
                    "currentScenario", "_currentScenario", "battleData", "_battleData" };

                foreach (var fieldName in scenarioFieldNames)
                {
                    var field = screenType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var scenarioData = field.GetValue(screen);
                        if (scenarioData != null)
                        {
                            MonsterTrainAccessibility.LogInfo($"Found scenario field: {fieldName} of type {scenarioData.GetType().Name}");
                            string name = GetBattleNameFromScenario(scenarioData);
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }

                // Also check properties
                string[] scenarioPropNames = { "ScenarioData", "Scenario", "CurrentScenario", "BattleData" };
                foreach (var propName in scenarioPropNames)
                {
                    var prop = screenType.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null)
                    {
                        var scenarioData = prop.GetValue(screen);
                        if (scenarioData != null)
                        {
                            MonsterTrainAccessibility.LogInfo($"Found scenario property: {propName} of type {scenarioData.GetType().Name}");
                            string name = GetBattleNameFromScenario(scenarioData);
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scenario name from screen: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Get the scenario name from SaveManager's run state
        /// </summary>
        internal static string GetScenarioNameFromSaveManager(object saveManager)
        {
            try
            {
                var saveManagerType = saveManager.GetType();

                // Log SaveManager fields/methods for debugging
                var methods = saveManagerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                var scenarioMethods = methods.Where(m => m.Name.Contains("Scenario") || m.Name.Contains("Battle") || m.Name.Contains("Wave")).ToList();
                MonsterTrainAccessibility.LogInfo($"SaveManager scenario-related methods: {string.Join(", ", scenarioMethods.Select(m => m.Name))}");

                // Try GetCurrentScenarioData method
                var getCurrentScenarioMethod = saveManagerType.GetMethod("GetCurrentScenarioData", BindingFlags.Public | BindingFlags.Instance);
                if (getCurrentScenarioMethod != null && getCurrentScenarioMethod.GetParameters().Length == 0)
                {
                    var scenarioData = getCurrentScenarioMethod.Invoke(saveManager, null);
                    if (scenarioData != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Got ScenarioData from GetCurrentScenarioData(): {scenarioData.GetType().Name}");
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
                        MonsterTrainAccessibility.LogInfo($"Got ScenarioData from GetScenario(): {scenarioData.GetType().Name}");
                        string name = GetBattleNameFromScenario(scenarioData);
                        if (!string.IsNullOrEmpty(name))
                            return name;
                    }
                }

                // Try to access run state
                string[] runStateFields = { "runState", "_runState", "currentRun", "_currentRun", "activeRun" };
                foreach (var fieldName in runStateFields)
                {
                    var field = saveManagerType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var runState = field.GetValue(saveManager);
                        if (runState != null)
                        {
                            MonsterTrainAccessibility.LogInfo($"Found run state: {fieldName} of type {runState.GetType().Name}");
                            string name = GetScenarioNameFromRunState(runState);
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }

                // Try GetBalanceData for current scenario info
                var getBalanceDataMethod = saveManagerType.GetMethod("GetBalanceData", BindingFlags.Public | BindingFlags.Instance);
                if (getBalanceDataMethod != null && getBalanceDataMethod.GetParameters().Length == 0)
                {
                    var balanceData = getBalanceDataMethod.Invoke(saveManager, null);
                    if (balanceData != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Got BalanceData: {balanceData.GetType().Name}");
                        // BalanceData might have scenario info
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scenario from SaveManager: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Get scenario name from run state object
        /// </summary>
        internal static string GetScenarioNameFromRunState(object runState)
        {
            try
            {
                var runStateType = runState.GetType();
                MonsterTrainAccessibility.LogInfo($"RunState type: {runStateType.Name}");

                // Log fields for debugging
                var fields = runStateType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var scenarioFields = fields.Where(f => f.Name.ToLower().Contains("scenario") || f.Name.ToLower().Contains("battle") || f.Name.ToLower().Contains("wave")).ToList();
                MonsterTrainAccessibility.LogInfo($"RunState scenario-related fields: {string.Join(", ", scenarioFields.Select(f => f.Name))}");

                // Try to get current scenario
                string[] scenarioFieldNames = { "currentScenario", "_currentScenario", "scenario", "_scenario", "battleScenario" };
                foreach (var fieldName in scenarioFieldNames)
                {
                    var field = runStateType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var scenarioData = field.GetValue(runState);
                        if (scenarioData != null)
                        {
                            MonsterTrainAccessibility.LogInfo($"Found scenario in run state: {fieldName}");
                            string name = GetBattleNameFromScenario(scenarioData);
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }

                // Try GetScenario method on run state
                var getScenarioMethod = runStateType.GetMethod("GetScenario", BindingFlags.Public | BindingFlags.Instance);
                if (getScenarioMethod != null && getScenarioMethod.GetParameters().Length == 0)
                {
                    var scenarioData = getScenarioMethod.Invoke(runState, null);
                    if (scenarioData != null)
                    {
                        string name = GetBattleNameFromScenario(scenarioData);
                        if (!string.IsNullOrEmpty(name))
                            return name;
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scenario from run state: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Get the battle name from a ScenarioData object
        /// </summary>
        internal static string GetBattleNameFromScenario(object scenarioData)
        {
            try
            {
                var dataType = scenarioData.GetType();
                MonsterTrainAccessibility.LogInfo($"ScenarioData type: {dataType.Name}");

                // Log fields for debugging
                var fields = dataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MonsterTrainAccessibility.LogInfo($"ScenarioData fields: {string.Join(", ", fields.Select(f => f.Name))}");

                // Try GetBattleName method first
                var getBattleNameMethod = dataType.GetMethod("GetBattleName", BindingFlags.Public | BindingFlags.Instance);
                if (getBattleNameMethod != null && getBattleNameMethod.GetParameters().Length == 0)
                {
                    var result = getBattleNameMethod.Invoke(scenarioData, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                    {
                        MonsterTrainAccessibility.LogInfo($"Got battle name via GetBattleName(): {name}");
                        return name;
                    }
                }

                // Try GetName method
                var getNameMethod = dataType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                {
                    var result = getNameMethod.Invoke(scenarioData, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                    {
                        MonsterTrainAccessibility.LogInfo($"Got battle name via GetName(): {name}");
                        return name;
                    }
                }

                // Try battleNameKey field
                string[] nameFieldNames = { "battleNameKey", "_battleNameKey", "nameKey", "_nameKey" };
                foreach (var fieldName in nameFieldNames)
                {
                    var field = dataType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var key = field.GetValue(scenarioData) as string;
                        if (!string.IsNullOrEmpty(key))
                        {
                            string localized = LocalizationHelper.LocalizeOrNull(key);
                            if (!string.IsNullOrEmpty(localized))
                            {
                                MonsterTrainAccessibility.LogInfo($"Got battle name from {fieldName}: {localized}");
                                return localized;
                            }
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
        /// Get the scenario description from BattleIntroScreen's ScenarioData or SaveManager
        /// </summary>
        internal static string GetScenarioDescriptionFromScreen(object screen, Type screenType)
        {
            try
            {
                // First try to get SaveManager and get scenario description from there
                var saveManagerField = screenType.GetField("saveManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (saveManagerField != null)
                {
                    var saveManager = saveManagerField.GetValue(screen);
                    if (saveManager != null)
                    {
                        string desc = GetScenarioDescriptionFromSaveManager(saveManager);
                        if (!string.IsNullOrEmpty(desc))
                            return desc;
                    }
                }

                // Look for scenario-related fields
                string[] scenarioFieldNames = { "scenarioData", "_scenarioData", "scenario", "_scenario" };

                foreach (var fieldName in scenarioFieldNames)
                {
                    var field = screenType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var scenarioData = field.GetValue(screen);
                        if (scenarioData != null)
                        {
                            return GetBattleDescriptionFromScenario(scenarioData);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scenario description: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Get battle metadata (type, difficulty, ring, etc.) from screen
        /// </summary>
        internal static string GetBattleMetadataFromScreen(object screen, Type screenType)
        {
            try
            {
                var saveManagerField = screenType.GetField("saveManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (saveManagerField == null) return null;

                var saveManager = saveManagerField.GetValue(screen);
                if (saveManager == null) return null;

                var parts = new List<string>();

                // Get current ring/floor
                string ringInfo = GetCurrentRingInfo(saveManager);
                if (!string.IsNullOrEmpty(ringInfo))
                {
                    parts.Add(ringInfo);
                }

                // Get battle type (boss, elite, normal)
                string battleType = GetBattleType(saveManager, screen, screenType);
                if (!string.IsNullOrEmpty(battleType))
                {
                    parts.Add(battleType);
                }

                // Get difficulty info from scenario
                string difficultyInfo = GetScenarioDifficulty(saveManager);
                if (!string.IsNullOrEmpty(difficultyInfo))
                {
                    parts.Add(difficultyInfo);
                }

                if (parts.Count > 0)
                {
                    return string.Join(", ", parts);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle metadata: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Get current ring/floor number
        /// </summary>
        internal static string GetCurrentRingInfo(object saveManager)
        {
            try
            {
                var saveManagerType = saveManager.GetType();

                // Try GetCurrentRing, GetRing, GetFloor, etc.
                string[] ringMethods = { "GetCurrentRing", "GetRing", "GetCurrentFloor", "GetFloor", "GetCurrentLevel" };
                foreach (var methodName in ringMethods)
                {
                    var method = saveManagerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                    if (method != null && method.GetParameters().Length == 0)
                    {
                        var result = method.Invoke(saveManager, null);
                        if (result != null)
                        {
                            int ring = Convert.ToInt32(result);
                            // Monster Train has rings 1-8 typically
                            if (ring >= 0 && ring <= 10)
                            {
                                MonsterTrainAccessibility.LogInfo($"Got ring from {methodName}: {ring}");
                                return $"Ring {ring + 1}"; // Convert 0-based to 1-based
                            }
                        }
                    }
                }

                // Try fields
                string[] ringFields = { "currentRing", "_currentRing", "ring", "_ring", "currentFloor", "_currentFloor" };
                foreach (var fieldName in ringFields)
                {
                    var field = saveManagerType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var value = field.GetValue(saveManager);
                        if (value != null)
                        {
                            int ring = Convert.ToInt32(value);
                            if (ring >= 0 && ring <= 10)
                            {
                                MonsterTrainAccessibility.LogInfo($"Got ring from field {fieldName}: {ring}");
                                return $"Ring {ring + 1}";
                            }
                        }
                    }
                }

                // Try to get from RunState
                var runStateField = saveManagerType.GetField("runState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (runStateField != null)
                {
                    var runState = runStateField.GetValue(saveManager);
                    if (runState != null)
                    {
                        var runStateType = runState.GetType();
                        foreach (var methodName in ringMethods)
                        {
                            var method = runStateType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                            if (method != null && method.GetParameters().Length == 0)
                            {
                                var result = method.Invoke(runState, null);
                                if (result != null)
                                {
                                    int ring = Convert.ToInt32(result);
                                    if (ring >= 0 && ring <= 10)
                                    {
                                        return $"Ring {ring + 1}";
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting ring info: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Determine battle type (Boss, Elite, Normal, etc.)
        /// </summary>
        internal static string GetBattleType(object saveManager, object screen, Type screenType)
        {
            try
            {
                // Check if this is a boss battle by looking at bigBossDisplay visibility
                // The bigBossDisplay is only visible/active during actual boss fights
                var bigBossField = screenType.GetField("bigBossDisplay", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (bigBossField != null)
                {
                    var bigBoss = bigBossField.GetValue(screen);
                    if (bigBoss != null && bigBoss is Component comp)
                    {
                        // Check if the boss display GameObject is active
                        if (comp.gameObject.activeInHierarchy)
                        {
                            MonsterTrainAccessibility.LogInfo("Boss display is active - this is a boss battle");
                            return "Boss Battle";
                        }
                        else
                        {
                            MonsterTrainAccessibility.LogInfo("Boss display exists but is not active - not a boss battle");
                        }
                    }
                }

                // Check ScenarioData for more info
                var saveManagerType = saveManager.GetType();
                var getCurrentScenarioMethod = saveManagerType.GetMethod("GetCurrentScenarioData", BindingFlags.Public | BindingFlags.Instance);
                if (getCurrentScenarioMethod != null && getCurrentScenarioMethod.GetParameters().Length == 0)
                {
                    var scenarioData = getCurrentScenarioMethod.Invoke(saveManager, null);
                    if (scenarioData != null)
                    {
                        var scenarioType = scenarioData.GetType();

                        // Check if there's a GetIsBoss or IsBossBattle method
                        var isBossMethod = scenarioType.GetMethod("GetIsBoss", BindingFlags.Public | BindingFlags.Instance) ??
                                          scenarioType.GetMethod("IsBossBattle", BindingFlags.Public | BindingFlags.Instance);
                        if (isBossMethod != null && isBossMethod.GetParameters().Length == 0)
                        {
                            var isBoss = isBossMethod.Invoke(scenarioData, null);
                            if (isBoss is bool b && b)
                            {
                                return "Boss Battle";
                            }
                        }

                        // Check difficulty field - higher values might indicate harder battles
                        var difficultyField = scenarioType.GetField("difficulty", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (difficultyField != null)
                        {
                            var difficulty = difficultyField.GetValue(scenarioData);
                            if (difficulty != null)
                            {
                                int diffValue = Convert.ToInt32(difficulty);
                                MonsterTrainAccessibility.LogInfo($"Scenario difficulty: {diffValue}");
                                // Could indicate elite/hard battle at certain thresholds
                            }
                        }

                        // Log bossVariant for reference (but don't use it to determine boss status)
                        var bossVariantField = scenarioType.GetField("bossVariant", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (bossVariantField != null)
                        {
                            var bossVariant = bossVariantField.GetValue(scenarioData);
                            if (bossVariant != null)
                            {
                                MonsterTrainAccessibility.LogInfo($"Ring boss pool: {bossVariant} (not current battle type)");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle type: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Get difficulty info from current scenario
        /// </summary>
        internal static string GetScenarioDifficulty(object saveManager)
        {
            try
            {
                var saveManagerType = saveManager.GetType();

                // Try to get covenant level (difficulty modifier)
                var getCovenantMethod = saveManagerType.GetMethod("GetCovenantLevel", BindingFlags.Public | BindingFlags.Instance);
                if (getCovenantMethod == null)
                {
                    getCovenantMethod = saveManagerType.GetMethod("GetAscensionLevel", BindingFlags.Public | BindingFlags.Instance);
                }
                if (getCovenantMethod != null && getCovenantMethod.GetParameters().Length == 0)
                {
                    var covenant = getCovenantMethod.Invoke(saveManager, null);
                    if (covenant != null)
                    {
                        int level = Convert.ToInt32(covenant);
                        if (level > 0)
                        {
                            MonsterTrainAccessibility.LogInfo($"Covenant level: {level}");
                            return $"Covenant {level}";
                        }
                    }
                }

                // Check fields
                string[] covenantFields = { "covenantLevel", "_covenantLevel", "ascensionLevel", "_ascensionLevel" };
                foreach (var fieldName in covenantFields)
                {
                    var field = saveManagerType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var value = field.GetValue(saveManager);
                        if (value != null)
                        {
                            int level = Convert.ToInt32(value);
                            if (level > 0)
                            {
                                return $"Covenant {level}";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scenario difficulty: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Get the scenario description from SaveManager's current scenario
        /// </summary>
        internal static string GetScenarioDescriptionFromSaveManager(object saveManager)
        {
            try
            {
                var saveManagerType = saveManager.GetType();

                // Try GetCurrentScenarioData method
                var getCurrentScenarioMethod = saveManagerType.GetMethod("GetCurrentScenarioData", BindingFlags.Public | BindingFlags.Instance);
                if (getCurrentScenarioMethod != null && getCurrentScenarioMethod.GetParameters().Length == 0)
                {
                    var scenarioData = getCurrentScenarioMethod.Invoke(saveManager, null);
                    if (scenarioData != null)
                    {
                        string desc = GetBattleDescriptionFromScenario(scenarioData);
                        if (!string.IsNullOrEmpty(desc))
                        {
                            MonsterTrainAccessibility.LogInfo($"Got battle description: {desc}");
                            return desc;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scenario description from SaveManager: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Get the battle description from a ScenarioData object
        /// </summary>
        internal static string GetBattleDescriptionFromScenario(object scenarioData)
        {
            try
            {
                var dataType = scenarioData.GetType();

                // Try GetBattleDescription method
                var getDescMethod = dataType.GetMethod("GetBattleDescription", BindingFlags.Public | BindingFlags.Instance);
                if (getDescMethod != null && getDescMethod.GetParameters().Length == 0)
                {
                    var result = getDescMethod.Invoke(scenarioData, null);
                    if (result is string desc && !string.IsNullOrEmpty(desc))
                    {
                        return desc;
                    }
                }

                // Try battleDescriptionKey field
                string[] descFieldNames = { "battleDescriptionKey", "_battleDescriptionKey", "descriptionKey", "_descriptionKey" };
                foreach (var fieldName in descFieldNames)
                {
                    var field = dataType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var key = field.GetValue(scenarioData) as string;
                        if (!string.IsNullOrEmpty(key))
                        {
                            string localized = LocalizationHelper.LocalizeOrNull(key);
                            if (!string.IsNullOrEmpty(localized))
                            {
                                return localized;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle description: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Extract battle name and description from ScenarioData
        /// </summary>
        internal static string GetBattleNameAndDescription(object scenarioData)
        {
            if (scenarioData == null) return null;

            try
            {
                var dataType = scenarioData.GetType();
                string battleName = null;
                string battleDescription = null;

                // Debug: log fields
                var fields = dataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MonsterTrainAccessibility.LogInfo($"Scenario fields: {string.Join(", ", fields.Select(f => f.Name))}");

                // Try to get battle name
                // Method: GetBattleName()
                var getBattleNameMethod = dataType.GetMethod("GetBattleName", BindingFlags.Public | BindingFlags.Instance);
                if (getBattleNameMethod != null && getBattleNameMethod.GetParameters().Length == 0)
                {
                    var result = getBattleNameMethod.Invoke(scenarioData, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                    {
                        battleName = name;
                        MonsterTrainAccessibility.LogInfo($"Got battle name from GetBattleName(): {battleName}");
                    }
                }

                // Try field: battleNameKey
                if (string.IsNullOrEmpty(battleName))
                {
                    string[] nameFieldNames = { "battleNameKey", "_battleNameKey", "nameKey", "_nameKey", "titleKey", "_titleKey" };
                    foreach (var fieldName in nameFieldNames)
                    {
                        var field = dataType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            var key = field.GetValue(scenarioData) as string;
                            if (!string.IsNullOrEmpty(key))
                            {
                                string localized = LocalizationHelper.LocalizeOrNull(key);
                                if (!string.IsNullOrEmpty(localized))
                                {
                                    battleName = localized;
                                    MonsterTrainAccessibility.LogInfo($"Got battle name from {fieldName}: {battleName}");
                                    break;
                                }
                            }
                        }
                    }
                }

                // Try to get battle description
                // Method: GetBattleDescription()
                var getBattleDescMethod = dataType.GetMethod("GetBattleDescription", BindingFlags.Public | BindingFlags.Instance);
                if (getBattleDescMethod != null && getBattleDescMethod.GetParameters().Length == 0)
                {
                    var result = getBattleDescMethod.Invoke(scenarioData, null);
                    if (result is string desc && !string.IsNullOrEmpty(desc))
                    {
                        battleDescription = desc;
                        MonsterTrainAccessibility.LogInfo($"Got battle description from GetBattleDescription(): {battleDescription}");
                    }
                }

                // Try field: battleDescriptionKey
                if (string.IsNullOrEmpty(battleDescription))
                {
                    string[] descFieldNames = { "battleDescriptionKey", "_battleDescriptionKey", "descriptionKey", "_descriptionKey" };
                    foreach (var fieldName in descFieldNames)
                    {
                        var field = dataType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            var key = field.GetValue(scenarioData) as string;
                            if (!string.IsNullOrEmpty(key))
                            {
                                string localized = LocalizationHelper.LocalizeOrNull(key);
                                if (!string.IsNullOrEmpty(localized))
                                {
                                    battleDescription = localized;
                                    MonsterTrainAccessibility.LogInfo($"Got battle description from {fieldName}: {battleDescription}");
                                    break;
                                }
                            }
                        }
                    }
                }

                // Build the result
                if (!string.IsNullOrEmpty(battleName))
                {
                    if (!string.IsNullOrEmpty(battleDescription))
                    {
                        return $"Fight: {battleName}. {battleDescription}";
                    }
                    return $"Fight: {battleName}";
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle name/description: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Look for TooltipTarget siblings to get enemy names for the Fight button
        /// </summary>
        internal static string GetEnemyNamesFromSiblings(GameObject go)
        {
            try
            {
                // Navigate up to find Enemy_Tooltips or similar container
                Transform searchRoot = go.transform.parent;
                while (searchRoot != null)
                {
                    // Look for a container that might have TooltipTargets
                    var tooltipContainer = FindChildByNameContains(searchRoot, "Tooltip");
                    if (tooltipContainer != null)
                    {
                        var enemyNames = new List<string>();

                        // Get all TooltipTarget children
                        foreach (Transform child in tooltipContainer)
                        {
                            if (!child.gameObject.activeInHierarchy) continue;
                            if (!child.name.Contains("TooltipTarget")) continue;

                            // Get the tooltip provider and extract the name
                            foreach (var component in child.GetComponents<Component>())
                            {
                                if (component == null) continue;
                                if (component.GetType().Name == "TooltipProviderComponent")
                                {
                                    string name = TooltipTextReader.GetTooltipProviderTitle(component, component.GetType());
                                    if (!string.IsNullOrEmpty(name))
                                    {
                                        enemyNames.Add(name);
                                    }
                                    break;
                                }
                            }
                        }

                        if (enemyNames.Count > 0)
                        {
                            return string.Join(", ", enemyNames);
                        }
                    }

                    // Also check siblings at this level
                    if (searchRoot.parent != null)
                    {
                        foreach (Transform sibling in searchRoot.parent)
                        {
                            if (sibling.name.Contains("Tooltip") || sibling.name.Contains("Enemy"))
                            {
                                var names = GetTooltipNamesFromContainer(sibling);
                                if (names.Count > 0)
                                {
                                    return string.Join(", ", names);
                                }
                            }
                        }
                    }

                    searchRoot = searchRoot.parent;

                    // Don't go too far up
                    if (searchRoot != null && searchRoot.name.Contains("BattleIntro"))
                        break;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting enemy names from siblings: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Find a child transform by partial name match
        /// </summary>
        internal static Transform FindChildByNameContains(Transform parent, string partialName)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Contains(partialName))
                    return child;

                var found = FindChildByNameContains(child, partialName);
                if (found != null)
                    return found;
            }
            return null;
        }


        /// <summary>
        /// Get all tooltip names from a container
        /// </summary>
        internal static List<string> GetTooltipNamesFromContainer(Transform container)
        {
            var names = new List<string>();

            foreach (Transform child in container)
            {
                if (!child.gameObject.activeInHierarchy) continue;

                foreach (var component in child.GetComponents<Component>())
                {
                    if (component == null) continue;
                    if (component.GetType().Name == "TooltipProviderComponent")
                    {
                        string name = TooltipTextReader.GetTooltipProviderTitle(component, component.GetType());
                        if (!string.IsNullOrEmpty(name))
                        {
                            names.Add(name);
                        }
                        break;
                    }
                }
            }

            return names;
        }


        /// <summary>
        /// Check if the current UI element is in a battle intro context (NOT during actual combat)
        /// Only add keyword explanations for battle intro enemy/boss tooltips
        /// </summary>
        internal static bool IsBattleRelatedContext(GameObject go)
        {
            if (go == null) return false;

            // Don't add keywords during actual battle - only during battle intro
            if (Help.ScreenStateTracker.CurrentScreen == Help.GameScreen.Battle)
                return false;

            // Only trigger for battle intro screen
            if (Help.ScreenStateTracker.CurrentScreen != Help.GameScreen.BattleIntro)
                return false;

            string goName = go.name.ToLower();

            // Check for enemy tooltip targets specifically
            if (goName.Contains("tooltiptarget") || goName.Contains("enemy_tooltip"))
                return true;

            // Check parent hierarchy for enemy tooltip context
            Transform current = go.transform;
            int depth = 0;
            while (current != null && depth < 5)
            {
                string parentName = current.name.ToLower();
                if (parentName.Contains("enemy_tooltip") || parentName.Contains("tooltiptarget"))
                    return true;
                current = current.parent;
                depth++;
            }

            return false;
        }


        /// <summary>
        /// Get text from a UI text component (TMP_Text, Text, etc.)
        /// </summary>
        internal static string GetTextFromComponent(object textComponent)
        {
            if (textComponent == null) return null;

            try
            {
                var type = textComponent.GetType();

                // Try 'text' property (common for both TMP_Text and Unity Text)
                var textProp = type.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                if (textProp != null)
                {
                    var text = textProp.GetValue(textComponent) as string;
                    if (!string.IsNullOrEmpty(text))
                        return text.Trim();
                }

                // Try GetParsedText for TMP (returns text without rich text tags)
                var getParsedMethod = type.GetMethod("GetParsedText", BindingFlags.Public | BindingFlags.Instance);
                if (getParsedMethod != null && getParsedMethod.GetParameters().Length == 0)
                {
                    var text = getParsedMethod.Invoke(textComponent, null) as string;
                    if (!string.IsNullOrEmpty(text))
                        return text.Trim();
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting text from component: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Get all text strings from child text components of a GameObject
        /// </summary>
        internal static List<string> GetAllTextFromChildren(GameObject go)
        {
            var texts = new List<string>();

            if (go == null) return texts;

            try
            {
                // Get all components in children
                var components = go.GetComponentsInChildren<Component>(false);

                foreach (var comp in components)
                {
                    if (comp == null || !comp.gameObject.activeInHierarchy) continue;

                    string typeName = comp.GetType().Name;

                    // Look for text components (TMP_Text, Text, TextMeshProUGUI, etc.)
                    if (typeName.Contains("Text"))
                    {
                        string text = GetTextFromComponent(comp);
                        if (!string.IsNullOrEmpty(text) && text.Length > 1)
                        {
                            // Filter out common UI noise
                            string lowerText = text.ToLower();
                            if (!lowerText.Contains("view") &&
                                !lowerText.StartsWith("$") &&
                                !text.All(c => char.IsDigit(c) || c == ' '))
                            {
                                texts.Add(TextUtilities.StripRichTextTags(text));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting text from children: {ex.Message}");
            }

            return texts;
        }


        /// <summary>
        /// Debug: Log all text content in a UI hierarchy
        /// </summary>
        internal static void LogAllTextInHierarchy(Transform root, string prefix)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"=== All text in {prefix} hierarchy ===");
                LogTextRecursive(root, sb, 0);
                MonsterTrainAccessibility.LogInfo(sb.ToString());
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error logging text hierarchy: {ex.Message}");
            }
        }


        internal static void LogTextRecursive(Transform node, StringBuilder sb, int depth)
        {
            if (node == null || depth > 10) return;

            string indent = new string(' ', depth * 2);

            // Get text from this node
            string text = UITextHelper.GetTMPTextDirect(node.gameObject);
            if (string.IsNullOrEmpty(text))
            {
                var uiText = node.GetComponent<Text>();
                text = uiText?.text;
            }

            // Log if there's text
            if (!string.IsNullOrEmpty(text) && text.Trim().Length > 0)
            {
                sb.AppendLine($"{indent}[{node.name}]: \"{text.Trim()}\"");
            }

            // Recurse to children
            foreach (Transform child in node)
            {
                if (child.gameObject.activeInHierarchy)
                {
                    LogTextRecursive(child, sb, depth + 1);
                }
            }
        }

    }
}
