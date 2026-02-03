using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Patches for card targeting - announces when unit targeting starts
    /// and when the player navigates between targets with Left/Right arrows.
    /// The game handles targeting natively; we just provide audio feedback.
    /// </summary>
    public static class CardTargetingPatches
    {
        // Cached reflection for reading targeting state from CardSelectionBehaviour
        private static FieldInfo _possibleTargetsField;
        private static FieldInfo _keyboardSelectedTargetIndexField;
        private static FieldInfo _behaviorStateField;
        private static FieldInfo _focusedCardStateField;
        private static MethodInfo _getParamCharacterStateMethod;
        private static MethodInfo _getParamSpawnPointUIMethod;
        private static bool _reflectionInitialized;

        // Track state to detect transitions
        private static int _lastAnnouncedTargetIndex = -1;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CardSelectionBehaviour");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogError("CardTargetingPatches: CardSelectionBehaviour type not found");
                    return;
                }

                // Patch MoveTargetWithKeyboard to announce target changes
                var moveTargetMethod = AccessTools.Method(targetType, "MoveTargetWithKeyboard");
                if (moveTargetMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(CardTargetingPatches).GetMethod(nameof(MoveTargetPostfix)));
                    harmony.Patch(moveTargetMethod, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched CardSelectionBehaviour.MoveTargetWithKeyboard");
                }
                else
                {
                    MonsterTrainAccessibility.LogWarning("CardTargetingPatches: MoveTargetWithKeyboard method not found");
                }

                // Patch SelectCardInternal to announce when targeting starts
                var selectCardMethod = AccessTools.Method(targetType, "SelectCardInternal",
                    new Type[] { typeof(bool), typeof(bool) });
                if (selectCardMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(CardTargetingPatches).GetMethod(nameof(SelectCardPostfix)));
                    harmony.Patch(selectCardMethod, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched CardSelectionBehaviour.SelectCardInternal");
                }
                else
                {
                    MonsterTrainAccessibility.LogWarning("CardTargetingPatches: SelectCardInternal method not found");
                }

                InitReflection(targetType);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CardTargeting: {ex.Message}");
            }
        }

        private static void InitReflection(Type cardSelectionType)
        {
            if (_reflectionInitialized) return;
            _reflectionInitialized = true;

            try
            {
                _possibleTargetsField = cardSelectionType.GetField("possibleTargets",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                _keyboardSelectedTargetIndexField = cardSelectionType.GetField("keyboardSelectedTargetIndex",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                _behaviorStateField = cardSelectionType.GetField("behaviorState",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                _focusedCardStateField = cardSelectionType.GetField("focusedCardState",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                // Get TargetWrapper methods
                var targetWrapperType = AccessTools.TypeByName("TargetValidator+TargetWrapper");
                if (targetWrapperType != null)
                {
                    _getParamCharacterStateMethod = targetWrapperType.GetMethod("GetParamCharacterState");
                    _getParamSpawnPointUIMethod = targetWrapperType.GetMethod("GetParamSpawnPointUI");
                }

                MonsterTrainAccessibility.LogInfo($"CardTargetingPatches reflection: possibleTargets={_possibleTargetsField != null}, " +
                    $"selectedIndex={_keyboardSelectedTargetIndexField != null}, " +
                    $"behaviorState={_behaviorStateField != null}, " +
                    $"characterState={_getParamCharacterStateMethod != null}");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"CardTargetingPatches InitReflection failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Called after SelectCardInternal - announces when targeting mode starts
        /// </summary>
        public static void SelectCardPostfix(object __instance, bool usingKeyboard, bool reselect)
        {
            try
            {
                if (reselect) return; // Don't re-announce on reselect (room change)

                var targets = GetPossibleTargets(__instance);
                int selectedIndex = GetSelectedTargetIndex(__instance);
                string cardName = GetFocusedCardName(__instance);

                if (targets == null || targets.Count == 0)
                {
                    // No character targets - this is a room/floor target card, not unit targeting
                    return;
                }

                // Check if any target has a character (spell targeting) vs spawn points only (monster placement)
                bool hasCharacterTargets = false;
                for (int i = 0; i < targets.Count; i++)
                {
                    var character = GetCharacterFromTarget(targets[i]);
                    if (character != null)
                    {
                        hasCharacterTargets = true;
                        break;
                    }
                }

                if (!hasCharacterTargets)
                {
                    // Monster placement card - don't announce unit targeting
                    return;
                }

                _lastAnnouncedTargetIndex = selectedIndex;

                string targetDesc = GetTargetDescription(targets, selectedIndex);
                string countInfo = targets.Count > 1 ? $"{targets.Count} targets. " : "";
                string currentInfo = !string.IsNullOrEmpty(targetDesc) ? $"Current: {targetDesc}" : "";

                string cardInfo = !string.IsNullOrEmpty(cardName) ? $"{cardName}. " : "";
                string message = $"{cardInfo}Select target. {countInfo}Left/Right to change. Enter to confirm, Escape to cancel. {currentInfo}";

                MonsterTrainAccessibility.ScreenReader?.Speak(message, false);
                MonsterTrainAccessibility.LogInfo($"Unit targeting started: {targets.Count} targets, selected={selectedIndex}");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in SelectCard targeting announce: {ex.Message}");
            }
        }

        /// <summary>
        /// Called after MoveTargetWithKeyboard - announces the new target
        /// </summary>
        public static void MoveTargetPostfix(object __instance, bool __result, int incr)
        {
            try
            {
                if (!__result) return; // Move didn't happen

                var targets = GetPossibleTargets(__instance);
                int selectedIndex = GetSelectedTargetIndex(__instance);

                if (targets == null || targets.Count == 0 || selectedIndex < 0 || selectedIndex >= targets.Count)
                    return;

                // Only announce if index actually changed
                if (selectedIndex == _lastAnnouncedTargetIndex)
                    return;

                _lastAnnouncedTargetIndex = selectedIndex;

                string targetDesc = GetTargetDescription(targets, selectedIndex);
                if (!string.IsNullOrEmpty(targetDesc))
                {
                    string message = $"{selectedIndex + 1} of {targets.Count}: {targetDesc}";
                    MonsterTrainAccessibility.ScreenReader?.Speak(message, false);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in MoveTarget announce: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset tracking when targeting ends
        /// </summary>
        public static void ResetTracking()
        {
            _lastAnnouncedTargetIndex = -1;
        }

        private static System.Collections.IList GetPossibleTargets(object instance)
        {
            if (_possibleTargetsField == null || instance == null) return null;
            return _possibleTargetsField.GetValue(instance) as System.Collections.IList;
        }

        private static int GetSelectedTargetIndex(object instance)
        {
            if (_keyboardSelectedTargetIndexField == null || instance == null) return -1;
            return (int)_keyboardSelectedTargetIndexField.GetValue(instance);
        }

        private static string GetFocusedCardName(object instance)
        {
            if (_focusedCardStateField == null || instance == null) return null;
            try
            {
                var cardState = _focusedCardStateField.GetValue(instance);
                if (cardState == null) return null;
                var getNameMethod = cardState.GetType().GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    return getNameMethod.Invoke(cardState, null) as string;
                }
            }
            catch { }
            return null;
        }

        private static object GetCharacterFromTarget(object targetWrapper)
        {
            if (_getParamCharacterStateMethod == null || targetWrapper == null) return null;
            try
            {
                return _getParamCharacterStateMethod.Invoke(targetWrapper, null);
            }
            catch { return null; }
        }

        private static string GetTargetDescription(System.Collections.IList targets, int index)
        {
            if (targets == null || index < 0 || index >= targets.Count)
                return null;

            var targetWrapper = targets[index];
            var character = GetCharacterFromTarget(targetWrapper);

            if (character != null)
            {
                // Use BattleAccessibility to get unit description
                var battle = MonsterTrainAccessibility.BattleHandler;
                if (battle != null)
                {
                    return battle.GetTargetUnitDescription(character);
                }

                // Fallback: try GetName
                try
                {
                    var type = character.GetType();
                    var getNameMethod = type.GetMethod("GetName", Type.EmptyTypes);
                    if (getNameMethod != null)
                    {
                        string name = getNameMethod.Invoke(character, null) as string;
                        if (!string.IsNullOrEmpty(name)) return name;
                    }

                    // Try character data
                    var getDataMethod = type.GetMethod("GetCharacterData", Type.EmptyTypes);
                    if (getDataMethod != null)
                    {
                        var data = getDataMethod.Invoke(character, null);
                        if (data != null)
                        {
                            var dataGetName = data.GetType().GetMethod("GetName", Type.EmptyTypes);
                            if (dataGetName != null)
                            {
                                return dataGetName.Invoke(data, null) as string;
                            }
                        }
                    }
                }
                catch { }

                return "Unknown unit";
            }

            // No character - could be spawn point for monster placement
            return "Empty position";
        }
    }
}
