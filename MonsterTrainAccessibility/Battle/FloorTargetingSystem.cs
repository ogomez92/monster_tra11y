using System;
using UnityEngine;

namespace MonsterTrainAccessibility.Battle
{
    /// <summary>
    /// Handles keyboard-based floor targeting for playing cards.
    /// When a card requires floor selection, this system allows the player
    /// to select a floor using Page Up/Down instead of mouse.
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
        /// Currently selected room index (0=bottom, 1=middle, 2=top)
        /// </summary>
        public int SelectedFloor { get; private set; } = 0;

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

            // Page Up/Down to cycle floors (matches game's native floor navigation)
            // After the key is pressed, read the floor from game state instead of assuming
            if (Input.GetKeyDown(KeyCode.PageUp) || Input.GetKeyDown(KeyCode.PageDown))
            {
                // Let the game process the key, then read the actual floor
                ReadFloorFromGameAndAnnounce();
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
        /// <param name="onConfirm">Called with selected room index when confirmed</param>
        /// <param name="onCancel">Called when targeting is cancelled</param>
        public void StartTargeting(object card, Action<int> onConfirm, Action onCancel)
        {
            _pendingCard = card;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            IsTargeting = true;

            // Try to read the selected floor from game state to stay in sync
            var battleHandler = MonsterTrainAccessibility.BattleHandler;
            if (battleHandler != null)
            {
                int gameFloor = battleHandler.GetSelectedFloor();
                if (gameFloor >= 0 && gameFloor <= 3)
                {
                    SelectedFloor = gameFloor;
                    MonsterTrainAccessibility.LogInfo($"Floor targeting started - synced to room index {gameFloor}");
                }
                else
                {
                    SelectedFloor = 0;
                    MonsterTrainAccessibility.LogInfo($"Floor targeting started - couldn't read game floor ({gameFloor}), defaulting to bottom");
                }
            }
            else
            {
                SelectedFloor = 0;
                MonsterTrainAccessibility.LogInfo($"Floor targeting started - no battle handler, defaulting to bottom");
            }

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
        /// Read the current floor from game state and announce it.
        /// This is called after Page Up/Down to stay in sync with the game.
        /// </summary>
        private void ReadFloorFromGameAndAnnounce()
        {
            // Use a coroutine with a tiny delay to let the game process the key first
            StartCoroutine(ReadFloorAfterDelay());
        }

        private System.Collections.IEnumerator ReadFloorAfterDelay()
        {
            // Wait one frame for the game to process the key
            yield return null;

            var battleHandler = MonsterTrainAccessibility.BattleHandler;
            if (battleHandler != null)
            {
                int gameFloor = battleHandler.GetSelectedFloor();
                if (gameFloor >= 0 && gameFloor <= 3)
                {
                    SelectedFloor = gameFloor;
                    MonsterTrainAccessibility.LogInfo($"ReadFloorFromGame: room index is {gameFloor}");

                    // Always announce the current floor from game state
                    AnnounceFloorSelection();
                }
                else
                {
                    // Couldn't read floor
                    MonsterTrainAccessibility.LogInfo($"ReadFloorFromGame: invalid floor {gameFloor}");
                }
            }
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

            string floorName = Screens.BattleAccessibility.RoomIndexToFloorName(floor);
            MonsterTrainAccessibility.ScreenReader?.Speak($"Playing on {floorName.ToLower()}", false);
            MonsterTrainAccessibility.LogInfo($"Floor targeting confirmed: room index {floor}");

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
            string floorName = Screens.BattleAccessibility.RoomIndexToFloorName(SelectedFloor);
            string summary = GetFloorSummary(SelectedFloor);
            string floorInfo = string.IsNullOrEmpty(summary) ? floorName : $"{floorName}. {summary}";
            string message = $"Select floor. Page Up/Down to change. Enter to confirm, Escape to cancel. {floorInfo}";
            MonsterTrainAccessibility.ScreenReader?.Speak(message, false);
        }

        /// <summary>
        /// Announce the current floor selection
        /// </summary>
        private void AnnounceFloorSelection()
        {
            string floorName = Screens.BattleAccessibility.RoomIndexToFloorName(SelectedFloor);
            string summary = GetFloorSummary(SelectedFloor);
            string message = string.IsNullOrEmpty(summary) ? floorName : $"{floorName}. {summary}";
            MonsterTrainAccessibility.ScreenReader?.Speak(message, false);
        }

        /// <summary>
        /// Get a summary of what's on a specific floor
        /// </summary>
        private string GetFloorSummary(int roomIndex)
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle != null)
            {
                return battle.GetFloorSummary(roomIndex);
            }
            return "";
        }
    }
}
