using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MonsterTrainAccessibility.Battle;
using MonsterTrainAccessibility.Core;
using MonsterTrainAccessibility.Help;
using MonsterTrainAccessibility.Help.Contexts;
using MonsterTrainAccessibility.Patches;
using MonsterTrainAccessibility.Screens;
using System;
using UnityEngine;

namespace MonsterTrainAccessibility
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInProcess("MonsterTrain.exe")]
    public class MonsterTrainAccessibility : BaseUnityPlugin
    {
        public const string GUID = "com.accessibility.monstertrain";
        public const string NAME = "Monster Train Accessibility";
        public const string VERSION = "1.0.0";

        // Static reference for global access
        public static MonsterTrainAccessibility Instance { get; private set; }

        // Logging
        internal static ManualLogSource Log { get; private set; }

        // Core modules
        public static ScreenReaderOutput ScreenReader { get; private set; }
        public static VirtualFocusManager FocusManager { get; private set; }
        public static InputInterceptor InputHandler { get; private set; }
        public static AccessibilityConfig AccessibilitySettings { get; private set; }

        // Help and targeting systems
        public static HelpSystem HelpSystem { get; private set; }
        public static FloorTargetingSystem FloorTargeting { get; private set; }
        public static UnitTargetingSystem UnitTargeting { get; private set; }

        // Screen-specific handlers
        public static MenuAccessibility MenuHandler { get; private set; }
        public static BattleAccessibility BattleHandler { get; private set; }
        public static CardDraftAccessibility DraftHandler { get; private set; }
        public static MapAccessibility MapHandler { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Log.LogInfo($"{NAME} v{VERSION} is loading...");

            try
            {
                // Initialize configuration first
                AccessibilitySettings = new AccessibilityConfig(Config);

                // Initialize screen reader output (Tolk)
                ScreenReader = new ScreenReaderOutput();
                ScreenReader.Initialize();

                // Initialize focus management
                FocusManager = new VirtualFocusManager();

                // Initialize help system
                HelpSystem = new HelpSystem();
                RegisterHelpContexts();

                // Initialize screen handlers (non-MonoBehaviour ones)
                BattleHandler = new BattleAccessibility();
                DraftHandler = new CardDraftAccessibility();
                MapHandler = new MapAccessibility();

                // Apply Harmony patches
                _harmony = new Harmony(GUID);
                ApplyPatches();

                Log.LogInfo($"{NAME} loaded successfully!");

                // Create the handler GameObjects (must be done after Awake)
                CreateHandlers();
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to initialize {NAME}: {ex.Message}");
                Log.LogError(ex.StackTrace);
            }
        }

        private void ApplyPatches()
        {
            // Screen transition patches
            MainMenuScreenPatch.TryPatch(_harmony);
            BattleIntroScreenPatch.TryPatch(_harmony);
            CombatStartPatch.TryPatch(_harmony);
            CardDraftScreenPatch.TryPatch(_harmony);
            ClassSelectionScreenPatch.TryPatch(_harmony);
            MapScreenPatch.TryPatch(_harmony);
            MerchantScreenPatch.TryPatch(_harmony);
            EnhancerSelectionScreenPatch.TryPatch(_harmony);
            GameOverScreenPatch.TryPatch(_harmony);
            SettingsScreenPatch.TryPatch(_harmony);
            ScreenManagerPatch.TryPatch(_harmony);

            // Combat event patches
            PlayerTurnStartPatch.TryPatch(_harmony);
            PlayerTurnEndPatch.TryPatch(_harmony);
            DamageAppliedPatch.TryPatch(_harmony);
            UnitDeathPatch.TryPatch(_harmony);
            StatusEffectPatch.TryPatch(_harmony);
            BattleVictoryPatch.TryPatch(_harmony);
            UnitSpawnPatch.TryPatch(_harmony);
            EnemyAscendPatch.TryPatch(_harmony);
            PyreDamagePatch.TryPatch(_harmony);
            EnemyDialoguePatch.TryPatch(_harmony);
            CombatPhasePatch.TryPatch(_harmony);

            // Card event patches
            CardDrawPatch.TryPatch(_harmony);
            CardPlayedPatch.TryPatch(_harmony);
            CardDiscardedPatch.TryPatch(_harmony);
            DeckShuffledPatch.TryPatch(_harmony);
            HandChangedPatch.TryPatch(_harmony);
        }

        private void CreateHandlers()
        {
            // Create a persistent GameObject for all MonoBehaviour handlers
            var handlerGO = new GameObject("MonsterTrainAccessibility_Handlers");
            DontDestroyOnLoad(handlerGO);

            InputHandler = handlerGO.AddComponent<InputInterceptor>();
            MenuHandler = handlerGO.AddComponent<MenuAccessibility>();
            FloorTargeting = handlerGO.AddComponent<FloorTargetingSystem>();
            UnitTargeting = handlerGO.AddComponent<UnitTargetingSystem>();
        }

        /// <summary>
        /// Register all help contexts with the help system
        /// </summary>
        private void RegisterHelpContexts()
        {
            HelpSystem.RegisterContexts(
                new GlobalHelp(),           // Priority 0 - fallback
                new MainMenuHelp(),         // Priority 40
                new ClanSelectionHelp(),    // Priority 50
                new MapHelp(),              // Priority 60
                new ShopHelp(),             // Priority 70
                new EventHelp(),            // Priority 70
                new CardDraftHelp(),        // Priority 80
                new BattleIntroHelp(),      // Priority 85 - pre-battle screen
                new BattleHelp(),           // Priority 90
                new TutorialHelp(),         // Priority 95 - tutorial popups
                new BattleTargetingHelp(),  // Priority 100 - floor targeting
                new UnitTargetingHelp()     // Priority 101 - unit targeting (highest)
            );
            Log.LogInfo("Registered help contexts");
        }

        private void OnDestroy()
        {
            // Clean up
            ScreenReader?.Shutdown();
            _harmony?.UnpatchSelf();

            // Destroy the handler GameObject (contains both InputHandler and MenuHandler)
            if (InputHandler != null && InputHandler.gameObject != null)
            {
                Destroy(InputHandler.gameObject);
            }
        }

        // Utility method for other classes to log
        public static void LogInfo(string message)
        {
            Log?.LogInfo(message);
        }

        public static void LogWarning(string message)
        {
            Log?.LogWarning(message);
        }

        public static void LogError(string message)
        {
            Log?.LogError(message);
        }
    }
}
