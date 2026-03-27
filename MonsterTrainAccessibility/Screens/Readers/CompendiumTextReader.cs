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
    /// Extracted reader for Compendium UI elements.
    /// </summary>
    internal static class CompendiumTextReader
    {

        /// <summary>
        /// Get text for logbook/compendium items
        /// </summary>
        internal static string GetLogbookItemText(GameObject go)
        {
            try
            {
                // Check if this or a parent is part of the compendium
                if (!IsInCompendiumContext(go))
                    return null;

                // Look for count labels (format like "25/250" or "X/Y")
                string countText = FindCountLabelText(go);
                string itemName = GetItemNameFromHierarchy(go);

                if (!string.IsNullOrEmpty(countText) && !string.IsNullOrEmpty(itemName))
                {
                    return $"{itemName}: {countText}";
                }
                else if (!string.IsNullOrEmpty(countText))
                {
                    // Try to make the count more readable
                    return FormatCountText(countText);
                }
                else if (!string.IsNullOrEmpty(itemName))
                {
                    return itemName;
                }
            }
            catch { }
            return null;
        }


        /// <summary>
        /// Check if we're in a compendium/logbook context
        /// </summary>
        internal static bool IsInCompendiumContext(GameObject go)
        {
            Transform current = go.transform;
            while (current != null)
            {
                string name = current.name.ToLower();
                if (name.Contains("compendium") || name.Contains("logbook") ||
                    name.Contains("collection") || name.Contains("cardlist"))
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }


        /// <summary>
        /// Find count label text (X/Y format) in the hierarchy
        /// </summary>
        internal static string FindCountLabelText(GameObject go)
        {
            try
            {
                Transform parent = go.transform.parent;
                if (parent == null) return null;

                // Collect all text from siblings to find count patterns
                var allTexts = new List<string>();
                foreach (Transform sibling in parent)
                {
                    string text = UITextHelper.GetTMPTextDirect(sibling.gameObject);
                    if (!string.IsNullOrEmpty(text))
                        allTexts.Add(text.Trim());

                    var uiText = sibling.GetComponent<Text>();
                    if (uiText != null && !string.IsNullOrEmpty(uiText.text))
                        allTexts.Add(uiText.text.Trim());

                    // Also check children
                    foreach (Transform child in sibling)
                    {
                        string childText = UITextHelper.GetTMPTextDirect(child.gameObject);
                        if (!string.IsNullOrEmpty(childText))
                            allTexts.Add(childText.Trim());
                    }
                }

                // Look for X/Y pattern
                foreach (var text in allTexts)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d+/\d+$"))
                    {
                        return text;
                    }
                }

                // Look for separate number that could be part of count
                string number = null;
                string total = null;
                foreach (var text in allTexts)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d+$"))
                    {
                        if (number == null)
                            number = text;
                        else if (total == null)
                            total = text;
                    }
                }

                if (number != null && total != null)
                {
                    return $"{number}/{total}";
                }
            }
            catch { }
            return null;
        }


        /// <summary>
        /// Get item name from hierarchy for logbook items
        /// </summary>
        internal static string GetItemNameFromHierarchy(GameObject go)
        {
            try
            {
                // Look for title/name in the hierarchy
                Transform current = go.transform;
                for (int i = 0; i < 3 && current != null; i++)
                {
                    if (current.parent != null)
                    {
                        foreach (Transform sibling in current.parent)
                        {
                            string sibName = sibling.name.ToLower();
                            if (sibName.Contains("title") || sibName.Contains("name") ||
                                sibName.Contains("label") || sibName.Contains("header"))
                            {
                                string text = UITextHelper.GetTMPTextDirect(sibling.gameObject);
                                if (string.IsNullOrEmpty(text))
                                {
                                    var uiText = sibling.GetComponent<Text>();
                                    text = uiText?.text;
                                }
                                if (!string.IsNullOrEmpty(text) && text.Length > 2)
                                {
                                    // Make sure it's not a number
                                    if (!System.Text.RegularExpressions.Regex.IsMatch(text.Trim(), @"^\d+$"))
                                    {
                                        return text.Trim();
                                    }
                                }
                            }
                        }
                    }
                    current = current.parent;
                }
            }
            catch { }
            return null;
        }


        /// <summary>
        /// Format count text to be more readable
        /// </summary>
        internal static string FormatCountText(string countText)
        {
            if (string.IsNullOrEmpty(countText))
                return null;

            // If it's already in X/Y format, make it more readable
            var match = System.Text.RegularExpressions.Regex.Match(countText, @"^(\d+)/(\d+)$");
            if (match.Success)
            {
                return $"{match.Groups[1].Value} of {match.Groups[2].Value} discovered";
            }

            return countText;
        }


        /// <summary>
        /// Get text for CompendiumRelicUI items in the relic/artifact grid
        /// </summary>
        internal static string GetCompendiumRelicText(GameObject go)
        {
            try
            {
                // Look for CompendiumRelicUI or RelicTooltipProvider component
                Component relicComponent = null;
                Transform current = go.transform;

                while (current != null && relicComponent == null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;

                        if (typeName == "CompendiumRelicUI" || typeName == "RelicIconUI")
                        {
                            relicComponent = component;
                            break;
                        }
                    }
                    current = current.parent;
                }

                if (relicComponent == null)
                    return null;

                // Only use this in compendium context
                if (!IsInCompendiumContext(go))
                    return null;

                var compType = relicComponent.GetType();

                // Try to get tooltips from ITooltipProvider - these contain name and description
                var tooltipsProp = compType.GetProperty("tooltips", BindingFlags.Public | BindingFlags.Instance);
                if (tooltipsProp != null)
                {
                    var tooltipsList = tooltipsProp.GetValue(relicComponent);
                    if (tooltipsList != null)
                    {
                        // It's a List<TooltipContent>
                        var listType = tooltipsList.GetType();
                        var countProp = listType.GetProperty("Count");
                        int count = (int)countProp.GetValue(tooltipsList);

                        if (count > 0)
                        {
                            var indexer = listType.GetProperty("Item");
                            var tooltip = indexer.GetValue(tooltipsList, new object[] { 0 });

                            if (tooltip != null)
                            {
                                var tooltipType = tooltip.GetType();

                                string title = null;
                                string body = null;

                                // Try title field/property
                                var titleField = tooltipType.GetField("title", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (titleField != null) title = titleField.GetValue(tooltip) as string;
                                if (string.IsNullOrEmpty(title))
                                {
                                    var titleProp = tooltipType.GetProperty("title", BindingFlags.Public | BindingFlags.Instance);
                                    if (titleProp != null) title = titleProp.GetValue(tooltip) as string;
                                }

                                // Try body field/property
                                var bodyField = tooltipType.GetField("body", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (bodyField != null) body = bodyField.GetValue(tooltip) as string;
                                if (string.IsNullOrEmpty(body))
                                {
                                    var bodyProp = tooltipType.GetProperty("body", BindingFlags.Public | BindingFlags.Instance);
                                    if (bodyProp != null) body = bodyProp.GetValue(tooltip) as string;
                                }

                                if (!string.IsNullOrEmpty(title))
                                {
                                    var sb = new StringBuilder();
                                    sb.Append("Artifact: ");
                                    sb.Append(title);
                                    if (!string.IsNullOrEmpty(body))
                                    {
                                        string cleanBody = TextUtilities.StripRichTextTags(body);
                                        cleanBody = TextUtilities.CleanSpriteTagsForSpeech(cleanBody);
                                        sb.Append(". ");
                                        sb.Append(cleanBody);
                                    }
                                    MonsterTrainAccessibility.LogInfo($"CompendiumRelicUI text: {sb}");
                                    return sb.ToString();
                                }
                            }
                        }
                    }
                }

                // Fallback: try relicState field from RelicTooltipProvider
                var relicStateField = compType.GetField("relicState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (relicStateField != null)
                {
                    var relicState = relicStateField.GetValue(relicComponent);
                    if (relicState != null)
                    {
                        var stateType = relicState.GetType();
                        var getNameMethod = stateType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                        if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                        {
                            string name = getNameMethod.Invoke(relicState, null) as string;
                            if (!string.IsNullOrEmpty(name))
                            {
                                MonsterTrainAccessibility.LogInfo($"CompendiumRelicUI from relicState: {name}");
                                return $"Artifact: {name}";
                            }
                        }
                    }
                }

                // For CompendiumRelicUI, try collectableRelicData property
                if (compType.Name == "CompendiumRelicUI")
                {
                    var relicDataProp = compType.GetProperty("collectableRelicData", BindingFlags.Public | BindingFlags.Instance);
                    if (relicDataProp != null)
                    {
                        var relicData = relicDataProp.GetValue(relicComponent);
                        if (relicData != null)
                        {
                            var dataType = relicData.GetType();
                            var getNameMethod = dataType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                            if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                            {
                                string name = getNameMethod.Invoke(relicData, null) as string;
                                if (!string.IsNullOrEmpty(name))
                                {
                                    MonsterTrainAccessibility.LogInfo($"CompendiumRelicUI from collectableRelicData: {name}");
                                    return $"Artifact: {name}";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting compendium relic text: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Get text for UpgradeLevelNode items in compendium champion upgrades section
        /// </summary>
        internal static string GetUpgradeLevelNodeText(GameObject go)
        {
            try
            {
                // Look for UpgradeLevelNode component
                Component upgradeLevelNode = null;
                Transform current = go.transform;

                while (current != null && upgradeLevelNode == null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "UpgradeLevelNode")
                        {
                            upgradeLevelNode = component;
                            break;
                        }
                    }
                    current = current.parent;
                }

                if (upgradeLevelNode == null)
                    return null;

                // Find the UpgradeTreeUI parent to get the title
                string treeTitle = null;
                Transform treeParent = upgradeLevelNode.transform.parent;
                while (treeParent != null)
                {
                    foreach (var component in treeParent.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "UpgradeTreeUI")
                        {
                            // Get titleLabel field
                            var titleField = component.GetType().GetField("titleLabel", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (titleField != null)
                            {
                                var titleLabel = titleField.GetValue(component);
                                if (titleLabel != null)
                                {
                                    var textProp = titleLabel.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                                    if (textProp != null)
                                    {
                                        treeTitle = textProp.GetValue(titleLabel) as string;
                                    }
                                }
                            }
                            break;
                        }
                    }
                    if (treeTitle != null) break;
                    treeParent = treeParent.parent;
                }

                // Determine the level index from sibling order
                int levelIndex = -1;
                if (upgradeLevelNode.transform.parent != null)
                {
                    for (int i = 0; i < upgradeLevelNode.transform.parent.childCount; i++)
                    {
                        if (upgradeLevelNode.transform.parent.GetChild(i) == upgradeLevelNode.transform)
                        {
                            levelIndex = i;
                            break;
                        }
                    }
                }

                // Check if enabled/interactable
                var buttonField = upgradeLevelNode.GetType().GetField("button", BindingFlags.NonPublic | BindingFlags.Instance);
                bool isEnabled = true;
                if (buttonField != null)
                {
                    var button = buttonField.GetValue(upgradeLevelNode);
                    if (button != null)
                    {
                        var interactableProp = button.GetType().GetProperty("interactable", BindingFlags.Public | BindingFlags.Instance);
                        if (interactableProp != null)
                        {
                            isEnabled = (bool)interactableProp.GetValue(button);
                        }
                    }
                }

                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(treeTitle) && !DialogTextReader.IsGarbageText(treeTitle))
                {
                    sb.Append(treeTitle);
                    sb.Append(", ");
                }
                sb.Append($"Upgrade level {levelIndex + 1}");
                if (!isEnabled)
                {
                    sb.Append(" (locked)");
                }
                MonsterTrainAccessibility.LogInfo($"UpgradeLevelNode text: {sb}");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting upgrade level node text: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Get text for PlayerStatRow items in compendium leaderboard section
        /// </summary>
        internal static string GetPlayerStatRowText(GameObject go)
        {
            try
            {
                // Look for PlayerStatRow component
                Component playerStatRow = null;
                Transform current = go.transform;

                while (current != null && playerStatRow == null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "PlayerStatRow")
                        {
                            playerStatRow = component;
                            break;
                        }
                    }
                    current = current.parent;
                }

                if (playerStatRow == null)
                    return null;

                var rowType = playerStatRow.GetType();

                // Read from the playerStats data property (runtime data, not template TMP text)
                string rank = null;
                string playerName = null;
                string value = null;

                var playerStatsProp = rowType.GetProperty("playerStats", BindingFlags.Public | BindingFlags.Instance);
                if (playerStatsProp != null)
                {
                    var playerStats = playerStatsProp.GetValue(playerStatRow);
                    if (playerStats != null)
                    {
                        var statsType = playerStats.GetType();

                        // Get Rank
                        var rankProp = statsType.GetProperty("Rank", BindingFlags.Public | BindingFlags.Instance);
                        if (rankProp != null)
                        {
                            var rankVal = rankProp.GetValue(playerStats);
                            if (rankVal != null) rank = rankVal.ToString();
                        }

                        // Get PlayerFriendlyName
                        var nameProp = statsType.GetProperty("PlayerFriendlyName", BindingFlags.Public | BindingFlags.Instance);
                        if (nameProp != null)
                        {
                            playerName = nameProp.GetValue(playerStats) as string;
                        }
                    }
                    else
                    {
                        // No data loaded for this row - skip it
                        return null;
                    }
                }

                // Read the displayed value from the TMP label (it's formatted by the game with the correct stat)
                var valueField = rowType.GetField("valueLabel", BindingFlags.NonPublic | BindingFlags.Instance);
                if (valueField != null)
                {
                    var valueLabel = valueField.GetValue(playerStatRow);
                    if (valueLabel != null)
                    {
                        var textProp = valueLabel.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                        if (textProp != null) value = textProp.GetValue(valueLabel) as string;
                    }
                }

                if (string.IsNullOrEmpty(playerName) && string.IsNullOrEmpty(rank))
                    return null;

                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(rank))
                {
                    sb.Append($"Rank {rank}");
                }
                if (!string.IsNullOrEmpty(playerName))
                {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(playerName);
                }
                if (!string.IsNullOrEmpty(value))
                {
                    sb.Append($": {value}");
                }

                MonsterTrainAccessibility.LogInfo($"PlayerStatRow text: {sb}");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting player stat row text: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Get text for ClanChecklistSection items in compendium checklist
        /// Reads clan name, unlock condition, and progress meter value
        /// </summary>
        internal static string GetClanChecklistText(GameObject go)
        {
            try
            {
                // Find ClanChecklistSection in parent hierarchy
                Component checklistSection = null;
                Transform current = go.transform;

                while (current != null && checklistSection == null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "ClanChecklistSection")
                        {
                            checklistSection = component;
                            break;
                        }
                    }
                    current = current.parent;
                }

                if (checklistSection == null)
                    return null;

                var sectionType = checklistSection.GetType();
                var sb = new StringBuilder();

                // Get clan name
                var clanNameField = sectionType.GetField("clanNameLabel", BindingFlags.NonPublic | BindingFlags.Instance);
                if (clanNameField != null)
                {
                    var clanNameLabel = clanNameField.GetValue(checklistSection);
                    if (clanNameLabel != null)
                    {
                        var textProp = clanNameLabel.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                        if (textProp != null)
                        {
                            string clanName = textProp.GetValue(clanNameLabel) as string;
                            if (!string.IsNullOrEmpty(clanName))
                            {
                                sb.Append(clanName);
                            }
                        }
                    }
                }

                // Check if unlock conditions are showing (locked clan)
                var unlockRootField = sectionType.GetField("unlockConditionsRoot", BindingFlags.NonPublic | BindingFlags.Instance);
                if (unlockRootField != null)
                {
                    var unlockRoot = unlockRootField.GetValue(checklistSection) as GameObject;
                    if (unlockRoot != null && unlockRoot.activeSelf)
                    {
                        // Get unlock condition label
                        var condLabelField = sectionType.GetField("unlockConditionsLabel", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (condLabelField != null)
                        {
                            var condLabel = condLabelField.GetValue(checklistSection);
                            if (condLabel != null)
                            {
                                var textProp = condLabel.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                                if (textProp != null)
                                {
                                    string condText = textProp.GetValue(condLabel) as string;
                                    if (!string.IsNullOrEmpty(condText))
                                    {
                                        if (sb.Length > 0) sb.Append(". ");
                                        sb.Append(condText);
                                    }
                                }
                            }
                        }

                        // Get meter value from unlockConditionsMeter.countLabel
                        var meterField = sectionType.GetField("unlockConditionsMeter", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (meterField != null)
                        {
                            var meter = meterField.GetValue(checklistSection);
                            if (meter != null)
                            {
                                var meterType = meter.GetType();
                                var countLabelField = meterType.GetField("countLabel", BindingFlags.NonPublic | BindingFlags.Instance);
                                if (countLabelField != null)
                                {
                                    var countLabel = countLabelField.GetValue(meter);
                                    if (countLabel != null)
                                    {
                                        var textProp = countLabel.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                                        if (textProp != null)
                                        {
                                            string meterText = textProp.GetValue(countLabel) as string;
                                            if (!string.IsNullOrEmpty(meterText))
                                            {
                                                sb.Append($" {meterText}");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Check if level indicators are showing (unlocked clan)
                var levelLayoutField = sectionType.GetField("levelIndicatorLayout", BindingFlags.NonPublic | BindingFlags.Instance);
                if (levelLayoutField != null)
                {
                    var levelLayout = levelLayoutField.GetValue(checklistSection);
                    if (levelLayout != null)
                    {
                        var layoutGO = (levelLayout as Component)?.gameObject;
                        if (layoutGO != null && layoutGO.activeSelf)
                        {
                            // Try to get level tooltip for "Hellhorned Level 5" style text
                            var tooltipField = sectionType.GetField("levelTooltipProvider", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (tooltipField != null)
                            {
                                var tooltipProvider = tooltipField.GetValue(checklistSection);
                                if (tooltipProvider != null)
                                {
                                    var providerType = tooltipProvider.GetType();
                                    var tooltipsProp = providerType.GetProperty("tooltips", BindingFlags.Public | BindingFlags.Instance);
                                    if (tooltipsProp != null)
                                    {
                                        var tooltipsList = tooltipsProp.GetValue(tooltipProvider);
                                        if (tooltipsList != null)
                                        {
                                            var listType = tooltipsList.GetType();
                                            var countProp = listType.GetProperty("Count");
                                            int count = (int)countProp.GetValue(tooltipsList);
                                            if (count > 0)
                                            {
                                                var indexer = listType.GetProperty("Item");
                                                var tooltip = indexer.GetValue(tooltipsList, new object[] { 0 });
                                                if (tooltip != null)
                                                {
                                                    string title = null;
                                                    string body = null;
                                                    var titleField = tooltip.GetType().GetField("title", BindingFlags.Public | BindingFlags.Instance);
                                                    if (titleField != null) title = titleField.GetValue(tooltip) as string;
                                                    var bodyField = tooltip.GetType().GetField("body", BindingFlags.Public | BindingFlags.Instance);
                                                    if (bodyField != null) body = bodyField.GetValue(tooltip) as string;

                                                    // Replace clan name with tooltip title (e.g. "Hellhorned Level 5")
                                                    if (!string.IsNullOrEmpty(title))
                                                    {
                                                        sb.Clear();
                                                        sb.Append(title);
                                                    }
                                                    if (!string.IsNullOrEmpty(body))
                                                    {
                                                        sb.Append($". {body}");
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

                if (sb.Length > 0)
                {
                    MonsterTrainAccessibility.LogInfo($"ClanChecklistSection text: {sb}");
                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting clan checklist text: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Get text for SubclanVictoryItem items in compendium checklist (clan combo victories)
        /// </summary>
        internal static string GetSubclanVictoryItemText(GameObject go)
        {
            try
            {
                // Look for SubclanVictoryItem component
                Component victoryItem = null;
                Transform current = go.transform;

                while (current != null && victoryItem == null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "SubclanVictoryItem")
                        {
                            victoryItem = component;
                            break;
                        }
                    }
                    current = current.parent;
                }

                if (victoryItem == null)
                    return null;

                var itemType = victoryItem.GetType();

                // Try to read from the tooltipProvider field first (has formatted text)
                var tooltipProviderField = itemType.GetField("tooltipProvider", BindingFlags.NonPublic | BindingFlags.Instance);
                if (tooltipProviderField != null)
                {
                    var tooltipProvider = tooltipProviderField.GetValue(victoryItem);
                    if (tooltipProvider != null)
                    {
                        var providerType = tooltipProvider.GetType();
                        var tooltipsProp = providerType.GetProperty("tooltips", BindingFlags.Public | BindingFlags.Instance);
                        if (tooltipsProp != null)
                        {
                            var tooltipsList = tooltipsProp.GetValue(tooltipProvider);
                            if (tooltipsList != null)
                            {
                                var listType = tooltipsList.GetType();
                                var countProp = listType.GetProperty("Count");
                                int count = (int)countProp.GetValue(tooltipsList);
                                if (count > 0)
                                {
                                    var indexer = listType.GetProperty("Item");
                                    var tooltip = indexer.GetValue(tooltipsList, new object[] { 0 });
                                    if (tooltip != null)
                                    {
                                        var tooltipType = tooltip.GetType();
                                        string title = null;
                                        string body = null;

                                        var titleField = tooltipType.GetField("title", BindingFlags.Public | BindingFlags.Instance);
                                        if (titleField != null) title = titleField.GetValue(tooltip) as string;

                                        var bodyField = tooltipType.GetField("body", BindingFlags.Public | BindingFlags.Instance);
                                        if (bodyField != null) body = bodyField.GetValue(tooltip) as string;

                                        var sb = new StringBuilder();
                                        if (!string.IsNullOrEmpty(title))
                                        {
                                            sb.Append(title);
                                        }
                                        if (!string.IsNullOrEmpty(body))
                                        {
                                            if (sb.Length > 0) sb.Append(". ");
                                            sb.Append(TextUtilities.StripRichTextTags(body));
                                        }

                                        if (sb.Length > 0)
                                        {
                                            MonsterTrainAccessibility.LogInfo($"SubclanVictoryItem tooltip text: {sb}");
                                            return sb.ToString();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Fallback: read from data property
                var dataProp = itemType.GetProperty("data", BindingFlags.Public | BindingFlags.Instance);
                if (dataProp != null)
                {
                    var data = dataProp.GetValue(victoryItem);
                    if (data != null)
                    {
                        var dataType = data.GetType();
                        string mainClan = null;
                        string subClan = null;

                        var mainField = dataType.GetField("mainClassData", BindingFlags.Public | BindingFlags.Instance);
                        if (mainField != null)
                        {
                            var mainClass = mainField.GetValue(data);
                            if (mainClass != null)
                            {
                                var getTitleMethod = mainClass.GetType().GetMethod("GetTitle", Type.EmptyTypes);
                                if (getTitleMethod != null) mainClan = getTitleMethod.Invoke(mainClass, null) as string;
                            }
                        }

                        var subField = dataType.GetField("subClassData", BindingFlags.Public | BindingFlags.Instance);
                        if (subField != null)
                        {
                            var subClass = subField.GetValue(data);
                            if (subClass != null)
                            {
                                var getTitleMethod = subClass.GetType().GetMethod("GetTitle", Type.EmptyTypes);
                                if (getTitleMethod != null) subClan = getTitleMethod.Invoke(subClass, null) as string;
                            }
                        }

                        if (!string.IsNullOrEmpty(mainClan) && !string.IsNullOrEmpty(subClan))
                        {
                            // Get covenant level
                            var covenantField = dataType.GetField("covenantLevels", BindingFlags.Public | BindingFlags.Instance);
                            string covenantInfo = "";
                            if (covenantField != null)
                            {
                                var levels = covenantField.GetValue(data) as System.Collections.IList;
                                if (levels != null)
                                {
                                    int maxLevel = -1;
                                    foreach (var level in levels)
                                    {
                                        if (level is int l && l > maxLevel) maxLevel = l;
                                    }
                                    if (maxLevel >= 0)
                                    {
                                        covenantInfo = $", Covenant {maxLevel}";
                                    }
                                    else
                                    {
                                        covenantInfo = ", Not yet won";
                                    }
                                }
                            }

                            string result = $"{mainClan} and {subClan}{covenantInfo}";
                            MonsterTrainAccessibility.LogInfo($"SubclanVictoryItem data text: {result}");
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting subclan victory item text: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Get text for RunStatRow items in compendium lifetime stats section
        /// </summary>
        internal static string GetRunStatRowText(GameObject go)
        {
            try
            {
                // Look for RunStatRow component
                Component runStatRow = null;
                Transform current = go.transform;

                while (current != null && runStatRow == null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "RunStatRow")
                        {
                            runStatRow = component;
                            break;
                        }
                    }
                    current = current.parent;
                }

                if (runStatRow == null)
                    return null;

                var rowType = runStatRow.GetType();
                string statName = null;
                string statValue = null;

                var nameField = rowType.GetField("statNameLabel", BindingFlags.NonPublic | BindingFlags.Instance);
                if (nameField != null)
                {
                    var label = nameField.GetValue(runStatRow);
                    if (label != null)
                    {
                        var textProp = label.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                        if (textProp != null) statName = textProp.GetValue(label) as string;
                    }
                }

                var valueField = rowType.GetField("statValueLabel", BindingFlags.NonPublic | BindingFlags.Instance);
                if (valueField != null)
                {
                    var label = valueField.GetValue(runStatRow);
                    if (label != null)
                    {
                        var textProp = label.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                        if (textProp != null) statValue = textProp.GetValue(label) as string;
                    }
                }

                if (string.IsNullOrEmpty(statName))
                    return null;

                var sb = new StringBuilder();
                sb.Append(statName);
                if (!string.IsNullOrEmpty(statValue))
                {
                    sb.Append($": {statValue}");
                }

                MonsterTrainAccessibility.LogInfo($"RunStatRow text: {sb}");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting run stat row text: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Get text for sort/filter buttons in compendium stats section (Covenant, Score, Wins, Streak, Clan)
        /// </summary>
        internal static string GetCompendiumSortButtonText(GameObject go)
        {
            try
            {
                // Only handle in compendium context
                if (!IsInCompendiumContext(go))
                    return null;

                // Check for FilterOptionButton component (clan filter, covenant filter)
                Component filterOptionButton = null;
                Transform current = go.transform;
                while (current != null && filterOptionButton == null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "FilterOptionButton")
                        {
                            filterOptionButton = component;
                            break;
                        }
                    }
                    current = current.parent;
                }

                if (filterOptionButton != null)
                {
                    var filterType = filterOptionButton.GetType();

                    // Try label TMP_Text field first
                    var labelField = filterType.GetField("label", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (labelField != null)
                    {
                        var label = labelField.GetValue(filterOptionButton);
                        if (label != null)
                        {
                            var textProp = label.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                            if (textProp != null)
                            {
                                string labelText = textProp.GetValue(label) as string;
                                if (!string.IsNullOrEmpty(labelText) && labelText != "?" && !DialogTextReader.IsGarbageText(labelText))
                                {
                                    MonsterTrainAccessibility.LogInfo($"FilterOptionButton label: {labelText}");
                                    return labelText.Trim();
                                }
                            }
                        }
                    }

                    // If label is empty/hidden, try tooltips for icon-only buttons (clan icons)
                    var tooltipsProp = filterType.GetProperty("tooltips", BindingFlags.Public | BindingFlags.Instance);
                    if (tooltipsProp != null)
                    {
                        var tooltipsList = tooltipsProp.GetValue(filterOptionButton);
                        if (tooltipsList != null)
                        {
                            var listType = tooltipsList.GetType();
                            var countProp = listType.GetProperty("Count");
                            int count = (int)countProp.GetValue(tooltipsList);
                            if (count > 0)
                            {
                                var indexer = listType.GetProperty("Item");
                                var tooltip = indexer.GetValue(tooltipsList, new object[] { 0 });
                                if (tooltip != null)
                                {
                                    var titleField = tooltip.GetType().GetField("title", BindingFlags.Public | BindingFlags.Instance);
                                    if (titleField != null)
                                    {
                                        string title = titleField.GetValue(tooltip) as string;
                                        if (!string.IsNullOrEmpty(title))
                                        {
                                            MonsterTrainAccessibility.LogInfo($"FilterOptionButton tooltip: {title}");
                                            return title.Trim();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Check if the object or parent has "button" in name and contains sort-related text
                string goName = go.name;
                if (goName.ToLower().Contains("button"))
                {
                    // Look for TMP text in children
                    foreach (var component in go.GetComponentsInChildren<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;
                        if (typeName == "TextMeshProUGUI" || typeName == "ExtendedTextMeshProUGUI" || typeName == "TextMeshPro")
                        {
                            var textProp = component.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                            if (textProp != null)
                            {
                                string text = textProp.GetValue(component) as string;
                                if (!string.IsNullOrEmpty(text) && text.Trim().Length > 0 && !DialogTextReader.IsGarbageText(text))
                                {
                                    return text.Trim();
                                }
                            }
                        }
                    }

                    // Fallback: clean up the button name
                    string cleanName = UITextHelper.CleanGameObjectName(goName);
                    if (!string.IsNullOrEmpty(cleanName) && cleanName.Length > 1)
                    {
                        return cleanName;
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting compendium sort button text: {ex.Message}");
            }
            return null;
        }

    }
}
