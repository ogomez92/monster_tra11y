using System;
using System.Collections.Generic;
using System.Reflection;

namespace MonsterTrainAccessibility.Core
{
    /// <summary>
    /// Builds a keyword dictionary from game localization data.
    /// Pulls status effects, character triggers, and card traits
    /// from the game's own localization system instead of hardcoding.
    /// </summary>
    public static class KeywordManager
    {
        private static Dictionary<string, string> _keywords;
        private static MethodInfo _localizeMethod;
        private static bool _localizeMethodSearched;

        /// <summary>
        /// Get the keyword dictionary, building it on first access.
        /// </summary>
        public static Dictionary<string, string> GetKeywords()
        {
            if (_keywords == null)
                BuildKeywords();
            return _keywords;
        }

        /// <summary>
        /// Force rebuild of the keyword dictionary (e.g. if first attempt was too early).
        /// </summary>
        public static void Reset()
        {
            _keywords = null;
        }

        private static void BuildKeywords()
        {
            _keywords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            EnsureLocalizeMethod();

            int statusCount = LoadStatusEffectKeywords();
            int triggerCount = LoadTriggerKeywords();
            int traitCount = LoadCardTraitKeywords();

            // Add fallback keywords that aren't in the game's status/trigger/trait systems
            int fallbackCount = LoadFallbackKeywords();

            MonsterTrainAccessibility.LogInfo(
                $"KeywordManager built {_keywords.Count} keywords " +
                $"(status={statusCount}, triggers={triggerCount}, traits={traitCount}, fallback={fallbackCount})");
        }

        /// <summary>
        /// Load keywords from StatusEffectManager.StatusIdToLocalizationExpression
        /// </summary>
        private static int LoadStatusEffectKeywords()
        {
            int count = 0;
            try
            {
                Type semType = FindTypeInAssemblies("StatusEffectManager");
                if (semType == null)
                {
                    MonsterTrainAccessibility.LogWarning("KeywordManager: StatusEffectManager type not found");
                    return 0;
                }

                var field = semType.GetField("StatusIdToLocalizationExpression",
                    BindingFlags.Public | BindingFlags.Static);
                if (field == null)
                {
                    // Try property
                    var prop = semType.GetProperty("StatusIdToLocalizationExpression",
                        BindingFlags.Public | BindingFlags.Static);
                    if (prop != null)
                    {
                        var dict = prop.GetValue(null) as System.Collections.IDictionary;
                        if (dict != null)
                            count = ProcessLocalizationDictionary(dict, "_CardText", "_CardTooltipText");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogWarning("KeywordManager: StatusIdToLocalizationExpression not found");
                    }
                }
                else
                {
                    var dict = field.GetValue(null) as System.Collections.IDictionary;
                    if (dict != null)
                        count = ProcessLocalizationDictionary(dict, "_CardText", "_CardTooltipText");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"KeywordManager: Error loading status effects: {ex.Message}");
            }
            return count;
        }

        /// <summary>
        /// Load keywords from CharacterTriggerData.TriggerToLocalizationExpression
        /// </summary>
        private static int LoadTriggerKeywords()
        {
            int count = 0;
            try
            {
                Type ctdType = FindTypeInAssemblies("CharacterTriggerData");
                if (ctdType == null)
                {
                    MonsterTrainAccessibility.LogWarning("KeywordManager: CharacterTriggerData type not found");
                    return 0;
                }

                var field = ctdType.GetField("TriggerToLocalizationExpression",
                    BindingFlags.Public | BindingFlags.Static);
                if (field == null)
                {
                    var prop = ctdType.GetProperty("TriggerToLocalizationExpression",
                        BindingFlags.Public | BindingFlags.Static);
                    if (prop != null)
                    {
                        var dict = prop.GetValue(null) as System.Collections.IDictionary;
                        if (dict != null)
                            count = ProcessLocalizationDictionary(dict, "_CardText", "_TooltipText");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogWarning("KeywordManager: TriggerToLocalizationExpression not found");
                    }
                }
                else
                {
                    var dict = field.GetValue(null) as System.Collections.IDictionary;
                    if (dict != null)
                        count = ProcessLocalizationDictionary(dict, "_CardText", "_TooltipText");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"KeywordManager: Error loading triggers: {ex.Message}");
            }
            return count;
        }

