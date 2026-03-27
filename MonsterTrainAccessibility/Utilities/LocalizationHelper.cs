using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Utilities
{
    /// <summary>
    /// Centralized localization helper that caches the game's Localize extension method
    /// and provides a single entry point for all localization calls across the mod.
    /// </summary>
    public static class LocalizationHelper
    {
        private static MethodInfo _localizeMethod;
        private static bool _searched;

        /// <summary>
        /// Try to localize a string key using the game's localization system.
        /// Returns the localized text, or null if localization failed.
        /// </summary>
        public static string TryLocalize(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            try
            {
                EnsureInitialized();

                if (_localizeMethod != null)
                {
                    var parameters = _localizeMethod.GetParameters();
                    var args = new object[parameters.Length];
                    args[0] = key;
                    for (int i = 1; i < parameters.Length; i++)
                    {
                        args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                    }

                    var result = _localizeMethod.Invoke(null, args) as string;
                    if (!string.IsNullOrEmpty(result))
                        return result;
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Localize a key, returning the key itself as fallback if localization fails.
        /// Also checks if the key looks like a localization key before attempting.
        /// </summary>
        public static string Localize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Check if it looks like a localization key (typically contains _ or is UPPERCASE)
            if (!text.Contains("_") && text != text.ToUpperInvariant())
                return text;

            return TryLocalize(text) ?? text;
        }

        /// <summary>
        /// Localize a key, returning null if it fails or returns the same key.
        /// Useful when you want to know if localization actually succeeded.
        /// </summary>
        public static string LocalizeOrNull(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            var result = TryLocalize(key);
            if (!string.IsNullOrEmpty(result) && result != key)
                return result;

            return null;
        }

        private static void EnsureInitialized()
        {
            if (_searched) return;
            _searched = true;

            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var asmName = assembly.GetName().Name;
                    if (!asmName.Contains("Assembly-CSharp") && !asmName.Contains("Trainworks"))
                        continue;

                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (!type.IsClass) continue;

                            var method = type.GetMethod("Localize", BindingFlags.Public | BindingFlags.Static);
                            if (method != null && method.ReturnType == typeof(string))
                            {
                                var parameters = method.GetParameters();
                                if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(string))
                                {
                                    _localizeMethod = method;
                                    MonsterTrainAccessibility.LogInfo($"LocalizationHelper: Found Localize method in {type.FullName}");
                                    return;
                                }
                            }
                        }
                    }
                    catch { }
                }

                MonsterTrainAccessibility.LogWarning("LocalizationHelper: Could not find Localize method");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"LocalizationHelper init error: {ex.Message}");
            }
        }
    }
}
