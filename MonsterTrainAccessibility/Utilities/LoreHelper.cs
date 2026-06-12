using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace MonsterTrainAccessibility.Utilities
{
    /// <summary>
    /// Reads the game's lore (flavor text) tooltips from cards, units and
    /// artifacts. Each object carries a list of localization keys (the
    /// commentary attributed to Herzal, Malicka or Heph); the game shows
    /// them as extra tooltips when the Lore Tooltips setting is enabled,
    /// and the mod mirrors that gate.
    /// </summary>
    internal static class LoreHelper
    {
        private static readonly string[] LoreKeyMethods =
        {
            "GetCardLoreTooltipKeys",      // CardState, CardData
            "GetCharacterLoreTooltipKeys", // CharacterState, CharacterData
            "GetRelicLoreTooltipKeys",     // RelicData (artifacts, enhancers)
        };

        /// <summary>
        /// Lore paragraphs for a card/unit/artifact state or data object,
        /// joined into one string - or null when the object has none or
        /// lore tooltips are disabled in the game settings.
        /// </summary>
        internal static string GetLore(object dataOrState)
        {
            if (dataOrState == null || !LoreTooltipsEnabled())
                return null;

            try
            {
                var type = dataOrState.GetType();
                foreach (var methodName in LoreKeyMethods)
                {
                    var method = type.GetMethod(methodName, Type.EmptyTypes);
                    if (method == null)
                        continue;

                    if (!(method.Invoke(dataOrState, null) is IEnumerable keys))
                        return null;

                    var parts = new List<string>();
                    foreach (var keyObj in keys)
                    {
                        if (!(keyObj is string key) || string.IsNullOrEmpty(key))
                            continue;
                        string text = Core.KeywordManager.TryLocalize(key);
                        if (!string.IsNullOrEmpty(text) && text != key)
                            parts.Add(TextUtilities.StripRichTextTags(text).Trim());
                    }
                    return parts.Count > 0 ? string.Join(" ", parts) : null;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogInfo($"Lore read failed: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// The game's Lore Tooltips preference. Defaults to true when the
        /// preference can't be read so lore is not silently lost.
        /// </summary>
        private static bool LoreTooltipsEnabled()
        {
            try
            {
                var pmType = ReflectionHelper.FindType("PreferencesManager");
                var instanceProp = pmType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                object instance = instanceProp?.GetValue(null)
                    ?? pmType?.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (instance == null)
                    return true;

                if (instance.GetType().GetProperty("LoreTooltipsEnabled")?.GetValue(instance) is bool enabled)
                    return enabled;
            }
            catch { }
            return true;
        }
    }
}
