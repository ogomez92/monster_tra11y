using BepInEx.Configuration;
using UnityEngine;

namespace MonsterTrainAccessibility.Core
{
    /// <summary>
    /// Configuration options for the accessibility mod
    /// </summary>
    public class AccessibilityConfig
    {
        // Speech settings
        public ConfigEntry<VerbosityLevel> VerbosityLevel { get; private set; }
        public ConfigEntry<bool> UseSAPIFallback { get; private set; }
        public ConfigEntry<bool> EnableBraille { get; private set; }

        // Navigation keys
        public ConfigEntry<KeyCode> NavigateUpKey { get; private set; }
        public ConfigEntry<KeyCode> NavigateDownKey { get; private set; }
        public ConfigEntry<KeyCode> NavigateLeftKey { get; private set; }
        public ConfigEntry<KeyCode> NavigateRightKey { get; private set; }
        public ConfigEntry<KeyCode> ActivateKey { get; private set; }
        public ConfigEntry<KeyCode> BackKey { get; private set; }
        public ConfigEntry<KeyCode> AlternateActivateKey { get; private set; }

        // Information hotkeys
        public ConfigEntry<KeyCode> ReadCurrentKey { get; private set; }
        public ConfigEntry<KeyCode> ReadTextKey { get; private set; }
        public ConfigEntry<KeyCode> ReadHandKey { get; private set; }
        public ConfigEntry<KeyCode> ReadFloorsKey { get; private set; }
        public ConfigEntry<KeyCode> ReadEnemiesKey { get; private set; }
        public ConfigEntry<KeyCode> ReadResourcesKey { get; private set; }
        public ConfigEntry<KeyCode> ToggleVerbosityKey { get; private set; }

        // Announcement preferences
        public ConfigEntry<bool> AnnounceCardDraws { get; private set; }
        public ConfigEntry<bool> AnnounceStatusEffects { get; private set; }
        public ConfigEntry<bool> AnnounceDamage { get; private set; }
        public ConfigEntry<bool> AnnounceDeaths { get; private set; }
        public ConfigEntry<bool> InterruptOnFocusChange { get; private set; }

        public AccessibilityConfig(ConfigFile config)
        {
            // ========== Speech Settings ==========
            VerbosityLevel = config.Bind(
                "Speech",
                "VerbosityLevel",
                Core.VerbosityLevel.Normal,
                "How much detail to include in announcements.\n" +
                "Minimal = Names and numbers only\n" +
                "Normal = Standard descriptions\n" +
                "Verbose = Full details including flavor text"
            );

            UseSAPIFallback = config.Bind(
                "Speech",
                "UseSAPIFallback",
                true,
                "Use Windows SAPI (Microsoft Speech) if no screen reader is detected"
            );

            EnableBraille = config.Bind(
                "Speech",
                "EnableBraille",
                true,
                "Send text to braille display if available"
            );

            // ========== Navigation Keys ==========
            NavigateUpKey = config.Bind(
                "Keys.Navigation",
                "NavigateUp",
                KeyCode.UpArrow,
                "Key to navigate up"
            );

            NavigateDownKey = config.Bind(
                "Keys.Navigation",
                "NavigateDown",
                KeyCode.DownArrow,
                "Key to navigate down"
            );

            NavigateLeftKey = config.Bind(
                "Keys.Navigation",
                "NavigateLeft",
                KeyCode.LeftArrow,
                "Key to navigate left"
            );

            NavigateRightKey = config.Bind(
                "Keys.Navigation",
                "NavigateRight",
                KeyCode.RightArrow,
                "Key to navigate right"
            );

            ActivateKey = config.Bind(
                "Keys.Navigation",
                "Activate",
                KeyCode.Return,
                "Key to activate/select the current item"
            );

            AlternateActivateKey = config.Bind(
                "Keys.Navigation",
                "AlternateActivate",
                KeyCode.Space,
                "Alternate key to activate/select (useful for some users)"
            );

            BackKey = config.Bind(
                "Keys.Navigation",
                "Back",
                KeyCode.Escape,
                "Key to go back or cancel"
            );

            // ========== Information Hotkeys ==========
            ReadCurrentKey = config.Bind(
                "Keys.Information",
                "ReadCurrent",
                KeyCode.C,
                "Key to re-read the currently focused item"
            );

            ReadTextKey = config.Bind(
                "Keys.Information",
                "ReadText",
                KeyCode.T,
                "Key to read all text content on screen (patch notes, descriptions, etc.)"
            );

            ReadHandKey = config.Bind(
                "Keys.Information",
                "ReadHand",
                KeyCode.H,
                "Key to read all cards in hand"
            );

            ReadFloorsKey = config.Bind(
                "Keys.Information",
                "ReadFloors",
                KeyCode.F,
                "Key to read all floor information"
            );

            ReadEnemiesKey = config.Bind(
                "Keys.Information",
                "ReadEnemies",
                KeyCode.E,
                "Key to read enemy information and intents"
            );

            ReadResourcesKey = config.Bind(
                "Keys.Information",
                "ReadResources",
                KeyCode.R,
                "Key to read ember, gold, and pyre health"
            );

            ToggleVerbosityKey = config.Bind(
                "Keys.Information",
                "ToggleVerbosity",
                KeyCode.V,
                "Key to cycle through verbosity levels"
            );

            // ========== Announcement Preferences ==========
            AnnounceCardDraws = config.Bind(
                "Announcements",
                "CardDraws",
                true,
                "Announce when cards are drawn"
            );

            AnnounceStatusEffects = config.Bind(
                "Announcements",
                "StatusEffects",
                true,
                "Announce when status effects are applied"
            );

            AnnounceDamage = config.Bind(
                "Announcements",
                "Damage",
                true,
                "Announce damage dealt during combat"
            );

            AnnounceDeaths = config.Bind(
                "Announcements",
                "Deaths",
                true,
                "Announce when units die"
            );

            InterruptOnFocusChange = config.Bind(
                "Announcements",
                "InterruptOnFocusChange",
                true,
                "Stop current speech when focus changes to a new item"
            );
        }

        /// <summary>
        /// Cycle to the next verbosity level
        /// </summary>
        public void CycleVerbosity()
        {
            var current = VerbosityLevel.Value;
            VerbosityLevel.Value = current switch
            {
                Core.VerbosityLevel.Minimal => Core.VerbosityLevel.Normal,
                Core.VerbosityLevel.Normal => Core.VerbosityLevel.Verbose,
                Core.VerbosityLevel.Verbose => Core.VerbosityLevel.Minimal,
                _ => Core.VerbosityLevel.Normal
            };

            MonsterTrainAccessibility.ScreenReader?.Speak($"Verbosity: {VerbosityLevel.Value}", true);
        }
    }

    /// <summary>
    /// How verbose the accessibility announcements should be
    /// </summary>
    public enum VerbosityLevel
    {
        /// <summary>Names and essential numbers only</summary>
        Minimal,

        /// <summary>Standard descriptions with key information</summary>
        Normal,

        /// <summary>Full details including flavor text and extended info</summary>
        Verbose
    }
}
