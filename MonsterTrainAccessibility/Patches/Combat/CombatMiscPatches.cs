using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches
{
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
                        else if (_lastPyreHP > 0 && currentHP > _lastPyreHP)
                        {
                            int healing = currentHP - _lastPyreHP;
                            MonsterTrainAccessibility.BattleHandler?.OnPyreHealed(healing, currentHP);
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
                    MonsterTrainAccessibility.LogInfo($"CharacterChatterData methods: {string.Join(", ", System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(methods, m => m.Name.Contains("Expression") || m.Name.Contains("Chatter")), m => m.Name))}");
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
    /// Detect healing via CharacterState.ApplyHeal
    /// Signature: ApplyHeal(int amount, bool triggerOnHeal = true, CardState responsibleCard = null, RelicState relicState = null, bool fromMaxHPChange = false)
    /// </summary>
    public static class HealAppliedPatch
    {
        private static float _lastHealTime = 0f;
        private static string _lastHealKey = "";

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var charStateType = AccessTools.TypeByName("CharacterState");
                if (charStateType != null)
                {
                    var method = AccessTools.Method(charStateType, "ApplyHeal");
                    if (method != null)
                    {
                        var prefix = new HarmonyMethod(typeof(HealAppliedPatch).GetMethod(nameof(Prefix)));
                        harmony.Patch(method, prefix: prefix);
                        MonsterTrainAccessibility.LogInfo("Patched CharacterState.ApplyHeal");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch ApplyHeal: {ex.Message}");
            }
        }

        // __instance is the CharacterState being healed, __0 is the heal amount
        public static void Prefix(object __instance, int __0)
        {
            try
            {
                // Skip if in preview mode
                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                    return;

                int amount = __0;
                if (amount <= 0 || __instance == null)
                    return;

                // Check if unit is alive and can be healed before announcing
                var charType = __instance.GetType();

                var isAliveProperty = charType.GetProperty("IsAlive");
                if (isAliveProperty != null)
                {
                    var alive = isAliveProperty.GetValue(__instance);
                    if (alive is bool b && !b)
                        return;
                }

                string targetName = "Unit";
                var getNameMethod = charType.GetMethod("GetName");
                if (getNameMethod != null)
                {
                    targetName = getNameMethod.Invoke(__instance, null) as string ?? "Unit";
                }

                // Deduplicate
                float currentTime = UnityEngine.Time.unscaledTime;
                string healKey = $"{targetName}_{amount}";
                if (healKey == _lastHealKey && currentTime - _lastHealTime < 0.3f)
                    return;

                _lastHealKey = healKey;
                _lastHealTime = currentTime;

                MonsterTrainAccessibility.ScreenReader?.Queue($"{targetName} healed for {amount}");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in heal patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect when an artifact/relic triggers during combat.
    /// Hooks RelicManager.NotifyRelicTriggered(RelicState, IRelicEffect)
    /// </summary>
    public static class RelicTriggeredPatch
    {
        private static float _lastTriggerTime = 0f;
        private static string _lastTriggerKey = "";

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var relicManagerType = AccessTools.TypeByName("RelicManager");
                if (relicManagerType == null) return;

                var method = AccessTools.Method(relicManagerType, "NotifyRelicTriggered");
                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(RelicTriggeredPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched RelicManager.NotifyRelicTriggered");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch NotifyRelicTriggered: {ex.Message}");
            }
        }

        // __0 = RelicState triggeredRelic, __1 = IRelicEffect triggeredEffect
        public static void Postfix(object __0, object __1)
        {
            try
            {
                if (__0 == null) return;

                string relicName = CharacterStateHelper.GetRelicName(__0);

                // Deduplicate rapid triggers of the same relic
                float currentTime = UnityEngine.Time.unscaledTime;
                if (relicName == _lastTriggerKey && currentTime - _lastTriggerTime < 0.3f)
                    return;

                _lastTriggerKey = relicName;
                _lastTriggerTime = currentTime;

                MonsterTrainAccessibility.BattleHandler?.OnRelicTriggered(relicName);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in relic triggered patch: {ex.Message}");
            }
        }
    }
}
