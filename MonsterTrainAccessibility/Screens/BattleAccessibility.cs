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
        private System.Reflection.MethodInfo _getGoldMethod;
        private System.Reflection.MethodInfo _getRoomMethod;
        private System.Reflection.MethodInfo _getSelectedRoomMethod;
        private System.Reflection.MethodInfo _getHPMethod;
        private System.Reflection.MethodInfo _getAttackDamageMethod;
        private System.Reflection.MethodInfo _getTeamTypeMethod;
        private System.Reflection.MethodInfo _getCharacterNameMethod;
        private bool _roomManagerMethodsLogged = false;

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
                    _getGoldMethod = saveManagerType.GetMethod("GetGold", Type.EmptyTypes);
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

                    // Log all RoomManager methods once to find the selected room method
                    if (!_roomManagerMethodsLogged)
                    {
                        _roomManagerMethodsLogged = true;
                        var methods = roomManagerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                        var relevantMethods = methods.Where(m =>
                            m.Name.Contains("Room") || m.Name.Contains("Select") ||
                            m.Name.Contains("Active") || m.Name.Contains("Focus") ||
                            m.Name.Contains("Current") || m.Name.Contains("View") ||
                            m.Name.Contains("Index") || m.Name.Contains("Floor"))
                            .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
                        MonsterTrainAccessibility.LogInfo($"RoomManager room-related methods: {string.Join(", ", relevantMethods)}");

                        // Also check properties
                        var properties = roomManagerType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                        var relevantProps = properties.Where(p =>
                            p.Name.Contains("Room") || p.Name.Contains("Select") ||
                            p.Name.Contains("Active") || p.Name.Contains("Focus") ||
                            p.Name.Contains("Current") || p.Name.Contains("View") ||
                            p.Name.Contains("Index") || p.Name.Contains("Floor"))
                            .Select(p => $"{p.Name} ({p.PropertyType.Name})");
                        MonsterTrainAccessibility.LogInfo($"RoomManager room-related properties: {string.Join(", ", relevantProps)}");
                    }

                    // Try to find the selected room method/property
                    _getSelectedRoomMethod = roomManagerType.GetMethod("GetSelectedRoom", Type.EmptyTypes) ??
                                             roomManagerType.GetMethod("GetActiveRoom", Type.EmptyTypes) ??
                                             roomManagerType.GetMethod("GetFocusedRoom", Type.EmptyTypes) ??
                                             roomManagerType.GetMethod("GetCurrentRoom", Type.EmptyTypes) ??
                                             roomManagerType.GetMethod("GetSelectedRoomIndex", Type.EmptyTypes) ??
                                             roomManagerType.GetMethod("GetActiveRoomIndex", Type.EmptyTypes) ??
                                             roomManagerType.GetMethod("GetFocusedRoomIndex", Type.EmptyTypes);
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
            Patches.PreviewModeDetector.Reset();

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
            output?.Speak("Your turn", false);

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
            MonsterTrainAccessibility.ScreenReader?.Speak("End turn. Combat phase.", false);
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
            MonsterTrainAccessibility.ScreenReader?.Speak("Victory! Battle won.", false);
        }

        /// <summary>
        /// Called when pyre is destroyed
        /// </summary>
        public void OnBattleLost()
        {
            IsInBattle = false;
            MonsterTrainAccessibility.ScreenReader?.Speak("Defeat. The pyre has been destroyed.", false);
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
                    MonsterTrainAccessibility.ScreenReader?.Speak("Hand is empty", false);
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
                    bool upgraded = HasCardUpgrades(card);

                    string playable = (currentEnergy >= 0 && cost > currentEnergy) ? ", unplayable" : "";
                    string upgradeStr = upgraded ? " (upgraded)" : "";

                    // Build card announcement based on verbosity
                    if (verbosity == Core.VerbosityLevel.Minimal)
                    {
                        sb.Append($"{i + 1}: {name}{upgradeStr}, {cost} ember{playable}. ");
                    }
                    else
                    {
                        // Normal and Verbose include type, clan, and description
                        string typeStr = !string.IsNullOrEmpty(cardType) ? $" ({cardType})" : "";
                        string clanStr = !string.IsNullOrEmpty(clanName) ? $", {clanName}" : "";
                        sb.Append($"{i + 1}: {name}{upgradeStr}{typeStr}{clanStr}, {cost} ember{playable}. ");

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

                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error announcing hand: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read hand", false);
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

        /// <summary>
        /// Check if a card has any upgrades applied
        /// </summary>
        private bool HasCardUpgrades(object cardState)
        {
            try
            {
                if (cardState == null) return false;
                var type = cardState.GetType();
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Try GetUpgrades method
                var getUpgradesMethod = type.GetMethod("GetUpgrades", Type.EmptyTypes);
                if (getUpgradesMethod != null)
                {
                    var upgrades = getUpgradesMethod.Invoke(cardState, null) as System.Collections.IList;
                    if (upgrades != null && upgrades.Count > 0) return true;
                }

                // Try upgrades field
                var upgradesField = type.GetField("upgrades", bindingFlags) ??
                                    type.GetField("_upgrades", bindingFlags) ??
                                    type.GetField("appliedUpgrades", bindingFlags);
                if (upgradesField != null)
                {
                    var upgrades = upgradesField.GetValue(cardState) as System.Collections.IList;
                    if (upgrades != null && upgrades.Count > 0) return true;
                }
            }
            catch { }
            return false;
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
        /// Converts game-specific tags to readable text and removes Unity rich text tags.
        /// </summary>
        public static string StripRichTextTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Convert sprite tags to readable text first
            // Handles: <sprite name=Gold>, <sprite name="Gold">, etc.
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<sprite\s+name\s*=\s*[""']?([^""'>\s]+)[""']?\s*/?>",
                match => " " + MapSpriteName(match.Groups[1].Value) + " ",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<sprite\s*=\s*[""']?([^""'>\s]+)[""']?\s*/?>",
                match => " " + MapSpriteName(match.Groups[1].Value) + " ",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Strip localization placeholders: {[codeint0]}, {[effect0.power]}, {[status0.power]}, etc.
            // These appear in generic tooltip text where no card/effect context is available.
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"\{?\[(?:effect|status|trait|paramint|codeint|dynamicint|statusmultiplier)[^\]]*\]\}?", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Handle <gold>X</gold> -> "X gold"
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<gold>([^<]*)</gold>",
                match => match.Groups[1].Value + " gold",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Handle <+Xpower> or <power>X</power> formats
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<\+?(\d+)power>",
                match => "+" + match.Groups[1].Value + " power",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<power>([^<]*)</power>",
                match => match.Groups[1].Value + " power",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Handle <ember>X</ember> format
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<ember>([^<]*)</ember>",
                match => match.Groups[1].Value + " ember",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Handle <health>X</health> format
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<health>([^<]*)</health>",
                match => match.Groups[1].Value + " health",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Handle <damage>X</damage> or <attack>X</attack> format
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<(?:damage|attack)>([^<]*)</(?:damage|attack)>",
                match => match.Groups[1].Value + " damage",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Handle <capacity>X</capacity> format
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<capacity>([^<]*)</capacity>",
                match => match.Groups[1].Value + " capacity",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Use regex to strip all remaining XML-like tags
            // This handles: <tag>, </tag>, <tag attribute="value">, self-closing <tag/>, etc.
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "");

            // Also clean up any double spaces that might result
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

            return text.Trim();
        }

        /// <summary>
        /// Map sprite names to readable text for screen readers.
        /// </summary>
        private static string MapSpriteName(string spriteName)
        {
            switch (spriteName.ToLowerInvariant())
            {
                case "xcost": return "X";
                case "gold": return "gold";
                case "capacity": return "capacity";
                case "ember": return "ember";
                case "health": return "health";
                case "attack": return "attack";
                case "damage": return "damage";
                default: return spriteName.ToLower();
            }
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

        /// <summary>
        /// Extract keyword definitions from a card's description
        /// </summary>
        private string GetCardKeywords(object cardState, string description)
        {
            if (string.IsNullOrEmpty(description)) return null;

            try
            {
                var keywords = new List<string>();

                // Keywords loaded from game localization + fallbacks
                var knownKeywords = Core.KeywordManager.GetKeywords();

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
        /// Convert a room index to a user-facing floor name.
        /// Room 0 = Bottom floor, Room 1 = Middle floor, Room 2 = Top floor, Room 3 = Pyre room.
        /// </summary>
        public static string RoomIndexToFloorName(int roomIndex)
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
        public void AnnounceAllFloors()
        {
            try
            {
                var output = MonsterTrainAccessibility.ScreenReader;
                output?.Speak("Floor status:", false);

                // Monster Train has 3 playable floors + pyre room
                // Room indices: 0=bottom, 1=middle, 2=top, 3=pyre room
                for (int roomIndex = 0; roomIndex <= 3; roomIndex++)
                {
                    var room = GetRoom(roomIndex);
                    if (room != null)
                    {
                        if (roomIndex == 3)
                        {
                            // Pyre room - show pyre health and any units
                            var pyreUnits = GetUnitsInRoom(room);
                            int pyreHP = GetPyreHealth();
                            int maxPyreHP = GetMaxPyreHealth();
                            var sb = new System.Text.StringBuilder();
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
                                    string unitDesc = GetUnitBriefDescription(unit);
                                    bool isEnemy = IsEnemyUnit(unit);
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
                            int usedCapacity = 0;
                            int maxCapacity = GetFloorCapacity(room);
                            var units = GetUnitsInRoom(room);

                            // Calculate used capacity from unit sizes
                            foreach (var unit in units)
                            {
                                usedCapacity += GetUnitSize(unit);
                            }

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
                                    string unitDesc = GetUnitBriefDescription(unit);
                                    bool isEnemy = IsEnemyUnit(unit);
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
        private string GetUnitBriefDescription(object unit)
        {
            string name = GetUnitName(unit);
            int hp = GetUnitHP(unit);
            int maxHp = GetUnitMaxHP(unit);
            int attack = GetUnitAttack(unit);
            int size = GetUnitSize(unit);
            bool isEnemy = IsEnemyUnit(unit);

            var sb = new StringBuilder();
            sb.Append($"{name} {attack} attack, {hp}");
            if (maxHp > 0 && maxHp != hp)
                sb.Append($" of {maxHp}");
            sb.Append(" health");

            // Add all status effects
            string statusEffects = GetUnitStatusEffects(unit);
            if (!string.IsNullOrEmpty(statusEffects))
            {
                sb.Append($" ({statusEffects})");
            }

            // Add unit abilities/keywords (like Relentless, Multistrike, etc.)
            string abilities = GetUnitAbilities(unit);
            if (!string.IsNullOrEmpty(abilities))
            {
                sb.Append($". {abilities}");
            }

            // Add keyword explanations for status effects and abilities
            string keywordExplanations = GetUnitKeywordExplanations(statusEffects, abilities);
            if (!string.IsNullOrEmpty(keywordExplanations))
            {
                sb.Append($". Keywords: {keywordExplanations}");
            }

            // Add intent for enemies
            if (isEnemy)
            {
                string intent = GetUnitIntent(unit);
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
        /// Look up keyword explanations for status effect and ability names found on a unit.
        /// </summary>
        private string GetUnitKeywordExplanations(string statusEffects, string abilities)
        {
            var keywords = Core.KeywordManager.GetKeywords();
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
                    string keyName = System.Text.RegularExpressions.Regex.Replace(trimmed, @"\s+\d+$", "").Trim();
                    if (string.IsNullOrEmpty(keyName) || seen.Contains(keyName)) continue;
                    seen.Add(keyName);

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

        /// <summary>
        /// Get a brief description of a unit for targeting announcements.
        /// Public wrapper around GetUnitBriefDescription for use by patches.
        /// </summary>
        public string GetTargetUnitDescription(object characterState)
        {
            if (characterState == null) return null;
            try
            {
                return GetUnitBriefDescription(characterState);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting target unit description: {ex.Message}");
                return GetUnitName(characterState) ?? "Unknown unit";
            }
        }

        /// <summary>
        /// Get the armor stacks on a unit
        /// </summary>
        private int GetUnitArmor(object characterState)
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
        private int GetUnitSize(object characterState)
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
        /// Get the maximum capacity of a floor/room
        /// </summary>
        private int GetFloorCapacity(object room)
        {
            try
            {
                var roomType = room.GetType();

                // Try GetCapacity method
                var getCapacityMethod = roomType.GetMethod("GetCapacity", Type.EmptyTypes);
                if (getCapacityMethod != null)
                {
                    var result = getCapacityMethod.Invoke(room, null);
                    if (result is int capacity)
                    {
                        return capacity;
                    }
                }

                // Try capacity field
                var capacityField = roomType.GetField("capacity", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                 ?? roomType.GetField("_capacity", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (capacityField != null)
                {
                    var result = capacityField.GetValue(room);
                    if (result is int capacity)
                    {
                        return capacity;
                    }
                }
            }
            catch { }
            return 7; // Default floor capacity in Monster Train
        }

        /// <summary>
        /// Get floor corruption info from a room state (Last Divinity DLC).
        /// Returns e.g. "Corruption: 2/4" or null if corruption is not active.
        /// </summary>
        private string GetFloorCorruption(object room)
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
        private string GetFloorEnchantments(object room)
        {
            try
            {
                if (room == null) return null;
                var roomType = room.GetType();
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                var enchantments = new List<string>();

                // Try GetRoomStateModifiers or similar
                var getModifiersMethod = roomType.GetMethod("GetRoomStateModifiers", Type.EmptyTypes) ??
                                         roomType.GetMethod("GetModifiers", Type.EmptyTypes) ??
                                         roomType.GetMethod("GetEnchantments", Type.EmptyTypes);

                if (getModifiersMethod != null)
                {
                    var modifiers = getModifiersMethod.Invoke(room, null) as System.Collections.IList;
                    if (modifiers != null && modifiers.Count > 0)
                    {
                        foreach (var modifier in modifiers)
                        {
                            if (modifier == null) continue;
                            var modType = modifier.GetType();
                            var getNameMethod = modType.GetMethod("GetName", Type.EmptyTypes);
                            if (getNameMethod != null)
                            {
                                string name = getNameMethod.Invoke(modifier, null) as string;
                                if (!string.IsNullOrEmpty(name))
                                    enchantments.Add(StripRichTextTags(name));
                            }
                        }
                    }
                }

                // Also check for status effects on the room itself
                var statusField = roomType.GetField("statusEffects", bindingFlags) ??
                                  roomType.GetField("_statusEffects", bindingFlags);
                if (statusField != null)
                {
                    var effects = statusField.GetValue(room) as System.Collections.IList;
                    if (effects != null)
                    {
                        foreach (var effect in effects)
                        {
                            if (effect == null) continue;
                            var effectType = effect.GetType();
                            string effectId = null;

                            var getIdMethod = effectType.GetMethod("GetStatusId", Type.EmptyTypes);
                            if (getIdMethod != null)
                            {
                                effectId = getIdMethod.Invoke(effect, null) as string;
                            }
                            else
                            {
                                var idField = effectType.GetField("statusId", bindingFlags);
                                effectId = idField?.GetValue(effect) as string;
                            }

                            if (!string.IsNullOrEmpty(effectId) && !enchantments.Contains(effectId))
                            {
                                enchantments.Add(effectId);
                            }
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
        public int GetSelectedFloor()
        {
            try
            {
                if (_roomManager == null)
                {
                    FindManagers();
                }

                if (_roomManager == null)
                {
                    MonsterTrainAccessibility.LogInfo("GetSelectedFloor: RoomManager is null");
                    return -1;
                }

                var roomManagerType = _roomManager.GetType();

                // GetSelectedRoom() returns an int (room index) directly
                var getSelectedRoomMethod = roomManagerType.GetMethod("GetSelectedRoom", Type.EmptyTypes);
                if (getSelectedRoomMethod != null)
                {
                    var result = getSelectedRoomMethod.Invoke(_roomManager, null);
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
        /// Takes room index directly (0=bottom, 1=middle, 2=top, 3=pyre room).
        /// </summary>
        public string GetFloorSummary(int roomIndex)
        {
            try
            {
                var room = GetRoom(roomIndex);
                if (room == null)
                {
                    return $"{RoomIndexToFloorName(roomIndex)}: Unknown";
                }

                // Pyre room - special handling
                if (roomIndex == 3)
                {
                    int pyreHP = GetPyreHealth();
                    int maxPyreHP = GetMaxPyreHealth();
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
                            string desc = GetUnitBriefDescription(unit);
                            bool isEnemy = IsEnemyUnit(unit);
                            pyreParts.Add($"{(isEnemy ? "Enemy " : "")}{desc}");
                        }
                    }
                    return pyreParts.Count > 0 ? string.Join(". ", pyreParts) : "Empty";
                }

                // Regular floor - get capacity info
                int maxCapacity = GetFloorCapacity(room);
                int usedCapacity = 0;

                var units = GetUnitsInRoom(room);

                // Calculate used capacity from unit sizes
                foreach (var unit in units)
                {
                    usedCapacity += GetUnitSize(unit);
                }

                string capacityInfo = $"{usedCapacity} of {maxCapacity} capacity";

                // Add corruption info (DLC)
                string corruptionInfo = GetFloorCorruption(room);

                if (units.Count == 0)
                {
                    string emptyInfo = $"Empty. {capacityInfo}";
                    if (!string.IsNullOrEmpty(corruptionInfo))
                        emptyInfo += $". {corruptionInfo}";
                    return emptyInfo;
                }

                var friendlyUnits = new List<string>();
                var enemyUnits = new List<string>();

                foreach (var unit in units)
                {
                    // Use GetUnitBriefDescription which includes abilities and intents
                    string description = GetUnitBriefDescription(unit);

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
                parts.Add(capacityInfo);
                if (!string.IsNullOrEmpty(corruptionInfo))
                    parts.Add(corruptionInfo);
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
        /// </summary>
        public List<string> GetAllEnemies()
        {
            var enemies = new List<string>();
            try
            {
                for (int roomIndex = 0; roomIndex <= 2; roomIndex++)
                {
                    var room = GetRoom(roomIndex);
                    if (room == null) continue;

                    var units = GetUnitsInRoom(room);
                    foreach (var unit in units)
                    {
                        if (IsEnemyUnit(unit))
                        {
                            string name = GetUnitName(unit);
                            int hp = GetUnitHP(unit);
                            int maxHp = GetUnitMaxHP(unit);
                            int attack = GetUnitAttack(unit);
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
        public List<string> GetAllFriendlyUnits()
        {
            var friendlies = new List<string>();
            try
            {
                for (int roomIndex = 0; roomIndex <= 2; roomIndex++)
                {
                    var room = GetRoom(roomIndex);
                    if (room == null) continue;

                    var units = GetUnitsInRoom(room);
                    foreach (var unit in units)
                    {
                        if (!IsEnemyUnit(unit))
                        {
                            string name = GetUnitName(unit);
                            int hp = GetUnitHP(unit);
                            int maxHp = GetUnitMaxHP(unit);
                            int attack = GetUnitAttack(unit);
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

                int gold = GetGold();
                if (gold >= 0)
                {
                    sb.Append($"Gold: {gold}. ");
                }

                int pyreHP = GetPyreHealth();
                int maxPyreHP = GetMaxPyreHealth();
                if (pyreHP >= 0)
                {
                    sb.Append($"Pyre: {pyreHP} of {maxPyreHP}. ");
                }

                // Pyre armor and attack
                int pyreArmor = GetPyreStatusEffect("armor");
                if (pyreArmor > 0)
                {
                    sb.Append($"Pyre Armor: {pyreArmor}. ");
                }

                int pyreAttack = GetPyreStatusEffect("attack");
                if (pyreAttack > 0)
                {
                    sb.Append($"Pyre Attack: {pyreAttack}. ");
                }

                var hand = GetHandCards();
                if (hand != null)
                {
                    sb.Append($"Cards in hand: {hand.Count}. ");
                }

                // Crystals and threat level (DLC)
                string crystalInfo = GetCrystalAndThreatInfo();
                if (!string.IsNullOrEmpty(crystalInfo))
                {
                    sb.Append($"{crystalInfo}. ");
                }

                // Wave counter
                string waveInfo = GetWaveInfo();
                if (!string.IsNullOrEmpty(waveInfo))
                {
                    sb.Append(waveInfo);
                }

                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error announcing resources: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read resources", false);
            }
        }

        /// <summary>
        /// Get crystal count and threat level info (Last Divinity DLC).
        /// Returns null if DLC is not active.
        /// </summary>
        private string GetCrystalAndThreatInfo()
        {
            try
            {
                if (_saveManager == null)
                {
                    FindManagers();
                }
                if (_saveManager == null) return null;

                var saveType = _saveManager.GetType();

                // Check if DLC crystals are shown (ShowPactCrystals)
                var showMethod = saveType.GetMethod("ShowPactCrystals", Type.EmptyTypes);
                if (showMethod != null)
                {
                    bool show = (bool)showMethod.Invoke(_saveManager, null);
                    if (!show) return null;
                }

                // Get crystal count via GetDlcSaveData<HellforgedSaveData>
                int crystals = -1;
                var getDlcMethod = saveType.GetMethod("GetDlcSaveData");
                if (getDlcMethod != null && getDlcMethod.IsGenericMethod)
                {
                    // Find HellforgedSaveData type
                    Type hellforgedType = null;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        hellforgedType = asm.GetType("HellforgedSaveData");
                        if (hellforgedType != null) break;
                    }
                    if (hellforgedType != null)
                    {
                        var genericMethod = getDlcMethod.MakeGenericMethod(hellforgedType);
                        // DLC enum: Hellforged = 1
                        var dlcEnum = typeof(int); // placeholder
                        Type dlcType = null;
                        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            dlcType = asm.GetType("DLC");
                            if (dlcType != null && dlcType.IsEnum) break;
                            dlcType = null;
                        }
                        if (dlcType != null)
                        {
                            var hellforgedValue = Enum.ToObject(dlcType, 1); // Hellforged = 1
                            var dlcSaveData = genericMethod.Invoke(_saveManager, new object[] { hellforgedValue });
                            if (dlcSaveData != null)
                            {
                                var getCrystalsMethod = dlcSaveData.GetType().GetMethod("GetCrystals", Type.EmptyTypes);
                                if (getCrystalsMethod != null)
                                {
                                    crystals = (int)getCrystalsMethod.Invoke(dlcSaveData, null);
                                }
                            }
                        }
                    }
                }

                // Fallback: try direct methods on SaveManager
                if (crystals < 0)
                {
                    var getPactMethod = saveType.GetMethod("GetPactCrystalCount", Type.EmptyTypes) ??
                                        saveType.GetMethod("GetCrystalCount", Type.EmptyTypes) ??
                                        saveType.GetMethod("GetShardCount", Type.EmptyTypes);
                    if (getPactMethod != null)
                    {
                        crystals = (int)getPactMethod.Invoke(_saveManager, null);
                    }
                }

                if (crystals < 0) return null;

                // Determine threat level based on crystal count
                string threat = GetThreatLevelName(crystals);
                if (!string.IsNullOrEmpty(threat))
                {
                    return $"Crystals: {crystals}. Threat: {threat}";
                }
                return $"Crystals: {crystals}";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting crystal/threat info: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get the threat level name based on crystal count.
        /// Threat bands: 0=None, >0=Low, >=lowAmount=Moderate, >=warningAmount=Warning, >=dangerAmount=Danger
        /// </summary>
        private string GetThreatLevelName(int crystals)
        {
            try
            {
                if (crystals <= 0) return "None";

                if (_saveManager == null) return "Low";
                var saveType = _saveManager.GetType();

                // Try to get threat level thresholds from BalanceData
                var getBalanceMethod = saveType.GetMethod("GetBalanceData", Type.EmptyTypes);
                if (getBalanceMethod != null)
                {
                    var balanceData = getBalanceMethod.Invoke(_saveManager, null);
                    if (balanceData != null)
                    {
                        // GetHellforgedThreatLevelAtDistance returns a HellforgedThreatLevel with low/warning/danger amounts
                        var getThreatMethod = balanceData.GetType().GetMethod("GetHellforgedThreatLevelAtDistance");
                        if (getThreatMethod != null)
                        {
                            // Pass 0 for current distance - threat levels may vary by ring
                            var threatData = getThreatMethod.Invoke(balanceData, new object[] { 0 });
                            if (threatData != null)
                            {
                                var threatType = threatData.GetType();
                                var lowField = threatType.GetField("lowAmount") ?? threatType.GetField("low");
                                var warnField = threatType.GetField("warningAmount") ?? threatType.GetField("warning");
                                var dangerField = threatType.GetField("dangerAmount") ?? threatType.GetField("danger");

                                int low = lowField != null ? (int)lowField.GetValue(threatData) : 10;
                                int warning = warnField != null ? (int)warnField.GetValue(threatData) : 50;
                                int danger = dangerField != null ? (int)dangerField.GetValue(threatData) : 80;

                                if (crystals >= danger) return "Danger";
                                if (crystals >= warning) return "Warning";
                                if (crystals >= low) return "Moderate";
                                return "Low";
                            }
                        }
                    }
                }

                // Fallback: rough estimate based on typical values
                if (crystals >= 80) return "Danger";
                if (crystals >= 50) return "Warning";
                if (crystals >= 25) return "Moderate";
                return "Low";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting threat level: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get wave info from the combat manager (current wave / total waves)
        /// </summary>
        private string GetWaveInfo()
        {
            try
            {
                if (_combatManager == null) return null;

                var cmType = _combatManager.GetType();
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Try GetCurrentWaveIndex or similar
                int currentWave = -1;
                int totalWaves = -1;

                var getCurrentWaveMethod = cmType.GetMethod("GetCurrentWaveIndex", Type.EmptyTypes) ??
                                           cmType.GetMethod("GetCurrentWave", Type.EmptyTypes);
                if (getCurrentWaveMethod != null)
                {
                    var result = getCurrentWaveMethod.Invoke(_combatManager, null);
                    if (result is int w) currentWave = w;
                }

                // Try field access
                if (currentWave < 0)
                {
                    var waveField = cmType.GetField("currentWaveIndex", bindingFlags) ??
                                    cmType.GetField("_currentWaveIndex", bindingFlags) ??
                                    cmType.GetField("currentWave", bindingFlags);
                    if (waveField != null)
                    {
                        var val = waveField.GetValue(_combatManager);
                        if (val is int w) currentWave = w;
                    }
                }

                // Get total waves
                var getTotalWavesMethod = cmType.GetMethod("GetNumWaves", Type.EmptyTypes) ??
                                          cmType.GetMethod("GetTotalWaves", Type.EmptyTypes) ??
                                          cmType.GetMethod("GetWaveCount", Type.EmptyTypes);
                if (getTotalWavesMethod != null)
                {
                    var result = getTotalWavesMethod.Invoke(_combatManager, null);
                    if (result is int w) totalWaves = w;
                }

                if (totalWaves < 0)
                {
                    var wavesField = cmType.GetField("numWaves", bindingFlags) ??
                                     cmType.GetField("_numWaves", bindingFlags) ??
                                     cmType.GetField("totalWaves", bindingFlags);
                    if (wavesField != null)
                    {
                        var val = wavesField.GetValue(_combatManager);
                        if (val is int w) totalWaves = w;
                    }
                }

                if (currentWave >= 0 && totalWaves > 0)
                {
                    return $"Wave {currentWave + 1} of {totalWaves}. ";
                }
                else if (currentWave >= 0)
                {
                    return $"Wave {currentWave + 1}. ";
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting wave info: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get a status effect value from the pyre (room index 3)
        /// </summary>
        private int GetPyreStatusEffect(string effectName)
        {
            try
            {
                var pyreRoom = GetRoom(3);
                if (pyreRoom == null) return 0;

                // Get pyre character from the room
                var units = GetUnitsInRoom(pyreRoom);
                foreach (var unit in units)
                {
                    int stacks = GetStatusEffectStacks(unit, effectName);
                    if (stacks > 0) return stacks;
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Get status effect stacks on a unit
        /// </summary>
        private int GetStatusEffectStacks(object unit, string effectId)
        {
            try
            {
                if (unit == null) return 0;
                var unitType = unit.GetType();

                var getStacksMethod = unitType.GetMethod("GetStatusEffectStacks",
                    new[] { typeof(string) });
                if (getStacksMethod != null)
                {
                    var result = getStacksMethod.Invoke(unit, new object[] { effectId });
                    if (result is int stacks) return stacks;
                }
            }
            catch { }
            return 0;
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

        public int GetPyreHealth()
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

        public int GetMaxPyreHealth()
        {
            try
            {
                var result = _getMaxTowerHPMethod?.Invoke(_saveManager, null);
                if (result is int hp) return hp;
            }
            catch { }
            return -1;
        }

        /// <summary>
        /// Get the current deck size
        /// </summary>
        public int GetDeckSize()
        {
            try
            {
                if (_cardManager == null)
                {
                    FindManagers();
                }

                if (_cardManager != null)
                {
                    var cardManagerType = _cardManager.GetType();

                    // Try GetAllCards or GetDeck method
                    var getCardsMethod = cardManagerType.GetMethod("GetAllCards", Type.EmptyTypes)
                                      ?? cardManagerType.GetMethod("GetDeck", Type.EmptyTypes);
                    if (getCardsMethod != null)
                    {
                        var cards = getCardsMethod.Invoke(_cardManager, null);
                        if (cards is System.Collections.ICollection collection)
                        {
                            return collection.Count;
                        }
                    }

                    // Try GetDeckCount method
                    var getCountMethod = cardManagerType.GetMethod("GetDeckCount", Type.EmptyTypes);
                    if (getCountMethod != null)
                    {
                        var result = getCountMethod.Invoke(_cardManager, null);
                        if (result is int count) return count;
                    }
                }
            }
            catch { }
            return -1;
        }

        private int GetGold()
        {
            if (_saveManager == null || _getGoldMethod == null)
            {
                FindManagers();
            }

            try
            {
                var result = _getGoldMethod?.Invoke(_saveManager, null);
                if (result is int gold) return gold;
            }
            catch { }
            return -1;
        }

        #endregion

        #region Enemy Reading

        /// <summary>
        /// Announce all units (player monsters and enemies) on each floor
        /// </summary>
        public void AnnounceEnemies()
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
                    var room = GetRoom(roomIndex);
                    if (room == null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Room {roomIndex} ({RoomIndexToFloorName(roomIndex)}) is null");
                        continue;
                    }
                    roomsFound++;

                    var units = GetUnitsInRoom(room);
                    totalUnits += units.Count;
                    MonsterTrainAccessibility.LogInfo($"Room {roomIndex} ({RoomIndexToFloorName(roomIndex)}) has {units.Count} units");

                    string floorName = RoomIndexToFloorName(roomIndex);
                    var playerDescriptions = new List<string>();
                    var enemyDescriptions = new List<string>();

                    foreach (var unit in units)
                    {
                        bool isEnemy = IsEnemyUnit(unit);
                        string unitDesc = GetDetailedEnemyDescription(unit);

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
                var pyreRoom = GetRoom(3);
                if (pyreRoom != null)
                {
                    roomsFound++;
                    var pyreUnits = GetUnitsInRoom(pyreRoom);
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
        public string GetDetailedUnitDescription(object unit)
        {
            return GetDetailedEnemyDescription(unit);
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

                // Get unit abilities/description from CharacterData
                string abilities = GetUnitAbilities(unit);
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
        /// Get unit abilities/description from CharacterData including subtypes, triggers, and traits
        /// </summary>
        private string GetUnitAbilities(object characterState)
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
        private string GetTriggerDescription(object trigger)
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
                        triggerName = StripRichTextTags(triggerName).Trim().TrimEnd(':');
                }

                // Fall back to trigger enum name if keyword text was empty
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
                            triggerTooltip = keywordEntry.Substring(colonIdx + 1).Trim();
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
                        if (!string.IsNullOrEmpty(localized))
                        {
                            // Resolve {[effect0.power]} etc. placeholders before stripping tags
                            if (localized.Contains("{["))
                                localized = ResolveTriggerEffectPlaceholders(localized, trigger, triggerType);
                            description = StripRichTextTags(localized).Trim();
                        }
                    }
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
        /// Resolve {[effect0.power]}, {[effect0.status0.power]}, {[#effect0.power]} placeholders
        /// in trigger description text using the trigger's effect data.
        /// </summary>
        private string ResolveTriggerEffectPlaceholders(string text, object trigger, Type triggerType)
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
                var regex = new System.Text.RegularExpressions.Regex(
                    @"\{\[#?effect(\d+)\.(?:status(\d+)\.)?(\w+)\]\}");

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
                var traitRegex = new System.Text.RegularExpressions.Regex(@"\{\[#?trait(\d+)\.(\w+)\]\}");
                if (traitRegex.IsMatch(text))
                {
                    // Traits are on the parent card, not the trigger - just strip unresolved ones
                    text = traitRegex.Replace(text, "");
                }

                // Strip any remaining unresolved placeholders like {[...]} to avoid reading raw variables
                var unresolvedRegex = new System.Text.RegularExpressions.Regex(@"\{\[[^\]]*\]\}");
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
        private string FormatTriggerType(string triggerType)
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
                    return System.Text.RegularExpressions.Regex.Replace(triggerType, "(\\B[A-Z])", " $1");
            }
        }

        /// <summary>
        /// Get special behaviors from CharacterData (flees, winged, treasure, etc.)
        /// </summary>
        private string GetSpecialBehaviors(object charData, Type charDataType)
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
        /// Get special ability description from character data (healing, buffs, etc.)
        /// </summary>
        private string GetCharacterSpecialAbility(object charData)
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
                        parts.Add($"targeting {RoomIndexToFloorName(roomIndex).ToLower()}");
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
        /// Announce unit death with floor info. roomIndex: 0=bottom, 1=middle, 2=top.
        /// </summary>
        public void OnUnitDied(string unitName, bool isEnemy, int roomIndex = -1)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceDeaths.Value)
                return;

            string prefix = isEnemy ? "Enemy" : "Your";
            string floorInfo = roomIndex >= 0 ? $" on {RoomIndexToFloorName(roomIndex).ToLower()}" : "";
            MonsterTrainAccessibility.ScreenReader?.Queue($"{prefix} {unitName} died{floorInfo}");
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
        /// Announce unit spawned (entering the battlefield). roomIndex: 0=bottom, 1=middle, 2=top.
        /// </summary>
        public void OnUnitSpawned(string unitName, bool isEnemy, int roomIndex)
        {
            MonsterTrainAccessibility.LogInfo($"OnUnitSpawned called: {unitName}, isEnemy={isEnemy}, roomIndex={roomIndex}, IsInBattle={IsInBattle}");

            if (!IsInBattle)
            {
                MonsterTrainAccessibility.LogInfo("OnUnitSpawned: skipping - not in battle");
                return;
            }

            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceSpawns.Value)
            {
                MonsterTrainAccessibility.LogInfo("OnUnitSpawned: skipping - AnnounceSpawns disabled");
                return;
            }

            // Skip invalid unit names
            if (string.IsNullOrEmpty(unitName) || unitName == "Unit")
            {
                MonsterTrainAccessibility.LogInfo($"OnUnitSpawned: skipping - invalid name '{unitName}'");
                return;
            }

            string floorName = roomIndex >= 0 ? RoomIndexToFloorName(roomIndex).ToLower() : "the battlefield";

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
        /// Announce enemies ascending floors (generic)
        /// </summary>
        public void OnEnemiesAscended()
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue("Enemies ascend");
        }

        /// <summary>
        /// Announce a specific enemy ascending to a floor. roomIndex: 0=bottom, 1=middle, 2=top, 3=pyre.
        /// </summary>
        public void OnEnemyAscended(string enemyName, int roomIndex)
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"{enemyName} ascends to {RoomIndexToFloorName(roomIndex).ToLower()}");
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

        /// <summary>
        /// Announce enemy dialogue/chatter (speech bubbles)
        /// </summary>
        public void OnEnemyDialogue(string text)
        {
            if (!IsInBattle)
                return;

            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceDialogue.Value)
                return;

            if (!string.IsNullOrEmpty(text))
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"Enemy says: {text}");
            }
        }

        /// <summary>
        /// Announce when combat resolution phase starts (units attacking each other)
        /// </summary>
        public void OnCombatResolutionStarted()
        {
            if (!IsInBattle)
                return;

            // Only announce if there are units to fight
            MonsterTrainAccessibility.ScreenReader?.Queue("Combat!");
        }

        /// <summary>
        /// Announce when an artifact/relic triggers during combat
        /// </summary>
        public void OnRelicTriggered(string relicName)
        {
            if (!IsInBattle)
                return;

            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceRelicTriggers.Value)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"{relicName} triggered");
        }

        /// <summary>
        /// Announce when a card is exhausted/consumed (removed from deck)
        /// </summary>
        public void OnCardExhausted(string cardName)
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"{cardName} consumed");
        }

        /// <summary>
        /// Announce pyre healing
        /// </summary>
        public void OnPyreHealed(int amount, int currentHP)
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"Pyre healed for {amount}. {currentHP} health");
        }

        /// <summary>
        /// Announce status effect removed from a unit
        /// </summary>
        public void OnStatusEffectRemoved(string unitName, string effectName, int stacks)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceStatusEffects.Value)
                return;

            // Format: "Hornbreaker Prince loses 5 Rage" or "Hornbreaker Prince loses Rage" if stacks is 1
            string message = stacks > 1
                ? $"{unitName} loses {stacks} {effectName}"
                : $"{unitName} loses {effectName}";
            MonsterTrainAccessibility.ScreenReader?.Queue(message);
        }

        /// <summary>
        /// Announce enemy descending to a lower floor (bumped down). roomIndex: 0=bottom, 1=middle, 2=top.
        /// </summary>
        public void OnEnemyDescended(string enemyName, int roomIndex)
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"{enemyName} descends to {RoomIndexToFloorName(roomIndex).ToLower()}");
        }

        /// <summary>
        /// Announce when all enemies in the current wave have been defeated
        /// </summary>
        public void OnAllEnemiesDefeated()
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue("All enemies defeated");
        }

        /// <summary>
        /// Announce combat phase transitions (MonsterTurn, HeroTurn, BossAction, etc.)
        /// </summary>
        public void OnCombatPhaseChanged(string phaseName)
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue(phaseName);
        }

        /// <summary>
        /// Announce when a unit's max HP is increased
        /// </summary>
        public void OnMaxHPBuffed(string unitName, int amount)
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName} gains {amount} max health");
        }

        #endregion
    }
}
