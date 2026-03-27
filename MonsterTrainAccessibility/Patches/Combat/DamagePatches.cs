using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches
{
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
                // Skip if we're in preview mode (damage preview, not actual damage)
                if (PreviewModeDetector.ShouldSuppressAnnouncement(__1))
                {
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

                // Damage announcement is handled by UpdateHpPatch (which fires after HP is actually changed).
                // This postfix fires before the coroutine runs, so HP would be wrong here.

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
            return roomIndex;
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
                // Skip if we're in preview mode (damage preview, not actual damage)
                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
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

                // Damage/HP/death announcements are handled by UpdateHpPatch (which fires after HP
                // is actually changed). This postfix fires before the coroutine body runs, so
                // GetHP() still returns the pre-damage value.
                if (damageKey != _lastDamageKey || currentTime - _lastDamageTime > 0.2f)
                {
                    _lastDamageKey = damageKey;
                    _lastDamageTime = currentTime;
                    MonsterTrainAccessibility.LogInfo($"Combat damage (pre-coroutine): {attackerName ?? "Unknown"} deals {damage} to {targetName} (enemy={isTargetEnemy}), HP={currentHP}");
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
            return roomIndex;
        }
    }

    /// <summary>
    /// Patch CharacterState.UpdateHp (private, non-coroutine) to announce damage and death
    /// with correct HP values. This fires AFTER the HP is actually changed, unlike
    /// ApplyDamage/ApplyDamageToTarget which are coroutines where postfixes fire too early.
    /// </summary>
    public static class UpdateHpPatch
    {
        // Store old HP per character for computing damage in postfix
        private static Dictionary<int, int> _preHpTracker = new Dictionary<int, int>();
        // Dedup: track last announcement to avoid repeats
        private static string _lastAnnounceKey = "";
        private static float _lastAnnounceTime = 0f;
        // Track recently announced deaths to avoid duplicates with UnitDeathPatch
        public static Dictionary<int, float> RecentDeaths = new Dictionary<int, float>();
        // Cached reflection for attacker lookup
        private static MethodInfo _getLastAttackerMethod;
        private static bool _reflectionCached;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType == null) return;

                // UpdateHp is private void UpdateHp(int newAmount)
                var method = AccessTools.Method(characterType, "UpdateHp", new Type[] { typeof(int) });
                if (method != null)
                {
                    var prefix = new HarmonyMethod(typeof(UpdateHpPatch).GetMethod(nameof(Prefix)));
                    var postfix = new HarmonyMethod(typeof(UpdateHpPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, prefix: prefix, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched CharacterState.UpdateHp for accurate HP tracking");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("CharacterState.UpdateHp not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch UpdateHp: {ex.Message}");
            }
        }

        public static void Prefix(object __instance, int __0)
        {
            try
            {
                if (__instance == null) return;
                int hash = __instance.GetHashCode();
                int currentHP = CharacterStateHelper.GetCurrentHP(__instance);
                _preHpTracker[hash] = currentHP;
            }
            catch { }
        }

        public static void Postfix(object __instance, int __0)
        {
            try
            {
                if (__instance == null) return;

                // Skip if in preview mode (includes floor targeting check)
                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                    return;

                // Skip if not in battle
                if (MonsterTrainAccessibility.BattleHandler == null || !MonsterTrainAccessibility.BattleHandler.IsInBattle)
                    return;

                int hash = __instance.GetHashCode();
                int newHP = __0; // The parameter passed to UpdateHp is the new HP value

                if (!_preHpTracker.TryGetValue(hash, out int oldHP))
                    return;

                // No change (UpdateHp returned early before setting)
                if (oldHP == newHP)
                    return;

                // HP decreased = damage
                if (newHP < oldHP)
                {
                    int damage = oldHP - newHP;
                    string targetName = CharacterStateHelper.GetUnitName(__instance);
                    bool isEnemy = CharacterStateHelper.IsEnemyUnit(__instance);

                    // Try to get attacker name from lastAttackerCharacter
                    string attackerName = GetLastAttackerName(__instance);

                    // Dedup check
                    float currentTime = UnityEngine.Time.unscaledTime;
                    string announceKey = $"{attackerName}_{targetName}_{damage}_{newHP}";
                    if (announceKey == _lastAnnounceKey && currentTime - _lastAnnounceTime < 0.3f)
                        return;
                    _lastAnnounceKey = announceKey;
                    _lastAnnounceTime = currentTime;

                    // Build announcement
                    string announcement;
                    if (!string.IsNullOrEmpty(attackerName) && attackerName != "Unit")
                    {
                        announcement = $"{attackerName} hits {targetName} for {damage}";
                    }
                    else
                    {
                        announcement = $"{targetName} takes {damage} damage";
                    }

                    if (newHP > 0)
                    {
                        announcement += $", {newHP} HP left";
                    }

                    MonsterTrainAccessibility.ScreenReader?.Queue(announcement);

                    // Death detection
                    if (newHP <= 0)
                    {
                        int roomIndex = CharacterStateHelper.GetRoomIndex(__instance);
                        RecentDeaths[hash] = currentTime;
                        MonsterTrainAccessibility.BattleHandler?.OnUnitDied(targetName, isEnemy, roomIndex);

                        // Clean up old death entries
                        var keysToRemove = new System.Collections.Generic.List<int>();
                        foreach (var kv in RecentDeaths)
                        {
                            if (currentTime - kv.Value > 2f)
                                keysToRemove.Add(kv.Key);
                        }
                        foreach (var key in keysToRemove)
                            RecentDeaths.Remove(key);
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in UpdateHp patch: {ex.Message}");
            }
        }

        private static string GetLastAttackerName(object characterState)
        {
            try
            {
                if (!_reflectionCached)
                {
                    _reflectionCached = true;
                    var type = characterState.GetType();
                    _getLastAttackerMethod = type.GetMethod("GetLastAttackerCharacter", Type.EmptyTypes);
                }

                if (_getLastAttackerMethod != null)
                {
                    var attacker = _getLastAttackerMethod.Invoke(characterState, null);
                    if (attacker != null)
                    {
                        return CharacterStateHelper.GetUnitName(attacker);
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
