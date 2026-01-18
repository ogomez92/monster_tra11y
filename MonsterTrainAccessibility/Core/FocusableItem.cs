using System;

namespace MonsterTrainAccessibility.Core
{
    /// <summary>
    /// Base class for all focusable items in the accessibility system.
    /// Represents any game element that can receive keyboard focus.
    /// </summary>
    public abstract class FocusableItem
    {
        /// <summary>
        /// Unique identifier for this item
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Index in a linear list
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Row position for grid navigation
        /// </summary>
        public int Row { get; set; }

        /// <summary>
        /// Column position for grid navigation
        /// </summary>
        public int Column { get; set; }

        /// <summary>
        /// Whether this item can currently receive focus
        /// </summary>
        public virtual bool CanFocus => true;

        /// <summary>
        /// Get the full accessible description for this item.
        /// This is what gets announced when the item receives focus.
        /// </summary>
        public abstract string GetAccessibleDescription();

        /// <summary>
        /// Get a brief label (for position announcements like "Item 2 of 5")
        /// </summary>
        public abstract string GetBriefLabel();

        /// <summary>
        /// Perform the default action when Enter is pressed
        /// </summary>
        public abstract void Activate();

        /// <summary>
        /// Optional secondary action (e.g., for right-click equivalent)
        /// </summary>
        public virtual void SecondaryAction()
        {
            // Override in subclasses if needed
        }

        /// <summary>
        /// Get additional details that can be spoken on demand
        /// </summary>
        public virtual string GetDetailedDescription()
        {
            return GetAccessibleDescription();
        }
    }

    /// <summary>
    /// A simple focusable item for menu buttons and similar elements
    /// </summary>
    public class FocusableMenuItem : FocusableItem
    {
        public string Label { get; set; }
        public string Description { get; set; }
        public Action OnActivate { get; set; }

        public override string GetAccessibleDescription()
        {
            if (!string.IsNullOrEmpty(Description))
            {
                return $"{Label}. {Description}";
            }
            return Label;
        }

        public override string GetBriefLabel()
        {
            return Label;
        }

        public override void Activate()
        {
            OnActivate?.Invoke();
        }
    }

    /// <summary>
    /// Focusable item wrapping a game card
    /// </summary>
    public class FocusableCard : FocusableItem
    {
        public object CardState { get; set; } // Will be CardState from game
        public string CardName { get; set; }
        public int Cost { get; set; }
        public string CardType { get; set; }
        public string BodyText { get; set; }
        public bool IsPlayable { get; set; }
        public Action OnPlay { get; set; }

        public override bool CanFocus => true;

        public override string GetAccessibleDescription()
        {
            string playable = IsPlayable ? "" : " (cannot play)";
            return $"{CardName}, {Cost} ember, {CardType}. {BodyText}{playable}";
        }

        public override string GetBriefLabel()
        {
            return $"{CardName}, {Cost} ember";
        }

        public override void Activate()
        {
            if (IsPlayable)
            {
                OnPlay?.Invoke();
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Cannot play this card");
            }
        }
    }

    /// <summary>
    /// Focusable item wrapping a game unit (monster or enemy)
    /// </summary>
    public class FocusableUnit : FocusableItem
    {
        public object CharacterState { get; set; } // Will be CharacterState from game
        public string UnitName { get; set; }
        public int Attack { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int Size { get; set; }
        public string StatusEffects { get; set; }
        public string Intent { get; set; } // For enemies
        public bool IsEnemy { get; set; }
        public Action OnSelect { get; set; }

        public override string GetAccessibleDescription()
        {
            string healthInfo = $"{Health} of {MaxHealth} health";
            string baseInfo = $"{UnitName}, {Attack} attack, {healthInfo}, Size {Size}";

            if (!string.IsNullOrEmpty(StatusEffects))
            {
                baseInfo += $". Status: {StatusEffects}";
            }

            if (IsEnemy && !string.IsNullOrEmpty(Intent))
            {
                baseInfo += $". Intent: {Intent}";
            }

            return baseInfo;
        }

        public override string GetBriefLabel()
        {
            return $"{UnitName} {Attack}/{Health}";
        }

        public override void Activate()
        {
            OnSelect?.Invoke();
        }
    }

    /// <summary>
    /// Focusable item wrapping a tower floor
    /// </summary>
    public class FocusableFloor : FocusableItem
    {
        public object RoomState { get; set; } // Will be RoomState from game
        public int FloorNumber { get; set; } // 1-3 (displayed), 0-2 internal
        public int UsedCapacity { get; set; }
        public int MaxCapacity { get; set; }
        public string FriendlyUnits { get; set; }
        public string EnemyUnits { get; set; }
        public Action OnSelect { get; set; }

        public override string GetAccessibleDescription()
        {
            string capacity = $"{UsedCapacity} of {MaxCapacity} capacity";
            string result = $"Floor {FloorNumber}, {capacity}";

            if (!string.IsNullOrEmpty(FriendlyUnits))
            {
                result += $". Your units: {FriendlyUnits}";
            }

            if (!string.IsNullOrEmpty(EnemyUnits))
            {
                result += $". Enemies: {EnemyUnits}";
            }

            if (string.IsNullOrEmpty(FriendlyUnits) && string.IsNullOrEmpty(EnemyUnits))
            {
                result += ". Empty";
            }

            return result;
        }

        public override string GetBriefLabel()
        {
            return $"Floor {FloorNumber}";
        }

        public override void Activate()
        {
            OnSelect?.Invoke();
        }
    }

    /// <summary>
    /// Focusable item wrapping a map node
    /// </summary>
    public class FocusableMapNode : FocusableItem
    {
        public object MapNode { get; set; }
        public string NodeType { get; set; } // Battle, Shop, Event, Boss, etc.
        public string Rewards { get; set; }
        public string Description { get; set; }
        public Action OnSelect { get; set; }

        public override string GetAccessibleDescription()
        {
            string result = NodeType;

            if (!string.IsNullOrEmpty(Rewards))
            {
                result += $". Rewards: {Rewards}";
            }

            if (!string.IsNullOrEmpty(Description))
            {
                result += $". {Description}";
            }

            return result;
        }

        public override string GetBriefLabel()
        {
            return NodeType;
        }

        public override void Activate()
        {
            OnSelect?.Invoke();
        }
    }
}
