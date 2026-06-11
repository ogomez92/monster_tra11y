using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// While Ctrl is held, arrow keys belong to the accessibility review
    /// buffers and map cursor (Ctrl+Up/Down/Left/Right). The game would
    /// otherwise still see the arrows and move the real selection at the same
    /// time, causing double actions and double announcements.
    ///
    /// Two input paths are suppressed:
    /// - UnityEngine.EventSystems.BaseInput.GetAxisRaw/GetButtonDown, which
    ///   feed StandaloneInputModule's UI focus navigation (the game's
    ///   GameInputModuleBridge inherits these unchanged)
    /// - ScreenManager.OnInputMappingSignaled, which forwards the game's own
    ///   InputManager.Controls.Left/Right/Up/Down (arrow keys via user key
    ///   mappings) to screens - e.g. HandUI cycles the selected card on
    ///   Controls.Left/Right. Only keyboard arrow mappings are suppressed;
    ///   WASD and gamepad input pass through.
    /// </summary>
    public static class CtrlNavigationSuppressionPatch
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
                        prefix: new HarmonyMethod(typeof(CtrlNavigationSuppressionPatch).GetMethod(nameof(AxisPrefix))));
                }

                var getButtonDown = AccessTools.Method(baseInputType, "GetButtonDown");
                if (getButtonDown != null)
                {
                    harmony.Patch(getButtonDown,
                        prefix: new HarmonyMethod(typeof(CtrlNavigationSuppressionPatch).GetMethod(nameof(ButtonPrefix))));
                }

                var screenManagerType = AccessTools.TypeByName("ScreenManager");
                var onMappingSignaled = screenManagerType != null
                    ? AccessTools.Method(screenManagerType, "OnInputMappingSignaled")
                    : null;
                if (onMappingSignaled != null)
                {
                    harmony.Patch(onMappingSignaled,
                        prefix: new HarmonyMethod(typeof(CtrlNavigationSuppressionPatch).GetMethod(nameof(MappingPrefix))));
                }
                else
                {
                    MonsterTrainAccessibility.LogWarning("Could not patch ScreenManager.OnInputMappingSignaled - Ctrl+arrows may also move the game selection");
                }

                MonsterTrainAccessibility.LogInfo("Patched Ctrl+arrow navigation suppression");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch Ctrl navigation suppression: {ex.Message}");
            }
        }

        public static bool AxisPrefix(string __0, ref float __result)
        {
            if (IsCtrlHeld() && IsNavigationAxis(__0))
            {
                __result = 0f;
                return false;
            }
            return true;
        }

        public static bool ButtonPrefix(string __0, ref bool __result)
        {
            if (IsCtrlHeld() && IsNavigationAxis(__0))
            {
                __result = false;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Swallow keyboard arrow control mappings while Ctrl is held, before
        /// ScreenManager forwards them to the active screens.
        /// </summary>
        public static bool MappingPrefix(object __0)
        {
            if (__0 == null || !IsCtrlHeld())
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

                if (_keyCodeField?.GetValue(__0) is KeyCode keyCode &&
                    (keyCode == KeyCode.UpArrow || keyCode == KeyCode.DownArrow ||
                     keyCode == KeyCode.LeftArrow || keyCode == KeyCode.RightArrow))
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
    }
}
