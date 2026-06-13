using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;
using System.Collections;
using System.Reflection;
using System.Text;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Reads the post-battle victory screen. VictoryUI is part of the (still
    /// active and interactable) Game screen, not a separate UIScreen, so it has
    /// no screen transition of its own to hook - we patch VictoryUI.Show and
    /// announce the rewards the player is about to collect, and mark the screen
    /// state as Rewards so help and review context match what is on screen.
    /// Floor review is kept out by BattleVictoryPatch tearing down IsInBattle
    /// before this runs.
    /// </summary>
    public static class VictoryUIPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("VictoryUI");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(VictoryUIPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched VictoryUI.Show");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogError("VictoryUI.Show not found - post-battle reward read disabled");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch VictoryUI: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Rewards);
                AnnounceRewards(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in VictoryUI patch: {ex.Message}");
            }
        }

        private static void AnnounceRewards(object victoryUI)
        {
            // Show() assigns the _rewards list last, so by the postfix it is set.
            var rewardsField = victoryUI.GetType().GetField("_rewards",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var rewards = rewardsField?.GetValue(victoryUI) as IList;

            var sb = new StringBuilder();
            if (rewards != null && rewards.Count > 0)
            {
                sb.Append("Rewards to collect: ");
                bool first = true;
                foreach (var reward in rewards)
                {
                    string name = RewardScreenPatch.GetRewardDisplayName(reward);
                    if (string.IsNullOrEmpty(name))
                        continue;
                    if (!first)
                        sb.Append(", ");
                    sb.Append(name);
                    first = false;
                }
                sb.Append(". Press Enter to collect.");
            }
            else
            {
                sb.Append("Battle complete. Press Enter to continue.");
            }

            MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), false);
        }
    }
}
