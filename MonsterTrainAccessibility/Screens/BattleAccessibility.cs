using MonsterTrainAccessibility.Core;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Handles accessibility for the battle/combat screen.
    /// Reads actual game state from the game's managers.
    /// </summary>
    public class BattleAccessibility
    {
        public bool IsInBattle { get; private set; }

        // Cached manager references (found at runtime)
        private object _cardManager;
        private object _saveManager;
        private object _roomManager;
        private object _playerManager;
        private object _combatManager;

        // Cached reflection info
        private System.Reflection.MethodInfo _getHandMethod;
        private System.Reflection.MethodInfo _getTitleMethod;
        private System.Reflection.MethodInfo _getCostMethod;
        private System.Reflection.MethodInfo _getTowerHPMethod;
        private System.Reflection.MethodInfo _getMaxTowerHPMethod;
        private System.Reflection.MethodInfo _getEnergyMethod;
        private System.Reflection.MethodInfo _getRoomMethod;
        private System.Reflection.MethodInfo _addCharactersMethod;
        private System.Reflection.MethodInfo _getHPMethod;
        private System.Reflection.MethodInfo _getAttackDamageMethod;
        private System.Reflection.MethodInfo _getTeamTypeMethod;
        private System.Reflection.MethodInfo _getCharacterNameMethod;

        public BattleAccessibility()
        {
        }

        #region Manager Discovery

        /// <summary>
        /// Find and cache references to game managers
        /// </summary>
        private void FindManagers()
        {
            try
            {
                // Find managers using FindObjectOfType
                _cardManager = FindManager("CardManager");
                _saveManager = FindManager("SaveManager");
                _roomManager = FindManager("RoomManager");
                _playerManager = FindManager("PlayerManager");
                _combatManager = FindManager("CombatManager");

                // Cache method info for performance
                CacheMethodInfo();

                MonsterTrainAccessibility.LogInfo($"Found managers - CardManager: {_cardManager != null}, SaveManager: {_saveManager != null}");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error finding managers: {ex.Message}");
            }
        }

        private object FindManager(string typeName)
        {
            try
            {
                // Find the type in the game assembly
                var type = Type.GetType(typeName + ", Assembly-CSharp");
                if (type == null)
                {
                    // Try without assembly qualifier
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = assembly.GetType(typeName);
                        if (type != null) break;
                    }
                }

                if (type != null)
                {
                    // FindObjectOfType is a generic method, use reflection
                    var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new Type[0]);
                    var genericMethod = findMethod.MakeGenericMethod(type);
                    return genericMethod.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error finding {typeName}: {ex.Message}");
            }
            return null;
        }

        private void CacheMethodInfo()
        {
            try
            {
                if (_cardManager != null)
                {
                    var cardManagerType = _cardManager.GetType();
                    _getHandMethod = cardManagerType.GetMethod("GetHand");
                }

                if (_saveManager != null)
                {
                    var saveManagerType = _saveManager.GetType();
                    _getTowerHPMethod = saveManagerType.GetMethod("GetTowerHP");
                    _getMaxTowerHPMethod = saveManagerType.GetMethod("GetMaxTowerHP");
                }

                if (_playerManager != null)
                {
                    var playerManagerType = _playerManager.GetType();
                    _getEnergyMethod = playerManagerType.GetMethod("GetEnergy");
                }

                if (_roomManager != null)
                {
                    var roomManagerType = _roomManager.GetType();
                    _getRoomMethod = roomManagerType.GetMethod("GetRoom");
                }

                // Cache CardState methods
                var cardStateType = Type.GetType("CardState, Assembly-CSharp");
                if (cardStateType != null)
                {
                    _getTitleMethod = cardStateType.GetMethod("GetTitle");
                    _getCostMethod = cardStateType.GetMethod("GetCostWithoutAnyModifications");
                }

                // Cache CharacterState methods
                var characterStateType = Type.GetType("CharacterState, Assembly-CSharp");
                if (characterStateType != null)
                {
                    _getHPMethod = characterStateType.GetMethod("GetHP");
                    _getAttackDamageMethod = characterStateType.GetMethod("GetAttackDamage");
                    _getTeamTypeMethod = characterStateType.GetMethod("GetTeamType");
                }

                // Cache CharacterData methods for getting name
                var characterDataType = Type.GetType("CharacterData, Assembly-CSharp");
                if (characterDataType != null)
                {
                    _getCharacterNameMethod = characterDataType.GetMethod("GetName");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error caching methods: {ex.Message}");
            }
        }

        #endregion

        #region Battle Lifecycle

        /// <summary>
        /// Called when combat begins
        /// </summary>
        public void OnBattleEntered()
        {
            IsInBattle = true;
            FindManagers();

            MonsterTrainAccessibility.LogInfo("Battle entered");
            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Battle started");

            // Announce initial state
            AnnounceResources();
        }

        /// <summary>
        /// Called when combat ends
        /// </summary>
        public void OnBattleExited()
        {
            IsInBattle = false;
            MonsterTrainAccessibility.LogInfo("Battle exited");
        }

        /// <summary>
        /// Called at the start of player's turn
        /// </summary>
        public void OnTurnStarted(int ember, int maxEmber, int cardsDrawn)
        {
            var output = MonsterTrainAccessibility.ScreenReader;
            output?.Speak("Your turn", true);

            // Read actual ember from game
            int actualEmber = GetCurrentEnergy();
            if (actualEmber >= 0)
            {
                output?.Queue($"{actualEmber} ember");
            }

            if (cardsDrawn > 0)
            {
                output?.Queue($"Drew {cardsDrawn} cards");
            }
        }

        /// <summary>
        /// Called when player ends their turn
        /// </summary>
        public void OnTurnEnded()
        {
            MonsterTrainAccessibility.ScreenReader?.Speak("End turn. Combat phase.", true);
        }

        /// <summary>
        /// Called when battle is won
        /// </summary>
        public void OnBattleWon()
        {
            IsInBattle = false;
            MonsterTrainAccessibility.ScreenReader?.Speak("Victory! Battle won.", true);
        }

        /// <summary>
        /// Called when pyre is destroyed
        /// </summary>
        public void OnBattleLost()
        {
            IsInBattle = false;
            MonsterTrainAccessibility.ScreenReader?.Speak("Defeat. The pyre has been destroyed.", true);
        }

        #endregion

        #region Hand Reading

        /// <summary>
        /// Announce all cards in hand
        /// </summary>
        public void AnnounceHand()
        {
            try
            {
                var hand = GetHandCards();
                if (hand == null || hand.Count == 0)
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak("Hand is empty", true);
                    return;
                }

                var sb = new StringBuilder();
                sb.Append($"Hand contains {hand.Count} cards. ");

                int currentEnergy = GetCurrentEnergy();

                for (int i = 0; i < hand.Count; i++)
                {
                    var card = hand[i];
                    string name = GetCardTitle(card);
                    int cost = GetCardCost(card);

                    string playable = (currentEnergy >= 0 && cost > currentEnergy) ? " (unplayable)" : "";
                    sb.Append($"{i + 1}: {name}, {cost} ember{playable}. ");
                }

                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), true);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error announcing hand: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read hand", true);
            }
        }

        private List<object> GetHandCards()
        {
            if (_cardManager == null || _getHandMethod == null)
            {
                FindManagers();
                if (_cardManager == null) return null;
            }

            try
            {
                var result = _getHandMethod.Invoke(_cardManager, new object[] { false });
                if (result is System.Collections.IList list)
                {
                    var cards = new List<object>();
                    foreach (var card in list)
                    {
                        cards.Add(card);
                    }
                    return cards;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting hand: {ex.Message}");
            }
            return null;
        }

        private string GetCardTitle(object cardState)
        {
            try
            {
                if (_getTitleMethod == null)
                {
                    var type = cardState.GetType();
                    _getTitleMethod = type.GetMethod("GetTitle");
                }
                return _getTitleMethod?.Invoke(cardState, null) as string ?? "Unknown Card";
            }
            catch
            {
                return "Unknown Card";
            }
        }

        private int GetCardCost(object cardState)
        {
            try
            {
                if (_getCostMethod == null)
                {
                    var type = cardState.GetType();
                    _getCostMethod = type.GetMethod("GetCostWithoutAnyModifications");
                }
                var result = _getCostMethod?.Invoke(cardState, null);
                if (result is int cost) return cost;
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Announce cards drawn (with card names)
        /// </summary>
        public void OnCardsDrawn(List<string> cardNames)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceCardDraws.Value)
                return;

            if (cardNames.Count == 1)
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"Drew {cardNames[0]}");
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"Drew: {string.Join(", ", cardNames)}");
            }
        }

        /// <summary>
        /// Announce cards drawn (count only, used when card names aren't available)
        /// </summary>
        public void OnCardsDrawn(int count)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceCardDraws.Value)
                return;

            if (count == 1)
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Drew 1 card");
            }
            else if (count > 1)
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"Drew {count} cards");
            }
        }

        /// <summary>
        /// Called when a card is played by index
        /// </summary>
        public void OnCardPlayed(int cardIndex)
        {
            // The card was played successfully
            MonsterTrainAccessibility.ScreenReader?.Queue("Card played");
        }

        /// <summary>
        /// Called when a card is discarded
        /// </summary>
        public void OnCardDiscarded(string cardName)
        {
            if (!string.IsNullOrEmpty(cardName) && cardName != "Card")
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"Discarded {cardName}");
            }
        }

        public void SelectCardByIndex(int index)
        {
            // This would need to interact with the game's card selection
            // For now, just announce the card at that index
            var hand = GetHandCards();
            if (hand != null && index >= 0 && index < hand.Count)
            {
                string name = GetCardTitle(hand[index]);
                int cost = GetCardCost(hand[index]);
                MonsterTrainAccessibility.ScreenReader?.Speak($"Card {index + 1}: {name}, {cost} ember", true);
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Speak($"No card at position {index + 1}", true);
            }
        }

        public void RefreshHand()
        {
            // Called when hand changes - could trigger re-announcement if desired
        }

        #endregion

        #region Floor Reading

        /// <summary>
        /// Announce all floors
        /// </summary>
        public void AnnounceAllFloors()
        {
            try
            {
                var output = MonsterTrainAccessibility.ScreenReader;
                output?.Speak("Tower status:", true);

                // Monster Train has 4 rooms (3 floors + pyre room)
                for (int i = 3; i >= 0; i--)
                {
                    var room = GetRoom(i);
                    if (room != null)
                    {
                        string floorName = i == 0 ? "Pyre" : $"Floor {i}";
                        var units = GetUnitsInRoom(room);

                        if (units.Count == 0)
                        {
                            output?.Queue($"{floorName}: Empty");
                        }
                        else
                        {
                            var descriptions = new List<string>();
                            foreach (var unit in units)
                            {
                                string name = GetUnitName(unit);
                                int hp = GetUnitHP(unit);
                                int attack = GetUnitAttack(unit);
                                bool isEnemy = IsEnemyUnit(unit);
                                string prefix = isEnemy ? "Enemy" : "";
                                descriptions.Add($"{prefix} {name} {attack}/{hp}");
                            }
                            output?.Queue($"{floorName}: {string.Join(", ", descriptions)}");
                        }
                    }
                }

                // Announce pyre health
                int pyreHP = GetPyreHealth();
                int maxPyreHP = GetMaxPyreHealth();
                if (pyreHP >= 0)
                {
                    output?.Queue($"Pyre: {pyreHP} of {maxPyreHP} health");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error announcing floors: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read floors", true);
            }
        }

        private object GetRoom(int roomIndex)
        {
            if (_roomManager == null || _getRoomMethod == null)
            {
                FindManagers();
                if (_roomManager == null) return null;
            }

            try
            {
                return _getRoomMethod?.Invoke(_roomManager, new object[] { roomIndex });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get a text summary of what's on a specific floor (for floor targeting).
        /// Floor numbers are 1-3 where 1 is bottom (pyre room is floor 0 internally).
        /// </summary>
        public string GetFloorSummary(int floorNumber)
        {
            try
            {
                // Convert user-facing floor number (1-3) to internal room index
                // In Monster Train: room 0 is pyre, rooms 1-3 are floors 1-3
                int roomIndex = floorNumber;

                var room = GetRoom(roomIndex);
                if (room == null)
                {
                    return $"Floor {floorNumber}: Unknown";
                }

                var units = GetUnitsInRoom(room);
                if (units.Count == 0)
                {
                    return "Empty";
                }

                var friendlyUnits = new List<string>();
                var enemyUnits = new List<string>();

                foreach (var unit in units)
                {
                    string name = GetUnitName(unit);
                    int hp = GetUnitHP(unit);
                    int attack = GetUnitAttack(unit);
                    string description = $"{name} {attack}/{hp}";

                    if (IsEnemyUnit(unit))
                    {
                        enemyUnits.Add(description);
                    }
                    else
                    {
                        friendlyUnits.Add(description);
                    }
                }

                var parts = new List<string>();
                if (friendlyUnits.Count > 0)
                {
                    parts.Add($"Your units: {string.Join(", ", friendlyUnits)}");
                }
                if (enemyUnits.Count > 0)
                {
                    parts.Add($"Enemies: {string.Join(", ", enemyUnits)}");
                }

                return string.Join(". ", parts);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting floor summary: {ex.Message}");
                return "";
            }
        }

        private List<object> GetUnitsInRoom(object room)
        {
            var units = new List<object>();
            try
            {
                if (_addCharactersMethod == null)
                {
                    var roomType = room.GetType();
                    _addCharactersMethod = roomType.GetMethod("AddCharactersToList");
                }

                // Get all units (both teams)
                // Team.Type is an enum, we need to figure out the values
                // Typically: Monsters = 0, Heroes = 1 (or similar)
                var teamType = Type.GetType("Team+Type, Assembly-CSharp");
                if (teamType != null && _addCharactersMethod != null)
                {
                    // Try to get both teams
                    var allTeams = 3; // Typically Monsters | Heroes = 3
                    _addCharactersMethod.Invoke(room, new object[] { units, allTeams, false });
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting units: {ex.Message}");
            }
            return units;
        }

        private string GetUnitName(object characterState)
        {
            try
            {
                // Try GetLocName or similar
                var type = characterState.GetType();
                var getNameMethod = type.GetMethod("GetName") ??
                                   type.GetMethod("GetLocName") ??
                                   type.GetMethod("GetTitle");
                if (getNameMethod != null)
                {
                    return getNameMethod.Invoke(characterState, null) as string ?? "Unit";
                }

                // Try getting CharacterData and its name
                var getDataMethod = type.GetMethod("GetCharacterData");
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(characterState, null);
                    if (data != null && _getCharacterNameMethod != null)
                    {
                        return _getCharacterNameMethod.Invoke(data, null) as string ?? "Unit";
                    }
                }
            }
            catch { }
            return "Unit";
        }

        private int GetUnitHP(object characterState)
        {
            try
            {
                if (_getHPMethod == null)
                {
                    var type = characterState.GetType();
                    _getHPMethod = type.GetMethod("GetHP");
                }
                var result = _getHPMethod?.Invoke(characterState, null);
                if (result is int hp) return hp;
            }
            catch { }
            return 0;
        }

        private int GetUnitAttack(object characterState)
        {
            try
            {
                if (_getAttackDamageMethod == null)
                {
                    var type = characterState.GetType();
                    _getAttackDamageMethod = type.GetMethod("GetAttackDamage");
                }
                var result = _getAttackDamageMethod?.Invoke(characterState, null);
                if (result is int attack) return attack;
            }
            catch { }
            return 0;
        }

        private bool IsEnemyUnit(object characterState)
        {
            try
            {
                if (_getTeamTypeMethod == null)
                {
                    var type = characterState.GetType();
                    _getTeamTypeMethod = type.GetMethod("GetTeamType");
                }
                var team = _getTeamTypeMethod?.Invoke(characterState, null);
                // In Monster Train, "Heroes" are the enemies attacking the train
                return team?.ToString() == "Heroes";
            }
            catch { }
            return false;
        }

        #endregion

        #region Resource Reading

        /// <summary>
        /// Announce current resources
        /// </summary>
        public void AnnounceResources()
        {
            try
            {
                var sb = new StringBuilder();

                int energy = GetCurrentEnergy();
                if (energy >= 0)
                {
                    sb.Append($"Ember: {energy}. ");
                }

                int pyreHP = GetPyreHealth();
                int maxPyreHP = GetMaxPyreHealth();
                if (pyreHP >= 0)
                {
                    sb.Append($"Pyre: {pyreHP} of {maxPyreHP}. ");
                }

                var hand = GetHandCards();
                if (hand != null)
                {
                    sb.Append($"Cards in hand: {hand.Count}.");
                }

                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), true);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error announcing resources: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read resources", true);
            }
        }

        private int GetCurrentEnergy()
        {
            if (_playerManager == null || _getEnergyMethod == null)
            {
                FindManagers();
            }

            try
            {
                var result = _getEnergyMethod?.Invoke(_playerManager, null);
                if (result is int energy) return energy;
            }
            catch { }
            return -1;
        }

        private int GetPyreHealth()
        {
            if (_saveManager == null || _getTowerHPMethod == null)
            {
                FindManagers();
            }

            try
            {
                var result = _getTowerHPMethod?.Invoke(_saveManager, null);
                if (result is int hp) return hp;
            }
            catch { }
            return -1;
        }

        private int GetMaxPyreHealth()
        {
            try
            {
                var result = _getMaxTowerHPMethod?.Invoke(_saveManager, null);
                if (result is int hp) return hp;
            }
            catch { }
            return -1;
        }

        #endregion

        #region Enemy Reading

        /// <summary>
        /// Announce enemies and their intents
        /// </summary>
        public void AnnounceEnemies()
        {
            try
            {
                var output = MonsterTrainAccessibility.ScreenReader;
                output?.Speak("Enemy summary:", true);

                bool hasEnemies = false;

                for (int i = 3; i >= 0; i--)
                {
                    var room = GetRoom(i);
                    if (room == null) continue;

                    var units = GetUnitsInRoom(room);
                    var enemies = new List<string>();

                    foreach (var unit in units)
                    {
                        if (IsEnemyUnit(unit))
                        {
                            hasEnemies = true;
                            string name = GetUnitName(unit);
                            int hp = GetUnitHP(unit);
                            int attack = GetUnitAttack(unit);
                            enemies.Add($"{name} {attack}/{hp}");
                        }
                    }

                    if (enemies.Count > 0)
                    {
                        string floorName = i == 0 ? "Pyre room" : $"Floor {i}";
                        output?.Queue($"{floorName}: {string.Join(", ", enemies)}");
                    }
                }

                if (!hasEnemies)
                {
                    output?.Queue("No enemies on the tower");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error announcing enemies: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read enemies", true);
            }
        }

        #endregion

        #region Combat Events

        /// <summary>
        /// Announce damage dealt
        /// </summary>
        public void OnDamageDealt(string sourceName, string targetName, int damage)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceDamage.Value)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"{sourceName} deals {damage} to {targetName}");
        }

        /// <summary>
        /// Announce unit death
        /// </summary>
        public void OnUnitDied(string unitName, bool isEnemy)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceDeaths.Value)
                return;

            string prefix = isEnemy ? "Enemy" : "Your";
            MonsterTrainAccessibility.ScreenReader?.Queue($"{prefix} {unitName} died");
        }

        /// <summary>
        /// Announce status effect applied
        /// </summary>
        public void OnStatusEffectApplied(string unitName, string effectName, int stacks)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceStatusEffects.Value)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName} gains {effectName} {stacks}");
        }

        /// <summary>
        /// Announce unit spawned (entering the battlefield)
        /// </summary>
        public void OnUnitSpawned(string unitName, bool isEnemy, int floorIndex)
        {
            if (!IsInBattle)
                return;

            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceSpawns.Value)
                return;

            // Determine floor name
            string floorName = floorIndex == 0 ? "pyre room" : $"floor {floorIndex}";

            if (isEnemy)
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"Enemy {unitName} enters on {floorName}");
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName} summoned on {floorName}");
            }
        }

        /// <summary>
        /// Announce enemies ascending floors
        /// </summary>
        public void OnEnemiesAscended()
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue("Enemies ascend");
        }

        /// <summary>
        /// Announce pyre damage
        /// </summary>
        public void OnPyreDamaged(int damage, int remainingHP)
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"Pyre takes {damage} damage! {remainingHP} health remaining");
        }

        #endregion
    }
}
