using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
    /// Signature: ApplyDamageToTarget(int damage, CharacterState target, ApplyDamageToTargetParameters parameters)
    /// </summary>
    public static class DamageAppliedPatch
    {
        // Track last damage to avoid duplicate announcements
        private static float _lastDamageTime = 0f;
        private static string _lastDamageKey = "";

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
                        // Log the actual parameters
                        var parameters = method.GetParameters();
                        MonsterTrainAccessibility.LogInfo($"ApplyDamageToTarget has {parameters.Length} parameters:");
                        foreach (var p in parameters)
                        {
                            MonsterTrainAccessibility.LogInfo($"  {p.Name}: {p.ParameterType.Name}");
                        }

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

        // Use positional parameters: __0 = damage (int), __1 = target (CharacterState)
        public static void Postfix(int __0, object __1)
        {
            try
            {
                int damage = __0;
                object target = __1;

                if (damage > 0 && target != null)
                {
                    string targetName = GetUnitName(target);
                    bool isEnemy = IsEnemyUnit(target);
                    int currentHP = GetCurrentHP(target);

                    // Create a key to prevent duplicate announcements within a short time
                    string damageKey = $"{targetName}_{damage}_{currentHP}";
                    float currentTime = UnityEngine.Time.unscaledTime;

                    if (damageKey != _lastDamageKey || currentTime - _lastDamageTime > 0.3f)
                    {
                        _lastDamageKey = damageKey;
                        _lastDamageTime = currentTime;

                        MonsterTrainAccessibility.LogInfo($"Damage: {damage} to {targetName} (enemy={isEnemy}), HP now {currentHP}");

                        // Announce based on who took damage
                        if (isEnemy)
                        {
                            // Enemy took damage (good for player)
                            MonsterTrainAccessibility.ScreenReader?.Queue($"{targetName} takes {damage} damage");
                        }
                        else
                        {
                            // Friendly unit took damage
                            MonsterTrainAccessibility.ScreenReader?.Queue($"{targetName} takes {damage} damage, {currentHP} HP remaining");
                        }

                        // Check for death
                        if (currentHP <= 0)
                        {
                            int roomIndex = GetRoomIndex(target);
                            int userFloor = RoomIndexToUserFloor(roomIndex);
                            MonsterTrainAccessibility.BattleHandler?.OnUnitDied(targetName, isEnemy, userFloor);
                        }
                    }
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
                var type = characterState.GetType();

                // Try GetName first (localized)
                var getNameMethod = type.GetMethod("GetName");
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(characterState, null) as string;
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }

                // Fallback to GetCharacterDataRead
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

        private static bool IsEnemyUnit(object characterState)
        {
            try
            {
                var getTeamMethod = characterState.GetType().GetMethod("GetTeamType");
                if (getTeamMethod != null)
                {
                    var team = getTeamMethod.Invoke(characterState, null);
                    // Team.Type.Heroes are enemies in Monster Train
                    return team?.ToString() == "Heroes";
                }
            }
            catch { }
            return false;
        }

        private static int GetCurrentHP(object characterState)
        {
            try
            {
                var getHPMethod = characterState.GetType().GetMethod("GetHP");
                if (getHPMethod != null)
                {
                    var result = getHPMethod.Invoke(characterState, null);
                    if (result is int hp)
                        return hp;
                }
            }
            catch { }
            return -1;
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

        private static int RoomIndexToUserFloor(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex > 2) return -1;
            return 3 - roomIndex;
        }
    }

    /// <summary>
    /// Detect unit death - CharacterState.Die doesn't exist, so we try alternative methods
    /// Death is also detected in DamageAppliedPatch when HP <= 0
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
                    // Try various death-related method names
                    var deathMethods = new[] { "Die", "Kill", "OnDeath", "ProcessDeath", "HandleDeath" };
                    MethodInfo method = null;

                    foreach (var methodName in deathMethods)
                    {
                        method = AccessTools.Method(characterType, methodName);
                        if (method != null)
                        {
                            MonsterTrainAccessibility.LogInfo($"Found death method: {methodName}");
                            break;
                        }
                    }

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(UnitDeathPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched death method: {method.Name}");
                    }
                    else
                    {
                        // List available methods for debugging
                        var methods = characterType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                        var relevantMethods = methods.Where(m =>
                            m.Name.Contains("Die") || m.Name.Contains("Death") || m.Name.Contains("Kill") || m.Name.Contains("Destroy"))
                            .Select(m => m.Name);
                        MonsterTrainAccessibility.LogInfo($"No standard death method found. Related methods: {string.Join(", ", relevantMethods)}");
                        MonsterTrainAccessibility.LogInfo("Death will be detected via damage patch when HP <= 0");
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
                int roomIndex = GetRoomIndex(__instance);
                int userFloor = RoomIndexToUserFloor(roomIndex);

                MonsterTrainAccessibility.BattleHandler?.OnUnitDied(unitName, isEnemy, userFloor);
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
                var type = characterState.GetType();
                var getNameMethod = type.GetMethod("GetName");
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(characterState, null) as string;
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }

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

        private static bool IsEnemyUnit(object characterState)
        {
            try
            {
                var getTeamMethod = characterState.GetType().GetMethod("GetTeamType");
                if (getTeamMethod != null)
                {
                    var team = getTeamMethod.Invoke(characterState, null);
                    return team?.ToString() == "Heroes";
                }
            }
            catch { }
            return false;
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

        private static int RoomIndexToUserFloor(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex > 2) return -1;
            return 3 - roomIndex;
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
    /// CharacterState.Setup is the most reliable, but name isn't available yet
    /// We use the Setup parameters to get CharacterData and read the name from there
    /// </summary>
    public static class UnitSpawnPatch
    {
        // Track announced spawns to avoid duplicates
        private static HashSet<int> _announcedSpawns = new HashSet<int>();
        private static float _lastClearTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try to patch MonsterManager.InstantiateCharacter for player units
                var monsterManagerType = AccessTools.TypeByName("MonsterManager");
                if (monsterManagerType != null)
                {
                    // Log available methods
                    var methods = monsterManagerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                    var spawnMethods = methods.Where(m => m.Name.Contains("Character") || m.Name.Contains("Spawn"))
                        .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
                    MonsterTrainAccessibility.LogInfo($"MonsterManager spawn-related methods: {string.Join(", ", spawnMethods)}");

                    var method = AccessTools.Method(monsterManagerType, "InstantiateCharacter") ??
                                 AccessTools.Method(monsterManagerType, "AddCharacter") ??
                                 AccessTools.Method(monsterManagerType, "SpawnCharacter");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(UnitSpawnPatch).GetMethod(nameof(PostfixMonster)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched MonsterManager.{method.Name}");
                    }
                }

                // Try to patch HeroManager for enemies
                var heroManagerType = AccessTools.TypeByName("HeroManager");
                if (heroManagerType != null)
                {
                    var methods = heroManagerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                    var spawnMethods = methods.Where(m => m.Name.Contains("Character") || m.Name.Contains("Spawn"))
                        .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
                    MonsterTrainAccessibility.LogInfo($"HeroManager spawn-related methods: {string.Join(", ", spawnMethods)}");

                    var method = AccessTools.Method(heroManagerType, "InstantiateCharacter") ??
                                 AccessTools.Method(heroManagerType, "AddCharacter") ??
                                 AccessTools.Method(heroManagerType, "SpawnCharacter");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(UnitSpawnPatch).GetMethod(nameof(PostfixHero)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched HeroManager.{method.Name}");
                    }
                }

                // Patch CharacterState.Setup - look at its parameters to get characterData
                var characterStateType = AccessTools.TypeByName("CharacterState");
                if (characterStateType != null)
                {
                    var method = AccessTools.Method(characterStateType, "Setup");
                    if (method != null)
                    {
                        // Log the parameters so we know what's available
                        var parameters = method.GetParameters();
                        MonsterTrainAccessibility.LogInfo($"CharacterState.Setup has {parameters.Length} parameters:");
                        foreach (var p in parameters)
                        {
                            MonsterTrainAccessibility.LogInfo($"  {p.Name}: {p.ParameterType.Name}");
                        }

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

        public static void PostfixMonster(object __instance, object __result)
        {
            try
            {
                // __result is usually the CharacterState that was created
                var character = __result;
                if (character == null) return;

                string name = GetUnitName(character);
                int roomIndex = GetRoomIndex(character);
                int userFloor = RoomIndexToUserFloor(roomIndex);

                MonsterTrainAccessibility.LogInfo($"Monster spawned: {name} on room {roomIndex} (floor {userFloor})");
                MonsterTrainAccessibility.BattleHandler?.OnUnitSpawned(name, false, userFloor);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in monster spawn patch: {ex.Message}");
            }
        }

        public static void PostfixHero(object __instance, object __result)
        {
            try
            {
                var character = __result;
                if (character == null) return;

                string name = GetUnitName(character);
                int roomIndex = GetRoomIndex(character);
                int userFloor = RoomIndexToUserFloor(roomIndex);

                MonsterTrainAccessibility.LogInfo($"Enemy spawned: {name} on room {roomIndex} (floor {userFloor})");
                MonsterTrainAccessibility.BattleHandler?.OnUnitSpawned(name, true, userFloor);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in hero spawn patch: {ex.Message}");
            }
        }

        // CharacterState.Setup likely has CharacterData as a parameter
        // Use __0 for the first parameter (likely CharacterData or similar)
        public static void PostfixCharacterSetup(object __instance, object __0)
        {
            try
            {
                // Clear old spawn tracking periodically
                float currentTime = UnityEngine.Time.unscaledTime;
                if (currentTime - _lastClearTime > 10f)
                {
                    _announcedSpawns.Clear();
                    _lastClearTime = currentTime;
                }

                // Try to get name from the first parameter (usually CharacterData or setup params)
                string name = null;
                bool isEnemy = IsEnemyUnit(__instance);
                int roomIndex = GetRoomIndex(__instance);

                // First try the first parameter (might be CharacterData)
                if (__0 != null)
                {
                    var paramType = __0.GetType();

                    // Try GetName on the parameter
                    var getNameMethod = paramType.GetMethod("GetName");
                    if (getNameMethod != null)
                    {
                        name = getNameMethod.Invoke(__0, null) as string;
                    }

                    // If that didn't work and it looks like a data object, try to access characterData
                    if (string.IsNullOrEmpty(name))
                    {
                        var charDataField = paramType.GetField("characterData") ??
                                           paramType.GetProperty("characterData")?.GetMethod as object;
                        if (charDataField != null)
                        {
                            // Try to read the field value
                        }
                    }
                }

                // Fallback to getting name from the instance
                if (string.IsNullOrEmpty(name))
                {
                    name = GetUnitName(__instance);
                }

                // Skip invalid names or duplicates
                if (string.IsNullOrEmpty(name) || name == "Unit" || name.Contains("KEY>"))
                {
                    MonsterTrainAccessibility.LogInfo($"CharacterState.Setup: skipping invalid name '{name}'");
                    return;
                }

                // Use instance hash to track duplicates
                int hash = __instance.GetHashCode();
                if (_announcedSpawns.Contains(hash))
                {
                    return;
                }
                _announcedSpawns.Add(hash);

                int userFloor = RoomIndexToUserFloor(roomIndex);
                MonsterTrainAccessibility.LogInfo($"Unit spawned via Setup: {name}, isEnemy={isEnemy}, floor={userFloor}");

                // Announce the spawn
                MonsterTrainAccessibility.BattleHandler?.OnUnitSpawned(name, isEnemy, userFloor);
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
                    if (!string.IsNullOrEmpty(name) && !name.Contains("KEY>"))
                        return name;
                }

                // Try GetCharacterData / GetCharacterDataRead
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
                    return team?.ToString() == "Heroes";
                }
            }
            catch { }
            return false;
        }

        private static int RoomIndexToUserFloor(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex > 2) return -1;
            return 3 - roomIndex;
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

    /// <summary>
    /// Detect enemy dialogue/chatter (speech bubbles like "These chains would suit you!")
    /// This hooks into the Chatter system to read enemy dialogue
    /// DisplayChatter signature: (ChatterExpressionType expressionType, CharacterState character, float delay, CharacterTriggerData+Trigger trigger)
    /// </summary>
    public static class EnemyDialoguePatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try to find the Chatter or ChatterUI class that displays speech bubbles
                var chatterType = AccessTools.TypeByName("Chatter");
                if (chatterType != null)
                {
                    // Look for method that sets/displays the chatter text
                    var method = AccessTools.Method(chatterType, "SetExpression") ??
                                 AccessTools.Method(chatterType, "ShowExpression") ??
                                 AccessTools.Method(chatterType, "DisplayChatter") ??
                                 AccessTools.Method(chatterType, "Play");

                    if (method != null)
                    {
                        // Log the parameters
                        var parameters = method.GetParameters();
                        MonsterTrainAccessibility.LogInfo($"Chatter.{method.Name} has {parameters.Length} parameters:");
                        foreach (var p in parameters)
                        {
                            MonsterTrainAccessibility.LogInfo($"  {p.Name}: {p.ParameterType.Name}");
                        }

                        var postfix = new HarmonyMethod(typeof(EnemyDialoguePatch).GetMethod(nameof(PostfixChatter)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched Chatter method: {method.Name}");
                    }
                }

                // Also try ChatterUI
                var chatterUIType = AccessTools.TypeByName("ChatterUI");
                if (chatterUIType != null)
                {
                    var method = AccessTools.Method(chatterUIType, "SetChatter") ??
                                 AccessTools.Method(chatterUIType, "DisplayChatter") ??
                                 AccessTools.Method(chatterUIType, "ShowChatter");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(EnemyDialoguePatch).GetMethod(nameof(PostfixChatterUI)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched ChatterUI method: {method.Name}");
                    }
                }

                // Try CharacterChatterData which stores the dialogue expressions
                var chatterDataType = AccessTools.TypeByName("CharacterChatterData");
                if (chatterDataType != null)
                {
                    // Log available methods for debugging
                    var methods = chatterDataType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                    MonsterTrainAccessibility.LogInfo($"CharacterChatterData methods: {string.Join(", ", methods.Where(m => m.Name.Contains("Expression") || m.Name.Contains("Chatter")).Select(m => m.Name))}");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch enemy dialogue: {ex.Message}");
            }
        }

        // Use positional parameters: __0 = expressionType (enum), __1 = character (CharacterState)
        public static void PostfixChatter(object __instance, object __0, object __1)
        {
            try
            {
                // __0 is the expression type enum, __1 is the CharacterState
                object expressionType = __0;
                object character = __1;

                if (expressionType == null) return;

                // Try to get the chatter data from the character
                string text = null;

                // First try to get text from the expression type
                text = GetExpressionText(expressionType);

                // If that didn't work, try to get the character's name and log the expression type
                if (string.IsNullOrEmpty(text))
                {
                    string charName = character != null ? GetCharacterName(character) : "Enemy";
                    string exprTypeName = expressionType.ToString();
                    MonsterTrainAccessibility.LogInfo($"Chatter: {charName} - {exprTypeName}");

                    // Don't announce if we couldn't get the actual text
                    return;
                }

                if (!string.IsNullOrEmpty(text))
                {
                    MonsterTrainAccessibility.BattleHandler?.OnEnemyDialogue(text);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in chatter patch: {ex.Message}");
            }
        }

        private static string GetCharacterName(object character)
        {
            try
            {
                var type = character.GetType();
                var getNameMethod = type.GetMethod("GetName");
                if (getNameMethod != null)
                {
                    return getNameMethod.Invoke(character, null) as string ?? "Enemy";
                }
            }
            catch { }
            return "Enemy";
        }

        public static void PostfixChatterUI(object __instance, string text)
        {
            try
            {
                if (!string.IsNullOrEmpty(text))
                {
                    MonsterTrainAccessibility.BattleHandler?.OnEnemyDialogue(text);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in chatter UI patch: {ex.Message}");
            }
        }

        private static string GetExpressionText(object expression)
        {
            try
            {
                var type = expression.GetType();

                // Try common property/method names for getting the text
                var getText = type.GetMethod("GetText") ?? type.GetMethod("GetLocalizedText");
                if (getText != null)
                {
                    var text = getText.Invoke(expression, null) as string;
                    if (!string.IsNullOrEmpty(text))
                        return text;
                }

                // Try text property
                var textProp = type.GetProperty("text") ?? type.GetProperty("Text");
                if (textProp != null)
                {
                    var text = textProp.GetValue(expression) as string;
                    if (!string.IsNullOrEmpty(text))
                        return text;
                }

                // Try localization key
                var getKey = type.GetMethod("GetLocalizationKey");
                if (getKey != null)
                {
                    var key = getKey.Invoke(expression, null) as string;
                    if (!string.IsNullOrEmpty(key))
                    {
                        // Try to localize
                        var localizeMethod = typeof(string).GetMethod("Localize", new Type[] { typeof(string) });
                        if (localizeMethod != null)
                        {
                            return localizeMethod.Invoke(null, new object[] { key }) as string ?? key;
                        }
                        return key;
                    }
                }
            }
            catch { }
            return null;
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
