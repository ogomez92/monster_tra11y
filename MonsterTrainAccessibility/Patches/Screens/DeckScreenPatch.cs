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
    /// Detect deck/card list screen (deck view, purge, upgrade, draw pile, discard pile)
    /// </summary>
    public static class DeckScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("DeckScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(DeckScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched DeckScreen.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch DeckScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.DeckView);
                AutoReadDeckScreen(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in DeckScreen patch: {ex.Message}");
            }
        }

        private static void AutoReadDeckScreen(object screen)
        {
            try
            {
                var screenType = screen.GetType();
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Try to determine the mode/source
                string modeStr = null;
                var modeField = screenType.GetField("mode", bindingFlags) ??
                                screenType.GetField("_mode", bindingFlags) ??
                                screenType.GetField("source", bindingFlags) ??
                                screenType.GetField("_source", bindingFlags);

                if (modeField != null)
                {
                    var modeValue = modeField.GetValue(screen);
                    modeStr = modeValue?.ToString();
                }

                // Get card count
                int cardCount = 0;
                var cardsField = screenType.GetField("cards", bindingFlags) ??
                                 screenType.GetField("_cards", bindingFlags) ??
                                 screenType.GetField("cardStates", bindingFlags);

                if (cardsField != null)
                {
                    var cards = cardsField.GetValue(screen) as System.Collections.IList;
                    if (cards != null)
                        cardCount = cards.Count;
                }

                // Build context-dependent announcement
                string announcement;
                if (!string.IsNullOrEmpty(modeStr))
                {
                    string modeLower = modeStr.ToLower();
                    if (modeLower.Contains("purge") || modeLower.Contains("remove"))
                        announcement = $"Card Purge. Select a card to remove. {cardCount} cards.";
                    else if (modeLower.Contains("upgrade") || modeLower.Contains("enhance") || modeLower.Contains("applyupgrade"))
                    {
                        // Try to get upgrade description from cardUpgradeData field
                        string upgradeDesc = GetUpgradeDescription(screen, screenType, bindingFlags);
                        if (!string.IsNullOrEmpty(upgradeDesc))
                            announcement = $"Card Upgrade: {upgradeDesc}. Select a card to apply it to. {cardCount} cards.";
                        else
                            announcement = $"Card Upgrade. Select a card to enhance. {cardCount} cards.";
                    }
                    else if (modeLower.Contains("draw"))
                        announcement = $"Draw Pile. {cardCount} cards.";
                    else if (modeLower.Contains("discard"))
                        announcement = $"Discard Pile. {cardCount} cards.";
                    else
                        announcement = cardCount > 0 ? $"Deck. {cardCount} cards." : "Deck.";
                }
                else
                {
                    announcement = cardCount > 0 ? $"Deck. {cardCount} cards." : "Deck.";
                }

                announcement += " Arrow keys to browse cards. Press F1 for help.";
                MonsterTrainAccessibility.ScreenReader?.Speak(announcement, false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error auto-reading deck screen: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Deck. Arrow keys to browse cards.", false);
            }
        }

        /// <summary>
        /// Extract upgrade description from DeckScreen's cardUpgradeData field
        /// </summary>
        private static string GetUpgradeDescription(object screen, Type screenType, BindingFlags bindingFlags)
        {
            try
            {
                // DeckScreen stores the upgrade in cardUpgradeData field (set during SetupRelicDataModes)
                var upgradeDataField = screenType.GetField("cardUpgradeData", bindingFlags);
                if (upgradeDataField == null) return null;

                var upgradeData = upgradeDataField.GetValue(screen);
                if (upgradeData == null) return null;

                return DescribeUpgradeData(upgradeData);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting upgrade description: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Build a human-readable description of a CardUpgradeData object
        /// </summary>
        internal static string DescribeUpgradeData(object upgradeData)
        {
            try
            {
                var upgradeType = upgradeData.GetType();
                var parts = new List<string>();

                // Try localized title first
                var getTitleKeyMethod = upgradeType.GetMethod("GetUpgradeTitleKey");
                if (getTitleKeyMethod != null)
                {
                    string titleKey = getTitleKeyMethod.Invoke(upgradeData, null) as string;
                    if (!string.IsNullOrEmpty(titleKey))
                    {
                        string title = KeywordManager.TryLocalize(titleKey);
                        if (!string.IsNullOrEmpty(title) && !title.Contains("_"))
                            parts.Add(title);
                    }
                }

                // Try localized description
                var getDescKeyMethod = upgradeType.GetMethod("GetUpgradeDescriptionKey");
                if (getDescKeyMethod != null)
                {
                    string descKey = getDescKeyMethod.Invoke(upgradeData, null) as string;
                    if (!string.IsNullOrEmpty(descKey))
                    {
                        string desc = KeywordManager.TryLocalize(descKey);
                        if (!string.IsNullOrEmpty(desc) && !desc.Contains("_"))
                        {
                            desc = Screens.BattleAccessibility.StripRichTextTags(desc);
                            parts.Add(desc);
                        }
                    }
                }

                // If no localized text, build from stats
                if (parts.Count == 0)
                {
                    var statParts = new List<string>();
                    int val;

                    var getBonusHP = upgradeType.GetMethod("GetBonusHP");
                    if (getBonusHP != null)
                    {
                        val = (int)getBonusHP.Invoke(upgradeData, null);
                        if (val != 0) statParts.Add($"{(val > 0 ? "+" : "")}{val} health");
                    }

                    var getBonusDamage = upgradeType.GetMethod("GetBonusDamage");
                    if (getBonusDamage != null)
                    {
                        val = (int)getBonusDamage.Invoke(upgradeData, null);
                        if (val != 0) statParts.Add($"{(val > 0 ? "+" : "")}{val} attack");
                    }

                    var getCostReduction = upgradeType.GetMethod("GetCostReduction");
                    if (getCostReduction != null)
                    {
                        val = (int)getCostReduction.Invoke(upgradeData, null);
                        if (val != 0) statParts.Add($"-{val} ember cost");
                    }

                    var getBonusSize = upgradeType.GetMethod("GetBonusSize");
                    if (getBonusSize != null)
                    {
                        val = (int)getBonusSize.Invoke(upgradeData, null);
                        if (val != 0) statParts.Add($"{(val > 0 ? "+" : "")}{val} capacity");
                    }

                    // Check for status effect upgrades
                    var getStatusEffects = upgradeType.GetMethod("GetStatusEffectUpgrades");
                    if (getStatusEffects != null)
                    {
                        var effects = getStatusEffects.Invoke(upgradeData, null) as System.Collections.IList;
                        if (effects != null && effects.Count > 0)
                        {
                            foreach (var effect in effects)
                            {
                                if (effect == null) continue;
                                var effectType = effect.GetType();
                                var statusIdField = effectType.GetField("statusId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                var countField = effectType.GetField("count", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                string statusId = statusIdField?.GetValue(effect) as string;
                                int count = 0;
                                if (countField != null)
                                {
                                    var countVal = countField.GetValue(effect);
                                    if (countVal is int c) count = c;
                                }
                                if (!string.IsNullOrEmpty(statusId))
                                    statParts.Add($"{statusId} {count}");
                            }
                        }
                    }

                    if (statParts.Count > 0)
                        parts.Add(string.Join(", ", statParts));
                }

                return parts.Count > 0 ? string.Join(". ", parts) : null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error describing upgrade data: {ex.Message}");
            }
            return null;
        }
    }
}
