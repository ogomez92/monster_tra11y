using MonsterTrainAccessibility.Core;
using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Handles accessibility for the battle/combat screen.
    /// Coordinates between reader classes that access actual game state.
    /// </summary>
    public class BattleAccessibility
    {
        /// <summary>
        /// Tracks which keyword definitions have already been announced via floor/unit reading.
        /// Persists for the entire game session so definitions are only spoken once.
        /// </summary>
        public static readonly HashSet<string> AnnouncedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public bool IsInBattle { get; private set; }

        // Shared manager cache used by all reader classes
        private readonly BattleManagerCache _cache = new BattleManagerCache();

        // Track which keyword descriptions have already been announced this battle
        private HashSet<string> _announcedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public BattleAccessibility()
        {
        }

        #region Battle Lifecycle

        /// <summary>
        /// Called when combat begins
        /// </summary>
        public void OnBattleEntered()
        {
            IsInBattle = true;
            _cache.FindManagers();
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
            output?.LogCombatEvent("Your turn");

            // Read actual ember from game
            int actualEmber = ResourceReader.GetCurrentEnergy(_cache);
            if (actualEmber >= 0)
            {
                output?.Queue($"{actualEmber} ember");
                output?.LogCombatEvent($"{actualEmber} ember");
            }

            if (cardsDrawn > 0)
            {
                output?.Queue($"Drew {cardsDrawn} cards");
                output?.LogCombatEvent($"Drew {cardsDrawn} cards");
            }
        }

        /// <summary>
        /// Called when player ends their turn
        /// </summary>
        public void OnTurnEnded()
        {
            MonsterTrainAccessibility.ScreenReader?.Speak("End turn. Combat phase.", false);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent("End turn. Combat phase.");
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
                if (_cache.CombatManager != null)
                {
                    var combatType = _cache.CombatManager.GetType();
                    var endTurnMethod = combatType.GetMethod("EndPlayerTurn", Type.EmptyTypes)
                        ?? combatType.GetMethod("PlayerEndTurn", Type.EmptyTypes)
                        ?? combatType.GetMethod("EndTurn", Type.EmptyTypes);

                    if (endTurnMethod != null)
                    {
                        endTurnMethod.Invoke(_cache.CombatManager, null);
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

        #region Delegating Methods

        // Hand Reading
        public void AnnounceHand() => HandReader.AnnounceHand(_cache);

        /// <summary>
        /// Announce cards drawn (with card names)
        /// </summary>
        public void OnCardsDrawn(List<string> cardNames)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceCardDraws.Value)
                return;

            string message;
            if (cardNames.Count == 1)
            {
                message = $"Drew {cardNames[0]}";
            }
            else
            {
                message = $"Drew: {string.Join(", ", cardNames)}";
            }
            MonsterTrainAccessibility.ScreenReader?.Queue(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        /// <summary>
        /// Announce cards drawn (count only, used when card names aren't available)
        /// </summary>
        public void OnCardsDrawn(int count)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceCardDraws.Value)
                return;

            string message;
            if (count == 1)
            {
                message = "Drew 1 card";
            }
            else if (count > 1)
            {
                message = $"Drew {count} cards";
            }
            else return;

            MonsterTrainAccessibility.ScreenReader?.Queue(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        /// <summary>
        /// Called when a card is played by index
        /// </summary>
        public void OnCardPlayed(int cardIndex)
        {
            // The card was played successfully
            MonsterTrainAccessibility.ScreenReader?.Queue("Card played");
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent("Card played");
        }

        /// <summary>
        /// Called when a card is discarded
        /// </summary>
        public void OnCardDiscarded(string cardName)
        {
            if (!string.IsNullOrEmpty(cardName) && cardName != "Card")
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"Discarded {cardName}");
                MonsterTrainAccessibility.ScreenReader?.LogCombatEvent($"Discarded {cardName}");
            }
        }

        public void RefreshHand()
        {
            // Called when hand changes - could trigger re-announcement if desired
        }

        // Floor Reading
        public static string RoomIndexToFloorName(int roomIndex) => FloorReader.RoomIndexToFloorName(roomIndex);
        public void AnnounceAllFloors(HashSet<string> announcedKeywords = null) => FloorReader.AnnounceAllFloors(_cache, announcedKeywords);
        public string GetFloorSummary(int roomIndex, HashSet<string> announcedKeywords = null) => FloorReader.GetFloorSummary(_cache, roomIndex, announcedKeywords);
        public int GetSelectedFloor() => FloorReader.GetSelectedFloor(_cache);
        public bool SetSelectedFloor(int roomIndex) => FloorReader.SetSelectedFloor(_cache, roomIndex);
        public List<string> GetAllEnemies() => FloorReader.GetAllEnemies(_cache);
        public List<string> GetAllFriendlyUnits() => FloorReader.GetAllFriendlyUnits(_cache);
        public List<string> GetAllUnits() => FloorReader.GetAllUnits(_cache);
        public string GetTargetUnitDescription(object characterState) => FloorReader.GetTargetUnitDescription(_cache, characterState);

        // Resource Reading
        public void AnnounceResources() => ResourceReader.AnnounceResources(_cache);
        public int GetPyreHealth() => ResourceReader.GetPyreHealth(_cache);
        public int GetMaxPyreHealth() => ResourceReader.GetMaxPyreHealth(_cache);
        public int GetDeckSize() => ResourceReader.GetDeckSize(_cache);

        // Enemy Reading
        public void AnnounceEnemies(HashSet<string> announcedKeywords = null) => EnemyReader.AnnounceEnemies(_cache, announcedKeywords);
        public string GetDetailedUnitDescription(object unit) => EnemyReader.GetDetailedUnitDescription(_cache, unit);

        /// <summary>
        /// Strip rich text tags from text for screen reader output.
        /// Converts game-specific tags to readable text and removes Unity rich text tags.
        /// </summary>
        public static string StripRichTextTags(string text) => TextUtilities.StripRichTextTags(text);

        #endregion

        #region Combat Events

        /// <summary>
        /// Announce damage dealt
        /// </summary>
        public void OnDamageDealt(string sourceName, string targetName, int damage)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceDamage.Value)
                return;

            string message = $"{sourceName} deals {damage} to {targetName}";
            MonsterTrainAccessibility.ScreenReader?.Queue(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
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
            string message = $"{prefix} {unitName} died{floorInfo}";
            MonsterTrainAccessibility.ScreenReader?.Queue(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        /// <summary>
        /// Announce status effect applied
        /// </summary>
        public void OnStatusEffectApplied(string unitName, string effectName, int stacks)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceStatusEffects.Value)
                return;

            string message = $"{unitName} gains {effectName} {stacks}";

            // Add keyword description only the first time this keyword is seen in this battle
            if (_announcedKeywords.Add(effectName))
            {
                var keywords = Core.KeywordManager.GetKeywords();
                if (keywords != null && keywords.TryGetValue(effectName, out string explanation))
                {
                    message += $". {explanation}";
                }
            }

            MonsterTrainAccessibility.ScreenReader?.Queue(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
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

            string message;
            if (isEnemy)
            {
                message = $"Enemy {unitName} enters on {floorName}";
            }
            else
            {
                message = $"{unitName} summoned on {floorName}";
            }
            MonsterTrainAccessibility.ScreenReader?.Queue(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        /// <summary>
        /// Announce enemies ascending floors (generic)
        /// </summary>
        public void OnEnemiesAscended()
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue("Enemies ascend");
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent("Enemies ascend");
        }

        /// <summary>
        /// Announce a specific enemy ascending to a floor. roomIndex: 0=bottom, 1=middle, 2=top, 3=pyre.
        /// </summary>
        public void OnEnemyAscended(string enemyName, int roomIndex)
        {
            if (!IsInBattle)
                return;

            string message = $"{enemyName} ascends to {RoomIndexToFloorName(roomIndex).ToLower()}";
            MonsterTrainAccessibility.ScreenReader?.Queue(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        /// <summary>
        /// Announce pyre damage
        /// </summary>
        public void OnPyreDamaged(int damage, int remainingHP)
        {
            if (!IsInBattle)
                return;

            string message = $"Pyre takes {damage} damage! {remainingHP} health remaining";
            MonsterTrainAccessibility.ScreenReader?.Queue(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
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
                string message = $"Enemy says: {text}";
                MonsterTrainAccessibility.ScreenReader?.Queue(message);
                MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
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
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent("Combat!");
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

            string message = $"{relicName} triggered";
            MonsterTrainAccessibility.ScreenReader?.Queue(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        /// <summary>
        /// Announce when a unit eats a morsel (Umbra feeding mechanic)
        /// </summary>
        public void OnMorselEaten(string feederName, string morselName)
        {
            if (!IsInBattle)
                return;

            string message = $"{feederName} eats {morselName}";
            MonsterTrainAccessibility.ScreenReader?.Queue(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        /// <summary>
        /// Announce when a card is exhausted/consumed (removed from deck)
        /// </summary>
        public void OnCardExhausted(string cardName)
        {
            if (!IsInBattle)
                return;

            string message = $"{cardName} consumed";
            MonsterTrainAccessibility.ScreenReader?.Queue(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        /// <summary>
        /// Announce pyre healing
        /// </summary>
        public void OnPyreHealed(int amount, int currentHP)
        {
            if (!IsInBattle)
                return;

            string message = $"Pyre healed for {amount}. {currentHP} health";
            MonsterTrainAccessibility.ScreenReader?.Queue(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
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
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        /// <summary>
        /// Announce enemy descending to a lower floor (bumped down). roomIndex: 0=bottom, 1=middle, 2=top.
        /// </summary>
        public void OnEnemyDescended(string enemyName, int roomIndex)
        {
            if (!IsInBattle)
                return;

            string message = $"{enemyName} descends to {RoomIndexToFloorName(roomIndex).ToLower()}";
            MonsterTrainAccessibility.ScreenReader?.Queue(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        /// <summary>
        /// Announce when all enemies in the current wave have been defeated
        /// </summary>
        public void OnAllEnemiesDefeated()
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue("All enemies defeated");
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent("All enemies defeated");
        }

        /// <summary>
        /// Announce combat phase transitions (MonsterTurn, HeroTurn, BossAction, etc.)
        /// </summary>
        public void OnCombatPhaseChanged(string phaseName)
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue(phaseName);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(phaseName);
        }

        /// <summary>
        /// Announce when a unit's max HP is increased
        /// </summary>
        public void OnMaxHPBuffed(string unitName, int amount)
        {
            if (!IsInBattle)
                return;

            string message = $"{unitName} gains {amount} max health";
            MonsterTrainAccessibility.ScreenReader?.Queue(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        #endregion
    }
}
