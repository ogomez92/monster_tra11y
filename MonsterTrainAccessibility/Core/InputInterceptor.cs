using UnityEngine;
using UnityEngine.EventSystems;

namespace MonsterTrainAccessibility.Core
{
    /// <summary>
    /// MonoBehaviour that handles accessibility hotkeys.
    /// Navigation is handled by the game's EventSystem - we just provide info hotkeys.
    /// </summary>
    public class InputInterceptor : MonoBehaviour
    {
        /// <summary>
        /// Whether input handling is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Cooldown to prevent key repeat spam
        /// </summary>
        private float _inputCooldown = 0f;
        private const float INPUT_COOLDOWN_TIME = 0.15f;

        private void Update()
        {
            if (!IsEnabled)
                return;

            // Check if game has focus
            if (!Application.isFocused)
                return;

            // Update cooldown
            if (_inputCooldown > 0)
            {
                _inputCooldown -= Time.unscaledDeltaTime;
                return;
            }

            var config = MonsterTrainAccessibility.AccessibilitySettings;

            if (config == null)
                return;

            // Information hotkeys - these don't interfere with game navigation
            if (Input.GetKeyDown(config.ReadCurrentKey.Value))
            {
                RereadCurrentSelection();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(config.ReadTextKey.Value))
            {
                ReadAllScreenText();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(config.ReadHandKey.Value))
            {
                ReadHand();
            }
            else if (Input.GetKeyDown(config.ReadFloorsKey.Value))
            {
                ReadFloors();
            }
            else if (Input.GetKeyDown(config.ReadEnemiesKey.Value))
            {
                ReadEnemies();
            }
            else if (Input.GetKeyDown(config.ReadResourcesKey.Value))
            {
                ReadResources();
            }
            else if (Input.GetKeyDown(config.ToggleVerbosityKey.Value))
            {
                config.CycleVerbosity();
            }

        }

        /// <summary>
        /// Re-read the currently selected UI element
        /// </summary>
        private void RereadCurrentSelection()
        {
            MonsterTrainAccessibility.MenuHandler?.RereadCurrentSelection();
        }

        /// <summary>
        /// Read all text on screen (patch notes, descriptions, etc.)
        /// </summary>
        private void ReadAllScreenText()
        {
            MonsterTrainAccessibility.MenuHandler?.ReadAllScreenText();
        }

        /// <summary>
        /// Read the player's current hand
        /// </summary>
        private void ReadHand()
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle != null && battle.IsInBattle)
            {
                battle.AnnounceHand();
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Speak("Not in battle", true);
            }
        }

        /// <summary>
        /// Read all floor information
        /// </summary>
        private void ReadFloors()
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle != null && battle.IsInBattle)
            {
                battle.AnnounceAllFloors();
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Speak("Not in battle", true);
            }
        }

        /// <summary>
        /// Read enemy information and intents
        /// </summary>
        private void ReadEnemies()
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle != null && battle.IsInBattle)
            {
                battle.AnnounceEnemies();
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Speak("Not in battle", true);
            }
        }

        /// <summary>
        /// Read current resources (ember, pyre health, gold)
        /// </summary>
        private void ReadResources()
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle != null && battle.IsInBattle)
            {
                battle.AnnounceResources();
            }
            else
            {
                // Could also read gold/other resources outside battle
                MonsterTrainAccessibility.ScreenReader?.Speak("Not in battle", true);
            }
        }

        /// <summary>
        /// Temporarily disable input handling
        /// </summary>
        public void Pause()
        {
            IsEnabled = false;
        }

        /// <summary>
        /// Re-enable input handling
        /// </summary>
        public void Resume()
        {
            IsEnabled = true;
        }
    }
}
