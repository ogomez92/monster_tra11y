using System;
using DavyKager;

namespace MonsterTrainAccessibility.Core
{
    /// <summary>
    /// Wrapper for the Tolk screen reader library.
    /// Provides speech output, braille support, and manages screen reader communication.
    /// </summary>
    public class ScreenReaderOutput
    {
        private bool _initialized;
        private string _detectedScreenReader;

        /// <summary>
        /// Whether the screen reader output system is ready
        /// </summary>
        public bool IsReady => _initialized;

        /// <summary>
        /// The name of the detected screen reader, or null if none
        /// </summary>
        public string DetectedScreenReader => _detectedScreenReader;

        /// <summary>
        /// Initialize the Tolk library and detect screen readers
        /// </summary>
        public void Initialize()
        {
            try
            {
                // Enable SAPI fallback so there's always voice output
                Tolk.TrySAPI(true);

                // Load the Tolk library
                Tolk.Load();
                _initialized = Tolk.IsLoaded();

                if (_initialized)
                {
                    _detectedScreenReader = Tolk.DetectScreenReader();

                    if (!string.IsNullOrEmpty(_detectedScreenReader))
                    {
                        MonsterTrainAccessibility.LogInfo($"Screen reader detected: {_detectedScreenReader}");

                        // Announce successful load
                        Speak($"Monster Train Accessibility loaded. Using {_detectedScreenReader}.", true);
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("No screen reader detected, using SAPI fallback");
                        Speak("Monster Train Accessibility loaded. Using Windows speech.", true);
                    }

                    // Log capabilities
                    MonsterTrainAccessibility.LogInfo($"Speech available: {Tolk.HasSpeech()}");
                    MonsterTrainAccessibility.LogInfo($"Braille available: {Tolk.HasBraille()}");
                }
                else
                {
                    MonsterTrainAccessibility.LogError("Tolk failed to load - screen reader output unavailable");
                }
            }
            catch (DllNotFoundException ex)
            {
                MonsterTrainAccessibility.LogError($"Tolk.dll not found: {ex.Message}");
                MonsterTrainAccessibility.LogError("Make sure Tolk.dll is in the plugins folder");
                _initialized = false;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to initialize Tolk: {ex.Message}");
                _initialized = false;
            }
        }

        /// <summary>
        /// Speak text through the screen reader.
        /// </summary>
        /// <param name="text">Text to speak</param>
        /// <param name="interrupt">If true, stops current speech and speaks immediately</param>
        public void Speak(string text, bool interrupt = false)
        {
            if (!_initialized || string.IsNullOrEmpty(text))
                return;

            try
            {
                // Apply verbosity filtering
                text = ApplyVerbosityFilter(text);
                if (string.IsNullOrEmpty(text))
                    return;

                Tolk.Speak(text, interrupt);

                // Also output to braille if available and enabled
                if (MonsterTrainAccessibility.AccessibilitySettings.EnableBraille.Value && Tolk.HasBraille())
                {
                    Tolk.Braille(text);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error speaking text: {ex.Message}");
            }
        }

        /// <summary>
        /// Queue speech (non-interrupting)
        /// </summary>
        public void Queue(string text)
        {
            Speak(text, false);
        }

        /// <summary>
        /// Output to both speech and braille simultaneously
        /// </summary>
        public void Output(string text, bool interrupt = false)
        {
            if (!_initialized || string.IsNullOrEmpty(text))
                return;

            try
            {
                text = ApplyVerbosityFilter(text);
                if (string.IsNullOrEmpty(text))
                    return;

                Tolk.Output(text, interrupt);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error outputting text: {ex.Message}");
            }
        }

        /// <summary>
        /// Send text to braille display only
        /// </summary>
        public void Braille(string text)
        {
            if (!_initialized || string.IsNullOrEmpty(text) || !Tolk.HasBraille())
                return;

            try
            {
                Tolk.Braille(text);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error outputting to braille: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop current speech immediately
        /// </summary>
        public void Silence()
        {
            if (!_initialized)
                return;

            try
            {
                Tolk.Silence();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error silencing: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if currently speaking
        /// </summary>
        public bool IsSpeaking()
        {
            if (!_initialized)
                return false;

            try
            {
                return Tolk.IsSpeaking();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Announce a screen transition (interrupts current speech)
        /// </summary>
        public void AnnounceScreen(string screenName)
        {
            Speak(screenName, true);
        }

        /// <summary>
        /// Announce the currently focused item (interrupts current speech)
        /// </summary>
        public void AnnounceFocus(string itemDescription)
        {
            bool shouldInterrupt = MonsterTrainAccessibility.AccessibilitySettings.InterruptOnFocusChange.Value;
            Speak(itemDescription, shouldInterrupt);
        }

        /// <summary>
        /// Announce a game event
        /// </summary>
        public void AnnounceEvent(string eventDescription, bool interrupt = false)
        {
            Speak(eventDescription, interrupt);
        }

        /// <summary>
        /// Announce with position info (e.g., "Card 2 of 5: Torch")
        /// </summary>
        public void AnnounceWithPosition(string itemName, int position, int total)
        {
            string announcement = $"{itemName}, {position} of {total}";
            AnnounceFocus(announcement);
        }

        /// <summary>
        /// Apply verbosity filtering based on user settings
        /// </summary>
        private string ApplyVerbosityFilter(string text)
        {
            // For now, return text as-is
            // In the future, we can filter based on verbosity level
            var verbosity = MonsterTrainAccessibility.AccessibilitySettings.VerbosityLevel.Value;

            switch (verbosity)
            {
                case VerbosityLevel.Minimal:
                    // Could strip out flavor text, detailed descriptions, etc.
                    return text;

                case VerbosityLevel.Normal:
                    return text;

                case VerbosityLevel.Verbose:
                    return text;

                default:
                    return text;
            }
        }

        /// <summary>
        /// Shutdown the Tolk library
        /// </summary>
        public void Shutdown()
        {
            if (_initialized)
            {
                try
                {
                    Silence();
                    Tolk.Unload();
                    _initialized = false;
                    MonsterTrainAccessibility.LogInfo("Screen reader output shutdown complete");
                }
                catch (Exception ex)
                {
                    MonsterTrainAccessibility.LogError($"Error shutting down Tolk: {ex.Message}");
                }
            }
        }
    }
}
