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

            // 1. Try Unity UI Text component
            var uiText = go.GetComponentInChildren<Text>();
            if (uiText != null && !string.IsNullOrEmpty(uiText.text))
            {
                text = uiText.text;
            }

            // 2. Try TextMeshPro (uses reflection since we may not have the assembly reference)
            if (string.IsNullOrEmpty(text))
            {
                text = GetTMPText(go);
            }

            // 3. Try the GameObject name as fallback (but make it more readable)
            if (string.IsNullOrEmpty(text))
            {
                text = CleanGameObjectName(go.name);
            }

            return text?.Trim();
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
