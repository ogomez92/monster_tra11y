using System.Collections.Generic;
using MonsterTrainAccessibility.Utilities;

namespace MonsterTrainAccessibility.Core.Buffers
{
    /// <summary>
    /// A reader's two-part result for focus changes: Summary is the short
    /// announcement spoken when the element gains focus (name, price, cost),
    /// Details are the reviewable items (rarity, description, keyword
    /// explanations) for the matching buffer. FullText preserves the complete
    /// single-string reading for callers that want everything at once.
    /// </summary>
    public class FocusReadout
    {
        public string Summary;
        public List<string> Details = new List<string>();
        public string FullText;

        /// <summary>
        /// Build a readout from a single full-text reading: the given summary
        /// is announced, the full text becomes the reviewable detail items.
        /// </summary>
        public static FocusReadout FromFullText(string fullText, string summary)
        {
            if (string.IsNullOrEmpty(fullText))
                return null;

            return new FocusReadout
            {
                Summary = string.IsNullOrEmpty(summary) ? fullText : summary,
                FullText = fullText,
                Details = TextUtilities.SplitIntoSpeechItems(fullText)
            };
        }
    }
}
