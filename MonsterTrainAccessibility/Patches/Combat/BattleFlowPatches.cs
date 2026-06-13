using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Detect battle end (victory or defeat) so combat state is torn down
    /// before the post-battle victory/reward UI appears.
    /// </summary>
    public static class BattleVictoryPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var combatType = AccessTools.TypeByName("CombatManager");
                if (combatType != null)
                {
                    // CombatManager.StopCombat(bool combatWon) is the real
                    // battle-end method. The previous EndCombat/OnCombatComplete
                    // names never existed on CombatManager, so this patch was a
                    // silent no-op and IsInBattle never reset after a battle -
                    // which left the post-battle VictoryUI (part of the still-
                    // interactable Game screen) wrongly opening floor review.
                    // StopCombat is an IEnumerator coroutine; Harmony patches the
                    // kickoff, so the postfix runs the moment combat is told to
                    // stop, before the victory/reward UI is shown.
                    var method = AccessTools.Method(combatType, "StopCombat");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(BattleVictoryPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched battle end: CombatManager.{method.Name}");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogError("CombatManager.StopCombat not found - battle end detection disabled");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch battle end: {ex.Message}");
            }
        }

        public static void Postfix(bool combatWon)
        {
            try
            {
                var battle = MonsterTrainAccessibility.BattleHandler;
                // StopCombat can be reached more than once (its body no-ops when
                // combat already stopped); only react on the first transition out.
                if (battle == null || !battle.IsInBattle)
                    return;

                if (combatWon)
                {
                    battle.OnBattleWon();
                }
                else
                {
                    battle.OnBattleLost();
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in battle end patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect when all enemies on the current wave have been defeated.
    /// Hooks HeroManager's internal notification for no more heroes.
    /// </summary>
    public static class AllEnemiesDefeatedPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var heroManagerType = AccessTools.TypeByName("HeroManager");
                if (heroManagerType == null) return;

                // Try public method first, then private
                var method = heroManagerType.GetMethod("NoMoreHeroes",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                    heroManagerType.GetMethod("SendNoMoreHeroesNotifications",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(AllEnemiesDefeatedPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched HeroManager.{method.Name} for all-enemies-defeated detection");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("NoMoreHeroes/SendNoMoreHeroesNotifications not found - all-enemies-defeated detection disabled");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch all enemies defeated: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.BattleHandler?.OnAllEnemiesDefeated();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in all enemies defeated patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect combat phase changes for better context
    /// </summary>
    public static class CombatPhasePatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var combatType = AccessTools.TypeByName("CombatManager");
                if (combatType != null)
                {
                    // Try to patch combat resolution (when units attack)
                    var method = AccessTools.Method(combatType, "ProcessCombat") ??
                                 AccessTools.Method(combatType, "ResolveCombat") ??
                                 AccessTools.Method(combatType, "RunCombat");

                    if (method != null)
                    {
                        var prefix = new HarmonyMethod(typeof(CombatPhasePatch).GetMethod(nameof(PrefixCombat)));
                        harmony.Patch(method, prefix: prefix);
                        MonsterTrainAccessibility.LogInfo($"Patched combat resolution: {method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch combat phase: {ex.Message}");
            }
        }

        public static void PrefixCombat()
        {
            try
            {
                MonsterTrainAccessibility.BattleHandler?.OnCombatResolutionStarted();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in combat phase patch: {ex.Message}");
            }
        }
    }
}
