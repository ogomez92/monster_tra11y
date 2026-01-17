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
    /// Detect cards drawn
    /// </summary>
    [HarmonyPatch]
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

        public static void Postfix(object __result)
        {
            try
            {
                if (__result == null) return;

                // __result is typically List<CardState>
                var cardsList = __result as System.Collections.IList;
                if (cardsList != null && cardsList.Count > 0)
                {
                    var cardNames = new List<string>();

                    foreach (var card in cardsList)
                    {
                        string name = GetCardName(card);
                        if (!string.IsNullOrEmpty(name))
                        {
                            cardNames.Add(name);
                        }
                    }

                    if (cardNames.Count > 0)
                    {
                        MonsterTrainAccessibility.BattleHandler?.OnCardsDrawn(cardNames);
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in draw cards patch: {ex.Message}");
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
                    var getNameMethod = data?.GetType().GetMethod("GetNameKey");
                    if (getNameMethod != null)
                    {
                        return getNameMethod.Invoke(data, null) as string ?? "Card";
                    }
                }
            }
            catch { }
            return "Card";
        }
    }

    /// <summary>
    /// Detect card played
    /// </summary>
    [HarmonyPatch]
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

        public static void Postfix(object card)
        {
            try
            {
                if (card == null) return;

                string cardName = GetCardName(card);
                MonsterTrainAccessibility.ScreenReader?.Speak($"Played {cardName}", true);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in play card patch: {ex.Message}");
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
                    var getNameMethod = data?.GetType().GetMethod("GetNameKey");
                    if (getNameMethod != null)
                    {
                        return getNameMethod.Invoke(data, null) as string ?? "Card";
                    }
                }
            }
            catch { }
            return "Card";
        }
    }

    /// <summary>
    /// Detect card discarded
    /// </summary>
    [HarmonyPatch]
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
                        var postfix = new HarmonyMethod(typeof(CardDiscardedPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched CardManager.DiscardCard");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch DiscardCard: {ex.Message}");
            }
        }

        public static void Postfix(object card)
        {
            try
            {
                if (card == null) return;

                string cardName = GetCardName(card);
                MonsterTrainAccessibility.ScreenReader?.Queue($"Discarded {cardName}");
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
                    var getNameMethod = data?.GetType().GetMethod("GetNameKey");
                    if (getNameMethod != null)
                    {
                        return getNameMethod.Invoke(data, null) as string ?? "Card";
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
    [HarmonyPatch]
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
    [HarmonyPatch]
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
