using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Reads resource information (ember, gold, pyre health, crystals, waves) from battle state.
    /// </summary>
    internal static class ResourceReader
    {
        /// <summary>
        /// Announce current resources
        /// </summary>
        internal static void AnnounceResources(BattleManagerCache cache)
        {
            try
            {
                var items = GetResourceStrings(cache);
                if (items == null || items.Count == 0)
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak("Could not read resources", false);
                    return;
                }

                MonsterTrainAccessibility.ScreenReader?.Speak(string.Join(" ", items), false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error announcing resources: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read resources", false);
            }
        }

        /// <summary>
        /// Build one announcement string per resource. Used by AnnounceResources
        /// and the Resources review buffer.
        /// </summary>
        internal static List<string> GetResourceStrings(BattleManagerCache cache)
        {
            var items = new List<string>();

            int energy = GetCurrentEnergy(cache);
            if (energy >= 0)
            {
                items.Add($"Ember: {energy}.");
            }

            int gold = GetGold(cache);
            if (gold >= 0)
            {
                items.Add($"Gold: {gold}.");
            }

            int pyreHP = GetPyreHealth(cache);
            int maxPyreHP = GetMaxPyreHealth(cache);
            if (pyreHP >= 0)
            {
                items.Add($"Pyre: {pyreHP} of {maxPyreHP}.");
            }

            // Pyre armor and attack
            int pyreArmor = GetPyreStatusEffect(cache, "armor");
            if (pyreArmor > 0)
            {
                items.Add($"Pyre Armor: {pyreArmor}.");
            }

            int pyreAttack = GetPyreStatusEffect(cache, "attack");
            if (pyreAttack > 0)
            {
                items.Add($"Pyre Attack: {pyreAttack}.");
            }

            var hand = HandReader.GetHandCards(cache);
            if (hand != null)
            {
                items.Add($"Cards in hand: {hand.Count}.");
            }

            // Crystals and threat level (DLC)
            if (cache.SaveManager == null)
            {
                cache.FindManagers();
            }
            string crystalInfo = GetCrystalAndThreatInfo(cache.SaveManager);
            if (!string.IsNullOrEmpty(crystalInfo))
            {
                items.Add($"{crystalInfo}.");
            }

            // Wave counter
            string waveInfo = GetWaveInfo(cache);
            if (!string.IsNullOrEmpty(waveInfo))
            {
                items.Add(waveInfo);
            }

            return items;
        }

        /// <summary>
        /// Get crystal count and threat level info (Last Divinity DLC).
        /// Returns null if DLC is not active. Works from any screen, so the
        /// Ctrl+R pact shard hotkey can use it outside battle too.
        /// </summary>
        internal static string GetCrystalAndThreatInfo(object saveManager, bool includeDescription = false)
        {
            try
            {
                if (saveManager == null) return null;

                var saveType = saveManager.GetType();

                // Check if DLC crystals are shown (ShowPactCrystals)
                var showMethod = saveType.GetMethod("ShowPactCrystals", Type.EmptyTypes);
                if (showMethod != null)
                {
                    bool show = (bool)showMethod.Invoke(saveManager, null);
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
                        // The DLC enum is namespaced (ShinyShoe.DLC), so a bare
                        // assembly GetType("DLC") lookup never finds it - take
                        // the enum type from the method's own parameter instead
                        var dlcType = genericMethod.GetParameters()[0].ParameterType;
                        if (dlcType.IsEnum)
                        {
                            var hellforgedValue = Enum.ToObject(dlcType, 1); // ShinyShoe.DLC.Hellforged = 1
                            var dlcSaveData = genericMethod.Invoke(saveManager, new object[] { hellforgedValue });
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
                        crystals = (int)getPactMethod.Invoke(saveManager, null);
                    }
                }

                if (crystals < 0) return null;

                int band = GetThreatBand(saveManager, crystals);
                string result = $"Pact shards: {crystals}. Threat: {GetThreatLevelName(band)}";

                if (includeDescription)
                {
                    string goal = GetLastDivinityRequirement(saveManager, crystals);
                    if (!string.IsNullOrEmpty(goal))
                        result += $". {goal}";

                    EnsureThreatTexts();
                    if (!string.IsNullOrEmpty(_threatBody))
                        result += $". {_threatBody}";
                }
                return result;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting crystal/threat info: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Threat band for the current ring: 0 none, 1 low, 2 moderate,
        /// 3 warning, 4 danger. Thresholds come from the game's
        /// HellforgedThreatLevel at the CURRENT distance - they vary by
        /// ring, so a fixed table can be wildly wrong.
        /// </summary>
        private static int GetThreatBand(object saveManager, int crystals)
        {
            if (crystals <= 0)
                return 0;

            int low = 10, warning = 50, danger = 80; // rough fallbacks
            try
            {
                var saveType = saveManager.GetType();
                int distance = saveType.GetMethod("GetCurrentDistance", Type.EmptyTypes)
                    ?.Invoke(saveManager, null) is int dist ? Math.Max(0, dist) : 0;
                var balanceData = saveType.GetMethod("GetBalanceData", Type.EmptyTypes)
                    ?.Invoke(saveManager, null);
                var threatData = balanceData?.GetType()
                    .GetMethod("GetHellforgedThreatLevelAtDistance")
                    ?.Invoke(balanceData, new object[] { distance });
                if (threatData != null)
                {
                    // The threshold fields are private; the properties are public
                    var threatType = threatData.GetType();
                    if (threatType.GetProperty("LowAmount")?.GetValue(threatData) is int l) low = l;
                    if (threatType.GetProperty("WarningAmount")?.GetValue(threatData) is int w) warning = w;
                    if (threatType.GetProperty("DangerAmount")?.GetValue(threatData) is int d) danger = d;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Threat thresholds unavailable, using defaults: {ex.Message}");
            }

            if (crystals >= danger) return 4;
            if (crystals >= warning) return 3;
            if (crystals >= low) return 2;
            return 1;
        }

        private static readonly string[] FallbackThreatNames = { "None", "Low", "Moderate", "Warning", "Danger" };

        private static string[] _threatNames; // the game's localized band names
        private static string _threatBody;    // the in-game threat explanation text

        private static string GetThreatLevelName(int band)
        {
            EnsureThreatTexts();
            var names = _threatNames ?? FallbackThreatNames;
            return names[Math.Max(0, Math.Min(band, names.Length - 1))];
        }

        /// <summary>
        /// Read the localized threat band names ("Extreme" etc.) and the
        /// tooltip explanation from a live HellforgedThreatLevelUI - its
        /// serialized localization key fields are the same ones the game's
        /// HUD uses. Retried until found (the HUD may not exist yet).
        /// </summary>
        private static void EnsureThreatTexts()
        {
            if (_threatNames != null)
                return;

            try
            {
                var uiType = Utilities.ReflectionHelper.FindType("HellforgedThreatLevelUI");
                if (uiType == null)
                    return;
                var instances = UnityEngine.Object.FindObjectsOfType(uiType);
                if (instances == null || instances.Length == 0)
                    return;
                var ui = instances[0];

                string Localize(string fieldName)
                {
                    var field = uiType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                    var key = field?.GetValue(ui) as string;
                    string text = string.IsNullOrEmpty(key) ? null : Core.KeywordManager.TryLocalize(key);
                    return string.IsNullOrEmpty(text) ? null : Utilities.TextUtilities.StripRichTextTags(text).Trim();
                }

                var names = new[]
                {
                    Localize("threatNoneKey"),
                    Localize("threatLowKey"),
                    Localize("threatModerateKey"),
                    Localize("threatWarningKey"),
                    Localize("threatDangerKey"),
                };
                if (Array.TrueForAll(names, n => !string.IsNullOrEmpty(n)))
                {
                    _threatBody = Localize("tooltipBodyKey");
                    _threatNames = names;
                    MonsterTrainAccessibility.LogInfo($"Threat names: {string.Join(", ", names)}");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Threat texts unavailable: {ex.Message}");
            }
        }

        /// <summary>
        /// The Last Divinity gate: the final ring's NodeState carries the
        /// pact shard count needed to face the true final boss (the /100 on
        /// the game's HUD meter).
        /// </summary>
        private static string GetLastDivinityRequirement(object saveManager, int crystals)
        {
            try
            {
                var saveType = saveManager.GetType();
                if (!(saveType.GetMethod("GetRunLength", Type.EmptyTypes)?.Invoke(saveManager, null) is int runLength) || runLength <= 0)
                    return null;
                var runState = saveType.GetMethod("GetRunState", Type.EmptyTypes)?.Invoke(saveManager, null);
                var node = runState?.GetType().GetMethod("GetNodeStateAtDistance")
                    ?.Invoke(runState, new object[] { runLength - 1 });
                if (node == null)
                    return null;

                var nodeType = node.GetType();
                bool requiresDlc = nodeType.GetField("RequiresHellforgedDlc")?.GetValue(node) is bool b && b;
                int required = nodeType.GetField("requiredCrystals")?.GetValue(node) is int r ? r : 0;
                if (!requiresDlc || required <= 0)
                    return null;

                return crystals >= required
                    ? $"{required} reached: The Last Divinity awaits beyond the final boss"
                    : $"{required} needed to face The Last Divinity";
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get wave info from the combat manager (current wave / total waves)
        /// </summary>
        private static string GetWaveInfo(BattleManagerCache cache)
        {
            try
            {
                if (cache.CombatManager == null) return null;

                var cmType = cache.CombatManager.GetType();
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Try GetCurrentWaveIndex or similar
                int currentWave = -1;
                int totalWaves = -1;

                var getCurrentWaveMethod = cmType.GetMethod("GetCurrentWaveIndex", Type.EmptyTypes) ??
                                           cmType.GetMethod("GetCurrentWave", Type.EmptyTypes);
                if (getCurrentWaveMethod != null)
                {
                    var result = getCurrentWaveMethod.Invoke(cache.CombatManager, null);
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
                        var val = waveField.GetValue(cache.CombatManager);
                        if (val is int w) currentWave = w;
                    }
                }

                // Get total waves
                var getTotalWavesMethod = cmType.GetMethod("GetNumWaves", Type.EmptyTypes) ??
                                          cmType.GetMethod("GetTotalWaves", Type.EmptyTypes) ??
                                          cmType.GetMethod("GetWaveCount", Type.EmptyTypes);
                if (getTotalWavesMethod != null)
                {
                    var result = getTotalWavesMethod.Invoke(cache.CombatManager, null);
                    if (result is int w) totalWaves = w;
                }

                if (totalWaves < 0)
                {
                    var wavesField = cmType.GetField("numWaves", bindingFlags) ??
                                     cmType.GetField("_numWaves", bindingFlags) ??
                                     cmType.GetField("totalWaves", bindingFlags);
                    if (wavesField != null)
                    {
                        var val = wavesField.GetValue(cache.CombatManager);
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
        private static int GetPyreStatusEffect(BattleManagerCache cache, string effectName)
        {
            try
            {
                var pyreRoom = FloorReader.GetRoom(cache, 3);
                if (pyreRoom == null) return 0;

                // Get pyre character from the room
                var units = FloorReader.GetUnitsInRoom(pyreRoom);
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
        internal static int GetStatusEffectStacks(object unit, string effectId)
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

        internal static int GetCurrentEnergy(BattleManagerCache cache)
        {
            if (cache.PlayerManager == null || cache.GetEnergyMethod == null)
            {
                cache.FindManagers();
            }

            try
            {
                var result = cache.GetEnergyMethod?.Invoke(cache.PlayerManager, null);
                if (result is int energy) return energy;
            }
            catch { }
            return -1;
        }

        internal static int GetPyreHealth(BattleManagerCache cache)
        {
            if (cache.SaveManager == null || cache.GetTowerHPMethod == null)
            {
                cache.FindManagers();
            }

            try
            {
                var result = cache.GetTowerHPMethod?.Invoke(cache.SaveManager, null);
                if (result is int hp) return hp;
            }
            catch { }
            return -1;
        }

        internal static int GetMaxPyreHealth(BattleManagerCache cache)
        {
            try
            {
                var result = cache.GetMaxTowerHPMethod?.Invoke(cache.SaveManager, null);
                if (result is int hp) return hp;
            }
            catch { }
            return -1;
        }

        /// <summary>
        /// Get the current deck size
        /// </summary>
        internal static int GetDeckSize(BattleManagerCache cache)
        {
            try
            {
                if (cache.CardManager == null)
                {
                    cache.FindManagers();
                }

                if (cache.CardManager != null)
                {
                    var cardManagerType = cache.CardManager.GetType();

                    // Try GetAllCards or GetDeck method
                    var getCardsMethod = cardManagerType.GetMethod("GetAllCards", Type.EmptyTypes)
                                      ?? cardManagerType.GetMethod("GetDeck", Type.EmptyTypes);
                    if (getCardsMethod != null)
                    {
                        var cards = getCardsMethod.Invoke(cache.CardManager, null);
                        if (cards is System.Collections.ICollection collection)
                        {
                            return collection.Count;
                        }
                    }

                    // Try GetDeckCount method
                    var getCountMethod = cardManagerType.GetMethod("GetDeckCount", Type.EmptyTypes);
                    if (getCountMethod != null)
                    {
                        var result = getCountMethod.Invoke(cache.CardManager, null);
                        if (result is int count) return count;
                    }
                }
            }
            catch { }
            return -1;
        }

        internal static int GetGold(BattleManagerCache cache)
        {
            if (cache.SaveManager == null || cache.GetGoldMethod == null)
            {
                cache.FindManagers();
            }

            try
            {
                var result = cache.GetGoldMethod?.Invoke(cache.SaveManager, null);
                if (result is int gold) return gold;
            }
            catch { }
            return -1;
        }
    }
}
