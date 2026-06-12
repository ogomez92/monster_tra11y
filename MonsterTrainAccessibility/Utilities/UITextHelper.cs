using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MonsterTrainAccessibility.Utilities
{
    /// <summary>
    /// Shared helpers for extracting text from Unity UI components.
    /// Used by all reader classes that need to get text from GameObjects.
    /// </summary>
    internal static class UITextHelper
    {
        /// <summary>
        /// Get TextMeshPro text directly from a GameObject's own components (not children).
        /// </summary>
        public static string GetTMPTextDirect(GameObject go)
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
        /// Get TextMeshPro text from a GameObject or any of its children.
        /// </summary>
        public static string GetTMPText(GameObject go)
        {
            try
            {
                var components = go.GetComponentsInChildren<Component>();
                foreach (var component in components)
                {
                    if (component == null) continue;

                    var type = component.GetType();
                    if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                    {
                        var textProperty = type.GetProperty("text");
                        if (textProperty != null)
                        {
                            string text = textProperty.GetValue(component) as string;
                            if (!string.IsNullOrEmpty(text))
                                return text;
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
        /// Get the rendered text from a TMP label obtained via reflection.
        /// The game's SetTextSafe extension uses TMP_Text.SetText(), which
        /// updates the rendered char buffer but NOT the text property - so
        /// reading .text returns the prefab's placeholder (e.g. "12345").
        /// GetParsedText() returns what is actually on screen; .text is the
        /// fallback for labels that haven't been rendered yet. Pass
        /// fallbackToTextProperty: false for read-the-screen dumps, where a
        /// label rendered empty shows nothing and the fallback would
        /// resurrect the prefab placeholder.
        /// </summary>
        public static string GetRenderedTMPLabelText(object tmpLabel, bool fallbackToTextProperty = true)
        {
            if (tmpLabel == null) return null;
            var type = tmpLabel.GetType();

            try
            {
                var getParsed = type.GetMethod("GetParsedText", Type.EmptyTypes);
                if (getParsed != null)
                {
                    string parsed = getParsed.Invoke(tmpLabel, null) as string;
                    if (!string.IsNullOrWhiteSpace(parsed))
                    {
                        // Sprites render as private-use/replacement chars in
                        // the parsed buffer - silent for speech, just drop them
                        parsed = Regex.Replace(parsed, "[\uE000-\uF8FF\uFFFD]", " ");
                        return parsed.Trim();
                    }
                }
            }
            catch { }

            if (!fallbackToTextProperty)
                return null;

            try
            {
                var textProp = type.GetProperty("text");
                string text = textProp?.GetValue(tmpLabel) as string;
                if (!string.IsNullOrWhiteSpace(text))
                    return text.Trim();
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Get the rendered (on-screen) TMP text from a GameObject's own
        /// components. Parsed text only, no .text fallback - meant for
        /// read-all-screen dumps where the .text property would return
        /// prefab placeholders ("Card text goes here.") for every label
        /// the game filled via SetText().
        /// </summary>
        public static string GetRenderedTMPTextDirect(GameObject go)
        {
            try
            {
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                    {
                        return GetRenderedTMPLabelText(component, fallbackToTextProperty: false);
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Clean up GameObject name to be more readable (removes Clone, Button suffixes, adds spaces).
        /// </summary>
        public static string CleanGameObjectName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            name = name.Replace("(Clone)", "");
            name = name.Replace("Button", "");
            name = name.Replace("Btn", "");
            name = name.Trim();

            if (name.StartsWith("SP ", StringComparison.OrdinalIgnoreCase))
                name = "Special " + name.Substring(3);

            name = Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");

            return name;
        }

        /// <summary>
        /// Find a component by type name anywhere in the GameObject's hierarchy (up to parent).
        /// </summary>
        public static Component FindComponentInHierarchy(GameObject go, string typeName)
        {
            if (go == null || string.IsNullOrEmpty(typeName)) return null;

            Transform current = go.transform;
            while (current != null)
            {
                foreach (var comp in current.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == typeName)
                        return comp;
                }
                current = current.parent;
            }
            return null;
        }

        /// <summary>
        /// Get all text from a transform hierarchy, concatenated.
        /// </summary>
        public static string GetAllTextFromTransform(Transform root)
        {
            var sb = new StringBuilder();
            CollectAllText(root, sb);
            return sb.ToString().Trim();
        }

        /// <summary>
        /// Recursively collect all TMP text from a transform hierarchy.
        /// Rendered text only (.text returns prefab placeholders for labels
        /// the game fills via SetText - settings values read "60" while the
        /// screen shows the real number), and consecutive duplicates are
        /// skipped (styled labels carry shadow/outline TMP copies of the
        /// same text, which otherwise read as "60 60 60 60 60").
        /// </summary>
        public static void CollectAllText(Transform transform, StringBuilder sb)
        {
            string lastCollected = null;
            CollectAllTextInner(transform, sb, ref lastCollected);
        }

        private static void CollectAllTextInner(Transform transform, StringBuilder sb, ref string lastCollected)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy)
                return;

            foreach (var component in transform.GetComponents<Component>())
            {
                if (component == null) continue;
                var type = component.GetType();
                if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                {
                    string text = GetRenderedTMPLabelText(component, fallbackToTextProperty: false);
                    if (!string.IsNullOrEmpty(text) && text != lastCollected)
                    {
                        if (sb.Length > 0) sb.Append(". ");
                        sb.Append(text);
                        lastCollected = text;
                    }
                }
            }

            foreach (Transform child in transform)
            {
                CollectAllTextInner(child, sb, ref lastCollected);
            }
        }
    }
}
