using System.Collections.Generic;
using MonsterTrainAccessibility.Utilities;
using UnityEngine;

namespace MonsterTrainAccessibility.Core.Buffers
{
    /// <summary>
    /// What kind of element the focus announcement described, reported by
    /// MenuAccessibility's reader chain so the matching detail buffer fills
    /// without running the readers a second time.
    /// </summary>
    public enum FocusDomain
    {
        None,
        Card,
        Artifact
    }

    /// <summary>
    /// Contextual buffers that follow the focused element, modeled on the
    /// Monster Train 2 accessibility mod:
    ///   UI       - text and details for the currently focused UI element
    ///   Card     - full details for the focused card
    ///   Creature - full details for the unit being targeted in battle
    ///   Artifact - full details for the focused artifact
    ///   Reward   - details for the focused reward element
    ///   Story    - the current story event's narrative text
    /// Each buffer only becomes available when focus lands on a matching
    /// element; moving focus chooses what information the buffers hold.
    /// </summary>
    internal static class FocusBuffers
    {
        private static List<string> _uiItems;
        private static List<string> _cardItems;
        private static List<string> _creatureItems;
        private static List<string> _artifactItems;
        private static List<string> _rewardItems;
        private static List<string> _storyItems;

        /// <summary>
        /// Register the contextual buffers in the MT2 cycle order:
        /// UI, Events, Card, Creature, Artifact, Reward, then Story.
        /// </summary>
        public static void Register(BufferManager buffers)
        {
            buffers.Register("UI", () => _uiItems);
            buffers.Register(buffers.Events);
            buffers.Register("Card", () => _cardItems);
            buffers.Register("Creature", () => _creatureItems);
            buffers.Register("Artifact", () => _artifactItems);
            buffers.Register("Reward", () => _rewardItems);
            buffers.Register("Story", () => _storyItems);
        }

        /// <summary>
        /// Called by MenuAccessibility whenever a focused element is announced.
        /// Fills the UI buffer with the element's detail items - the reader's
        /// full readout when it provided one, otherwise the announced text
        /// split up - and the matching domain buffer (Card, Artifact, Reward);
        /// the others become unavailable.
        /// </summary>
        public static void OnFocusAnnounced(GameObject focused, string announcedText, FocusDomain domain,
            List<string> detailItems = null)
        {
            _uiItems = detailItems != null && detailItems.Count > 0
                ? CleanItems(detailItems)
                : SplitIntoItems(announcedText);
            _cardItems = domain == FocusDomain.Card ? _uiItems : null;
            _artifactItems = domain == FocusDomain.Artifact ? _uiItems : null;
            _rewardItems = IsRewardElement(focused) ? _uiItems : null;
        }

        /// <summary>
        /// Called by StoryEventScreenPatch with each captured narrative chunk
        /// so story text stays reviewable while the event screen is open.
        /// </summary>
        public static void SetStory(string narrative)
        {
            _storyItems = SplitIntoItems(TextUtilities.CleanSpriteTagsForSpeech(narrative ?? ""));
        }

        /// <summary>
        /// Called by the targeting patches when a battle unit is selected.
        /// </summary>
        public static void SetCreature(string details)
        {
            _creatureItems = SplitIntoItems(details);
        }

        public static void ClearCreature()
        {
            _creatureItems = null;
        }

        /// <summary>
        /// Drop all contextual content, e.g. on screen transitions.
        /// </summary>
        public static void Clear()
        {
            _uiItems = null;
            _cardItems = null;
            _creatureItems = null;
            _artifactItems = null;
            _rewardItems = null;
            _storyItems = null;
        }

        /// <summary>
        /// Reward screen elements carry Reward-named components (e.g. RewardItemUI),
        /// either on themselves or on an ancestor.
        /// </summary>
        private static bool IsRewardElement(GameObject focused)
        {
            if (focused == null)
                return false;

            try
            {
                foreach (var component in focused.GetComponentsInParent<Component>(true))
                {
                    if (component != null && component.GetType().Name.Contains("Reward"))
                        return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Split text into reviewable chunks, or null when there is nothing
        /// to review (null marks the buffer unavailable).
        /// </summary>
        private static List<string> SplitIntoItems(string text)
        {
            var items = TextUtilities.SplitIntoSpeechItems(text);
            return items.Count > 0 ? items : null;
        }

        /// <summary>
        /// Clean reader-provided detail items for speech, keeping the reader's
        /// item granularity (e.g. one item per keyword explanation).
        /// </summary>
        private static List<string> CleanItems(List<string> items)
        {
            var result = new List<string>();
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item))
                    continue;
                string cleaned = TextUtilities.CleanSpriteTagsForSpeech(item).Trim();
                if (cleaned.Length > 0)
                    result.Add(cleaned);
            }
            return result.Count > 0 ? result : null;
        }
    }
}
