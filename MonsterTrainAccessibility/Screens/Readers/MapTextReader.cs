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
    /// Extracted reader for Map UI elements.
    /// </summary>
    internal static class MapTextReader
    {

        /// <summary>
        /// Get text for map nodes (battles, events, shops, etc.)
        /// Extracts proper encounter names instead of just button labels like "Fight!"
        /// </summary>
        internal static string GetMapNodeText(GameObject go)
        {
            try
            {
                // Check for Minimap components first (Monster Train's map system)
                var mapInfo = GetMinimapNodeInfo(go);
                if (mapInfo != null)
                {
                    return mapInfo;
                }

                // Look for MapNodeIcon or similar component on this object or parents
                Component mapNodeComponent = null;
                Transform current = go.transform;

                while (current != null && mapNodeComponent == null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;

                        // Look for various map-related component names
                        if (typeName.Contains("MapNode") ||
                            typeName.Contains("NodeIcon") ||
                            typeName.Contains("MapIcon") ||
                            typeName.Contains("RouteNode"))
                        {
                            mapNodeComponent = component;
                            MonsterTrainAccessibility.LogInfo($"Found map component: {typeName}");
                            break;
                        }
                    }
                    current = current.parent;
                }

                if (mapNodeComponent == null)
                {
                    // No map component found. Only try tooltip fallback on map-related screens
                    // to avoid stealing tooltip text from unrelated screens (e.g., champion buttons on clan selection).
                    var currentScreen = Help.ScreenStateTracker.CurrentScreen;
                    if (currentScreen == Help.GameScreen.Map || currentScreen == Help.GameScreen.BattleIntro ||
                        currentScreen == Help.GameScreen.Rewards || currentScreen == Help.GameScreen.ChampionUpgrade)
                    {
                        string tooltipText = TooltipTextReader.GetTooltipTextWithBody(go);
                        if (!string.IsNullOrEmpty(tooltipText) && !tooltipText.Contains("Enemy_Tooltip"))
                        {
                            return tooltipText;
                        }
                    }
                    return null;
                }

                // Try to get the MapNodeData from the component
                var iconType = mapNodeComponent.GetType();
                object mapNodeData = null;

                // Try common field/property names for the node data
                var dataField = iconType.GetField("mapNodeData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (dataField != null)
                {
                    mapNodeData = dataField.GetValue(mapNodeComponent);
                }
                else
                {
                    var dataProp = iconType.GetProperty("MapNodeData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (dataProp != null)
                    {
                        mapNodeData = dataProp.GetValue(mapNodeComponent);
                    }
                }

                // Also try _mapNodeData (common naming convention)
                if (mapNodeData == null)
                {
                    dataField = iconType.GetField("_mapNodeData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (dataField != null)
                    {
                        mapNodeData = dataField.GetValue(mapNodeComponent);
                    }
                }

                if (mapNodeData == null)
                {
                    // MapNodeUI found but no mapNodeData - try tooltip fallback
                    MonsterTrainAccessibility.LogInfo($"MapNodeUI found but mapNodeData is null, trying tooltip fallback");
                    string tooltipText = TooltipTextReader.GetTooltipTextWithBody(go);
                    MonsterTrainAccessibility.LogInfo($"Tooltip fallback result: '{tooltipText ?? "null"}'");
                    if (!string.IsNullOrEmpty(tooltipText) && !tooltipText.Contains("Enemy_Tooltip"))
                    {
                        // Clean sprite tags from tooltip
                        return TextUtilities.CleanSpriteTagsForSpeech(tooltipText);
                    }
                    // Log available fields for debugging
                    LogMapNodeUIFields(mapNodeComponent);
                    return null;
                }

                var nodeDataType = mapNodeData.GetType();
                string nodeName = nodeDataType.Name;

                // Check if this is a ScenarioData (battle node)
                if (nodeName == "ScenarioData" || nodeDataType.BaseType?.Name == "ScenarioData")
                {
                    return TooltipTextReader.GetBattleNodeName(mapNodeData, nodeDataType);
                }

                // For other node types (rewards, merchants, etc.), try tooltipTitleKey
                return TooltipTextReader.GetGenericNodeName(mapNodeData, nodeDataType);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting map node text: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Get text for map branch choice elements (when choosing between paths)
        /// </summary>
        internal static string GetBranchChoiceText(GameObject go)
        {
            try
            {
                // Skip ClassSelectionIcon and ChampionChoiceButton elements - they have their own handlers
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    string typeName = component.GetType().Name;
                    if (typeName == "ClassSelectionIcon" || typeName == "ChampionChoiceButton")
                        return null;

                    // Handle StoryChoiceItem (random event choices)
                    if (typeName == "StoryChoiceItem")
                    {
                        return EventTextReader.GetStoryChoiceText(go, component);
                    }
                }

                // Check if this is a branch choice element or map navigation arrow
                string goName = go.name.ToLower();
                bool isBranchChoice = goName.Contains("branch") || goName.Contains("choice");
                bool isMapArrow = goName.Contains("arrow") || goName.Contains("navigation") ||
                                  goName.Contains("left") || goName.Contains("right");

                // Also check parent names
                if (!isBranchChoice && !isMapArrow && go.transform.parent != null)
                {
                    string parentName = go.transform.parent.name.ToLower();
                    isBranchChoice = parentName.Contains("branch") || parentName.Contains("choice");
                    isMapArrow = parentName.Contains("arrow") || parentName.Contains("navigation");
                }

                if (!isBranchChoice && !isMapArrow)
                    return null;

                MonsterTrainAccessibility.LogInfo($"Found map choice element: {go.name}");

                // Try to enumerate all nodes on this branch from the MapSection
                string allNodes = GetAllBranchNodeTitles(go);
                if (!string.IsNullOrEmpty(allNodes))
                {
                    string direction = "";
                    if (goName.Contains("left"))
                        direction = "Left path: ";
                    else if (goName.Contains("right"))
                        direction = "Right path: ";

                    return direction + allNodes;
                }

                // Fallback: try to find the currently visible tooltip on screen
                string visibleTooltip = GetVisibleMapTooltip();
                if (!string.IsNullOrEmpty(visibleTooltip))
                {
                    // Determine direction if it's an arrow
                    string direction = "";
                    if (goName.Contains("left"))
                        direction = "Left path: ";
                    else if (goName.Contains("right"))
                        direction = "Right path: ";

                    return direction + visibleTooltip;
                }

                // Try to get destination node info from the button component
                string destInfo = GetMapArrowDestination(go);
                if (!string.IsNullOrEmpty(destInfo))
                {
                    return destInfo;
                }

                // Look for node type indicators in children (icons, labels)
                var nodeType = GetBranchNodeType(go);

                // Look for enemy names or battle info in tooltip or children
                string enemyInfo = GetBranchEnemyInfo(go);

                if (!string.IsNullOrEmpty(nodeType))
                {
                    if (!string.IsNullOrEmpty(enemyInfo))
                    {
                        return $"{nodeType}: {enemyInfo}";
                    }
                    return nodeType;
                }

                // Fallback: try to find any meaningful text in children
                string childText = GetFirstMeaningfulChildText(go);
                if (!string.IsNullOrEmpty(childText))
                {
                    return $"Path: {childText}";
                }

                return "Map path option";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting branch choice text: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Get all node titles for a branch path from the MapSection data.
        /// Returns comma-separated list of node names (e.g. "Artifact, Gold, Card Draft").
        /// </summary>
        internal static string GetAllBranchNodeTitles(GameObject go)
        {
            try
            {
                bool isLeft = go.name.ToLower().Contains("left");
                bool isRight = go.name.ToLower().Contains("right");
                if (!isLeft && !isRight) return null;

                // Find the MapSection component from parent hierarchy
                Component mapSection = FindMapSectionComponent(go.transform);
                if (mapSection == null) return null;

                var sectionType = mapSection.GetType();

                // Access the branch-specific node list (leftMapNodes or rightMapNodes)
                string listFieldName = isLeft ? "leftMapNodes" : "rightMapNodes";
                var listField = sectionType.GetField(listFieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (listField == null)
                {
                    MonsterTrainAccessibility.LogInfo($"Could not find field {listFieldName} on MapSection");
                    return null;
                }

                var nodeList = listField.GetValue(mapSection) as System.Collections.IList;

                // Also get shared nodes
                var sharedField = sectionType.GetField("sharedMapNodes", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                var sharedList = sharedField?.GetValue(mapSection) as System.Collections.IList;

                var titles = new List<string>();

                // Collect titles from branch-specific nodes
                if (nodeList != null)
                {
                    foreach (var node in nodeList)
                    {
                        string title = GetMapNodeUITitle(node);
                        if (!string.IsNullOrEmpty(title))
                            titles.Add(title);
                    }
                }

                // Collect titles from shared nodes
                if (sharedList != null)
                {
                    foreach (var node in sharedList)
                    {
                        string title = GetMapNodeUITitle(node);
                        if (!string.IsNullOrEmpty(title))
                            titles.Add(title);
                    }
                }

                if (titles.Count == 0) return null;

                MonsterTrainAccessibility.LogInfo($"Branch node titles: {string.Join(", ", titles)}");
                return string.Join(", ", titles);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting branch node titles: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Get the tooltip title from a MapNodeUI component.
        /// Returns null if the node is inactive or has no data.
        /// </summary>
        internal static string GetMapNodeUITitle(object mapNodeUI)
        {
            try
            {
                if (mapNodeUI == null) return null;

                var uiType = mapNodeUI.GetType();

                // Check if the GameObject is active
                var goProp = uiType.GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance);
                if (goProp != null)
                {
                    var gameObj = goProp.GetValue(mapNodeUI) as GameObject;
                    if (gameObj == null || !gameObj.activeInHierarchy) return null;
                }

                // Get the MapNodeData from the 'data' field
                var dataField = uiType.GetField("data", BindingFlags.NonPublic | BindingFlags.Instance);
                if (dataField == null) return null;

                var data = dataField.GetValue(mapNodeUI);
                if (data == null) return null;

                var dataType = data.GetType();

                // Try GetTooltipTitle() for localized name
                var getTitleMethod = dataType.GetMethod("GetTooltipTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    var result = getTitleMethod.Invoke(data, null);
                    if (result is string title && !string.IsNullOrEmpty(title))
                        return title;
                }

                // Fallback: use asset name, cleaned up
                var nameProp = dataType.GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                if (nameProp != null)
                {
                    var name = nameProp.GetValue(data) as string;
                    if (!string.IsNullOrEmpty(name))
                        return TooltipTextReader.CleanAssetName(name);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting MapNodeUI title: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Try to get destination info from a map arrow button
        /// </summary>
        internal static string GetMapArrowDestination(GameObject go)
        {
            try
            {
                // Determine if this is left or right button
                bool isLeft = go.name.ToLower().Contains("left");
                bool isRight = go.name.ToLower().Contains("right");
                string direction = isLeft ? "Left" : (isRight ? "Right" : "");
                int buttonIndex = isLeft ? 0 : (isRight ? 1 : -1);

                // Look for BranchChoiceUI component on this object or parent
                Component branchChoiceUI = null;
                Transform current = go.transform;

                while (current != null && branchChoiceUI == null)
                {
                    foreach (var comp in current.GetComponents<Component>())
                    {
                        if (comp != null && comp.GetType().Name == "BranchChoiceUI")
                        {
                            branchChoiceUI = comp;
                            break;
                        }
                    }
                    current = current.parent;
                }

                if (branchChoiceUI != null)
                {
                    var bcType = branchChoiceUI.GetType();
                    var allFields = bcType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    // lastHighlightedBranch is an int index, we need to find MapSection for actual data
                    int branchIndex = buttonIndex; // Default to our position-based index
                    var highlightedField = allFields.FirstOrDefault(f => f.Name == "lastHighlightedBranch");
                    if (highlightedField != null)
                    {
                        var highlightedValue = highlightedField.GetValue(branchChoiceUI);
                        if (highlightedValue is int idx)
                        {
                            branchIndex = idx;
                            MonsterTrainAccessibility.LogInfo($"lastHighlightedBranch index: {branchIndex}");
                        }
                    }

                    // Find the MapSection component which has the actual branch node data
                    Component mapSection = FindMapSectionComponent(branchChoiceUI.transform);
                    if (mapSection != null)
                    {
                        string branchInfo = GetBranchInfoFromMapSection(mapSection, branchIndex);
                        if (!string.IsNullOrEmpty(branchInfo))
                        {
                            return $"{direction} path: {branchInfo}";
                        }
                    }
                }

                // Fallback: look at direct components on the GO
                var components = go.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    var type = comp.GetType();

                    var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var field in fields)
                    {
                        string fieldName = field.Name.ToLower();
                        if (fieldName.Contains("node") || fieldName.Contains("destination") || fieldName.Contains("target") || fieldName.Contains("branch"))
                        {
                            var value = field.GetValue(comp);
                            if (value != null)
                            {
                                string info = ExtractMapNodeInfo(value);
                                if (!string.IsNullOrEmpty(info))
                                {
                                    return $"{direction} path: {info}";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting map arrow destination: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Extract info from a branch button item (from branchButtons list)
        /// </summary>
        internal static string ExtractBranchButtonInfo(object buttonItem)
        {
            if (buttonItem == null) return null;

            try
            {
                var itemType = buttonItem.GetType();
                var itemFields = itemType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                MonsterTrainAccessibility.LogInfo($"Extracting from {itemType.Name}, fields: {string.Join(", ", itemFields.Select(f => f.Name))}");

                // Look for node data, map node, or reward data fields
                foreach (var field in itemFields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("node") || fieldName.Contains("data") || fieldName.Contains("reward") ||
                        fieldName.Contains("branch") || fieldName.Contains("encounter"))
                    {
                        var value = field.GetValue(buttonItem);
                        if (value != null && !value.GetType().IsPrimitive && value.GetType() != typeof(string))
                        {
                            MonsterTrainAccessibility.LogInfo($"Found potential node data in field: {field.Name} ({value.GetType().Name})");
                            string info = ExtractMapNodeInfo(value);
                            if (!string.IsNullOrEmpty(info))
                            {
                                return info;
                            }
                        }
                    }
                }

                // Try methods on the button item itself
                var methods = itemType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetParameters().Length == 0)
                    .ToArray();

                foreach (var method in methods)
                {
                    string methodName = method.Name;
                    if (methodName.StartsWith("Get") &&
                        (methodName.Contains("Node") || methodName.Contains("Name") || methodName.Contains("Description") ||
                         methodName.Contains("Reward") || methodName.Contains("Encounter")))
                    {
                        try
                        {
                            var result = method.Invoke(buttonItem, null);
                            if (result is string str && !string.IsNullOrEmpty(str))
                            {
                                MonsterTrainAccessibility.LogInfo($"Got string from {methodName}: {str}");
                                return TextUtilities.StripRichTextTags(str);
                            }
                            else if (result != null && !result.GetType().IsPrimitive)
                            {
                                string info = ExtractMapNodeInfo(result);
                                if (!string.IsNullOrEmpty(info))
                                {
                                    return info;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting branch button info: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Find the MapSection component in the hierarchy
        /// </summary>
        internal static Component FindMapSectionComponent(Transform startFrom)
        {
            if (startFrom == null) return null;

            // Look up the hierarchy for MapSection
            Transform current = startFrom;
            while (current != null)
            {
                foreach (var comp in current.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == "MapSection")
                    {
                        MonsterTrainAccessibility.LogInfo($"Found MapSection on {current.name}");
                        return comp;
                    }
                }
                current = current.parent;
            }

            // Also look down the hierarchy
            foreach (var comp in startFrom.GetComponentsInChildren<Component>(true))
            {
                if (comp != null && comp.GetType().Name == "MapSection")
                {
                    MonsterTrainAccessibility.LogInfo($"Found MapSection in children: {comp.gameObject.name}");
                    return comp;
                }
            }

            return null;
        }


        /// <summary>
        /// Get branch info from a MapSection component
        /// </summary>
        internal static string GetBranchInfoFromMapSection(Component mapSection, int branchIndex)
        {
            if (mapSection == null) return null;

            try
            {
                var sectionType = mapSection.GetType();
                var allFields = sectionType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var allMethods = sectionType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetParameters().Length == 0 && m.Name.StartsWith("Get"))
                    .Select(m => m.Name)
                    .Take(30)
                    .ToArray();

                MonsterTrainAccessibility.LogInfo($"MapSection fields: {string.Join(", ", allFields.Select(f => f.Name))}");
                MonsterTrainAccessibility.LogInfo($"MapSection methods: {string.Join(", ", allMethods)}");

                // Look for branch data fields - common patterns
                string[] branchFieldNames = new[] {
                    "branches", "branchNodes", "branchData", "nodes",
                    "nextNodes", "choices", "rewards", "encounters",
                    "mapNodes", "sectionNodes", "nodeData"
                };

                foreach (var fieldName in branchFieldNames)
                {
                    var field = allFields.FirstOrDefault(f =>
                        f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase) ||
                        f.Name.ToLower().Contains(fieldName.ToLower()));

                    if (field != null)
                    {
                        var value = field.GetValue(mapSection);
                        MonsterTrainAccessibility.LogInfo($"Found field {field.Name}: {value?.GetType().Name ?? "null"}");

                        if (value is System.Collections.IList list && list.Count > 0)
                        {
                            MonsterTrainAccessibility.LogInfo($"Field {field.Name} is list with {list.Count} items");

                            // Get item at branchIndex or first/last based on index
                            int index = branchIndex >= 0 && branchIndex < list.Count ? branchIndex : 0;
                            var nodeData = list[index];

                            if (nodeData != null)
                            {
                                string info = ExtractMapNodeInfo(nodeData);
                                if (!string.IsNullOrEmpty(info))
                                {
                                    return info;
                                }
                            }
                        }
                        else if (value != null && !value.GetType().IsPrimitive && value.GetType() != typeof(string))
                        {
                            string info = ExtractMapNodeInfo(value);
                            if (!string.IsNullOrEmpty(info))
                            {
                                return info;
                            }
                        }
                    }
                }

                // Try methods that might return branch/node data
                string[] nodeMethodNames = new[] {
                    "GetBranches", "GetNodes", "GetNextNodes", "GetRewards",
                    "GetMapNodes", "GetNodeData", "GetCurrentNode", "GetSectionData"
                };

                foreach (var methodName in nodeMethodNames)
                {
                    var method = sectionType.GetMethod(methodName, Type.EmptyTypes);
                    if (method != null)
                    {
                        try
                        {
                            var result = method.Invoke(mapSection, null);
                            MonsterTrainAccessibility.LogInfo($"{methodName} returned: {result?.GetType().Name ?? "null"}");

                            if (result is System.Collections.IList list && list.Count > 0)
                            {
                                int index = branchIndex >= 0 && branchIndex < list.Count ? branchIndex : 0;
                                var nodeData = list[index];
                                if (nodeData != null)
                                {
                                    string info = ExtractMapNodeInfo(nodeData);
                                    if (!string.IsNullOrEmpty(info))
                                    {
                                        return info;
                                    }
                                }
                            }
                            else if (result != null && !result.GetType().IsPrimitive && result.GetType() != typeof(string))
                            {
                                string info = ExtractMapNodeInfo(result);
                                if (!string.IsNullOrEmpty(info))
                                {
                                    return info;
                                }
                            }
                        }
                        catch { }
                    }
                }

                // Look for specific data types that might contain node info
                foreach (var field in allFields)
                {
                    var value = field.GetValue(mapSection);
                    if (value == null) continue;

                    var valueType = value.GetType();
                    string typeName = valueType.Name.ToLower();

                    // Look for types that sound like they contain node/reward data
                    if (typeName.Contains("node") || typeName.Contains("reward") ||
                        typeName.Contains("encounter") || typeName.Contains("branch"))
                    {
                        MonsterTrainAccessibility.LogInfo($"Found potential data type: {field.Name} = {valueType.Name}");

                        if (value is System.Collections.IList list && list.Count > 0)
                        {
                            int index = branchIndex >= 0 && branchIndex < list.Count ? branchIndex : 0;
                            string info = ExtractMapNodeInfo(list[index]);
                            if (!string.IsNullOrEmpty(info))
                            {
                                return info;
                            }
                        }
                        else
                        {
                            string info = ExtractMapNodeInfo(value);
                            if (!string.IsNullOrEmpty(info))
                            {
                                return info;
                            }
                        }
                    }
                }

                // Look for branch node arrays/lists with left/right entries
                string[] branchArrayNames = new[] { "branchNodeUIs", "branchNodes", "nodeUIs", "leftNode", "rightNode" };
                foreach (var arrayName in branchArrayNames)
                {
                    var field = allFields.FirstOrDefault(f => f.Name.ToLower().Contains(arrayName.ToLower()));
                    if (field != null)
                    {
                        var value = field.GetValue(mapSection);
                        if (value is System.Collections.IList list && list.Count > branchIndex && branchIndex >= 0)
                        {
                            var nodeUI = list[branchIndex];
                            string info = ExtractMapNodeUIInfo(nodeUI);
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
                MonsterTrainAccessibility.LogError($"Error getting branch info from MapSection: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Extract info from a MapBattleNodeUI or similar UI component
        /// </summary>
        internal static string ExtractMapNodeUIInfo(object nodeUI)
        {
            if (nodeUI == null) return null;

            try
            {
                var uiType = nodeUI.GetType();
                var uiMethods = uiType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                var uiFields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // First priority: Try GetMapNodeDataName() method - this should give us the actual name
                var getNameMethod = uiMethods.FirstOrDefault(m => m.Name == "GetMapNodeDataName" && m.GetParameters().Length == 0);
                if (getNameMethod != null)
                {
                    try
                    {
                        var nodeName = getNameMethod.Invoke(nodeUI, null) as string;
                        if (!string.IsNullOrEmpty(nodeName))
                        {
                            MonsterTrainAccessibility.LogInfo($"GetMapNodeDataName returned: {nodeName}");
                            return TextUtilities.StripRichTextTags(nodeName);
                        }
                    }
                    catch { }
                }

                // Second priority: Try GetData() method to get the actual node data
                var getDataMethod = uiMethods.FirstOrDefault(m => m.Name == "GetData" && m.GetParameters().Length == 0);
                if (getDataMethod != null)
                {
                    try
                    {
                        var data = getDataMethod.Invoke(nodeUI, null);
                        if (data != null)
                        {
                            MonsterTrainAccessibility.LogInfo($"GetData returned: {data.GetType().Name}");
                            string dataInfo = ExtractNodeDataInfo(data);
                            if (!string.IsNullOrEmpty(dataInfo))
                            {
                                return dataInfo;
                            }
                        }
                    }
                    catch { }
                }

                // Third priority: Try "data" field
                var dataField = uiFields.FirstOrDefault(f => f.Name == "data");
                if (dataField != null)
                {
                    var data = dataField.GetValue(nodeUI);
                    if (data != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"data field contains: {data.GetType().Name}");
                        string dataInfo = ExtractNodeDataInfo(data);
                        if (!string.IsNullOrEmpty(dataInfo))
                        {
                            return dataInfo;
                        }
                    }
                }

                // Fourth priority: Try tooltipProvider component
                var providerField = uiFields.FirstOrDefault(f => f.Name == "tooltipProvider");
                if (providerField != null)
                {
                    var provider = providerField.GetValue(nodeUI);
                    if (provider != null)
                    {
                        var providerInfo = ExtractTooltipProviderInfo(provider);
                        if (!string.IsNullOrEmpty(providerInfo))
                        {
                            MonsterTrainAccessibility.LogInfo($"Got info from tooltipProvider: {providerInfo}");
                            return providerInfo;
                        }
                    }
                }

                // Fifth priority: Check if boss and get basic info
                bool isBoss = false;
                var bossField = uiFields.FirstOrDefault(f => f.Name == "isBoss");
                if (bossField != null)
                {
                    var bossValue = bossField.GetValue(nodeUI);
                    if (bossValue is bool b)
                        isBoss = b;
                }

                if (isBoss)
                {
                    return "Boss Battle";
                }

                // Last resort: tooltip title/content (localization keys)
                string title = null;
                var titleField = uiFields.FirstOrDefault(f => f.Name == "defaultTooltipTitle");
                if (titleField != null)
                {
                    title = titleField.GetValue(nodeUI) as string;
                    if (!string.IsNullOrEmpty(title))
                    {
                        // Try to localize
                        string localized = LocalizationHelper.Localize(title);
                        if (!string.IsNullOrEmpty(localized) && localized != title)
                        {
                            return TextUtilities.StripRichTextTags(localized);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting map node UI info: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Extract info from node data (the actual game data, not UI)
        /// </summary>
        internal static string ExtractNodeDataInfo(object data)
        {
            if (data == null) return null;

            try
            {
                var dataType = data.GetType();
                MonsterTrainAccessibility.LogInfo($"Extracting node data from: {dataType.Name}");

                // Log available methods
                var methods = dataType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetParameters().Length == 0 && m.Name.StartsWith("Get"))
                    .Select(m => m.Name)
                    .Take(20)
                    .ToArray();
                MonsterTrainAccessibility.LogInfo($"Data methods: {string.Join(", ", methods)}");

                // Try GetName first
                var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(data, null) as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        MonsterTrainAccessibility.LogInfo($"GetName returned: {name}");
                        return TextUtilities.StripRichTextTags(name);
                    }
                }

                // Try GetTooltipTitle
                var getTitleMethod = dataType.GetMethod("GetTooltipTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    var title = getTitleMethod.Invoke(data, null) as string;
                    if (!string.IsNullOrEmpty(title))
                    {
                        MonsterTrainAccessibility.LogInfo($"GetTooltipTitle returned: {title}");
                        return TextUtilities.StripRichTextTags(title);
                    }
                }

                // Try GetDescription
                var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    var desc = getDescMethod.Invoke(data, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                    {
                        MonsterTrainAccessibility.LogInfo($"GetDescription returned: {desc}");
                        return TextUtilities.StripRichTextTags(desc);
                    }
                }

                // Try to get reward info for reward nodes
                var getRewardMethod = dataType.GetMethod("GetReward", Type.EmptyTypes) ??
                                     dataType.GetMethod("GetRewardData", Type.EmptyTypes);
                if (getRewardMethod != null)
                {
                    var reward = getRewardMethod.Invoke(data, null);
                    if (reward != null)
                    {
                        string rewardInfo = ExtractRewardInfo(reward);
                        if (!string.IsNullOrEmpty(rewardInfo))
                        {
                            return rewardInfo;
                        }
                    }
                }

                // Look at fields
                var fields = dataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MonsterTrainAccessibility.LogInfo($"Data fields: {string.Join(", ", fields.Select(f => f.Name).Take(15))}");

                // Try name field
                var nameField = fields.FirstOrDefault(f => f.Name.ToLower() == "name" || f.Name.ToLower().Contains("nodename"));
                if (nameField != null)
                {
                    var name = nameField.GetValue(data) as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        return TextUtilities.StripRichTextTags(LocalizationHelper.Localize(name));
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting node data info: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Extract info from a tooltip provider component
        /// </summary>
        internal static string ExtractTooltipProviderInfo(object provider)
        {
            if (provider == null) return null;

            try
            {
                var providerType = provider.GetType();

                // Try GetTitle/GetDescription methods
                var getTitleMethod = providerType.GetMethod("GetTitle", Type.EmptyTypes) ??
                                    providerType.GetMethod("GetTooltipTitle", Type.EmptyTypes);
                var getDescMethod = providerType.GetMethod("GetDescription", Type.EmptyTypes) ??
                                   providerType.GetMethod("GetTooltipDescription", Type.EmptyTypes) ??
                                   providerType.GetMethod("GetTooltipBody", Type.EmptyTypes);

                string title = null;
                string desc = null;

                if (getTitleMethod != null)
                {
                    title = getTitleMethod.Invoke(provider, null) as string;
                }
                if (getDescMethod != null)
                {
                    desc = getDescMethod.Invoke(provider, null) as string;
                }

                // Also try fields
                var providerFields = providerType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (string.IsNullOrEmpty(title))
                {
                    var titleField = providerFields.FirstOrDefault(f => f.Name.ToLower().Contains("title"));
                    if (titleField != null)
                        title = titleField.GetValue(provider) as string;
                }
                if (string.IsNullOrEmpty(desc))
                {
                    var descField = providerFields.FirstOrDefault(f =>
                        f.Name.ToLower().Contains("desc") || f.Name.ToLower().Contains("content") || f.Name.ToLower().Contains("body"));
                    if (descField != null)
                        desc = descField.GetValue(provider) as string;
                }

                List<string> parts = new List<string>();
                if (!string.IsNullOrEmpty(title))
                    parts.Add(LocalizationHelper.Localize(title));
                if (!string.IsNullOrEmpty(desc) && desc != title)
                    parts.Add(LocalizationHelper.Localize(desc));

                if (parts.Count > 0)
                {
                    return TextUtilities.StripRichTextTags(string.Join(". ", parts));
                }
            }
            catch { }

            return null;
        }


        /// <summary>
        /// Extract readable info from a map node data object
        /// </summary>
        internal static string ExtractMapNodeInfo(object nodeData)
        {
            try
            {
                var nodeType = nodeData.GetType();
                MonsterTrainAccessibility.LogInfo($"Extracting info from node type: {nodeType.Name}");

                // If this is a UI component (MapBattleNodeUI, etc.), use specialized extraction
                if (nodeType.Name.Contains("NodeUI") || nodeType.Name.Contains("MapNode"))
                {
                    string uiInfo = ExtractMapNodeUIInfo(nodeData);
                    if (!string.IsNullOrEmpty(uiInfo))
                    {
                        return uiInfo;
                    }
                }

                // Log methods available
                var methods = nodeType.GetMethods()
                    .Where(m => m.GetParameters().Length == 0 && m.Name.StartsWith("Get"))
                    .Select(m => m.Name)
                    .Take(30)
                    .ToArray();
                MonsterTrainAccessibility.LogInfo($"Node methods: {string.Join(", ", methods)}");

                // Also log fields
                var fields = nodeType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Select(f => f.Name)
                    .Take(20)
                    .ToArray();
                MonsterTrainAccessibility.LogInfo($"Node fields: {string.Join(", ", fields)}");

                string name = null;
                string description = null;
                string nodeTypeStr = null;

                // Try to get node type first (battle, event, shop, etc.)
                var getTypeMethod = nodeType.GetMethod("GetNodeType", Type.EmptyTypes) ??
                                   nodeType.GetMethod("GetMapNodeType", Type.EmptyTypes) ??
                                   nodeType.GetMethod("GetRewardType", Type.EmptyTypes);
                if (getTypeMethod != null)
                {
                    var nodeTypeValue = getTypeMethod.Invoke(nodeData, null);
                    if (nodeTypeValue != null)
                    {
                        nodeTypeStr = nodeTypeValue.ToString();
                        MonsterTrainAccessibility.LogInfo($"Node type: {nodeTypeStr}");
                    }
                }

                // Try various methods to get name
                string[] nameMethodNames = new[] {
                    "GetName", "GetTitle", "GetDisplayName", "GetNodeName",
                    "GetNameKey", "GetTitleKey", "GetTooltipTitle",
                    "GetRewardName", "GetEncounterName"
                };

                foreach (var methodName in nameMethodNames)
                {
                    var getNameMethod = nodeType.GetMethod(methodName, Type.EmptyTypes);
                    if (getNameMethod != null)
                    {
                        var result = getNameMethod.Invoke(nodeData, null);
                        if (result is string str && !string.IsNullOrEmpty(str))
                        {
                            MonsterTrainAccessibility.LogInfo($"{methodName} returned: {str}");
                            // Try to localize if it looks like a key
                            name = LocalizationHelper.Localize(str);
                            if (!string.IsNullOrEmpty(name))
                                break;
                        }
                    }
                }

                // Try to get description
                string[] descMethodNames = new[] {
                    "GetDescription", "GetTooltipDescription", "GetDescriptionKey",
                    "GetRewardDescription", "GetTooltipBody"
                };

                foreach (var methodName in descMethodNames)
                {
                    var getDescMethod = nodeType.GetMethod(methodName, Type.EmptyTypes);
                    if (getDescMethod != null)
                    {
                        var result = getDescMethod.Invoke(nodeData, null);
                        if (result is string str && !string.IsNullOrEmpty(str))
                        {
                            MonsterTrainAccessibility.LogInfo($"{methodName} returned: {str}");
                            description = LocalizationHelper.Localize(str);
                            if (!string.IsNullOrEmpty(description))
                                break;
                        }
                    }
                }

                // Try to get reward data (for reward nodes)
                var getRewardMethod = nodeType.GetMethod("GetRewardData", Type.EmptyTypes) ??
                                     nodeType.GetMethod("GetReward", Type.EmptyTypes);
                if (getRewardMethod != null)
                {
                    var reward = getRewardMethod.Invoke(nodeData, null);
                    if (reward != null)
                    {
                        string rewardInfo = ExtractRewardInfo(reward);
                        if (!string.IsNullOrEmpty(rewardInfo))
                        {
                            if (string.IsNullOrEmpty(name))
                                name = rewardInfo;
                            else
                                description = rewardInfo;
                        }
                    }
                }

                // Build result
                List<string> parts = new List<string>();

                if (!string.IsNullOrEmpty(nodeTypeStr) && nodeTypeStr != name)
                {
                    // Convert enum-style type to readable name
                    string readableType = FormatNodeType(nodeTypeStr);
                    if (!string.IsNullOrEmpty(readableType))
                        parts.Add(readableType);
                }

                if (!string.IsNullOrEmpty(name))
                {
                    parts.Add(TextUtilities.StripRichTextTags(name));
                }

                if (!string.IsNullOrEmpty(description) && description != name)
                {
                    parts.Add(TextUtilities.StripRichTextTags(description));
                }

                if (parts.Count > 0)
                {
                    return string.Join(". ", parts);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting map node info: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Extract info from a reward data object
        /// </summary>
        internal static string ExtractRewardInfo(object reward)
        {
            if (reward == null) return null;

            try
            {
                var rewardType = reward.GetType();

                // Try to get name
                var getNameMethod = rewardType.GetMethod("GetName", Type.EmptyTypes) ??
                                   rewardType.GetMethod("GetDisplayName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(reward, null) as string;
                    if (!string.IsNullOrEmpty(name))
                        return LocalizationHelper.Localize(name);
                }

                // Try to get description
                var getDescMethod = rewardType.GetMethod("GetDescription", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    var desc = getDescMethod.Invoke(reward, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                        return LocalizationHelper.Localize(desc);
                }
            }
            catch { }
            return null;
        }


        /// <summary>
        /// Format a node type enum value to readable text
        /// </summary>
        internal static string FormatNodeType(string nodeType)
        {
            if (string.IsNullOrEmpty(nodeType))
                return null;

            // Common node type mappings
            switch (nodeType.ToLower())
            {
                case "battle":
                case "combat":
                    return "Battle";
                case "event":
                case "randomchoice":
                    return "Event";
                case "shop":
                case "merchant":
                    return "Shop";
                case "upgrade":
                case "forge":
                    return "Upgrade";
                case "artifact":
                case "relic":
                    return "Artifact";
                case "healing":
                case "rest":
                    return "Healing";
                case "boss":
                case "bossbattle":
                    return "Boss";
                case "unitreward":
                case "unit":
                    return "Unit Reward";
                case "cardreward":
                case "card":
                    return "Card Reward";
                default:
                    // Add spaces before capitals and clean up
                    return System.Text.RegularExpressions.Regex.Replace(nodeType, "([a-z])([A-Z])", "$1 $2");
            }
        }


        /// <summary>
        /// Determine the type of node in a branch (Battle, Event, Shop, etc.)
        /// </summary>
        internal static string GetBranchNodeType(GameObject go)
        {
            try
            {
                // Look for components or child objects that indicate node type
                var allChildren = go.GetComponentsInChildren<Transform>(true);
                foreach (var child in allChildren)
                {
                    string name = child.name.ToLower();

                    // Check for type indicators in object names
                    if (name.Contains("battle") || name.Contains("combat") || name.Contains("fight"))
                        return "Battle";
                    if (name.Contains("event") || name.Contains("cavern"))
                        return "Event";
                    if (name.Contains("shop") || name.Contains("merchant") || name.Contains("store"))
                        return "Shop";
                    if (name.Contains("upgrade") || name.Contains("forge"))
                        return "Upgrade";
                    if (name.Contains("heal") || name.Contains("restore") || name.Contains("rest"))
                        return "Heal";
                    if (name.Contains("artifact") || name.Contains("relic"))
                        return "Artifact";
                    if (name.Contains("boss"))
                        return "Boss Battle";
                }

                // Check components for type info
                foreach (var child in allChildren)
                {
                    var components = child.GetComponents<Component>();
                    foreach (var comp in components)
                    {
                        if (comp == null) continue;
                        string typeName = comp.GetType().Name.ToLower();

                        if (typeName.Contains("battle"))
                            return "Battle";
                        if (typeName.Contains("event"))
                            return "Event";
                        if (typeName.Contains("shop"))
                            return "Shop";
                    }
                }
            }
            catch { }
            return null;
        }


        /// <summary>
        /// Get enemy info from a branch node (for battle nodes)
        /// </summary>
        internal static string GetBranchEnemyInfo(GameObject go)
        {
            try
            {
                // Look for tooltip data that might contain enemy names
                string tooltip = TooltipTextReader.GetTooltipTextWithBody(go);
                if (!string.IsNullOrEmpty(tooltip) && !tooltip.Contains("Enemy_Tooltip"))
                {
                    return TextUtilities.StripRichTextTags(tooltip);
                }

                // Look for TMP text in children that might be enemy names
                var allChildren = go.GetComponentsInChildren<Transform>(true);
                foreach (var child in allChildren)
                {
                    string text = UITextHelper.GetTMPTextDirect(child.gameObject);
                    if (!string.IsNullOrEmpty(text) && text.Length > 1)
                    {
                        // Filter out single letters and generic labels
                        text = text.Trim();
                        if (text.Length > 2 && !text.Equals("A", StringComparison.OrdinalIgnoreCase) &&
                            !text.Equals("B", StringComparison.OrdinalIgnoreCase))
                        {
                            return TextUtilities.StripRichTextTags(text);
                        }
                    }
                }
            }
            catch { }
            return null;
        }


        /// <summary>
        /// Get first meaningful text from child elements
        /// </summary>
        internal static string GetFirstMeaningfulChildText(GameObject go)
        {
            try
            {
                var allChildren = go.GetComponentsInChildren<Transform>(true);
                foreach (var child in allChildren)
                {
                    if (child == go.transform) continue;

                    string text = UITextHelper.GetTMPTextDirect(child.gameObject);
                    if (!string.IsNullOrEmpty(text))
                    {
                        text = text.Trim();
                        // Skip single letters and very short text
                        if (text.Length > 2)
                        {
                            return TextUtilities.StripRichTextTags(text);
                        }
                    }
                }
            }
            catch { }
            return null;
        }


        /// <summary>
        /// Get comprehensive info from Minimap nodes (MinimapNodeMarker, MinimapBattleNode)
        /// </summary>
        internal static string GetMinimapNodeInfo(GameObject go)
        {
            try
            {
                Transform current = go.transform;
                Component minimapComponent = null;
                string componentType = null;
                string pathPosition = null; // left, right, center

                // Find minimap component
                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;

                        if (typeName == "MinimapNodeMarker" || typeName == "MinimapBattleNode")
                        {
                            minimapComponent = component;
                            componentType = typeName;
                            break;
                        }
                    }
                    if (minimapComponent != null) break;
                    current = current.parent;
                }

                if (minimapComponent == null)
                    return null;

                var sb = new StringBuilder();

                // Check if this is the current player position
                bool isCurrentPosition = IsCurrentMapPosition(minimapComponent);

                // Determine path position from parent hierarchy
                pathPosition = DeterminePathPosition(minimapComponent.transform);

                // Get ring/section info and section index for coordinates
                string ringInfo = GetRingInfo(minimapComponent.transform, out int ringIndex);

                // Get tooltip info (title and body)
                string title = null;
                string body = null;
                TooltipTextReader.GetTooltipTitleAndBody(go, out title, out body);

                // Build the announcement with coordinate first
                string coordinate = BuildCoordinate(ringIndex, pathPosition);
                if (!string.IsNullOrEmpty(coordinate))
                {
                    sb.Append(coordinate);
                    sb.Append(": ");
                }

                // Mark current position
                if (isCurrentPosition)
                {
                    sb.Append("Current position. ");
                }

                // Try to get MapNodeUI data for visited status and more details
                string visitedStatus = TooltipTextReader.GetMapNodeVisitedStatus(go);
                string rewardDetails = TooltipTextReader.GetMapNodeRewardDetails(go);

                if (componentType == "MinimapBattleNode")
                {
                    sb.Append("Battle");
                    if (!string.IsNullOrEmpty(title) && title != "Battle")
                    {
                        sb.Append($" - {title}");
                    }
                }
                else if (!string.IsNullOrEmpty(title))
                {
                    sb.Append(title);
                }
                else
                {
                    sb.Append("Unknown node");
                }

                // Add visited status
                if (!string.IsNullOrEmpty(visitedStatus))
                {
                    sb.Append($" ({visitedStatus})");
                }

                // Add body/description if available
                if (!string.IsNullOrEmpty(body))
                {
                    sb.Append($". {body}");
                }

                // Add reward details if available (more specific than tooltip body)
                if (!string.IsNullOrEmpty(rewardDetails))
                {
                    sb.Append($". {rewardDetails}");
                }

                // Find available directions from sibling nodes
                string availableDirections = GetAvailableDirections(minimapComponent.transform, pathPosition);
                if (!string.IsNullOrEmpty(availableDirections))
                {
                    sb.Append($". {availableDirections}");
                }

                string result = sb.ToString();
                MonsterTrainAccessibility.LogInfo($"Map node info: {result}");
                return result;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting minimap node info: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Check if a map node represents the player's current position
        /// </summary>
        internal static bool IsCurrentMapPosition(Component minimapComponent)
        {
            try
            {
                var type = minimapComponent.GetType();

                // Check for "isCurrent", "isCurrentNode", "current", "isActive" type fields
                string[] currentFieldNames = { "isCurrent", "_isCurrent", "isCurrentNode", "_isCurrentNode",
                                                 "current", "_current", "isActive", "_isActive",
                                                 "isCompleted", "_isCompleted", "completed", "_completed" };

                foreach (var fieldName in currentFieldNames)
                {
                    var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null && field.FieldType == typeof(bool))
                    {
                        bool value = (bool)field.GetValue(minimapComponent);
                        // "isCurrent" means current position, "isCompleted" means past position
                        if (fieldName.ToLower().Contains("current") && value)
                            return true;
                    }

                    var prop = type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null && prop.PropertyType == typeof(bool) && prop.CanRead)
                    {
                        bool value = (bool)prop.GetValue(minimapComponent);
                        if (fieldName.ToLower().Contains("current") && value)
                            return true;
                    }
                }

                // Check the GameObject name or children for "current" indicator
                string goName = minimapComponent.gameObject.name.ToLower();
                if (goName.Contains("current") || goName.Contains("active") || goName.Contains("player"))
                    return true;

                // Check for a child object that might indicate current position
                var transform = minimapComponent.transform;
                foreach (Transform child in transform)
                {
                    if (child == null || !child.gameObject.activeInHierarchy)
                        continue;

                    string childName = child.name.ToLower();
                    if (childName.Contains("current") || childName.Contains("indicator") ||
                        childName.Contains("player") || childName.Contains("marker"))
                    {
                        // Check if this indicator is actually visible/active
                        if (child.gameObject.activeInHierarchy)
                            return true;
                    }
                }

            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error checking current map position: {ex.Message}");
            }

            return false;
        }


        /// <summary>
        /// Build a coordinate string like "Ring 3, Left" for position identification
        /// </summary>
        internal static string BuildCoordinate(int ringIndex, string pathPosition)
        {
            var parts = new List<string>();

            if (ringIndex >= 0)
            {
                parts.Add($"Ring {ringIndex + 1}");
            }

            if (!string.IsNullOrEmpty(pathPosition))
            {
                // Simplify "Left path" to "Left" for coordinate
                string pos = pathPosition.Replace(" path", "");
                parts.Add(pos);
            }

            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }


        /// <summary>
        /// Find available directions by scanning sibling nodes in the same ring/section
        /// </summary>
        internal static string GetAvailableDirections(Transform currentNodeTransform, string currentPosition)
        {
            try
            {
                // Find the parent section that contains map nodes
                Transform sectionParent = FindMapSection(currentNodeTransform);
                if (sectionParent == null)
                    return null;

                var availablePositions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Scan all descendants for other map nodes
                ScanForMapNodes(sectionParent, currentNodeTransform, availablePositions);

                // Remove current position
                if (!string.IsNullOrEmpty(currentPosition))
                {
                    availablePositions.Remove(currentPosition);
                    availablePositions.Remove(currentPosition.Replace(" path", ""));
                }

                if (availablePositions.Count == 0)
                    return null;

                // Build direction string
                var directions = new List<string>();
                if (availablePositions.Contains("Left") || availablePositions.Contains("Left path"))
                    directions.Add("left");
                if (availablePositions.Contains("Center"))
                    directions.Add("center");
                if (availablePositions.Contains("Right") || availablePositions.Contains("Right path"))
                    directions.Add("right");

                if (directions.Count == 0)
                    return null;

                return $"Can go {string.Join(", ", directions)}";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting available directions: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Find the parent section/container that holds map nodes for this ring
        /// </summary>
        internal static Transform FindMapSection(Transform nodeTransform)
        {
            Transform current = nodeTransform.parent;
            while (current != null)
            {
                // Check if this is a section container
                string name = current.name.ToLower();
                if (name.Contains("section") || name.Contains("ring") || name.Contains("row"))
                {
                    return current;
                }

                // Check for MinimapSection component
                foreach (var component in current.GetComponents<Component>())
                {
                    if (component != null && component.GetType().Name == "MinimapSection")
                    {
                        return current;
                    }
                }

                current = current.parent;
            }

            // Fallback: use grandparent or parent
            if (nodeTransform.parent != null)
                return nodeTransform.parent.parent ?? nodeTransform.parent;

            return null;
        }


        /// <summary>
        /// Recursively scan for map nodes and collect their positions
        /// </summary>
        internal static void ScanForMapNodes(Transform parent, Transform excludeNode, HashSet<string> positions)
        {
            if (parent == null)
                return;

            foreach (Transform child in parent)
            {
                if (child == null || !child.gameObject.activeInHierarchy)
                    continue;

                // Skip the node we're currently on
                if (child == excludeNode || IsDescendantOf(excludeNode, child))
                    continue;

                // Check if this is a map node
                bool isMapNode = false;
                foreach (var component in child.GetComponents<Component>())
                {
                    if (component == null) continue;
                    string typeName = component.GetType().Name;
                    if (typeName == "MinimapNodeMarker" || typeName == "MinimapBattleNode")
                    {
                        isMapNode = true;
                        break;
                    }
                }

                if (isMapNode)
                {
                    string pos = DeterminePathPosition(child);
                    if (!string.IsNullOrEmpty(pos))
                    {
                        positions.Add(pos.Replace(" path", ""));
                    }
                }

                // Recurse into children
                ScanForMapNodes(child, excludeNode, positions);
            }
        }


        /// <summary>
        /// Check if excludeNode is a descendant of potentialAncestor
        /// </summary>
        internal static bool IsDescendantOf(Transform node, Transform potentialAncestor)
        {
            if (node == null || potentialAncestor == null)
                return false;

            Transform current = node;
            while (current != null)
            {
                if (current == potentialAncestor)
                    return true;
                current = current.parent;
            }
            return false;
        }


        /// <summary>
        /// Determine if this node is on the left path, right path, or center
        /// </summary>
        internal static string DeterminePathPosition(Transform nodeTransform)
        {
            try
            {
                // Walk up the hierarchy looking for position indicators
                Transform current = nodeTransform;
                while (current != null)
                {
                    string name = current.name.ToLower();

                    if (name.Contains("left"))
                        return "Left path";
                    if (name.Contains("right"))
                        return "Right path";
                    if (name.Contains("center") || name.Contains("shared"))
                        return "Center";

                    // Check if parent is a node layout
                    if (current.parent != null)
                    {
                        string parentName = current.parent.name.ToLower();
                        if (parentName.Contains("left"))
                            return "Left path";
                        if (parentName.Contains("right"))
                            return "Right path";
                        if (parentName.Contains("center"))
                            return "Center";
                    }

                    current = current.parent;
                }

                // Check position relative to screen center as fallback
                var rectTransform = nodeTransform.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    float xPos = rectTransform.position.x;
                    float screenCenter = Screen.width / 2f;
                    float threshold = Screen.width * 0.1f;

                    if (xPos < screenCenter - threshold)
                        return "Left path";
                    if (xPos > screenCenter + threshold)
                        return "Right path";
                    return "Center";
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error determining path position: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Get the ring/section info for a map node
        /// </summary>
        internal static string GetRingInfo(Transform nodeTransform, out int ringIndex)
        {
            ringIndex = -1;

            try
            {
                // Look for MinimapSection in parents
                Transform current = nodeTransform;
                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "MinimapSection")
                        {
                            // Try to get ring number from the section
                            var sectionType = component.GetType();

                            // Try various field names
                            string[] ringFieldNames = { "ringIndex", "_ringIndex", "sectionIndex", "_sectionIndex", "index", "_index" };
                            foreach (var fieldName in ringFieldNames)
                            {
                                var field = sectionType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (field != null)
                                {
                                    var value = field.GetValue(component);
                                    if (value != null)
                                    {
                                        ringIndex = Convert.ToInt32(value);
                                        return $"Ring {ringIndex + 1}";
                                    }
                                }
                            }

                            // Try to extract from the section's name or label
                            var labelField = sectionType.GetField("ringLabel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (labelField == null)
                                labelField = sectionType.GetField("_ringLabel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                            if (labelField != null)
                            {
                                var labelObj = labelField.GetValue(component);
                                if (labelObj != null)
                                {
                                    string labelText = BattleIntroTextReader.GetTextFromComponent(labelObj);
                                    if (!string.IsNullOrEmpty(labelText))
                                    {
                                        return labelText;
                                    }
                                }
                            }
                        }
                    }

                    // Also check the object's name for ring number
                    if (current.name.Contains("section") || current.name.Contains("Section"))
                    {
                        // Try to extract number from name like "Minimap section(Clone)"
                        var match = System.Text.RegularExpressions.Regex.Match(current.name, @"(\d+)");
                        if (match.Success)
                        {
                            if (int.TryParse(match.Groups[1].Value, out int parsed))
                            {
                                ringIndex = parsed - 1; // Adjust to 0-based
                            }
                            return $"Ring {match.Groups[1].Value}";
                        }
                    }

                    current = current.parent;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting ring info: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Find and read the currently visible tooltip on the map screen
        /// </summary>
        internal static string GetVisibleMapTooltip()
        {
            try
            {
                // Find all active tooltip displays in the scene
                var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                foreach (var obj in allObjects)
                {
                    if (!obj.activeInHierarchy) continue;

                    string name = obj.name.ToLower();
                    // Look for tooltip display objects
                    if (name.Contains("tooltip") && (name.Contains("display") || name.Contains("panel") || name.Contains("popup")))
                    {
                        // Check if it has visible text
                        string title = null;
                        string body = null;

                        // Try to get TMP text from children
                        var children = obj.GetComponentsInChildren<Transform>(true);
                        foreach (var child in children)
                        {
                            if (!child.gameObject.activeInHierarchy) continue;

                            string childName = child.name.ToLower();
                            string text = UITextHelper.GetTMPTextDirect(child.gameObject);

                            if (!string.IsNullOrEmpty(text))
                            {
                                text = TextUtilities.StripRichTextTags(text.Trim());
                                if (string.IsNullOrEmpty(text)) continue;

                                // Title is usually shorter and comes first
                                if (childName.Contains("title") || childName.Contains("header") || childName.Contains("name"))
                                {
                                    title = text;
                                }
                                else if (childName.Contains("body") || childName.Contains("description") || childName.Contains("desc"))
                                {
                                    body = text;
                                }
                                else if (title == null && text.Length < 50)
                                {
                                    title = text;
                                }
                                else if (body == null)
                                {
                                    body = text;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(title))
                        {
                            MonsterTrainAccessibility.LogInfo($"Found visible tooltip: {title} - {body}");
                            if (!string.IsNullOrEmpty(body))
                                return $"{title}. {body}";
                            return title;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error finding visible tooltip: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Debug: Log all components on a GameObject and its parents
        /// </summary>
        internal static void LogMapNodeComponents(GameObject go)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Components on '{go.name}':");

                Transform current = go.transform;
                int depth = 0;
                while (current != null && depth < 5)
                {
                    sb.Append($"  [{depth}] {current.name}: ");
                    var components = current.GetComponents<Component>();
                    foreach (var comp in components)
                    {
                        if (comp != null)
                        {
                            sb.Append(comp.GetType().Name + ", ");
                        }
                    }
                    sb.AppendLine();
                    current = current.parent;
                    depth++;
                }

                MonsterTrainAccessibility.LogInfo(sb.ToString());
            }
            catch { }
        }


        /// <summary>
        /// Log all fields on a MapNodeUI component for debugging
        /// </summary>
        internal static void LogMapNodeUIFields(Component mapNodeComponent)
        {
            try
            {
                var type = mapNodeComponent.GetType();
                var sb = new StringBuilder();
                sb.AppendLine($"=== Fields on {type.Name} ===");

                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    try
                    {
                        var value = field.GetValue(mapNodeComponent);
                        string valueStr = value?.ToString() ?? "null";
                        if (valueStr.Length > 80) valueStr = valueStr.Substring(0, 80) + "...";
                        sb.AppendLine($"  {field.FieldType.Name} {field.Name} = {valueStr}");
                    }
                    catch { }
                }

                MonsterTrainAccessibility.LogInfo(sb.ToString());
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error logging MapNodeUI fields: {ex.Message}");
            }
        }

    }
}
