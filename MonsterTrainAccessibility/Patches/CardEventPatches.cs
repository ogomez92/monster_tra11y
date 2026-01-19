using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Patches for card-related events like drawing, playing, and discarding cards.
    /// </summary>

    /// <summary>
    /// Detect cards drawn - DrawCards is a void method, so we use a simple postfix
    /// </summary>
    public static class CardDrawPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var cardManagerType = AccessTools.TypeByName("CardManager");
                if (cardManagerType != null)
                {
                    var method = AccessTools.Method(cardManagerType, "DrawCards");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(CardDrawPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched CardManager.DrawCards");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch DrawCards: {ex.Message}");
            }
        }

        // DrawCards is void, so we just get notified that cards were drawn
        // The cardCount parameter tells us how many cards were requested
        public static void Postfix(int cardCount)
        {
            try
            {
                if (cardCount > 0)
                {
                    // Just notify that drawing happened - the hand will be refreshed
                    MonsterTrainAccessibility.BattleHandler?.OnCardsDrawn(cardCount);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in draw cards patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect card played
    /// PlayCard signature: PlayCard(int cardIndex, SpawnPoint dropLocation, CardSelectionBehaviour+SelectionError& lastSelectionError)
    /// </summary>
    public static class CardPlayedPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var cardManagerType = AccessTools.TypeByName("CardManager");
                if (cardManagerType != null)
                {
                    var method = AccessTools.Method(cardManagerType, "PlayCard");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(CardPlayedPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched CardManager.PlayCard");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch PlayCard: {ex.Message}");
            }
        }

        // The method takes cardIndex, not a card object. We just get notified a card was played.
        public static void Postfix(int cardIndex, bool __result)
        {
            try
            {
                // __result indicates if the card was successfully played
                if (__result)
                {
                    MonsterTrainAccessibility.BattleHandler?.OnCardPlayed(cardIndex);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in play card patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect card discarded
    /// DiscardCard signature: DiscardCard(CardManager+DiscardCardParams discardCardParams, bool fromNaturalPlay)
    /// Returns IEnumerator (coroutine)
    /// </summary>
    public static class CardDiscardedPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var cardManagerType = AccessTools.TypeByName("CardManager");
                if (cardManagerType != null)
                {
                    var method = AccessTools.Method(cardManagerType, "DiscardCard");
                    if (method != null)
                    {
                        // Use prefix since we want to see the params before the coroutine starts
                        var prefix = new HarmonyMethod(typeof(CardDiscardedPatch).GetMethod(nameof(Prefix)));
                        harmony.Patch(method, prefix: prefix);
                        MonsterTrainAccessibility.LogInfo("Patched CardManager.DiscardCard");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch DiscardCard: {ex.Message}");
            }
        }

        // Use prefix to capture the discard params before the coroutine runs
        public static void Prefix(object discardCardParams)
        {
            try
            {
                if (discardCardParams == null) return;

                // Try to get the card from DiscardCardParams
                var paramsType = discardCardParams.GetType();
                var cardField = paramsType.GetField("discardCard") ??
                               paramsType.GetField("card") ??
                               paramsType.GetField("_discardCard");

                if (cardField != null)
                {
                    var card = cardField.GetValue(discardCardParams);
                    if (card != null)
                    {
                        string cardName = GetCardName(card);
                        MonsterTrainAccessibility.BattleHandler?.OnCardDiscarded(cardName);
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in discard patch: {ex.Message}");
            }
        }

        private static string GetCardName(object cardState)
        {
            try
            {
                var getDataMethod = cardState.GetType().GetMethod("GetCardDataRead");
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(cardState, null);
                    if (data != null)
                    {
                        // Try GetName first (returns localized name)
                        var getNameMethod = data.GetType().GetMethod("GetName");
                        if (getNameMethod != null)
                        {
                            var name = getNameMethod.Invoke(data, null) as string;
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }
            }
            catch { }
            return "Card";
        }
    }

    /// <summary>
    /// Detect deck shuffled
    /// </summary>
    public static class DeckShuffledPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var cardManagerType = AccessTools.TypeByName("CardManager");
                if (cardManagerType != null)
                {
                    var method = AccessTools.Method(cardManagerType, "ShuffleDeck") ??
                                 AccessTools.Method(cardManagerType, "Shuffle");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(DeckShuffledPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched deck shuffle");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch shuffle: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Deck shuffled");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in shuffle patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect hand changed (for refreshing accessible hand info)
    /// </summary>
    public static class HandChangedPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var cardManagerType = AccessTools.TypeByName("CardManager");
                if (cardManagerType != null)
                {
                    // Try to find a method that fires when hand contents change
                    var method = AccessTools.Method(cardManagerType, "OnHandChanged") ??
                                 AccessTools.Method(cardManagerType, "UpdateHand");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(HandChangedPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched hand change: {method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch hand change: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                // Trigger refresh of the hand context if in battle
                MonsterTrainAccessibility.BattleHandler?.RefreshHand();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in hand change patch: {ex.Message}");
            }
        }
    }
}
