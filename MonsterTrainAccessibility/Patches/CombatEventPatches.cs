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
        // Track HP before damage to filter out previews
        private static Dictionary<int, int> _preHpTracker = new Dictionary<int, int>();

        // Track recently damaged targets for death correlation
        // Key = target hash, Value = time of damage announcement
        public static Dictionary<int, float> RecentlyDamagedTargets = new Dictionary<int, float>();

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

                        var prefix = new HarmonyMethod(typeof(DamageAppliedPatch).GetMethod(nameof(Prefix)));
                        var postfix = new HarmonyMethod(typeof(DamageAppliedPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, prefix: prefix, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched damage method: {method.Name} (with preview filter)");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch damage: {ex.Message}");
            }
        }

        // PREFIX: Record HP before damage - __1 is the target CharacterState
        public static void Prefix(object __1)
        {
            try
            {
                if (__1 == null) return;
                int hash = __1.GetHashCode();
                int hp = GetCurrentHP(__1);
                _preHpTracker[hash] = hp;
            }
            catch { }
        }

        // Use positional parameters - ApplyDamageToTarget may take a struct parameter
        public static void Postfix(object __0, object __1, object __2)
        {
            try
            {
                // Skip if we're in floor targeting mode (this is a preview, not actual damage)
                var targeting = MonsterTrainAccessibility.FloorTargeting;
                if (targeting != null && targeting.IsTargeting)
                {
                    MonsterTrainAccessibility.LogInfo("DamageAppliedPatch: Skipping - floor targeting active (preview)");
                    return;
                }

                // Try to extract damage and target from the parameters
                int damage = 0;
                object target = null;

                // If __0 is the damage amount (int)
                if (__0 is int dmg)
                {
                    damage = dmg;
                    target = __1;
                }
                // If __0 is a parameters struct, try to extract from it
                else if (__0 != null)
                {
                    var paramsType = __0.GetType();

                    // Try to get damage from struct
                    var damageField = paramsType.GetField("damage", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (damageField != null && damageField.GetValue(__0) is int d)
                        damage = d;

                    // Try to get target from struct
                    var targetField = paramsType.GetField("target", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (targetField != null)
                        target = targetField.GetValue(__0);
                }

                if (damage <= 0 || target == null)
                {
                    return;
                }

                string targetName = GetUnitName(target);
                bool isEnemy = IsEnemyUnit(target);
                float currentTime = UnityEngine.Time.unscaledTime;
                int targetHash = target.GetHashCode();

                // Track this damage call - duplicate filtering using delay
                // If we see the same target+damage within 0.3s, skip (likely duplicate call)
                string damageKey = $"{targetName}_{damage}";

                if (damageKey == _lastDamageKey && currentTime - _lastDamageTime < 0.3f)
                {
                    return;
                }

                _lastDamageKey = damageKey;
                _lastDamageTime = currentTime;

                // Announce damage
                MonsterTrainAccessibility.ScreenReader?.Queue($"{targetName} takes {damage} damage");

                // Record this target as recently damaged for death correlation
                RecentlyDamagedTargets[targetHash] = currentTime;

                // Clean up old entries (older than 2 seconds)
                var keysToRemove = RecentlyDamagedTargets.Where(kv => currentTime - kv.Value > 2f).Select(kv => kv.Key).ToList();
                foreach (var key in keysToRemove)
                    RecentlyDamagedTargets.Remove(key);
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
    /// Detect combat damage via CharacterState.ApplyDamage
    /// This catches melee combat damage that doesn't go through CombatManager.ApplyDamageToTarget
    /// Signature: ApplyDamage(int damage, ApplyDamageParams damageParams)
    /// </summary>
    public static class CharacterDamagePatch
    {
        private static float _lastDamageTime = 0f;
        private static string _lastDamageKey = "";

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType == null) return;

                // List all ApplyDamage overloads for debugging
                var allMethods = characterType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == "ApplyDamage")
                    .ToList();

                MonsterTrainAccessibility.LogInfo($"Found {allMethods.Count} ApplyDamage overloads:");
                foreach (var m in allMethods)
                {
                    var ps = m.GetParameters();
                    var sig = string.Join(", ", ps.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    MonsterTrainAccessibility.LogInfo($"  ApplyDamage({sig})");
                }

                // Find the ApplyDamageParams type
                var applyDamageParamsType = AccessTools.TypeByName("ApplyDamageParams");

                // Try to find the overload that takes (int damage, ApplyDamageParams params)
                MethodInfo method = null;
                if (applyDamageParamsType != null)
                {
                    method = characterType.GetMethod("ApplyDamage",
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new Type[] { typeof(int), applyDamageParamsType },
                        null);
                }

                // Fallback: try to find any ApplyDamage with an int first parameter
                if (method == null)
                {
                    method = allMethods.FirstOrDefault(m =>
                    {
                        var ps = m.GetParameters();
                        return ps.Length >= 1 && ps[0].ParameterType == typeof(int);
                    });
                }

                // Last fallback: use AccessTools
                if (method == null)
                {
                    method = AccessTools.Method(characterType, "ApplyDamage");
                }

                if (method != null)
                {
                    var parameters = method.GetParameters();
                    MonsterTrainAccessibility.LogInfo($"Patching ApplyDamage with {parameters.Length} parameters:");
                    foreach (var p in parameters)
                    {
                        MonsterTrainAccessibility.LogInfo($"  {p.Name}: {p.ParameterType.Name}");
                    }

                    var prefix = new HarmonyMethod(typeof(CharacterDamagePatch).GetMethod(nameof(Prefix)));
                    var postfix = new HarmonyMethod(typeof(CharacterDamagePatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, prefix: prefix, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched CharacterState.ApplyDamage for combat damage (with preview filter)");
                }
                else
                {
                    MonsterTrainAccessibility.LogWarning("Could not find suitable ApplyDamage method to patch");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CharacterState.ApplyDamage: {ex.Message}");
            }
        }

        // Track HP before damage to detect if it's a preview (preview doesn't change HP)
        private static Dictionary<int, int> _preHpTracker = new Dictionary<int, int>();

        // PREFIX: Record HP before damage is applied
        public static void Prefix(object __instance)
        {
            try
            {
                if (__instance == null) return;
                int hash = __instance.GetHashCode();
                int hp = GetCurrentHP(__instance);
                _preHpTracker[hash] = hp;
            }
            catch { }
        }

        // POSTFIX: Announce combat damage
        // __instance is the CharacterState receiving damage, __0 is damage amount, __1 is ApplyDamageParams
        public static void Postfix(object __instance, int __0, object __1)
        {
            try
            {
                // Skip if we're in floor targeting mode (this is a preview, not actual damage)
                var targeting = MonsterTrainAccessibility.FloorTargeting;
                if (targeting != null && targeting.IsTargeting)
                {
                    return;
                }

                int damage = __0;
                object target = __instance;
                object damageParams = __1;

                MonsterTrainAccessibility.LogInfo($"CharacterDamagePatch.Postfix called: damage={damage}");

                if (damage <= 0 || target == null) return;

                int currentHP = GetCurrentHP(target);
                string targetName = GetUnitName(target);
                bool isTargetEnemy = IsEnemyUnit(target);

                // Try to get attacker name from damageParams
                string attackerName = null;
                if (damageParams != null)
                {
                    attackerName = GetAttackerName(damageParams);
                }

                // Create a key to prevent duplicate announcements
                string damageKey = $"{attackerName}_{targetName}_{damage}_{currentHP}";
                float currentTime = UnityEngine.Time.unscaledTime;

                if (damageKey != _lastDamageKey || currentTime - _lastDamageTime > 0.2f)
                {
                    _lastDamageKey = damageKey;
                    _lastDamageTime = currentTime;

                    MonsterTrainAccessibility.LogInfo($"Combat damage: {attackerName ?? "Unknown"} deals {damage} to {targetName} (enemy={isTargetEnemy}), HP now {currentHP}");

                    // Build the announcement
                    string announcement;
                    if (!string.IsNullOrEmpty(attackerName) && attackerName != "Unknown")
                    {
                        announcement = $"{attackerName} hits {targetName} for {damage}";
                    }
                    else
                    {
                        announcement = $"{targetName} takes {damage} damage";
                    }

                    // Add HP info for friendly units
                    if (!isTargetEnemy && currentHP > 0)
                    {
                        announcement += $", {currentHP} HP left";
                    }

                    MonsterTrainAccessibility.ScreenReader?.Queue(announcement);

                    // Check for death
                    if (currentHP <= 0)
                    {
                        int roomIndex = GetRoomIndex(target);
                        int userFloor = RoomIndexToUserFloor(roomIndex);
                        MonsterTrainAccessibility.BattleHandler?.OnUnitDied(targetName, isTargetEnemy, userFloor);
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CharacterState.ApplyDamage patch: {ex.Message}");
            }
        }

        private static string GetAttackerName(object damageParams)
        {
            try
            {
                var paramsType = damageParams.GetType();

                // Try to get the attacker field
                var attackerField = paramsType.GetField("attacker", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (attackerField != null)
                {
                    var attacker = attackerField.GetValue(damageParams);
                    if (attacker != null)
                    {
                        return GetUnitName(attacker);
                    }
                }
            }
            catch { }
            return null;
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
                    if (!string.IsNullOrEmpty(name) && !name.Contains("KEY>"))
                        return name;
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
                // Skip if we're in floor targeting mode (this is a preview, not actual death)
                var targeting = MonsterTrainAccessibility.FloorTargeting;
                if (targeting != null && targeting.IsTargeting)
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

                string unitName = GetUnitName(__instance);
                bool isEnemy = IsEnemyUnit(__instance);
                int roomIndex = GetRoomIndex(__instance);
                int userFloor = RoomIndexToUserFloor(roomIndex);

                MonsterTrainAccessibility.LogInfo($"Unit died: {unitName} (enemy={isEnemy}) on floor {userFloor}");
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

                int userFloor = RoomIndexToUserFloor(roomIndex);

                // Build a proper announcement
                string floorText = userFloor > 0 ? $"floor {userFloor}" : "the battlefield";
                string unitType = isEnemy ? "Enemy" : "Friendly";

                MonsterTrainAccessibility.LogInfo($"Unit spawned via Setup: {name}, isEnemy={isEnemy}, roomIndex={roomIndex}, floor={userFloor}");

                // Announce the spawn with proper floor text
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
