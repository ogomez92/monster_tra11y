using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches
{
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
                    // InnerCharacterDeath is the actual death method in Monster Train
                    var deathMethods = new[] { "InnerCharacterDeath", "Die", "Kill", "OnDeath", "ProcessDeath", "HandleDeath" };
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
                // Skip if we're in preview mode (preview death, not actual death)
                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                {
                    return;
                }

                // Only announce death if this target was recently damaged
                // This filters out preview deaths (where InnerCharacterDeath is called during card preview)
                int targetHash = __instance.GetHashCode();
                float currentTime = UnityEngine.Time.unscaledTime;

                if (!DamageAppliedPatch.RecentlyDamagedTargets.TryGetValue(targetHash, out float damageTime))
                {
                    // This target wasn't recently damaged - probably a preview
                    return;
                }

                // Remove from tracking since we're handling the death
                DamageAppliedPatch.RecentlyDamagedTargets.Remove(targetHash);

                // Only announce if death happened within 1 second of damage
                if (currentTime - damageTime > 1f)
                {
                    return;
                }

                // Check if UpdateHpPatch already announced this death
                int targetHash2 = __instance.GetHashCode();
                if (UpdateHpPatch.RecentDeaths.TryGetValue(targetHash2, out float deathTime) && currentTime - deathTime < 1f)
                {
                    // Already announced by UpdateHpPatch
                    return;
                }

                string unitName = GetUnitName(__instance);
                bool isEnemy = IsEnemyUnit(__instance);
                int roomIndex = GetRoomIndex(__instance);
                int userFloor = RoomIndexToUserFloor(roomIndex);

                MonsterTrainAccessibility.LogInfo($"Unit died (via death patch): {unitName} (enemy={isEnemy}) on floor {userFloor}");
                MonsterTrainAccessibility.BattleHandler?.OnUnitDied(unitName, isEnemy, userFloor);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in death patch: {ex.Message}");
            }
        }

        private static int GetCurrentHP(object characterState)
        {
            try
            {
                var type = characterState.GetType();
                var getHPMethod = type.GetMethod("GetHP");
                if (getHPMethod != null)
                {
                    var result = getHPMethod.Invoke(characterState, null);
                    if (result is int hp)
                        return hp;
                }
            }
            catch { }
            return -1; // Unknown HP, treat as potentially dead
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
            return roomIndex;
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

                // Skip if the character isn't fully initialized yet - CharacterState.Setup will handle it
                if (string.IsNullOrEmpty(name) || name == "Unit" || roomIndex < 0)
                {
                    MonsterTrainAccessibility.LogInfo($"Monster spawned: {name} on room {roomIndex} (floor {userFloor}) - skipping, not fully initialized");
                    return;
                }

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

                // Skip if the character isn't fully initialized yet - CharacterState.Setup will handle it
                if (string.IsNullOrEmpty(name) || name == "Unit" || roomIndex < 0)
                {
                    MonsterTrainAccessibility.LogInfo($"Enemy spawned: {name} on room {roomIndex} (floor {userFloor}) - skipping, not fully initialized");
                    return;
                }

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

                // Use instance hash to track duplicates - check early
                int hash = __instance.GetHashCode();
                if (_announcedSpawns.Contains(hash))
                {
                    return;
                }

                string name = null;
                bool isEnemy = IsEnemyUnit(__instance);
                int roomIndex = -1;

                // After Setup completes, the instance should have the name available
                // Try GetName on the instance first (most reliable after setup)
                var instanceType = __instance.GetType();
                var getNameMethod = instanceType.GetMethod("GetName");
                if (getNameMethod != null)
                {
                    name = getNameMethod.Invoke(__instance, null) as string;
                }

                // If instance GetName failed, try the first parameter (CharacterData)
                if ((string.IsNullOrEmpty(name) || name == "Unit" || name.Contains("KEY>")) && __0 != null)
                {
                    var paramType = __0.GetType();
                    var paramGetNameMethod = paramType.GetMethod("GetName");
                    if (paramGetNameMethod != null)
                    {
                        var paramName = paramGetNameMethod.Invoke(__0, null) as string;
                        if (!string.IsNullOrEmpty(paramName) && paramName != "Unit" && !paramName.Contains("KEY>"))
                        {
                            name = paramName;
                        }
                    }
                }

                // Get room index from instance
                roomIndex = GetRoomIndex(__instance);

                // Skip invalid names
                if (string.IsNullOrEmpty(name) || name == "Unit" || name.Contains("KEY>"))
                {
                    MonsterTrainAccessibility.LogInfo($"CharacterState.Setup: skipping invalid name '{name}'");
                    return;
                }

                // Track this spawn
                _announcedSpawns.Add(hash);

                int floorIndex = RoomIndexToUserFloor(roomIndex);

                MonsterTrainAccessibility.LogInfo($"Unit spawned via Setup: {name}, isEnemy={isEnemy}, roomIndex={roomIndex}");

                // Announce the spawn - BattleAccessibility handles floor name display
                MonsterTrainAccessibility.BattleHandler?.OnUnitSpawned(name, isEnemy, floorIndex);
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
            return roomIndex;
        }
    }
}
