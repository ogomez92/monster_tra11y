using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MonsterTrainAccessibility.Utilities;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Virtual map cursor for the map screen, modeled on Say the Spire's map
    /// viewer. Lets blind players browse the entire run map without moving the
    /// game's real selection:
    ///   Ctrl+Up    - move forward one ring (toward the final boss)
    ///   Ctrl+Down  - move backward one ring
    ///   Ctrl+Right - next stop on this ring
    ///   Ctrl+Left  - previous stop on this ring
    /// All data is read from RunState/NodeState via reflection, using
    /// MapSection.GetMapNodeDataForBranch so node locations match the game's
    /// own CanBeTriggered/HasBeenVisited bookkeeping.
    /// </summary>
    public class MapNavigator
    {
        private int _cursorDistance = -1;
        private int _cursorNodeIndex;
        private List<string> _ringNodes = new List<string>();

        private static MethodInfo _getMapNodeDataForBranchMethod;
        private static Type _branchSelectionType;
        private static FieldInfo _nodeDataField;
        private static FieldInfo _nodeLocationField;

        /// <summary>
        /// Called when the map screen is shown - park the cursor at the train's
        /// current position so the first Ctrl+arrow press starts from there.
        /// </summary>
        public void OnMapScreenShown()
        {
            _cursorDistance = -1;
            _cursorNodeIndex = 0;
            _ringNodes.Clear();
        }

        /// <summary>
        /// Ctrl+Up: move the cursor one ring toward the final boss.
        /// The first press announces the current ring instead of moving.
        /// </summary>
        public void NextRing()
        {
            if (!EnsureInitialized())
                return;
            MoveToRing(_cursorDistance + 1);
        }

        /// <summary>
        /// Ctrl+Down: move the cursor one ring back toward the start.
        /// </summary>
        public void PreviousRing()
        {
            if (!EnsureInitialized())
                return;
            MoveToRing(_cursorDistance - 1);
        }

        /// <summary>
        /// Ctrl+Right: next stop on the cursor's ring.
        /// </summary>
        public void NextNode()
        {
            if (!EnsureInitialized())
                return;

            if (_ringNodes.Count == 0)
            {
                Speak("No stops on this ring");
                return;
            }

            if (_cursorNodeIndex >= _ringNodes.Count - 1)
            {
                Speak($"Last stop on ring {_cursorDistance + 1}");
                return;
            }

            _cursorNodeIndex++;
            AnnounceNode();
        }

        /// <summary>
        /// Ctrl+Left: previous stop on the cursor's ring.
        /// </summary>
        public void PreviousNode()
        {
            if (!EnsureInitialized())
                return;

            if (_ringNodes.Count == 0)
            {
                Speak("No stops on this ring");
                return;
            }

            if (_cursorNodeIndex <= 0)
            {
                Speak($"First stop on ring {_cursorDistance + 1}");
                return;
            }

            _cursorNodeIndex--;
            AnnounceNode();
        }

        /// <summary>
        /// Initialize the cursor at the train's current ring if needed.
        /// Returns false when map data is unavailable (announces why).
        /// When initialization happens, the current ring is announced and
        /// the triggering move is swallowed so users start oriented.
        /// </summary>
        private bool EnsureInitialized()
        {
            if (_cursorDistance >= 0)
                return true;

            var saveManager = GetSaveManager();
            if (saveManager == null)
            {
                Speak("Map data not available");
                return false;
            }

            int currentDistance = InvokeInt(saveManager, "GetCurrentDistance");
            int runLength = InvokeInt(saveManager, "GetRunLength");
            if (runLength <= 0)
            {
                Speak("Map data not available");
                return false;
            }

            _cursorDistance = Math.Max(0, Math.Min(currentDistance, runLength - 1));
            _cursorNodeIndex = 0;
            BuildRing();
            AnnounceRing();
            return false; // swallow the first move so the user starts at their position
        }

        private void MoveToRing(int targetDistance)
        {
            var saveManager = GetSaveManager();
            if (saveManager == null)
            {
                Speak("Map data not available");
                return;
            }

            int runLength = InvokeInt(saveManager, "GetRunLength");

            if (targetDistance < 0)
            {
                Speak("First ring");
                return;
            }
            if (targetDistance >= runLength)
            {
                Speak("Last ring");
                return;
            }

            _cursorDistance = targetDistance;
            _cursorNodeIndex = 0;
            BuildRing();
            AnnounceRing();
        }

        #region Announcements

        private void AnnounceRing()
        {
            var saveManager = GetSaveManager();
            if (saveManager == null)
                return;

            try
            {
                int runLength = InvokeInt(saveManager, "GetRunLength");
                int currentDistance = InvokeInt(saveManager, "GetCurrentDistance");

                var parts = new List<string>
                {
                    $"Ring {_cursorDistance + 1} of {runLength}"
                };

                if (_cursorDistance == currentDistance)
                    parts.Add("current position");
                else if (_cursorDistance < currentDistance)
                    parts.Add("traveled");

                // Which branch was taken, for traveled branching rings
                var runState = Invoke(saveManager, "GetRunState");
                if (runState != null)
                {
                    int branches = InvokeInt(runState, "GetNumBranchesAtDistance", _cursorDistance);
                    if (branches > 1)
                    {
                        int chosen = InvokeInt(runState, "GetChosenBranchAtDistance", _cursorDistance);
                        if (chosen == 0)
                            parts.Add("left path taken");
                        else if (chosen == 1)
                            parts.Add("right path taken");
                        else
                            parts.Add("branches left and right");
                    }

                    // DLC pact shard requirement (The Last Divinity)
                    var nodeState = Invoke(runState, "GetNodeStateAtDistance", _cursorDistance);
                    if (nodeState != null)
                    {
                        var crystalsField = nodeState.GetType().GetField("requiredCrystals",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (crystalsField?.GetValue(nodeState) is int crystals && crystals > 0)
                            parts.Add($"requires {crystals} pact shards");
                    }
                }

                string battle = GetBattleDescription(saveManager, _cursorDistance);
                if (!string.IsNullOrEmpty(battle))
                    parts.Add(battle);

                int stops = _ringNodes.Count;
                parts.Add(stops == 1 ? "1 stop" : $"{stops} stops");

                Speak(string.Join(", ", parts) + ".");
                AnnounceNode();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error announcing map ring: {ex.Message}");
            }
        }

        private void AnnounceNode()
        {
            if (_cursorNodeIndex >= 0 && _cursorNodeIndex < _ringNodes.Count)
            {
                // Position info always trails the content (user preference)
                Speak($"{_ringNodes[_cursorNodeIndex]}. Stop {_cursorNodeIndex + 1} of {_ringNodes.Count}");
            }
        }

        #endregion

        #region Ring data

        /// <summary>
        /// Build the descriptions for every stop on the cursor's ring:
        /// nodes on both paths first, then left path, then right path.
        /// </summary>
        private void BuildRing()
        {
            _ringNodes = new List<string>();

            var saveManager = GetSaveManager();
            if (saveManager == null)
                return;

            try
            {
                var runState = Invoke(saveManager, "GetRunState");
                if (runState == null)
                    return;

                int branches = InvokeInt(runState, "GetNumBranchesAtDistance", _cursorDistance);
                int currentDistance = InvokeInt(saveManager, "GetCurrentDistance");
                int chosen = branches > 1 ? InvokeInt(runState, "GetChosenBranchAtDistance", _cursorDistance) : -1;

                // HasBeenVisited/CanBeTriggered are only meaningful once the ring's
                // rewards exist - the game generates them on arrival at a distance.
                // On future rings a merchant's empty goods list reads as "visited"
                // (TrueForAll on empty) and gold merchants always report triggerable,
                // so skip both there; "available" only matters on the current ring.
                bool futureRing = _cursorDistance > currentDistance;
                bool currentRing = _cursorDistance == currentDistance;

                var left = GetNodesForBranch(saveManager, _cursorDistance, 0);
                var right = branches > 1
                    ? GetNodesForBranch(saveManager, _cursorDistance, 1)
                    : new List<(object data, object location)>();

                // Nodes present on both branches are the same MapNodeData asset
                var sharedData = new HashSet<object>();
                foreach (var (leftData, _) in left)
                {
                    foreach (var (rightData, _) in right)
                    {
                        if (ReferenceEquals(leftData, rightData))
                        {
                            sharedData.Add(leftData);
                            break;
                        }
                    }
                }

                foreach (var (data, location) in left)
                {
                    if (sharedData.Contains(data))
                        _ringNodes.Add(DescribeNode(saveManager, data, location, branches > 1 ? "both paths" : null,
                            announceVisited: !futureRing,
                            announceAvailable: currentRing));
                }
                foreach (var (data, location) in left)
                {
                    if (sharedData.Contains(data))
                        continue;
                    // Hellforged pact nodes only live in branch 0's data but the
                    // game presents them as reachable from either path
                    bool eitherPath = branches <= 1 || IsHellforgedNode(data);
                    string label = branches <= 1 ? null : (eitherPath ? "both paths" : "left path");
                    _ringNodes.Add(DescribeNode(saveManager, data, location, label,
                        announceVisited: !futureRing,
                        announceAvailable: currentRing && (eitherPath || chosen != 1)));
                }
                foreach (var (data, location) in right)
                {
                    if (!sharedData.Contains(data))
                        _ringNodes.Add(DescribeNode(saveManager, data, location, "right path",
                            announceVisited: !futureRing,
                            announceAvailable: currentRing && chosen != 0));
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error building map ring: {ex.Message}");
            }
        }

        private static bool IsHellforgedNode(object data)
        {
            try
            {
                var dlc = data.GetType().GetMethod("GetRequiredDlc")?.Invoke(data, null);
                return dlc?.ToString() == "Hellforged";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Call MapSection.GetMapNodeDataForBranch so node locations match the
        /// game's own indexing (merchants, events, then rewards per branch).
        /// </summary>
        private List<(object data, object location)> GetNodesForBranch(object saveManager, int distance, int branch)
        {
            var result = new List<(object, object)>();

            if (_getMapNodeDataForBranchMethod == null)
            {
                var mapSectionType = ReflectionHelper.FindType("MapSection");
                _getMapNodeDataForBranchMethod = mapSectionType?.GetMethod("GetMapNodeDataForBranch",
                    BindingFlags.Public | BindingFlags.Static);
                if (_getMapNodeDataForBranchMethod != null)
                    _branchSelectionType = _getMapNodeDataForBranchMethod.GetParameters()[1].ParameterType;
            }

            if (_getMapNodeDataForBranchMethod == null || _branchSelectionType == null)
                return result;

            object branchValue = Enum.ToObject(_branchSelectionType, branch);
            var list = _getMapNodeDataForBranchMethod.Invoke(null,
                new object[] { distance, branchValue, saveManager }) as IList;
            if (list == null)
                return result;

            foreach (var entry in list)
            {
                if (entry == null)
                    continue;

                if (_nodeDataField == null || _nodeLocationField == null)
                {
                    var entryType = entry.GetType();
                    _nodeDataField = entryType.GetField("data");
                    _nodeLocationField = entryType.GetField("location");
                }

                var data = _nodeDataField?.GetValue(entry);
                var location = _nodeLocationField?.GetValue(entry);
                if (data != null && location != null)
                    result.Add((data, location));
            }

            return result;
        }

        private string DescribeNode(object saveManager, object data, object location, string branchLabel,
            bool announceVisited, bool announceAvailable)
        {
            var parts = new List<string>();

            string title = GetNodeTitle(data);
            parts.Add(title);

            if (!string.IsNullOrEmpty(branchLabel))
                parts.Add(branchLabel);

            try
            {
                var dataType = data.GetType();
                if (announceVisited)
                {
                    var visitedMethod = dataType.GetMethod("HasBeenVisited");
                    if (visitedMethod != null && (bool)visitedMethod.Invoke(data, new[] { location, saveManager }))
                        parts.Add("visited");
                }

                if (announceAvailable)
                {
                    var canTriggerMethod = dataType.GetMethod("CanBeTriggered");
                    if (canTriggerMethod != null && (bool)canTriggerMethod.Invoke(data, new[] { location, saveManager }))
                        parts.Add("available");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Could not read map node state: {ex.Message}");
            }

            string description = string.Join(", ", parts);

            string body = GetNodeBody(data);
            if (!string.IsNullOrEmpty(body))
                description += $". {body}";

            return description;
        }

        private static string GetNodeTitle(object data)
        {
            try
            {
                var title = data.GetType().GetMethod("GetTooltipTitle")?.Invoke(data, null) as string;
                title = TextUtilities.StripRichTextTags(title);
                if (!string.IsNullOrEmpty(title) && !LooksLikeLocalizationKey(title))
                    return title;
            }
            catch { }

            return CleanNodeTypeName(data.GetType().Name);
        }

        private static string GetNodeBody(object data)
        {
            try
            {
                var body = data.GetType().GetMethod("GetTooltipBody")?.Invoke(data, null) as string;
                body = TextUtilities.StripRichTextTags(body);
                if (!string.IsNullOrEmpty(body) && !LooksLikeLocalizationKey(body))
                    return body;
            }
            catch { }
            return null;
        }

        private static bool LooksLikeLocalizationKey(string text)
        {
            return text.Contains("_") && text.Contains("-");
        }

        private static string CleanNodeTypeName(string typeName)
        {
            if (typeName.Contains("Merchant")) return "Merchant";
            if (typeName.Contains("Story") || typeName.Contains("Event")) return "Event";
            if (typeName.Contains("Random")) return "Mystery";
            if (typeName.Contains("Pact") || typeName.Contains("Divine")) return "Divine";
            if (typeName.Contains("Reward")) return "Reward";
            return typeName.Replace("MapNodeData", "").Replace("PoolData", "").Replace("Data", "");
        }

        private static string GetBattleDescription(object saveManager, int distance)
        {
            try
            {
                var scenario = Invoke(saveManager, "GetScenarioData", distance);
                if (scenario == null)
                    return null;

                string name = scenario.GetType().GetMethod("GetBattleName")?.Invoke(scenario, null) as string;
                name = TextUtilities.StripRichTextTags(name);

                string difficulty = scenario.GetType().GetMethod("GetDifficulty")?.Invoke(scenario, null)?.ToString();
                string prefix = difficulty switch
                {
                    "Boss" => "Boss battle",
                    "Hard" => "Hard battle",
                    _ => "Battle"
                };

                return string.IsNullOrEmpty(name) ? prefix : $"{prefix}: {name}";
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Reflection helpers

        private static object GetSaveManager()
        {
            return ReflectionHelper.FindManager("SaveManager");
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            if (target == null)
                return null;

            try
            {
                var types = new Type[args.Length];
                for (int i = 0; i < args.Length; i++)
                    types[i] = args[i]?.GetType() ?? typeof(object);

                var method = target.GetType().GetMethod(methodName, types) ??
                             target.GetType().GetMethod(methodName);
                return method?.Invoke(target, args);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Map reflection call {methodName} failed: {ex.Message}");
                return null;
            }
        }

        private static int InvokeInt(object target, string methodName, params object[] args)
        {
            var result = Invoke(target, methodName, args);
            return result is int value ? value : -1;
        }

        private static void Speak(string text)
        {
            MonsterTrainAccessibility.ScreenReader?.Speak(text, false);
        }

        #endregion
    }
}
