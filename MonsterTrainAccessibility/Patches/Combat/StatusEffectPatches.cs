using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches
{
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
                    // We must patch the 3-param overload (string, int, AddStatusEffectParams)
                    // because that's what the game calls directly from card effects.
                    // The 2-param overload is just a wrapper that's rarely called.
                    var methods = characterType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    foreach (var m in methods)
                    {
                        if (m.Name == "AddStatusEffect")
                        {
                            var parameters = m.GetParameters();
                            // Prefer the 3-param overload (string statusId, int numStacks, AddStatusEffectParams)
                            if (parameters.Length == 3 &&
                                parameters[0].ParameterType == typeof(string) &&
                                parameters[1].ParameterType == typeof(int))
                            {
                                method = m;
                                break;
                            }
                            // Fall back to any overload if we can't find the 3-param one
                            if (method == null)
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

                // Skip if we're in preview mode
                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
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

                // Get localized name (e.g., "poison" -> "Frostbite", "armor" -> "Armor")
                string effectName = CharacterStateHelper.CleanStatusName(statusId);

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
    /// Detect when a status effect is removed from a unit.
    /// Hooks CharacterState.RemoveStatusEffect(string statusId, bool removeAtEndOfTurn, int numStacks, ...)
    /// </summary>
    public static class StatusEffectRemovedPatch
    {
        private static string _lastRemovedEffect = "";
        private static float _lastRemovedTime = 0f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType == null) return;

                var method = AccessTools.Method(characterType, "RemoveStatusEffect");
                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(StatusEffectRemovedPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched CharacterState.RemoveStatusEffect");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch RemoveStatusEffect: {ex.Message}");
            }
        }

        // __instance = CharacterState, __0 = statusId, __1 = removeAtEndOfTurn, __2 = numStacks
        public static void Postfix(object __instance, string __0, bool __1, int __2)
        {
            try
            {
                if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceStatusEffects.Value)
                    return;

                if (string.IsNullOrEmpty(__0) || __2 <= 0)
                    return;

                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                    return;

                string unitName = CharacterStateHelper.GetUnitName(__instance);
                string effectName = CharacterStateHelper.CleanStatusName(__0);

                // Deduplicate
                string effectKey = $"{unitName}_{__0}_{__2}_remove";
                float currentTime = UnityEngine.Time.unscaledTime;
                if (effectKey == _lastRemovedEffect && currentTime - _lastRemovedTime < 0.5f)
                    return;

                _lastRemovedEffect = effectKey;
                _lastRemovedTime = currentTime;

                MonsterTrainAccessibility.BattleHandler?.OnStatusEffectRemoved(unitName, effectName, __2);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in status effect removed patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect when a unit's max HP is buffed.
    /// Hooks CharacterState.BuffMaxHP(int amount, bool triggerOnHeal, RelicState relicState)
    /// This is a coroutine (IEnumerator), so we use prefix to capture params.
    /// </summary>
    public static class MaxHPBuffPatch
    {
        private static float _lastBuffTime = 0f;
        private static string _lastBuffKey = "";

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType == null) return;

                var method = AccessTools.Method(characterType, "BuffMaxHP");
                if (method != null)
                {
                    var prefix = new HarmonyMethod(typeof(MaxHPBuffPatch).GetMethod(nameof(Prefix)));
                    harmony.Patch(method, prefix: prefix);
                    MonsterTrainAccessibility.LogInfo("Patched CharacterState.BuffMaxHP");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch BuffMaxHP: {ex.Message}");
            }
        }

        // __instance = CharacterState, __0 = amount (int)
        public static void Prefix(object __instance, int __0)
        {
            try
            {
                if (__0 <= 0 || __instance == null) return;

                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                    return;

                string unitName = CharacterStateHelper.GetUnitName(__instance);

                // Deduplicate
                float currentTime = UnityEngine.Time.unscaledTime;
                string buffKey = $"{unitName}_{__0}";
                if (buffKey == _lastBuffKey && currentTime - _lastBuffTime < 0.3f)
                    return;

                _lastBuffKey = buffKey;
                _lastBuffTime = currentTime;

                MonsterTrainAccessibility.BattleHandler?.OnMaxHPBuffed(unitName, __0);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in max HP buff patch: {ex.Message}");
            }
        }
    }
}
