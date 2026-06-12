using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Keeps accessibility navigation keys away from the game:
    ///
    /// - While Ctrl is held, the arrow keys belong to the review buffers and
    ///   map cursor (Ctrl+Up/Down/Left/Right). The game would otherwise still
    ///   see the arrows and move the real selection at the same time, causing
    ///   double actions and double announcements.
    /// - While the F1 help list is open, Up/Down browse help entries and
    ///   Enter/Escape close it, so those keys (and Submit/Cancel) must not
    ///   reach the game either.
    /// - While the battle floor review is open, the arrows navigate floors
    ///   and units, Enter reads details, and Escape closes it. The plain Up
    ///   arrow that opens the review is claimed whenever it could open it
    ///   (in battle, no targeting) - keyed off the physical key state like
    ///   the Ctrl checks, because script execution order is undefined and
    ///   the game could otherwise process the opening press first.
    ///
    /// Two input paths are suppressed:
    /// - UnityEngine.EventSystems.BaseInput.GetAxisRaw/GetButtonDown, which
    ///   feed StandaloneInputModule's UI focus navigation (the game's
    ///   GameInputModuleBridge inherits these unchanged)
    /// - ScreenManager.OnInputMappingSignaled, which forwards the game's own
    ///   InputManager.Controls (arrow keys, Enter, Escape via user key
    ///   mappings) to screens - e.g. HandUI cycles the selected card on
    ///   Controls.Left/Right. Only keyboard mappings are suppressed;
    ///   WASD and gamepad input pass through.
    /// </summary>
    public static class InputSuppressionPatch
    {
        private static FieldInfo _deviceIdField;
        private static FieldInfo _keyCodeField;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var baseInputType = typeof(UnityEngine.EventSystems.BaseInput);

                var getAxisRaw = AccessTools.Method(baseInputType, "GetAxisRaw");
                if (getAxisRaw != null)
                {
                    harmony.Patch(getAxisRaw,
                        prefix: new HarmonyMethod(typeof(InputSuppressionPatch).GetMethod(nameof(AxisPrefix))));
                }

                var getButtonDown = AccessTools.Method(baseInputType, "GetButtonDown");
                if (getButtonDown != null)
                {
                    harmony.Patch(getButtonDown,
                        prefix: new HarmonyMethod(typeof(InputSuppressionPatch).GetMethod(nameof(ButtonPrefix))));
                }

                var screenManagerType = AccessTools.TypeByName("ScreenManager");
                var onMappingSignaled = screenManagerType != null
                    ? AccessTools.Method(screenManagerType, "OnInputMappingSignaled")
                    : null;
                if (onMappingSignaled != null)
                {
                    harmony.Patch(onMappingSignaled,
                        prefix: new HarmonyMethod(typeof(InputSuppressionPatch).GetMethod(nameof(MappingPrefix))));
                }
                else
                {
                    MonsterTrainAccessibility.LogWarning("Could not patch ScreenManager.OnInputMappingSignaled - Ctrl+arrows may also move the game selection");
                }

                MonsterTrainAccessibility.LogInfo("Patched accessibility input suppression");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch input suppression: {ex.Message}");
            }
        }

        public static bool AxisPrefix(string __0, ref float __result)
        {
            bool suppress = (IsCtrlHeld() || IsHelpOpen() || IsReviewOpen()) && IsNavigationAxis(__0);
            // The Up press that opens the floor review must not also move the UI focus
            suppress |= __0 == "Vertical" && IsBattleUpArrowClaim();

            if (suppress)
            {
                __result = 0f;
                return false;
            }
            return true;
        }

        public static bool ButtonPrefix(string __0, ref bool __result)
        {
            bool suppress = (IsCtrlHeld() || IsHelpOpen() || IsReviewOpen()) && IsNavigationAxis(__0);
            suppress |= __0 == "Vertical" && IsBattleUpArrowClaim();
            // While help or the floor review is open, Enter and Escape act on
            // the modal instead of the game
            suppress |= (IsHelpOpen() || IsReviewOpen()) && (__0 == "Submit" || __0 == "Cancel");

            if (suppress)
            {
                __result = false;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Swallow keyboard control mappings claimed by the accessibility layer
        /// before ScreenManager forwards them to the active screens.
        /// </summary>
        public static bool MappingPrefix(object __0)
        {
            bool helpOpen = IsHelpOpen();
            bool reviewOpen = IsReviewOpen();
            bool upArrowClaim = IsBattleUpArrowClaim();
            if (__0 == null || (!IsCtrlHeld() && !helpOpen && !reviewOpen && !upArrowClaim))
                return true;

            try
            {
                if (_deviceIdField == null || _keyCodeField == null)
                {
                    var mappingType = __0.GetType();
                    _deviceIdField = mappingType.GetField("deviceID");
                    _keyCodeField = mappingType.GetField("keyCode");
                }

                if (_deviceIdField?.GetValue(__0)?.ToString() != "Keyboard")
                    return true;

                if (!(_keyCodeField?.GetValue(__0) is KeyCode keyCode))
                    return true;

                bool isArrow = keyCode == KeyCode.UpArrow || keyCode == KeyCode.DownArrow ||
                               keyCode == KeyCode.LeftArrow || keyCode == KeyCode.RightArrow;
                if (isArrow && (IsCtrlHeld() || helpOpen || reviewOpen))
                    return false;

                // The plain Up arrow opens the floor review in battle, so the
                // game must never see it there (it would move navigation to
                // the tower). Other arrows stay with the game until the
                // review is actually open.
                if (keyCode == KeyCode.UpArrow && upArrowClaim)
                    return false;

                // The game's mappings ignore modifiers, so Ctrl+H/R/G would
                // also fire e.g. ToggleSynthesisTooltips (H) or
                // LivePresenceToggle (R). While Ctrl is held those keys belong
                // to the mod's run info hotkeys.
                if (IsCtrlHeld() && IsRunInfoKey(keyCode))
                    return false;

                if (helpOpen &&
                    (keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter ||
                     keyCode == KeyCode.Escape || keyCode == KeyCode.Space))
                {
                    return false;
                }

                if (reviewOpen &&
                    (keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter ||
                     keyCode == KeyCode.Escape))
                {
                    return false;
                }
            }
            catch
            {
                // Never break the game's input pipeline
            }

            return true;
        }

        private static bool IsNavigationAxis(string name)
        {
            return name == "Horizontal" || name == "Vertical";
        }

        private static bool IsCtrlHeld()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

        private static bool IsRunInfoKey(KeyCode keyCode)
        {
            var config = MonsterTrainAccessibility.AccessibilitySettings;
            if (config == null)
                return false;

            return keyCode == config.ReadGoldKey.Value ||
                   keyCode == config.ReadPyreHealthKey.Value ||
                   keyCode == config.ReadPactShardsKey.Value;
        }

        /// <summary>
        /// Includes the frame help was closed: the closing keypress reads as
        /// pressed for the whole frame and must not leak into the game.
        /// </summary>
        private static bool IsHelpOpen()
        {
            var help = MonsterTrainAccessibility.HelpSystem;
            return help != null && (help.IsBrowsing || help.ClosedThisFrame);
        }

        /// <summary>
        /// Includes the frame the floor review was closed, so the Escape that
        /// closed it does not also pause the game.
        /// </summary>
        private static bool IsReviewOpen()
        {
            var review = MonsterTrainAccessibility.FloorReview;
            return review != null && (review.IsActive || review.ClosedThisFrame);
        }

        /// <summary>
        /// In battle the plain Up arrow belongs to the floor review (it opens
        /// it). Checked from physical key state, not mod state, so the press
        /// that opens the review is claimed regardless of script order.
        /// </summary>
        private static bool IsBattleUpArrowClaim()
        {
            if (MonsterTrainAccessibility.FloorReview == null)
                return false;
            if (!Input.GetKey(KeyCode.UpArrow))
                return false;
            // Only when the battle screen itself has focus - pile views,
            // dialogs, and other overlays keep their arrow navigation
            if (!Battle.FloorReviewSystem.IsBattleScreenFrontmost())
                return false;
            return Battle.FloorTargetingSystem.Instance?.IsTargeting != true &&
                   Battle.UnitTargetingSystem.Instance?.IsTargeting != true;
        }
    }
}
