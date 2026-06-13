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
                            bool summaryMode = MonsterTrainAccessibility.AccessibilitySettings.CombatSummaryMode.Value;
                            bool inBattle = MonsterTrainAccessibility.BattleHandler?.IsInBattle ?? false;
                            // Pyre hits arrive via the pyre heart's HP-changed listener, which can
                            // fire after the phase has advanced past Combat/HeroTurn - so fold in any
                            // pyre damage outside the player's own turn, not just the fight phases.
                            if (summaryMode && inBattle && !CombatPhaseChangePatch.IsMonsterTurn)
                            {
                                MonsterTrainAccessibility.BattleHandler?.AccumulatePyre(damage, currentHP);
                                MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(
                                    $"Pyre takes {damage} damage! {currentHP} health remaining");
                            }
                            else
                            {
                                MonsterTrainAccessibility.BattleHandler?.OnPyreDamaged(damage, currentHP);
                            }
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
    /// Announce chatter - the speech bubbles above units in battle and the
    /// merchant's lines in shops. ChatterExpression.Express receives the final
    /// localized line as a parameter, so the postfix announces exactly what
    /// the bubble shows.
    /// </summary>
    public static class EnemyDialoguePatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Combat bubbles: ChatterExpression.Express(Chatter, ChatterExpressionType,
                // CharacterState character, float delay, string translatedText)
                var expressionType = AccessTools.TypeByName("ChatterExpression");
                var expressMethod = expressionType != null ? AccessTools.Method(expressionType, "Express") : null;
                if (expressMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(EnemyDialoguePatch).GetMethod(nameof(PostfixExpress)));
                    harmony.Patch(expressMethod, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched ChatterExpression.Express");
                }

                // Merchant bubbles: MerchantCharacterUI.ShowChatter(MerchantChatter, float, float)
                var merchantUIType = AccessTools.TypeByName("MerchantCharacterUI");
                var showChatterMethod = merchantUIType != null ? AccessTools.Method(merchantUIType, "ShowChatter") : null;
                if (showChatterMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(EnemyDialoguePatch).GetMethod(nameof(PostfixMerchantChatter)));
                    harmony.Patch(showChatterMethod, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched MerchantCharacterUI.ShowChatter");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch chatter: {ex.Message}");
            }
        }

        private static FieldInfo _associatedCharacterField;

        // __2 = CharacterState character, __4 = string translatedText
        public static void PostfixExpress(object __instance, object __2, string __4)
        {
            try
            {
                if (string.IsNullOrEmpty(__4))
                    return;

                // Express bails without showing anything when the expression is
                // already associated with another character; a successful call
                // leaves associatedCharacter == character
                if (_associatedCharacterField == null)
                    _associatedCharacterField = __instance.GetType().GetField("associatedCharacter",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (_associatedCharacterField != null &&
                    !ReferenceEquals(_associatedCharacterField.GetValue(__instance), __2))
                    return;

                string text = Utilities.TextUtilities.StripRichTextTags(__4).Trim();
                if (text.Length == 0)
                    return;

                MonsterTrainAccessibility.BattleHandler?.OnUnitChatter(GetCharacterName(__2), text);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in chatter patch: {ex.Message}");
            }
        }

        // __0 = MerchantCharacterData.MerchantChatter; its dialogue field is a localization key
        public static void PostfixMerchantChatter(object __instance, object __0)
        {
            try
            {
                if (__0 == null)
                    return;
                if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceDialogue.Value)
                    return;
                if (!MerchantChatterShown(__instance))
                    return;

                var dialogueField = __0.GetType().GetField("dialogue",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                string key = dialogueField?.GetValue(__0) as string;
                if (string.IsNullOrEmpty(key))
                    return;

                string text = Utilities.LocalizationHelper.Localize(key);
                if (string.IsNullOrEmpty(text))
                    return;

                text = Utilities.TextUtilities.StripRichTextTags(text).Trim();
                if (text.Length == 0)
                    return;

                MonsterTrainAccessibility.ScreenReader?.Queue($"Merchant says: {text}");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in merchant chatter patch: {ex.Message}");
            }
        }

        private static string GetCharacterName(object character)
        {
            try
            {
                var getNameMethod = character?.GetType().GetMethod("GetName", Type.EmptyTypes);
                string name = getNameMethod?.Invoke(character, null) as string;
                if (!string.IsNullOrEmpty(name))
                    return Utilities.TextUtilities.StripRichTextTags(name);
            }
            catch { }
            return "Unit";
        }

        /// <summary>
        /// Mirror ShowChatter's own display gate: merchant bubbles are hidden
        /// in Hell Rush (Matchmaker) runs unless the character opts in via
        /// ShowInHR. Announces anyway if the gate can't be read.
        /// </summary>
        private static bool MerchantChatterShown(object merchantUI)
        {
            try
            {
                var uiType = merchantUI.GetType();
                var data = uiType.GetField("data",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(merchantUI);
                if (data?.GetType().GetProperty("ShowInHR")?.GetValue(data) is bool showInHR && showInHR)
                    return true;

                var saveManager = uiType.GetField("saveManager",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(merchantUI);
                var runType = saveManager?.GetType().GetMethod("GetRunType", Type.EmptyTypes)?.Invoke(saveManager, null);
                return runType?.ToString() != "Matchmaker";
            }
            catch
            {
                return true;
            }
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
