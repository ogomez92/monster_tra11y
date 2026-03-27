using System.Text.RegularExpressions;

namespace MonsterTrainAccessibility.Utilities
{
    /// <summary>
    /// Shared text cleaning utilities for converting game rich text / sprite tags
    /// into plain text suitable for screen reader output.
    /// </summary>
    public static class TextUtilities
    {
        /// <summary>
        /// Strip all Unity rich text tags from a string, converting game-specific tags
        /// (sprites, gold, power, ember, health, damage, capacity) into readable words.
        /// Also strips localization placeholders like {[codeint0]}.
        /// </summary>
        public static string StripRichTextTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Convert sprite tags to readable text first
            // Handles: <sprite name=Gold>, <sprite name="Gold">, etc.
            text = Regex.Replace(
                text,
                @"<sprite\s+name\s*=\s*[""']?([^""'>\s]+)[""']?\s*/?>",
                match => " " + MapSpriteName(match.Groups[1].Value) + " ",
                RegexOptions.IgnoreCase);

            text = Regex.Replace(
                text,
                @"<sprite\s*=\s*[""']?([^""'>\s]+)[""']?\s*/?>",
                match => " " + MapSpriteName(match.Groups[1].Value) + " ",
                RegexOptions.IgnoreCase);

            // Strip localization placeholders: {[codeint0]}, {[effect0.power]}, {[status0.power]}, etc.
            // These appear in generic tooltip text where no card/effect context is available.
            text = Regex.Replace(
                text, @"\{?\[(?:effect|status|trait|paramint|codeint|dynamicint|statusmultiplier)[^\]]*\]\}?", "",
                RegexOptions.IgnoreCase);

            // Handle <gold>X</gold> -> "X gold"
            text = Regex.Replace(
                text,
                @"<gold>([^<]*)</gold>",
                match => match.Groups[1].Value + " gold",
                RegexOptions.IgnoreCase);

            // Handle <+Xpower> or <power>X</power> formats
            text = Regex.Replace(
                text,
                @"<\+?(\d+)power>",
                match => "+" + match.Groups[1].Value + " power",
                RegexOptions.IgnoreCase);
            text = Regex.Replace(
                text,
                @"<power>([^<]*)</power>",
                match => match.Groups[1].Value + " power",
                RegexOptions.IgnoreCase);

            // Handle <ember>X</ember> format
            text = Regex.Replace(
                text,
                @"<ember>([^<]*)</ember>",
                match => match.Groups[1].Value + " ember",
                RegexOptions.IgnoreCase);

            // Handle <health>X</health> format
            text = Regex.Replace(
                text,
                @"<health>([^<]*)</health>",
                match => match.Groups[1].Value + " health",
                RegexOptions.IgnoreCase);

            // Handle <damage>X</damage> or <attack>X</attack> format
            text = Regex.Replace(
                text,
                @"<(?:damage|attack)>([^<]*)</(?:damage|attack)>",
                match => match.Groups[1].Value + " damage",
                RegexOptions.IgnoreCase);

            // Handle <capacity>X</capacity> format
            text = Regex.Replace(
                text,
                @"<capacity>([^<]*)</capacity>",
                match => match.Groups[1].Value + " capacity",
                RegexOptions.IgnoreCase);

            // Use regex to strip all remaining XML-like tags
            text = Regex.Replace(text, @"<[^>]+>", "");

            // Clean up double spaces
            text = Regex.Replace(text, @"\s+", " ");

            return text.Trim();
        }

