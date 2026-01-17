using MonsterTrainAccessibility.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MonsterTrainAccessibility.Utilities
{
    /// <summary>
    /// Utility class to read game state using reflection.
    /// Provides methods to extract data from game objects without compile-time dependencies.
    /// </summary>
    public static class GameStateReader
    {
        #region Manager Access

        /// <summary>
        /// Try to get the SaveManager instance
        /// </summary>
        public static object GetSaveManager()
        {
            try
            {
                var providerManager = GetProviderManager();
                if (providerManager != null)
                {
                    var method = providerManager.GetType().GetMethod("TryGetProvider");
                    if (method != null)
                    {
                        var saveManagerType = AccessTools.TypeByName("SaveManager");
                        if (saveManagerType != null)
                        {
                            var genericMethod = method.MakeGenericMethod(saveManagerType);
                            var parameters = new object[] { null };
                            genericMethod.Invoke(providerManager, parameters);
                            return parameters[0];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to get SaveManager: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get ProviderManager using Trainworks pattern
        /// </summary>
        private static object GetProviderManager()
        {
            try
            {
                var type = AccessTools.TypeByName("Trainworks.Managers.ProviderManager");
                return type;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Card Reading

        /// <summary>
        /// Read card information from a CardState object
        /// </summary>
        public static CardInfo ReadCard(object cardState)
        {
            if (cardState == null) return null;

            try
            {
                var info = new CardInfo();
                var type = cardState.GetType();

                // Get card data
                var getDataMethod = type.GetMethod("GetCardDataRead");
                var cardData = getDataMethod?.Invoke(cardState, null);

                if (cardData != null)
                {
                    var dataType = cardData.GetType();

                    // Name
                    var getNameKey = dataType.GetMethod("GetNameKey");
                    var nameKey = getNameKey?.Invoke(cardData, null) as string;
                    info.Name = Localize(nameKey) ?? nameKey ?? "Unknown Card";

                    // Card type
                    var getCardType = dataType.GetMethod("GetCardType");
                    var cardTypeEnum = getCardType?.Invoke(cardData, null);
                    info.Type = cardTypeEnum?.ToString() ?? "Unknown";

                    // Check if monster
                    info.IsMonster = info.Type == "Monster" || info.Type == "Spell_Monster";
                }

                // Cost
                var getCost = type.GetMethod("GetCost");
                if (getCost != null)
                {
                    info.Cost = (int)getCost.Invoke(cardState, null);
                }

                // Body text
                var getBodyText = type.GetMethod("GetCardBodyText");
                if (getBodyText != null)
                {
                    info.BodyText = getBodyText.Invoke(cardState, null) as string ?? "";
                }

                // Needs target (simplified check)
                info.NeedsTarget = !info.IsMonster && info.Type != "Blight";

                // Store reference
                info.GameCardState = cardState;

                return info;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading card: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Read all cards in the player's hand
        /// </summary>
        public static List<CardInfo> ReadHand(object cardManager)
        {
            var cards = new List<CardInfo>();

            try
            {
                if (cardManager == null) return cards;

                var type = cardManager.GetType();
                var getHand = type.GetMethod("GetHand") ?? type.GetProperty("Hand")?.GetMethod;

                if (getHand != null)
                {
                    var hand = getHand.Invoke(cardManager, null) as System.Collections.IEnumerable;
                    if (hand != null)
                    {
                        foreach (var cardState in hand)
                        {
                            var info = ReadCard(cardState);
                            if (info != null)
                            {
                                cards.Add(info);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading hand: {ex.Message}");
            }

            return cards;
        }

        #endregion

        #region Unit Reading

        /// <summary>
        /// Read unit information from a CharacterState object
        /// </summary>
        public static UnitInfo ReadUnit(object characterState)
        {
            if (characterState == null) return null;

            try
            {
                var info = new UnitInfo();
                var type = characterState.GetType();

                // Get character data
                var getDataMethod = type.GetMethod("GetCharacterDataRead");
                var charData = getDataMethod?.Invoke(characterState, null);

                if (charData != null)
                {
                    var dataType = charData.GetType();

                    // Name
                    var getNameKey = dataType.GetMethod("GetNameKey");
                    var nameKey = getNameKey?.Invoke(charData, null) as string;
                    info.Name = Localize(nameKey) ?? nameKey ?? "Unknown Unit";
                }

                // Health
                var getHP = type.GetMethod("GetHP");
                var getMaxHP = type.GetMethod("GetMaxHP");
                if (getHP != null) info.Health = (int)getHP.Invoke(characterState, null);
                if (getMaxHP != null) info.MaxHealth = (int)getMaxHP.Invoke(characterState, null);

                // Attack
                var getAttack = type.GetMethod("GetAttackDamage");
                if (getAttack != null) info.Attack = (int)getAttack.Invoke(characterState, null);

                // Size
                var getSize = type.GetMethod("GetSize");
                if (getSize != null) info.Size = (int)getSize.Invoke(characterState, null);

                // Team
                var getTeam = type.GetMethod("GetTeamType");
                if (getTeam != null)
                {
                    var team = getTeam.Invoke(characterState, null);
                    info.IsEnemy = team?.ToString() == "Heroes"; // Heroes are enemies in MT
                }

                // Intent (for enemies)
                if (info.IsEnemy)
                {
                    info.Intent = GetEnemyIntent(characterState);
                }

                // Status effects
                info.StatusEffects = GetStatusEffectsSummary(characterState);

                info.GameCharacterState = characterState;
                return info;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading unit: {ex.Message}");
                return null;
            }
        }

        private static string GetEnemyIntent(object characterState)
        {
            try
            {
                var type = characterState.GetType();
                var canAttack = type.GetMethod("CanAttack");

                if (canAttack != null && (bool)canAttack.Invoke(characterState, null))
                {
                    var getAttack = type.GetMethod("GetAttackDamage");
                    if (getAttack != null)
                    {
                        int damage = (int)getAttack.Invoke(characterState, null);
                        return $"will attack for {damage}";
                    }
                }
            }
            catch { }

            return "waiting";
        }

        private static string GetStatusEffectsSummary(object characterState)
        {
            try
            {
                var type = characterState.GetType();
                var getStatuses = type.GetMethod("GetStatusEffects");

                if (getStatuses != null)
                {
                    var statuses = getStatuses.Invoke(characterState, null) as System.Collections.IEnumerable;
                    if (statuses != null)
                    {
                        var effects = new List<string>();

                        foreach (var status in statuses)
                        {
                            var statusType = status.GetType();
                            var getId = statusType.GetMethod("GetStatusId");
                            var getStacks = statusType.GetMethod("GetStacks");

                            if (getId != null && getStacks != null)
                            {
                                string id = getId.Invoke(status, null) as string;
                                int stacks = (int)getStacks.Invoke(status, null);

                                if (!string.IsNullOrEmpty(id) && stacks > 0)
                                {
                                    string name = Localize(id) ?? id;
                                    effects.Add($"{name} {stacks}");
                                }
                            }
                        }

                        if (effects.Count > 0)
                        {
                            return string.Join(", ", effects);
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        #endregion

        #region Floor Reading

        /// <summary>
        /// Read floor information from a RoomState object
        /// </summary>
        public static FloorInfo ReadFloor(object roomState, int floorIndex)
        {
            if (roomState == null) return new FloorInfo { FloorNumber = floorIndex + 1 };

            try
            {
                var info = new FloorInfo { FloorNumber = floorIndex + 1 };
                var type = roomState.GetType();

                // Capacity
                var getCapacity = type.GetMethod("GetCapacity");
                if (getCapacity != null) info.MaxCapacity = (int)getCapacity.Invoke(roomState, null);

                // Get characters in room
                var getChars = type.GetMethod("GetCharactersInRoom");
                if (getChars != null)
                {
                    // Get friendly units (Team.Type.Monsters)
                    var friendlyUnits = GetUnitsInRoom(roomState, "Monsters");
                    info.FriendlyUnits = friendlyUnits;
                    info.UsedCapacity = friendlyUnits.Sum(u => u.Size);

                    // Get enemy units (Team.Type.Heroes)
                    info.EnemyUnits = GetUnitsInRoom(roomState, "Heroes");
                }

                info.GameRoomState = roomState;
                return info;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading floor: {ex.Message}");
                return new FloorInfo { FloorNumber = floorIndex + 1 };
            }
        }

        private static List<UnitInfo> GetUnitsInRoom(object roomState, string teamTypeName)
        {
            var units = new List<UnitInfo>();

            try
            {
                var type = roomState.GetType();
                var getChars = type.GetMethod("GetCharactersInRoom");

                if (getChars != null)
                {
                    // Find the Team.Type enum value
                    var teamType = AccessTools.TypeByName("Team+Type");
                    if (teamType != null)
                    {
                        var teamValue = Enum.Parse(teamType, teamTypeName);
                        var characters = getChars.Invoke(roomState, new[] { teamValue }) as System.Collections.IEnumerable;

                        if (characters != null)
                        {
                            foreach (var character in characters)
                            {
                                var unitInfo = ReadUnit(character);
                                if (unitInfo != null)
                                {
                                    units.Add(unitInfo);
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return units;
        }

        #endregion

        #region Localization

        /// <summary>
        /// Localize a string key
        /// </summary>
        public static string Localize(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            try
            {
                // Try using I2 Localization
                var locManager = AccessTools.TypeByName("I2.Loc.LocalizationManager");
                if (locManager != null)
                {
                    var getTrans = locManager.GetMethod("GetTranslation",
                        new[] { typeof(string) });
                    if (getTrans != null)
                    {
                        return getTrans.Invoke(null, new object[] { key }) as string;
                    }
                }

                // Fallback: try extension method pattern
                var extensionType = AccessTools.TypeByName("LocalizationExtensions");
                if (extensionType != null)
                {
                    var localizeMethod = extensionType.GetMethod("Localize",
                        BindingFlags.Public | BindingFlags.Static);
                    if (localizeMethod != null)
                    {
                        return localizeMethod.Invoke(null, new object[] { key }) as string;
                    }
                }
            }
            catch { }

            // Return key without "Key_" prefix as fallback
            if (key.StartsWith("Key_"))
            {
                return key.Substring(4).Replace("_", " ");
            }

            return key;
        }

        #endregion

        #region Reflection Helpers

        /// <summary>
        /// Get type by name from any loaded assembly
        /// </summary>
        public static Type AccessTypes(string typeName)
        {
            return AccessTools.TypeByName(typeName);
        }

        #endregion
    }

    /// <summary>
    /// Extension to HarmonyLib's AccessTools
    /// </summary>
    internal static class AccessTools
    {
        public static Type TypeByName(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(name);
                    if (type != null) return type;

                    // Try with different namespace patterns
                    type = assembly.GetType($"ShinyShoe.{name}");
                    if (type != null) return type;
                }
                catch { }
            }
            return null;
        }

        public static MethodInfo Method(Type type, string name)
        {
            if (type == null) return null;
            return type.GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static);
        }
    }
}
