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
        public bool IsInBattle { get; private set; }

        // Shared manager cache used by all reader classes
        private readonly BattleManagerCache _cache = new BattleManagerCache();

        // Accumulates automated-combat damage for the end-of-turn summary (CombatSummaryMode).
        private readonly CombatTurnSummary _turnSummary = new CombatTurnSummary();

        /// <summary>
        /// Shared manager cache, exposed for the review buffer providers.
        /// </summary>
        internal BattleManagerCache Cache => _cache;

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
            _turnSummary.Reset();
            Patches.CombatPhaseChangePatch.CurrentPhase = -1;

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
            _turnSummary.Reset();
            Patches.CombatPhaseChangePatch.CurrentPhase = -1;
            Core.Buffers.FocusBuffers.ClearCreature();
            MonsterTrainAccessibility.FloorReview?.Close(announce: false);
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
        /// Called when battle is won
        /// </summary>
        public void OnBattleWon()
        {
            IsInBattle = false;
            _turnSummary.Reset();
            MonsterTrainAccessibility.FloorReview?.Close(announce: false);
            MonsterTrainAccessibility.ScreenReader?.Speak("Victory! Battle won.", false);
        }

        /// <summary>
        /// Called when pyre is destroyed
        /// </summary>
        public void OnBattleLost()
        {
            IsInBattle = false;
            _turnSummary.Reset();
            MonsterTrainAccessibility.FloorReview?.Close(announce: false);
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
        public void AnnounceAllFloors() => FloorReader.AnnounceAllFloors(_cache);
        public string GetFloorSummary(int roomIndex, bool includeKeywords = false) => FloorReader.GetFloorSummary(_cache, roomIndex, includeKeywords);
        public int GetSelectedFloor() => FloorReader.GetSelectedFloor(_cache);
        public bool SetSelectedFloor(int roomIndex) => FloorReader.SetSelectedFloor(_cache, roomIndex);
        public List<string> GetAllEnemies() => FloorReader.GetAllEnemies(_cache);
        public List<string> GetAllFriendlyUnits() => FloorReader.GetAllFriendlyUnits(_cache);
        public List<string> GetAllUnits() => FloorReader.GetAllUnits(_cache);
        public string GetTargetUnitDescription(object characterState) => FloorReader.GetTargetUnitDescription(_cache, characterState);

        // Resource Reading
        public void AnnounceResources() => ResourceReader.AnnounceResources(_cache);
        public int GetCurrentEnergy() => ResourceReader.GetCurrentEnergy(_cache);
        public int GetPyreHealth() => ResourceReader.GetPyreHealth(_cache);
        public int GetMaxPyreHealth() => ResourceReader.GetMaxPyreHealth(_cache);
        public int GetDeckSize() => ResourceReader.GetDeckSize(_cache);

        // Enemy Reading
        public void AnnounceEnemies() => EnemyReader.AnnounceEnemies(_cache);
        public string GetDetailedUnitDescription(object unit, bool includeKeywords = false) => EnemyReader.GetDetailedUnitDescription(_cache, unit, includeKeywords);

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

            // During automated combat the death is folded into the end-of-turn summary
            // (per floor, and logged silently); your own spell-kills during MonsterTurn stay live.
            if (IsAutoEventSuppressed())
                _turnSummary.AddDeath(unitName, isEnemy, roomIndex);

            SpeakOrLogAutoEvent(message);
        }

        /// <summary>
        /// True when CombatSummaryMode is on and we are NOT in the player's card-play
        /// phase — i.e. an automated-combat event that should be deferred to the summary.
        /// </summary>
        private static bool IsAutoEventSuppressed()
            => MonsterTrainAccessibility.AccessibilitySettings.CombatSummaryMode.Value
               && !Patches.CombatPhaseChangePatch.IsMonsterTurn;

        /// <summary>Armor churns constantly as it absorbs hits; it's reported via the hp+armor briefs.</summary>
        private static bool IsArmorEffect(string effectName)
            => !string.IsNullOrEmpty(effectName)
               && effectName.Trim().Equals("armor", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Speak a combat event live, OR — when <see cref="IsAutoEventSuppressed"/> — log it
        /// silently so the blow-by-blow stays available in the Events buffer / combat log
        /// while only the end-of-turn summary is spoken.
        /// </summary>
        private void SpeakOrLogAutoEvent(string message)
        {
            var output = MonsterTrainAccessibility.ScreenReader;
            if (IsAutoEventSuppressed())
            {
                output?.LogCombatEvent(message);
            }
            else
            {
                output?.Queue(message);
                output?.LogCombatEvent(message);
            }
        }

        /// <summary>
        /// Announce status effect applied
        /// </summary>
        public void OnStatusEffectApplied(string unitName, string effectName, int stacks)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceStatusEffects.Value)
                return;

            // Keyword explanations are reviewable via the buffers, so the live
            // announcement stays short
            string message = $"{unitName} gains {effectName} {stacks}";
            // Armor changes are suppressed during automated combat (reflected in the
            // hp+armor briefs); other "powers" still announce live.
            if (IsArmorEffect(effectName))
                SpeakOrLogAutoEvent(message);
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Queue(message);
                MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
            }
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
            // Enemy wave spawns (PreCombat) and auto-spawns are folded into the turn-start
            // board listing; your own summons during MonsterTurn stay live.
            SpeakOrLogAutoEvent(message);
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
            SpeakOrLogAutoEvent(message);
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
        /// Announce unit chatter (speech bubbles above units)
        /// </summary>
        public void OnUnitChatter(string speakerName, string text)
        {
            if (!IsInBattle)
                return;

            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceDialogue.Value)
                return;

            if (!string.IsNullOrEmpty(text))
            {
                string speaker = string.IsNullOrEmpty(speakerName) ? "Unit" : speakerName;
                string message = $"{speaker} says: {text}";
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

            // In summary mode the "Combat!" framing cue is suppressed (user wants minimal
            // cues; the end-of-turn summary is the only spoken combat output).
            if (MonsterTrainAccessibility.AccessibilitySettings.CombatSummaryMode.Value)
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
            // Armor loss spams during combat (it absorbs every hit); suppress it during
            // automated combat - the surviving units' briefs report current armor instead.
            if (IsArmorEffect(effectName))
                SpeakOrLogAutoEvent(message);
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Queue(message);
                MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
            }
        }

        /// <summary>
        /// Announce enemy descending to a lower floor (bumped down). roomIndex: 0=bottom, 1=middle, 2=top.
        /// </summary>
        public void OnEnemyDescended(string enemyName, int roomIndex)
        {
            if (!IsInBattle)
                return;

            string message = $"{enemyName} descends to {RoomIndexToFloorName(roomIndex).ToLower()}";
            SpeakOrLogAutoEvent(message);
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

        #region Combat Summary (CombatSummaryMode)

        /// <summary>Record pyre damage taken during automated combat for the summary.</summary>
        internal void AccumulatePyre(int damage, int remaining)
            => _turnSummary.AddPyre(damage, remaining);

        /// <summary>
        /// Speak the accumulated automated-combat summary — who died on each side, then who
        /// stayed alive per floor (current board state), then any pyre damage — and reset the
        /// accumulator. Called when the player regains control (entering MonsterTurn). Leads
        /// with "Combat summary." Speaks nothing on a turn with nothing to report.
        /// </summary>
        internal void AnnounceCombatSummary()
        {
            try
            {
                if (!IsInBattle)
                    return;

                var parts = new List<string>();

                // Per floor (0..2 = bottom..top): who was defeated/lost there, then who is
                // still standing (current board state). Side blocks are enemies-first.
                for (int roomIndex = 0; roomIndex <= 2; roomIndex++)
                {
                    var yourUnits = new List<string>();
                    var enemyUnits = new List<string>();

                    var room = FloorReader.GetRoom(_cache, roomIndex);
                    if (room != null)
                    {
                        foreach (var unit in FloorReader.GetUnitsInRoom(room))
                        {
                            if (unit == null) continue;
                            int hp = FloorReader.GetUnitHP(_cache, unit);
                            if (hp <= 0) continue; // skip corpses not yet removed
                            int armor = FloorReader.GetUnitArmor(unit);
                            string brief = armor > 0
                                ? $"{FloorReader.GetUnitName(_cache, unit)} {hp} hp, {armor} armor"
                                : $"{FloorReader.GetUnitName(_cache, unit)} {hp} hp";
                            if (FloorReader.IsEnemyUnit(_cache, unit))
                                enemyUnits.Add(brief);
                            else
                                yourUnits.Add(brief);
                        }
                    }

                    var enemyDead = _turnSummary.EnemyDeathsOnFloor(roomIndex);
                    var yourDead = _turnSummary.YourDeathsOnFloor(roomIndex);

                    if (yourUnits.Count == 0 && enemyUnits.Count == 0
                        && enemyDead.Count == 0 && yourDead.Count == 0)
                        continue;

                    var floorParts = new List<string>();
                    if (enemyDead.Count > 0)
                        floorParts.Add($"enemies defeated {string.Join(", ", enemyDead)}");
                    if (enemyUnits.Count > 0)
                        floorParts.Add($"enemy {string.Join(", ", enemyUnits)}");
                    if (yourDead.Count > 0)
                        floorParts.Add($"your units lost {string.Join(", ", yourDead)}");
                    if (yourUnits.Count > 0)
                        floorParts.Add($"your {string.Join(", ", yourUnits)}");

                    parts.Add($"{RoomIndexToFloorName(roomIndex)}: {string.Join("; ", floorParts)}");
                }

                // Deaths whose floor couldn't be pinned down (e.g. on the pyre).
                var enemyDeadElsewhere = _turnSummary.EnemyDeathsOffFloor();
                if (enemyDeadElsewhere.Count > 0)
                    parts.Add($"Enemies defeated: {string.Join(", ", enemyDeadElsewhere)}");
                var yourDeadElsewhere = _turnSummary.YourDeathsOffFloor();
                if (yourDeadElsewhere.Count > 0)
                    parts.Add($"Your units lost: {string.Join(", ", yourDeadElsewhere)}");

                // Pyre damage stays gated by the Damage announcement toggle.
                if (MonsterTrainAccessibility.AccessibilitySettings.AnnounceDamage.Value && _turnSummary.PyreTouched)
                    parts.Add($"Pyre took {_turnSummary.PyreDamageTotal} damage, {_turnSummary.PyreRemaining} remaining");

                if (parts.Count > 0)
                {
                    string text = "Combat summary. " + string.Join(". ", parts) + ".";
                    MonsterTrainAccessibility.ScreenReader?.Speak(text, false);
                    MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(text);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in AnnounceCombatSummary: {ex.Message}");
            }
            finally
            {
                _turnSummary.Reset();
            }
        }

        #endregion
    }
}
