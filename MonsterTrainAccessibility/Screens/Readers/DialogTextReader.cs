using MonsterTrainAccessibility.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Extracted reader for Dialog UI elements.
    /// </summary>
    internal static class DialogTextReader
    {

        /// <summary>
        /// Check if text appears to be placeholder/debug text that shouldn't be read.
        /// </summary>
        internal static bool IsPlaceholderText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            string lower = text.ToLower();

            // Check for common placeholder patterns
            if (lower.Contains("placeholder"))
                return true;
            if (lower.Contains("(should"))  // Developer comments like "(should layer below cards)"
                return true;
            if (lower.Contains("todo"))
                return true;
            if (lower.Contains("fixme"))
                return true;
            // Drag and drop hints
            if (lower.Contains("+drag") || lower.Contains("drag ") || lower.StartsWith("drag"))
                return true;
            // Missing/unset references
            if (lower == "missing" || lower.StartsWith("missing ") || lower.Contains("missing:"))
                return true;
            // Localization keys that weren't resolved
            if (text.Contains("_descriptionKey") || text.Contains("_nameKey") || text.Contains("_titleKey"))
                return true;
            // Unity default text
            if (lower == "new text" || lower == "text" || lower == "label")
                return true;
            // Debug strings
            if (lower.StartsWith("debug") || lower.Contains("[debug]"))
                return true;

            return false;
        }


        /// <summary>
        /// Check if text is garbage/meaningless that shouldn't be read when pressing T.
        /// This is more aggressive filtering than IsPlaceholderText().
        /// </summary>
        internal static bool IsGarbageText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            text = text.Trim();
            if (text.Length == 0)
                return true;

            // Check placeholder text first
            if (IsPlaceholderText(text))
                return true;

            // Filter random strings of the same letter (e.g., "ggg", "Gggggggg", "xxxxxxx")
            if (text.Length >= 3)
            {
                char firstChar = char.ToLower(text[0]);
                if (char.IsLetter(firstChar))
                {
                    bool allSameLetter = true;
                    for (int i = 1; i < text.Length; i++)
                    {
                        if (char.ToLower(text[i]) != firstChar)
                        {
                            allSameLetter = false;
                            break;
                        }
                    }
                    if (allSameLetter)
                        return true;
                }
            }

            // Filter pure markup/symbols with no actual content
            string stripped = TextUtilities.StripRichTextTags(text);
            if (string.IsNullOrWhiteSpace(stripped))
                return true;

            // Filter text that is mostly punctuation/symbols
            int letterCount = 0;
            int symbolCount = 0;
            foreach (char c in stripped)
            {
                if (char.IsLetterOrDigit(c))
                    letterCount++;
                else if (!char.IsWhiteSpace(c))
                    symbolCount++;
            }
            // If text has symbols but very few letters, it's probably garbage
            if (symbolCount > 0 && letterCount < 2 && stripped.Length < 10)
                return true;

            // Filter single characters that aren't meaningful
            if (stripped.Length == 1 && !char.IsDigit(stripped[0]))
                return true;

            // Filter strings that look like icon font characters (common in Unity UI)
            if (stripped.Length <= 2)
            {
                bool allSpecial = true;
                foreach (char c in stripped)
                {
                    // Common letter/number characters are fine, but special chars aren't
                    if (char.IsLetterOrDigit(c))
                    {
                        allSpecial = false;
                        break;
                    }
                }
                if (allSpecial)
                    return true;
            }

            // Filter "The " prefix followed by garbage (e.g., "The Gggggggg")
            if (stripped.StartsWith("The ", StringComparison.OrdinalIgnoreCase) && stripped.Length > 4)
            {
                string afterThe = stripped.Substring(4).Trim();
                if (IsGarbageText(afterThe))
                    return true;
            }

            // Filter multi-word garbage where each word is the same letter repeated (e.g., "GGGGG GGGG")
            if (stripped.Contains(' '))
            {
                var words = stripped.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length >= 1 && words.All(w =>
                {
                    if (w.Length < 3) return false;
                    char fc = char.ToLower(w[0]);
                    return char.IsLetter(fc) && w.All(c => char.ToLower(c) == fc);
                }))
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Check if a transform is inside a button element.
        /// </summary>
        internal static bool IsInsideButton(Transform t, Transform root)
        {
            Transform current = t;
            while (current != null && current != root)
            {
                string name = current.name.ToLower();
                if (name.Contains("button") || name.Contains("yes") || name.Contains("no") ||
                    name.Contains("ok") || name.Contains("cancel") || name.Contains("confirm"))
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }


        /// <summary>
        /// Check if the given GameObject is inside a Dialog context (has a Dialog component in parent hierarchy).
        /// </summary>
        internal static bool IsInDialogContext(GameObject go)
        {
            if (go == null) return false;

            Transform current = go.transform;
            int depth = 0;
            while (current != null && depth < 8)
            {
                foreach (var comp in current.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == "Dialog")
                    {
                        return true;
                    }
                }
                current = current.parent;
                depth++;
            }
            return false;
        }

    }
}
