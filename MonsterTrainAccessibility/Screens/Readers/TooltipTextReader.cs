using MonsterTrainAccessibility.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Extracted reader for Tooltip UI elements.
    /// </summary>
    internal static class TooltipTextReader
    {

        /// <summary>
        /// Get both title and body from tooltip
        /// </summary>
        internal static void GetTooltipTitleAndBody(GameObject go, out string title, out string body)
        {
            title = null;
            body = null;

            try
            {
                Transform current = go.transform;
                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "TooltipProviderComponent")
                        {
                            var type = component.GetType();
                            var tooltipsField = type.GetField("_tooltips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (tooltipsField != null)
                            {
                                var tooltipsList = tooltipsField.GetValue(component) as System.Collections.IList;
                                if (tooltipsList != null && tooltipsList.Count > 0)
                                {
                                    var tooltip = tooltipsList[0];
                                    var tooltipType = tooltip.GetType();

                                    // Get title and localize if it's a key
                                    var titleField = tooltipType.GetField("title", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (titleField != null)
                                    {
                                        string rawTitle = titleField.GetValue(tooltip) as string;
                                        if (!string.IsNullOrEmpty(rawTitle))
                                        {
                                            // Try to localize - if it looks like a key
                                            title = LocalizationHelper.Localize(rawTitle);
                                            if (string.IsNullOrEmpty(title))
                                                title = rawTitle;
                                        }
                                    }

                                    // Get body and localize if it's a key
                                    var bodyField = tooltipType.GetField("body", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (bodyField != null)
                                    {
                                        string rawBody = bodyField.GetValue(tooltip) as string;
                                        if (!string.IsNullOrEmpty(rawBody))
                                        {
                                            // Try to localize - if it looks like a key
                                            body = LocalizationHelper.Localize(rawBody);
                                            if (string.IsNullOrEmpty(body))
                                                body = rawBody;
                                        }
                                    }

                                    return;
                                }
                            }
                        }
                    }
                    current = current.parent;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting tooltip title and body: {ex.Message}");
            }
        }


        /// <summary>
        /// Get tooltip text including body/description
        /// </summary>
        internal static string GetTooltipTextWithBody(GameObject go)
        {
            string title, body;
            GetTooltipTitleAndBody(go, out title, out body);

            if (!string.IsNullOrEmpty(title))
            {
                if (!string.IsNullOrEmpty(body))
                {
                    return $"{title}. {body}";
                }
                return title;
            }

            return null;
        }


        /// <summary>
        /// Get visited status from MapNodeUI component
        /// </summary>
        internal static string GetMapNodeVisitedStatus(GameObject go)
        {
            try
            {
                Transform current = go.transform;
                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;
                        if (typeName == "MapNodeUI")
                        {
                            var type = component.GetType();

                            // Check visited field
                            var visitedField = type.GetField("visited", BindingFlags.NonPublic | BindingFlags.Instance);
                            bool visited = false;
                            if (visitedField != null)
                            {
                                visited = (bool)visitedField.GetValue(component);
                            }

                            // Check canActivate field
                            var canActivateProp = type.GetProperty("CanActivate", BindingFlags.Public | BindingFlags.Instance);
                            bool canActivate = false;
                            if (canActivateProp != null)
                            {
                                canActivate = (bool)canActivateProp.GetValue(component);
                            }

                            if (visited)
                                return "visited";
                            else if (canActivate)
                                return "available";
                            else
                                return "locked";
                        }
                    }
                    current = current.parent;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting map node visited status: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Get detailed reward info from MapNodeUI/MapNodeData - extracts specific rewards
        /// </summary>
        internal static string GetMapNodeRewardDetails(GameObject go)
        {
            try
            {
                Transform current = go.transform;
                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;
                        if (typeName == "MapNodeUI")
                        {
                            var type = component.GetType();

                            // Get the MapNodeData
                            var dataField = type.GetField("data", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (dataField == null) continue;

                            var mapNodeData = dataField.GetValue(component);
                            if (mapNodeData == null) continue;

                            var dataType = mapNodeData.GetType();

                            // Check if it's a RewardNodeData
                            if (dataType.Name == "RewardNodeData" || dataType.BaseType?.Name == "RewardNodeData")
                            {
                                return ExtractRewardNodeDetails(mapNodeData, dataType);
                            }

                            // Check for DLC crystal cost
                            var crystalCostField = dataType.GetField("dlcHellforgedCrystalsCost", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (crystalCostField != null)
                            {
                                int cost = (int)crystalCostField.GetValue(mapNodeData);
                                if (cost > 0)
                                {
                                    return $"Costs {cost} crystal{(cost > 1 ? "s" : "")}";
                                }
                            }

                            return null;
                        }
                    }
                    current = current.parent;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting map node reward details: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Extract specific reward details from a RewardNodeData
        /// </summary>
        internal static string ExtractRewardNodeDetails(object rewardNodeData, Type dataType)
        {
            try
            {
                var details = new List<string>();

                // Check for crystal cost
                var crystalCostProp = dataType.GetProperty("DlcHellforgedCrystalsCost", BindingFlags.Public | BindingFlags.Instance);
                if (crystalCostProp != null)
                {
                    int cost = (int)crystalCostProp.GetValue(rewardNodeData);
                    if (cost > 0)
                    {
                        details.Add($"Costs {cost} crystal{(cost > 1 ? "s" : "")}");
                    }
                }

                // Try to get rewards list
                var getRewardsMethod = dataType.GetMethod("GetRewards", BindingFlags.Public | BindingFlags.Instance);
                if (getRewardsMethod != null)
                {
                    var rewards = getRewardsMethod.Invoke(rewardNodeData, null);
                    if (rewards is System.Collections.IEnumerable rewardList)
                    {
                        foreach (var reward in rewardList)
                        {
                            if (reward == null) continue;
                            var rewardType = reward.GetType();

                            // Get RewardTitle property
                            var titleProp = rewardType.GetProperty("RewardTitle", BindingFlags.Public | BindingFlags.Instance);
                            if (titleProp != null)
                            {
                                string title = titleProp.GetValue(reward) as string;
                                if (!string.IsNullOrEmpty(title) && !title.Contains("KEY>"))
                                {
                                    // Also try to get RewardValue for gold/health amounts
                                    var valueProp = rewardType.GetProperty("RewardValue", BindingFlags.Public | BindingFlags.Instance);
                                    if (valueProp != null)
                                    {
                                        int value = (int)valueProp.GetValue(reward);
                                        if (value > 0 && !title.Contains(value.ToString()))
                                        {
                                            details.Add($"{title}: {value}");
                                        }
                                        else
                                        {
                                            details.Add(title);
                                        }
                                    }
                                    else
                                    {
                                        details.Add(title);
                                    }
                                }
                            }

                            // Only add first reward to keep it concise
                            if (details.Count >= 2)
                                break;
                        }
                    }
                }

                return details.Count > 0 ? string.Join(", ", details) : null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting reward node details: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Try to get tooltip text from a GameObject's tooltip components
        /// </summary>
        internal static string GetTooltipText(GameObject go)
        {
            try
            {
                Transform current = go.transform;
                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        var type = component.GetType();
                        string typeName = type.Name;

                        // Look for TooltipProviderComponent specifically (Monster Train's tooltip system)
                        if (typeName == "TooltipProviderComponent" || typeName.Contains("TooltipProvider"))
                        {
                            string tooltipTitle = GetTooltipProviderTitle(component, type);
                            if (!string.IsNullOrEmpty(tooltipTitle))
                            {
                                MonsterTrainAccessibility.LogInfo($"Found tooltip title: {tooltipTitle}");
                                return tooltipTitle;
                            }
                        }

                        // Look for other tooltip-related components
                        if (typeName.Contains("Tooltip") || typeName.Contains("TooltipDisplay"))
                        {
                            // Try to get tooltip title/text
                            var titleField = type.GetField("titleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (titleField == null)
                                titleField = type.GetField("_titleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (titleField == null)
                                titleField = type.GetField("tooltipTitleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                            if (titleField != null)
                            {
                                string titleKey = titleField.GetValue(component) as string;
                                if (!string.IsNullOrEmpty(titleKey))
                                {
                                    string localized = LocalizationHelper.LocalizeOrNull(titleKey);
                                    if (!string.IsNullOrEmpty(localized))
                                        return localized;
                                }
                            }

                            // Try GetTitle method
                            var getTitleMethod = type.GetMethod("GetTitle", BindingFlags.Public | BindingFlags.Instance);
                            if (getTitleMethod != null && getTitleMethod.GetParameters().Length == 0)
                            {
                                var result = getTitleMethod.Invoke(component, null);
                                if (result is string title && !string.IsNullOrEmpty(title))
                                    return title;
                            }
                        }

                        // Look for scenario/battle data reference
                        if (typeName.Contains("Scenario") || typeName.Contains("Battle"))
                        {
                            // Try GetName method
                            var getNameMethod = type.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                            if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                            {
                                var result = getNameMethod.Invoke(component, null);
                                if (result is string name && !string.IsNullOrEmpty(name))
                                    return "Battle: " + name;
                            }
                        }
                    }
                    current = current.parent;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting tooltip text: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Extract title from TooltipProviderComponent or LocalizedTooltipProvider
        /// </summary>
        internal static string GetTooltipProviderTitle(Component tooltipProvider, Type type)
        {
            try
            {
                string typeName = type.Name;

                // Handle LocalizedTooltipProvider specifically
                if (typeName == "LocalizedTooltipProvider")
                {
                    // Try to get titleKey field
                    var titleKeyField = type.GetField("titleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                     ?? type.GetField("_titleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                     ?? type.GetField("tooltipTitleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (titleKeyField != null)
                    {
                        string titleKey = titleKeyField.GetValue(tooltipProvider) as string;
                        if (!string.IsNullOrEmpty(titleKey))
                        {
                            string localized = LocalizationHelper.Localize(titleKey);
                            if (!string.IsNullOrEmpty(localized))
                            {
                                MonsterTrainAccessibility.LogInfo($"LocalizedTooltipProvider title: {localized}");
                                return localized;
                            }
                        }
                    }

                    // Log fields for debugging
                    var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    MonsterTrainAccessibility.LogInfo($"LocalizedTooltipProvider fields: {string.Join(", ", fields.Select(f => f.Name))}");
                }

                // The TooltipProviderComponent has a _tooltips field which is a list of tooltip data
                var tooltipsField = type.GetField("_tooltips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (tooltipsField == null)
                {
                    // Try the property
                    var tooltipsProp = type.GetProperty("tooltips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (tooltipsProp != null)
                    {
                        var tooltipsList = tooltipsProp.GetValue(tooltipProvider) as System.Collections.IList;
                        if (tooltipsList != null && tooltipsList.Count > 0)
                        {
                            return ExtractTitleFromTooltip(tooltipsList[0]);
                        }
                    }
                }
                else
                {
                    var tooltipsList = tooltipsField.GetValue(tooltipProvider) as System.Collections.IList;
                    if (tooltipsList != null && tooltipsList.Count > 0)
                    {
                        return ExtractTitleFromTooltip(tooltipsList[0]);
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting tooltip provider title: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Extract the title from a tooltip data object
        /// </summary>
        internal static string ExtractTitleFromTooltip(object tooltip)
        {
            if (tooltip == null) return null;

            try
            {
                var tooltipType = tooltip.GetType();
                MonsterTrainAccessibility.LogInfo($"Tooltip type: {tooltipType.Name}");

                // Log fields for debugging
                var fields = tooltipType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MonsterTrainAccessibility.LogInfo($"Tooltip fields: {string.Join(", ", fields.Select(f => f.Name))}");

                // Try GetTitle method first
                var getTitleMethod = tooltipType.GetMethod("GetTitle", BindingFlags.Public | BindingFlags.Instance);
                if (getTitleMethod != null && getTitleMethod.GetParameters().Length == 0)
                {
                    var result = getTitleMethod.Invoke(tooltip, null);
                    if (result is string title && !string.IsNullOrEmpty(title))
                    {
                        MonsterTrainAccessibility.LogInfo($"Got title from GetTitle(): {title}");
                        return title;
                    }
                }

                // Try common title field names
                string[] titleFieldNames = { "title", "_title", "titleKey", "_titleKey", "tooltipTitleKey", "_tooltipTitleKey", "name", "_name" };
                foreach (var fieldName in titleFieldNames)
                {
                    var field = tooltipType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var value = field.GetValue(tooltip);
                        if (value is string str && !string.IsNullOrEmpty(str))
                        {
                            MonsterTrainAccessibility.LogInfo($"Found title in field {fieldName}: {str}");
                            // Try to localize if it looks like a key
                            string localized = LocalizationHelper.LocalizeOrNull(str);
                            if (!string.IsNullOrEmpty(localized) && localized != str)
                            {
                                MonsterTrainAccessibility.LogInfo($"Localized to: {localized}");
                                return localized;
                            }
                            return str;
                        }
                    }
                }

                // Try title properties
                string[] titlePropNames = { "Title", "TitleKey", "Name" };
                foreach (var propName in titlePropNames)
                {
                    var prop = tooltipType.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null)
                    {
                        var value = prop.GetValue(tooltip);
                        if (value is string str && !string.IsNullOrEmpty(str))
                        {
                            MonsterTrainAccessibility.LogInfo($"Found title in property {propName}: {str}");
                            string localized = LocalizationHelper.LocalizeOrNull(str);
                            if (!string.IsNullOrEmpty(localized) && localized != str)
                                return localized;
                            return str;
                        }
                    }
                }

                // Check if it has a nested data object (like CharacterData, ScenarioData)
                string[] dataFieldNames = { "data", "_data", "characterData", "_characterData", "scenarioData", "_scenarioData" };
                foreach (var fieldName in dataFieldNames)
                {
                    var field = tooltipType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var dataObj = field.GetValue(tooltip);
                        if (dataObj != null)
                        {
                            string name = GetNameFromDataObject(dataObj);
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting title from tooltip: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Get name from a game data object (CharacterData, ScenarioData, etc.)
        /// </summary>
        internal static string GetNameFromDataObject(object dataObj)
        {
            if (dataObj == null) return null;

            try
            {
                var dataType = dataObj.GetType();
                MonsterTrainAccessibility.LogInfo($"Data object type: {dataType.Name}");

                // Try GetName method
                var getNameMethod = dataType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                {
                    var result = getNameMethod.Invoke(dataObj, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                        return name;
                }

                // Try GetNameKey for localized names
                var getNameKeyMethod = dataType.GetMethod("GetNameKey", BindingFlags.Public | BindingFlags.Instance);
                if (getNameKeyMethod != null && getNameKeyMethod.GetParameters().Length == 0)
                {
                    var result = getNameKeyMethod.Invoke(dataObj, null);
                    if (result is string key && !string.IsNullOrEmpty(key))
                    {
                        string localized = LocalizationHelper.LocalizeOrNull(key);
                        return !string.IsNullOrEmpty(localized) ? localized : key;
                    }
                }

                // Try name fields
                string[] nameFields = { "name", "_name", "nameKey", "_nameKey" };
                foreach (var fieldName in nameFields)
                {
                    var field = dataType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var value = field.GetValue(dataObj);
                        if (value is string str && !string.IsNullOrEmpty(str))
                        {
                            string localized = LocalizationHelper.LocalizeOrNull(str);
                            return !string.IsNullOrEmpty(localized) ? localized : str;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting name from data object: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Get the name of a battle node from ScenarioData
        /// </summary>
        internal static string GetBattleNodeName(object scenarioData, Type dataType)
        {
            try
            {
                // Try battleNameKey field
                var battleNameField = dataType.GetField("battleNameKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (battleNameField == null)
                {
                    battleNameField = dataType.GetField("_battleNameKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                string battleNameKey = null;
                if (battleNameField != null)
                {
                    battleNameKey = battleNameField.GetValue(scenarioData) as string;
                }

                // Also try GetBattleName method
                if (string.IsNullOrEmpty(battleNameKey))
                {
                    var getBattleNameMethod = dataType.GetMethod("GetBattleName", BindingFlags.Public | BindingFlags.Instance);
                    if (getBattleNameMethod != null && getBattleNameMethod.GetParameters().Length == 0)
                    {
                        var result = getBattleNameMethod.Invoke(scenarioData, null);
                        if (result is string name && !string.IsNullOrEmpty(name))
                        {
                            return "Battle: " + name;
                        }
                    }
                }

                // Localize the key if found
                if (!string.IsNullOrEmpty(battleNameKey))
                {
                    string localized = LocalizationHelper.LocalizeOrNull(battleNameKey);
                    if (!string.IsNullOrEmpty(localized))
                    {
                        return "Battle: " + localized;
                    }
                }

                // Fallback to GetName or name property
                return GetFallbackNodeName(scenarioData, dataType, "Battle");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle node name: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Get the name of a generic node (reward, merchant, event, etc.)
        /// </summary>
        internal static string GetGenericNodeName(object nodeData, Type dataType)
        {
            try
            {
                // Try tooltipTitleKey field
                var titleField = dataType.GetField("tooltipTitleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (titleField == null)
                {
                    titleField = dataType.GetField("_tooltipTitleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                string titleKey = null;
                if (titleField != null)
                {
                    titleKey = titleField.GetValue(nodeData) as string;
                }

                // Localize the key if found
                if (!string.IsNullOrEmpty(titleKey))
                {
                    string localized = LocalizationHelper.LocalizeOrNull(titleKey);
                    if (!string.IsNullOrEmpty(localized))
                    {
                        return localized;
                    }
                }

                // Determine node type for prefix
                string typeName = dataType.Name;
                string prefix = "";
                if (typeName.Contains("Merchant") || typeName.Contains("Shop"))
                    prefix = "Shop";
                else if (typeName.Contains("Event"))
                    prefix = "Event";
                else if (typeName.Contains("Reward"))
                    prefix = "Reward";

                return GetFallbackNodeName(nodeData, dataType, prefix);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting generic node name: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Try fallback methods to get node name (GetName, name property, etc.)
        /// </summary>
        internal static string GetFallbackNodeName(object nodeData, Type dataType, string prefix)
        {
            try
            {
                // Try GetName method
                var getNameMethod = dataType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                {
                    var result = getNameMethod.Invoke(nodeData, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                    {
                        return string.IsNullOrEmpty(prefix) ? name : $"{prefix}: {name}";
                    }
                }

                // Try name property
                var nameProp = dataType.GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                if (nameProp != null)
                {
                    var result = nameProp.GetValue(nodeData);
                    if (result is string name && !string.IsNullOrEmpty(name))
                    {
                        // Clean up asset names (remove underscores, etc.)
                        name = CleanAssetName(name);
                        return string.IsNullOrEmpty(prefix) ? name : $"{prefix}: {name}";
                    }
                }
            }
            catch { }

            return null;
        }


        /// <summary>
        /// Clean up asset names to be more readable
        /// </summary>
        internal static string CleanAssetName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            // Remove common prefixes/suffixes
            name = name.Replace("_", " ");
            name = name.Replace("Data", "");
            name = name.Replace("Scenario", "");

            // Add spaces before capital letters
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");

            return name.Trim();
        }


        /// <summary>
        /// Get text for buttons with LocalizedTooltipProvider (mutator options, challenges, etc.)
        /// </summary>
        internal static string GetLocalizedTooltipButtonText(GameObject go)
        {
            try
            {
                // Check if this button has LocalizedTooltipProvider
                Component tooltipProvider = null;
                bool hasButtonToggle = false;

                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    string typeName = component.GetType().Name;

                    if (typeName == "LocalizedTooltipProvider")
                    {
                        tooltipProvider = component;
                    }
                    if (typeName == "ButtonStateBehaviourToggle")
                    {
                        hasButtonToggle = true;
                    }
                }

                // Only handle if we have LocalizedTooltipProvider
                if (tooltipProvider == null)
                    return null;

                var type = tooltipProvider.GetType();

                // Log fields for debugging
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MonsterTrainAccessibility.LogInfo($"LocalizedTooltipProvider fields: {string.Join(", ", fields.Select(f => f.Name))}");

                // Try to get the tooltip title
                string tooltipTitle = null;
                string tooltipBody = null;

                // Try various field names for title
                var titleFieldNames = new[] { "titleKey", "_titleKey", "tooltipTitleKey", "title", "_title" };
                foreach (var fieldName in titleFieldNames)
                {
                    var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        string titleKey = field.GetValue(tooltipProvider) as string;
                        if (!string.IsNullOrEmpty(titleKey))
                        {
                            tooltipTitle = LocalizationHelper.Localize(titleKey);
                            MonsterTrainAccessibility.LogInfo($"Found tooltip title key: {titleKey} -> {tooltipTitle}");
                            break;
                        }
                    }
                }

                // Try various field names for body
                var bodyFieldNames = new[] { "bodyKey", "_bodyKey", "tooltipBodyKey", "body", "_body", "descriptionKey" };
                foreach (var fieldName in bodyFieldNames)
                {
                    var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        string bodyKey = field.GetValue(tooltipProvider) as string;
                        if (!string.IsNullOrEmpty(bodyKey))
                        {
                            tooltipBody = LocalizationHelper.Localize(bodyKey);
                            MonsterTrainAccessibility.LogInfo($"Found tooltip body key: {bodyKey} -> {tooltipBody}");
                            break;
                        }
                    }
                }

                // Build result from button name and tooltip
                var result = new StringBuilder();

                // Use clean button name
                string buttonName = UITextHelper.CleanGameObjectName(go.name);
                if (!string.IsNullOrEmpty(buttonName))
                {
                    result.Append(buttonName);
                }

                // Add tooltip title if different from button name
                if (!string.IsNullOrEmpty(tooltipTitle) && tooltipTitle != buttonName)
                {
                    if (result.Length > 0)
                        result.Append(": ");
                    result.Append(tooltipTitle);
                }

                // Add tooltip body
                if (!string.IsNullOrEmpty(tooltipBody))
                {
                    if (result.Length > 0)
                        result.Append(". ");
                    result.Append(TextUtilities.StripRichTextTags(tooltipBody));
                }

                // Check if button shows locked state
                if (hasButtonToggle)
                {
                    // Check interactable state
                    var button = go.GetComponent<Button>();
                    if (button != null && !button.interactable)
                    {
                        result.Append(" (Locked)");
                    }
                }

                if (result.Length > 0)
                    return result.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting localized tooltip button text: {ex.Message}");
            }
            return null;
        }

    }
}
