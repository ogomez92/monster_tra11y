using System;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for tutorial/FTUE popups.
    /// Higher priority than battle help to ensure tutorial text is announced.
    /// </summary>
    public class TutorialHelp : IHelpContext
    {
        public string ContextId => "tutorial";
        public string ContextName => "Tutorial";
        public int Priority => 95; // Higher than BattleHelp (90)

        // Cache for detecting when tutorial changes
        private static string _lastTutorialText = null;
        private static float _lastCheckTime = 0f;
        private static bool _lastActiveState = false;

        public bool IsActive()
        {
            try
            {
                // Check periodically to avoid performance issues
                if (Time.unscaledTime - _lastCheckTime < 0.2f)
                {
                    return _lastActiveState;
                }
                _lastCheckTime = Time.unscaledTime;

                // Look for tutorial UI elements
                _lastActiveState = FindActiveTutorialPanel() != null;
                return _lastActiveState;
            }
            catch
            {
                return false;
            }
        }

        public string GetHelpText()
        {
            string tutorialText = GetCurrentTutorialText();
            if (!string.IsNullOrEmpty(tutorialText))
            {
                return $"Tutorial: {tutorialText}. Press Enter or click to continue. Press Escape to dismiss.";
            }

            return "Tutorial popup is showing. Press Enter or click to continue. Press Escape to dismiss.";
        }

        /// <summary>
        /// Find the currently active tutorial panel GameObject
        /// </summary>
        public static GameObject FindActiveTutorialPanel()
        {
            try
            {
                var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

                foreach (var root in rootObjects)
                {
                    if (!root.activeInHierarchy) continue;

                    var result = FindTutorialPanelRecursive(root.transform);
                    if (result != null)
                        return result;
                }

                // Also check DontDestroyOnLoad objects
                var allGameObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                foreach (var go in allGameObjects)
                {
                    if (!go.activeInHierarchy) continue;

                    string name = go.name.ToLower();
                    if (IsTutorialName(name) && IsVisiblePanel(go))
                    {
                        return go;
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Recursively search for tutorial panel
        /// </summary>
        private static GameObject FindTutorialPanelRecursive(Transform transform)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy)
                return null;

            string name = transform.name.ToLower();

            // Check if this is a tutorial panel
            if (IsTutorialName(name) && IsVisiblePanel(transform.gameObject))
            {
                return transform.gameObject;
            }

            // Check children
            foreach (Transform child in transform)
            {
                var result = FindTutorialPanelRecursive(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Check if the name indicates a tutorial element
        /// More strict matching to avoid false positives
        /// </summary>
        private static bool IsTutorialName(string name)
        {
            // Explicitly exclude tooltip-related elements
            if (name.Contains("tooltip"))
                return false;

            // Match "tutorial" anywhere in name (but not as part of "tooltip")
            if (name.Contains("tutorial"))
                return true;

            // Match "ftue" (first time user experience)
            if (name.Contains("ftue"))
                return true;

            // Match coach marks/onboarding
            if (name.Contains("coachmark") || name.Contains("coach_mark"))
                return true;

            if (name.Contains("onboarding"))
                return true;

            // Match help overlay/popup patterns specific to tutorials
            if (name.Contains("helpoverlay") || name.Contains("help_overlay"))
                return true;

            if (name.Contains("helppopup") || name.Contains("help_popup"))
                return true;

            // Match instruction patterns
            if (name.Contains("instructionpanel") || name.Contains("instruction_panel"))
                return true;

            return false;
        }

        /// <summary>
        /// Check if the panel is actually visible (not hidden by CanvasGroup)
        /// </summary>
        private static bool IsVisiblePanel(GameObject go)
        {
            if (go == null || !go.activeInHierarchy)
                return false;

            // Check CanvasGroup alpha up the hierarchy via reflection
            Transform current = go.transform;
            while (current != null)
            {
                if (IsHiddenByCanvasGroup(current.gameObject))
                    return false;

                current = current.parent;
            }

            // Check if it has any text content (tutorials should have text)
            return HasTextContent(go);
        }

        /// <summary>
        /// Check if a GameObject is hidden by CanvasGroup (alpha = 0) via reflection
        /// </summary>
        private static bool IsHiddenByCanvasGroup(GameObject go)
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
        /// Check if the panel has text content
        /// </summary>
        private static bool HasTextContent(GameObject go)
        {
            // Check for Text component
            var text = go.GetComponentInChildren<Text>();
            if (text != null && !string.IsNullOrEmpty(text.text))
                return true;

            // Check for TMP components via reflection
            foreach (var component in go.GetComponentsInChildren<Component>())
            {
                if (component == null) continue;
                var type = component.GetType();
                if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                {
                    var textProp = type.GetProperty("text");
                    if (textProp != null)
                    {
                        string tmpText = textProp.GetValue(component) as string;
                        if (!string.IsNullOrEmpty(tmpText))
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Get the current tutorial text content
        /// </summary>
        public static string GetCurrentTutorialText()
        {
            try
            {
                var panel = FindActiveTutorialPanel();
                if (panel == null)
                    return null;

                var sb = new System.Text.StringBuilder();

                // Collect all text from the tutorial panel
                CollectTextFromPanel(panel.transform, sb);

                string text = sb.ToString().Trim();

                // Clean up the text
                text = System.Text.RegularExpressions.Regex.Replace(text, @"(\r?\n){2,}", "\n");

                return text;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Collect text from a panel and its children
        /// </summary>
        private static void CollectTextFromPanel(Transform transform, System.Text.StringBuilder sb)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy)
                return;

            // Get Text component
            var text = transform.GetComponent<Text>();
            if (text != null && !string.IsNullOrEmpty(text.text))
            {
                string cleaned = text.text.Trim();
                if (!string.IsNullOrEmpty(cleaned) && cleaned.Length > 1)
                {
                    sb.AppendLine(cleaned);
                }
            }

            // Get TMP text via reflection
            foreach (var component in transform.GetComponents<Component>())
            {
                if (component == null) continue;
                var type = component.GetType();
                if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                {
                    var textProp = type.GetProperty("text");
                    if (textProp != null)
                    {
                        string tmpText = textProp.GetValue(component) as string;
                        if (!string.IsNullOrEmpty(tmpText))
                        {
                            string cleaned = tmpText.Trim();
                            if (!string.IsNullOrEmpty(cleaned) && cleaned.Length > 1)
                            {
                                sb.AppendLine(cleaned);
                            }
                        }
                    }
                }
            }

            // Recurse into children
            foreach (Transform child in transform)
            {
                CollectTextFromPanel(child, sb);
            }
        }

        /// <summary>
        /// Check if new tutorial text has appeared and return it if so
        /// </summary>
        public static string CheckForNewTutorialText()
        {
            try
            {
                if (!_lastActiveState)
                {
                    _lastTutorialText = null;
                    return null;
                }

                string currentText = GetCurrentTutorialText();

                // If we have new text that's different from the last one
                if (!string.IsNullOrEmpty(currentText) && currentText != _lastTutorialText)
                {
                    _lastTutorialText = currentText;
                    return currentText;
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Reset the tutorial tracking state
        /// </summary>
        public static void Reset()
        {
            _lastTutorialText = null;
            _lastActiveState = false;
        }
    }
}
