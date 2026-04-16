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
    /// Extracted reader for Settings UI elements.
    /// </summary>
    internal static class SettingsTextReader
    {

        /// <summary>
        /// Get text for settings screen elements (dropdowns, sliders, toggles)
        /// These have a parent with SettingsEntry component containing the label
        /// </summary>
        internal static string GetSettingsElementText(GameObject go)
        {
            try
            {
                // Find SettingsEntry component in parent hierarchy
                string settingLabel = null;
                Transform current = go.transform;

                for (int i = 0; i < 3 && current.parent != null; i++)
                {
                    Transform parent = current.parent;

                    // Check if parent has SettingsEntry component
                    foreach (var component in parent.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "SettingsEntry")
                        {
                            // Found it - get the label from parent's name
                            settingLabel = CleanSettingsLabel(parent.name);
                            break;
                        }
                    }

                    if (settingLabel != null) break;
                    current = parent;
                }

                if (string.IsNullOrEmpty(settingLabel))
                    return null;

                // Now get the current value based on the control type
                string value = null;

                // Check for dropdown (GameUISelectableDropdown)
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    string typeName = type.Name;

                    if (typeName.Contains("Dropdown"))
                    {
                        value = GetDropdownValue(component);
                        break;
                    }
                    else if (typeName.Contains("Slider"))
                    {
                        value = GetSliderValue(go);
                        break;
                    }
                    else if (typeName.Contains("Toggle") || typeName.Contains("Checkbox"))
                    {
                        value = GetToggleValue(go);
                        break;
                    }
                }

                // If we couldn't get a specific value, try to get text from children
                if (string.IsNullOrEmpty(value))
                {
                    value = UITextHelper.GetTMPText(go);
                    if (!string.IsNullOrEmpty(value))
                    {
                        value = TextUtilities.StripRichTextTags(value.Trim());
                    }
                }

                if (!string.IsNullOrEmpty(value))
                {
                    return $"{settingLabel}: {value}";
                }

                return settingLabel;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting settings element text: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Clean up settings label names (e.g., "ResolutionDropdown" -> "Resolution")
        /// </summary>
        internal static string CleanSettingsLabel(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            // Remove common suffixes
            name = name.Replace("Dropdown", "");
            name = name.Replace("dropdown", "");
            name = name.Replace("Toggle", "");
            name = name.Replace("toggle", "");
            name = name.Replace("Slider", "");
            name = name.Replace("slider", "");
            name = name.Replace("Control", "");
            name = name.Replace("control", "");
            name = name.Replace("Option", "");
            name = name.Replace("option", "");
            name = name.Replace("Setting", "");
            name = name.Replace("setting", "");
            name = name.Replace("Entry", "");
            name = name.Replace("entry", "");
            name = name.Replace("input", "");
            name = name.Replace("Input", "");

            // Add spaces before capital letters
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");

            // Handle specific labels
            name = name.Replace("BG", "Background");
            name = name.Replace("SFX", "Sound Effects");
            name = name.Replace("VSync", "V-Sync");
            name = name.Replace("Vsync", "V-Sync");
            name = name.Replace("UI", "Interface");

            return name.Trim();
        }


        /// <summary>
        /// Get the current value from a dropdown component
        /// </summary>
        internal static string GetDropdownValue(Component dropdown)
        {
            try
            {
                var type = dropdown.GetType();

                // Try to get the current selected text
                var getCurrentTextMethod = type.GetMethod("GetCurrentText") ??
                                           type.GetMethod("GetText") ??
                                           type.GetMethod("GetSelectedText");
                if (getCurrentTextMethod != null)
                {
                    var result = getCurrentTextMethod.Invoke(dropdown, null);
                    if (result != null)
                        return result.ToString();
                }

                // Try currentText or text property
                var textProp = type.GetProperty("currentText") ??
                               type.GetProperty("text") ??
                               type.GetProperty("captionText");
                if (textProp != null)
                {
                    var result = textProp.GetValue(dropdown);
                    if (result != null)
                    {
                        // It might be a TMP_Text component
                        var textComponent = result as Component;
                        if (textComponent != null)
                        {
                            var tmpText = UITextHelper.GetTMPTextDirect(textComponent.gameObject);
                            if (!string.IsNullOrEmpty(tmpText))
                                return tmpText;
                        }
                        return result.ToString();
                    }
                }

                // Try to find the label/caption child
                var dropdownGO = dropdown.gameObject;
                foreach (Transform child in dropdownGO.transform)
                {
                    string childName = child.name.ToLower();
                    if (childName.Contains("label") || childName.Contains("caption") || childName.Contains("text"))
                    {
                        string text = UITextHelper.GetTMPTextDirect(child.gameObject);
                        if (!string.IsNullOrEmpty(text))
                            return text.Trim();
                    }
                }

                // Last resort - get any TMP text in children
                string anyText = UITextHelper.GetTMPText(dropdownGO);
                if (!string.IsNullOrEmpty(anyText))
                    return anyText.Trim();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting dropdown value: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Get the current value from a slider
        /// </summary>
        internal static string GetSliderValue(GameObject go)
        {
            try
            {
                var slider = go.GetComponent<Slider>();
                if (slider != null)
                {
                    // Return as percentage
                    int percent = Mathf.RoundToInt(slider.normalizedValue * 100);
                    return $"{percent}%";
                }

                // Try reflection for custom slider types
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();

                    var valueProp = type.GetProperty("value") ?? type.GetProperty("normalizedValue");
                    if (valueProp != null)
                    {
                        var val = valueProp.GetValue(component);
                        if (val is float f)
                        {
                            int percent = Mathf.RoundToInt(f * 100);
                            return $"{percent}%";
                        }
                    }
                }
            }
            catch { }
            return null;
        }


        /// <summary>
        /// Get the current value from a toggle (on/off)
        /// </summary>
        internal static string GetToggleValue(GameObject go)
        {
            try
            {
                var toggle = go.GetComponent<Toggle>();
                if (toggle != null)
                {
                    return toggle.isOn ? "on" : "off";
                }

                // Try reflection for custom toggle types
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();

                    var isOnProp = type.GetProperty("isOn") ?? type.GetProperty("IsOn") ?? type.GetProperty("isChecked");
                    if (isOnProp != null)
                    {
                        var val = isOnProp.GetValue(component);
                        if (val is bool b)
                            return b ? "on" : "off";
                    }

                    // Check underlying Unity Toggle if it's a wrapper
                    var toggleField = type.GetField("toggle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (toggleField != null)
                    {
                        var innerToggle = toggleField.GetValue(component) as Toggle;
                        if (innerToggle != null)
                            return innerToggle.isOn ? "on" : "off";
                    }
                }
            }
            catch { }
            return null;
        }


        /// <summary>
        /// Special handling for the Trial toggle on BattleIntroScreen
        /// Returns full trial info: name, description, reward, and toggle state
        /// </summary>
        internal static string GetTrialToggleText(GameObject go)
        {
            try
            {
                // Check if this might be a trial toggle by looking at hierarchy
                bool isBattleIntroToggle = false;
                Component battleIntroScreen = null;
                Transform current = go.transform;

                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "BattleIntroScreen")
                        {
                            battleIntroScreen = component;
                            isBattleIntroToggle = true;
                            break;
                        }
                    }
                    if (isBattleIntroToggle) break;
                    current = current.parent;
                }

                if (!isBattleIntroToggle || battleIntroScreen == null)
                    return null;

                // Get the BattleIntroScreen's trial data
                var screenType = battleIntroScreen.GetType();

                // Get trialEnabled field
                var trialEnabledField = screenType.GetField("trialEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                bool trialEnabled = false;
                if (trialEnabledField != null)
                {
                    var val = trialEnabledField.GetValue(battleIntroScreen);
                    if (val is bool b) trialEnabled = b;
                }

                // Get trialData field
                var trialDataField = screenType.GetField("trialData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                object trialData = trialDataField?.GetValue(battleIntroScreen);

                if (trialData == null)
                    return null;

                // Extract trial information
                var trialType = trialData.GetType();
                string ruleName = null;
                string ruleDescription = null;
                string rewardName = null;

                // The rule comes from the 'sin' field (SinsData), which is a RelicData subclass
                var sinField = trialType.GetField("sin", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (sinField != null)
                {
                    var sinData = sinField.GetValue(trialData);
                    if (sinData != null)
                    {
                        var sinType = sinData.GetType();

                        // Get the rule name from sin
                        var getNameMethod = sinType.GetMethod("GetName");
                        if (getNameMethod != null)
                        {
                            ruleName = getNameMethod.Invoke(sinData, null) as string;
                        }

                        // Get the description by creating a RelicState from the SinsData.
                        // RelicState.GetDescription() fills in numeric parameters (e.g., attack values)
                        // that raw localization of the description key would miss.
                        ruleDescription = GetRelicDescription(sinData);
                    }
                }

                // Get reward info from the 'reward' field
                var rewardField = trialType.GetField("reward", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (rewardField != null)
                {
                    var rewardData = rewardField.GetValue(trialData);
                    if (rewardData != null)
                    {
                        rewardName = ShopTextReader.GetRewardName(rewardData);
                    }
                }

                // Build the announcement
                var sb = new StringBuilder();
                sb.Append("Trial toggle: ");
                sb.Append(trialEnabled ? "ON" : "OFF");
                sb.Append(". ");

                if (trialEnabled)
                {
                    if (!string.IsNullOrEmpty(ruleName))
                    {
                        sb.Append("Additional rule: ");
                        sb.Append(ruleName);
                        sb.Append(". ");
                    }

                    if (!string.IsNullOrEmpty(ruleDescription))
                    {
                        sb.Append(TextUtilities.StripRichTextTags(ruleDescription));
                        sb.Append(" ");
                    }

                    if (!string.IsNullOrEmpty(rewardName))
                    {
                        sb.Append("You will gain additional reward: ");
                        sb.Append(rewardName);
                        sb.Append(". ");
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(ruleName))
                    {
                        sb.Append("If enabled, additional rule: ");
                        sb.Append(ruleName);
                        sb.Append(". ");
                    }

                    if (!string.IsNullOrEmpty(ruleDescription))
                    {
                        sb.Append(TextUtilities.StripRichTextTags(ruleDescription));
                        sb.Append(" ");
                    }

                    if (!string.IsNullOrEmpty(rewardName))
                    {
                        sb.Append("Enable to gain additional reward: ");
                        sb.Append(rewardName);
                        sb.Append(". ");
                    }
                }

                sb.Append("Press Enter to toggle.");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting trial toggle text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get a relic's description with numeric parameters filled in.
        /// Creates a RelicState from the RelicData and calls GetDescription(),
        /// which uses CardEffectLocalizationContext to substitute values.
        /// </summary>
        internal static string GetRelicDescription(object relicData)
        {
            try
            {
                // Find RelicState type and create an instance from the RelicData
                var relicStateType = Utilities.ReflectionHelper.FindType("RelicState");
                if (relicStateType == null) return null;

                var ctor = relicStateType.GetConstructor(new[] { relicData.GetType().BaseType ?? relicData.GetType() });
                if (ctor == null)
                {
                    // Try finding constructor that takes RelicData specifically
                    var relicDataType = Utilities.ReflectionHelper.FindType("RelicData");
                    if (relicDataType != null)
                        ctor = relicStateType.GetConstructor(new[] { relicDataType });
                }
                if (ctor == null) return null;

                var relicState = ctor.Invoke(new[] { relicData });
                if (relicState == null) return null;

                // Call GetDescription() which handles parameter substitution
                var getDescMethod = relicStateType.GetMethod("GetDescription");
                if (getDescMethod == null) return null;

                // GetDescription has optional RelicManager parameter
                var parameters = getDescMethod.GetParameters();
                object result;
                if (parameters.Length == 0)
                    result = getDescMethod.Invoke(relicState, null);
                else
                    result = getDescMethod.Invoke(relicState, new object[] { null });

                return result as string;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting relic description: {ex.Message}");
                return null;
            }
        }
    }
}
