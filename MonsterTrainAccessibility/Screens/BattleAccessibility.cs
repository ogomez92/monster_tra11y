using MonsterTrainAccessibility.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Handles accessibility for the battle/combat screen.
    /// Manages navigation between hand, floors, targeting, and unit placement.
    /// </summary>
    public class BattleAccessibility
    {
        /// <summary>
        /// Current battle focus mode
        /// </summary>
        public enum BattleFocusMode
        {
            Hand,           // Browsing cards in hand
            Floors,         // Browsing tower floors
            Targeting,      // Selecting target for a spell
            UnitPlacement   // Selecting floor to place a monster
        }

        public BattleFocusMode CurrentMode { get; private set; } = BattleFocusMode.Hand;
        public bool IsInBattle { get; private set; }

        // Focus contexts for different modes
        private GridFocusContext _handContext;
        private ListFocusContext _floorContext;
        private ListFocusContext _targetContext;

        // Current battle state (would be populated from game)
        private List<CardInfo> _currentHand = new List<CardInfo>();
        private List<FloorInfo> _floors = new List<FloorInfo>();
        private CardInfo _selectedCard;

        // Game state references (would be actual game manager references)
        private int _currentEmber;
        private int _maxEmber;
        private int _pyreHealth;
        private int _maxPyreHealth;

        public BattleAccessibility()
        {
            // Initialize floor info (Monster Train has 3 floors)
            for (int i = 0; i < 3; i++)
            {
                _floors.Add(new FloorInfo { FloorNumber = i + 1 });
            }
        }

        #region Battle Lifecycle

        /// <summary>
        /// Called when combat begins
        /// </summary>
        public void OnBattleEntered()
        {
            IsInBattle = true;
            CurrentMode = BattleFocusMode.Hand;

            MonsterTrainAccessibility.LogInfo("Battle entered");

            // Create hand context
            _handContext = new GridFocusContext("Hand", 5, OnHandBack);

            MonsterTrainAccessibility.FocusManager.SetContext(_handContext, false);

            // Announce battle start
            AnnounceNewBattle();
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
            _currentEmber = ember;
            _maxEmber = maxEmber;

            // Return to hand mode
            CurrentMode = BattleFocusMode.Hand;
            RefreshHand();

            MonsterTrainAccessibility.FocusManager.ReplaceContext(_handContext);

            var output = MonsterTrainAccessibility.ScreenReader;
            output?.Speak("Your turn", true);
            output?.Queue($"{ember} of {maxEmber} ember");

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
        /// Called when all enemies on current wave are defeated
        /// </summary>
        public void OnWaveComplete()
        {
            MonsterTrainAccessibility.ScreenReader?.Speak("Wave complete!", true);
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

        #region Hand Management

        /// <summary>
        /// Refresh hand context from current game state
        /// </summary>
        public void RefreshHand()
        {
            var items = _currentHand.Select((card, i) => new FocusableCard
            {
                Id = card.Id,
                Index = i,
                CardName = card.Name,
                Cost = card.Cost,
                CardType = card.Type,
                BodyText = card.BodyText,
                IsPlayable = card.Cost <= _currentEmber,
                CardState = card.GameCardState,
                OnPlay = () => StartPlayingCard(card)
            }).Cast<FocusableItem>().ToList();

            _handContext.SetItems(items);
        }

        /// <summary>
        /// Update the hand with new cards
        /// </summary>
        public void UpdateHand(List<CardInfo> cards)
        {
            _currentHand = cards;
            RefreshHand();
        }

        /// <summary>
        /// Announce all cards in hand
        /// </summary>
        public void AnnounceHand()
        {
            if (_currentHand.Count == 0)
            {
                MonsterTrainAccessibility.ScreenReader?.Speak("Hand is empty", true);
                return;
            }

            var sb = new StringBuilder();
            sb.Append($"Hand contains {_currentHand.Count} cards. ");

            for (int i = 0; i < _currentHand.Count; i++)
            {
                var card = _currentHand[i];
                string playable = card.Cost <= _currentEmber ? "" : " (unplayable)";
                sb.Append($"{i + 1}: {card.Name}, {card.Cost} ember{playable}. ");
            }

            MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), true);
        }

        /// <summary>
        /// Select a card by index (for number key shortcuts)
        /// </summary>
        public void SelectCardByIndex(int index)
        {
            if (index >= 0 && index < _currentHand.Count)
            {
                MonsterTrainAccessibility.FocusManager.SetFocusByIndex(index);
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Speak($"No card at position {index + 1}", true);
            }
        }

        private void StartPlayingCard(CardInfo card)
        {
            _selectedCard = card;

            if (card.Cost > _currentEmber)
            {
                MonsterTrainAccessibility.ScreenReader?.Speak("Not enough ember to play this card", true);
                return;
            }

            if (card.IsMonster)
            {
                // Enter placement mode
                EnterPlacementMode();
            }
            else if (card.NeedsTarget)
            {
                // Enter targeting mode
                EnterTargetingMode();
            }
            else
            {
                // Targetless spell - play immediately
                PlayCard(card, -1, null);
            }
        }

        private void OnHandBack()
        {
            // Could show end turn confirmation or similar
            MonsterTrainAccessibility.ScreenReader?.Speak(
                "Press Enter to end turn, or continue playing cards", true);
        }

        #endregion

        #region Floor Navigation

        /// <summary>
        /// Enter floor selection mode
        /// </summary>
        public void EnterFloorMode()
        {
            CurrentMode = BattleFocusMode.Floors;

            _floorContext = new ListFocusContext("Tower Floors", ExitFloorMode);

            // Add floors (top to bottom, as that's how they're displayed)
            for (int i = 2; i >= 0; i--)
            {
                var floor = _floors[i];
                _floorContext.AddItem(new FocusableFloor
                {
                    Id = $"floor_{floor.FloorNumber}",
                    FloorNumber = floor.FloorNumber,
                    UsedCapacity = floor.UsedCapacity,
                    MaxCapacity = floor.MaxCapacity,
                    FriendlyUnits = floor.FriendlyUnitsSummary,
                    EnemyUnits = floor.EnemyUnitsSummary,
                    RoomState = floor.GameRoomState,
                    OnSelect = () => SelectFloor(floor)
                });
            }

            MonsterTrainAccessibility.FocusManager.SetContext(_floorContext);
        }

        private void ExitFloorMode()
        {
            CurrentMode = BattleFocusMode.Hand;
            MonsterTrainAccessibility.FocusManager.GoBack();
        }

        /// <summary>
        /// Announce all floors
        /// </summary>
        public void AnnounceAllFloors()
        {
            var output = MonsterTrainAccessibility.ScreenReader;
            output?.Speak("Tower status:", true);

            // Announce from top to bottom
            for (int i = 2; i >= 0; i--)
            {
                var floor = _floors[i];
                string floorDesc = GetFloorDescription(floor);
                output?.Queue(floorDesc);
            }

            // Announce pyre
            output?.Queue($"Pyre: {_pyreHealth} of {_maxPyreHealth} health");
        }

        /// <summary>
        /// Update floor information
        /// </summary>
        public void UpdateFloor(int floorIndex, FloorInfo info)
        {
            if (floorIndex >= 0 && floorIndex < _floors.Count)
            {
                _floors[floorIndex] = info;
            }
        }

        private string GetFloorDescription(FloorInfo floor)
        {
            var sb = new StringBuilder();
            sb.Append($"Floor {floor.FloorNumber}, {floor.UsedCapacity} of {floor.MaxCapacity} capacity");

            if (!string.IsNullOrEmpty(floor.FriendlyUnitsSummary))
            {
                sb.Append($". Your units: {floor.FriendlyUnitsSummary}");
            }

            if (!string.IsNullOrEmpty(floor.EnemyUnitsSummary))
            {
                sb.Append($". Enemies: {floor.EnemyUnitsSummary}");
            }

            if (string.IsNullOrEmpty(floor.FriendlyUnitsSummary) &&
                string.IsNullOrEmpty(floor.EnemyUnitsSummary))
            {
                sb.Append(". Empty");
            }

            return sb.ToString();
        }

        private void SelectFloor(FloorInfo floor)
        {
            MonsterTrainAccessibility.ScreenReader?.Speak($"Selected floor {floor.FloorNumber}", true);

            if (CurrentMode == BattleFocusMode.UnitPlacement && _selectedCard != null)
            {
                // Place the unit on this floor
                PlayCard(_selectedCard, floor.FloorNumber - 1, null);
                _selectedCard = null;
                ExitPlacementMode();
            }
        }

        #endregion

        #region Targeting Mode

        /// <summary>
        /// Enter targeting mode for spells
        /// </summary>
        private void EnterTargetingMode()
        {
            CurrentMode = BattleFocusMode.Targeting;

            _targetContext = new ListFocusContext("Select Target", ExitTargetingMode);

            // Populate with valid targets based on the selected card
            // This would be populated from actual game targeting system
            var targets = GetValidTargets(_selectedCard);

            foreach (var target in targets)
            {
                _targetContext.AddItem(new FocusableUnit
                {
                    Id = target.Id,
                    UnitName = target.Name,
                    Attack = target.Attack,
                    Health = target.Health,
                    MaxHealth = target.MaxHealth,
                    Size = target.Size,
                    IsEnemy = target.IsEnemy,
                    Intent = target.Intent,
                    CharacterState = target.GameCharacterState,
                    OnSelect = () => SelectTarget(target)
                });
            }

            MonsterTrainAccessibility.FocusManager.SetContext(_targetContext);
            MonsterTrainAccessibility.ScreenReader?.Queue(
                "Select target. Use arrows to browse, Enter to confirm, Escape to cancel.");
        }

        private void ExitTargetingMode()
        {
            _selectedCard = null;
            CurrentMode = BattleFocusMode.Hand;
            MonsterTrainAccessibility.FocusManager.GoBack();
        }

        private void SelectTarget(UnitInfo target)
        {
            if (_selectedCard != null)
            {
                PlayCard(_selectedCard, -1, target);
                _selectedCard = null;
            }
            ExitTargetingMode();
        }

        private List<UnitInfo> GetValidTargets(CardInfo card)
        {
            // This would query the game's targeting system
            // For now, return all units as potential targets
            var targets = new List<UnitInfo>();

            foreach (var floor in _floors)
            {
                targets.AddRange(floor.FriendlyUnits);
                targets.AddRange(floor.EnemyUnits);
            }

            return targets;
        }

        #endregion

        #region Unit Placement Mode

        /// <summary>
        /// Enter placement mode for monster cards
        /// </summary>
        private void EnterPlacementMode()
        {
            CurrentMode = BattleFocusMode.UnitPlacement;

            _floorContext = new ListFocusContext("Select Floor for Unit", ExitPlacementMode);

            // Add floors that have capacity
            for (int i = 2; i >= 0; i--)
            {
                var floor = _floors[i];
                int remainingCapacity = floor.MaxCapacity - floor.UsedCapacity;
                bool hasSpace = remainingCapacity >= (_selectedCard?.Size ?? 1);

                _floorContext.AddItem(new FocusableFloor
                {
                    Id = $"floor_{floor.FloorNumber}",
                    FloorNumber = floor.FloorNumber,
                    UsedCapacity = floor.UsedCapacity,
                    MaxCapacity = floor.MaxCapacity,
                    FriendlyUnits = floor.FriendlyUnitsSummary,
                    EnemyUnits = floor.EnemyUnitsSummary,
                    RoomState = floor.GameRoomState,
                    OnSelect = hasSpace ? (Action)(() => SelectFloor(floor)) : null
                });
            }

            MonsterTrainAccessibility.FocusManager.SetContext(_floorContext);
            MonsterTrainAccessibility.ScreenReader?.Queue(
                $"Place {_selectedCard?.Name}. Select floor. Use Up/Down, Enter to confirm, Escape to cancel.");
        }

        private void ExitPlacementMode()
        {
            _selectedCard = null;
            CurrentMode = BattleFocusMode.Hand;
            MonsterTrainAccessibility.FocusManager.GoBack();
        }

        #endregion

        #region Combat Events

        /// <summary>
        /// Called when a card is played
        /// </summary>
        private void PlayCard(CardInfo card, int floorIndex, UnitInfo target)
        {
            string announcement = $"Played {card.Name}";

            if (floorIndex >= 0)
            {
                announcement += $" on floor {floorIndex + 1}";
            }

            if (target != null)
            {
                announcement += $" targeting {target.Name}";
            }

            MonsterTrainAccessibility.ScreenReader?.Speak(announcement, true);

            // Update ember
            _currentEmber -= card.Cost;

            // Actual card play would be triggered through game API
        }

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
        /// Announce cards drawn
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

        #endregion

        #region Resource Announcements

        /// <summary>
        /// Announce current resources
        /// </summary>
        public void AnnounceResources()
        {
            var sb = new StringBuilder();
            sb.Append($"Ember: {_currentEmber} of {_maxEmber}. ");
            sb.Append($"Pyre health: {_pyreHealth} of {_maxPyreHealth}. ");
            sb.Append($"Cards in hand: {_currentHand.Count}.");

            MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), true);
        }

        /// <summary>
        /// Update resource values
        /// </summary>
        public void UpdateResources(int ember, int maxEmber, int pyreHealth, int maxPyreHealth)
        {
            _currentEmber = ember;
            _maxEmber = maxEmber;
            _pyreHealth = pyreHealth;
            _maxPyreHealth = maxPyreHealth;
        }

        /// <summary>
        /// Announce enemies and their intents
        /// </summary>
        public void AnnounceEnemies()
        {
            var output = MonsterTrainAccessibility.ScreenReader;
            output?.Speak("Enemy summary:", true);

            bool hasEnemies = false;

            for (int i = 2; i >= 0; i--)
            {
                var floor = _floors[i];
                if (floor.EnemyUnits.Count > 0)
                {
                    hasEnemies = true;
                    var enemyDescs = floor.EnemyUnits.Select(e =>
                        $"{e.Name} {e.Attack}/{e.Health}, {e.Intent}");
                    output?.Queue($"Floor {floor.FloorNumber}: {string.Join(", ", enemyDescs)}");
                }
            }

            if (!hasEnemies)
            {
                output?.Queue("No enemies on the tower");
            }
        }

        private void AnnounceNewBattle()
        {
            var output = MonsterTrainAccessibility.ScreenReader;

            output?.AnnounceScreen("Battle started");
            output?.Queue($"Ember: {_currentEmber} of {_maxEmber}");
            output?.Queue($"Cards in hand: {_currentHand.Count}");

            // Summarize enemies
            int totalEnemies = _floors.Sum(f => f.EnemyUnits.Count);
            if (totalEnemies > 0)
            {
                output?.Queue($"{totalEnemies} enemies approaching");
            }
        }

        #endregion
    }

    #region Data Classes

    /// <summary>
    /// Information about a card
    /// </summary>
    public class CardInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Cost { get; set; }
        public string Type { get; set; }
        public string BodyText { get; set; }
        public bool IsMonster { get; set; }
        public bool NeedsTarget { get; set; }
        public int Size { get; set; } = 1;
        public object GameCardState { get; set; } // Actual CardState from game
    }

    /// <summary>
    /// Information about a tower floor
    /// </summary>
    public class FloorInfo
    {
        public int FloorNumber { get; set; }
        public int UsedCapacity { get; set; }
        public int MaxCapacity { get; set; } = 7;
        public List<UnitInfo> FriendlyUnits { get; set; } = new List<UnitInfo>();
        public List<UnitInfo> EnemyUnits { get; set; } = new List<UnitInfo>();
        public object GameRoomState { get; set; } // Actual RoomState from game

        public string FriendlyUnitsSummary =>
            FriendlyUnits.Count > 0
                ? string.Join(", ", FriendlyUnits.Select(u => $"{u.Name} {u.Attack}/{u.Health}"))
                : null;

        public string EnemyUnitsSummary =>
            EnemyUnits.Count > 0
                ? string.Join(", ", EnemyUnits.Select(u => $"{u.Name} {u.Attack}/{u.Health}"))
                : null;
    }

    /// <summary>
    /// Information about a unit (monster or enemy)
    /// </summary>
    public class UnitInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Attack { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int Size { get; set; }
        public bool IsEnemy { get; set; }
        public string Intent { get; set; } // For enemies: "will attack for 15"
        public string StatusEffects { get; set; }
        public object GameCharacterState { get; set; } // Actual CharacterState from game
    }

    #endregion
}
