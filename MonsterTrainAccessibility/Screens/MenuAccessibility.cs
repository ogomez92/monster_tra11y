using MonsterTrainAccessibility.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Handles accessibility for menus by reading Unity's EventSystem selection.
    /// Instead of maintaining a fake menu, we track what the game has selected
    /// and read the text from the actual UI elements.
    /// </summary>
    public class MenuAccessibility : MonoBehaviour
    {
        private GameObject _lastSelectedObject;
        private float _pollInterval = 0.1f;
        private float _pollTimer = 0f;
        private bool _isActive = true;

        // Text content monitoring
        private string _lastScrollContent = null;
        private string _lastScreenTextHash = null;
        private float _textCheckInterval = 0.5f;
        private float _textCheckTimer = 0f;

        // Blacklist of panel names that should be ignored when scanning for text
        private static readonly HashSet<string> _panelBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "quitconfirmation", "exitdialog", "confirmdialog", "confirmationpopup",
            "quitpanel", "exitpanel", "confirmquit", "quitgame", "exitgame"
        };

        private void Update()
        {
            if (!_isActive)
                return;

            _pollTimer += Time.unscaledDeltaTime;
            _textCheckTimer += Time.unscaledDeltaTime;

            // Check selection changes frequently
            if (_pollTimer >= _pollInterval)
            {
                _pollTimer = 0f;
                CheckForSelectionChange();
            }

            // Check for text content changes less frequently
            if (_textCheckTimer >= _textCheckInterval)
            {
                _textCheckTimer = 0f;
                CheckForTextChanges();
            }
        }

        /// <summary>
        /// Check if any monitored text content has changed
        /// </summary>
        private void CheckForTextChanges()
        {
            try
            {
                // If we're focused on a scrollbar or scroll area, monitor its content
                if (_lastSelectedObject != null && IsScrollbar(_lastSelectedObject))
                {
                    string currentContent = GetScrollViewContentText(_lastSelectedObject);
                    if (!string.IsNullOrEmpty(currentContent) && currentContent != _lastScrollContent)
                    {
                        _lastScrollContent = currentContent;

                        // Announce the new content (truncated for initial announcement)
                        string announcement = currentContent;
                        if (announcement.Length > 500)
                        {
                            announcement = announcement.Substring(0, 500) + "...";
                        }
                        MonsterTrainAccessibility.ScreenReader?.Speak(announcement, true);
                    }
                }

                // Also check for any new large text panels appearing
                CheckForNewTextPanels();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error checking text changes: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if new text panels have appeared (dialogs, popups, etc.)
        /// </summary>
        private void CheckForNewTextPanels()
        {
            try
            {
                // First, check for tutorial panels specifically (highest priority)
                CheckForTutorialText();

                // Get a hash of current visible text to detect changes
                var sb = new StringBuilder();
                var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

                foreach (var root in rootObjects)
                {
                    if (root.activeInHierarchy)
                    {
                        // Look for popup/dialog/panel objects that might contain text
                        FindLargeTextContent(root.transform, sb);
                    }
                }

                string currentHash = sb.ToString();

                // If content changed significantly, announce it
                if (!string.IsNullOrEmpty(currentHash) && currentHash != _lastScreenTextHash)
                {
                    // Only announce if there's substantial new content
                    if (_lastScreenTextHash == null ||
                        currentHash.Length > _lastScreenTextHash.Length + 50)
                    {
                        _lastScreenTextHash = currentHash;

                        // Find what's new and announce it
                        string newContent = currentHash;
                        if (_lastScreenTextHash != null && currentHash.StartsWith(_lastScreenTextHash))
                        {
                            newContent = currentHash.Substring(_lastScreenTextHash.Length);
                        }

                        if (newContent.Length > 50) // Only announce substantial text
                        {
                            if (newContent.Length > 500)
                            {
                                newContent = newContent.Substring(0, 500) + "...";
                            }
                            MonsterTrainAccessibility.ScreenReader?.Queue(newContent.Trim());
                        }
                    }
                    _lastScreenTextHash = currentHash;
                }
            }
            catch { }
        }

        /// <summary>
        /// Check for and announce tutorial text when it appears
        /// </summary>
        private void CheckForTutorialText()
        {
            try
            {
                string newTutorialText = Help.Contexts.TutorialHelp.CheckForNewTutorialText();
                if (!string.IsNullOrEmpty(newTutorialText))
                {
                    // Announce tutorial text with "Tutorial:" prefix
                    string announcement = "Tutorial: " + newTutorialText;
                    if (announcement.Length > 600)
                    {
                        announcement = announcement.Substring(0, 600) + "...";
                    }
                    MonsterTrainAccessibility.ScreenReader?.Queue(announcement);
                    MonsterTrainAccessibility.LogInfo("Tutorial text announced");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error checking tutorial text: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a GameObject is actually visible (not hidden by CanvasGroup or blacklisted)
        /// </summary>
        private bool IsActuallyVisible(GameObject go)
        {
            if (go == null || !go.activeInHierarchy)
                return false;

            // Check if this object or any parent is blacklisted
            Transform current = go.transform;
            while (current != null)
            {
                string objName = current.name.Replace(" ", "").Replace("_", "");
                if (_panelBlacklist.Contains(objName))
                    return false;

                // Check for CanvasGroup with alpha = 0 (hidden) via reflection
                if (IsHiddenByCanvasGroup(current.gameObject))
                    return false;

                // Check for Canvas that's disabled via reflection
                if (IsCanvasDisabled(current.gameObject))
                    return false;

                current = current.parent;
            }

            return true;
        }

        /// <summary>
        /// Check if a GameObject is hidden by CanvasGroup (alpha = 0) via reflection
        /// </summary>
        private bool IsHiddenByCanvasGroup(GameObject go)
        {
            try
            {
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.Name == "CanvasGroup")
                    {
                        var alphaProp = type.GetProperty("alpha");
                        if (alphaProp != null)
                        {
                            float alpha = (float)alphaProp.GetValue(component);
                            if (alpha <= 0.01f)
                                return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if a Canvas component is disabled via reflection
        /// </summary>
        private bool IsCanvasDisabled(GameObject go)
        {
            try
            {
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.Name == "Canvas")
                    {
                        var enabledProp = type.GetProperty("enabled");
                        if (enabledProp != null)
                        {
                            bool enabled = (bool)enabledProp.GetValue(component);
                            if (!enabled)
                                return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Find large text content in dialogs/popups
        /// </summary>
        private void FindLargeTextContent(Transform transform, StringBuilder sb)
        {
            if (transform == null || !IsActuallyVisible(transform.gameObject)) return;

            string name = transform.name.ToLower();

            // Look for common dialog/popup/panel names
            bool isTextPanel = name.Contains("dialog") || name.Contains("popup") ||
                              name.Contains("panel") || name.Contains("modal") ||
                              name.Contains("tooltip") || name.Contains("description") ||
                              name.Contains("content") || name.Contains("text");

            if (isTextPanel)
            {
                // Get all text from this panel
                string text = GetTMPText(transform.gameObject);
                if (!string.IsNullOrEmpty(text) && text.Length > 20)
                {
                    sb.AppendLine(text);
                }

                var uiText = transform.GetComponent<Text>();
                if (uiText != null && !string.IsNullOrEmpty(uiText.text) && uiText.text.Length > 20)
                {
                    sb.AppendLine(uiText.text);
                }
            }

            // Recurse into children
            foreach (Transform child in transform)
            {
                FindLargeTextContent(child, sb);
            }
        }

        /// <summary>
        /// Check if the game's UI selection has changed and announce it
        /// </summary>
        private void CheckForSelectionChange()
        {
            if (EventSystem.current == null)
                return;

            GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

            // Selection changed?
            if (currentSelected != _lastSelectedObject)
            {
                _lastSelectedObject = currentSelected;

                // Reset scroll content tracking when selection changes
                _lastScrollContent = null;

                if (currentSelected != null)
                {
                    string text = GetTextFromGameObject(currentSelected);
                    if (!string.IsNullOrEmpty(text))
                    {
                        MonsterTrainAccessibility.ScreenReader?.AnnounceFocus(text);
                    }

                    // If this is a scroll area, remember the initial content
                    if (IsScrollbar(currentSelected))
                    {
                        _lastScrollContent = GetScrollViewContentText(currentSelected);
                    }
                }
            }
        }

        /// <summary>
        /// Extract readable text from a UI GameObject.
        /// Tries multiple approaches to find text.
        /// </summary>
        private string GetTextFromGameObject(GameObject go)
        {
            if (go == null)
                return null;

            string text = null;

            // Check if this is a scrollbar - if so, try to find the scroll view's content
            if (IsScrollbar(go))
            {
                text = GetScrollViewContentText(go);
                if (!string.IsNullOrEmpty(text))
                {
                    // Truncate very long text for the focus announcement
                    if (text.Length > 300)
                    {
                        text = text.Substring(0, 300) + "... Press T to read full text.";
                    }
                    return text;
                }
            }

            // 1. Check for Fight button on BattleIntro screen - get battle name
            text = GetBattleIntroText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 2. Check for map node (battle/event/shop nodes on the map)
            text = GetMapNodeText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 2. Check for toggle/checkbox components first
            text = GetToggleText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 3. Check for logbook/compendium items
            text = GetLogbookItemText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 4. Try to get text with context (handles short button labels)
            text = GetTextWithContext(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 5. Try the GameObject name as fallback (but make it more readable)
            text = CleanGameObjectName(go.name);

            return text?.Trim();
        }

        /// <summary>
        /// Get text for map nodes (battles, events, shops, etc.)
        /// Extracts proper encounter names instead of just button labels like "Fight!"
        /// </summary>
        private string GetMapNodeText(GameObject go)
        {
            try
            {
                // Debug: Log all components on this object and parents to understand structure
                LogMapNodeComponents(go);

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
                    // Try finding tooltip data directly from the selected object
                    string tooltipText = GetTooltipTextWithBody(go);
                    if (!string.IsNullOrEmpty(tooltipText) && !tooltipText.Contains("Enemy_Tooltip"))
                    {
                        return tooltipText;
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
                    return null;

                var nodeDataType = mapNodeData.GetType();
                string nodeName = nodeDataType.Name;

                // Check if this is a ScenarioData (battle node)
                if (nodeName == "ScenarioData" || nodeDataType.BaseType?.Name == "ScenarioData")
                {
                    return GetBattleNodeName(mapNodeData, nodeDataType);
                }

                // For other node types (rewards, merchants, etc.), try tooltipTitleKey
                return GetGenericNodeName(mapNodeData, nodeDataType);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting map node text: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get comprehensive info from Minimap nodes (MinimapNodeMarker, MinimapBattleNode)
        /// </summary>
        private string GetMinimapNodeInfo(GameObject go)
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

                // Determine path position from parent hierarchy
                pathPosition = DeterminePathPosition(minimapComponent.transform);

                // Get ring/section info
                string ringInfo = GetRingInfo(minimapComponent.transform);

                // Get tooltip info (title and body)
                string title = null;
                string body = null;
                GetTooltipTitleAndBody(go, out title, out body);

                // Build the announcement
                if (!string.IsNullOrEmpty(ringInfo))
                {
                    sb.Append(ringInfo);
                    sb.Append(". ");
                }

                if (!string.IsNullOrEmpty(pathPosition))
                {
                    sb.Append(pathPosition);
                    sb.Append(": ");
                }

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

                // Add body/description if available
                if (!string.IsNullOrEmpty(body))
                {
                    sb.Append($". {body}");
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
        /// Determine if this node is on the left path, right path, or center
        /// </summary>
        private string DeterminePathPosition(Transform nodeTransform)
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
        private string GetRingInfo(Transform nodeTransform)
        {
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
                                        int ringNum = Convert.ToInt32(value);
                                        return $"Ring {ringNum + 1}";
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
                                    string labelText = GetTextFromComponent(labelObj);
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
        /// Get both title and body from tooltip
        /// </summary>
        private void GetTooltipTitleAndBody(GameObject go, out string title, out string body)
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

                                    // Get title
                                    var titleField = tooltipType.GetField("title", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (titleField != null)
                                    {
                                        title = titleField.GetValue(tooltip) as string;
                                    }

                                    // Get body
                                    var bodyField = tooltipType.GetField("body", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (bodyField != null)
                                    {
                                        body = bodyField.GetValue(tooltip) as string;
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
        private string GetTooltipTextWithBody(GameObject go)
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
        /// Get battle info when on the Fight button of BattleIntro screen
        /// </summary>
        private string GetBattleIntroText(GameObject go)
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
        /// Get text from a UI text component (TMP_Text, Text, etc.)
        /// </summary>
        private string GetTextFromComponent(object textComponent)
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
        /// Debug: Log all text content in a UI hierarchy
        /// </summary>
        private void LogAllTextInHierarchy(Transform root, string prefix)
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

        private void LogTextRecursive(Transform node, StringBuilder sb, int depth)
        {
            if (node == null || depth > 10) return;

            string indent = new string(' ', depth * 2);

            // Get text from this node
            string text = GetTMPTextDirect(node.gameObject);
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

        /// <summary>
        /// Find the scenario/wave name in children of BattleIntroScreen
        /// The battleNameLabel shows the boss name, but we want the wave/scenario name
        /// </summary>
        private string FindScenarioTextInChildren(Transform root)
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

        private void CollectTextLabels(Transform node, Dictionary<string, string> labels)
        {
            if (node == null) return;

            // Get text from this node
            string text = GetTMPTextDirect(node.gameObject);
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
        private void LogScreenFields(Type screenType, object screen)
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
        private string GetScenarioNameFromScreen(object screen, Type screenType)
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
        private string GetScenarioNameFromSaveManager(object saveManager)
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
        private string GetScenarioNameFromRunState(object runState)
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
        private string GetBattleNameFromScenario(object scenarioData)
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
                            string localized = LocalizeKey(key);
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
        private string GetScenarioDescriptionFromScreen(object screen, Type screenType)
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
        private string GetBattleMetadataFromScreen(object screen, Type screenType)
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
        private string GetCurrentRingInfo(object saveManager)
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
        private string GetBattleType(object saveManager, object screen, Type screenType)
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
        private string GetScenarioDifficulty(object saveManager)
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
        private string GetScenarioDescriptionFromSaveManager(object saveManager)
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
        private string GetBattleDescriptionFromScenario(object scenarioData)
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
                            string localized = LocalizeKey(key);
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
        private string GetBattleNameAndDescription(object scenarioData)
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
                                string localized = LocalizeKey(key);
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
                                string localized = LocalizeKey(key);
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
        private string GetEnemyNamesFromSiblings(GameObject go)
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
                                    string name = GetTooltipProviderTitle(component, component.GetType());
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
        private Transform FindChildByNameContains(Transform parent, string partialName)
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
        private List<string> GetTooltipNamesFromContainer(Transform container)
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
                        string name = GetTooltipProviderTitle(component, component.GetType());
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
        /// Debug: Log all components on a GameObject and its parents
        /// </summary>
        private void LogMapNodeComponents(GameObject go)
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
        /// Try to get tooltip text from a GameObject's tooltip components
        /// </summary>
        private string GetTooltipText(GameObject go)
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
                                    string localized = LocalizeKey(titleKey);
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
        /// Extract title from TooltipProviderComponent
        /// </summary>
        private string GetTooltipProviderTitle(Component tooltipProvider, Type type)
        {
            try
            {
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
        private string ExtractTitleFromTooltip(object tooltip)
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
                            string localized = LocalizeKey(str);
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
                            string localized = LocalizeKey(str);
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
        private string GetNameFromDataObject(object dataObj)
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
                        string localized = LocalizeKey(key);
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
                            string localized = LocalizeKey(str);
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
        private string GetBattleNodeName(object scenarioData, Type dataType)
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
                    string localized = LocalizeKey(battleNameKey);
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
        private string GetGenericNodeName(object nodeData, Type dataType)
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
                    string localized = LocalizeKey(titleKey);
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
        private string GetFallbackNodeName(object nodeData, Type dataType, string prefix)
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
        /// Localize a string key using the game's localization system
        /// </summary>
        private string LocalizeKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            try
            {
                // Try to call the Localize extension method
                var stringType = typeof(string);

                // Look for the Localize extension method in all assemblies
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!type.IsClass || !type.IsSealed || !type.IsAbstract)
                            continue;

                        var method = type.GetMethod("Localize",
                            BindingFlags.Public | BindingFlags.Static,
                            null,
                            new Type[] { typeof(string) },
                            null);

                        if (method != null && method.ReturnType == typeof(string))
                        {
                            var result = method.Invoke(null, new object[] { key });
                            if (result is string localized && !string.IsNullOrEmpty(localized) && localized != key)
                            {
                                return localized;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error localizing key '{key}': {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Clean up asset names to be more readable
        /// </summary>
        private string CleanAssetName(string name)
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
        /// Get text for toggle/checkbox controls with their label
        /// </summary>
        private string GetToggleText(GameObject go)
        {
            try
            {
                // Check for Unity UI Toggle component first
                var unityToggle = go.GetComponent<Toggle>();
                if (unityToggle != null)
                {
                    string label = GetToggleLabelFromHierarchy(go);
                    string state = unityToggle.isOn ? "on" : "off";
                    return string.IsNullOrEmpty(label) ? state : $"{label}: {state}";
                }

                // Check for game-specific toggle types via reflection
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    string typeName = type.Name;

                    // Look for GameUISelectableToggle, GameUISelectableCheckbox, etc.
                    if (typeName.Contains("Toggle") || typeName.Contains("Checkbox"))
                    {
                        // Try to get isOn or isChecked property
                        bool? isOn = null;
                        var isOnProp = type.GetProperty("isOn");
                        if (isOnProp != null)
                        {
                            isOn = isOnProp.GetValue(component) as bool?;
                        }
                        if (isOn == null)
                        {
                            var isCheckedProp = type.GetProperty("isChecked");
                            if (isCheckedProp != null)
                            {
                                isOn = isCheckedProp.GetValue(component) as bool?;
                            }
                        }
                        // Also try m_IsOn field
                        if (isOn == null)
                        {
                            var isOnField = type.GetField("m_IsOn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (isOnField != null)
                            {
                                isOn = isOnField.GetValue(component) as bool?;
                            }
                        }

                        if (isOn.HasValue)
                        {
                            string label = GetToggleLabelFromHierarchy(go);
                            string state = isOn.Value ? "on" : "off";
                            return string.IsNullOrEmpty(label) ? state : $"{label}: {state}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting toggle text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Find the label for a toggle by looking at hierarchy
        /// </summary>
        private string GetToggleLabelFromHierarchy(GameObject go)
        {
            try
            {
                // Check siblings for label text
                Transform parent = go.transform.parent;
                if (parent != null)
                {
                    foreach (Transform sibling in parent)
                    {
                        if (sibling == go.transform) continue;

                        // Skip on/off labels
                        string sibName = sibling.name.ToLower();
                        if (sibName.Contains("onlabel") || sibName.Contains("offlabel") ||
                            sibName == "on" || sibName == "off")
                            continue;

                        string sibText = GetTMPTextDirect(sibling.gameObject);
                        if (string.IsNullOrEmpty(sibText))
                        {
                            var uiText = sibling.GetComponent<Text>();
                            sibText = uiText?.text;
                        }

                        // Skip very short or on/off text
                        if (!string.IsNullOrEmpty(sibText) && sibText.Length > 2)
                        {
                            string lower = sibText.ToLower().Trim();
                            if (lower != "on" && lower != "off")
                            {
                                return sibText.Trim();
                            }
                        }
                    }

                    // Check parent's name for context
                    string parentName = CleanGameObjectName(parent.name);
                    if (!string.IsNullOrEmpty(parentName) && parentName.Length > 2)
                    {
                        return parentName;
                    }

                    // Check grandparent siblings
                    if (parent.parent != null)
                    {
                        foreach (Transform uncle in parent.parent)
                        {
                            if (uncle == parent) continue;

                            string uncleText = GetTMPTextDirect(uncle.gameObject);
                            if (string.IsNullOrEmpty(uncleText))
                            {
                                var uiText = uncle.GetComponent<Text>();
                                uncleText = uiText?.text;
                            }

                            if (!string.IsNullOrEmpty(uncleText) && uncleText.Length > 2)
                            {
                                string lower = uncleText.ToLower().Trim();
                                if (lower != "on" && lower != "off")
                                {
                                    return uncleText.Trim();
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
        /// Get text with additional context for buttons with short labels
        /// </summary>
        private string GetTextWithContext(GameObject go)
        {
            string directText = GetDirectText(go);

            // If text is very short (1-4 chars) or empty, look for context
            if (string.IsNullOrEmpty(directText) || directText.Length <= 4)
            {
                // Check parent for label
                string contextLabel = GetContextLabelFromHierarchy(go);
                if (!string.IsNullOrEmpty(contextLabel))
                {
                    if (string.IsNullOrEmpty(directText))
                    {
                        return contextLabel;
                    }
                    return $"{contextLabel}: {directText}";
                }
            }

            return directText;
        }

        /// <summary>
        /// Get direct text from a GameObject (immediate text components)
        /// </summary>
        private string GetDirectText(GameObject go)
        {
            // Try TMP text first
            string text = GetTMPText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text.Trim();
            }

            // Try Unity UI Text
            var uiText = go.GetComponentInChildren<Text>();
            if (uiText != null && !string.IsNullOrEmpty(uiText.text))
            {
                return uiText.text.Trim();
            }

            return null;
        }

        /// <summary>
        /// Get context label from hierarchy (parent/sibling/grandparent)
        /// </summary>
        private string GetContextLabelFromHierarchy(GameObject go)
        {
            try
            {
                Transform current = go.transform;

                // Walk up the hierarchy looking for meaningful labels
                for (int depth = 0; depth < 4 && current.parent != null; depth++)
                {
                    Transform parent = current.parent;

                    // Check siblings of current
                    foreach (Transform sibling in parent)
                    {
                        if (sibling == current) continue;

                        string sibText = GetTMPTextDirect(sibling.gameObject);
                        if (string.IsNullOrEmpty(sibText))
                        {
                            var uiText = sibling.GetComponent<Text>();
                            sibText = uiText?.text;
                        }

                        // Accept text that's longer than what we already have
                        if (!string.IsNullOrEmpty(sibText) && sibText.Trim().Length > 4)
                        {
                            return sibText.Trim();
                        }
                    }

                    // Check parent's name
                    string parentName = parent.name;
                    if (!string.IsNullOrEmpty(parentName))
                    {
                        // Clean up common UI suffixes
                        string cleaned = CleanGameObjectName(parentName);
                        if (!string.IsNullOrEmpty(cleaned) && cleaned.Length > 4)
                        {
                            // Make sure it's not just a generic container name
                            string lower = cleaned.ToLower();
                            if (!lower.Contains("container") && !lower.Contains("panel") &&
                                !lower.Contains("holder") && !lower.Contains("group") &&
                                !lower.Contains("content") && !lower.Contains("root"))
                            {
                                return cleaned;
                            }
                        }
                    }

                    current = parent;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get text for logbook/compendium items
        /// </summary>
        private string GetLogbookItemText(GameObject go)
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
        private bool IsInCompendiumContext(GameObject go)
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
        private string FindCountLabelText(GameObject go)
        {
            try
            {
                Transform parent = go.transform.parent;
                if (parent == null) return null;

                // Collect all text from siblings to find count patterns
                var allTexts = new List<string>();
                foreach (Transform sibling in parent)
                {
                    string text = GetTMPTextDirect(sibling.gameObject);
                    if (!string.IsNullOrEmpty(text))
                        allTexts.Add(text.Trim());

                    var uiText = sibling.GetComponent<Text>();
                    if (uiText != null && !string.IsNullOrEmpty(uiText.text))
                        allTexts.Add(uiText.text.Trim());

                    // Also check children
                    foreach (Transform child in sibling)
                    {
                        string childText = GetTMPTextDirect(child.gameObject);
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
        private string GetItemNameFromHierarchy(GameObject go)
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
                                string text = GetTMPTextDirect(sibling.gameObject);
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
        private string FormatCountText(string countText)
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
        /// Check if a GameObject is a scrollbar
        /// </summary>
        private bool IsScrollbar(GameObject go)
        {
            if (go == null) return false;

            string name = go.name.ToLower();
            if (name.Contains("scrollbar") || name.Contains("scroll bar"))
                return true;

            return go.GetComponent<Scrollbar>() != null;
        }

        /// <summary>
        /// Try to find and read text from a scroll view's content area
        /// </summary>
        private string GetScrollViewContentText(GameObject scrollbarGo)
        {
            try
            {
                // Try to find the ScrollRect in parent or siblings
                Transform parent = scrollbarGo.transform.parent;
                while (parent != null)
                {
                    var scrollRect = parent.GetComponent<ScrollRect>();
                    if (scrollRect != null && scrollRect.content != null)
                    {
                        return GetAllTextFromTransform(scrollRect.content);
                    }

                    // Also check siblings
                    foreach (Transform sibling in parent)
                    {
                        var siblingScrollRect = sibling.GetComponent<ScrollRect>();
                        if (siblingScrollRect != null && siblingScrollRect.content != null)
                        {
                            return GetAllTextFromTransform(siblingScrollRect.content);
                        }
                    }

                    parent = parent.parent;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scroll content: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get all text from a transform and its children
        /// </summary>
        private string GetAllTextFromTransform(Transform root)
        {
            var sb = new StringBuilder();
            CollectAllText(root, sb);
            return sb.ToString().Trim();
        }

        /// <summary>
        /// Recursively collect text from all Text and TMP components
        /// </summary>
        private void CollectAllText(Transform transform, StringBuilder sb)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy) return;

            // Get Text component on this object
            var uiText = transform.GetComponent<Text>();
            if (uiText != null && !string.IsNullOrEmpty(uiText.text))
            {
                string cleaned = uiText.text.Trim();
                if (!string.IsNullOrEmpty(cleaned))
                {
                    sb.AppendLine(cleaned);
                }
            }

            // Get TMP text
            string tmpText = GetTMPTextDirect(transform.gameObject);
            if (!string.IsNullOrEmpty(tmpText))
            {
                string cleaned = tmpText.Trim();
                if (!string.IsNullOrEmpty(cleaned))
                {
                    sb.AppendLine(cleaned);
                }
            }

            // Recurse into children
            foreach (Transform child in transform)
            {
                CollectAllText(child, sb);
            }
        }

        /// <summary>
        /// Get TMP text from a specific GameObject (not children)
        /// </summary>
        private string GetTMPTextDirect(GameObject go)
        {
            try
            {
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                    {
                        var textProperty = type.GetProperty("text");
                        if (textProperty != null)
                        {
                            return textProperty.GetValue(component) as string;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Clean up GameObject name to be more readable
        /// </summary>
        private string CleanGameObjectName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            name = name.Replace("(Clone)", "");
            name = name.Replace("Button", "");
            name = name.Replace("Btn", "");
            name = name.Trim();

            // Add spaces before capital letters (CamelCase to words)
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");

            return name;
        }

        /// <summary>
        /// Try to get TextMeshPro text using reflection
        /// </summary>
        private string GetTMPText(GameObject go)
        {
            try
            {
                // Look for TMP_Text component (base class for both TextMeshProUGUI and TextMeshPro)
                var components = go.GetComponentsInChildren<Component>();
                foreach (var component in components)
                {
                    if (component == null)
                        continue;

                    var type = component.GetType();

                    // Check if it's a TextMeshPro type
                    if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                    {
                        var textProperty = type.GetProperty("text");
                        if (textProperty != null)
                        {
                            string text = textProperty.GetValue(component) as string;
                            if (!string.IsNullOrEmpty(text))
                            {
                                return text;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting TMP text: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Read all visible text on the current screen (press T)
        /// </summary>
        public void ReadAllScreenText()
        {
            try
            {
                var collectedTexts = new HashSet<string>();
                var sb = new StringBuilder();

                // Find all root objects and search for text
                var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var root in rootObjects)
                {
                    if (root.activeInHierarchy)
                    {
                        CollectAllTextUnique(root.transform, collectedTexts, sb);
                    }
                }

                // Also find all TMP components directly (they may be in DontDestroyOnLoad)
                FindAllTMPText(collectedTexts, sb);

                // Also check Unity UI Text components
                var allTextComponents = FindObjectsOfType<Text>();
                foreach (var textComp in allTextComponents)
                {
                    if (textComp.gameObject.activeInHierarchy && !string.IsNullOrEmpty(textComp.text))
                    {
                        string cleanText = textComp.text.Trim();
                        if (!string.IsNullOrEmpty(cleanText) && !collectedTexts.Contains(cleanText))
                        {
                            collectedTexts.Add(cleanText);
                            sb.AppendLine(cleanText);
                        }
                    }
                }

                string allText = sb.ToString().Trim();

                if (string.IsNullOrEmpty(allText))
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak("No text found on screen", true);
                }
                else
                {
                    // Clean up - remove duplicate empty lines
                    allText = System.Text.RegularExpressions.Regex.Replace(allText, @"(\r?\n){3,}", "\n\n");
                    MonsterTrainAccessibility.ScreenReader?.Speak(allText, true);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading screen text: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read screen text", true);
            }
        }

        /// <summary>
        /// Find all TextMeshPro components using reflection
        /// </summary>
        private void FindAllTMPText(HashSet<string> collectedTexts, StringBuilder sb)
        {
            try
            {
                // Find the TMP_Text type
                Type tmpTextType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    tmpTextType = assembly.GetType("TMPro.TMP_Text");
                    if (tmpTextType != null) break;
                }

                if (tmpTextType == null) return;

                // Find all instances using FindObjectsOfType
                var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", new Type[0]);
                var genericMethod = findMethod.MakeGenericMethod(tmpTextType);
                var allTMP = genericMethod.Invoke(null, null) as Array;

                if (allTMP == null) return;

                var textProperty = tmpTextType.GetProperty("text");
                if (textProperty == null) return;

                foreach (var tmp in allTMP)
                {
                    if (tmp == null) continue;

                    // Check if active
                    var component = tmp as Component;
                    if (component == null || !component.gameObject.activeInHierarchy) continue;

                    string text = textProperty.GetValue(tmp) as string;
                    if (!string.IsNullOrEmpty(text))
                    {
                        string cleanText = text.Trim();
                        if (!string.IsNullOrEmpty(cleanText) && !collectedTexts.Contains(cleanText))
                        {
                            collectedTexts.Add(cleanText);
                            sb.AppendLine(cleanText);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error finding TMP text: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively collect text, avoiding duplicates
        /// </summary>
        private void CollectAllTextUnique(Transform transform, HashSet<string> collected, StringBuilder sb)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy) return;

            // Get Text component on this object
            var uiText = transform.GetComponent<Text>();
            if (uiText != null && !string.IsNullOrEmpty(uiText.text))
            {
                string cleaned = uiText.text.Trim();
                if (!string.IsNullOrEmpty(cleaned) && !collected.Contains(cleaned))
                {
                    collected.Add(cleaned);
                    sb.AppendLine(cleaned);
                }
            }

            // Get TMP text
            string tmpText = GetTMPTextDirect(transform.gameObject);
            if (!string.IsNullOrEmpty(tmpText))
            {
                string cleaned = tmpText.Trim();
                if (!string.IsNullOrEmpty(cleaned) && !collected.Contains(cleaned))
                {
                    collected.Add(cleaned);
                    sb.AppendLine(cleaned);
                }
            }

            // Recurse into children
            foreach (Transform child in transform)
            {
                CollectAllTextUnique(child, collected, sb);
            }
        }

        /// <summary>
        /// Called when the main menu screen is shown
        /// </summary>
        public void OnMainMenuEntered(object screen)
        {
            MonsterTrainAccessibility.LogInfo("Main menu entered");
            _isActive = true;
            _lastSelectedObject = null;

            // Announce that we're at the main menu
            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Main Menu");

            // Read the currently selected item if any
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                string text = GetTextFromGameObject(EventSystem.current.currentSelectedGameObject);
                if (!string.IsNullOrEmpty(text))
                {
                    MonsterTrainAccessibility.ScreenReader?.Queue(text);
                }
            }
        }

        /// <summary>
        /// Force re-read the current selection
        /// </summary>
        public void RereadCurrentSelection()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                string text = GetTextFromGameObject(EventSystem.current.currentSelectedGameObject);
                if (!string.IsNullOrEmpty(text))
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak(text, true);
                }
                else
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak("Unknown item", true);
                }
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Speak("Nothing selected", true);
            }
        }

        /// <summary>
        /// Pause menu reading (e.g., during loading)
        /// </summary>
        public void Pause()
        {
            _isActive = false;
        }

        /// <summary>
        /// Resume menu reading
        /// </summary>
        public void Resume()
        {
            _isActive = true;
            _lastSelectedObject = null; // Force re-announce
        }
    }

    /// <summary>
    /// Info about a clan/class for selection
    /// </summary>
    public class ClanInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Results from a completed run
    /// </summary>
    public class RunResults
    {
        public bool Won { get; set; }
        public int Score { get; set; }
        public int CovenantLevel { get; set; }
        public int FloorsCleared { get; set; }
        public int EnemiesDefeated { get; set; }
        public int CardsPlayed { get; set; }
    }
}
