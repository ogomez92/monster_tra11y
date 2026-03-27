using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Utility to check if the game is currently in preview mode.
    /// Preview mode is used when the game calculates damage previews
    /// (e.g., when selecting a card or hovering over targets).
    /// </summary>
    public static class PreviewModeDetector
    {
        private static PropertyInfo _saveManagerPreviewProp;
        private static object _saveManagerInstance;
        private static bool _initialized;
        private static float _lastLookupTime;

        /// <summary>
        /// Check if the game is currently in preview mode via SaveManager.PreviewMode.
        /// </summary>
        public static bool IsInPreviewMode()
        {
            try
            {
                // Re-lookup SaveManager periodically (it may not exist at startup)
                float currentTime = UnityEngine.Time.unscaledTime;
                if (!_initialized || (_saveManagerInstance == null && currentTime - _lastLookupTime > 5f))
                {
                    _lastLookupTime = currentTime;
                    _initialized = true;
                    FindSaveManager();
                }

                if (_saveManagerInstance != null && _saveManagerPreviewProp != null)
                {
                    var result = _saveManagerPreviewProp.GetValue(_saveManagerInstance);
                    if (result is bool preview)
                        return preview;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if a specific CharacterState is in preview mode.
        /// </summary>
        public static bool IsCharacterInPreview(object characterState)
        {
            if (characterState == null) return false;
            try
            {
                var previewProp = characterState.GetType().GetProperty("PreviewMode");
                if (previewProp != null)
                {
                    var result = previewProp.GetValue(characterState);
                    if (result is bool preview)
                        return preview;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if we're in any kind of preview (SaveManager or character-level).
        /// Also checks FloorTargetingSystem.IsTargeting as a fallback.
        /// </summary>
        public static bool ShouldSuppressAnnouncement(object characterState = null)
        {
            // Check global preview mode first (most reliable)
            if (IsInPreviewMode())
                return true;

            // Check character-level preview
            if (characterState != null && IsCharacterInPreview(characterState))
                return true;

            // Check floor targeting (legacy fallback)
            var targeting = MonsterTrainAccessibility.FloorTargeting;
            if (targeting != null && targeting.IsTargeting)
                return true;

            return false;
        }

        private static void FindSaveManager()
        {
            try
            {
                Type saveManagerType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!assembly.GetName().Name.Contains("Assembly-CSharp"))
                        continue;
                    saveManagerType = assembly.GetType("SaveManager");
                    if (saveManagerType != null) break;
                }

                if (saveManagerType == null) return;

                _saveManagerPreviewProp = saveManagerType.GetProperty("PreviewMode",
                    BindingFlags.Public | BindingFlags.Instance);

                // Find the instance
                var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new Type[0]);
                if (findMethod != null)
                {
                    var genericMethod = findMethod.MakeGenericMethod(saveManagerType);
                    _saveManagerInstance = genericMethod.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"PreviewModeDetector: Error finding SaveManager: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset cached references (call when entering a new battle)
        /// </summary>
        public static void Reset()
        {
            _saveManagerInstance = null;
            _initialized = false;
        }
    }
}
