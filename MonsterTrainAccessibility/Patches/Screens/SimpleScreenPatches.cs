using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Detect settings screen
    /// </summary>
    public static class SettingsScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("SettingsScreen");
                if (targetType == null)
                {
                    targetType = AccessTools.TypeByName("OptionsScreen");
                }

                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Show") ??
                                 AccessTools.Method(targetType, "Open");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(SettingsScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched {targetType.Name}.{method.Name}");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("SettingsScreen methods not found");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch SettingsScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Settings. Press Tab to switch between tabs.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in SettingsScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect when compendium/logbook screen is shown
    /// </summary>
    public static class CompendiumScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CompendiumScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(CompendiumScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched CompendiumScreen.Initialize");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CompendiumScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Compendium);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Logbook. Use Page Up and Page Down to switch sections. Left and Right arrows to turn pages.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CompendiumScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Announce section changes in the compendium/logbook (PageUp/PageDown)
    /// Patches CompendiumScreen.SetSection(Section) to read the new section name.
    /// </summary>
    public static class CompendiumSectionPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = HarmonyLib.AccessTools.TypeByName("CompendiumScreen");
                if (targetType != null)
                {
                    var method = HarmonyLib.AccessTools.Method(targetType, "SetSection");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(CompendiumSectionPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched CompendiumScreen.SetSection");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CompendiumScreen.SetSection: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                // Read the currentSection field to get the active section
                var screenType = __instance.GetType();
                var sectionField = screenType.GetField("currentSection",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (sectionField != null)
                {
                    var section = sectionField.GetValue(__instance);
                    string sectionName = SectionToName(section?.ToString());
                    if (!string.IsNullOrEmpty(sectionName))
                    {
                        // Try to get page info from paginated sections
                        string pageInfo = GetPageInfo(__instance, screenType);
                        string message = string.IsNullOrEmpty(pageInfo)
                            ? sectionName
                            : $"{sectionName}. {pageInfo}";
                        MonsterTrainAccessibility.ScreenReader?.Speak(message, false);
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CompendiumScreen.SetSection patch: {ex.Message}");
            }
        }

        internal static string SectionToName(string section)
        {
            if (string.IsNullOrEmpty(section) || section == "NONE") return null;
            switch (section)
            {
                case "Checklist": return "Checklist";
                case "Cards": return "Cards";
                case "ChampUpgrades": return "Champion Upgrades";
                case "Blessings": return "Artifacts";
                case "CardFrames": return "Card Frames";
                case "Stats": return "Statistics";
                default: return section;
            }
        }

        internal static string GetPageInfo(object screen, Type screenType)
        {
            try
            {
                // Get the current section UI to read page count
                var getCurrentSectionMethod = screenType.GetMethod("GetCurrentSectionUI",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (getCurrentSectionMethod == null) return null;

                var sectionUI = getCurrentSectionMethod.Invoke(screen, null);
                if (sectionUI == null) return null;

                // Check if it's a PaginatedCompendiumSection
                var sectionUIType = sectionUI.GetType();
                var currentPageField = sectionUIType.GetField("currentPage",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var pagesField = sectionUIType.GetField("pages",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (currentPageField != null && pagesField != null)
                {
                    var currentPage = currentPageField.GetValue(sectionUI);
                    var pages = pagesField.GetValue(sectionUI);

                    if (currentPage is int page && pages is System.Collections.IList pageList && pageList.Count > 1)
                    {
                        return $"Page {page + 1} of {pageList.Count}";
                    }
                }
            }
            catch { }
            return null;
        }
    }

    /// <summary>
    /// Announce page turns in the compendium/logbook (Left/Right arrows)
    /// Patches CompendiumSection.TurnPage(int) to read the new page number.
    /// </summary>
    public static class CompendiumPageTurnPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Patch the base CompendiumSection.TurnPage or PaginatedCompendiumSection.TurnPage
                var targetType = HarmonyLib.AccessTools.TypeByName("PaginatedCompendiumSection");
                if (targetType == null)
                    targetType = HarmonyLib.AccessTools.TypeByName("CompendiumSection");
                if (targetType != null)
                {
                    var method = HarmonyLib.AccessTools.Method(targetType, "TurnPage");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(CompendiumPageTurnPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched {targetType.Name}.TurnPage");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CompendiumSection.TurnPage: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                var sectionType = __instance.GetType();

                // Read current page index and total pages
                var currentPageField = sectionType.GetField("currentPage",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var pagesField = sectionType.GetField("pages",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (currentPageField != null && pagesField != null)
                {
                    var currentPage = currentPageField.GetValue(__instance);
                    var pages = pagesField.GetValue(__instance);

                    if (currentPage is int page && pages is System.Collections.IList pageList)
                    {
                        string message = pageList.Count > 1
                            ? $"Page {page + 1} of {pageList.Count}"
                            : $"Page {page + 1}";
                        MonsterTrainAccessibility.ScreenReader?.Speak(message, false);
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CompendiumSection.TurnPage patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Generic screen manager patch to catch all screen transitions
    /// </summary>
    public static class ScreenManagerPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("ScreenManager");
                if (targetType != null)
                {
                    // Try to find the method that handles screen changes
                    var method = AccessTools.Method(targetType, "ChangeScreen") ??
                                 AccessTools.Method(targetType, "LoadScreen") ??
                                 AccessTools.Method(targetType, "ShowScreen");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(ScreenManagerPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched ScreenManager.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch ScreenManager: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                // Could log screen transitions for debugging
                MonsterTrainAccessibility.LogInfo("Screen transition detected");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ScreenManager patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect run history screen
    /// </summary>
    public static class RunHistoryScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetNames = new[] { "RunHistoryScreen", "RunLogScreen", "PastRunsScreen" };
                foreach (var name in targetNames)
                {
                    var targetType = AccessTools.TypeByName(name);
                    if (targetType != null)
                    {
                        var method = AccessTools.Method(targetType, "Setup") ??
                                     AccessTools.Method(targetType, "Show");
                        if (method != null)
                        {
                            var postfix = new HarmonyMethod(typeof(RunHistoryScreenPatch).GetMethod(nameof(Postfix)));
                            harmony.Patch(method, postfix: postfix);
                            MonsterTrainAccessibility.LogInfo($"Patched {name}.{method.Name}");
                            return;
                        }
                    }
                }
                MonsterTrainAccessibility.LogInfo("RunHistoryScreen not found");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch RunHistoryScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.RunHistory);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Run History. Use arrows to browse runs.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RunHistoryScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect challenge details screen
    /// </summary>
    public static class ChallengeDetailsScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("ChallengeDetailsScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(ChallengeDetailsScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched ChallengeDetailsScreen.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch ChallengeDetailsScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.ChallengeDetails);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Challenge Details.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ChallengeDetailsScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect challenge overview screen
    /// </summary>
    public static class ChallengeOverviewScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("ChallengeOverviewScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(ChallengeOverviewScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched ChallengeOverviewScreen.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch ChallengeOverviewScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.ChallengeOverview);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Challenges.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ChallengeOverviewScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect minimap screen
    /// </summary>
    public static class MinimapScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("MinimapScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(MinimapScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched MinimapScreen.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch MinimapScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Minimap);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Minimap.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in MinimapScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect credits screen
    /// </summary>
    public static class CreditsScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CreditsScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(CreditsScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched CreditsScreen.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CreditsScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Credits);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Credits. Press Escape to return.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CreditsScreen patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect key mapping screen
    /// </summary>
    public static class KeyMappingScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetNames = new[] { "KeyMappingScreen", "KeyBindingsScreen", "ControlsScreen", "InputMappingScreen" };
                foreach (var name in targetNames)
                {
                    var targetType = AccessTools.TypeByName(name);
                    if (targetType != null)
                    {
                        var method = AccessTools.Method(targetType, "Initialize") ??
                                     AccessTools.Method(targetType, "Show") ??
                                     AccessTools.Method(targetType, "Setup");
                        if (method != null)
                        {
                            var postfix = new HarmonyMethod(typeof(KeyMappingScreenPatch).GetMethod(nameof(Postfix)));
                            harmony.Patch(method, postfix: postfix);
                            MonsterTrainAccessibility.LogInfo($"Patched {name}.{method.Name}");
                            return;
                        }
                    }
                }
                MonsterTrainAccessibility.LogInfo("KeyMappingScreen not found");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch KeyMappingScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.KeyMapping);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Key Mapping. Use arrows to navigate.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in KeyMappingScreen patch: {ex.Message}");
            }
        }
    }
}
