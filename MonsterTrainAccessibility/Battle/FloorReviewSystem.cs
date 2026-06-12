using System;
using System.Collections.Generic;
using System.Reflection;
using MonsterTrainAccessibility.Core.Buffers;
using MonsterTrainAccessibility.Screens;
using MonsterTrainAccessibility.Utilities;
using UnityEngine;

namespace MonsterTrainAccessibility.Battle
{
    /// <summary>
    /// Virtual review cursor over the battle floors, opened with the plain
    /// Up arrow during battle. MT1 has no keyboard way to inspect units in
    /// place (only mouse hover tooltips, and clicking units never performs
    /// actions), so this fills that gap.
    ///
    /// Up/Down move between floors (bottom, middle, top, pyre room) and
    /// Left/Right step through the units on the floor in spatial order:
    /// your units back-to-front, then enemies front-to-back, matching the
    /// targeting convention that your frontmost unit faces the first enemy.
    /// Enter re-reads the focused floor or unit with full details, Escape
    /// closes.
    ///
    /// The cursor never moves the real game selection, and game state is
    /// re-read on every keypress so the view stays current while units
    /// move or die. Focus announcements stay concise; full details (status
    /// effects with keyword explanations, abilities, intents, floor
    /// corruption and enchantments) feed the UI buffer - and the Creature
    /// buffer for units - for Ctrl+arrow review.
    ///
    /// Input is driven from InputInterceptor; InputSuppressionPatch keeps
    /// the claimed keys (arrows, Enter, Escape) away from the game while
    /// the review is open, including the Up press that opens it.
    /// </summary>
    public class FloorReviewSystem
    {
        /// <summary>
        /// Whether the review cursor is currently open
        /// </summary>
        public bool IsActive { get; private set; }

        private int _closedFrame = -1;

        /// <summary>
        /// True for the rest of the frame in which the review was closed.
        /// The Escape that closed it reads as pressed for the whole frame
        /// and must stay claimed or it would also pause the game.
        /// </summary>
        public bool ClosedThisFrame => _closedFrame == Time.frameCount;

        private int _roomIndex;

        /// <summary>-1 focuses the floor itself; 0.. index into the unit lineup.</summary>
        private int _unitIndex = -1;

        private struct LineupEntry
        {
            public object Unit;
            public bool IsEnemy;
            public int Rank;      // 1 = frontmost of its team
            public int TeamCount;
        }

        private static BattleManagerCache Cache => MonsterTrainAccessibility.BattleHandler?.Cache;

        /// <summary>
        /// Open the review at the game's currently selected floor.
        /// </summary>
        public void Open()
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle == null || !IsBattleScreenFrontmost())
                return;

