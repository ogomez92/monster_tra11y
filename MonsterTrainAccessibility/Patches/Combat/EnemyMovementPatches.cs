using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Detect when enemies ascend floors by tracking spawn point changes
    /// </summary>
    public static class EnemyAscendPatch
    {
        private static int _lastAnnouncedRoomIndex = -1;
        private static float _lastAscendTime = 0f;
        private static bool _spawnPointFieldsLogged = false;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try to patch OnSpawnPointChanged on HeroManager (called when enemies move floors)
                var heroManagerType = AccessTools.TypeByName("HeroManager");
                if (heroManagerType != null)
                {
                    var method = AccessTools.Method(heroManagerType, "OnSpawnPointChanged");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(EnemyAscendPatch).GetMethod(nameof(OnSpawnPointChangedPostfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched HeroManager.OnSpawnPointChanged for ascend detection");
                    }
                }

                // Also try PostAscensionDescensionSingularCharacterTrigger
                if (heroManagerType != null)
                {
                    var method = AccessTools.Method(heroManagerType, "PostAscensionDescensionSingularCharacterTrigger");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(EnemyAscendPatch).GetMethod(nameof(PostAscensionPostfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched HeroManager.PostAscensionDescensionSingularCharacterTrigger");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch enemy ascend: {ex.Message}");
            }
        }

        public static void OnSpawnPointChangedPostfix(object __0, object __1, object __2)
        {
            try
            {
                // __0 is CharacterState, __1 is old SpawnPoint, __2 is new SpawnPoint
                // SpawnPoint format appears to be (room, position) e.g., (2,0) = room 2, position 0
                MonsterTrainAccessibility.LogInfo($"OnSpawnPointChangedPostfix called: __0={__0?.GetType().Name ?? "null"}, __1={__1}, __2={__2}");

                if (__0 == null || __2 == null)
                {
                    MonsterTrainAccessibility.LogInfo("  Early return: character or new spawn point is null");
                    return;
                }

                // Check if this is an initial spawn (old spawn point is empty/default)
                string oldSpawnStr = __1?.ToString() ?? "";
                string newSpawnStr = __2.ToString();
                bool isInitialSpawn = string.IsNullOrEmpty(oldSpawnStr) && !string.IsNullOrEmpty(newSpawnStr);

                if (isInitialSpawn)
                {
                    // Parse new spawn point (room, position) format
                    // Extract room index from format like "(2,0)"
                    int roomIndex = -1;
                    if (newSpawnStr.StartsWith("(") && newSpawnStr.Contains(","))
                    {
                        string roomPart = newSpawnStr.Substring(1, newSpawnStr.IndexOf(',') - 1);
                        int.TryParse(roomPart, out roomIndex);
                    }

                    // Get character name
                    string charName = "Enemy";
                    var spawnCharType = __0.GetType();
                    var getNameMethod = spawnCharType.GetMethod("GetName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                    {
                        charName = getNameMethod.Invoke(__0, null) as string ?? "Enemy";
                    }

                    int floor = RoomIndexToUserFloor(roomIndex);
                    MonsterTrainAccessibility.LogInfo($"Initial enemy spawn: {charName} on floor {floor} (room {roomIndex})");
                    MonsterTrainAccessibility.BattleHandler?.OnUnitSpawned(charName, true, floor);
                    return;
                }

                // For non-initial spawns, __1 must be valid
                if (__1 == null || string.IsNullOrEmpty(oldSpawnStr))
                {
                    return;
                }

                var spawnType = __1.GetType();
                var charType = __0.GetType();

                // Log all fields on SpawnPoint once
                if (!_spawnPointFieldsLogged)
                {
                    MonsterTrainAccessibility.LogInfo($"SpawnPoint type: {spawnType.FullName}");
                    foreach (var f in spawnType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                    {
                        MonsterTrainAccessibility.LogInfo($"  SpawnPoint field: {f.Name} ({f.FieldType.Name})");
                    }
                    _spawnPointFieldsLogged = true;
                }

                // Parse room indices from SpawnPoint format "(room,position)"
                int oldRoomIndex = -1;
                int newRoomIndex = -1;

                if (oldSpawnStr.StartsWith("(") && oldSpawnStr.Contains(","))
                {
                    string roomPart = oldSpawnStr.Substring(1, oldSpawnStr.IndexOf(',') - 1);
                    int.TryParse(roomPart, out oldRoomIndex);
                }
                if (newSpawnStr.StartsWith("(") && newSpawnStr.Contains(","))
                {
                    string roomPart = newSpawnStr.Substring(1, newSpawnStr.IndexOf(',') - 1);
                    int.TryParse(roomPart, out newRoomIndex);
                }

                MonsterTrainAccessibility.LogInfo($"  Room change: {oldRoomIndex} -> {newRoomIndex}");

                // If enemy moved to a higher room index (ascending towards pyre: Room 0 -> 1 -> 2 -> 3/Pyre)
                if (newRoomIndex >= 0 && oldRoomIndex >= 0 && newRoomIndex > oldRoomIndex)
                {
                    // Get character name
                    string charName = "Enemy";
                    var getNameMethod = charType.GetMethod("GetName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                    {
                        charName = getNameMethod.Invoke(__0, null) as string ?? "Enemy";
                    }

                    int newFloor = RoomIndexToUserFloor(newRoomIndex);

                    // Debounce - don't spam if multiple enemies ascend at once
                    float currentTime = UnityEngine.Time.time;
                    if (currentTime - _lastAscendTime > 0.5f || newRoomIndex != _lastAnnouncedRoomIndex)
                    {
                        MonsterTrainAccessibility.LogInfo($"Enemy ascended: {charName} to floor {newFloor}");
                        MonsterTrainAccessibility.BattleHandler?.OnEnemyAscended(charName, newFloor);
                        _lastAscendTime = currentTime;
                        _lastAnnouncedRoomIndex = newRoomIndex;
                    }
                }
                else if (newRoomIndex >= 0 && oldRoomIndex >= 0 && newRoomIndex < oldRoomIndex)
                {
                    // Enemy descended (bumped down a floor)
                    string charName = "Enemy";
                    var getNameMethod = charType.GetMethod("GetName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                    {
                        charName = getNameMethod.Invoke(__0, null) as string ?? "Enemy";
                    }

                    int newFloor = RoomIndexToUserFloor(newRoomIndex);

                    float currentTime = UnityEngine.Time.time;
                    if (currentTime - _lastAscendTime > 0.5f || newRoomIndex != _lastAnnouncedRoomIndex)
                    {
                        MonsterTrainAccessibility.LogInfo($"Enemy descended: {charName} to floor {newFloor}");
                        MonsterTrainAccessibility.BattleHandler?.OnEnemyDescended(charName, newFloor);
                        _lastAscendTime = currentTime;
                        _lastAnnouncedRoomIndex = newRoomIndex;
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in OnSpawnPointChanged patch: {ex.Message}");
            }
        }

        public static void PostAscensionPostfix(object __0, object __1, bool __2)
        {
            try
            {
                // __0 is CharacterState, __1 is BumpDirection, __2 is some bool
                if (__0 == null) return;

                var charType = __0.GetType();

                // Get character name
                string charName = "Enemy";
                var getNameMethod = charType.GetMethod("GetName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                {
                    charName = getNameMethod.Invoke(__0, null) as string ?? "Enemy";
                }

                // Get current room
                int roomIndex = -1;
                var spawnPointProp = charType.GetProperty("SpawnPoint");
                if (spawnPointProp != null)
                {
                    var spawnPoint = spawnPointProp.GetValue(__0);
                    if (spawnPoint != null)
                    {
                        var roomField = spawnPoint.GetType().GetField("roomIndex", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (roomField != null)
                        {
                            roomIndex = (int)roomField.GetValue(spawnPoint);
                        }
                    }
                }

                int userFloor = RoomIndexToUserFloor(roomIndex);

                // Debounce
                float currentTime = UnityEngine.Time.time;
                if (currentTime - _lastAscendTime > 0.3f)
                {
                    MonsterTrainAccessibility.LogInfo($"Post-ascension trigger: {charName} on floor {userFloor}");
                    // Don't announce here - OnSpawnPointChanged should handle it
                    _lastAscendTime = currentTime;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in PostAscension patch: {ex.Message}");
            }
        }

        private static int RoomIndexToUserFloor(int roomIndex)
        {
            if (roomIndex < 0) return -1;
            return roomIndex;
        }
    }

    /// <summary>
    /// Detect when a floor gets frozen/disabled (RoomState.SetRoomEnabled)
    /// </summary>
    public static class RoomDisabledPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var roomStateType = AccessTools.TypeByName("RoomState");
                if (roomStateType == null) return;

                var method = AccessTools.Method(roomStateType, "SetRoomEnabled", new Type[] { typeof(bool) });
                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(RoomDisabledPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched RoomState.SetRoomEnabled");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch SetRoomEnabled: {ex.Message}");
            }
        }

        public static void Postfix(object __instance, bool __0)
        {
            try
            {
                if (MonsterTrainAccessibility.BattleHandler == null || !MonsterTrainAccessibility.BattleHandler.IsInBattle)
                    return;

                bool enable = __0;
                if (enable) return; // Only announce when floor gets disabled

                // Get room index
                var getRoomIndexMethod = __instance.GetType().GetMethod("GetRoomIndex", Type.EmptyTypes);
                if (getRoomIndexMethod == null) return;

                int roomIndex = (int)getRoomIndexMethod.Invoke(__instance, null);
                string floorName = Screens.BattleAccessibility.RoomIndexToFloorName(roomIndex);

                string announcement = $"{floorName} frozen";
                MonsterTrainAccessibility.ScreenReader?.Queue(announcement);
                MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(announcement);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in SetRoomEnabled patch: {ex.Message}");
            }
        }
    }
}
