using System;
using System.Collections.Generic;
using System.Reflection;
using MonsterTrainAccessibility.Utilities;

namespace MonsterTrainAccessibility.Core
{
    /// <summary>
    /// Reads run-wide resources (gold, pyre health, pact shards) straight from
    /// SaveManager, so the resource hotkeys (Ctrl+G, Ctrl+H, Ctrl+R) work on
    /// every screen, not just in battle.
    /// </summary>
    internal static class RunInfoReader
    {
        private static readonly Dictionary<string, MethodInfo> _methodCache =
            new Dictionary<string, MethodInfo>();

        public static int GetGold() => InvokeInt("GetGold");

        public static int GetPyreHealth() => InvokeInt("GetTowerHP");

        public static int GetMaxPyreHealth() => InvokeInt("GetMaxTowerHP");

        /// <summary>
        /// Pact shard count and threat level (The Last Divinity DLC), or null
        /// when the DLC run feature is not active.
        /// </summary>
        public static string GetPactShardInfo()
        {
            return Screens.ResourceReader.GetCrystalAndThreatInfo(
                ReflectionHelper.FindManager("SaveManager"));
        }

        private static int InvokeInt(string methodName)
        {
            try
            {
                var saveManager = ReflectionHelper.FindManager("SaveManager");
                if (saveManager == null)
                    return -1;

                if (!_methodCache.TryGetValue(methodName, out var method))
                {
                    method = saveManager.GetType().GetMethod(methodName, Type.EmptyTypes);
                    _methodCache[methodName] = method;
                }

                if (method?.Invoke(saveManager, null) is int value)
                    return value;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading SaveManager.{methodName}: {ex.Message}");
            }
            return -1;
        }
    }
}
