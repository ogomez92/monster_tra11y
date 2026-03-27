using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;
using System.Reflection;
using System.Text;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Detect story event screen and announce narrative text when choices/continue appear
    /// </summary>
    public static class StoryEventScreenPatch
    {
        private static FieldInfo _currentTextContentField;
        // Captured narrative text - set by prefixes BEFORE the method body clears it
        private static string _capturedNarrative;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("StoryEventScreen");
                if (targetType == null) return;

                // Patch Initialize for screen transition announcement
                var initMethod = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Show");
                if (initMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(StoryEventScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(initMethod, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched StoryEventScreen.{initMethod.Name}");
                }

                // Patch OnChoicesPresented - PREFIX to capture text, POSTFIX to speak it
                var choicesMethod = AccessTools.Method(targetType, "OnChoicesPresented");
                if (choicesMethod != null)
                {
                    var prefix = new HarmonyMethod(typeof(StoryEventScreenPatch).GetMethod(nameof(OnChoicesPresentedPrefix)));
                    var postfix = new HarmonyMethod(typeof(StoryEventScreenPatch).GetMethod(nameof(OnChoicesPresentedPostfix)));
                    harmony.Patch(choicesMethod, prefix: prefix, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched StoryEventScreen.OnChoicesPresented");
                }

                // Patch OnStoryFinished - PREFIX to capture text, POSTFIX to speak it
                var finishedMethod = AccessTools.Method(targetType, "OnStoryFinished");
                if (finishedMethod != null)
                {
                    var prefix = new HarmonyMethod(typeof(StoryEventScreenPatch).GetMethod(nameof(OnStoryFinishedPrefix)));
                    var postfix = new HarmonyMethod(typeof(StoryEventScreenPatch).GetMethod(nameof(OnStoryFinishedPostfix)));
                    harmony.Patch(finishedMethod, prefix: prefix, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched StoryEventScreen.OnStoryFinished");
                }

                // Cache reflection for currentTextContent StringBuilder field
                _currentTextContentField = targetType.GetField("currentTextContent",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch StoryEventScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                _capturedNarrative = null;
                ScreenStateTracker.SetScreen(Help.GameScreen.Event);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Event. Navigate choices with arrows. Enter to select. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in StoryEventScreen patch: {ex.Message}");
            }
        }

        /// <summary>
        /// PREFIX: Capture currentTextContent BEFORE OnChoicesPresented clears it via AppendTextContent.
        /// OnContentReady -> AdvanceStory() -> OnChoicesPresented all run synchronously,
        /// so by the time this prefix runs, currentTextContent has the full narrative text.
        /// </summary>
        public static void OnChoicesPresentedPrefix(object __instance)
        {
            try
            {
                _capturedNarrative = CaptureCurrentText(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in OnChoicesPresented prefix: {ex.Message}");
            }
        }

        /// <summary>
        /// PREFIX: Capture currentTextContent BEFORE OnStoryFinished processes it.
        /// </summary>
        public static void OnStoryFinishedPrefix(object __instance)
        {
            try
            {
                _capturedNarrative = CaptureCurrentText(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in OnStoryFinished prefix: {ex.Message}");
            }
        }

        /// <summary>
        /// Read currentTextContent StringBuilder before the method body clears it.
        /// </summary>
        private static string CaptureCurrentText(object instance)
        {
            if (_currentTextContentField == null) return null;

            var sb = _currentTextContentField.GetValue(instance) as StringBuilder;
            if (sb == null || sb.Length == 0) return null;

            string text = sb.ToString();
            text = Screens.BattleAccessibility.StripRichTextTags(text).Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        public static void OnChoicesPresentedPostfix(object __instance)
        {
            try
            {
                string narrative = _capturedNarrative;
                _capturedNarrative = null;

                if (!string.IsNullOrEmpty(narrative))
                {
                    MonsterTrainAccessibility.LogInfo($"Event narrative (choices): {narrative}");
                    MonsterTrainAccessibility.ScreenReader?.Speak(narrative);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in OnChoicesPresented patch: {ex.Message}");
            }
        }

        public static void OnStoryFinishedPostfix(object __instance)
        {
            try
            {
                string narrative = _capturedNarrative;
                _capturedNarrative = null;

                if (!string.IsNullOrEmpty(narrative))
                {
                    MonsterTrainAccessibility.LogInfo($"Event narrative (finished): {narrative}");
                    MonsterTrainAccessibility.ScreenReader?.Speak(narrative + ". Continue.");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in OnStoryFinished patch: {ex.Message}");
            }
        }
    }
}
