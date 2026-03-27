using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;
using System.Reflection;
using System.Text;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Detect artifact/relic draft screen
    /// </summary>
    public static class RelicDraftScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("RelicDraftScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(RelicDraftScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched RelicDraftScreen.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch RelicDraftScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.RelicDraft);
                AutoReadRelicDraft(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RelicDraftScreen patch: {ex.Message}");
            }
        }

        private static void AutoReadRelicDraft(object screen)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("Artifact Draft. ");

                var screenType = screen.GetType();
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Try to get relic choices
                var relicsField = screenType.GetField("relicChoices", bindingFlags) ??
                                  screenType.GetField("relics", bindingFlags) ??
                                  screenType.GetField("_relics", bindingFlags) ??
                                  screenType.GetField("draftRelics", bindingFlags);

                if (relicsField != null)
                {
                    var relics = relicsField.GetValue(screen) as System.Collections.IList;
                    if (relics != null && relics.Count > 0)
                    {
                        sb.Append($"{relics.Count} artifacts: ");
                        foreach (var relic in relics)
                        {
                            string name = GetRelicName(relic);
                            if (!string.IsNullOrEmpty(name))
                            {
                                sb.Append($"{name}, ");
                            }
                        }
                    }
                }

                sb.Append("Left and Right to browse. Enter to select. Escape to skip.");
                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error auto-reading relic draft: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Artifact Draft. Left and Right to browse. Enter to select.", false);
            }
        }

        private static string GetRelicName(object relic)
        {
            if (relic == null) return null;
            try
            {
                var relicType = relic.GetType();
                var getNameMethod = relicType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    return getNameMethod.Invoke(relic, null) as string;
                }

                // Try name field
                var nameField = relicType.GetField("name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return nameField?.GetValue(relic) as string;
            }
            catch { return null; }
        }
    }
}
