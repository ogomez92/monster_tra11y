using HarmonyLib;
using System;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Patches for combat events like turn changes, damage, and deaths.
    /// </summary>

    /// <summary>
    /// Detect player turn start
    /// </summary>
    [HarmonyPatch]
    public static class PlayerTurnStartPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CombatManager");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "StartPlayerTurn");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(PlayerTurnStartPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched CombatManager.StartPlayerTurn");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch StartPlayerTurn: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                // Would extract actual ember values from game state
                MonsterTrainAccessibility.BattleHandler?.OnTurnStarted(3, 3, 5);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in PlayerTurnStart patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect player turn end
    /// </summary>
    [HarmonyPatch]
    public static class PlayerTurnEndPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CombatManager");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "EndPlayerTurn");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(PlayerTurnEndPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched CombatManager.EndPlayerTurn");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch EndPlayerTurn: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.BattleHandler?.OnTurnEnded();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in PlayerTurnEnd patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect damage dealt
    /// </summary>
    [HarmonyPatch]
    public static class DamageAppliedPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var combatType = AccessTools.TypeByName("CombatManager");
                if (combatType != null)
                {
                    // Try different method names that might handle damage
                    var method = AccessTools.Method(combatType, "ApplyDamageToTarget") ??
                                 AccessTools.Method(combatType, "ApplyDamage");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(DamageAppliedPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched damage method: {method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch damage: {ex.Message}");
            }
        }

        public static void Postfix(int damage, object target)
        {
            try
            {
                if (damage > 0 && target != null)
                {
                    // Would extract actual unit name from CharacterState
                    string targetName = GetUnitName(target);
                    MonsterTrainAccessibility.BattleHandler?.OnDamageDealt("Attack", targetName, damage);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in damage patch: {ex.Message}");
            }
        }

        private static string GetUnitName(object characterState)
        {
            try
            {
                // Use reflection to get the character name
                var getDataMethod = characterState.GetType().GetMethod("GetCharacterDataRead");
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(characterState, null);
                    var getNameMethod = data?.GetType().GetMethod("GetNameKey");
                    if (getNameMethod != null)
                    {
                        var nameKey = getNameMethod.Invoke(data, null) as string;
                        // Would call Localize() on the key
                        return nameKey ?? "Unit";
                    }
                }
            }
            catch { }
            return "Unit";
        }
    }

    /// <summary>
    /// Detect unit death
    /// </summary>
    [HarmonyPatch]
    public static class UnitDeathPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType != null)
                {
                    var method = AccessTools.Method(characterType, "Die");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(UnitDeathPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched CharacterState.Die");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch Die: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                string unitName = GetUnitName(__instance);
                bool isEnemy = IsEnemyUnit(__instance);
                MonsterTrainAccessibility.BattleHandler?.OnUnitDied(unitName, isEnemy);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in death patch: {ex.Message}");
            }
        }

        private static string GetUnitName(object characterState)
        {
            try
            {
                var getDataMethod = characterState.GetType().GetMethod("GetCharacterDataRead");
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(characterState, null);
                    var getNameMethod = data?.GetType().GetMethod("GetNameKey");
                    if (getNameMethod != null)
                    {
                        return getNameMethod.Invoke(data, null) as string ?? "Unit";
                    }
                }
            }
            catch { }
            return "Unit";
        }

        private static bool IsEnemyUnit(object characterState)
        {
            try
            {
                var getTeamMethod = characterState.GetType().GetMethod("GetTeamType");
                if (getTeamMethod != null)
                {
                    var team = getTeamMethod.Invoke(characterState, null);
                    // Team.Type.Heroes are enemies in Monster Train (attacking the train)
                    return team?.ToString() == "Heroes";
                }
            }
            catch { }
            return false;
        }
    }

    /// <summary>
    /// Detect status effect application
    /// </summary>
    [HarmonyPatch]
    public static class StatusEffectPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType != null)
                {
                    var method = AccessTools.Method(characterType, "AddStatusEffect");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(StatusEffectPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched CharacterState.AddStatusEffect");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch AddStatusEffect: {ex.Message}");
            }
        }

        public static void Postfix(object __instance, object statusEffect)
        {
            try
            {
                string unitName = "Unit";
                string effectName = "Effect";
                int stacks = 1;

                // Extract actual values via reflection
                try
                {
                    var getDataMethod = __instance.GetType().GetMethod("GetCharacterDataRead");
                    if (getDataMethod != null)
                    {
                        var data = getDataMethod.Invoke(__instance, null);
                        var getNameMethod = data?.GetType().GetMethod("GetNameKey");
                        if (getNameMethod != null)
                        {
                            unitName = getNameMethod.Invoke(data, null) as string ?? "Unit";
                        }
                    }

                    // Get effect info
                    var statusIdField = statusEffect.GetType().GetField("statusId");
                    var countField = statusEffect.GetType().GetField("count");

                    if (statusIdField != null)
                    {
                        effectName = statusIdField.GetValue(statusEffect) as string ?? "Effect";
                    }
                    if (countField != null)
                    {
                        stacks = (int)countField.GetValue(statusEffect);
                    }
                }
                catch { }

                MonsterTrainAccessibility.BattleHandler?.OnStatusEffectApplied(unitName, effectName, stacks);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in status effect patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect battle end (victory)
    /// </summary>
    [HarmonyPatch]
    public static class BattleVictoryPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var combatType = AccessTools.TypeByName("CombatManager");
                if (combatType != null)
                {
                    var method = AccessTools.Method(combatType, "EndCombat") ??
                                 AccessTools.Method(combatType, "OnCombatComplete");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(BattleVictoryPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched battle end: {method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch battle end: {ex.Message}");
            }
        }

        public static void Postfix(bool victory)
        {
            try
            {
                if (victory)
                {
                    MonsterTrainAccessibility.BattleHandler?.OnBattleWon();
                }
                else
                {
                    MonsterTrainAccessibility.BattleHandler?.OnBattleLost();
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in battle end patch: {ex.Message}");
            }
        }
    }
}
