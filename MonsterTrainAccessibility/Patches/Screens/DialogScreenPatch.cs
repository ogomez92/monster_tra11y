using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;
using System.Reflection;
using System.Text;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Detect dialog/popup screen
    /// </summary>
    public static class DialogScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("DialogScreen");
                if (targetType == null)
                {
                    targetType = AccessTools.TypeByName("DialogPopup");
                }

                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "ShowDialog") ??
                                 AccessTools.Method(targetType, "Show") ??
                                 AccessTools.Method(targetType, "Initialize");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(DialogScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched {targetType.Name}.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch DialogScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Dialog);
                AutoReadDialog(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in DialogScreen patch: {ex.Message}");
            }
        }

        private static void AutoReadDialog(object screen)
        {
            try
            {
                var screenType = screen.GetType();
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                var sb = new StringBuilder();
                sb.Append("Dialog. ");

                // Try to get dialog content text
                string[] textFieldNames = { "contentText", "messageText", "bodyText", "dialogText", "content", "_content", "message", "_message" };
                foreach (var fieldName in textFieldNames)
                {
                    var field = screenType.GetField(fieldName, bindingFlags);
                    if (field != null)
                    {
                        var value = field.GetValue(screen);
                        if (value != null)
                        {
                            // Could be a TMP_Text component or a string
                            string text = null;
                            if (value is string str)
                            {
                                text = str;
                            }
                            else
                            {
                                var textProp = value.GetType().GetProperty("text");
                                if (textProp != null)
                                {
                                    text = textProp.GetValue(value) as string;
                                }
                            }

                            if (!string.IsNullOrEmpty(text))
                            {
                                sb.Append(Screens.BattleAccessibility.StripRichTextTags(text));
                                sb.Append(" ");
                                break;
                            }
                        }
                    }
                }

                sb.Append("Press Enter to confirm or Escape to cancel.");
                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error auto-reading dialog: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Dialog. Press Enter to confirm or Escape to cancel.", false);
            }
        }
    }
}
