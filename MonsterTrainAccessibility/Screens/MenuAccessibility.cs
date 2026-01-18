using MonsterTrainAccessibility.Core;
using System;
using System.Collections.Generic;
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
                            MonsterTrainAccessibility.ScreenReader?.Speak(newContent.Trim(), true);
                        }
                    }
                    _lastScreenTextHash = currentHash;
                }
            }
            catch { }
        }

        /// <summary>
        /// Find large text content in dialogs/popups
        /// </summary>
        private void FindLargeTextContent(Transform transform, StringBuilder sb)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy) return;

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

            // 1. Check for toggle/checkbox components first
            text = GetToggleText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 2. Check for logbook/compendium items
            text = GetLogbookItemText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 3. Try to get text with context (handles short button labels)
            text = GetTextWithContext(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 4. Try the GameObject name as fallback (but make it more readable)
            text = CleanGameObjectName(go.name);

            return text?.Trim();
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
