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
                var sb = new StringBuilder();

                int energy = GetCurrentEnergy(cache);
                if (energy >= 0)
                {
                    sb.Append($"Ember: {energy}. ");
                }

                int gold = GetGold(cache);
                if (gold >= 0)
                {
                    sb.Append($"Gold: {gold}. ");
                }

                int pyreHP = GetPyreHealth(cache);
                int maxPyreHP = GetMaxPyreHealth(cache);
                if (pyreHP >= 0)
                {
                    sb.Append($"Pyre: {pyreHP} of {maxPyreHP}. ");
                }

                // Pyre armor and attack
                int pyreArmor = GetPyreStatusEffect(cache, "armor");
                if (pyreArmor > 0)
                {
                    sb.Append($"Pyre Armor: {pyreArmor}. ");
                }

                int pyreAttack = GetPyreStatusEffect(cache, "attack");
                if (pyreAttack > 0)
                {
                    sb.Append($"Pyre Attack: {pyreAttack}. ");
                }

                var hand = HandReader.GetHandCards(cache);
                if (hand != null)
                {
                    sb.Append($"Cards in hand: {hand.Count}. ");
                }

                // Crystals and threat level (DLC)
                string crystalInfo = GetCrystalAndThreatInfo(cache);
                if (!string.IsNullOrEmpty(crystalInfo))
                {
                    sb.Append($"{crystalInfo}. ");
                }

                // Wave counter
                string waveInfo = GetWaveInfo(cache);
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
        private static string GetCrystalAndThreatInfo(BattleManagerCache cache)
        {
            try
            {
                if (cache.SaveManager == null)
                {
                    cache.FindManagers();
                }
                if (cache.SaveManager == null) return null;

                var saveType = cache.SaveManager.GetType();

                // Check if DLC crystals are shown (ShowPactCrystals)
                var showMethod = saveType.GetMethod("ShowPactCrystals", Type.EmptyTypes);
                if (showMethod != null)
                {
                    bool show = (bool)showMethod.Invoke(cache.SaveManager, null);
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
                            var dlcSaveData = genericMethod.Invoke(cache.SaveManager, new object[] { hellforgedValue });
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
                        crystals = (int)getPactMethod.Invoke(cache.SaveManager, null);
                    }
                }

                if (crystals < 0) return null;

                // Determine threat level based on crystal count
                string threat = GetThreatLevelName(cache, crystals);
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
        private static string GetThreatLevelName(BattleManagerCache cache, int crystals)
        {
            try
            {
                if (crystals <= 0) return "None";

                if (cache.SaveManager == null) return "Low";
                var saveType = cache.SaveManager.GetType();

                // Try to get threat level thresholds from BalanceData
                var getBalanceMethod = saveType.GetMethod("GetBalanceData", Type.EmptyTypes);
                if (getBalanceMethod != null)
                {
                    var balanceData = getBalanceMethod.Invoke(cache.SaveManager, null);
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