            int floor = battle.GetSelectedFloor();
            _roomIndex = floor >= 0 && floor <= 3 ? floor : 0;
            _unitIndex = -1;
            IsActive = true;
            AnnounceFocus("Floor review. ");
        }

        /// <summary>
        /// Close the review; quiet when closed externally (battle ends,
        /// targeting starts).
        /// </summary>
        public void Close(bool announce)
        {
            if (!IsActive)
                return;

            IsActive = false;
            _closedFrame = Time.frameCount;
            _unitIndex = -1;
            if (announce)
                MonsterTrainAccessibility.ScreenReader?.Speak("Floor review closed", false);
        }

        /// <summary>
        /// Handle a keypress while the review is open. Returns true when the
        /// key was claimed (InputInterceptor then applies its cooldown);
        /// unclaimed keys fall through to the normal hotkeys (F1, H, T...).
        /// </summary>
        public bool HandleInput()
        {
            if (!IsActive)
                return false;

            // An overlay (pile view, dialog, settings) took the screen - give
            // the arrows back to it
            if (!IsBattleScreenFrontmost())
            {
                Close(announce: false);
                return false;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close(announce: true);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveFloor(+1);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveFloor(-1);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveUnit(+1);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveUnit(-1);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ReadFocusDetails();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Room indices run bottom (0) to pyre room (3), so up is +1.
        /// At the ends the current floor is re-read instead of wrapping.
        /// </summary>
        private void MoveFloor(int direction)
        {
            _roomIndex = Mathf.Clamp(_roomIndex + direction, 0, 3);
            _unitIndex = -1;
            AnnounceFocus();
        }

        /// <summary>
        /// Step through the floor's units; moving left of the first unit
        /// returns to the floor overview.
        /// </summary>
        private void MoveUnit(int direction)
        {
            var lineup = BuildLineup();
            if (lineup.Count == 0 && direction > 0)
            {
                MonsterTrainAccessibility.ScreenReader?.Speak("No units on this floor", false);
                return;
            }
            _unitIndex = Mathf.Clamp(_unitIndex + direction, -1, lineup.Count - 1);
            AnnounceFocus();
        }

        /// <summary>
        /// Announce the focused floor or unit concisely and feed its full
        /// details to the review buffers.
        /// </summary>
        private void AnnounceFocus(string prefix = "")
        {
            try
            {
                var lineup = BuildLineup();
                if (_unitIndex >= lineup.Count)
                    _unitIndex = lineup.Count - 1; // units may have died since the last keypress

                string announcement;
                string details;
                bool isCreature = _unitIndex >= 0;
                if (isCreature)
                {
                    var entry = lineup[_unitIndex];
                    announcement = prefix + DescribeUnit(entry, brief: true);
                    details = DescribeUnit(entry, brief: false);
                }
                else
                {
                    announcement = prefix + GetFloorOverview(lineup);
                    details = GetFloorDetails();
                }

                FocusBuffers.SetReviewFocus(details, isCreature);
                MonsterTrainAccessibility.ScreenReader?.Speak(announcement, false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Floor review announce failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Enter: read the focused floor or unit with full details live.
        /// </summary>
        private void ReadFocusDetails()
        {
            try
            {
                var lineup = BuildLineup();
                if (_unitIndex >= lineup.Count)
                    _unitIndex = lineup.Count - 1;

                string details = _unitIndex >= 0
                    ? DescribeUnit(lineup[_unitIndex], brief: false)
                    : GetFloorDetails();
                MonsterTrainAccessibility.ScreenReader?.Speak(details, false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Floor review detail read failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Build the floor's lineup left to right: your units back-to-front,
        /// then enemies front-to-back (the readers return each team
        /// front-to-back).
        /// </summary>
        private List<LineupEntry> BuildLineup()
        {
            var lineup = new List<LineupEntry>();
            try
            {
                var cache = Cache;
                if (cache == null)
                    return lineup;
                var room = FloorReader.GetRoom(cache, _roomIndex);
                if (room == null)
                    return lineup;

                var friendly = new List<object>();
                var enemies = new List<object>();
                foreach (var unit in FloorReader.GetUnitsInRoom(room))
                {
                    if (FloorReader.IsEnemyUnit(cache, unit))
                        enemies.Add(unit);
                    else
                        friendly.Add(unit);
                }

                for (int i = friendly.Count - 1; i >= 0; i--)
                    lineup.Add(new LineupEntry { Unit = friendly[i], IsEnemy = false, Rank = i + 1, TeamCount = friendly.Count });
                for (int i = 0; i < enemies.Count; i++)
                    lineup.Add(new LineupEntry { Unit = enemies[i], IsEnemy = true, Rank = i + 1, TeamCount = enemies.Count });
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Floor review lineup failed: {ex.Message}");
            }
            return lineup;
        }

        private string DescribeUnit(LineupEntry entry, bool brief)
        {
            var cache = Cache;
            string side = entry.IsEnemy ? "Enemy" : "Your unit";
            string position = "";
            if (entry.TeamCount > 1)
            {
                if (entry.Rank == 1)
                    position = ", front";
                else if (entry.Rank == entry.TeamCount)
                    position = ", back";
            }
            string body = brief
                ? FloorReader.GetUnitBriefDescription(cache, entry.Unit)
                : EnemyReader.GetDetailedUnitDescription(cache, entry.Unit, includeKeywords: true);
            // Position info always trails the content (user preference)
            return $"{body}. {side} {entry.Rank} of {entry.TeamCount}{position}";
        }

        /// <summary>
        /// Concise floor announcement: capacity (or pyre health), frozen
        /// state, corruption, then unit names front-to-back per team.
        /// Per-unit stats and floor enchantments live in the buffers.
        /// </summary>
        private string GetFloorOverview(List<LineupEntry> lineup)
        {
            var cache = Cache;
            string floorName = FloorReader.RoomIndexToFloorName(_roomIndex);
            var parts = new List<string>();

            var room = cache != null ? FloorReader.GetRoom(cache, _roomIndex) : null;
            if (_roomIndex == 3)
            {
                int pyreHP = ResourceReader.GetPyreHealth(cache);
                if (pyreHP >= 0)
                    parts.Add($"Pyre {pyreHP} of {ResourceReader.GetMaxPyreHealth(cache)} health");
            }
            else if (room != null)
            {
                if (IsRoomFrozen(room))
                    parts.Add("Frozen");
                var (used, max) = FloorReader.GetFloorCapacityInfo(room);
                if (max > 0)
                    parts.Add($"{used} of {max} capacity");
                string corruption = FloorReader.GetFloorCorruption(room);
                if (!string.IsNullOrEmpty(corruption))
                    parts.Add(corruption);
            }

            var friendlyNames = new List<string>();
            var enemyNames = new List<string>();
            foreach (var entry in lineup)
            {
                string name = FloorReader.GetUnitName(cache, entry.Unit);
                if (entry.IsEnemy)
                    enemyNames.Add(name);
                else
                    friendlyNames.Add(name);
            }
            friendlyNames.Reverse(); // lineup holds yours back-to-front
            if (friendlyNames.Count > 0)
                parts.Add($"Your units: {string.Join(", ", friendlyNames)}");
            if (enemyNames.Count > 0)
                parts.Add($"Enemies: {string.Join(", ", enemyNames)}");
            if (friendlyNames.Count == 0 && enemyNames.Count == 0)
                parts.Add("Empty");

            return $"{floorName}: {string.Join(". ", parts)}";
        }

        private string GetFloorDetails()
        {
            var cache = Cache;
            string floorName = FloorReader.RoomIndexToFloorName(_roomIndex);
            string summary = cache != null
                ? FloorReader.GetFloorSummary(cache, _roomIndex, includeKeywords: true)
                : null;
            return string.IsNullOrEmpty(summary) ? floorName : $"{floorName}: {summary}";
        }

        private static bool IsRoomFrozen(object room)
        {
            try
            {
                var method = room.GetType().GetMethod("IsRoomEnabled", Type.EmptyTypes);
                return method?.Invoke(room, null) is bool enabled && !enabled;
            }
            catch
            {
                return false;
            }
        }

        #region Battle screen frontmost check

        private static object _screenManager;
        private static MethodInfo _getScreenActiveMethod;
        private static object[] _overlayScreenNames;
        private static int _frontmostFrame = -1;
        private static bool _frontmostResult;

        /// <summary>
        /// ScreenName enum values (from the decompiled game source) of
        /// screens that can overlay the battle and need the arrow keys:
        /// Deck (also the draw/discard/exhaust/eaten pile views), Cheat,
        /// Minimap, Settings (also the pause menu), Dialog, ModSettings,
        /// Compendium, KeyMapping, RunHistory.
        /// </summary>
        private static readonly int[] OverlayScreenNameValues = { 3, 8, 17, 18, 19, 20, 25, 30, 35 };

        /// <summary>
        /// True when the battle screen itself has input focus: in battle
        /// with no overlay screen open above it. The plain Up arrow is only
        /// claimed then. Asks the game's ScreenManager directly because
        /// ScreenStateTracker only sees screens opening, not closing.
        /// Cached per frame - this runs from the input suppression patches.
        /// </summary>
        internal static bool IsBattleScreenFrontmost()
        {
            if (Time.frameCount == _frontmostFrame)
                return _frontmostResult;
            _frontmostFrame = Time.frameCount;
            _frontmostResult = ComputeBattleScreenFrontmost();
            return _frontmostResult;
        }

        private static bool ComputeBattleScreenFrontmost()
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle == null || !battle.IsInBattle)
                return false;

            try
            {
                // UnityEngine.Object's Equals reports destroyed instances as null
                if (_screenManager == null || _screenManager.Equals(null))
                {
                    _screenManager = ReflectionHelper.FindManager("ScreenManager");
                    _getScreenActiveMethod = _screenManager?.GetType().GetMethod("GetScreenActive");

                    var screenNameType = ReflectionHelper.FindType("ScreenName");
                    if (screenNameType != null)
                    {
                        _overlayScreenNames = new object[OverlayScreenNameValues.Length];
                        for (int i = 0; i < OverlayScreenNameValues.Length; i++)
                            _overlayScreenNames[i] = Enum.ToObject(screenNameType, OverlayScreenNameValues[i]);
                    }
                }

                if (_screenManager == null || _getScreenActiveMethod == null || _overlayScreenNames == null)
                {
                    // Fall back to the tracker (DeckScreenPatch.ClosePostfix
                    // restores it to Battle when a pile view closes mid-battle)
                    return Help.ScreenStateTracker.CurrentScreen == Help.GameScreen.Battle;
                }

                var args = new object[1];
                foreach (var screenName in _overlayScreenNames)
                {
                    args[0] = screenName;
                    if (_getScreenActiveMethod.Invoke(_screenManager, args) is bool active && active)
                        return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Floor review screen check failed: {ex.Message}");
                return Help.ScreenStateTracker.CurrentScreen == Help.GameScreen.Battle;
            }
        }

        #endregion
    }
}
