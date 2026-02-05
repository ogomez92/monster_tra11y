using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Shared reflection helpers for reading game state from CharacterState, CardState, and RelicState.
    /// Used by new combat event patches to avoid duplicating reflection boilerplate.
    /// </summary>
    public static class CharacterStateHelper
    {
        // Cached status effect localization data
        private static Dictionary<string, string> _statusIdToLocPrefix;
        private static MethodInfo _localizeMethod;
        private static bool _localizationInitialized;

        public static string GetUnitName(object characterState)
        {
            if (characterState == null) return "Unit";
            try
            {
                var type = characterState.GetType();
                var getNameMethod = type.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(characterState, null) as string;
                    if (!string.IsNullOrEmpty(name) && !name.Contains("KEY>"))
                        return name;
                }

                var getDataMethod = type.GetMethod("GetCharacterDataRead") ?? type.GetMethod("GetCharacterData");
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(characterState, null);
                    if (data != null)
                    {
                        var dataGetNameMethod = data.GetType().GetMethod("GetName");
                        if (dataGetNameMethod != null)
                        {
                            var name = dataGetNameMethod.Invoke(data, null) as string;
                            if (!string.IsNullOrEmpty(name) && !name.Contains("KEY>"))
                                return name;
                        }
                    }
                }
            }
            catch { }
            return "Unit";
        }

        public static bool IsEnemyUnit(object characterState)
        {
            if (characterState == null) return false;
            try
            {
                var getTeamMethod = characterState.GetType().GetMethod("GetTeamType");
                if (getTeamMethod != null)
                {
                    var team = getTeamMethod.Invoke(characterState, null);
                    return team?.ToString() == "Heroes";
                }
            }
            catch { }
            return false;
        }

        public static int GetCurrentHP(object characterState)
        {
            if (characterState == null) return -1;
            try
            {
                var getHPMethod = characterState.GetType().GetMethod("GetHP");
                if (getHPMethod != null)
                {
                    var result = getHPMethod.Invoke(characterState, null);
                    if (result is int hp) return hp;
                }
            }
            catch { }
            return -1;
        }

        public static int GetRoomIndex(object characterState)
        {
            if (characterState == null) return -1;
            try
            {
                var getRoomMethod = characterState.GetType().GetMethod("GetCurrentRoomIndex");
                if (getRoomMethod != null)
                {
                    var result = getRoomMethod.Invoke(characterState, null);
                    if (result is int index) return index;
                }
            }
            catch { }
            return -1;
        }

        public static string GetCardName(object cardState)
        {
            if (cardState == null) return "Card";
            try
            {
                // Try GetTitle first (CardState method)
                var getTitleMethod = cardState.GetType().GetMethod("GetTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    var name = getTitleMethod.Invoke(cardState, null) as string;
                    if (!string.IsNullOrEmpty(name) && !name.Contains("KEY>"))
                        return name;
                }

                // Try GetName
                var getNameMethod = cardState.GetType().GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(cardState, null) as string;
                    if (!string.IsNullOrEmpty(name) && !name.Contains("KEY>"))
                        return name;
                }

                // Try through CardData
                var getDataMethod = cardState.GetType().GetMethod("GetCardDataRead");
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(cardState, null);
                    if (data != null)
                    {
                        var dataGetNameMethod = data.GetType().GetMethod("GetName");
                        if (dataGetNameMethod != null)
                        {
                            var name = dataGetNameMethod.Invoke(data, null) as string;
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }
            }
            catch { }
            return "Card";
        }

        public static string GetRelicName(object relicState)
        {
            if (relicState == null) return "Artifact";
            try
            {
                var getNameMethod = relicState.GetType().GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(relicState, null) as string;
                    if (!string.IsNullOrEmpty(name) && !name.Contains("KEY>"))
                        return name;
                }

                // Try through RelicData
                var getDataMethod = relicState.GetType().GetMethod("GetRelicDataRead") ??
                                     relicState.GetType().GetMethod("GetRelicData");
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(relicState, null);
                    if (data != null)
                    {
                        var dataGetNameMethod = data.GetType().GetMethod("GetName");
                        if (dataGetNameMethod != null)
                        {
                            var name = dataGetNameMethod.Invoke(data, null) as string;
                            if (!string.IsNullOrEmpty(name) && !name.Contains("KEY>"))
                                return name;
                        }
                    }
                }
            }
            catch { }
            return "Artifact";
        }

        public static string CleanStatusName(string statusId)
        {
            if (string.IsNullOrEmpty(statusId))
                return "effect";

            // Try to get the localized name from the game's status effect system
            string localizedName = GetLocalizedStatusName(statusId);
            if (!string.IsNullOrEmpty(localizedName))
                return localizedName;

            // Fall back to string cleanup
            string name = statusId
                .Replace("_StatusId", "")
                .Replace("StatusId", "")
                .Replace("_", " ");

            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            return name.ToLower().Trim();
        }

        /// <summary>
        /// Get the localized display name for a status effect ID using the game's localization system.
        /// </summary>
        private static string GetLocalizedStatusName(string statusId)
        {
            try
            {
                EnsureLocalizationInitialized();

                if (_statusIdToLocPrefix == null || _localizeMethod == null)
                    return null;

                // Look up the localization prefix for this status ID
                if (!_statusIdToLocPrefix.TryGetValue(statusId, out string locPrefix))
                    return null;

                // Try to get the localized name using {prefix}_CardText
                string nameKey = locPrefix + "_CardText";
                string localizedName = InvokeLocalize(nameKey);

                if (!string.IsNullOrEmpty(localizedName) && localizedName != nameKey)
                {
                    // Strip any rich text tags
                    localizedName = Screens.BattleAccessibility.StripRichTextTags(localizedName).Trim();
                    if (!string.IsNullOrEmpty(localizedName))
                        return localizedName;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"GetLocalizedStatusName error: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Initialize the status effect localization lookup dictionary.
        /// </summary>
        private static void EnsureLocalizationInitialized()
        {
            if (_localizationInitialized)
                return;
            _localizationInitialized = true;

            try
            {
                // Find the Localize method
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
                                    break;
                                }
                            }
                        }
                    }
                    catch { }

                    if (_localizeMethod != null) break;
                }

                // Find StatusEffectManager.StatusIdToLocalizationExpression
                Type semType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    semType = assembly.GetType("StatusEffectManager");
                    if (semType != null) break;
                }

                if (semType != null)
                {
                    // Try field first, then property
                    var field = semType.GetField("StatusIdToLocalizationExpression",
                        BindingFlags.Public | BindingFlags.Static);
                    IDictionary dict = null;

                    if (field != null)
                    {
                        dict = field.GetValue(null) as IDictionary;
                    }
                    else
                    {
                        var prop = semType.GetProperty("StatusIdToLocalizationExpression",
                            BindingFlags.Public | BindingFlags.Static);
                        if (prop != null)
                        {
                            dict = prop.GetValue(null) as IDictionary;
                        }
                    }

                    if (dict != null)
                    {
                        _statusIdToLocPrefix = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (DictionaryEntry entry in dict)
                        {
                            string key = entry.Key?.ToString();
                            string value = entry.Value as string;
                            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                            {
                                _statusIdToLocPrefix[key] = value;
                            }
                        }
                        MonsterTrainAccessibility.LogInfo($"CharacterStateHelper: Loaded {_statusIdToLocPrefix.Count} status effect localizations");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"EnsureLocalizationInitialized error: {ex.Message}");
            }
        }

        /// <summary>
        /// Invoke the cached Localize method.
        /// </summary>
        private static string InvokeLocalize(string key)
        {
            if (string.IsNullOrEmpty(key) || _localizeMethod == null)
                return null;

            try
            {
                var parameters = _localizeMethod.GetParameters();
                var args = new object[parameters.Length];
                args[0] = key;
                for (int i = 1; i < parameters.Length; i++)
                {
                    args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                }

                return _localizeMethod.Invoke(null, args) as string;
            }
            catch { }

            return null;
        }
    }
}
