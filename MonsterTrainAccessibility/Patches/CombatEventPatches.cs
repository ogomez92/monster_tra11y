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
    /// Note: AddStatusEffect has multiple overloads, so we need to find the right one
    /// </summary>
    public static class StatusEffectPatch
    {
        // Track last announced effect to avoid duplicate announcements
        private static string _lastAnnouncedEffect = "";
        private static float _lastAnnouncedTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType != null)
                {
                    // AddStatusEffect has multiple overloads - try to find a specific one
                    System.Reflection.MethodInfo method = null;

                    // Get all methods named AddStatusEffect
                    var methods = characterType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    foreach (var m in methods)
                    {
                        if (m.Name == "AddStatusEffect")
                        {
                            var parameters = m.GetParameters();
                            // Prefer the simplest overload
                            if (method == null || parameters.Length < method.GetParameters().Length)
                            {
                                method = m;
                            }
                        }
                    }

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(StatusEffectPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched CharacterState.AddStatusEffect (params: {method.GetParameters().Length})");
                    }
                    else
                    {
                        // Expected in some game versions - not a critical patch
                        MonsterTrainAccessibility.LogInfo("AddStatusEffect method not found - status announcements disabled");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Skipping AddStatusEffect patch: {ex.Message}");
            }
        }

        public static void Postfix(object __instance, string statusId, int numStacks)
        {
            try
            {
                if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceStatusEffects.Value)
                    return;

                if (string.IsNullOrEmpty(statusId) || numStacks <= 0)
                    return;

                // Get unit name
                string unitName = GetUnitName(__instance);

                // Create a key to detect duplicate announcements
                string effectKey = $"{unitName}_{statusId}_{numStacks}";
                float currentTime = UnityEngine.Time.unscaledTime;

                // Avoid duplicate announcements within 0.5 seconds
                if (effectKey == _lastAnnouncedEffect && currentTime - _lastAnnouncedTime < 0.5f)
                    return;

                _lastAnnouncedEffect = effectKey;
                _lastAnnouncedTime = currentTime;

                // Make the status ID more readable (e.g., "armor" instead of "Armor_StatusId")
                string effectName = CleanStatusName(statusId);

                MonsterTrainAccessibility.BattleHandler?.OnStatusEffectApplied(unitName, effectName, numStacks);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in status effect patch: {ex.Message}");
            }
        }

        private static string GetUnitName(object characterState)
        {
            try
            {
                var type = characterState.GetType();

                // Try GetName method first
                var getNameMethod = type.GetMethod("GetName");
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(characterState, null) as string;
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }

                // Try GetCharacterDataRead
                var getDataMethod = type.GetMethod("GetCharacterDataRead");
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(characterState, null);
                    if (data != null)
                    {
                        var dataGetNameMethod = data.GetType().GetMethod("GetName");
                        if (dataGetNameMethod != null)
                        {
                            var name = dataGetNameMethod.Invoke(data, null) as string;
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }
            }
            catch { }
            return "Unit";
        }

        private static string CleanStatusName(string statusId)
        {
            if (string.IsNullOrEmpty(statusId))
                return "effect";

            // Remove common suffixes
            string name = statusId
                .Replace("_StatusId", "")
                .Replace("StatusId", "")
                .Replace("_", " ");

            // Add space before capital letters (camelCase to words)
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");

            return name.ToLower().Trim();
        }
    }

    /// <summary>
    /// Detect battle end (victory)
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

    /// <summary>
    /// Detect when a unit is spawned (added to the game board)
    /// </summary>
    public static class UnitSpawnPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try MonsterManager.AddCharacter for player units
                var monsterManagerType = AccessTools.TypeByName("MonsterManager");
                if (monsterManagerType != null)
                {
                    var method = AccessTools.Method(monsterManagerType, "AddCharacter");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(UnitSpawnPatch).GetMethod(nameof(PostfixMonster)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched MonsterManager.AddCharacter");
                    }
                }

                // Try HeroManager.AddCharacter for enemies
                var heroManagerType = AccessTools.TypeByName("HeroManager");
                if (heroManagerType != null)
                {
                    var method = AccessTools.Method(heroManagerType, "AddCharacter");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(UnitSpawnPatch).GetMethod(nameof(PostfixHero)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched HeroManager.AddCharacter");
                    }
                }

                // Also try CharacterState.Setup as fallback
                var characterStateType = AccessTools.TypeByName("CharacterState");
                if (characterStateType != null)
                {
                    var method = AccessTools.Method(characterStateType, "Setup");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(UnitSpawnPatch).GetMethod(nameof(PostfixCharacterSetup)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched CharacterState.Setup");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch unit spawn: {ex.Message}");
            }
        }

        public static void PostfixMonster(object __instance, object character)
        {
            try
            {
                if (character == null) return;

                string name = GetUnitName(character);
                int roomIndex = GetRoomIndex(character);

                MonsterTrainAccessibility.LogInfo($"Monster spawned: {name} on floor {roomIndex}");
                MonsterTrainAccessibility.BattleHandler?.OnUnitSpawned(name, false, roomIndex);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in monster spawn patch: {ex.Message}");
            }
        }

        public static void PostfixHero(object __instance, object character)
        {
            try
            {
                if (character == null) return;

                string name = GetUnitName(character);
                int roomIndex = GetRoomIndex(character);

                MonsterTrainAccessibility.LogInfo($"Enemy spawned: {name} on floor {roomIndex}");
                MonsterTrainAccessibility.BattleHandler?.OnUnitSpawned(name, true, roomIndex);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in hero spawn patch: {ex.Message}");
            }
        }

        public static void PostfixCharacterSetup(object __instance)
        {
            try
            {
                // This is a fallback - only log for debugging
                string name = GetUnitName(__instance);
                bool isEnemy = IsEnemyUnit(__instance);
                MonsterTrainAccessibility.LogInfo($"CharacterState.Setup called: {name}, isEnemy={isEnemy}");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in character setup patch: {ex.Message}");
            }
        }

        private static string GetUnitName(object characterState)
        {
            try
            {
                var type = characterState.GetType();

                // Try GetName first
                var getNameMethod = type.GetMethod("GetName");
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(characterState, null) as string;
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }

                // Try GetCharacterData
                var getDataMethod = type.GetMethod("GetCharacterData") ?? type.GetMethod("GetCharacterDataRead");
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(characterState, null);
                    if (data != null)
                    {
                        var dataGetNameMethod = data.GetType().GetMethod("GetName");
                        if (dataGetNameMethod != null)
                        {
                            var name = dataGetNameMethod.Invoke(data, null) as string;
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }
            }
            catch { }
            return "Unit";
        }

        private static int GetRoomIndex(object characterState)
        {
            try
            {
                var type = characterState.GetType();
                var getRoomMethod = type.GetMethod("GetCurrentRoomIndex");
                if (getRoomMethod != null)
                {
                    var result = getRoomMethod.Invoke(characterState, null);
                    if (result is int index)
                        return index;
                }
            }
            catch { }
            return -1;
        }

        private static bool IsEnemyUnit(object characterState)
        {
            try
            {
                var type = characterState.GetType();
                var getTeamMethod = type.GetMethod("GetTeamType");
                if (getTeamMethod != null)
                {
                    var team = getTeamMethod.Invoke(characterState, null);
                    // In Monster Train, "Heroes" are the enemies
                    return team?.ToString() == "Heroes";
                }
            }
            catch { }
            return false;
        }
    }

    /// <summary>
    /// Detect when enemies ascend floors
    /// </summary>
    public static class EnemyAscendPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try to find the ascend method on CombatManager or HeroManager
                var combatType = AccessTools.TypeByName("CombatManager");
                if (combatType != null)
                {
                    // Try various method names that might handle ascension
                    var method = AccessTools.Method(combatType, "AscendEnemies") ??
                                 AccessTools.Method(combatType, "MoveEnemies") ??
                                 AccessTools.Method(combatType, "ProcessAscend");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(EnemyAscendPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched ascend method: {method.Name}");
                    }
                    else
                    {
                        // Expected in some game versions - ascend is announced via other means
                        MonsterTrainAccessibility.LogInfo("Ascend method not found - will use alternative detection");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch enemy ascend: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.BattleHandler?.OnEnemiesAscended();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ascend patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect pyre damage
    /// </summary>
    public static class PyreDamagePatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try to find the pyre/tower damage method
                var saveManagerType = AccessTools.TypeByName("SaveManager");
                if (saveManagerType != null)
                {
                    var method = AccessTools.Method(saveManagerType, "SetTowerHP") ??
                                 AccessTools.Method(saveManagerType, "DamageTower") ??
                                 AccessTools.Method(saveManagerType, "ModifyTowerHP");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(PyreDamagePatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched pyre damage: {method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch pyre damage: {ex.Message}");
            }
        }

        private static int _lastPyreHP = -1;

        public static void Postfix(object __instance)
        {
            try
            {
                // Get current pyre HP
                var type = __instance.GetType();
                var getHPMethod = type.GetMethod("GetTowerHP");
                if (getHPMethod != null)
                {
                    var result = getHPMethod.Invoke(__instance, null);
                    if (result is int currentHP)
                    {
                        if (_lastPyreHP > 0 && currentHP < _lastPyreHP)
                        {
                            int damage = _lastPyreHP - currentHP;
                            MonsterTrainAccessibility.BattleHandler?.OnPyreDamaged(damage, currentHP);
                        }
                        _lastPyreHP = currentHP;
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in pyre damage patch: {ex.Message}");
            }
        }
    }
}
