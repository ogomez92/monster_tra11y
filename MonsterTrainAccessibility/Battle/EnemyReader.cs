using MonsterTrainAccessibility.Core;
using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Reads detailed enemy/unit information including abilities, triggers, boss actions, and status effects.
    /// </summary>
    internal static class EnemyReader
    {
        /// <summary>
        /// Announce all units (player monsters and enemies) on each floor
        /// </summary>
        internal static void AnnounceEnemies(BattleManagerCache cache, HashSet<string> announcedKeywords = null)
        {
            try
            {
                var output = MonsterTrainAccessibility.ScreenReader;
                output?.Speak("Units on train:", false);

                bool hasAnyUnits = false;
                int roomsFound = 0;
                int totalUnits = 0;

                // Iterate room indices from bottom (0) to top (2)
                for (int roomIndex = 0; roomIndex <= 2; roomIndex++)
                {
                    var room = FloorReader.GetRoom(cache, roomIndex);
                    if (room == null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Room {roomIndex} ({FloorReader.RoomIndexToFloorName(roomIndex)}) is null");
                        continue;
                    }
                    roomsFound++;

                    var units = FloorReader.GetUnitsInRoom(room);
                    totalUnits += units.Count;
                    MonsterTrainAccessibility.LogInfo($"Room {roomIndex} ({FloorReader.RoomIndexToFloorName(roomIndex)}) has {units.Count} units");

                    string floorName = FloorReader.RoomIndexToFloorName(roomIndex);
                    var playerDescriptions = new List<string>();
                    var enemyDescriptions = new List<string>();

                    foreach (var unit in units)
                    {
                        bool isEnemy = FloorReader.IsEnemyUnit(cache, unit);
                        string unitDesc = GetDetailedEnemyDescription(cache, unit, announcedKeywords);

                        if (isEnemy)
                        {
                            enemyDescriptions.Add(unitDesc);
                        }
                        else
                        {
                            playerDescriptions.Add(unitDesc);
                        }
                    }

                    // Announce floor if it has any units
                    if (playerDescriptions.Count > 0 || enemyDescriptions.Count > 0)
                    {
                        hasAnyUnits = true;
                        output?.Queue($"{floorName}:");

                        // Announce player units first
                        foreach (var desc in playerDescriptions)
                        {
                            output?.Queue($"  Your unit: {desc}");
                        }

                        // Then announce enemies
                        foreach (var desc in enemyDescriptions)
                        {
                            output?.Queue($"  Enemy: {desc}");
                        }
                    }
                }

                // Also check pyre room (room index 3)
                var pyreRoom = FloorReader.GetRoom(cache, 3);
                if (pyreRoom != null)
                {
                    roomsFound++;
                    var pyreUnits = FloorReader.GetUnitsInRoom(pyreRoom);
                    totalUnits += pyreUnits.Count;
                    // Pyre room units would be announced here if needed, but typically empty
                }

                MonsterTrainAccessibility.LogInfo($"AnnounceEnemies: found {roomsFound} rooms, {totalUnits} total units, hasAnyUnits: {hasAnyUnits}");

                if (!hasAnyUnits)
                {
                    output?.Queue("No units on the train");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error announcing units: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read units", false);
            }
        }

        /// <summary>
        /// Get a detailed description of any unit (public wrapper for targeting)
        /// </summary>
        internal static string GetDetailedUnitDescription(BattleManagerCache cache, object unit)
        {
            return GetDetailedEnemyDescription(cache, unit, BattleAccessibility.AnnouncedKeywords);
        }

        /// <summary>
        /// Get a detailed description of an enemy unit including stats, status effects, and intent
        /// </summary>
        internal static string GetDetailedEnemyDescription(BattleManagerCache cache, object unit, HashSet<string> announcedKeywords = null)
        {
            try
            {
                var sb = new StringBuilder();

                // Get basic info
                string name = FloorReader.GetUnitName(cache, unit);
                int hp = FloorReader.GetUnitHP(cache, unit);
                int maxHp = FloorReader.GetUnitMaxHP(unit);
                int attack = FloorReader.GetUnitAttack(cache, unit);

                sb.Append($"{name}: {attack} attack, {hp}");
                if (maxHp > 0 && maxHp != hp)
                {
                    sb.Append($" of {maxHp}");
                }
                sb.Append(" health");

                // Get unit abilities/description from CharacterData
                string abilities = GetUnitAbilities(cache, unit);
                if (!string.IsNullOrEmpty(abilities))
                {
                    sb.Append($". {abilities}");
                }

                // Get status effects
                string statusEffects = GetUnitStatusEffects(unit);
                if (!string.IsNullOrEmpty(statusEffects))
                {
                    sb.Append($". Status: {statusEffects}");
                }

                // Add keyword explanations for status effects and abilities
                string keywordExplanations = GetUnitKeywordExplanations(statusEffects, abilities, announcedKeywords);
                if (!string.IsNullOrEmpty(keywordExplanations))
                {
                    sb.Append($". Keywords: {keywordExplanations}");
                }

                // Get intent (for bosses or units with visible intent)
                string intent = GetUnitIntent(cache, unit);
                if (!string.IsNullOrEmpty(intent))
                {
                    sb.Append($". Intent: {intent}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting enemy description: {ex.Message}");
                return FloorReader.GetUnitName(cache, unit) ?? "Unknown enemy";
            }
        }

        /// <summary>
        /// Get unit abilities/description from CharacterData including subtypes, triggers, and traits
        /// </summary>
        internal static string GetUnitAbilities(BattleManagerCache cache, object characterState)
        {
            try
            {
                var type = characterState.GetType();
                var parts = new List<string>();

                // Get CharacterData from CharacterState
                var getCharDataMethod = type.GetMethod("GetCharacterData", Type.EmptyTypes);
                object charData = null;
                if (getCharDataMethod != null)
                {
                    charData = getCharDataMethod.Invoke(characterState, null);
                }

                if (charData != null)
                {
                    var charDataType = charData.GetType();

                    // Check if unit can attack
                    var getCanAttackMethod = charDataType.GetMethod("GetCanAttack", Type.EmptyTypes);
                    if (getCanAttackMethod != null)
                    {
                        var canAttackResult = getCanAttackMethod.Invoke(charData, null);
                        if (canAttackResult is bool canAttack && !canAttack)
                        {
                            parts.Add("Does not attack");
                        }
                    }

                    // Get subtypes (like "Treasure", etc.)
                    var getSubtypesMethod = charDataType.GetMethod("GetSubtypeKeys", Type.EmptyTypes);
                    if (getSubtypesMethod != null)
                    {
                        var subtypes = getSubtypesMethod.Invoke(charData, null) as System.Collections.IEnumerable;
                        if (subtypes != null)
                        {
                            foreach (var subtype in subtypes)
                            {
                                string subtypeStr = subtype?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(subtypeStr) && subtypeStr != "SubtypesData_None")
                                {
                                    // Clean up subtype name
                                    subtypeStr = subtypeStr.Replace("SubtypesData_", "").Replace("_", " ");
                                    if (!string.IsNullOrEmpty(subtypeStr))
                                    {
                                        parts.Add(subtypeStr);
                                    }
                                }
                            }
                        }
                    }

                    // Try to get description/abilities from triggers
                    var getTriggersMethod = charDataType.GetMethod("GetTriggers", Type.EmptyTypes);
                    if (getTriggersMethod != null)
                    {
                        var triggers = getTriggersMethod.Invoke(charData, null) as System.Collections.IList;
                        if (triggers != null && triggers.Count > 0)
                        {
                            foreach (var trigger in triggers)
                            {
                                string triggerDesc = GetTriggerDescription(trigger);
                                if (!string.IsNullOrEmpty(triggerDesc))
                                {
                                    parts.Add(triggerDesc);
                                }
                            }
                        }
                    }

                    // Check for special behaviors like flees, winged, etc.
                    string specialBehaviors = GetSpecialBehaviors(charData, charDataType);
                    if (!string.IsNullOrEmpty(specialBehaviors))
                    {
                        parts.Add(specialBehaviors);
                    }
                }

                return parts.Count > 0 ? string.Join(". ", parts) : null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting unit abilities: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get description of a character trigger (ability that triggers on certain conditions)
        /// </summary>
        private static string GetTriggerDescription(object trigger)
        {
            try
            {
                if (trigger == null) return null;
                var triggerType = trigger.GetType();

                // Get the localized trigger name (e.g. "Strike:", "On Death:")
                string triggerName = null;
                var getKeywordTextMethod = triggerType.GetMethod("GetKeywordText", Type.EmptyTypes);
                if (getKeywordTextMethod == null)
                {
                    // CharacterTriggerData.GetKeywordText has optional bool param - find it
                    foreach (var m in triggerType.GetMethods())
                    {
                        if (m.Name == "GetKeywordText" && m.GetParameters().Length <= 1)
                        {
                            getKeywordTextMethod = m;
                            break;
                        }
                    }
                }
                if (getKeywordTextMethod != null)
                {
                    // Handle optional parameters
                    var methodParams = getKeywordTextMethod.GetParameters();
                    object[] callArgs;
                    if (methodParams.Length == 0)
                        callArgs = Array.Empty<object>();
                    else
                    {
                        callArgs = new object[methodParams.Length];
                        for (int i = 0; i < methodParams.Length; i++)
                            callArgs[i] = methodParams[i].HasDefaultValue ? methodParams[i].DefaultValue : false;
                    }
                    triggerName = getKeywordTextMethod.Invoke(trigger, callArgs) as string;
                    if (!string.IsNullOrEmpty(triggerName))
                        triggerName = TextUtilities.StripRichTextTags(triggerName).Trim().TrimEnd(':');
                }

                // Detect unlocalized KEY>>...<< format and discard it
                if (!string.IsNullOrEmpty(triggerName) && IsUnlocalizedKey(triggerName))
                    triggerName = null;

                // Fall back to trigger enum name if keyword text was empty or unlocalized
                if (string.IsNullOrEmpty(triggerName))
                {
                    var getTriggerTypeMethod = triggerType.GetMethod("GetTrigger", Type.EmptyTypes);
                    if (getTriggerTypeMethod != null)
                    {
                        var triggerTypeVal = getTriggerTypeMethod.Invoke(trigger, null);
                        if (triggerTypeVal != null)
                            triggerName = FormatTriggerType(triggerTypeVal.ToString());
                    }
                }

                // Look up the trigger name in KeywordManager to get the tooltip explanation
                // e.g. "Extinguish" -> "Extinguish: Triggers after combat resolves"
                string triggerTooltip = null;
                if (!string.IsNullOrEmpty(triggerName))
                {
                    var keywords = KeywordManager.GetKeywords();
                    if (keywords != null && keywords.TryGetValue(triggerName, out string keywordEntry))
                    {
                        // keywordEntry is "TriggerName: tooltip explanation"
                        // Extract just the tooltip part after the name
                        int colonIdx = keywordEntry.IndexOf(':');
                        if (colonIdx >= 0 && colonIdx < keywordEntry.Length - 1)
                        {
                            triggerTooltip = keywordEntry.Substring(colonIdx + 1).Trim();
                            // Also discard unlocalized tooltip values
                            if (IsUnlocalizedKey(triggerTooltip))
                                triggerTooltip = null;
                        }
                    }
                }

                // Get the description key and localize it to get the actual effect text
                string description = null;
                var getDescKeyMethod = triggerType.GetMethod("GetDescriptionKey", Type.EmptyTypes);
                if (getDescKeyMethod != null)
                {
                    var descKey = getDescKeyMethod.Invoke(trigger, null) as string;
                    if (!string.IsNullOrEmpty(descKey))
                    {
                        var localized = KeywordManager.TryLocalize(descKey);
                        if (!string.IsNullOrEmpty(localized) && !IsUnlocalizedKey(localized))
                        {
                            // Resolve {[effect0.power]} etc. placeholders before stripping tags
                            if (localized.Contains("{["))
                                localized = ResolveTriggerEffectPlaceholders(localized, trigger, triggerType);
                            description = TextUtilities.StripRichTextTags(localized).Trim();
                            if (IsUnlocalizedKey(description))
                                description = null;
                        }
                    }
                }

                // If description is still null, try reading effect descriptions directly
                if (string.IsNullOrEmpty(description))
                {
                    description = GetTriggerEffectDescriptions(trigger, triggerType);
                }

                // Combine: "Extinguish (triggers after combat): Gain 50 gold"
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(triggerName))
                {
                    sb.Append(triggerName);
                    if (!string.IsNullOrEmpty(triggerTooltip))
                        sb.Append($" ({triggerTooltip})");
                    if (!string.IsNullOrEmpty(description))
                        sb.Append($": {description}");
                }
                else if (!string.IsNullOrEmpty(description))
                {
                    sb.Append(description);
                }

                if (sb.Length > 0)
                    return sb.ToString();
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Detect unlocalized key format: KEY>>...<< or raw localization key patterns
        /// </summary>
        internal static bool IsUnlocalizedKey(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.Contains("KEY>>") || text.Contains("<<");
        }

        /// <summary>
        /// Try to get effect descriptions directly from a trigger's CardEffectData list.
        /// Fallback when localized description keys fail.
        /// </summary>
        private static string GetTriggerEffectDescriptions(object trigger, Type triggerType)
        {
            try
            {
                var getEffectsMethod = triggerType.GetMethod("GetEffects", Type.EmptyTypes);
                if (getEffectsMethod == null)
                {
                    foreach (var m in triggerType.GetMethods())
                    {
                        if (m.Name == "GetEffects" && m.GetParameters().Length <= 1)
                        {
                            getEffectsMethod = m;
                            break;
                        }
                    }
                }
                if (getEffectsMethod == null) return null;

                var methodParams = getEffectsMethod.GetParameters();
                object[] callArgs;
                if (methodParams.Length == 0)
                    callArgs = Array.Empty<object>();
                else
                {
                    callArgs = new object[methodParams.Length];
                    for (int i = 0; i < methodParams.Length; i++)
                        callArgs[i] = methodParams[i].HasDefaultValue ? methodParams[i].DefaultValue : true;
                }

                var effects = getEffectsMethod.Invoke(trigger, callArgs) as System.Collections.IList;
                if (effects == null || effects.Count == 0) return null;

                var descriptions = new List<string>();
                foreach (var effect in effects)
                {
                    if (effect == null) continue;
                    var effectType = effect.GetType();

                    // Try GetCardText() for CardEffectState / CardEffectData
                    string effectText = null;
                    var getCardTextMethod = effectType.GetMethod("GetCardText");
                    if (getCardTextMethod != null)
                    {
                        var ctParams = getCardTextMethod.GetParameters();
                        object[] ctArgs = new object[ctParams.Length];
                        for (int i = 0; i < ctParams.Length; i++)
                            ctArgs[i] = ctParams[i].HasDefaultValue ? ctParams[i].DefaultValue : null;

                        effectText = getCardTextMethod.Invoke(effect, ctArgs) as string;
                    }

                    if (!string.IsNullOrEmpty(effectText))
                    {
                        effectText = TextUtilities.StripRichTextTags(effectText).Trim();
                        if (!string.IsNullOrEmpty(effectText) && !IsUnlocalizedKey(effectText))
                            descriptions.Add(effectText);
                    }
                }

                if (descriptions.Count > 0)
                    return string.Join(". ", descriptions);
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Resolve {[effect0.power]}, {[effect0.status0.power]}, {[#effect0.power]} placeholders
        /// in trigger description text using the trigger's effect data.
        /// </summary>
        private static string ResolveTriggerEffectPlaceholders(string text, object trigger, Type triggerType)
        {
            try
            {
                // Get effects from the trigger (works for both CharacterTriggerState and CharacterTriggerData)
                var getEffectsMethod = triggerType.GetMethod("GetEffects", Type.EmptyTypes);
                // CharacterTriggerState.GetEffects() has an optional bool param; try no-arg first
                if (getEffectsMethod == null)
                {
                    // Try the overload with bool parameter: GetEffects(bool getStackable = true)
                    foreach (var m in triggerType.GetMethods())
                    {
                        if (m.Name == "GetEffects" && m.GetParameters().Length <= 1)
                        {
                            getEffectsMethod = m;
                            break;
                        }
                    }
                }

                // Also try getting effects from the underlying trigger data
                if (getEffectsMethod == null)
                {
                    var getTriggerDataMethod = triggerType.GetMethod("GetTriggerData", Type.EmptyTypes);
                    if (getTriggerDataMethod != null)
                    {
                        var triggerData = getTriggerDataMethod.Invoke(trigger, null);
                        if (triggerData != null)
                        {
                            var tdType = triggerData.GetType();
                            getEffectsMethod = tdType.GetMethod("GetEffects", Type.EmptyTypes);
                            if (getEffectsMethod != null)
                            {
                                trigger = triggerData;
                                triggerType = tdType;
                            }
                        }
                    }
                }

                if (getEffectsMethod == null) return text;

                // Call GetEffects - handle optional parameters
                var methodParams = getEffectsMethod.GetParameters();
                object[] callArgs;
                if (methodParams.Length == 0)
                    callArgs = Array.Empty<object>();
                else
                {
                    callArgs = new object[methodParams.Length];
                    for (int i = 0; i < methodParams.Length; i++)
                        callArgs[i] = methodParams[i].HasDefaultValue ? methodParams[i].DefaultValue : null;
                }

                var effects = getEffectsMethod.Invoke(trigger, callArgs) as System.Collections.IList;
                if (effects == null || effects.Count == 0) return text;

                // Match {[effect0.power]}, {[effect0.status0.power]}, {[#effect0.power]}, {[#effect0.status0.power]}
                var regex = new Regex(@"\{\[#?effect(\d+)\.(?:status(\d+)\.)?(\w+)\]\}");

                text = regex.Replace(text, match =>
                {
                    int effectIndex = int.Parse(match.Groups[1].Value);
                    string property = match.Groups[3].Value.ToLower();
                    int statusIndex = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : -1;

                    if (effectIndex >= effects.Count) return match.Value;
                    var effect = effects[effectIndex];
                    if (effect == null) return match.Value;

                    var effectType = effect.GetType();
                    var bindFlags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

                    // Status effect stack count: {[effect0.status0.power]}
                    if (statusIndex >= 0 && property == "power")
                    {
                        var statusField = effectType.GetField("paramStatusEffects", bindFlags);
                        if (statusField != null)
                        {
                            var statusEffects = statusField.GetValue(effect) as Array;
                            if (statusEffects != null && statusIndex < statusEffects.Length)
                            {
                                var se = statusEffects.GetValue(statusIndex);
                                if (se != null)
                                {
                                    var countField = se.GetType().GetField("count", BindingFlags.Public | BindingFlags.Instance);
                                    if (countField != null)
                                        return countField.GetValue(se)?.ToString() ?? match.Value;
                                }
                            }
                        }
                        return match.Value;
                    }

                    // Map property name to field name
                    string fieldName;
                    switch (property)
                    {
                        case "power": fieldName = "paramInt"; break;
                        case "powerabs": fieldName = "paramInt"; break;
                        case "minpower": fieldName = "paramMinInt"; break;
                        case "maxpower": fieldName = "paramMaxInt"; break;
                        default: fieldName = "param" + char.ToUpper(property[0]) + property.Substring(1); break;
                    }

                    var field = effectType.GetField(fieldName, bindFlags);
                    if (field != null)
                    {
                        var value = field.GetValue(effect);
                        if (property == "powerabs" && value is int intVal)
                            return Math.Abs(intVal).ToString();
                        return value?.ToString() ?? match.Value;
                    }

                    return match.Value;
                });

                // Also handle {[trait0.power]} patterns (less common in triggers but possible)
                var traitRegex = new Regex(@"\{\[#?trait(\d+)\.(\w+)\]\}");
                if (traitRegex.IsMatch(text))
                {
                    // Traits are on the parent card, not the trigger - just strip unresolved ones
                    text = traitRegex.Replace(text, "");
                }

                // Strip any remaining unresolved placeholders like {[...]} to avoid reading raw variables
                var unresolvedRegex = new Regex(@"\{\[[^\]]*\]\}");
                text = unresolvedRegex.Replace(text, "");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"ResolveTriggerEffectPlaceholders error: {ex.Message}");
            }

            return text;
        }

        /// <summary>
        /// Format a trigger type into readable text
        /// </summary>
        private static string FormatTriggerType(string triggerType)
        {
            if (string.IsNullOrEmpty(triggerType)) return null;

            switch (triggerType.ToLower())
            {
                case "ondeath": return "Extinguish";
                case "postcombat": return "Resolve";
                case "onspawn": return "Summon";
                case "onspawnnotfromcard": return "Summon";
                case "onattacking": return "Strike";
                case "onkill": return "Slay";
                case "onhit": return "On hit";
                case "onheal": return "On heal";
                case "onteamturnbegin": return "On team turn begin";
                case "onturnbegin": return "On turn begin";
                case "precombat": return "Pre-combat";
                case "postascension": return "After ascending";
                case "postdescension": return "After descending";
                case "postcombatcharacterability":
                case "postcombatheraling": return "After combat healing";
                case "cardspellplayed": return "On spell played";
                case "cardmonsterplayed": return "On unit played";
                case "cardcorruptplayed": return "On corrupt played";
                case "cardexhausted": return "On card consumed";
                case "corruptionadded": return "On corruption added";
                case "onarmoradded": return "On armor added";
                case "onfoodspawn": return "On morsel spawn";
                case "endturnprehanddiscard": return "End of turn";
                case "onfeed": return "On feed";
                case "oneaten": return "On eaten";
                case "onburnout": return "On burnout";
                case "onhatched": return "On hatched";
                case "onendofcombat": return "End of combat";
                case "afterspawnenchant": return "After spawn enchant";
                case "onanyherodeathonfloor": return "On enemy death on floor";
                case "onanymonsterdeathonfloor": return "On friendly death on floor";
                case "onanyunitdeathonfloor": return "On any unit death on floor";
                default:
                    // Convert camelCase to readable
                    return Regex.Replace(triggerType, "(\\B[A-Z])", " $1");
            }
        }

        /// <summary>
        /// Get special behaviors from CharacterData (flees, winged, treasure, etc.)
        /// </summary>
        private static string GetSpecialBehaviors(object charData, Type charDataType)
        {
            var behaviors = new List<string>();

            try
            {
                // Check for isTreasure or similar flags
                var fields = charDataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    string fieldName = field.Name.ToLower();

                    // Check for treasure/fleeing units
                    if (fieldName.Contains("treasure") || fieldName.Contains("flees") || fieldName.Contains("fleeing"))
                    {
                        var val = field.GetValue(charData);
                        if (val is bool bVal && bVal)
                        {
                            if (fieldName.Contains("treasure")) behaviors.Add("Treasure unit (drops reward on kill)");
                            else if (fieldName.Contains("flee")) behaviors.Add("Flees after combat round");
                        }
                    }

                    // Check for winged/flying
                    if (fieldName.Contains("winged") || fieldName.Contains("flying"))
                    {
                        var val = field.GetValue(charData);
                        if (val is bool bVal && bVal)
                        {
                            behaviors.Add("Winged (enters random floor)");
                        }
                    }
                }

                // Check properties too
                var properties = charDataType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in properties)
                {
                    string propName = prop.Name.ToLower();

                    if (propName.Contains("treasure") || propName.Contains("flees"))
                    {
                        try
                        {
                            var val = prop.GetValue(charData);
                            if (val is bool bVal && bVal)
                            {
                                if (propName.Contains("treasure")) behaviors.Add("Treasure unit");
                                else if (propName.Contains("flee")) behaviors.Add("Flees");
                            }
                        }
                        catch { }
                    }
                }

                // Try GetIsFleeingUnit or similar methods
                var fleeMethods = charDataType.GetMethods()
                    .Where(m => m.Name.ToLower().Contains("flee") && m.GetParameters().Length == 0);
                foreach (var method in fleeMethods)
                {
                    try
                    {
                        var result = method.Invoke(charData, null);
                        if (result is bool bVal && bVal)
                        {
                            if (!behaviors.Any(b => b.Contains("Flee")))
                                behaviors.Add("Flees after combat round");
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting special behaviors: {ex.Message}");
            }

            return behaviors.Count > 0 ? string.Join(", ", behaviors) : null;
        }

        /// <summary>
        /// Get status effects on a unit as a readable string
        /// </summary>
        internal static string GetUnitStatusEffects(object characterState)
        {
            try
            {
                var type = characterState.GetType();

                // Try GetStatusEffects method which takes an out parameter
                var getStatusMethod = type.GetMethods()
                    .FirstOrDefault(m => m.Name == "GetStatusEffects" && m.GetParameters().Length >= 1);

                if (getStatusMethod != null)
                {
                    // Create the list parameter
                    var parameters = getStatusMethod.GetParameters();
                    var listType = parameters[0].ParameterType;

                    // Handle out parameter - need to create array for Invoke
                    var args = new object[parameters.Length];

                    // For out parameters, we pass null and get the value back
                    if (parameters[0].IsOut)
                    {
                        args[0] = null;
                    }
                    else
                    {
                        // Create empty list
                        args[0] = Activator.CreateInstance(listType);
                    }

                    // Fill additional params with defaults
                    for (int i = 1; i < parameters.Length; i++)
                    {
                        if (parameters[i].ParameterType == typeof(bool))
                            args[i] = false;
                        else
                            args[i] = parameters[i].ParameterType.IsValueType
                                ? Activator.CreateInstance(parameters[i].ParameterType)
                                : null;
                    }

                    getStatusMethod.Invoke(characterState, args);

                    // The list should now be populated (args[0] for out param)
                    var statusList = args[0] as System.Collections.IList;
                    if (statusList != null && statusList.Count > 0)
                    {
                        var effects = new List<string>();
                        foreach (var statusStack in statusList)
                        {
                            string effectName = GetStatusEffectName(statusStack);
                            int stacks = GetStatusEffectStacks(statusStack);

                            if (!string.IsNullOrEmpty(effectName))
                            {
                                if (stacks > 1)
                                    effects.Add($"{effectName} {stacks}");
                                else
                                    effects.Add(effectName);
                            }
                        }

                        if (effects.Count > 0)
                        {
                            return string.Join(", ", effects);
                        }
                    }
                }

                // Alternative: try to get individual status effects by common IDs
                var commonStatuses = new[] { "armor", "damage shield", "rage", "haste", "multistrike", "regen",
                    "poison", "sap", "dazed", "rooted", "spell weakness", "spikes", "lifesteal", "stealth",
                    "fragile", "endless", "quick", "trample", "sweep", "melee weakness" };
                var foundEffects = new List<string>();

                var getStacksMethod = type.GetMethod("GetStatusEffectStacks", new[] { typeof(string) });
                if (getStacksMethod != null)
                {
                    foreach (var statusId in commonStatuses)
                    {
                        try
                        {
                            var result = getStacksMethod.Invoke(characterState, new object[] { statusId });
                            if (result is int stacks && stacks > 0)
                            {
                                string displayName = Patches.CharacterStateHelper.CleanStatusName(statusId);
                                if (stacks > 1)
                                    foundEffects.Add($"{displayName} {stacks}");
                                else
                                    foundEffects.Add(displayName);
                            }
                        }
                        catch { }
                    }

                    if (foundEffects.Count > 0)
                    {
                        return string.Join(", ", foundEffects);
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting status effects: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get the name of a status effect from a StatusEffectStack
        /// </summary>
        private static string GetStatusEffectName(object statusStack)
        {
            try
            {
                var stackType = statusStack.GetType();

                // Try to get State property which returns StatusEffectState
                var stateProp = stackType.GetProperty("State");
                if (stateProp != null)
                {
                    var state = stateProp.GetValue(statusStack);
                    if (state != null)
                    {
                        var stateType = state.GetType();

                        // Use GetDisplayName() first - it calls StatusEffectManager.GetLocalizedName()
                        // which properly maps internal IDs to display names (e.g., "poison" -> "Frostbite")
                        var getDisplayNameMethod = stateType.GetMethod("GetDisplayName");
                        if (getDisplayNameMethod != null)
                        {
                            var args = new object[] { false }; // inBold = false
                            var displayName = getDisplayNameMethod.Invoke(state, args) as string;
                            if (!string.IsNullOrEmpty(displayName))
                            {
                                return TextUtilities.StripRichTextTags(displayName);
                            }
                        }

                        // Fallback: use GetStatusId with localization via CharacterStateHelper
                        var getIdMethod = stateType.GetMethod("GetStatusId", Type.EmptyTypes);
                        if (getIdMethod != null)
                        {
                            var id = getIdMethod.Invoke(state, null) as string;
                            if (!string.IsNullOrEmpty(id))
                            {
                                return Patches.CharacterStateHelper.CleanStatusName(id);
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get stack count from a StatusEffectStack
        /// </summary>
        private static int GetStatusEffectStacks(object statusStack)
        {
            try
            {
                var stackType = statusStack.GetType();

                // Try Count property
                var countProp = stackType.GetProperty("Count");
                if (countProp != null)
                {
                    var result = countProp.GetValue(statusStack);
                    if (result is int count) return count;
                }

                // Try count field
                var countField = stackType.GetField("count", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (countField != null)
                {
                    var result = countField.GetValue(statusStack);
                    if (result is int count) return count;
                }
            }
            catch { }
            return 1;
        }

        /// <summary>
        /// Format a status effect ID into a readable name
        /// </summary>
        private static string FormatStatusName(string statusId)
        {
            if (string.IsNullOrEmpty(statusId)) return statusId;

            // Convert snake_case or camelCase to Title Case
            statusId = statusId.Replace("_", " ");
            statusId = Regex.Replace(statusId, "([a-z])([A-Z])", "$1 $2");

            // Capitalize first letter of each word
            var words = statusId.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }

            return string.Join(" ", words);
        }

        /// <summary>
        /// Get the intent/action of an enemy (what they will do)
        /// </summary>
        internal static string GetUnitIntent(BattleManagerCache cache, object characterState)
        {
            try
            {
                var type = characterState.GetType();

                // Check if this is a boss with a BossState
                var getBossStateMethod = type.GetMethod("GetBossState", Type.EmptyTypes);
                if (getBossStateMethod != null)
                {
                    var bossState = getBossStateMethod.Invoke(characterState, null);
                    if (bossState != null)
                    {
                        string bossIntent = GetBossIntent(bossState);
                        if (!string.IsNullOrEmpty(bossIntent))
                        {
                            return bossIntent;
                        }
                    }
                }

                // For regular enemies, try to get their current action/behavior
                // Check for ActionGroupState or similar
                var getActionMethod = type.GetMethod("GetCurrentAction", Type.EmptyTypes) ??
                                     type.GetMethod("GetNextAction", Type.EmptyTypes);
                if (getActionMethod != null)
                {
                    var action = getActionMethod.Invoke(characterState, null);
                    if (action != null)
                    {
                        return GetActionDescription(action);
                    }
                }

                // Try to get character data for special abilities
                var getCharDataMethod = type.GetMethod("GetCharacterData", Type.EmptyTypes);
                if (getCharDataMethod != null)
                {
                    var charData = getCharDataMethod.Invoke(characterState, null);
                    if (charData != null)
                    {
                        string specialAbility = GetCharacterSpecialAbility(charData);
                        if (!string.IsNullOrEmpty(specialAbility))
                        {
                            return specialAbility;
                        }
                    }
                }

                // Try to get trigger effects (abilities that activate on certain conditions)
                var getTriggerMethod = type.GetMethod("GetTriggers", Type.EmptyTypes) ??
                                       type.GetMethod("GetCharacterTriggers", Type.EmptyTypes);
                if (getTriggerMethod != null)
                {
                    var triggers = getTriggerMethod.Invoke(characterState, null) as System.Collections.IList;
                    if (triggers != null && triggers.Count > 0)
                    {
                        var triggerDescs = new List<string>();
                        foreach (var trigger in triggers)
                        {
                            string triggerDesc = GetTriggerDescription(trigger);
                            if (!string.IsNullOrEmpty(triggerDesc))
                            {
                                triggerDescs.Add(triggerDesc);
                            }
                        }
                        if (triggerDescs.Count > 0)
                        {
                            return string.Join(", ", triggerDescs);
                        }
                    }
                }

                // Check attack damage to infer basic intent
                int attack = FloorReader.GetUnitAttack(cache, characterState);
                if (attack > 0)
                {
                    return $"Will attack for {attack} damage";
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting unit intent: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get special ability description from character data (healing, buffs, etc.)
        /// </summary>
        private static string GetCharacterSpecialAbility(object charData)
        {
            try
            {
                var charType = charData.GetType();

                // Look for subtypes (healing characters, support characters, etc.)
                var subtypesField = charType.GetField("subtypes", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (subtypesField != null)
                {
                    var subtypes = subtypesField.GetValue(charData) as System.Collections.IList;
                    if (subtypes != null)
                    {
                        foreach (var subtype in subtypes)
                        {
                            string subtypeName = subtype?.ToString()?.ToLower() ?? "";
                            if (subtypeName.Contains("healer") || subtypeName.Contains("support"))
                            {
                                return "Healer/Support unit";
                            }
                        }
                    }
                }

                // Look for triggers that might indicate special behavior
                var triggersField = charType.GetField("triggers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (triggersField != null)
                {
                    var triggers = triggersField.GetValue(charData) as System.Collections.IList;
                    if (triggers != null && triggers.Count > 0)
                    {
                        foreach (var trigger in triggers)
                        {
                            string desc = GetTriggerDescription(trigger);
                            if (!string.IsNullOrEmpty(desc))
                            {
                                return desc;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting character special ability: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get the intent of a boss enemy via BossState.GetNextBossAction() -> BossActionState
        /// </summary>
        private static string GetBossIntent(object bossState)
        {
            try
            {
                var bossType = bossState.GetType();

                // BossState.GetNextBossAction() returns BossActionState
                var getNextActionMethod = bossType.GetMethod("GetNextBossAction", Type.EmptyTypes);
                if (getNextActionMethod != null)
                {
                    var bossAction = getNextActionMethod.Invoke(bossState, null);
                    if (bossAction != null)
                    {
                        var actionType = bossAction.GetType();
                        var parts = new List<string>();

                        // BossActionState.GetTooltipDescription() - localized description
                        var getDescMethod = actionType.GetMethod("GetTooltipDescription", Type.EmptyTypes);
                        if (getDescMethod != null)
                        {
                            string desc = getDescMethod.Invoke(bossAction, null) as string;
                            if (!string.IsNullOrEmpty(desc))
                            {
                                parts.Add(TextUtilities.StripRichTextTags(desc).Trim());
                            }
                        }

                        // BossActionState.GetTargetedRoomIndex()
                        var getTargetRoomMethod = actionType.GetMethod("GetTargetedRoomIndex", Type.EmptyTypes);
                        if (getTargetRoomMethod != null)
                        {
                            var result = getTargetRoomMethod.Invoke(bossAction, null);
                            if (result is int roomIndex && roomIndex >= 0 && roomIndex <= 2)
                            {
                                parts.Add($"targeting {FloorReader.RoomIndexToFloorName(roomIndex).ToLower()}");
                            }
                        }

                        if (parts.Count > 0)
                        {
                            return string.Join(", ", parts);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting boss intent: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get description of a boss action
        /// </summary>
        private static string GetBossActionDescription(object bossAction)
        {
            try
            {
                var actionType = bossAction.GetType();
                var parts = new List<string>();

                // Get target room
                var getTargetRoomMethod = actionType.GetMethod("GetTargetedRoomIndex", Type.EmptyTypes);
                if (getTargetRoomMethod != null)
                {
                    var result = getTargetRoomMethod.Invoke(bossAction, null);
                    if (result is int roomIndex && roomIndex >= 0)
                    {
                        parts.Add($"targeting {FloorReader.RoomIndexToFloorName(roomIndex).ToLower()}");
                    }
                }

                // Get effects/damage
                var getEffectsMethod = actionType.GetMethod("GetEffects", Type.EmptyTypes);
                if (getEffectsMethod != null)
                {
                    var effects = getEffectsMethod.Invoke(bossAction, null) as System.Collections.IList;
                    if (effects != null && effects.Count > 0)
                    {
                        foreach (var effect in effects)
                        {
                            string effectDesc = GetActionDescription(effect);
                            if (!string.IsNullOrEmpty(effectDesc))
                            {
                                parts.Add(effectDesc);
                                break; // Just get the first meaningful effect
                            }
                        }
                    }
                }

                if (parts.Count > 0)
                {
                    return string.Join(", ", parts);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting boss action description: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get description of a card/action effect
        /// </summary>
        private static string GetActionDescription(object action)
        {
            try
            {
                var actionType = action.GetType();

                // Try GetDescription
                var getDescMethod = actionType.GetMethod("GetDescription", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    var desc = getDescMethod.Invoke(action, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                    {
                        return TextUtilities.StripRichTextTags(desc);
                    }
                }

                // Try to get damage amount
                var getDamageMethod = actionType.GetMethod("GetDamageAmount", Type.EmptyTypes) ??
                                     actionType.GetMethod("GetParamInt", Type.EmptyTypes);
                if (getDamageMethod != null)
                {
                    var result = getDamageMethod.Invoke(action, null);
                    if (result is int damage && damage > 0)
                    {
                        return $"{damage} damage";
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Look up keyword explanations for status effect and ability names found on a unit.
        /// </summary>
        internal static string GetUnitKeywordExplanations(string statusEffects, string abilities, HashSet<string> announcedKeywords = null)
        {
            var keywords = KeywordManager.GetKeywords();
            if (keywords == null || keywords.Count == 0) return null;

            var explanations = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Extract individual keyword names from the comma-separated status effects
            // Format is like "Relentless, Hunter 3, Immune" - strip stack counts
            void CheckText(string text)
            {
                if (string.IsNullOrEmpty(text)) return;
                foreach (var part in text.Split(','))
                {
                    // Trim and remove trailing stack count (e.g. "Armor 5" -> "Armor")
                    string trimmed = part.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    // Remove trailing number (stack count)
                    string keyName = Regex.Replace(trimmed, @"\s+\d+$", "").Trim();
                    if (string.IsNullOrEmpty(keyName) || seen.Contains(keyName)) continue;
                    seen.Add(keyName);

                    // Skip if already announced in this browsing session
                    if (announcedKeywords != null && !announcedKeywords.Add(keyName)) continue;

                    if (keywords.TryGetValue(keyName, out string explanation))
                    {
                        explanations.Add(explanation);
                    }
                }
            }

            CheckText(statusEffects);
            CheckText(abilities);

            return explanations.Count > 0 ? string.Join(". ", explanations) + "." : null;
        }
    }
}