        /// <summary>
        /// Process a dictionary of {enum/key -> localization prefix} into keyword entries.
        /// </summary>
        private static int ProcessLocalizationDictionary(
            System.Collections.IDictionary dict, string nameSuffix, string tooltipSuffix)
        {
            int count = 0;
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                try
                {
                    string prefix = entry.Value as string;
                    if (string.IsNullOrEmpty(prefix)) continue;

                    string name = Localize(prefix + nameSuffix);
                    string tooltip = Localize(prefix + tooltipSuffix);

                    if (string.IsNullOrEmpty(name) || name == (prefix + nameSuffix))
                        continue;

                    name = Screens.BattleAccessibility.StripRichTextTags(name).Trim();

                    if (string.IsNullOrEmpty(name))
                        continue;

                    string value;
                    if (!string.IsNullOrEmpty(tooltip) && tooltip != (prefix + tooltipSuffix))
                    {
                        tooltip = Screens.BattleAccessibility.StripRichTextTags(tooltip).Trim();
                        value = $"{name}: {tooltip}";
                    }
                    else
                    {
                        value = name;
                    }

                    if (!_keywords.ContainsKey(name))
                    {
                        _keywords[name] = value;
                        count++;
                    }
                }
                catch { }
            }
            return count;
        }

        /// <summary>
        /// Load keywords from known card trait state names.
        /// No master dictionary exists in game, so we use a known list.
        /// </summary>
        private static int LoadCardTraitKeywords()
        {
            int count = 0;

            var traitNames = new[]
            {
                "CardTraitExhaustState",
                "CardTraitIntrinsicState",
                "CardTraitRetainState",
                "CardTraitSelfPurgeState",
                "CardTraitFreeze",
                "CardTraitPermafrostState",
                "CardTraitUnplayable",
                "CardTraitCopyOnPlay",
                "CardTraitIgnoreArmor",
                "CardTraitCorruptRestricted",
                "CardTraitScalingAddDamage",
                "CardTraitScalingAddStatusEffect",
                "CardTraitScalingBuffDamage",
                "CardTraitScalingHeal",
                "CardTraitScalingReduceCost",
                "CardTraitScalingUpgradeUnitAttack",
                "CardTraitScalingUpgradeUnitHealth",
                "CardTraitScalingUpgradeUnitSize",
                "CardTraitScalingUpgradeUnitStatusEffect",
            };

            foreach (string traitName in traitNames)
            {
                try
                {
                    string name = Localize(traitName + "_CardText");
                    string tooltip = Localize(traitName + "_TooltipText");

                    // Also try _CardTooltipText variant
                    if (string.IsNullOrEmpty(tooltip) || tooltip == (traitName + "_TooltipText"))
                        tooltip = Localize(traitName + "_CardTooltipText");

                    if (string.IsNullOrEmpty(name) || name == (traitName + "_CardText"))
                        continue;

                    name = Screens.BattleAccessibility.StripRichTextTags(name).Trim();
                    if (string.IsNullOrEmpty(name)) continue;

                    string value;
                    if (!string.IsNullOrEmpty(tooltip) &&
                        tooltip != (traitName + "_TooltipText") &&
                        tooltip != (traitName + "_CardTooltipText"))
                    {
                        tooltip = Screens.BattleAccessibility.StripRichTextTags(tooltip).Trim();
                        value = $"{name}: {tooltip}";
                    }
                    else
                    {
                        value = name;
                    }

                    if (!_keywords.ContainsKey(name))
                    {
                        _keywords[name] = value;
                        count++;
                    }
                }
                catch { }
            }

            return count;
        }

