using MonsterTrainAccessibility.Battle;
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

        /// <summary>
        /// Currently browsed floor room index for Page Up/Down outside targeting mode
        /// </summary>
        private int _browsingFloor = 2; // Start at bottom (Floor 1)

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

            // Skip most input handling if floor or unit targeting is active
            // (Targeting systems handle their own input)
            if (FloorTargetingSystem.Instance?.IsTargeting == true ||
                UnitTargetingSystem.Instance?.IsTargeting == true)
            {
                // Only allow help key during targeting
                if (Input.GetKeyDown(config.HelpKey.Value))
                {
                    MonsterTrainAccessibility.HelpSystem?.ShowHelp();
                    _inputCooldown = INPUT_COOLDOWN_TIME;
                }
                return;
            }

            // Help key (F1) - always available
            if (Input.GetKeyDown(config.HelpKey.Value))
            {
                MonsterTrainAccessibility.HelpSystem?.ShowHelp();
                _inputCooldown = INPUT_COOLDOWN_TIME;
                return;
            }

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
                MonsterTrainAccessibility.LogInfo($"R key pressed - BattleHandler null: {MonsterTrainAccessibility.BattleHandler == null}, IsInBattle: {MonsterTrainAccessibility.BattleHandler?.IsInBattle}");
                ReadResources();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(config.ReadGoldKey.Value))
            {
                ReadGold();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(config.ToggleVerbosityKey.Value))
            {
                config.CycleVerbosity();
            }
            else if (Input.GetKeyDown(config.EndTurnKey.Value))
            {
                EndTurn();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(KeyCode.Tab))
            {
                // Skip train stats read when in Settings screen - Tab is used for tab navigation there
                if (Help.ScreenStateTracker.CurrentScreen != Help.GameScreen.Settings)
                {
                    MonsterTrainAccessibility.LogInfo("TAB key pressed - starting train stats read");
                    // When TAB is pressed, read the train stats panel after a short delay
                    // (to allow the panel to open first)
                    StartCoroutine(ReadTrainStatsDelayed());
                    _inputCooldown = INPUT_COOLDOWN_TIME;
                }
            }
            else if (Input.GetKeyDown(KeyCode.N))
            {
                // N is the game's native combat speed toggle
                // We don't block it - just announce the new speed after a short delay
                // (to allow the game to process the speed change first)
                StartCoroutine(AnnounceSpeedChangeDelayed());
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (Input.GetKeyDown(KeyCode.PageUp) || Input.GetKeyDown(KeyCode.PageDown))
            {
                var battle = MonsterTrainAccessibility.BattleHandler;
                if (battle != null && battle.IsInBattle)
                {
                    BrowseFloor(Input.GetKeyDown(KeyCode.PageUp));
                    _inputCooldown = INPUT_COOLDOWN_TIME;
                }
            }

        }

        /// <summary>
        /// Read train stats after a short delay (to allow panel to open)
        /// </summary>
        private System.Collections.IEnumerator ReadTrainStatsDelayed()
        {
            MonsterTrainAccessibility.LogInfo("ReadTrainStatsDelayed coroutine started");
            // Wait for the stats panel to open
            yield return new WaitForSecondsRealtime(0.3f);

            MonsterTrainAccessibility.LogInfo($"ReadTrainStatsDelayed: MenuHandler null: {MonsterTrainAccessibility.MenuHandler == null}");
            // Try to read the train stats panel
            MonsterTrainAccessibility.MenuHandler?.ReadTrainStatsPanel();
        }

        /// <summary>
        /// Announce the combat speed after N key is pressed (game toggles speed)
        /// </summary>
        private System.Collections.IEnumerator AnnounceSpeedChangeDelayed()
        {
            // Wait for the game to process the speed change
            yield return new WaitForSecondsRealtime(0.1f);

            string speedName = GetCurrentSpeedName();
            MonsterTrainAccessibility.ScreenReader?.Speak($"Speed: {speedName}", false);
        }

        private static System.Reflection.MethodInfo _getActiveGameSpeedMethod;
        private static bool _speedMethodSearched;

        /// <summary>
        /// Get the current game speed setting name
        /// </summary>
        private string GetCurrentSpeedName()
        {
            try
            {
                // Find SaveManager.GetActiveGameSpeed() via reflection (cached)
                if (!_speedMethodSearched)
                {
                    _speedMethodSearched = true;
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (!assembly.GetName().Name.Contains("Assembly-CSharp"))
                            continue;

                        var saveManagerType = assembly.GetType("SaveManager");
                        if (saveManagerType != null)
                        {
                            _getActiveGameSpeedMethod = saveManagerType.GetMethod("GetActiveGameSpeed",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            break;
                        }
                    }
                }

                if (_getActiveGameSpeedMethod != null)
                {
                    // Find the SaveManager instance in the scene
                    var saveManager = UnityEngine.Object.FindObjectOfType(_getActiveGameSpeedMethod.DeclaringType);
                    if (saveManager != null)
                    {
                        var speed = _getActiveGameSpeedMethod.Invoke(saveManager, null);
                        if (speed != null)
                        {
                            return FormatGameSpeed(speed);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting speed: {ex.Message}");
            }

            return "Changed";
        }

        /// <summary>
        /// Format a SaveManager.GameSpeed enum value into a readable name
        /// </summary>
        private string FormatGameSpeed(object value)
        {
            // The enum is: Normal=0, Fast=1, Ultra=2, SuperUltra=3, Instant=100
            string name = value.ToString();
            if (name == "SuperUltra") return "Super Ultra";
            return name;
        }

        /// <summary>
        /// End the player's turn
        /// </summary>
        private void EndTurn()
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle != null && battle.IsInBattle)
            {
                battle.EndTurn();
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Not in battle");
            }
        }

        /// <summary>
        /// Browse floors with Page Up/Down when not in targeting mode
        /// </summary>
        private void BrowseFloor(bool up)
        {
            // Room index 0 = top floor, 2 = bottom floor
            // Page Up = go up = decrease room index, Page Down = go down = increase room index
            if (up)
                _browsingFloor = Mathf.Max(0, _browsingFloor - 1);
            else
                _browsingFloor = Mathf.Min(2, _browsingFloor + 1);

            var battle = MonsterTrainAccessibility.BattleHandler;
            string floorName = Screens.BattleAccessibility.RoomIndexToFloorName(_browsingFloor);
            string summary = battle?.GetFloorSummary(_browsingFloor) ?? "";
            string message = string.IsNullOrEmpty(summary) ? floorName : $"{floorName}. {summary}";
            MonsterTrainAccessibility.ScreenReader?.Speak(message, false);
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
                MonsterTrainAccessibility.ScreenReader?.Queue("Not in battle");
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
                MonsterTrainAccessibility.ScreenReader?.Queue("Not in battle");
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
                MonsterTrainAccessibility.ScreenReader?.Queue("Not in battle");
            }
        }

        /// <summary>
        /// Read current resources (ember, pyre health, gold)
        /// </summary>
        private void ReadResources()
        {
            MonsterTrainAccessibility.LogInfo("ReadResources called");
            var battle = MonsterTrainAccessibility.BattleHandler;
            MonsterTrainAccessibility.LogInfo($"ReadResources: battle null: {battle == null}, IsInBattle: {battle?.IsInBattle}");
            if (battle != null && battle.IsInBattle)
            {
                MonsterTrainAccessibility.LogInfo("Calling AnnounceResources");
                battle.AnnounceResources();
            }
            else
            {
                MonsterTrainAccessibility.LogInfo("Not in battle, queueing message");
                // Could also read gold/other resources outside battle
                MonsterTrainAccessibility.ScreenReader?.Queue("Not in battle");
            }
        }

        /// <summary>
        /// Read current gold amount
        /// </summary>
        private void ReadGold()
        {
            int gold = GetCurrentGold();
            if (gold >= 0)
            {
                MonsterTrainAccessibility.ScreenReader?.Speak($"{gold} gold", false);
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Gold not available");
            }
        }

        /// <summary>
        /// Get the player's current gold from SaveManager
        /// </summary>
        public static int GetCurrentGold()
        {
            try
            {
                // Find SaveManager instance
                var saveManagerType = System.Type.GetType("SaveManager, Assembly-CSharp");
                if (saveManagerType == null)
                {
                    // Try finding it in loaded assemblies
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        saveManagerType = assembly.GetType("SaveManager");
                        if (saveManagerType != null) break;
                    }
                }

                if (saveManagerType != null)
                {
                    // Try to get instance
                    var instanceProp = saveManagerType.GetProperty("Instance",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    object saveManager = instanceProp?.GetValue(null);

                    if (saveManager == null)
                    {
                        // Try FindObjectOfType
                        var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                            null, new[] { typeof(System.Type) }, null);
                        if (findMethod != null)
                        {
                            saveManager = findMethod.Invoke(null, new object[] { saveManagerType });
                        }
                    }

                    if (saveManager != null)
                    {
                        // Try GetGold method
                        var getGoldMethod = saveManagerType.GetMethod("GetGold", System.Type.EmptyTypes);
                        if (getGoldMethod != null)
                        {
                            var result = getGoldMethod.Invoke(saveManager, null);
                            if (result is int gold)
                            {
                                return gold;
                            }
                        }

                        // Try gold field/property
                        var goldProp = saveManagerType.GetProperty("Gold") ??
                                      saveManagerType.GetProperty("CurrentGold");
                        if (goldProp != null)
                        {
                            var result = goldProp.GetValue(saveManager);
                            if (result is int gold)
                            {
                                return gold;
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting gold: {ex.Message}");
            }

            return -1;
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
