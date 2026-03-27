using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Detect map screen
    /// </summary>
    public static class MapScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("MapScreen");
                if (targetType == null)
                {
                    targetType = AccessTools.TypeByName("MapNodeScreen");
                }

                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(MapScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched {targetType.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch MapScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Map);

                // Try to get map progress from the MapScreen instance
                string progressInfo = GetMapProgress(__instance);
                if (!string.IsNullOrEmpty(progressInfo))
                {
                    MonsterTrainAccessibility.ScreenReader?.AnnounceScreen($"Map. {progressInfo} Press F1 for help.");
                }
                else
                {
                    MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Map. Press F1 for help.");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in MapScreen patch: {ex.Message}");
            }
        }

        private static string GetMapProgress(object mapScreen)
        {
            try
            {
                var type = mapScreen.GetType();

                // Try to get saveManager field
                var saveManagerField = type.GetField("saveManager", BindingFlags.NonPublic | BindingFlags.Instance);
                if (saveManagerField == null) return null;

                var saveManager = saveManagerField.GetValue(mapScreen);
                if (saveManager == null) return null;

                var saveManagerType = saveManager.GetType();

                // Get current distance (ring/section)
                var getCurrentDistanceMethod = saveManagerType.GetMethod("GetCurrentDistance");
                var getRunLengthMethod = saveManagerType.GetMethod("GetRunLength");

                if (getCurrentDistanceMethod == null || getRunLengthMethod == null)
                    return null;

                int currentDistance = (int)getCurrentDistanceMethod.Invoke(saveManager, null);
                int runLength = (int)getRunLengthMethod.Invoke(saveManager, null);

                // Ring is 1-indexed for user display
                int currentRing = currentDistance + 1;
                int totalRings = runLength;

                // Check victory state
                var getVictorySectionStateMethod = saveManagerType.GetMethod("GetVictorySectionState");
                if (getVictorySectionStateMethod != null)
                {
                    var victoryState = getVictorySectionStateMethod.Invoke(saveManager, null);
                    string victoryStateName = victoryState?.ToString() ?? "";

                    if (victoryStateName == "Victory")
                    {
                        return "Victory!";
                    }
                    else if (victoryStateName == "PreHellforgedBoss")
                    {
                        return $"Ring {currentRing} of {totalRings}. Final boss ahead.";
                    }
                }

                // Try to get available map nodes/paths
                string pathInfo = GetAvailablePaths(mapScreen, type);
                if (!string.IsNullOrEmpty(pathInfo))
                {
                    return $"Ring {currentRing} of {totalRings}. {pathInfo}";
                }

                return $"Ring {currentRing} of {totalRings}.";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting map progress: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get descriptions of available map paths/nodes
        /// </summary>
        private static string GetAvailablePaths(object mapScreen, Type screenType)
        {
            try
            {
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Try to get available nodes
                var nodesField = screenType.GetField("availableNodes", bindingFlags) ??
                                 screenType.GetField("selectableNodes", bindingFlags) ??
                                 screenType.GetField("currentNodes", bindingFlags) ??
                                 screenType.GetField("_availableNodes", bindingFlags);

                if (nodesField != null)
                {
                    var nodes = nodesField.GetValue(mapScreen) as System.Collections.IList;
                    if (nodes != null && nodes.Count > 0)
                    {
                        var nodeNames = new List<string>();
                        foreach (var node in nodes)
                        {
                            if (node == null) continue;
                            string nodeName = GetMapNodeName(node);
                            if (!string.IsNullOrEmpty(nodeName))
                            {
                                nodeNames.Add(nodeName);
                            }
                        }

                        if (nodeNames.Count > 0)
                        {
                            return $"{nodeNames.Count} paths: {string.Join(", ", nodeNames)}.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting map paths: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get a readable name for a map node
        /// </summary>
        private static string GetMapNodeName(object node)
        {
            try
            {
                var nodeType = node.GetType();

                // Try GetNodeData or similar
                var getDataMethod = nodeType.GetMethod("GetMapNodeData", Type.EmptyTypes) ??
                                    nodeType.GetMethod("GetNodeData", Type.EmptyTypes);

                object nodeData = getDataMethod?.Invoke(node, null) ?? node;
                var dataType = nodeData.GetType();
                string typeName = dataType.Name;

                // Map known node data types to readable names
                if (typeName.Contains("Battle") || typeName.Contains("Combat")) return "Battle";
                if (typeName.Contains("Merchant") || typeName.Contains("Shop")) return "Shop";
                if (typeName.Contains("Event") || typeName.Contains("Story")) return "Event";
                if (typeName.Contains("Relic") || typeName.Contains("Artifact")) return "Artifact";
                if (typeName.Contains("Upgrade") || typeName.Contains("Enhancer")) return "Upgrade";
                if (typeName.Contains("Purge")) return "Purge";
                if (typeName.Contains("Pact") || typeName.Contains("Divine")) return "Divine";
                if (typeName.Contains("Reward")) return "Reward";

                // Try to get the name from the node data
                var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    string name = getNameMethod.Invoke(nodeData, null) as string;
                    if (!string.IsNullOrEmpty(name) && !name.Contains("_"))
                        return Screens.BattleAccessibility.StripRichTextTags(name);
                }

                return typeName.Replace("MapNodeData", "").Replace("Data", "");
            }
            catch { }
            return null;
        }
    }
}