        /// <summary>
        /// Fallback keywords for game mechanics not in the status/trigger/trait systems.
        /// These are card effect descriptions or mechanics that appear as bold text in cards.
        /// Only added if not already present from game localization.
        /// </summary>
        private static int LoadFallbackKeywords()
        {
            int count = 0;
            var fallbacks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Piercing", "Piercing: Damage ignores Armor and shields" },
                { "Magic Power", "Magic Power: Boosts spell damage and healing" },
                { "Attuned", "Attuned: Multiplies Magic Power effects by 5" },
                { "Doublestack", "Doublestack: Status effect stacks added by this card are doubled" },
                { "Offering", "Offering: Played automatically if discarded" },
                { "Reserve", "Reserve: Triggers if card remains in hand at end of turn" },
                { "Pyrebound", "Pyrebound: Only playable in Pyre Room or floor below" },
                { "X Cost", "X Cost: Spends all remaining Ember, effect scales with amount" },
                { "Unplayable", "Unplayable: This card cannot be played" },
                { "Spellchain", "Spellchain: Creates a copy with +1 cost and Purge" },
                { "Infused", "Infused: Floor gains 1 Echo when played" },
                { "Extract", "Extract: Removes charged echoes when played" },
                { "Ascend", "Ascend: Move up a floor to the back" },
                { "Descend", "Descend: Move down a floor to the back" },
                { "Reform", "Reform: Return a defeated friendly unit to hand" },
                { "Sacrifice", "Sacrifice: Kill a friendly unit to play this card" },
                { "Cultivate", "Cultivate: Increase stats of lowest health friendly unit" },
                { "Recover", "Recover: Restores health to friendly units after combat" },
                { "Enchant", "Enchant: Other friendly units on floor gain a bonus" },
                { "Eaten", "Eaten: Will be eaten by front unit after combat" },
                { "Soul", "Soul: Powers Devourer of Death's Extinguish ability" },
                { "Shard", "Shard: Powers Solgard the Martyr's abilities" },
                { "Buffet", "Buffet: Can be eaten multiple times" },
            };

            foreach (var kv in fallbacks)
            {
                if (!_keywords.ContainsKey(kv.Key))
                {
                    _keywords[kv.Key] = kv.Value;
                    count++;
                }
            }

            return count;
        }

        #region Localization Helpers

        private static void EnsureLocalizeMethod()
        {
            if (_localizeMethodSearched) return;
            _localizeMethodSearched = true;

            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var asmName = assembly.GetName().Name;
                    if (!asmName.Contains("Assembly-CSharp") && !asmName.Contains("Trainworks"))
                        continue;

                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (!type.IsClass) continue;

                            var method = type.GetMethod("Localize", BindingFlags.Public | BindingFlags.Static);
                            if (method != null && method.ReturnType == typeof(string))
                            {
                                var parameters = method.GetParameters();
                                if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(string))
                                {
                                    _localizeMethod = method;
                                    return;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"KeywordManager: Error finding Localize method: {ex.Message}");
            }
        }

        private static string Localize(string key)
        {
            if (string.IsNullOrEmpty(key) || _localizeMethod == null)
                return null;

            try
            {
                var parameters = _localizeMethod.GetParameters();
                var args = new object[parameters.Length];
                args[0] = key;
                for (int i = 1; i < parameters.Length; i++)
                {
                    args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                }

                var result = _localizeMethod.Invoke(null, args) as string;
                if (!string.IsNullOrEmpty(result) && result != key)
                    return result;
            }
            catch { }

            return null;
        }

        private static Type FindTypeInAssemblies(string typeName)
        {
            // Try Assembly-CSharp first
            var type = Type.GetType(typeName + ", Assembly-CSharp");
            if (type != null) return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
            }
            return null;
        }

        #endregion
    }
}
