using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Detect when a unit eats a morsel (Umbra clan feeding mechanic).
    /// Hooks CombatFeederRules.RunFoodRules to announce the feeding event.
    /// </summary>
    public static class MorselEatenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var feederRulesType = AccessTools.TypeByName("CombatFeederRules");
                if (feederRulesType != null)
                {
                    var method = AccessTools.Method(feederRulesType, "RunFoodRules");
                    if (method != null)
                    {
                        var prefix = new HarmonyMethod(typeof(MorselEatenPatch).GetMethod(nameof(Prefix)));
                        harmony.Patch(method, prefix: prefix);
                        MonsterTrainAccessibility.LogInfo("Patched CombatFeederRules.RunFoodRules");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("CombatFeederRules.RunFoodRules not found");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch morsel eating: {ex.Message}");
            }
        }

        // RunFoodRules signature:
        // static IEnumerator RunFoodRules(CombatManager, StatusEffectManager, List<CharacterState> sourceFoodUnits,
        //   List<CharacterState> allTeamCharactersInRoom, int numTimesToTrigger, RelicState, CharacterState overriddenFeederUnit)
        // Positional: __0=CombatManager, __1=StatusEffectManager, __2=sourceFoodUnits, __3=allTeamCharactersInRoom,
        //   __4=numTimesToTrigger, __5=srcRelicState, __6=overriddenFeederUnit
        public static void Prefix(object __2, object __3, object __6)
        {
            try
            {
                if (PreviewModeDetector.IsInPreviewMode())
                    return;

                var foodUnits = __2 as System.Collections.IList;
                var allUnits = __3 as System.Collections.IList;
                object overriddenFeeder = __6;

                if (foodUnits == null || foodUnits.Count == 0)
                    return;

                // Determine feeder unit: use override if provided, otherwise find front feeder
                object feederUnit = overriddenFeeder ?? FindFrontFeeder(allUnits);
                if (feederUnit == null)
                    return;

                string feederName = CharacterStateHelper.GetUnitName(feederUnit);

                // Announce each morsel being eaten
                foreach (object foodUnit in foodUnits)
                {
                    if (!IsFoodUnit(foodUnit))
                        continue;

                    string morselName = CharacterStateHelper.GetUnitName(foodUnit);
                    MonsterTrainAccessibility.BattleHandler?.OnMorselEaten(feederName, morselName);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in morsel eaten patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Replicates CombatFeederRules.GetFrontFeederUnit logic:
        /// Returns the first alive, non-untouchable, non-food unit.
        /// </summary>
        private static object FindFrontFeeder(System.Collections.IList allTeamCharacters)
        {
            if (allTeamCharacters == null) return null;

            foreach (object unit in allTeamCharacters)
            {
                if (unit == null) continue;

                // Check HasStatusEffect("untouchable")
                if (HasStatusEffect(unit, "untouchable"))
                    continue;

                // Skip food units
                if (IsFoodUnit(unit))
                    continue;

                return unit;
            }
            return null;
        }

        private static bool IsFoodUnit(object characterState)
        {
            if (characterState == null) return false;
            try
            {
                // Check for "inedible" status
                if (HasStatusEffect(characterState, "inedible"))
                    return false;

                // Check for OnEaten trigger
                var type = characterState.GetType();
                var getTriggersMethod = type.GetMethod("GetTriggers");
                if (getTriggersMethod != null)
                {
                    var triggers = getTriggersMethod.Invoke(characterState, null) as System.Collections.IEnumerable;
                    if (triggers != null)
                    {
                        foreach (object trigger in triggers)
                        {
                            var getTriggerMethod = trigger.GetType().GetMethod("GetTrigger");
                            if (getTriggerMethod != null)
                            {
                                var triggerValue = getTriggerMethod.Invoke(trigger, null);
                                if (triggerValue != null && triggerValue.ToString() == "OnEaten")
                                    return true;
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool HasStatusEffect(object characterState, string statusId)
        {
            try
            {
                var method = characterState.GetType().GetMethod("HasStatusEffect", new[] { typeof(string) });
                if (method != null)
                {
                    var result = method.Invoke(characterState, new object[] { statusId });
                    if (result is bool b)
                        return b;
                }
            }
            catch { }
            return false;
        }
    }
}
