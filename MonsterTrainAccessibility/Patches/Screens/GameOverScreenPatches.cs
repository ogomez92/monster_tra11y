using HarmonyLib;
using System;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Detect game over / run summary screen (victory or defeat)
    /// </summary>
    public static class GameOverScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try different possible class names for the game over screen
                var targetNames = new[] {
                    "GameOverScreen",
                    "RunSummaryScreen",
                    "VictoryScreen",
                    "DefeatScreen",
                    "RunEndScreen",
                    "EndRunScreen"
                };

                foreach (var name in targetNames)
                {
                    var targetType = AccessTools.TypeByName(name);
                    if (targetType != null)
                    {
                        var method = AccessTools.Method(targetType, "Initialize") ??
                                     AccessTools.Method(targetType, "Setup") ??
                                     AccessTools.Method(targetType, "Show") ??
                                     AccessTools.Method(targetType, "Open");

                        if (method != null)
                        {
                            var postfix = new HarmonyMethod(typeof(GameOverScreenPatch).GetMethod(nameof(Postfix)));
                            harmony.Patch(method, postfix: postfix);
                            MonsterTrainAccessibility.LogInfo($"Patched {name}.{method.Name}");
                            return;
                        }
                    }
                }

                MonsterTrainAccessibility.LogInfo("GameOverScreen not found - will use alternative detection");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch GameOverScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Game over screen entered");

                // Auto-read the game over screen with full details
                AutoReadGameOverScreen(__instance);

                // Fix navigation by selecting the first button after a short delay
                if (__instance is MonoBehaviour screenBehaviour)
                {
                    screenBehaviour.StartCoroutine(SelectFirstButtonDelayed(__instance));
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in GameOverScreen patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Select the first selectable button on the game over screen after a delay
        /// </summary>
        private static System.Collections.IEnumerator SelectFirstButtonDelayed(object screen)
        {
            // Wait for UI to fully load
            yield return new WaitForSecondsRealtime(0.5f);

            GameObject screenGO = null;
            UnityEngine.UI.Selectable firstSelectable = null;
            bool success = false;

            try
            {
                if (screen is Component comp)
                {
                    screenGO = comp.gameObject;
                }
                else if (screen is GameObject go)
                {
                    screenGO = go;
                }

                if (screenGO != null)
                {
                    // Find all selectable/button elements on the screen
                    var allSelectables = screenGO.GetComponentsInChildren<UnityEngine.UI.Selectable>(false);

                    // Filter to only active, interactable selectables
                    foreach (var sel in allSelectables)
                    {
                        if (sel != null && sel.gameObject.activeInHierarchy && sel.interactable)
                        {
                            firstSelectable = sel;
                            break;
                        }
                    }

                    if (firstSelectable != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Selecting first button on game over screen: {firstSelectable.name}");
                        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(firstSelectable.gameObject);
                        success = true;
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("No selectable buttons found on game over screen");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error selecting first button: {ex.Message}");
            }

            // Announce navigation help if we found a button
            if (success)
            {
                yield return new WaitForSecondsRealtime(0.3f);
                MonsterTrainAccessibility.ScreenReader?.Speak("Use arrow keys to navigate, Enter to select.", false);
            }
        }

        private static void AutoReadGameOverScreen(object screen)
        {
            try
            {
                var sb = new StringBuilder();
                var screenType = screen?.GetType();
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Get key data from the screen object and its base classes
                string victoryTypeStr = null;
                int finalScore = 0;
                string mainClassName = null;
                string subClassName = null;
                int covenantLevel = 0;
                int battlesWon = 0;
                double runTimeSeconds = 0;
                int gold = 0;

                // Check all types in inheritance chain (GameOverScreen extends GameOverScreenBase)
                var currentType = screenType;
                while (currentType != null)
                {
                    // Get victoryType field (SaveManager.VictoryType enum)
                    var victoryTypeField = currentType.GetField("victoryType", bindingFlags);
                    if (victoryTypeField != null && victoryTypeStr == null)
                    {
                        var vt = victoryTypeField.GetValue(screen);
                        if (vt != null)
                        {
                            victoryTypeStr = vt.ToString();
                        }
                    }

                    // Get finalScore field
                    var finalScoreField = currentType.GetField("finalScore", bindingFlags);
                    if (finalScoreField != null && finalScore == 0)
                    {
                        var fs = finalScoreField.GetValue(screen);
                        if (fs is int score) finalScore = score;
                    }

                    // Get currentRunData (RunAggregateData) for detailed info
                    var runDataField = currentType.GetField("currentRunData", bindingFlags);
                    if (runDataField != null)
                    {
                        var runData = runDataField.GetValue(screen);
                        if (runData != null)
                        {
                            var runDataType = runData.GetType();

                            // Get battles won
                            var getBattlesMethod = runDataType.GetMethod("GetNumBattlesWon");
                            if (getBattlesMethod != null)
                            {
                                var result = getBattlesMethod.Invoke(runData, null);
                                if (result is int b) battlesWon = b;
                            }

                            // Get run time
                            var getRunTimeMethod = runDataType.GetMethod("GetRunTime");
                            if (getRunTimeMethod != null)
                            {
                                var result = getRunTimeMethod.Invoke(runData, null);
                                if (result is TimeSpan ts) runTimeSeconds = ts.TotalSeconds;
                            }

                            // Get gold
                            var getGoldMethod = runDataType.GetMethod("GetFinalGold");
                            if (getGoldMethod != null)
                            {
                                var result = getGoldMethod.Invoke(runData, null);
                                if (result is int g) gold = g;
                            }

                            // Get covenant/ascension level
                            var getAscensionMethod = runDataType.GetMethod("GetAscensionLevel");
                            if (getAscensionMethod != null)
                            {
                                var result = getAscensionMethod.Invoke(runData, null);
                                if (result is int a) covenantLevel = a;
                            }

                            // Get score from run data if not found elsewhere
                            if (finalScore == 0)
                            {
                                var getScoreMethod = runDataType.GetMethod("GetScore");
                                if (getScoreMethod != null)
                                {
                                    var result = getScoreMethod.Invoke(runData, null);
                                    if (result is int s) finalScore = s;
                                }
                            }
                        }
                    }

                    // Get saveManager for clan names and score
                    var saveManagerField = currentType.GetField("saveManager", bindingFlags);
                    if (saveManagerField != null && mainClassName == null)
                    {
                        var saveManager = saveManagerField.GetValue(screen);
                        if (saveManager != null)
                        {
                            var smType = saveManager.GetType();

                            // Get main class
                            var getMainClassMethod = smType.GetMethod("GetMainClass");
                            if (getMainClassMethod != null)
                            {
                                var mainClass = getMainClassMethod.Invoke(saveManager, null);
                                if (mainClass != null)
                                {
                                    var getTitleMethod = mainClass.GetType().GetMethod("GetTitle");
                                    if (getTitleMethod != null)
                                    {
                                        mainClassName = getTitleMethod.Invoke(mainClass, null) as string;
                                    }
                                }
                            }

                            // Get sub class
                            var getSubClassMethod = smType.GetMethod("GetSubClass");
                            if (getSubClassMethod != null)
                            {
                                var subClass = getSubClassMethod.Invoke(saveManager, null);
                                if (subClass != null)
                                {
                                    var getTitleMethod = subClass.GetType().GetMethod("GetTitle");
                                    if (getTitleMethod != null)
                                    {
                                        subClassName = getTitleMethod.Invoke(subClass, null) as string;
                                    }
                                }
                            }

                            // Get covenant level from saveManager if not found
                            if (covenantLevel == 0)
                            {
                                var getAscensionMethod = smType.GetMethod("GetAscensionLevel");
                                if (getAscensionMethod != null)
                                {
                                    var result = getAscensionMethod.Invoke(saveManager, null);
                                    if (result is int a) covenantLevel = a;
                                }
                            }

                            // Get battles won from saveManager if not found
                            if (battlesWon == 0)
                            {
                                var getBattlesMethod = smType.GetMethod("GetNumBattlesWon");
                                if (getBattlesMethod != null)
                                {
                                    var result = getBattlesMethod.Invoke(saveManager, null);
                                    if (result is int b) battlesWon = b;
                                }
                            }

                            // Get score directly from saveManager - most reliable source
                            // saveManager.GetScore() computes the full score including
                            // battle scores, gold bonus, crystal bonus, and distance modifiers
                            var getScoreMethod = smType.GetMethod("GetScore");
                            if (getScoreMethod != null)
                            {
                                var result = getScoreMethod.Invoke(saveManager, null);
                                if (result is int s && s > 0)
                                {
                                    MonsterTrainAccessibility.LogInfo($"Score from saveManager.GetScore(): {s} (field was: {finalScore})");
                                    finalScore = s;
                                }
                            }
                        }
                    }

                    currentType = currentType.BaseType;
                }

                // Also try to get the title label text directly
                string titleText = null;
                var titleField = screenType?.GetField("titleLabel", bindingFlags);
                if (titleField != null)
                {
                    var labelObj = titleField.GetValue(screen);
                    if (labelObj != null)
                    {
                        var textProp = labelObj.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            titleText = textProp.GetValue(labelObj) as string;
                        }
                    }
                }

                // Build announcement
                // Victory/Defeat - use title text if available, otherwise infer from victoryType
                if (!string.IsNullOrEmpty(titleText))
                {
                    sb.Append($"{titleText}! ");
                }
                else if (!string.IsNullOrEmpty(victoryTypeStr))
                {
                    if (victoryTypeStr == "Hellforged")
                        sb.Append("Hellforged Victory! ");
                    else if (victoryTypeStr == "Standard")
                        sb.Append("Victory! ");
                    else
                        sb.Append("Defeat. ");
                }
                else
                {
                    sb.Append("Run complete. ");
                }

                // Clans
                if (!string.IsNullOrEmpty(mainClassName) && !string.IsNullOrEmpty(subClassName))
                {
                    sb.Append($"{mainClassName} and {subClassName}. ");
                }
                else if (!string.IsNullOrEmpty(mainClassName))
                {
                    sb.Append($"{mainClassName}. ");
                }

                // Covenant
                if (covenantLevel > 0)
                {
                    sb.Append($"Covenant {covenantLevel}. ");
                }

                // Battles won
                if (battlesWon > 0)
                {
                    sb.Append($"{battlesWon} battles won. ");
                }

                // Score
                if (finalScore > 0)
                {
                    sb.Append($"Score: {finalScore:N0}. ");
                }

                // Gold
                if (gold > 0)
                {
                    sb.Append($"Gold: {gold:N0}. ");
                }

                // Run time
                if (runTimeSeconds > 0)
                {
                    var runTime = TimeSpan.FromSeconds(runTimeSeconds);
                    if (runTime.TotalHours >= 1)
                    {
                        sb.Append($"Time: {(int)runTime.TotalHours} hours {runTime.Minutes} minutes. ");
                    }
                    else
                    {
                        sb.Append($"Time: {runTime.Minutes} minutes {runTime.Seconds} seconds. ");
                    }
                }

                sb.Append("Press S for run summary. ");

                // Announce
                string announcement = sb.ToString().Trim();
                MonsterTrainAccessibility.LogInfo($"Game over auto-read: {announcement}");
                MonsterTrainAccessibility.ScreenReader?.Speak(announcement, false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in AutoReadGameOverScreen: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Run complete. Press S for run summary.", false);
            }
        }
    }

    /// <summary>
    /// Announce stat highlights as they appear on the game over screen.
    /// Hooks StatHighlightUI.AnimateInCoroutine to read the stat when it animates in.
    /// </summary>
    public static class StatHighlightPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("StatHighlightUI");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("StatHighlightUI not found");
                    return;
                }

                // Hook AnimateInCoroutine - this is called when the stat becomes visible
                var animateMethod = AccessTools.Method(targetType, "AnimateInCoroutine");
                if (animateMethod != null)
                {
                    var prefix = new HarmonyMethod(typeof(StatHighlightPatch).GetMethod(nameof(AnimatePrefix)));
                    harmony.Patch(animateMethod, prefix: prefix);
                    MonsterTrainAccessibility.LogInfo("Patched StatHighlightUI.AnimateInCoroutine");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch StatHighlightUI: {ex.Message}");
            }
        }

        // Prefix runs before AnimateInCoroutine - at this point the labels are already set
        public static void AnimatePrefix(object __instance)
        {
            try
            {
                if (__instance == null) return;

                var instanceType = __instance.GetType();
                var bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;

                string headerText = null;
                string statText = null;

                // Get headerLabel text
                var headerField = instanceType.GetField("headerLabel", bindingFlags);
                if (headerField != null)
                {
                    var label = headerField.GetValue(__instance);
                    if (label != null)
                    {
                        var textProp = label.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            headerText = textProp.GetValue(label) as string;
                        }
                    }
                }

                // Get statLabel text
                var statField = instanceType.GetField("statLabel", bindingFlags);
                if (statField != null)
                {
                    var label = statField.GetValue(__instance);
                    if (label != null)
                    {
                        var textProp = label.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            statText = textProp.GetValue(label) as string;
                        }
                    }
                }

                // Build and announce
                if (!string.IsNullOrEmpty(headerText) || !string.IsNullOrEmpty(statText))
                {
                    // Clean rich text tags
                    headerText = Screens.BattleAccessibility.StripRichTextTags(headerText ?? "");
                    statText = Screens.BattleAccessibility.StripRichTextTags(statText ?? "");

                    // Remove newlines for cleaner speech
                    statText = statText.Replace("\n", " ").Replace("\r", "");

                    string announcement = $"{headerText}: {statText}".Trim();
                    MonsterTrainAccessibility.LogInfo($"Stat highlight: {announcement}");
                    MonsterTrainAccessibility.ScreenReader?.Queue(announcement);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in StatHighlightPatch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Announce win streak when it appears on the game over screen.
    /// </summary>
    public static class WinStreakPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("WinStreakIncreaseUI");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("WinStreakIncreaseUI not found");
                    return;
                }

                // Hook AnimateInCoroutine
                var animateMethod = AccessTools.Method(targetType, "AnimateInCoroutine");
                if (animateMethod != null)
                {
                    var prefix = new HarmonyMethod(typeof(WinStreakPatch).GetMethod(nameof(AnimatePrefix)));
                    harmony.Patch(animateMethod, prefix: prefix);
                    MonsterTrainAccessibility.LogInfo("Patched WinStreakIncreaseUI.AnimateInCoroutine");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch WinStreakIncreaseUI: {ex.Message}");
            }
        }

        public static void AnimatePrefix(object __instance)
        {
            try
            {
                if (__instance == null) return;

                var instanceType = __instance.GetType();
                var bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

                string countText = null;
                string previousText = null;

                // Get countLabel from base WinStreakUI class
                var countField = instanceType.GetField("countLabel", bindingFlags);
                if (countField == null)
                {
                    // Try base type
                    countField = instanceType.BaseType?.GetField("countLabel", bindingFlags);
                }
                if (countField != null)
                {
                    var label = countField.GetValue(__instance);
                    if (label != null)
                    {
                        var textProp = label.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            countText = textProp.GetValue(label) as string;
                        }
                    }
                }

                // Get previousWinStreakLabel
                var prevField = instanceType.GetField("previousWinStreakLabel", bindingFlags);
                if (prevField != null)
                {
                    var label = prevField.GetValue(__instance);
                    if (label != null)
                    {
                        var textProp = label.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            previousText = textProp.GetValue(label) as string;
                        }
                    }
                }

                // Build announcement
                if (!string.IsNullOrEmpty(countText))
                {
                    countText = Screens.BattleAccessibility.StripRichTextTags(countText);
                    string announcement;
                    if (!string.IsNullOrEmpty(previousText))
                    {
                        previousText = Screens.BattleAccessibility.StripRichTextTags(previousText);
                        announcement = $"Win streak increased! {previousText} to {countText}";
                    }
                    else
                    {
                        announcement = $"Win streak: {countText}";
                    }
                    MonsterTrainAccessibility.LogInfo($"Win streak: {announcement}");
                    MonsterTrainAccessibility.ScreenReader?.Queue(announcement);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in WinStreakPatch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Announce unlocks (level ups, new cards/relics, clan unlocks, etc.) as they appear.
    /// </summary>
    public static class UnlockScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("UnlockScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("UnlockScreen not found");
                    return;
                }

                // Hook ShowNextUnlock - this is called each time an unlock is shown
                var showMethod = AccessTools.Method(targetType, "ShowNextUnlock");
                if (showMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(UnlockScreenPatch).GetMethod(nameof(ShowNextUnlockPostfix)));
                    harmony.Patch(showMethod, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched UnlockScreen.ShowNextUnlock");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch UnlockScreen: {ex.Message}");
            }
        }

        public static void ShowNextUnlockPostfix(object __instance)
        {
            try
            {
                if (__instance == null) return;

                var instanceType = __instance.GetType();
                var bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;

                // Get currentItem (UnlockDisplayData) via the property
                var currentItemProp = instanceType.GetProperty("currentItem", bindingFlags);
                if (currentItemProp == null) return;

                var currentItem = currentItemProp.GetValue(__instance);
                if (currentItem == null) return;

                var itemType = currentItem.GetType();

                // Get source (UnlockSource enum)
                var sourceField = itemType.GetField("source");
                string source = sourceField?.GetValue(currentItem)?.ToString() ?? "";

                // Get headerTextContent (clan name, etc.)
                var headerContentField = itemType.GetField("headerTextContent");
                string headerContent = headerContentField?.GetValue(currentItem) as string ?? "";

                // Get headerLevel
                var headerLevelField = itemType.GetField("headerLevel");
                int headerLevel = -1;
                if (headerLevelField != null)
                {
                    var lvl = headerLevelField.GetValue(currentItem);
                    if (lvl is int l) headerLevel = l;
                }

                // Get unlocked card name
                string unlockedCardName = null;
                var cardDataField = itemType.GetField("unlockedCardData");
                if (cardDataField != null)
                {
                    var cardData = cardDataField.GetValue(currentItem);
                    if (cardData != null)
                    {
                        var getNameMethod = cardData.GetType().GetMethod("GetName");
                        if (getNameMethod != null)
                        {
                            unlockedCardName = getNameMethod.Invoke(cardData, null) as string;
                        }
                    }
                }

                // Get unlocked relic name
                string unlockedRelicName = null;
                var relicDataField = itemType.GetField("unlockedRelicData");
                if (relicDataField != null)
                {
                    var relicData = relicDataField.GetValue(currentItem);
                    if (relicData != null)
                    {
                        var getNameMethod = relicData.GetType().GetMethod("GetName");
                        if (getNameMethod != null)
                        {
                            unlockedRelicName = getNameMethod.Invoke(relicData, null) as string;
                        }
                    }
                }

                // Get unlocked feature data (title)
                string featureTitle = null;
                var featureDataField = itemType.GetField("unlockedFeatureData");
                if (featureDataField != null)
                {
                    var featureData = featureDataField.GetValue(currentItem);
                    if (featureData != null)
                    {
                        var titleField = featureData.GetType().GetField("title");
                        if (titleField != null)
                        {
                            featureTitle = titleField.GetValue(featureData) as string;
                        }
                    }
                }

                // Build announcement based on source type
                string announcement = BuildUnlockAnnouncement(source, headerContent, headerLevel,
                    unlockedCardName, unlockedRelicName, featureTitle);

                if (!string.IsNullOrEmpty(announcement))
                {
                    MonsterTrainAccessibility.LogInfo($"Unlock: {announcement}");
                    MonsterTrainAccessibility.ScreenReader?.Speak(announcement, false);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in UnlockScreenPatch: {ex.Message}");
            }
        }

        private static string BuildUnlockAnnouncement(string source, string headerContent, int headerLevel,
            string cardName, string relicName, string featureTitle)
        {
            var sb = new StringBuilder();

            switch (source)
            {
                case "ClanLevelUp":
                    sb.Append($"{headerContent} reached level {headerLevel}! ");
                    if (!string.IsNullOrEmpty(cardName))
                        sb.Append($"Unlocked card: {cardName}. ");
                    if (!string.IsNullOrEmpty(relicName))
                        sb.Append($"Unlocked artifact: {relicName}. ");
                    if (!string.IsNullOrEmpty(featureTitle))
                        sb.Append($"Unlocked: {featureTitle}. ");
                    break;

                case "NewClan":
                    sb.Append($"New clan unlocked: {featureTitle ?? headerContent}! ");
                    break;

                case "CovenantUnlocked":
                    sb.Append($"Covenant mode unlocked! {featureTitle}");
                    break;

                case "ChallengeLevelUp":
                    sb.Append($"New covenant level unlocked! {featureTitle}");
                    break;

                case "CardMastery":
                    sb.Append("Card mastery achieved! ");
                    break;

                case "DivineCardMastery":
                    sb.Append("Divine card mastery achieved! ");
                    break;

                case "FeatureUnlocked":
                    sb.Append($"Feature unlocked: {featureTitle}! ");
                    break;

                case "MasteryCardFrameUnlocked":
                    sb.Append("Mastery card frame unlocked! ");
                    break;

                default:
                    if (!string.IsNullOrEmpty(cardName))
                        sb.Append($"Unlocked card: {cardName}. ");
                    else if (!string.IsNullOrEmpty(relicName))
                        sb.Append($"Unlocked artifact: {relicName}. ");
                    else if (!string.IsNullOrEmpty(featureTitle))
                        sb.Append($"Unlocked: {featureTitle}. ");
                    break;
            }

            if (!string.IsNullOrEmpty(cardName) || !string.IsNullOrEmpty(relicName))
            {
                sb.Append("Press Up arrow to view details. ");
            }
            sb.Append("Press Enter to continue.");
            return sb.ToString().Trim();
        }
    }
}
