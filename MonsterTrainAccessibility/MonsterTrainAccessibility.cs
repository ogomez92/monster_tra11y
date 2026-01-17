using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MonsterTrainAccessibility.Core;
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

                // Initialize screen handlers
                MenuHandler = new MenuAccessibility();
                BattleHandler = new BattleAccessibility();
                DraftHandler = new CardDraftAccessibility();
                MapHandler = new MapAccessibility();

                // Apply Harmony patches
                _harmony = new Harmony(GUID);
                _harmony.PatchAll();

                Log.LogInfo($"{NAME} loaded successfully!");

                // Create the input handler GameObject (must be done after Awake)
                CreateInputHandler();
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to initialize {NAME}: {ex.Message}");
                Log.LogError(ex.StackTrace);
            }
        }

        private void CreateInputHandler()
        {
            // Create a persistent GameObject for input handling
            var inputHandlerGO = new GameObject("MonsterTrainAccessibility_InputHandler");
            DontDestroyOnLoad(inputHandlerGO);
            InputHandler = inputHandlerGO.AddComponent<InputInterceptor>();
        }

        private void OnDestroy()
        {
            // Clean up
            ScreenReader?.Shutdown();
            _harmony?.UnpatchSelf();

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
