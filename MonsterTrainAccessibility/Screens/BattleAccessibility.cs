using MonsterTrainAccessibility.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
                    // GetHand has overloads, try to find the one without parameters or with bool
                    _getHandMethod = cardManagerType.GetMethod("GetHand", new Type[] { typeof(bool) })
                                  ?? cardManagerType.GetMethod("GetHand", Type.EmptyTypes);
                }

                if (_saveManager != null)
                {
                    var saveManagerType = _saveManager.GetType();
                    _getTowerHPMethod = saveManagerType.GetMethod("GetTowerHP", Type.EmptyTypes);
                    _getMaxTowerHPMethod = saveManagerType.GetMethod("GetMaxTowerHP", Type.EmptyTypes);
                }

                if (_playerManager != null)
                {
                    var playerManagerType = _playerManager.GetType();
                    _getEnergyMethod = playerManagerType.GetMethod("GetEnergy", Type.EmptyTypes);
                }

                if (_roomManager != null)
                {
                    var roomManagerType = _roomManager.GetType();
                    // GetRoom takes an int parameter (room index)
                    _getRoomMethod = roomManagerType.GetMethod("GetRoom", new Type[] { typeof(int) });
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error caching manager methods: {ex.Message}");
            }

            // Cache game type methods separately to isolate errors
            try
            {
                // Cache CardState methods
                var cardStateType = Type.GetType("CardState, Assembly-CSharp");
                if (cardStateType != null)
                {
                    _getTitleMethod = cardStateType.GetMethod("GetTitle", Type.EmptyTypes);
                    _getCostMethod = cardStateType.GetMethod("GetCostWithoutAnyModifications", Type.EmptyTypes)
                                  ?? cardStateType.GetMethod("GetCost", Type.EmptyTypes);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error caching CardState methods: {ex.Message}");
            }

            try
            {
                // Cache CharacterState methods
                var characterStateType = Type.GetType("CharacterState, Assembly-CSharp");
                if (characterStateType != null)
                {
                    _getHPMethod = characterStateType.GetMethod("GetHP", Type.EmptyTypes);
                    _getAttackDamageMethod = characterStateType.GetMethod("GetAttackDamage", Type.EmptyTypes);
                    _getTeamTypeMethod = characterStateType.GetMethod("GetTeamType", Type.EmptyTypes);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error caching CharacterState methods: {ex.Message}");
            }

            try
            {
                // Cache CharacterData methods for getting name
                var characterDataType = Type.GetType("CharacterData, Assembly-CSharp");
                if (characterDataType != null)
                {
                    _getCharacterNameMethod = characterDataType.GetMethod("GetName", Type.EmptyTypes);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error caching CharacterData methods: {ex.Message}");
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
        /// End the player's turn via UI button click or method call
        /// </summary>
        public void EndTurn()
        {
            if (!IsInBattle)
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Not in battle");
                return;
            }

            try
            {
                // Try to find and click the End Turn button in the UI
                // Look for BattleHud first, then find the end turn button within it
                var battleHudType = Type.GetType("BattleHud, Assembly-CSharp");
                if (battleHudType == null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        battleHudType = assembly.GetType("BattleHud");
                        if (battleHudType != null) break;
                    }
                }

                if (battleHudType != null)
                {
                    var battleHud = GameObject.FindObjectOfType(battleHudType);
                    if (battleHud != null)
                    {
                        // Try to find EndTurn method or button
                        var endTurnMethod = battleHudType.GetMethod("EndTurn", Type.EmptyTypes)
                            ?? battleHudType.GetMethod("OnEndTurnPressed", Type.EmptyTypes)
                            ?? battleHudType.GetMethod("OnEndTurnClicked", Type.EmptyTypes);

                        if (endTurnMethod != null)
                        {
                            endTurnMethod.Invoke(battleHud, null);
                            MonsterTrainAccessibility.LogInfo("Ended turn via BattleHud method");
                            return;
                        }

                        // Try to find the button component and click it
                        var go = (battleHud as Component)?.gameObject;
                        if (go != null)
                        {
                            // Search for a button named EndTurn or similar
                            var buttons = go.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                            foreach (var button in buttons)
                            {
                                var name = button.gameObject.name.ToLower();
                                if (name.Contains("endturn") || name.Contains("end turn") || name.Contains("pass"))
                                {
                                    button.onClick?.Invoke();
                                    MonsterTrainAccessibility.LogInfo($"Clicked end turn button: {button.gameObject.name}");
                                    return;
                                }
                            }
                        }
                    }
                }

                // Fallback: try CombatManager.EndPlayerTurn
                if (_combatManager != null)
                {
                    var combatType = _combatManager.GetType();
                    var endTurnMethod = combatType.GetMethod("EndPlayerTurn", Type.EmptyTypes)
                        ?? combatType.GetMethod("PlayerEndTurn", Type.EmptyTypes)
                        ?? combatType.GetMethod("EndTurn", Type.EmptyTypes);

                    if (endTurnMethod != null)
                    {
                        endTurnMethod.Invoke(_combatManager, null);
                        MonsterTrainAccessibility.LogInfo("Ended turn via CombatManager");
                        return;
                    }
                    else
                    {
                        // Log available methods for debugging
                        var methods = combatType.GetMethods()
                            .Where(m => m.Name.ToLower().Contains("turn") || m.Name.ToLower().Contains("end"))
                            .Select(m => m.Name)
                            .Distinct()
                            .ToArray();
                        MonsterTrainAccessibility.LogInfo($"CombatManager turn-related methods: {string.Join(", ", methods)}");
                    }
                }

                MonsterTrainAccessibility.ScreenReader?.Queue("Could not find end turn button");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error ending turn: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Queue("Error ending turn");
            }
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
                var verbosity = MonsterTrainAccessibility.AccessibilitySettings.VerbosityLevel.Value;

                for (int i = 0; i < hand.Count; i++)
                {
                    var card = hand[i];
                    string name = GetCardTitle(card);
                    int cost = GetCardCost(card);
                    string cardType = GetCardType(card);
                    string clanName = GetCardClan(card);
                    string description = GetCardDescription(card);

                    string playable = (currentEnergy >= 0 && cost > currentEnergy) ? ", unplayable" : "";

                    // Build card announcement based on verbosity
                    if (verbosity == Core.VerbosityLevel.Minimal)
                    {
                        sb.Append($"{i + 1}: {name}, {cost} ember{playable}. ");
                    }
                    else
                    {
                        // Normal and Verbose include type, clan, and description
                        string typeStr = !string.IsNullOrEmpty(cardType) ? $" ({cardType})" : "";
                        string clanStr = !string.IsNullOrEmpty(clanName) ? $", {clanName}" : "";
                        sb.Append($"{i + 1}: {name}{typeStr}{clanStr}, {cost} ember{playable}. ");

                        if (!string.IsNullOrEmpty(description))
                        {
                            sb.Append($"{description} ");
                        }

                        // At Verbose level, include keyword tooltips
                        if (verbosity == Core.VerbosityLevel.Verbose)
                        {
                            string keywords = GetCardKeywords(card, description);
                            if (!string.IsNullOrEmpty(keywords))
                            {
                                sb.Append($"Keywords: {keywords} ");
                            }
                        }
                    }
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
                var title = _getTitleMethod?.Invoke(cardState, null) as string ?? "Unknown Card";
                return StripRichTextTags(title);
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

        private string GetCardDescription(object cardState)
        {
            try
            {
                var type = cardState.GetType();
                string result = null;

                // Try GetDescription first
                var getDescMethod = type.GetMethod("GetDescription");
                if (getDescMethod != null)
                {
                    var desc = getDescMethod.Invoke(cardState, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                        result = desc;
                }

                // Try GetEffectDescription
                if (result == null)
                {
                    var getEffectDescMethod = type.GetMethod("GetEffectDescription");
                    if (getEffectDescMethod != null)
                    {
                        var desc = getEffectDescMethod.Invoke(cardState, null) as string;
                        if (!string.IsNullOrEmpty(desc))
                            result = desc;
                    }
                }

                // Try getting from CardData
                if (result == null)
                {
                    var getCardDataMethod = type.GetMethod("GetCardDataID") ?? type.GetMethod("GetCardData");
                    if (getCardDataMethod != null)
                    {
                        var cardData = getCardDataMethod.Invoke(cardState, null);
                        if (cardData != null)
                        {
                            var dataType = cardData.GetType();
                            var dataDescMethod = dataType.GetMethod("GetDescription");
                            if (dataDescMethod != null)
                            {
                                var desc = dataDescMethod.Invoke(cardData, null) as string;
                                if (!string.IsNullOrEmpty(desc))
                                    result = desc;
                            }
                        }
                    }
                }

                // Strip rich text tags before returning
                return StripRichTextTags(result);
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Strip rich text tags from text for screen reader output.
        /// Removes Unity rich text tags like <nobr>, <color>, <upgradeHighlight>, etc.
        /// </summary>
        public static string StripRichTextTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Use regex to strip all XML-like tags
            // This handles: <tag>, </tag>, <tag attribute="value">, self-closing <tag/>, etc.
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "");

            // Also clean up any double spaces that might result
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

            return text.Trim();
        }

        private string GetCardType(object cardState)
        {
            try
            {
                var type = cardState.GetType();
                var getCardTypeMethod = type.GetMethod("GetCardType");
                if (getCardTypeMethod != null)
                {
                    var cardType = getCardTypeMethod.Invoke(cardState, null);
                    if (cardType != null)
                    {
                        string typeName = cardType.ToString();
                        // Convert enum value to readable name
                        if (typeName == "Monster") return "Unit";
                        if (typeName == "Spell") return "Spell";
                        if (typeName == "Blight") return "Blight";
                        return typeName;
                    }
                }
            }
            catch { }
            return null;
        }

        private string GetCardClan(object cardState)
        {
            try
            {
                var type = cardState.GetType();

                // Get CardData from CardState
                var getCardDataMethod = type.GetMethod("GetCardDataRead", Type.EmptyTypes)
                                     ?? type.GetMethod("GetCardData", Type.EmptyTypes);
                if (getCardDataMethod == null) return null;

                var cardData = getCardDataMethod.Invoke(cardState, null);
                if (cardData == null) return null;

                var cardDataType = cardData.GetType();

                // Get linkedClass field from CardData
                var linkedClassField = cardDataType.GetField("linkedClass",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (linkedClassField == null) return null;

                var linkedClass = linkedClassField.GetValue(cardData);
                if (linkedClass == null) return null;

                var classType = linkedClass.GetType();

                // Try GetTitle() for localized name
                var getTitleMethod = classType.GetMethod("GetTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    var clanName = getTitleMethod.Invoke(linkedClass, null) as string;
                    if (!string.IsNullOrEmpty(clanName)) return clanName;
                }

                // Fallback to GetName()
                var getNameMethod = classType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    return getNameMethod.Invoke(linkedClass, null) as string;
                }
            }
            catch { }
            return null;
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
                var card = hand[index];
                string name = GetCardTitle(card);
                int cost = GetCardCost(card);
                string cardType = GetCardType(card);
                string clanName = GetCardClan(card);
                string description = GetCardDescription(card);

                string typeStr = !string.IsNullOrEmpty(cardType) ? $" ({cardType})" : "";
                string clanStr = !string.IsNullOrEmpty(clanName) ? $", {clanName}" : "";
                var sb = new StringBuilder();
                sb.Append($"Card {index + 1}: {name}{typeStr}{clanStr}, {cost} ember.");

                if (!string.IsNullOrEmpty(description))
                {
                    sb.Append($" {description}");
                }

                // Add keyword tooltips
                string keywords = GetCardKeywords(card, description);
                if (!string.IsNullOrEmpty(keywords))
                {
                    sb.Append($" Keywords: {keywords}");
                }

                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), true);
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Speak($"No card at position {index + 1}", true);
            }
        }

        /// <summary>
        /// Extract keyword definitions from a card's description
        /// </summary>
        private string GetCardKeywords(object cardState, string description)
        {
            if (string.IsNullOrEmpty(description)) return null;

            try
            {
                var keywords = new List<string>();

                // Known keywords with their definitions
                var knownKeywords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Armor", "Armor: Reduces damage taken" },
                    { "Rage", "Rage: Increases attack damage" },
                    { "Regen", "Regen: Restores health each turn" },
                    { "Frostbite", "Frostbite: Deals damage at end of turn" },
                    { "Sap", "Sap: Reduces attack" },
                    { "Dazed", "Dazed: Cannot attack this turn" },
                    { "Rooted", "Rooted: Cannot move floors" },
                    { "Quick", "Quick: Attacks before other units" },
                    { "Multistrike", "Multistrike: Attacks multiple times" },
                    { "Sweep", "Sweep: Attacks all enemies on floor" },
                    { "Trample", "Trample: Excess damage hits next enemy" },
                    { "Lifesteal", "Lifesteal: Heals for damage dealt" },
                    { "Spikes", "Spikes: Deals damage to attackers" },
                    { "Damage Shield", "Damage Shield: Blocks next attack" },
                    { "Stealth", "Stealth: Cannot be targeted until it attacks" },
                    { "Burnout", "Burnout: Dies at end of turn" },
                    { "Endless", "Endless: Returns to hand when killed" },
                    { "Fragile", "Fragile: Dies when damaged" },
                    { "Heartless", "Heartless: Cannot be healed" },
                    { "Immobile", "Immobile: Cannot be moved" },
                    { "Permafrost", "Permafrost: Remains in hand between turns" },
                    { "Frozen", "Frozen: Cannot be played until unfrozen" },
                    { "Consume", "Consume: Removed from deck after playing" },
                    { "Holdover", "Holdover: Returns to hand at end of turn" },
                    { "Purge", "Purge: Removed from deck permanently" },
                    { "Intrinsic", "Intrinsic: Always drawn on first turn" },
                    { "Etch", "Etch: Permanently upgrade this card" },
                    { "Emberdrain", "Emberdrain: Reduces ember at start of turn" },
                    { "Spell Weakness", "Spell Weakness: Takes extra spell damage" },
                    { "Melee Weakness", "Melee Weakness: Takes extra attack damage" },
                    { "Spellshield", "Spellshield: Blocks spell damage" },
                    { "Phased", "Phased: Cannot be targeted" }
                };

                foreach (var keyword in knownKeywords)
                {
                    // Check if keyword appears in description (as whole word)
                    if (System.Text.RegularExpressions.Regex.IsMatch(description,
                        $@"\b{System.Text.RegularExpressions.Regex.Escape(keyword.Key)}\b",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        if (!keywords.Contains(keyword.Value))
                        {
                            keywords.Add(keyword.Value);
                        }
                    }
                }

                if (keywords.Count > 0)
                {
                    return string.Join(". ", keywords);
                }
            }
            catch { }
            return null;
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
                output?.Speak("Floor status:", true);

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
                if (_roomManager == null)
                {
                    MonsterTrainAccessibility.LogInfo("GetRoom: RoomManager is null");
                    return null;
                }
                if (_getRoomMethod == null)
                {
                    MonsterTrainAccessibility.LogInfo("GetRoom: _getRoomMethod is null");
                    return null;
                }
            }

            try
            {
                var room = _getRoomMethod?.Invoke(_roomManager, new object[] { roomIndex });
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

        /// <summary>
        /// Get a list of all enemy units on all floors (for unit targeting).
        /// Returns a list of formatted strings like "Armored Shiv 10/20 on floor 2"
        /// </summary>
        public List<string> GetAllEnemies()
        {
            var enemies = new List<string>();
            try
            {
                // Check all 3 floors
                for (int floorNumber = 1; floorNumber <= 3; floorNumber++)
                {
                    var room = GetRoom(floorNumber);
                    if (room == null) continue;

                    var units = GetUnitsInRoom(room);
                    foreach (var unit in units)
                    {
                        if (IsEnemyUnit(unit))
                        {
                            string name = GetUnitName(unit);
                            int hp = GetUnitHP(unit);
                            int attack = GetUnitAttack(unit);
                            enemies.Add($"{name} {attack}/{hp} on floor {floorNumber}");
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
        /// Returns a list of formatted strings like "Train Steward 5/8 on floor 1"
        /// </summary>
        public List<string> GetAllFriendlyUnits()
        {
            var friendlies = new List<string>();
            try
            {
                // Check all 3 floors
                for (int floorNumber = 1; floorNumber <= 3; floorNumber++)
                {
                    var room = GetRoom(floorNumber);
                    if (room == null) continue;

                    var units = GetUnitsInRoom(room);
                    foreach (var unit in units)
                    {
                        if (!IsEnemyUnit(unit))
                        {
                            string name = GetUnitName(unit);
                            int hp = GetUnitHP(unit);
                            int attack = GetUnitAttack(unit);
                            friendlies.Add($"{name} {attack}/{hp} on floor {floorNumber}");
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
        public List<string> GetAllUnits()
        {
            var allUnits = new List<string>();
            allUnits.AddRange(GetAllFriendlyUnits());
            allUnits.AddRange(GetAllEnemies());
            return allUnits;
        }

        private List<object> GetUnitsInRoom(object room)
        {
            var units = new List<object>();
            try
            {
                var roomType = room.GetType();

                // First try AddCharactersToList method - the primary way to get characters from a room
                // This method takes a List<CharacterState> parameter and populates it
                var addCharsMethods = roomType.GetMethods().Where(m => m.Name == "AddCharactersToList").ToArray();
                foreach (var addCharsMethod in addCharsMethods)
                {
                    var parameters = addCharsMethod.GetParameters();
                    if (parameters.Length >= 1)
                    {
                        var listType = parameters[0].ParameterType;
                        // Check if it's a List type
                        if (listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(List<>))
                        {
                            try
                            {
                                // Create a new instance of the typed list
                                var charList = Activator.CreateInstance(listType);

                                // Build the argument array (some overloads may have additional params)
                                var args = new object[parameters.Length];
                                args[0] = charList;
                                // Fill additional params with defaults if any
                                for (int i = 1; i < parameters.Length; i++)
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
                                        if (c != null) units.Add(c);
                                    }
                                    if (units.Count > 0)
                                    {
                                        MonsterTrainAccessibility.LogInfo($"GetUnitsInRoom found {units.Count} units via AddCharactersToList");
                                        return units;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                MonsterTrainAccessibility.LogInfo($"AddCharactersToList failed: {ex.Message}");
                            }
                        }
                    }
                }

                // Fallback: try to access the characters field directly
                string[] fieldNames = { "characters", "_characters", "m_characters", "characterList" };
                foreach (var fieldName in fieldNames)
                {
                    var charsField = roomType.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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

        private string GetUnitName(object characterState)
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
                        if (data != null && _getCharacterNameMethod != null)
                        {
                            name = _getCharacterNameMethod.Invoke(data, null) as string;
                        }
                    }
                }

                return StripRichTextTags(name) ?? "Unit";
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
                output?.Speak("Enemies:", true);

                bool hasEnemies = false;
                int roomsFound = 0;
                int totalUnits = 0;

                // Iterate floors from top (3) to bottom (1), then pyre room (0)
                for (int i = 3; i >= 0; i--)
                {
                    var room = GetRoom(i);
                    if (room == null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Room {i} is null");
                        continue;
                    }
                    roomsFound++;

                    var units = GetUnitsInRoom(room);
                    totalUnits += units.Count;
                    MonsterTrainAccessibility.LogInfo($"Room {i} has {units.Count} units");

                    string floorName = i == 0 ? "Pyre room" : $"Floor {i}";
                    var enemyDescriptions = new List<string>();

                    foreach (var unit in units)
                    {
                        bool isEnemy = IsEnemyUnit(unit);
                        if (!isEnemy) continue;

                        hasEnemies = true;
                        string enemyDesc = GetDetailedEnemyDescription(unit);
                        enemyDescriptions.Add(enemyDesc);
                    }

                    if (enemyDescriptions.Count > 0)
                    {
                        output?.Queue($"{floorName}:");
                        foreach (var desc in enemyDescriptions)
                        {
                            output?.Queue(desc);
                        }
                    }
                }

                MonsterTrainAccessibility.LogInfo($"AnnounceEnemies: found {roomsFound} rooms, {totalUnits} total units, hasEnemies: {hasEnemies}");

                if (!hasEnemies)
                {
                    output?.Queue("No enemies on the train");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error announcing enemies: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read enemies", true);
            }
        }

        /// <summary>
        /// Get a detailed description of an enemy unit including stats, status effects, and intent
        /// </summary>
        private string GetDetailedEnemyDescription(object unit)
        {
            try
            {
                var sb = new StringBuilder();

                // Get basic info
                string name = GetUnitName(unit);
                int hp = GetUnitHP(unit);
                int maxHp = GetUnitMaxHP(unit);
                int attack = GetUnitAttack(unit);

                sb.Append($"{name}: {attack} attack, {hp}");
                if (maxHp > 0 && maxHp != hp)
                {
                    sb.Append($" of {maxHp}");
                }
                sb.Append(" health");

                // Get status effects
                string statusEffects = GetUnitStatusEffects(unit);
                if (!string.IsNullOrEmpty(statusEffects))
                {
                    sb.Append($". Status: {statusEffects}");
                }

                // Get intent (for bosses or units with visible intent)
                string intent = GetUnitIntent(unit);
                if (!string.IsNullOrEmpty(intent))
                {
                    sb.Append($". Intent: {intent}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting enemy description: {ex.Message}");
                return GetUnitName(unit) ?? "Unknown enemy";
            }
        }

        /// <summary>
        /// Get the maximum HP of a unit
        /// </summary>
        private int GetUnitMaxHP(object characterState)
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

        /// <summary>
        /// Get status effects on a unit as a readable string
        /// </summary>
        private string GetUnitStatusEffects(object characterState)
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
                var commonStatuses = new[] { "armor", "damage shield", "rage", "quick", "multistrike", "regen", "sap", "dazed", "rooted", "spell weakness" };
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
                                string displayName = FormatStatusName(statusId);
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
        private string GetStatusEffectName(object statusStack)
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

                        // Try GetStatusId
                        var getIdMethod = stateType.GetMethod("GetStatusId", Type.EmptyTypes);
                        if (getIdMethod != null)
                        {
                            var id = getIdMethod.Invoke(state, null) as string;
                            if (!string.IsNullOrEmpty(id))
                            {
                                return FormatStatusName(id);
                            }
                        }

                        // Try GetName or similar
                        var getNameMethod = stateType.GetMethod("GetName", Type.EmptyTypes) ??
                                           stateType.GetMethod("GetDisplayName", Type.EmptyTypes);
                        if (getNameMethod != null)
                        {
                            var name = getNameMethod.Invoke(state, null) as string;
                            if (!string.IsNullOrEmpty(name))
                            {
                                return StripRichTextTags(name);
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
        private int GetStatusEffectStacks(object statusStack)
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
        private string FormatStatusName(string statusId)
        {
            if (string.IsNullOrEmpty(statusId)) return statusId;

            // Convert snake_case or camelCase to Title Case
            statusId = statusId.Replace("_", " ");
            statusId = System.Text.RegularExpressions.Regex.Replace(statusId, "([a-z])([A-Z])", "$1 $2");

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
        private string GetUnitIntent(object characterState)
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

                // Check attack damage to infer basic intent
                int attack = GetUnitAttack(characterState);
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
        /// Get the intent of a boss enemy
        /// </summary>
        private string GetBossIntent(object bossState)
        {
            try
            {
                var bossType = bossState.GetType();

                // Try to get the current action group
                var getActionGroupMethod = bossType.GetMethod("GetCurrentActionGroup", Type.EmptyTypes) ??
                                          bossType.GetMethod("GetActionGroup", Type.EmptyTypes);

                object actionGroup = null;
                if (getActionGroupMethod != null)
                {
                    actionGroup = getActionGroupMethod.Invoke(bossState, null);
                }

                // Try via field if method not found
                if (actionGroup == null)
                {
                    var actionGroupField = bossType.GetField("_actionGroup", BindingFlags.NonPublic | BindingFlags.Instance) ??
                                          bossType.GetField("actionGroup", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (actionGroupField != null)
                    {
                        actionGroup = actionGroupField.GetValue(bossState);
                    }
                }

                if (actionGroup != null)
                {
                    var agType = actionGroup.GetType();

                    // Get next action
                    var getNextActionMethod = agType.GetMethod("GetNextAction", Type.EmptyTypes);
                    if (getNextActionMethod != null)
                    {
                        var nextAction = getNextActionMethod.Invoke(actionGroup, null);
                        if (nextAction != null)
                        {
                            return GetBossActionDescription(nextAction);
                        }
                    }

                    // Get all actions
                    var getActionsMethod = agType.GetMethod("GetActions", Type.EmptyTypes);
                    if (getActionsMethod != null)
                    {
                        var actions = getActionsMethod.Invoke(actionGroup, null) as System.Collections.IList;
                        if (actions != null && actions.Count > 0)
                        {
                            return GetBossActionDescription(actions[0]);
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
        private string GetBossActionDescription(object bossAction)
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
                        string floorName = roomIndex == 0 ? "pyre room" : $"floor {roomIndex}";
                        parts.Add($"targeting {floorName}");
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
        private string GetActionDescription(object action)
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
                        return StripRichTextTags(desc);
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
