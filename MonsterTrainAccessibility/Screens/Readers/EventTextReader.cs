using MonsterTrainAccessibility.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Extracted reader for Event UI elements.
    /// </summary>
    internal static class EventTextReader
    {

        /// <summary>
        /// Handle elements on the StoryEventScreen: choice items and Continue button.
        /// Uses direct reflection on StoryChoiceItem properties instead of generic TMP search.
        /// Narrative text is announced separately by the OnChoicesPresented/OnStoryFinished patches.
        /// </summary>
        internal static string GetEventScreenElementText(GameObject go)
        {
            if (Help.ScreenStateTracker.CurrentScreen != Help.GameScreen.Event)
                return null;

            try
            {
                // Check if this GO has a StoryChoiceItem component
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == "StoryChoiceItem")
                    {
                        return GetStoryChoiceText(go, comp);
                    }
                }

                // Check if this element is on the StoryEventScreen (e.g. Continue button)
                Component eventScreen = FindStoryEventScreenInHierarchy(go);
                if (eventScreen != null)
                {
                    // Just announce "Continue" - narrative was already spoken by the patch
                    return "Continue";
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in GetEventScreenElementText: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Walk up the hierarchy to find a StoryEventScreen component
        /// </summary>
        internal static Component FindStoryEventScreenInHierarchy(GameObject go)
        {
            Transform current = go.transform;
            while (current != null)
            {
                foreach (var comp in current.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == "StoryEventScreen")
                        return comp;
                }
                current = current.parent;
            }
            return null;
        }


        /// <summary>
        /// Get text from StoryChoiceItem (random event choices like "The Doors. (Get Trap Chute.)")
        /// </summary>
        internal static string GetStoryChoiceText(GameObject go, Component storyChoiceComponent)
        {
            try
            {
                var componentType = storyChoiceComponent.GetType();
                var parts = new List<string>();

                // Primary: use the choiceText property which returns the localized choice text
                var choiceTextProp = componentType.GetProperty("choiceText", BindingFlags.Public | BindingFlags.Instance);
                if (choiceTextProp != null)
                {
                    string choiceText = choiceTextProp.GetValue(storyChoiceComponent) as string;
                    if (!string.IsNullOrEmpty(choiceText))
                    {
                        parts.Add(TextUtilities.StripRichTextTags(choiceText).Trim());
                    }
                }

                // Fallback: read the label TMP field directly
                if (parts.Count == 0)
                {
                    var labelField = componentType.GetField("label", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (labelField != null)
                    {
                        object labelObj = labelField.GetValue(storyChoiceComponent);
                        string labelText = BattleIntroTextReader.GetTMPTextFromObject(labelObj);
                        if (!string.IsNullOrEmpty(labelText))
                        {
                            parts.Add(TextUtilities.StripRichTextTags(labelText).Trim());
                        }
                    }
                }

                // Read the optional reward text from the choice data (most reliable source)
                // StoryChoiceItem.choice.optionalText has the localized, reward-name-replaced text
                string optionalText = null;
                var choiceDataField = componentType.GetField("choiceData", BindingFlags.NonPublic | BindingFlags.Instance);
                if (choiceDataField != null)
                {
                    var choiceData = choiceDataField.GetValue(storyChoiceComponent);
                    if (choiceData != null)
                    {
                        var choiceProp = choiceData.GetType().GetProperty("choice", BindingFlags.Public | BindingFlags.Instance);
                        if (choiceProp != null)
                        {
                            var choice = choiceProp.GetValue(choiceData);
                            if (choice != null)
                            {
                                var optTextProp = choice.GetType().GetProperty("optionalText", BindingFlags.Public | BindingFlags.Instance)
                                               ?? choice.GetType().GetProperty("optionalText", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (optTextProp != null)
                                {
                                    optionalText = optTextProp.GetValue(choice) as string;
                                }
                                // Try field if property not found
                                if (string.IsNullOrEmpty(optionalText))
                                {
                                    var optTextField = choice.GetType().GetField("optionalText", BindingFlags.Public | BindingFlags.Instance);
                                    if (optTextField != null)
                                    {
                                        optionalText = optTextField.GetValue(choice) as string;
                                    }
                                }
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(optionalText))
                {
                    parts.Add("(" + TextUtilities.StripRichTextTags(optionalText).Trim() + ")");
                }
                else
                {
                    // Fallback: read from optionalRewardLabel TMP
                    var rewardLabelField = componentType.GetField("optionalRewardLabel", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (rewardLabelField != null)
                    {
                        object rewardLabelObj = rewardLabelField.GetValue(storyChoiceComponent);
                        if (rewardLabelObj != null)
                        {
                            var gameObjectProp = rewardLabelObj.GetType().GetProperty("gameObject");
                            if (gameObjectProp != null)
                            {
                                var rewardGO = gameObjectProp.GetValue(rewardLabelObj) as GameObject;
                                if (rewardGO != null && rewardGO.activeInHierarchy)
                                {
                                    string rewardText = BattleIntroTextReader.GetTMPTextFromObject(rewardLabelObj);
                                    if (!string.IsNullOrEmpty(rewardText))
                                    {
                                        parts.Add(TextUtilities.StripRichTextTags(rewardText).Trim());
                                    }
                                }
                            }
                        }
                    }
                }

                // Also try the structured reward preview
                string rewardPreviewText = GetStoryChoiceRewardPreview(go);
                if (!string.IsNullOrEmpty(rewardPreviewText))
                {
                    parts.Add("Reward: " + rewardPreviewText);
                }

                if (parts.Count > 0)
                {
                    string result = string.Join(". ", parts);
                    MonsterTrainAccessibility.LogInfo($"StoryChoiceItem text: {result}");
                    return result;
                }

                MonsterTrainAccessibility.LogInfo($"StoryChoiceItem: could not extract text from {go.name}");
                return "Event choice";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting story choice text: {ex.Message}");
                return "Event choice";
            }
        }


        /// <summary>
        /// Find and read the reward preview (card/relic) for a story choice.
        /// Uses reflection to call GetRewardsInfo() on the StoryChoiceItem, then looks up
        /// card/relic data via SaveManager to get actual names and descriptions.
        /// Also falls back to reading the ChoiceRewardPreview UI if visible.
        /// </summary>
        internal static string GetStoryChoiceRewardPreview(GameObject choiceGO)
        {
            try
            {
                // Find StoryChoiceItem component
                Component storyChoiceItem = null;
                foreach (var comp in choiceGO.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == "StoryChoiceItem")
                    {
                        storyChoiceItem = comp;
                        break;
                    }
                }

                if (storyChoiceItem == null) return null;

                // Walk up the hierarchy to find the StoryEventScreen
                Transform current = choiceGO.transform;
                Component storyEventScreen = null;
                while (current != null)
                {
                    foreach (var comp in current.GetComponents<Component>())
                    {
                        if (comp != null && comp.GetType().Name == "StoryEventScreen")
                        {
                            storyEventScreen = comp;
                            break;
                        }
                    }
                    if (storyEventScreen != null) break;
                    current = current.parent;
                }

                // Try to get rewards directly from StoryChoiceItem via GetRewardsInfo()
                var choiceType = storyChoiceItem.GetType();
                var getRewardsMethod = choiceType.GetMethod("GetRewardsInfo", BindingFlags.Public | BindingFlags.Instance);
                if (getRewardsMethod != null)
                {
                    object rewardsList = null;
                    var parameters = getRewardsMethod.GetParameters();
                    if (parameters.Length == 0)
                    {
                        rewardsList = getRewardsMethod.Invoke(storyChoiceItem, null);
                    }
                    else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(bool))
                    {
                        rewardsList = getRewardsMethod.Invoke(storyChoiceItem, new object[] { false });
                    }

                    if (rewardsList is System.Collections.IList rewards && rewards.Count > 0)
                    {
                        // Get SaveManager and RelicManager from StoryEventScreen
                        object saveManager = null;
                        object relicManager = null;
                        if (storyEventScreen != null)
                        {
                            var saveManagerField = storyEventScreen.GetType().GetField("saveManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (saveManagerField != null)
                                saveManager = saveManagerField.GetValue(storyEventScreen);
                            var relicManagerField = storyEventScreen.GetType().GetField("relicManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (relicManagerField != null)
                                relicManager = relicManagerField.GetValue(storyEventScreen);
                        }

                        var rewardTexts = new List<string>();
                        foreach (var reward in rewards)
                        {
                            if (reward == null) continue;
                            var rewardType = reward.GetType();
                            var previewTypeField = rewardType.GetField("previewType", BindingFlags.Public | BindingFlags.Instance);
                            var dataKeyField = rewardType.GetField("dataKey", BindingFlags.Public | BindingFlags.Instance);
                            if (previewTypeField == null || dataKeyField == null) continue;

                            int previewTypeVal = (int)previewTypeField.GetValue(reward);
                            string dataKey = dataKeyField.GetValue(reward) as string;

                            if (string.IsNullOrEmpty(dataKey)) continue;

                            MonsterTrainAccessibility.LogInfo($"Story choice reward: previewType={previewTypeVal}, dataKey={dataKey}");

                            string rewardText = RelicTextReader.GetRewardTextFromData(previewTypeVal, dataKey, saveManager, relicManager);
                            if (!string.IsNullOrEmpty(rewardText))
                            {
                                rewardTexts.Add(rewardText);
                            }
                        }

                        if (rewardTexts.Count > 0)
                        {
                            return string.Join(". ", rewardTexts);
                        }
                    }
                }

                // Fall back to reading the ChoiceRewardPreview UI if visible
                if (storyEventScreen != null)
                {
                    var screenType = storyEventScreen.GetType();
                    var rewardPreviewField = screenType.GetField("rewardPreview", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (rewardPreviewField != null)
                    {
                        var rewardPreview = rewardPreviewField.GetValue(storyEventScreen) as Component;
                        if (rewardPreview != null && rewardPreview.gameObject.activeSelf)
                        {
                            foreach (var comp in rewardPreview.GetComponentsInChildren<Component>(false))
                            {
                                if (comp == null) continue;
                                string typeName = comp.GetType().Name;

                                if (typeName == "CardUI" && comp.gameObject.activeSelf)
                                {
                                    string cardText = CardTextReader.GetCardUIText(comp.gameObject);
                                    if (!string.IsNullOrEmpty(cardText))
                                    {
                                        MonsterTrainAccessibility.LogInfo($"Story choice reward card from preview: {cardText}");
                                        return cardText;
                                    }
                                }

                                if (typeName == "RelicInfoUI" && comp.gameObject.activeSelf)
                                {
                                    string relicText = RelicTextReader.GetRelicInfoText(comp.gameObject);
                                    if (!string.IsNullOrEmpty(relicText))
                                    {
                                        MonsterTrainAccessibility.LogInfo($"Story choice reward relic from preview: {relicText}");
                                        return relicText;
                                    }
                                }
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading story choice reward preview: {ex.Message}");
                return null;
            }
        }

    }
}
