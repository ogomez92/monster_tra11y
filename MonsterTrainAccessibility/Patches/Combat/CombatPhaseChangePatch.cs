using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Detect combat phase transitions for granular announcements.
    /// Hooks CombatManager.SetCombatPhase(Phase) - private method.
    /// Complements existing CombatPhasePatch by announcing MonsterTurn, HeroTurn, BossAction, etc.
    /// </summary>
    public static class CombatPhaseChangePatch
    {
        private static string _lastPhase = "";

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var combatType = AccessTools.TypeByName("CombatManager");
                if (combatType == null) return;

                var method = combatType.GetMethod("SetCombatPhase",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(CombatPhaseChangePatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched CombatManager.SetCombatPhase");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch SetCombatPhase: {ex.Message}");
            }
        }

        // __0 = Phase enum value, __instance = CombatManager
        public static void Postfix(object __instance, object __0)
        {
            try
            {
                if (__0 == null) return;
                string phaseName = __0.ToString();

                if (phaseName == _lastPhase) return;
                _lastPhase = phaseName;

                // Map phases to user-friendly names
                // Skip "Combat" - already announced by existing CombatPhasePatch
                // Skip Start, Placement, PreCombat, MonsterTurnQueueClear - too noisy / not useful
                string announcement = null;
                switch (phaseName)
                {
                    case "MonsterTurn":
                        announcement = "Your units attack";
                        break;
                    case "HeroTurn":
                        announcement = "Enemy turn";
                        break;
                    case "EndOfCombat":
                        announcement = "Combat ended";
                        break;
                    case "BossActionPreCombat":
                    case "BossActionPostCombat":
                        announcement = GetBossActionAnnouncement(__instance);
                        break;
                }

                if (announcement != null)
                {
                    MonsterTrainAccessibility.BattleHandler?.OnCombatPhaseChanged(announcement);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in combat phase change patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Read the boss's next action description from CombatManager -> HeroManager -> boss CharacterState -> BossState
        /// </summary>
        private static string GetBossActionAnnouncement(object combatManager)
        {
            try
            {
                if (combatManager == null) return "Boss action";

                // CombatManager.heroManager (private field)
                var heroManagerField = combatManager.GetType().GetField("heroManager",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (heroManagerField == null) return "Boss action";

                var heroManager = heroManagerField.GetValue(combatManager);
                if (heroManager == null) return "Boss action";

                // HeroManager.GetOuterTrainBossCharacter()
                var getBossMethod = heroManager.GetType().GetMethod("GetOuterTrainBossCharacter",
                    Type.EmptyTypes);
                if (getBossMethod == null) return "Boss action";

                var bossCharacter = getBossMethod.Invoke(heroManager, null);
                if (bossCharacter == null) return "Boss action";

                // Get boss name
                string bossName = "Boss";
                var getNameMethod = bossCharacter.GetType().GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    string name = getNameMethod.Invoke(bossCharacter, null) as string;
                    if (!string.IsNullOrEmpty(name))
                        bossName = Screens.BattleAccessibility.StripRichTextTags(name).Trim();
                }

                // CharacterState.GetBossState()
                var getBossStateMethod = bossCharacter.GetType().GetMethod("GetBossState", Type.EmptyTypes);
                if (getBossStateMethod == null) return $"{bossName} action";

                var bossState = getBossStateMethod.Invoke(bossCharacter, null);
                if (bossState == null) return $"{bossName} action";

                // BossState.GetNextBossAction() -> BossActionState
                var getNextActionMethod = bossState.GetType().GetMethod("GetNextBossAction", Type.EmptyTypes);
                if (getNextActionMethod == null) return $"{bossName} action";

                var bossAction = getNextActionMethod.Invoke(bossState, null);
                if (bossAction == null) return $"{bossName} action";

                var actionType = bossAction.GetType();
                var parts = new List<string>();

                // Check if room destroy action
                var isRoomDestroyMethod = actionType.GetMethod("IsRoomDestroyAction", Type.EmptyTypes);
                bool isRoomDestroy = false;
                if (isRoomDestroyMethod != null)
                {
                    var result = isRoomDestroyMethod.Invoke(bossAction, null);
                    if (result is bool b) isRoomDestroy = b;
                }

                // Check if empty action (no effects, just movement)
                var isEmptyMethod = actionType.GetMethod("IsEmptyAction", Type.EmptyTypes);
                bool isEmpty = false;
                if (isEmptyMethod != null)
                {
                    var result = isEmptyMethod.Invoke(bossAction, null);
                    if (result is bool b) isEmpty = b;
                }

                // BossActionState.GetTargetedRoomIndex() - which floor
                string floorStr = "";
                var getTargetRoomMethod = actionType.GetMethod("GetTargetedRoomIndex", Type.EmptyTypes);
                if (getTargetRoomMethod != null)
                {
                    var result = getTargetRoomMethod.Invoke(bossAction, null);
                    if (result is int roomIndex && roomIndex >= 0 && roomIndex <= 2)
                    {
                        floorStr = Screens.BattleAccessibility.RoomIndexToFloorName(roomIndex);
                    }
                }

                if (isRoomDestroy)
                {
                    parts.Add($"{bossName} freezes {floorStr}");
                }
                else if (isEmpty)
                {
                    parts.Add($"{bossName} moves to {floorStr}");
                }
                else
                {
                    // BossActionState.GetTooltipDescription() - the localized description
                    string desc = null;
                    var getDescMethod = actionType.GetMethod("GetTooltipDescription", Type.EmptyTypes);
                    if (getDescMethod != null)
                    {
                        desc = getDescMethod.Invoke(bossAction, null) as string;
                        if (!string.IsNullOrEmpty(desc))
                        {
                            desc = Screens.BattleAccessibility.StripRichTextTags(desc).Trim();
                        }
                    }

                    // If tooltip description is empty, build one from the effects
                    if (string.IsNullOrEmpty(desc))
                    {
                        desc = BuildDescriptionFromEffects(bossAction);
                    }

                    // Last resort: try to read from the stringBuilder field directly
                    if (string.IsNullOrEmpty(desc))
                    {
                        var sbField = actionType.GetField("stringBuilder", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (sbField != null)
                        {
                            var sb = sbField.GetValue(bossAction) as System.Text.StringBuilder;
                            if (sb != null && sb.Length > 0)
                            {
                                desc = Screens.BattleAccessibility.StripRichTextTags(sb.ToString()).Trim();
                            }
                        }
                    }

                    // Try reading effect descriptions by getting RelicManager for better text
                    if (string.IsNullOrEmpty(desc))
                    {
                        var relicManagerField = combatManager.GetType().GetField("relicManager",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (relicManagerField != null)
                        {
                            var relicManager = relicManagerField.GetValue(combatManager);
                            desc = BuildDescriptionFromEffectsWithRelicManager(bossAction, relicManager);
                        }
                    }

                    if (!string.IsNullOrEmpty(desc))
                    {
                        parts.Add($"{bossName}: {desc}");
                    }
                    else
                    {
                        parts.Add($"{bossName} acts");
                    }

                    if (!string.IsNullOrEmpty(floorStr))
                    {
                        parts.Add(floorStr);
                    }
                }

                string announcement = string.Join(". ", parts);
                MonsterTrainAccessibility.LogInfo($"Boss action announcement: {announcement}");
                return announcement;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading boss action: {ex.Message}");
                return "Boss action";
            }
        }

        /// <summary>
        /// Build description from effects using RelicManager for better GetCardText results
        /// </summary>
        private static string BuildDescriptionFromEffectsWithRelicManager(object bossAction, object relicManager)
        {
            try
            {
                var actionType = bossAction.GetType();
                var getEffectsMethod = actionType.GetMethod("GetEffects", Type.EmptyTypes);
                if (getEffectsMethod == null) return null;

                var effects = getEffectsMethod.Invoke(bossAction, null) as System.Collections.IList;
                if (effects == null || effects.Count == 0) return null;

                var descriptions = new List<string>();
                foreach (var effect in effects)
                {
                    if (effect == null) continue;
                    var effectType = effect.GetType();

                    // Try GetCardText(RelicManager) with the actual RelicManager
                    var methods = effectType.GetMethods();
                    foreach (var m in methods)
                    {
                        if (m.Name != "GetCardText") continue;
                        var ps = m.GetParameters();
                        if (ps.Length == 1)
                        {
                            try
                            {
                                string text = m.Invoke(effect, new[] { relicManager }) as string;
                                if (!string.IsNullOrEmpty(text?.Trim()))
                                {
                                    descriptions.Add(Screens.BattleAccessibility.StripRichTextTags(text).Trim());
                                    break;
                                }
                            }
                            catch { }
                        }
                    }
                }

                return descriptions.Count > 0 ? string.Join(". ", descriptions) : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Build a human-readable description from BossActionState effects
        /// when GetTooltipDescription() returns empty.
        /// </summary>
        private static string BuildDescriptionFromEffects(object bossAction)
        {
            try
            {
                var actionType = bossAction.GetType();
                var getEffectsMethod = actionType.GetMethod("GetEffects", Type.EmptyTypes);
                if (getEffectsMethod == null) return null;

                var effects = getEffectsMethod.Invoke(bossAction, null) as System.Collections.IList;
                if (effects == null || effects.Count == 0) return null;

                var descriptions = new List<string>();
                foreach (var effect in effects)
                {
                    if (effect == null) continue;
                    var effectType = effect.GetType();

                    // Try GetCardText(RelicManager) first
                    var getCardTextMethod = effectType.GetMethod("GetCardText",
                        new[] { typeof(object).Assembly.GetType("RelicManager") ?? effectType.Assembly.GetType("RelicManager") });
                    if (getCardTextMethod == null)
                    {
                        // Try parameterless or any GetCardText
                        getCardTextMethod = effectType.GetMethod("GetCardText", Type.EmptyTypes);
                    }
                    if (getCardTextMethod == null)
                    {
                        var methods = effectType.GetMethods();
                        foreach (var m in methods)
                        {
                            if (m.Name == "GetCardText")
                            {
                                getCardTextMethod = m;
                                break;
                            }
                        }
                    }
                    if (getCardTextMethod != null)
                    {
                        try
                        {
                            var ps = getCardTextMethod.GetParameters();
                            var args = new object[ps.Length];
                            for (int i = 0; i < ps.Length; i++)
                            {
                                args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
                            }
                            string text = getCardTextMethod.Invoke(effect, args) as string;
                            if (!string.IsNullOrEmpty(text?.Trim()))
                            {
                                descriptions.Add(Screens.BattleAccessibility.StripRichTextTags(text).Trim());
                                continue;
                            }
                        }
                        catch { }
                    }

                    // Build description from effect parameters
                    string effectDesc = BuildEffectDescription(effect);
                    if (!string.IsNullOrEmpty(effectDesc))
                    {
                        descriptions.Add(effectDesc);
                    }
                }

                return descriptions.Count > 0 ? string.Join(". ", descriptions) : null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error building boss effect descriptions: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Build a description for a single CardEffectState from its parameters.
        /// Reads the effect class name, paramInt (damage/amount), and status effects.
        /// </summary>
        private static string BuildEffectDescription(object effectState)
        {
            try
            {
                var effectType = effectState.GetType();

                // Get the actual effect class via GetCardEffect()
                string effectClassName = null;
                var getCardEffectMethod = effectType.GetMethod("GetCardEffect", Type.EmptyTypes);
                if (getCardEffectMethod != null)
                {
                    var cardEffect = getCardEffectMethod.Invoke(effectState, null);
                    if (cardEffect != null)
                    {
                        effectClassName = cardEffect.GetType().Name;
                    }
                }

                // Get paramInt (damage amount, heal amount, etc.)
                int paramInt = 0;
                var getParamIntMethod = effectType.GetMethod("GetParamInt", Type.EmptyTypes);
                if (getParamIntMethod != null)
                {
                    var result = getParamIntMethod.Invoke(effectState, null);
                    if (result is int val) paramInt = val;
                }

                // Get status effects
                var getStatusEffectsMethod = effectType.GetMethod("GetParamStatusEffects", Type.EmptyTypes);
                System.Array statusEffects = null;
                if (getStatusEffectsMethod != null)
                {
                    statusEffects = getStatusEffectsMethod.Invoke(effectState, null) as System.Array;
                }

                MonsterTrainAccessibility.LogInfo($"Boss effect: class={effectClassName}, paramInt={paramInt}, statusEffects={statusEffects?.Length ?? 0}");

                // Map effect class names to readable descriptions
                if (effectClassName != null)
                {
                    if (effectClassName.Contains("Damage"))
                    {
                        return paramInt > 0 ? $"Deal {paramInt} damage" : "Deal damage";
                    }
                    if (effectClassName.Contains("Heal"))
                    {
                        if (effectClassName.Contains("Train") || effectClassName.Contains("Pyre"))
                            return paramInt > 0 ? $"Heal Pyre for {paramInt}" : "Heal Pyre";
                        return paramInt > 0 ? $"Heal {paramInt}" : "Heal";
                    }
                    if (effectClassName.Contains("AddStatusEffect") && statusEffects != null && statusEffects.Length > 0)
                    {
                        var statusParts = new List<string>();
                        foreach (var se in statusEffects)
                        {
                            if (se == null) continue;
                            var seType = se.GetType();
                            var statusIdField = seType.GetField("statusId", BindingFlags.Public | BindingFlags.Instance);
                            var countField = seType.GetField("count", BindingFlags.Public | BindingFlags.Instance);
                            string statusId = statusIdField?.GetValue(se) as string ?? "effect";
                            int count = 0;
                            if (countField != null)
                            {
                                var countVal = countField.GetValue(se);
                                if (countVal is int c) count = c;
                            }
                            statusParts.Add(count > 0 ? $"Apply {count} {statusId}" : $"Apply {statusId}");
                        }
                        if (statusParts.Count > 0)
                            return string.Join(", ", statusParts);
                    }
                    if (effectClassName.Contains("RemoveStatusEffect") && statusEffects != null && statusEffects.Length > 0)
                    {
                        var statusParts = new List<string>();
                        foreach (var se in statusEffects)
                        {
                            if (se == null) continue;
                            var seType = se.GetType();
                            var statusIdField = seType.GetField("statusId", BindingFlags.Public | BindingFlags.Instance);
                            string statusId = statusIdField?.GetValue(se) as string ?? "effect";
                            statusParts.Add($"Remove {statusId}");
                        }
                        if (statusParts.Count > 0)
                            return string.Join(", ", statusParts);
                    }
                    if (effectClassName.Contains("Buff"))
                    {
                        return paramInt > 0 ? $"Buff {paramInt}" : "Buff units";
                    }
                    if (effectClassName.Contains("Kill") || effectClassName.Contains("Sacrifice"))
                    {
                        return "Kill unit";
                    }
                    if (effectClassName.Contains("SpawnMonster") || effectClassName.Contains("SpawnHero"))
                    {
                        return "Spawn unit";
                    }
                    if (effectClassName.Contains("AdjustEnergy") || effectClassName.Contains("GainEnergy"))
                    {
                        return paramInt != 0 ? $"Adjust ember by {paramInt}" : "Modify ember";
                    }
                    if (effectClassName.Contains("Draw"))
                    {
                        return paramInt > 0 ? $"Draw {paramInt} cards" : "Draw cards";
                    }
                    if (effectClassName.Contains("Discard"))
                    {
                        return "Discard cards";
                    }
                    if (effectClassName.Contains("AdjustRoomCapacity"))
                    {
                        return paramInt != 0 ? $"Adjust room capacity by {paramInt}" : "Modify room capacity";
                    }
                }

                // Fallback: if we have paramInt and know nothing else
                if (paramInt > 0)
                {
                    return $"{paramInt} damage";
                }

                return null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Error building effect description: {ex.Message}");
                return null;
            }
        }
    }
}
