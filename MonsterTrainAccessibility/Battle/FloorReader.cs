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
    /// Reads floor/room state including units, capacity, corruption, and enchantments.
    /// </summary>
    internal static class FloorReader
    {
        /// <summary>
        /// Convert a room index to a user-facing floor name.
        /// Room 0 = Bottom floor, Room 1 = Middle floor, Room 2 = Top floor, Room 3 = Pyre room.
        /// </summary>
        internal static string RoomIndexToFloorName(int roomIndex)
        {
            switch (roomIndex)
            {
                case 0: return "Bottom floor";
                case 1: return "Middle floor";
                case 2: return "Top floor";
                case 3: return "Pyre room";
                default: return "Unknown floor";
            }
        }

        /// <summary>
        /// Announce all floors
        /// </summary>
        internal static void AnnounceAllFloors(BattleManagerCache cache, HashSet<string> announcedKeywords = null)
        {
            try
            {
                var output = MonsterTrainAccessibility.ScreenReader;
                output?.Speak("Floor status:", false);

                // Monster Train has 3 playable floors + pyre room
                // Room indices: 0=bottom, 1=middle, 2=top, 3=pyre room
                for (int roomIndex = 0; roomIndex <= 3; roomIndex++)
                {
                    var room = GetRoom(cache, roomIndex);
                    if (room != null)
                    {
                        if (roomIndex == 3)
                        {
                            // Pyre room - show pyre health and any units
                            var pyreUnits = GetUnitsInRoom(room);
                            int pyreHP = ResourceReader.GetPyreHealth(cache);
                            int maxPyreHP = ResourceReader.GetMaxPyreHealth(cache);
                            var sb = new StringBuilder();
                            sb.Append("Pyre room");
                            if (pyreHP >= 0)
                            {
                                sb.Append($": Pyre {pyreHP} of {maxPyreHP} health");
                            }
                            if (pyreUnits.Count > 0)
                            {
                                var unitDescs = new List<string>();
                                foreach (var unit in pyreUnits)
                                {
                                    string unitDesc = GetUnitBriefDescription(cache, unit, announcedKeywords);
                                    bool isEnemy = IsEnemyUnit(cache, unit);
                                    string prefix = isEnemy ? "Enemy " : "";
                                    unitDescs.Add($"{prefix}{unitDesc}");
                                }
                                sb.Append($". {string.Join(", ", unitDescs)}");
                            }
                            output?.Queue(sb.ToString());
                        }
                        else
                        {
                            // Regular floor - show capacity and units
                            var (usedCapacity, maxCapacity) = GetFloorCapacityInfo(room);
                            var units = GetUnitsInRoom(room);

                            string capacityInfo = maxCapacity > 0 ? $" ({usedCapacity}/{maxCapacity} capacity)" : "";
                            string floorName = $"{RoomIndexToFloorName(roomIndex)}{capacityInfo}";

                            // Get floor corruption (DLC)
                            string corruption = GetFloorCorruption(room);
                            if (!string.IsNullOrEmpty(corruption))
                            {
                                floorName += $". {corruption}";
                            }

                            // Get floor enchantments/modifiers
                            string enchantments = GetFloorEnchantments(room);
                            if (!string.IsNullOrEmpty(enchantments))
                            {
                                floorName += $". {enchantments}";
                            }

                            if (units.Count == 0)
                            {
                                output?.Queue($"{floorName}: Empty");
                            }
                            else
                            {
                                var descriptions = new List<string>();
                                foreach (var unit in units)
                                {
                                    string unitDesc = GetUnitBriefDescription(cache, unit, announcedKeywords);
                                    bool isEnemy = IsEnemyUnit(cache, unit);
                                    string prefix = isEnemy ? "Enemy " : "";
                                    descriptions.Add($"{prefix}{unitDesc}");
                                }
                                output?.Queue($"{floorName}: {string.Join(", ", descriptions)}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error announcing floors: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read floors", false);
            }
        }

        /// <summary>
        /// Get a brief description of a unit including attack/health, status effects, abilities, and size
        /// </summary>
        internal static string GetUnitBriefDescription(BattleManagerCache cache, object unit, HashSet<string> announcedKeywords = null)
        {
            string name = GetUnitName(cache, unit);
            int hp = GetUnitHP(cache, unit);
            int maxHp = GetUnitMaxHP(unit);
            int attack = GetUnitAttack(cache, unit);
            int size = GetUnitSize(unit);
            bool isEnemy = IsEnemyUnit(cache, unit);

            var sb = new StringBuilder();
            sb.Append($"{name} {attack} attack, {hp}");
            if (maxHp > 0 && maxHp != hp)
                sb.Append($" of {maxHp}");
            sb.Append(" health");

            // Add all status effects
            string statusEffects = EnemyReader.GetUnitStatusEffects(unit);
            if (!string.IsNullOrEmpty(statusEffects))
            {
                sb.Append($" ({statusEffects})");
            }

            // Add unit abilities/keywords (like Relentless, Multistrike, etc.)
            string abilities = EnemyReader.GetUnitAbilities(cache, unit);
            if (!string.IsNullOrEmpty(abilities))
            {
                sb.Append($". {abilities}");
            }

            // Add keyword explanations for status effects and abilities
            string keywordExplanations = EnemyReader.GetUnitKeywordExplanations(statusEffects, abilities, announcedKeywords);
            if (!string.IsNullOrEmpty(keywordExplanations))
            {
                sb.Append($". Keywords: {keywordExplanations}");
            }

            // Add intent for enemies
            if (isEnemy)
            {
                string intent = EnemyReader.GetUnitIntent(cache, unit);
                if (!string.IsNullOrEmpty(intent))
                {
                    sb.Append($". Intent: {intent}");
                }
            }

            if (size != 1) // Only mention size if it's not the default of 1
            {
                sb.Append($", size {size}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get a brief description of a unit for targeting announcements.
        /// Public wrapper around GetUnitBriefDescription for use by patches.
        /// </summary>
        internal static string GetTargetUnitDescription(BattleManagerCache cache, object characterState)
        {
            if (characterState == null) return null;
            try
            {
                return GetUnitBriefDescription(cache, characterState, BattleAccessibility.AnnouncedKeywords);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting target unit description: {ex.Message}");
                return GetUnitName(cache, characterState) ?? "Unknown unit";
            }
        }

        /// <summary>
        /// Get the armor stacks on a unit
        /// </summary>
        private static int GetUnitArmor(object characterState)
        {
            try
            {
                var type = characterState.GetType();

                // Try GetStatusEffectStacks with armor status ID
                var getStacksMethod = type.GetMethod("GetStatusEffectStacks", new[] { typeof(string) });
                if (getStacksMethod != null)
                {
                    // Try different armor status IDs
                    string[] armorIds = { "armor", "Armor", "StatusEffectArmor" };
                    foreach (var armorId in armorIds)
                    {
                        try
                        {
                            var result = getStacksMethod.Invoke(characterState, new object[] { armorId });
                            if (result is int stacks && stacks > 0)
                            {
                                return stacks;
                            }
                        }
                        catch { }
                    }
                }

                // Alternative: check the character's armor directly
                var getArmorMethod = type.GetMethod("GetArmor", Type.EmptyTypes);
                if (getArmorMethod != null)
                {
                    var result = getArmorMethod.Invoke(characterState, null);
                    if (result is int armor && armor > 0)
                    {
                        return armor;
                    }
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Get the size of a unit (how much floor capacity it uses)
        /// </summary>
        internal static int GetUnitSize(object characterState)
        {
            try
            {
                var type = characterState.GetType();

                // Try GetSize method
                var getSizeMethod = type.GetMethod("GetSize", Type.EmptyTypes);
                if (getSizeMethod != null)
                {
                    var result = getSizeMethod.Invoke(characterState, null);
                    if (result is int size)
                    {
                        return size;
                    }
                }

                // Try getting from CharacterData
                var getCharDataMethod = type.GetMethod("GetCharacterData", Type.EmptyTypes);
                if (getCharDataMethod != null)
                {
                    var charData = getCharDataMethod.Invoke(characterState, null);
                    if (charData != null)
                    {
                        var charDataType = charData.GetType();
                        var dataSizeMethod = charDataType.GetMethod("GetSize", Type.EmptyTypes);
                        if (dataSizeMethod != null)
                        {
                            var result = dataSizeMethod.Invoke(charData, null);
                            if (result is int size)
                            {
                                return size;
                            }
                        }
                    }
                }
            }
            catch { }
            return 1; // Default size
        }

        /// <summary>
        /// Get the capacity info (used and max) for a floor/room from the game's CapacityInfo.
        /// Returns (usedCapacity, maxCapacity). The game tracks player (Monsters) capacity.
        /// </summary>
        internal static (int used, int max) GetFloorCapacityInfo(object room)
        {
            try
            {
                var roomType = room.GetType();

                var getCapacityInfoMethod = roomType.GetMethod("GetCapacityInfo");
                if (getCapacityInfoMethod != null)
                {
                    var parameters = getCapacityInfoMethod.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType.IsEnum)
                    {
                        // Team.Type.Monsters = 0
                        var monstersValue = Enum.ToObject(parameters[0].ParameterType, 0);
                        var capacityInfo = getCapacityInfoMethod.Invoke(room, new[] { monstersValue });
                        if (capacityInfo != null)
                        {
                            var ciType = capacityInfo.GetType();
                            int used = 0, max = 5;
                            var countField = ciType.GetField("count");
                            if (countField != null && countField.GetValue(capacityInfo) is int c)
                                used = c;
                            var maxField = ciType.GetField("max");
                            if (maxField != null && maxField.GetValue(capacityInfo) is int m)
                                max = m;
                            return (used, max);
                        }
                    }
                }
            }
            catch { }
            return (0, 5); // Default floor capacity in Monster Train is 5
        }

        /// Get the maximum capacity of a floor/room
        internal static int GetFloorCapacity(object room)
        {
            return GetFloorCapacityInfo(room).max;
        }

        /// <summary>
        /// Get floor corruption info from a room state (Last Divinity DLC).
        /// Returns e.g. "Corruption: 2/4" or null if corruption is not active.
        /// </summary>
        internal static string GetFloorCorruption(object room)
        {
            try
            {
                if (room == null) return null;
                var roomType = room.GetType();

                // Check if corruption is enabled on this room
                var enabledField = roomType.GetField("corruptionEnabled",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (enabledField != null)
                {
                    bool enabled = (bool)enabledField.GetValue(room);
                    if (!enabled) return null;
                }

                // Get current and max corruption
                var getCurrentMethod = roomType.GetMethod("GetCurrentNonPreviewCorruption", Type.EmptyTypes);
                var getMaxMethod = roomType.GetMethod("GetMaxCorruption", Type.EmptyTypes);

                if (getCurrentMethod != null && getMaxMethod != null)
                {
                    int current = (int)getCurrentMethod.Invoke(room, null);
                    int max = (int)getMaxMethod.Invoke(room, null);

                    if (max > 0)
                    {
                        // Also check permanent corruption
                        var getPermanentMethod = roomType.GetMethod("GetPermanentCorruption", Type.EmptyTypes);
                        int permanent = 0;
                        if (getPermanentMethod != null)
                        {
                            permanent = (int)getPermanentMethod.Invoke(room, null);
                        }

                        string info = $"Corruption: {current}/{max}";
                        if (permanent > 0)
                        {
                            info += $" ({permanent} permanent)";
                        }
                        return info;
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting floor corruption: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get floor enchantments/modifiers from a room state
        /// </summary>
        internal static string GetFloorEnchantments(object room)
        {
            try
            {
                if (room == null) return null;
                var enchantments = new List<string>();

                // Room modifiers live on units (CharacterState.GetRoomStateModifiers()),
                // not on RoomState itself. Iterate all units on this floor.
                var units = GetUnitsInRoom(room);
                foreach (var unit in units)
                {
                    if (unit == null) continue;
                    var unitType = unit.GetType();
                    var getModifiersMethod = unitType.GetMethod("GetRoomStateModifiers", Type.EmptyTypes);
                    if (getModifiersMethod == null) continue;

                    var modifiers = getModifiersMethod.Invoke(unit, null) as System.Collections.IList;
                    if (modifiers == null || modifiers.Count == 0) continue;

                    foreach (var modifier in modifiers)
                    {
                        if (modifier == null) continue;
                        var modType = modifier.GetType();

                        // Try GetDescriptionKeyInPlay first (active description), then GetDescriptionKey
                        string description = null;
                        var getDescInPlayMethod = modType.GetMethod("GetDescriptionKeyInPlay", Type.EmptyTypes);
                        if (getDescInPlayMethod != null)
                        {
                            string descKey = getDescInPlayMethod.Invoke(modifier, null) as string;
                            if (!string.IsNullOrEmpty(descKey))
                                description = KeywordManager.TryLocalize(descKey);
                        }

                        if (string.IsNullOrEmpty(description) || description.Contains("_"))
                        {
                            var getDescMethod = modType.GetMethod("GetDescriptionKey", Type.EmptyTypes);
                            if (getDescMethod != null)
                            {
                                string descKey = getDescMethod.Invoke(modifier, null) as string;
                                if (!string.IsNullOrEmpty(descKey))
                                {
                                    string localized = KeywordManager.TryLocalize(descKey);
                                    if (!string.IsNullOrEmpty(localized) && !localized.Contains("_"))
                                        description = localized;
                                }
                            }
                        }

                        // Try tooltip title as a shorter label
                        if (string.IsNullOrEmpty(description) || description.Contains("_"))
                        {
                            var getTitleMethod = modType.GetMethod("GetExtraTooltipTitleKey", Type.EmptyTypes);
                            if (getTitleMethod != null)
                            {
                                string titleKey = getTitleMethod.Invoke(modifier, null) as string;
                                if (!string.IsNullOrEmpty(titleKey))
                                {
                                    string localized = KeywordManager.TryLocalize(titleKey);
                                    if (!string.IsNullOrEmpty(localized) && !localized.Contains("_"))
                                        description = localized;
                                }
                            }
                        }

                        // Fallback: use the modifier class name in a readable format
                        if (string.IsNullOrEmpty(description) || description.Contains("_"))
                        {
                            string className = modType.Name;
                            // Convert "RoomStateEnergyModifier" -> "Energy Modifier"
                            className = className.Replace("RoomState", "").Replace("Modifier", "");
                            if (!string.IsNullOrEmpty(className))
                                description = Regex.Replace(className, "([a-z])([A-Z])", "$1 $2");
                        }

                        if (!string.IsNullOrEmpty(description))
                        {
                            string cleaned = TextUtilities.StripRichTextTags(description);
                            if (!enchantments.Contains(cleaned))
                                enchantments.Add(cleaned);
                        }
                    }
                }

                if (enchantments.Count > 0)
                {
                    return "Enchantments: " + string.Join(", ", enchantments);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting floor enchantments: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get the currently selected room index from the game state.
        /// Returns room index (0=bottom, 1=middle, 2=top, 3=pyre).
        /// Returns -1 if unable to determine.
        /// </summary>
        internal static int GetSelectedFloor(BattleManagerCache cache)
        {
            try
            {
                if (cache.RoomManager == null)
                {
                    cache.FindManagers();
                }

                if (cache.RoomManager == null)
                {
                    MonsterTrainAccessibility.LogInfo("GetSelectedFloor: RoomManager is null");
                    return -1;
                }

                var roomManagerType = cache.RoomManager.GetType();

                // GetSelectedRoom() returns an int (room index) directly
                var getSelectedRoomMethod = roomManagerType.GetMethod("GetSelectedRoom", Type.EmptyTypes);
                if (getSelectedRoomMethod != null)
                {
                    var result = getSelectedRoomMethod.Invoke(cache.RoomManager, null);
                    if (result is int roomIndex)
                    {
                        MonsterTrainAccessibility.LogInfo($"GetSelectedFloor: GetSelectedRoom() = {roomIndex}");
                        return roomIndex;
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo($"GetSelectedFloor: GetSelectedRoom() returned {result?.GetType().Name ?? "null"}: {result}");
                    }
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("GetSelectedFloor: GetSelectedRoom method not found");
                }

                return -1;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"GetSelectedFloor error: {ex.Message}");
                return -1;
            }
        }

        internal static object GetRoom(BattleManagerCache cache, int roomIndex)
        {
            if (cache.RoomManager == null || cache.GetRoomMethod == null)
            {
                cache.FindManagers();
                if (cache.RoomManager == null)
                {
                    MonsterTrainAccessibility.LogInfo("GetRoom: RoomManager is null");
                    return null;
                }
                if (cache.GetRoomMethod == null)
                {
                    MonsterTrainAccessibility.LogInfo("GetRoom: GetRoomMethod is null");
                    return null;
                }
            }

            try
            {
                var room = cache.GetRoomMethod?.Invoke(cache.RoomManager, new object[] { roomIndex });
                MonsterTrainAccessibility.LogInfo($"GetRoom({roomIndex}): {(room != null ? room.GetType().Name : "null")}");
                return room;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"GetRoom({roomIndex}) error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get a text summary of what's on a specific floor (for floor targeting).
        /// Takes room index directly (0=bottom, 1=middle, 2=top, 3=pyre room).
        /// </summary>
        internal static string GetFloorSummary(BattleManagerCache cache, int roomIndex, HashSet<string> announcedKeywords = null)
        {
            try
            {
                var room = GetRoom(cache, roomIndex);
                if (room == null)
                {
                    return $"{RoomIndexToFloorName(roomIndex)}: Unknown";
                }

                // Pyre room - special handling
                if (roomIndex == 3)
                {
                    int pyreHP = ResourceReader.GetPyreHealth(cache);
                    int maxPyreHP = ResourceReader.GetMaxPyreHealth(cache);
                    var pyreParts = new List<string>();
                    if (pyreHP >= 0)
                    {
                        pyreParts.Add($"Pyre {pyreHP} of {maxPyreHP} health");
                    }
                    var pyreUnits = GetUnitsInRoom(room);
                    if (pyreUnits.Count > 0)
                    {
                        foreach (var unit in pyreUnits)
                        {
                            string desc = GetUnitBriefDescription(cache, unit, announcedKeywords);
                            bool isEnemy = IsEnemyUnit(cache, unit);
                            pyreParts.Add($"{(isEnemy ? "Enemy " : "")}{desc}");
                        }
                    }
                    return pyreParts.Count > 0 ? string.Join(". ", pyreParts) : "Empty";
                }

                // Check if floor is frozen/disabled (e.g. destroyed by boss action)
                bool isRoomFrozen = false;
                var roomType = room.GetType();
                var isEnabledMethod = roomType.GetMethod("IsRoomEnabled", Type.EmptyTypes);
                if (isEnabledMethod != null)
                {
                    var enabled = isEnabledMethod.Invoke(room, null);
                    if (enabled is bool isEnabled && !isEnabled)
                    {
                        isRoomFrozen = true;
                    }
                }

                // Regular floor - get capacity info from game
                var (usedCapacity, maxCapacity) = GetFloorCapacityInfo(room);
                var units = GetUnitsInRoom(room);

                string capacityInfo = $"{usedCapacity} of {maxCapacity} capacity";

                // Add corruption info (DLC)
                string corruptionInfo = GetFloorCorruption(room);

                // Add floor enchantments (Emberdrain, etc.)
                string enchantmentInfo = GetFloorEnchantments(room);

                if (units.Count == 0)
                {
                    string emptyInfo = isRoomFrozen ? $"Frozen. {capacityInfo}" : $"Empty. {capacityInfo}";
                    if (!string.IsNullOrEmpty(corruptionInfo))
                        emptyInfo += $". {corruptionInfo}";
                    if (!string.IsNullOrEmpty(enchantmentInfo))
                        emptyInfo += $". {enchantmentInfo}";
                    return emptyInfo;
                }

                var friendlyUnits = new List<string>();
                var enemyUnits = new List<string>();

                foreach (var unit in units)
                {
                    // Use GetUnitBriefDescription which includes abilities and intents
                    string description = GetUnitBriefDescription(cache, unit, announcedKeywords);

                    if (IsEnemyUnit(cache, unit))
                    {
                        enemyUnits.Add(description);
                    }
                    else
                    {
                        friendlyUnits.Add(description);
                    }
                }

                var parts = new List<string>();
                if (isRoomFrozen)
                    parts.Add("Frozen");
                parts.Add(capacityInfo);
                if (!string.IsNullOrEmpty(corruptionInfo))
                    parts.Add(corruptionInfo);
                if (!string.IsNullOrEmpty(enchantmentInfo))
                    parts.Add(enchantmentInfo);
                if (enemyUnits.Count > 0)
                {
                    parts.Add($"Enemies: {string.Join(", ", enemyUnits)}");
                }
                if (friendlyUnits.Count > 0)
                {
                    parts.Add($"Your units: {string.Join(", ", friendlyUnits)}");
                }

                return string.Join(". ", parts);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting floor summary: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Get a list of all enemy units on all floors (for unit targeting).
        /// </summary>
        internal static List<string> GetAllEnemies(BattleManagerCache cache)
        {
            var enemies = new List<string>();
            try
            {
                for (int roomIndex = 0; roomIndex <= 2; roomIndex++)
                {
                    var room = GetRoom(cache, roomIndex);
                    if (room == null) continue;

                    var units = GetUnitsInRoom(room);
                    foreach (var unit in units)
                    {
                        if (IsEnemyUnit(cache, unit))
                        {
                            string name = GetUnitName(cache, unit);
                            int hp = GetUnitHP(cache, unit);
                            int maxHp = GetUnitMaxHP(unit);
                            int attack = GetUnitAttack(cache, unit);
                            string hpText = (maxHp > 0 && maxHp != hp) ? $"{hp} of {maxHp}" : $"{hp}";
                            enemies.Add($"{name} {attack} attack, {hpText} health on {RoomIndexToFloorName(roomIndex).ToLower()}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting all enemies: {ex.Message}");
            }
            return enemies;
        }

        /// <summary>
        /// Get a list of all friendly units on all floors (for unit targeting).
        /// </summary>
        internal static List<string> GetAllFriendlyUnits(BattleManagerCache cache)
        {
            var friendlies = new List<string>();
            try
            {
                for (int roomIndex = 0; roomIndex <= 2; roomIndex++)
                {
                    var room = GetRoom(cache, roomIndex);
                    if (room == null) continue;

                    var units = GetUnitsInRoom(room);
                    foreach (var unit in units)
                    {
                        if (!IsEnemyUnit(cache, unit))
                        {
                            string name = GetUnitName(cache, unit);
                            int hp = GetUnitHP(cache, unit);
                            int maxHp = GetUnitMaxHP(unit);
                            int attack = GetUnitAttack(cache, unit);
                            string hpText = (maxHp > 0 && maxHp != hp) ? $"{hp} of {maxHp}" : $"{hp}";
                            friendlies.Add($"{name} {attack} attack, {hpText} health on {RoomIndexToFloorName(roomIndex).ToLower()}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting friendly units: {ex.Message}");
            }
            return friendlies;
        }

        /// <summary>
        /// Get a list of all units (both friendly and enemy) on all floors.
        /// </summary>
        internal static List<string> GetAllUnits(BattleManagerCache cache)
        {
            var allUnits = new List<string>();
            allUnits.AddRange(GetAllFriendlyUnits(cache));
            allUnits.AddRange(GetAllEnemies(cache));
            return allUnits;
        }

        internal static List<object> GetUnitsInRoom(object room)
        {
            var units = new List<object>();
            try
            {
                var roomType = room.GetType();

                // First try AddCharactersToList method - the primary way to get characters from a room
                // This method signature is: AddCharactersToList(List<CharacterState>, Team.Type, bool)
                // We need to call it for BOTH team types to get all units
                var addCharsMethods = roomType.GetMethods().Where(m => m.Name == "AddCharactersToList").ToArray();

                // Find the Team.Type enum at runtime
                Type teamTypeEnum = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    teamTypeEnum = assembly.GetType("Team+Type") ?? assembly.GetType("Team`Type");
                    if (teamTypeEnum != null) break;

                    // Try to find nested type
                    var teamType = assembly.GetType("Team");
                    if (teamType != null)
                    {
                        teamTypeEnum = teamType.GetNestedType("Type");
                        if (teamTypeEnum != null) break;
                    }
                }

                foreach (var addCharsMethod in addCharsMethods)
                {
                    var parameters = addCharsMethod.GetParameters();
                    // Look for the overload with List<T>, Team.Type, bool
                    if (parameters.Length >= 2)
                    {
                        var listType = parameters[0].ParameterType;
                        var secondParamType = parameters[1].ParameterType;

                        // Check if it's a List type and the second param is an enum (Team.Type)
                        if (listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(List<>) && secondParamType.IsEnum)
                        {
                            try
                            {
                                // Get all enum values for Team.Type (Monsters=0, Heroes=1)
                                var enumValues = Enum.GetValues(secondParamType);

                                foreach (var teamValue in enumValues)
                                {
                                    // Create a new instance of the typed list for each call
                                    var charList = Activator.CreateInstance(listType);

                                    // Build the argument array
                                    var args = new object[parameters.Length];
                                    args[0] = charList;
                                    args[1] = teamValue; // Use the actual team type enum value

                                    // Fill remaining params with defaults
                                    for (int i = 2; i < parameters.Length; i++)
                                    {
                                        args[i] = parameters[i].ParameterType.IsValueType
                                            ? Activator.CreateInstance(parameters[i].ParameterType)
                                            : null;
                                    }

                                    // Call the method
                                    addCharsMethod.Invoke(room, args);

                                    // Extract results from the typed list
                                    if (charList is System.Collections.IEnumerable enumerable)
                                    {
                                        foreach (var c in enumerable)
                                        {
                                            if (c != null && !units.Contains(c))
                                            {
                                                units.Add(c);
                                            }
                                        }
                                    }
                                }

                                if (units.Count > 0)
                                {
                                    MonsterTrainAccessibility.LogInfo($"GetUnitsInRoom found {units.Count} units via AddCharactersToList (both teams)");
                                    return units;
                                }
                            }
                            catch (Exception ex)
                            {
                                MonsterTrainAccessibility.LogInfo($"AddCharactersToList with team types failed: {ex.Message}");
                            }
                        }
                        // Also handle the WeakRefList overload if present
                        else if (listType.Name.Contains("WeakRefList") && secondParamType.IsEnum)
                        {
                            // Skip WeakRefList - prefer List<T> overload
                            continue;
                        }
                    }
                    // Fallback for single-param overloads (if any)
                    else if (parameters.Length == 1)
                    {
                        var listType = parameters[0].ParameterType;
                        if (listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(List<>))
                        {
                            try
                            {
                                var charList = Activator.CreateInstance(listType);
                                addCharsMethod.Invoke(room, new object[] { charList });

                                if (charList is System.Collections.IEnumerable enumerable)
                                {
                                    foreach (var c in enumerable)
                                    {
                                        if (c != null) units.Add(c);
                                    }
                                    if (units.Count > 0)
                                    {
                                        MonsterTrainAccessibility.LogInfo($"GetUnitsInRoom found {units.Count} units via AddCharactersToList (single param)");
                                        return units;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                MonsterTrainAccessibility.LogInfo($"AddCharactersToList single-param failed: {ex.Message}");
                            }
                        }
                    }
                }

                // Fallback: try to access the characters field directly
                string[] fieldNames = { "characters", "_characters", "m_characters", "characterList" };
                foreach (var fieldName in fieldNames)
                {
                    var charsField = roomType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (charsField != null)
                    {
                        var chars = charsField.GetValue(room);
                        if (chars != null)
                        {
                            if (chars is System.Collections.IEnumerable enumerable)
                            {
                                foreach (var c in enumerable)
                                {
                                    if (c != null)
                                    {
                                        units.Add(c);
                                    }
                                }
                                if (units.Count > 0)
                                {
                                    MonsterTrainAccessibility.LogInfo($"GetUnitsInRoom found {units.Count} units via field '{fieldName}'");
                                    return units;
                                }
                            }
                        }
                    }
                }

                // Log available methods for debugging if nothing worked
                if (units.Count == 0)
                {
                    var methods = roomType.GetMethods().Where(m => m.Name.Contains("Character") || m.Name.Contains("Unit")).ToList();
                    var methodLog = string.Join(", ", methods.Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})"));
                    MonsterTrainAccessibility.LogInfo($"Room character-related methods: {methodLog}");
                }

                MonsterTrainAccessibility.LogInfo($"GetUnitsInRoom found {units.Count} units (no method worked)");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting units: {ex.Message}");
            }
            return units;
        }

        internal static string GetUnitName(BattleManagerCache cache, object characterState)
        {
            try
            {
                string name = null;

                // Try GetLocName or similar
                var type = characterState.GetType();
                var getNameMethod = type.GetMethod("GetName") ??
                                   type.GetMethod("GetLocName") ??
                                   type.GetMethod("GetTitle");
                if (getNameMethod != null)
                {
                    name = getNameMethod.Invoke(characterState, null) as string;
                }

                // Try getting CharacterData and its name
                if (string.IsNullOrEmpty(name))
                {
                    var getDataMethod = type.GetMethod("GetCharacterData");
                    if (getDataMethod != null)
                    {
                        var data = getDataMethod.Invoke(characterState, null);
                        if (data != null && cache.GetCharacterNameMethod != null)
                        {
                            name = cache.GetCharacterNameMethod.Invoke(data, null) as string;
                        }
                    }
                }

                return TextUtilities.StripRichTextTags(name) ?? "Unit";
            }
            catch { }
            return "Unit";
        }

        internal static int GetUnitHP(BattleManagerCache cache, object characterState)
        {
            try
            {
                if (cache.GetHPMethod == null)
                {
                    var type = characterState.GetType();
                    cache.GetHPMethod = type.GetMethod("GetHP");
                }
                var result = cache.GetHPMethod?.Invoke(characterState, null);
                if (result is int hp) return hp;
            }
            catch { }
            return 0;
        }

        internal static int GetUnitAttack(BattleManagerCache cache, object characterState)
        {
            try
            {
                if (cache.GetAttackDamageMethod == null)
                {
                    var type = characterState.GetType();
                    cache.GetAttackDamageMethod = type.GetMethod("GetAttackDamage");
                }
                var result = cache.GetAttackDamageMethod?.Invoke(characterState, null);
                if (result is int attack) return attack;
            }
            catch { }
            return 0;
        }

        internal static bool IsEnemyUnit(BattleManagerCache cache, object characterState)
        {
            try
            {
                if (cache.GetTeamTypeMethod == null)
                {
                    var type = characterState.GetType();
                    cache.GetTeamTypeMethod = type.GetMethod("GetTeamType");
                }
                var team = cache.GetTeamTypeMethod?.Invoke(characterState, null);
                string teamStr = team?.ToString() ?? "null";
                MonsterTrainAccessibility.LogInfo($"IsEnemyUnit: team = {teamStr}");
                // In Monster Train, "Heroes" are the enemies attacking the train
                return teamStr == "Heroes";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"IsEnemyUnit error: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Get the maximum HP of a unit
        /// </summary>
        internal static int GetUnitMaxHP(object characterState)
        {
            try
            {
                var type = characterState.GetType();
                var method = type.GetMethod("GetMaxHP", Type.EmptyTypes);
                if (method != null)
                {
                    var result = method.Invoke(characterState, null);
                    if (result is int hp) return hp;
                }
            }
            catch { }
            return -1;
        }
    }
}
