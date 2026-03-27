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
        /// </summary>
        public static void CollectAllText(Transform transform, StringBuilder sb)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy)
                return;

            foreach (var component in transform.GetComponents<Component>())
            {
                if (component == null) continue;
                var type = component.GetType();
                if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                {
                    var textProp = type.GetProperty("text");
                    if (textProp != null)
                    {
                        string text = textProp.GetValue(component) as string;
                        if (!string.IsNullOrEmpty(text))
                        {
                            text = text.Trim();
                            if (text.Length > 0)
                            {
                                if (sb.Length > 0) sb.Append(". ");
                                sb.Append(text);
                            }
                        }
                    }
                }
            }

            foreach (Transform child in transform)
            {
                CollectAllText(child, sb);
            }
        }
    }
}
