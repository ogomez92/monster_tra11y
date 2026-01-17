using MonsterTrainAccessibility.Core;
using System;
using System.Collections.Generic;
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

        private void Update()
        {
            if (!_isActive)
                return;

            _pollTimer += Time.unscaledDeltaTime;
            if (_pollTimer < _pollInterval)
                return;

            _pollTimer = 0f;
            CheckForSelectionChange();
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

                if (currentSelected != null)
                {
                    string text = GetTextFromGameObject(currentSelected);
                    if (!string.IsNullOrEmpty(text))
                    {
                        MonsterTrainAccessibility.ScreenReader?.AnnounceFocus(text);
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

            // Try to get text from the object itself or its children
            string text = null;

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

            // 3. Try the GameObject name as fallback
            if (string.IsNullOrEmpty(text))
            {
                // Clean up the name - remove common Unity suffixes
                text = go.name;
                text = text.Replace("(Clone)", "");
                text = text.Replace("Button", "");
                text = text.Replace("Btn", "");
                text = text.Trim();

                // Add spaces before capital letters (CamelCase to words)
                text = System.Text.RegularExpressions.Regex.Replace(text, "([a-z])([A-Z])", "$1 $2");
            }

            return text?.Trim();
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
