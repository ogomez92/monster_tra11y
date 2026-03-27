using HarmonyLib;
using MonsterTrainAccessibility.Core;
using MonsterTrainAccessibility.Help;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Detect champion upgrade screen
    /// </summary>
    public static class ChampionUpgradeScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("ChampionUpgradeScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(ChampionUpgradeScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched ChampionUpgradeScreen.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch ChampionUpgradeScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.ChampionUpgrade);
                AutoReadChampionUpgrade(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ChampionUpgradeScreen patch: {ex.Message}");
            }
        }

        private static void AutoReadChampionUpgrade(object screen)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("Champion Upgrade. ");

                var screenType = screen.GetType();
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Try to get upgrade tree data
                var treeField = screenType.GetField("upgradeTree", bindingFlags) ??
                                screenType.GetField("upgradeTreeData", bindingFlags) ??
                                screenType.GetField("_upgradeTreeData", bindingFlags);

                if (treeField != null)
                {
                    var treeData = treeField.GetValue(screen);
                    if (treeData != null)
                    {
                        // Try to get champion name
                        var getNameMethod = treeData.GetType().GetMethod("GetName", Type.EmptyTypes);
                        if (getNameMethod != null)
                        {
                            string name = getNameMethod.Invoke(treeData, null) as string;
                            if (!string.IsNullOrEmpty(name))
                            {
                                sb.Append($"Champion: {name}. ");
                            }
                        }
                    }
                }

                // Try to get upgrade choices count
                var choicesField = screenType.GetField("upgradeChoices", bindingFlags) ??
                                   screenType.GetField("choices", bindingFlags) ??
                                   screenType.GetField("_choices", bindingFlags);

                if (choicesField != null)
                {
                    var choices = choicesField.GetValue(screen) as System.Collections.IList;
                    if (choices != null)
                    {
                        sb.Append($"{choices.Count} upgrade paths available. ");
                    }
                }

                sb.Append("Left and Right to browse. Enter to select. Press F1 for help.");
                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error auto-reading champion upgrade: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Champion Upgrade. Left and Right to browse. Enter to select.", false);
            }
        }
    }

    /// <summary>
    /// Detect unit synthesis screen (DLC)
    /// </summary>
    public static class SynthesisScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("SynthesisScreen");
                if (targetType == null)
                {
                    // DLC type may not be present
                    MonsterTrainAccessibility.LogInfo("SynthesisScreen not found (DLC may not be installed)");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");
                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(SynthesisScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched SynthesisScreen.{method.Name}");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch SynthesisScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Synthesis);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Unit Synthesis. Arrow keys to browse units. Enter to select. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in SynthesisScreen patch: {ex.Message}");
            }
        }
    }
}
