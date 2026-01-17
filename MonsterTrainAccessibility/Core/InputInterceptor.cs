using UnityEngine;

namespace MonsterTrainAccessibility.Core
{
    /// <summary>
    /// MonoBehaviour that intercepts keyboard input for accessibility navigation.
    /// Runs every frame and checks for key presses.
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
            var focus = MonsterTrainAccessibility.FocusManager;

            if (config == null || focus == null)
                return;

            // Navigation keys
            if (Input.GetKeyDown(config.NavigateUpKey.Value))
            {
                focus.NavigateUp();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(config.NavigateDownKey.Value))
            {
                focus.NavigateDown();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(config.NavigateLeftKey.Value))
            {
                focus.NavigateLeft();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(config.NavigateRightKey.Value))
            {
                focus.NavigateRight();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }

            // Activation
            else if (Input.GetKeyDown(config.ActivateKey.Value) ||
                     Input.GetKeyDown(config.AlternateActivateKey.Value))
            {
                focus.Activate();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }

            // Back/Cancel
            else if (Input.GetKeyDown(config.BackKey.Value))
            {
                focus.GoBack();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }

            // Information hotkeys
            else if (Input.GetKeyDown(config.ReadCurrentKey.Value))
            {
                focus.RereadCurrentFocus();
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

            // Number keys for quick card selection (1-9)
            for (int i = 1; i <= 9; i++)
            {
                KeyCode key = KeyCode.Alpha1 + (i - 1);
                if (Input.GetKeyDown(key))
                {
                    SelectCardByNumber(i);
                    _inputCooldown = INPUT_COOLDOWN_TIME;
                    break;
                }
            }
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
        /// Quick-select a card by number key
        /// </summary>
        private void SelectCardByNumber(int cardNumber)
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle != null && battle.IsInBattle)
            {
                battle.SelectCardByIndex(cardNumber - 1);
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
