using System;
using UnityEngine;

namespace MonsterTrainAccessibility.Battle
{
    /// <summary>
    /// Handles keyboard-based floor targeting for playing cards.
    /// When a card requires floor selection, this system allows the player
    /// to select a floor using number keys (1-3) or arrow keys instead of mouse.
    /// </summary>
    public class FloorTargetingSystem : MonoBehaviour
    {
        /// <summary>
        /// Singleton instance
        /// </summary>
        public static FloorTargetingSystem Instance { get; private set; }

        /// <summary>
        /// Whether floor targeting mode is currently active
        /// </summary>
        public bool IsTargeting { get; private set; }

        /// <summary>
        /// Currently selected floor (1-3, where 1 is bottom, 3 is top)
        /// </summary>
        public int SelectedFloor { get; private set; } = 1;

        /// <summary>
        /// The card being played (for reference during targeting)
        /// </summary>
        private object _pendingCard;

        /// <summary>
        /// Callback when floor is confirmed
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

            // Number keys for direct floor selection
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                SelectFloor(1);
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                SelectFloor(2);
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                SelectFloor(3);
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            // Arrow keys to cycle floors
            else if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                CycleFloor(1);
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                CycleFloor(-1);
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
        /// Start floor targeting mode for a card
        /// </summary>
        /// <param name="card">The card being played</param>
        /// <param name="onConfirm">Called with selected floor (1-3) when confirmed</param>
        /// <param name="onCancel">Called when targeting is cancelled</param>
        public void StartTargeting(object card, Action<int> onConfirm, Action onCancel)
        {
            _pendingCard = card;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            IsTargeting = true;
            SelectedFloor = 1; // Default to bottom floor

            MonsterTrainAccessibility.LogInfo("Floor targeting started");
            AnnounceTargetingStart();
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
                MonsterTrainAccessibility.LogInfo("Floor targeting force cancelled");
            }
        }

        /// <summary>
        /// Select a specific floor
        /// </summary>
        private void SelectFloor(int floor)
        {
            if (floor < 1 || floor > 3)
                return;

            SelectedFloor = floor;
            AnnounceFloorSelection();
        }

        /// <summary>
        /// Cycle to the next/previous floor
        /// </summary>
        private void CycleFloor(int direction)
        {
            SelectedFloor += direction;
            if (SelectedFloor > 3) SelectedFloor = 1;
            if (SelectedFloor < 1) SelectedFloor = 3;

            AnnounceFloorSelection();
        }

        /// <summary>
        /// Confirm the current floor selection
        /// </summary>
        private void ConfirmSelection()
        {
            IsTargeting = false;
            var callback = _onConfirm;
            var floor = SelectedFloor;

            _pendingCard = null;
            _onConfirm = null;
            _onCancel = null;

            MonsterTrainAccessibility.ScreenReader?.Speak($"Playing on floor {floor}", false);
            MonsterTrainAccessibility.LogInfo($"Floor targeting confirmed: floor {floor}");

            callback?.Invoke(floor);
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

            MonsterTrainAccessibility.ScreenReader?.Speak("Card cancelled", false);
            MonsterTrainAccessibility.LogInfo("Floor targeting cancelled");

            callback?.Invoke();
        }

        /// <summary>
        /// Announce that targeting mode has started
        /// </summary>
        private void AnnounceTargetingStart()
        {
            string message = "Select floor. Use 1, 2, or 3 to select floor. Enter to confirm, Escape to cancel. ";
            message += GetFloorSummary(SelectedFloor);
            MonsterTrainAccessibility.ScreenReader?.Speak(message, false);
        }

        /// <summary>
        /// Announce the current floor selection
        /// </summary>
        private void AnnounceFloorSelection()
        {
            string message = $"Floor {SelectedFloor}. {GetFloorSummary(SelectedFloor)}";
            MonsterTrainAccessibility.ScreenReader?.Speak(message, false);
        }

        /// <summary>
        /// Get a summary of what's on a specific floor
        /// </summary>
        private string GetFloorSummary(int floor)
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle != null)
            {
                return battle.GetFloorSummary(floor);
            }
            return "";
        }
    }
}
