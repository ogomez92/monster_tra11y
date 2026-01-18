using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTrainAccessibility.Battle
{
    /// <summary>
    /// Handles keyboard-based unit targeting for spell cards.
    /// When a spell requires a target, this system allows the player
    /// to select a unit using arrow keys instead of mouse.
    /// </summary>
    public class UnitTargetingSystem : MonoBehaviour
    {
        /// <summary>
        /// Singleton instance
        /// </summary>
        public static UnitTargetingSystem Instance { get; private set; }

        /// <summary>
        /// Whether unit targeting mode is currently active
        /// </summary>
        public bool IsTargeting { get; private set; }

        /// <summary>
        /// Currently selected target index
        /// </summary>
        public int SelectedIndex { get; private set; } = 0;

        /// <summary>
        /// List of available targets (unit names/descriptions)
        /// </summary>
        private List<string> _targets = new List<string>();

        /// <summary>
        /// The card being played (for reference during targeting)
        /// </summary>
        private object _pendingCard;

        /// <summary>
        /// Callback when target is confirmed
        /// </summary>
        private Action<int> _onConfirm;

        /// <summary>
        /// Callback when targeting is cancelled
        /// </summary>
        private Action _onCancel;

        /// <summary>
        /// Input cooldown to prevent key repeat
        /// </summary>
        private float _inputCooldown = 0f;
        private const float INPUT_COOLDOWN_TIME = 0.15f;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!IsTargeting)
                return;

            // Update cooldown
            if (_inputCooldown > 0)
            {
                _inputCooldown -= Time.unscaledDeltaTime;
                return;
            }

            // Arrow keys to cycle targets
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                CycleTarget(-1);
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                CycleTarget(1);
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            // Number keys for direct target selection (1-9)
            else if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                SelectTarget(0);
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                SelectTarget(1);
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                SelectTarget(2);
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                SelectTarget(3);
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
            {
                SelectTarget(4);
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            // Enter to confirm
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ConfirmSelection();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            // Escape to cancel
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelTargeting();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
        }

        /// <summary>
        /// Start unit targeting mode for a spell
        /// </summary>
        /// <param name="card">The card being played</param>
        /// <param name="onConfirm">Called with selected target index when confirmed</param>
        /// <param name="onCancel">Called when targeting is cancelled</param>
        public void StartTargeting(object card, Action<int> onConfirm, Action onCancel)
        {
            _pendingCard = card;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            IsTargeting = true;
            SelectedIndex = 0;

            // Get available targets from battle
            RefreshTargets();

            MonsterTrainAccessibility.LogInfo("Unit targeting started");
            AnnounceTargetingStart();
        }

        /// <summary>
        /// Refresh the list of available targets
        /// </summary>
        private void RefreshTargets()
        {
            _targets.Clear();
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle != null)
            {
                // Get all enemies on all floors
                var enemies = battle.GetAllEnemies();
                if (enemies != null)
                {
                    _targets.AddRange(enemies);
                }
            }

            // If no targets found, add placeholder
            if (_targets.Count == 0)
            {
                _targets.Add("No valid targets");
            }
        }

        /// <summary>
        /// Cancel targeting mode externally (e.g., if battle ends)
        /// </summary>
        public void ForceCancel()
        {
            if (IsTargeting)
            {
                IsTargeting = false;
                _pendingCard = null;
                _onConfirm = null;
                _onCancel = null;
                _targets.Clear();
                MonsterTrainAccessibility.LogInfo("Unit targeting force cancelled");
            }
        }

        /// <summary>
        /// Select a specific target by index
        /// </summary>
        private void SelectTarget(int index)
        {
            if (index < 0 || index >= _targets.Count)
                return;

            SelectedIndex = index;
            AnnounceCurrentTarget();
        }

        /// <summary>
        /// Cycle to the next/previous target
        /// </summary>
        private void CycleTarget(int direction)
        {
            if (_targets.Count == 0)
                return;

            SelectedIndex += direction;
            if (SelectedIndex >= _targets.Count) SelectedIndex = 0;
            if (SelectedIndex < 0) SelectedIndex = _targets.Count - 1;

            AnnounceCurrentTarget();
        }

        /// <summary>
        /// Confirm the current target selection
        /// </summary>
        private void ConfirmSelection()
        {
            IsTargeting = false;
            var callback = _onConfirm;
            var index = SelectedIndex;

            string targetName = _targets.Count > index ? _targets[index] : "target";

            _pendingCard = null;
            _onConfirm = null;
            _onCancel = null;
            _targets.Clear();

            MonsterTrainAccessibility.ScreenReader?.Speak($"Targeting {targetName}", true);
            MonsterTrainAccessibility.LogInfo($"Unit targeting confirmed: index {index}");

            callback?.Invoke(index);
        }

        /// <summary>
        /// Cancel the targeting
        /// </summary>
        private void CancelTargeting()
        {
            IsTargeting = false;
            var callback = _onCancel;

            _pendingCard = null;
            _onConfirm = null;
            _onCancel = null;
            _targets.Clear();

            MonsterTrainAccessibility.ScreenReader?.Speak("Spell cancelled", true);
            MonsterTrainAccessibility.LogInfo("Unit targeting cancelled");

            callback?.Invoke();
        }

        /// <summary>
        /// Announce that targeting mode has started
        /// </summary>
        private void AnnounceTargetingStart()
        {
            string message = $"Select target. {_targets.Count} available. ";
            message += "Use arrows to cycle, number keys to select directly. Enter to confirm, Escape to cancel. ";
            if (_targets.Count > 0 && SelectedIndex < _targets.Count)
            {
                message += $"Current: {_targets[SelectedIndex]}";
            }
            MonsterTrainAccessibility.ScreenReader?.Speak(message, true);
        }

        /// <summary>
        /// Announce the current target
        /// </summary>
        private void AnnounceCurrentTarget()
        {
            if (_targets.Count > 0 && SelectedIndex < _targets.Count)
            {
                string message = $"Target {SelectedIndex + 1} of {_targets.Count}: {_targets[SelectedIndex]}";
                MonsterTrainAccessibility.ScreenReader?.Speak(message, true);
            }
        }
    }
}