        /// <summary>
        /// Clean sprite tags and game-specific formatting for screen reader speech output.
        /// Similar to StripRichTextTags but also resolves KEY>> patterns and upgrade highlights.
        /// </summary>
        public static string CleanSpriteTagsForSpeech(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Resolve KEY>>localizationKey<< patterns (game's unresolved localization)
            text = ResolveInlineKeys(text);

            // Convert sprite tags to readable text
            text = Regex.Replace(
                text,
                @"<sprite\s+name\s*=\s*[""']?([^""'>\s]+)[""']?\s*/?>",
                match => " " + match.Groups[1].Value.ToLower() + " ",
                RegexOptions.IgnoreCase);

            text = Regex.Replace(
                text,
                @"<sprite\s*=\s*[""']?([^""'>\s]+)[""']?\s*/?>",
                match => " " + match.Groups[1].Value.ToLower() + " ",
                RegexOptions.IgnoreCase);

            // Handle <gold>X</gold> -> "X gold"
            text = Regex.Replace(
                text,
                @"<gold>([^<]*)</gold>",
                match => match.Groups[1].Value + " gold",
                RegexOptions.IgnoreCase);

            // Handle <+Xpower> or <power>X</power> formats
            text = Regex.Replace(
                text,
                @"<\+?(\d+)power>",
                match => "+" + match.Groups[1].Value + " power",
                RegexOptions.IgnoreCase);
            text = Regex.Replace(
                text,
                @"<power>([^<]*)</power>",
                match => match.Groups[1].Value + " power",
                RegexOptions.IgnoreCase);

            // Handle <ember>X</ember> format
            text = Regex.Replace(
                text,
                @"<ember>([^<]*)</ember>",
                match => match.Groups[1].Value + " ember",
                RegexOptions.IgnoreCase);

            // Handle <health>X</health> format
            text = Regex.Replace(
                text,
                @"<health>([^<]*)</health>",
                match => match.Groups[1].Value + " health",
                RegexOptions.IgnoreCase);

            // Handle <damage>X</damage> or <attack>X</attack> format
            text = Regex.Replace(
                text,
                @"<(?:damage|attack)>([^<]*)</(?:damage|attack)>",
                match => match.Groups[1].Value + " damage",
                RegexOptions.IgnoreCase);

            // Handle <capacity>X</capacity> format
            text = Regex.Replace(
                text,
                @"<capacity>([^<]*)</capacity>",
                match => match.Groups[1].Value + " capacity",
                RegexOptions.IgnoreCase);

            // Remove upgrade highlight tags but keep their content
            text = Regex.Replace(
                text,
                @"</?(?:temp)?[Uu]pgrade[Hh]ighlight>",
                "",
                RegexOptions.IgnoreCase);

            // Strip any remaining rich text tags
            text = Regex.Replace(text, @"<[^>]+>", "");

            // Clean up double spaces
            text = Regex.Replace(text, @"\s+", " ");

            return text.Trim();
        }

        /// <summary>
        /// Map sprite names to readable text for screen readers.
        /// </summary>
        public static string MapSpriteName(string spriteName)
        {
            switch (spriteName.ToLowerInvariant())
            {
                case "xcost": return "X";
                case "gold": return "gold";
                case "capacity": return "capacity";
                case "ember": return "ember";
                case "health": return "health";
                case "attack": return "attack";
                case "damage": return "damage";
                default: return spriteName.ToLower();
            }
        }

        /// <summary>
        /// Resolve KEY>>...&lt;&lt; patterns in text by localizing the embedded key.
        /// The game produces these when its own LocalizeInk fails at runtime,
        /// but the regular Localize method often succeeds for the same key.
        /// </summary>
        public static string ResolveInlineKeys(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains("KEY>>"))
                return text;

            try
            {
                var result = Regex.Replace(text, @"KEY>>([^<]+)<<", match =>
                {
                    string key = match.Groups[1].Value;
                    string localized = Core.KeywordManager.TryLocalize(key);
                    if (!string.IsNullOrEmpty(localized) && localized != key && !localized.Contains("KEY>>"))
                        return localized;
                    localized = LocalizationHelper.TryLocalize(key);
                    if (!string.IsNullOrEmpty(localized) && localized != key && !localized.Contains("KEY>>"))
                        return localized;
                    return key;
                });
                return result;
            }
            catch
            {
                return text;
            }
        }
    }
}
