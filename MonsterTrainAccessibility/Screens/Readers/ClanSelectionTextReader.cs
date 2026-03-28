using MonsterTrainAccessibility.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System;
using UnityEngine.UI;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Extracted reader for ClanSelection UI elements.
    /// </summary>
    internal static class ClanSelectionTextReader
    {

        /// <summary>
        /// Get text for clan selection icons (ClassSelectionIcon component)
        /// </summary>
        internal static string GetClanSelectionText(GameObject go)
        {
            try
            {
                // Look for ClassSelectionIcon component on this object or parents
                Component classSelectionIcon = null;
                Transform current = go.transform;

                while (current != null && classSelectionIcon == null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;

                        if (typeName == "ClassSelectionIcon")
                        {
                            classSelectionIcon = component;
                            break;
                        }
                    }
                    current = current.parent;
                }

                if (classSelectionIcon == null)
                    return null;

                var iconType = classSelectionIcon.GetType();

                // Determine if this is main clan or allied clan selection based on parent names
                bool isMainClan = false;
                bool isAlliedClan = false;
                current = go.transform;
                while (current != null)
                {
                    string parentName = current.name.ToLower();
                    if (parentName.Contains("main class") || parentName.Contains("primary"))
                    {
                        isMainClan = true;
                        break;
                    }
                    if (parentName.Contains("sub class") || parentName.Contains("allied") || parentName.Contains("secondary"))
                    {
                        isAlliedClan = true;
                        break;
                    }
                    current = current.parent;
                }

                // Try to get the ClassData from the component
                object classData = null;

                // Try the 'data' property first (found in log output)
                var dataProp = iconType.GetProperty("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (dataProp != null)
                {
                    classData = dataProp.GetValue(classSelectionIcon);
                    if (classData != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found clan data via property: data, type: {classData.GetType().Name}");
                    }
                }

                // Try backing field if property didn't work
                if (classData == null)
                {
                    var backingField = iconType.GetField("<data>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (backingField != null)
                    {
                        classData = backingField.GetValue(classSelectionIcon);
                        if (classData != null)
                        {
                            MonsterTrainAccessibility.LogInfo($"Found clan data via backing field, type: {classData.GetType().Name}");
                        }
                    }
                }

                // Try various other field names for the class data
                if (classData == null)
                {
                    var fieldNames = new[] { "classData", "_classData", "linkedClass", "_linkedClass", "ClassData", "_data" };
                    foreach (var fieldName in fieldNames)
                    {
                        var field = iconType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            classData = field.GetValue(classSelectionIcon);
                            if (classData != null)
                            {
                                MonsterTrainAccessibility.LogInfo($"Found clan data via field: {fieldName}");
                                break;
                            }
                        }
                    }
                }

                // Try other properties if still not found
                if (classData == null)
                {
                    var propNames = new[] { "ClassData", "LinkedClass", "GetClassData", "Data" };
                    foreach (var propName in propNames)
                    {
                        var prop = iconType.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (prop != null)
                        {
                            classData = prop.GetValue(classSelectionIcon);
                            if (classData != null)
                            {
                                MonsterTrainAccessibility.LogInfo($"Found clan data via property: {propName}");
                                break;
                            }
                        }
                    }
                }

                if (classData == null)
                {
                    // Log available fields/properties for debugging
                    var fields = iconType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var props = iconType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    MonsterTrainAccessibility.LogInfo($"ClassSelectionIcon fields: {string.Join(", ", fields.Select(f => f.Name))}");
                    MonsterTrainAccessibility.LogInfo($"ClassSelectionIcon properties: {string.Join(", ", props.Select(p => p.Name))}");
                    return "Clan option";
                }

                var classOptionDataType = classData.GetType();

                // The data property returns ClassOptionData which wraps the actual ClassData
                // ClassOptionData has: isRandom, classData, isLocked
                bool isRandom = false;
                bool isLocked = false;
                object actualClassData = classData;

                // Check if this is ClassOptionData (wrapper type)
                if (classOptionDataType.Name == "ClassOptionData")
                {
                    // Get isRandom field
                    var isRandomField = classOptionDataType.GetField("isRandom", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (isRandomField != null)
                    {
                        isRandom = (bool)isRandomField.GetValue(classData);
                    }

                    // Get isLocked field
                    var isLockedField = classOptionDataType.GetField("isLocked", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (isLockedField != null)
                    {
                        isLocked = (bool)isLockedField.GetValue(classData);
                    }

                    // Get the actual classData field
                    var actualClassDataField = classOptionDataType.GetField("classData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (actualClassDataField != null)
                    {
                        actualClassData = actualClassDataField.GetValue(classData);
                    }

                    MonsterTrainAccessibility.LogInfo($"ClassOptionData: isRandom={isRandom}, isLocked={isLocked}, actualClassData={actualClassData?.GetType().Name ?? "null"}");
                }

                // Handle random option
                if (isRandom)
                {
                    if (isMainClan)
                        return "Primary clan: Random. Select a random clan for this run.";
                    else if (isAlliedClan)
                        return "Allied clan: Random. Select a random allied clan for this run.";
                    else
                        return "Random clan option";
                }

                // If we couldn't get the actual class data, return locked message or generic
                if (actualClassData == null)
                {
                    if (isLocked)
                        return isMainClan ? "Primary clan: Locked" : (isAlliedClan ? "Allied clan: Locked" : "Locked clan");
                    return "Clan option";
                }

                var classDataType = actualClassData.GetType();
                MonsterTrainAccessibility.LogInfo($"Actual ClassData type: {classDataType.Name}");

                // Log available methods and fields on the actual classData for debugging
                var classDataMethods = classDataType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object))
                    .Select(m => m.Name)
                    .Distinct()
                    .Take(20);
                var classDataFieldNames = classDataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Select(f => f.Name)
                    .Take(20);
                MonsterTrainAccessibility.LogInfo($"ClassData methods: {string.Join(", ", classDataMethods)}");
                MonsterTrainAccessibility.LogInfo($"ClassData fields: {string.Join(", ", classDataFieldNames)}");

                string clanName = null;
                string clanDescription = null;

                // Get clan name via GetTitle() method
                var getTitleMethod = classDataType.GetMethod("GetTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    clanName = getTitleMethod.Invoke(actualClassData, null) as string;
                    MonsterTrainAccessibility.LogInfo($"GetTitle() returned: {clanName}");
                }

                // Fallback: try titleLoc field with Localize()
                if (string.IsNullOrEmpty(clanName))
                {
                    var titleLocField = classDataType.GetField("titleLoc", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (titleLocField != null)
                    {
                        var titleLoc = titleLocField.GetValue(actualClassData) as string;
                        MonsterTrainAccessibility.LogInfo($"titleLoc field: {titleLoc}");
                        if (!string.IsNullOrEmpty(titleLoc))
                        {
                            clanName = LocalizationHelper.Localize(titleLoc);
                        }
                    }
                }

                // Get description - use GetSubclassDescription for allied clan, GetDescription for main clan
                // These methods return localized text directly
                string descMethodName = isAlliedClan ? "GetSubclassDescription" : "GetDescription";
                var getDescMethod = classDataType.GetMethod(descMethodName, Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    clanDescription = getDescMethod.Invoke(actualClassData, null) as string;
                    MonsterTrainAccessibility.LogInfo($"{descMethodName}() returned: {clanDescription}");
                }

                // Fallback to regular GetDescription if subclass description wasn't found
                if (string.IsNullOrEmpty(clanDescription) && isAlliedClan)
                {
                    var fallbackDescMethod = classDataType.GetMethod("GetDescription", Type.EmptyTypes);
                    if (fallbackDescMethod != null)
                    {
                        clanDescription = fallbackDescMethod.Invoke(actualClassData, null) as string;
                        MonsterTrainAccessibility.LogInfo($"GetDescription() fallback returned: {clanDescription}");
                    }
                }

                // Build the result
                var result = new StringBuilder();

                if (isMainClan)
                {
                    result.Append("Primary clan: ");
                }
                else if (isAlliedClan)
                {
                    result.Append("Allied clan: ");
                }

                if (!string.IsNullOrEmpty(clanName))
                {
                    result.Append(clanName);
                    if (isLocked)
                    {
                        result.Append(" (Locked");
                        string unlockReason = GetUnlockProgressString(actualClassData);
                        if (!string.IsNullOrEmpty(unlockReason))
                            result.Append($". {unlockReason}");
                        result.Append(")");
                    }
                }
                else
                {
                    result.Append(isLocked ? "Locked clan" : "Unknown clan");
                }

                if (!string.IsNullOrEmpty(clanDescription))
                {
                    result.Append(". ");
                    result.Append(TextUtilities.StripRichTextTags(clanDescription));
                }

                // Get clan level and XP progression from SaveManager
                try
                {
                    string progressionInfo = GetClanProgressionInfo(actualClassData);
                    if (!string.IsNullOrEmpty(progressionInfo))
                    {
                        result.Append(". ");
                        result.Append(progressionInfo);
                    }
                }
                catch (Exception progressEx)
                {
                    MonsterTrainAccessibility.LogError($"Error getting clan progression: {progressEx.Message}");
                }

                result.Append(". Use Left and Right arrows to change selection.");

                return result.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting clan selection text: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Get clan level and XP progression info directly from SaveManager
        /// </summary>
        internal static string GetClanProgressionInfo(object classData)
        {
            if (classData == null) return null;

            var classDataType = classData.GetType();

            // Get class ID
            var getIdMethod = classDataType.GetMethod("GetID", Type.EmptyTypes);
            if (getIdMethod == null) return null;
            string classId = getIdMethod.Invoke(classData, null) as string;
            if (string.IsNullOrEmpty(classId)) return null;

            // Find SaveManager from ClassSelectionScreen
            Type screenType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.GetName().Name.Contains("Assembly-CSharp")) continue;
                screenType = assembly.GetType("ClassSelectionScreen");
                if (screenType != null) break;
            }
            if (screenType == null) return null;

            var screen = UnityEngine.Object.FindObjectOfType(screenType);
            if (screen == null) return null;

            var saveManagerField = screenType.GetField("saveManager", BindingFlags.NonPublic | BindingFlags.Instance);
            if (saveManagerField == null) return null;
            var saveManager = saveManagerField.GetValue(screen);
            if (saveManager == null) return null;

            var saveType = saveManager.GetType();
            var sb = new StringBuilder();

            // Get class level: saveManager.GetClassLevel(classId)
            var getClassLevelMethod = saveType.GetMethod("GetClassLevel", new[] { typeof(string) });
            if (getClassLevelMethod != null)
            {
                var levelResult = getClassLevelMethod.Invoke(saveManager, new object[] { classId });
                if (levelResult is int level)
                {
                    sb.Append($"Clan level {level}");

                    // Get class XP: saveManager.GetClassXP(classId)
                    var getClassXPMethod = saveType.GetMethod("GetClassXP", new[] { typeof(string) });
                    if (getClassXPMethod != null)
                    {
                        var xpResult = getClassXPMethod.Invoke(saveManager, new object[] { classId });
                        if (xpResult is int xp)
                        {
                            // Get XP required for next level from BalanceData
                            var getBalanceMethod = saveType.GetMethod("GetBalanceData", Type.EmptyTypes);
                            if (getBalanceMethod != null)
                            {
                                var balanceData = getBalanceMethod.Invoke(saveManager, null);
                                if (balanceData != null)
                                {
                                    var balanceType = balanceData.GetType();

                                    // Apply XP calculation (same as ClassLevelMeterUI.ShowXP does)
                                    var applyXpMethod = balanceType.GetMethod("ApplyClassXP");
                                    if (applyXpMethod != null)
                                    {
                                        object[] applyArgs = new object[] { level, xp };
                                        applyXpMethod.Invoke(balanceData, applyArgs);
                                        level = (int)applyArgs[0];
                                        xp = (int)applyArgs[1];

                                        // Update the level text with the adjusted value
                                        sb.Clear();
                                        sb.Append($"Clan level {level}");
                                    }

                                    var hasNextMethod = balanceType.GetMethod("HasNextClassLevel");
                                    if (hasNextMethod != null)
                                    {
                                        bool hasNext = (bool)hasNextMethod.Invoke(balanceData, new object[] { level });
                                        if (hasNext)
                                        {
                                            var getXpRequired = balanceType.GetMethod("GetXPRequiredForNextClassLevel");
                                            if (getXpRequired != null)
                                            {
                                                var totalXp = getXpRequired.Invoke(balanceData, new object[] { level });
                                                if (totalXp is int total)
                                                {
                                                    sb.Append($", XP {xp:N0}/{total:N0}");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            sb.Append(" (max level)");
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Get next level unlock preview
                    var getPreviewMethod = classDataType.GetMethod("GetLocalizedClassUnlockPreview");
                    if (getPreviewMethod != null)
                    {
                        string previewKey = getPreviewMethod.Invoke(classData, new object[] { level }) as string;
                        if (!string.IsNullOrEmpty(previewKey))
                        {
                            string previewText = LocalizationHelper.Localize(previewKey);
                            if (!string.IsNullOrEmpty(previewText))
                            {
                                sb.Append($". Next unlock: {TextUtilities.StripRichTextTags(previewText)}");
                            }
                        }
                    }
                }
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        /// <summary>
        /// Get unlock progress string for a locked clan via ClassData.GetFullClassUnlockProgressString(SaveManager)
        /// </summary>
        private static string GetUnlockProgressString(object classData)
        {
            if (classData == null) return null;
            try
            {
                var classDataType = classData.GetType();

                // Need SaveManager to call GetFullClassUnlockProgressString
                var saveManager = FindSaveManager();
                if (saveManager == null) return null;

                var method = classDataType.GetMethod("GetFullClassUnlockProgressString",
                    new[] { saveManager.GetType() });
                if (method != null)
                {
                    var result = method.Invoke(classData, new[] { saveManager }) as string;
                    if (!string.IsNullOrEmpty(result))
                        return TextUtilities.StripRichTextTags(result);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting unlock progress: {ex.Message}");
            }
            return null;
        }

        private static object FindSaveManager()
        {
            Type screenType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.GetName().Name.Contains("Assembly-CSharp")) continue;
                screenType = assembly.GetType("ClassSelectionScreen");
                if (screenType != null) break;
            }
            if (screenType == null) return null;
            var screen = UnityEngine.Object.FindObjectOfType(screenType);
            if (screen == null) return null;
            var field = screenType.GetField("saveManager", BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(screen);
        }


        /// <summary>
        /// Get text for champion choice buttons on the clan selection screen.
        /// The focused object is the GameUISelectableButton referenced by ChampionChoiceButton.Button.
        /// This button may NOT be in the same hierarchy as ChampionChoiceButton (serialized references
        /// can point anywhere), so we find ChampionSelectionUI in parents and match the focused object
        /// against each ChampionChoiceButton's Button property.
        /// </summary>
        internal static string GetChampionChoiceText(GameObject go)
        {
            try
            {
                // Find ChampionSelectionUI anywhere in parents - this is the container for all champion buttons
                Component championSelectionUI = FindComponentInSelfOrParents(go.transform, "ChampionSelectionUI");
                if (championSelectionUI == null)
                    return null;

                var uiType = championSelectionUI.GetType();
                var bindFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Get the championChoiceButtons list
                var buttonsField = uiType.GetField("championChoiceButtons", bindFlags);
                var buttons = buttonsField?.GetValue(championSelectionUI) as System.Collections.IList;
                if (buttons == null || buttons.Count == 0)
                    return null;

                // Find which ChampionChoiceButton owns the focused GameUISelectableButton.
                // ChampionChoiceButton.Button returns the GameUISelectableButton - match it against go's components.
                int buttonIndex = -1;
                Component matchedButton = null;
                for (int i = 0; i < buttons.Count; i++)
                {
                    var choiceButton = buttons[i] as Component;
                    if (choiceButton == null) continue;

                    // Read the Button property (returns GameUISelectableButton)
                    var buttonProp = choiceButton.GetType().GetProperty("Button", bindFlags | BindingFlags.Public);
                    var selectableButton = buttonProp?.GetValue(choiceButton) as Component;
                    if (selectableButton != null && selectableButton.gameObject == go)
                    {
                        buttonIndex = i;
                        matchedButton = choiceButton;
                        break;
                    }
                }

                if (buttonIndex < 0 || matchedButton == null)
                    return null; // Focused object isn't a champion choice button

                bool isButtonLocked = IsChampionButtonLocked(matchedButton);

                // Get champion data via ClassData.GetChampionData(index)
                var classDataField = uiType.GetField("classData", bindFlags);
                var classData = classDataField?.GetValue(championSelectionUI);
                if (classData == null)
                    return isButtonLocked ? "Champion: Locked" : "Champion option";

                var getChampionDataMethod = classData.GetType().GetMethod("GetChampionData", new[] { typeof(int) });
                object championData = getChampionDataMethod?.Invoke(classData, new object[] { buttonIndex });
                if (championData == null)
                    return isButtonLocked ? "Champion: Locked" : "Champion option";

                return BuildChampionText(championData, isButtonLocked);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting champion choice text: {ex.Message}");
            }
            return null;
        }

        private static Component FindComponentInSelfOrParents(Transform transform, string typeName)
        {
            Transform current = transform;
            while (current != null)
            {
                foreach (var component in current.GetComponents<Component>())
                {
                    if (component != null && component.GetType().Name == typeName)
                        return component;
                }
                current = current.parent;
            }
            return null;
        }

        private static bool IsChampionButtonLocked(Component championButton)
        {
            // The game sets lockedTooltipProvider.enabled = locked in ChampionChoiceButton.SetState
            var providerField = championButton.GetType().GetField("lockedTooltipProvider",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (providerField == null) return false;

            var provider = providerField.GetValue(championButton);
            if (provider == null) return false;

            var enabledProp = provider.GetType().GetProperty("enabled");
            if (enabledProp == null) return false;

            return (bool)enabledProp.GetValue(provider);
        }

        private static string BuildChampionText(object championData, bool isLocked)
        {
            var champType = championData.GetType();
            var bindFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            // Get champion card data
            var cardData = champType.GetField("championCardData", bindFlags)?.GetValue(championData);
            string championName = GetNameFromData(cardData);
            if (string.IsNullOrEmpty(championName))
                return isLocked ? "Champion: Locked" : "Champion option";

            var sb = new StringBuilder("Champion: ");
            sb.Append(championName);

            // Description and stats from champion card
            if (cardData != null)
            {
                AppendCardDescription(sb, cardData);
                AppendCharacterStats(sb, cardData);
            }

            // Starter card
            var starterCard = champType.GetField("starterCardData", bindFlags)?.GetValue(championData);
            AppendStarterCard(sb, starterCard);

            if (isLocked)
                sb.Append(" (Locked)");

            sb.Append(". Use Left and Right arrows to change champion.");
            return sb.ToString();
        }

        private static string GetNameFromData(object cardData)
        {
            if (cardData == null) return null;
            var method = cardData.GetType().GetMethod("GetName", Type.EmptyTypes);
            return method?.Invoke(cardData, null) as string;
        }

        private static void AppendCardDescription(StringBuilder sb, object cardData)
        {
            var method = cardData.GetType().GetMethod("GetDescription", Type.EmptyTypes);
            var desc = method?.Invoke(cardData, null) as string;
            if (!string.IsNullOrEmpty(desc))
            {
                sb.Append(". ");
                sb.Append(TextUtilities.StripRichTextTags(desc));
            }
        }

        private static void AppendCharacterStats(StringBuilder sb, object cardData)
        {
            var getCharDataMethod = cardData.GetType().GetMethod("GetSpawnCharacterData", Type.EmptyTypes);
            var charData = getCharDataMethod?.Invoke(cardData, null);
            if (charData == null) return;

            var charType = charData.GetType();
            int attack = InvokeIntMethod(charType, charData, "GetAttackDamage");
            int health = InvokeIntMethod(charType, charData, "GetHealth");
            if (health < 0)
                health = InvokeIntMethod(charType, charData, "GetHP");
            int size = InvokeIntMethod(charType, charData, "GetSize");

            if (attack < 0 && health < 0 && size < 0) return;

            sb.Append(". Stats: ");
            var parts = new List<string>();
            if (attack >= 0) parts.Add($"{attack} attack");
            if (health >= 0) parts.Add($"{health} health");
            if (size >= 0) parts.Add($"size {size}");
            sb.Append(string.Join(", ", parts));
        }

        private static int InvokeIntMethod(Type type, object instance, string methodName)
        {
            var method = type.GetMethod(methodName, Type.EmptyTypes);
            if (method != null && method.Invoke(instance, null) is int value)
                return value;
            return -1;
        }

        private static void AppendStarterCard(StringBuilder sb, object starterCard)
        {
            string starterName = GetNameFromData(starterCard);
            if (string.IsNullOrEmpty(starterName)) return;

            sb.Append($". Starter card: {starterName}");
            var method = starterCard.GetType().GetMethod("GetDescription", Type.EmptyTypes);
            var desc = method?.Invoke(starterCard, null) as string;
            if (!string.IsNullOrEmpty(desc))
            {
                sb.Append(". ");
                sb.Append(TextUtilities.StripRichTextTags(desc));
            }
        }


        /// <summary>
        /// Get text for covenant selector buttons (difficulty selection)
        /// </summary>
        internal static string GetCovenantSelectorText(GameObject go)
        {
            try
            {
                // Find CovenantSelectionUI on this object or parents
                Component covenantUI = null;

                Transform current = go.transform;
                while (current != null && covenantUI == null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;

                        if (typeName == "CovenantSelectionUI")
                        {
                            covenantUI = component;
                            break;
                        }
                    }
                    current = current.parent;
                }

                if (covenantUI == null)
                    return null;

                var uiType = covenantUI.GetType();

                // Read currentLevel and maxLevel fields
                int covenantLevel = -1;
                int maxLevel = -1;

                var levelField = uiType.GetField("currentLevel", BindingFlags.NonPublic | BindingFlags.Instance);
                if (levelField != null)
                {
                    var val = levelField.GetValue(covenantUI);
                    if (val != null) covenantLevel = Convert.ToInt32(val);
                }

                var maxField = uiType.GetField("maxLevel", BindingFlags.NonPublic | BindingFlags.Instance);
                if (maxField != null)
                {
                    var val = maxField.GetValue(covenantUI);
                    if (val != null) maxLevel = Convert.ToInt32(val);
                }

                var sb = new StringBuilder();

                // When maxLevel is 0, covenant is locked - labels contain placeholder text
                if (maxLevel <= 0)
                {
                    sb.Append("Covenant: Locked. Win a run to unlock covenant ranks.");
                    return sb.ToString();
                }

                // Covenant is unlocked - read the actual UI labels
                // Read the levelLabel TMP_Text for the displayed level name
                string levelLabelText = null;
                var levelLabelField = uiType.GetField("levelLabel", BindingFlags.NonPublic | BindingFlags.Instance);
                if (levelLabelField != null)
                {
                    var levelLabel = levelLabelField.GetValue(covenantUI);
                    if (levelLabel != null)
                    {
                        var textProp = levelLabel.GetType().GetProperty("text");
                        if (textProp != null)
                            levelLabelText = textProp.GetValue(levelLabel) as string;
                    }
                }

                // Read the descriptionLabel TMP_Text for the covenant description
                string descriptionText = null;
                var descLabelField = uiType.GetField("descriptionLabel", BindingFlags.NonPublic | BindingFlags.Instance);
                if (descLabelField != null)
                {
                    var descLabel = descLabelField.GetValue(covenantUI);
                    if (descLabel != null)
                    {
                        var textProp = descLabel.GetType().GetProperty("text");
                        if (textProp != null)
                            descriptionText = textProp.GetValue(descLabel) as string;
                    }
                }

                // Build announcement
                if (!string.IsNullOrEmpty(levelLabelText))
                {
                    sb.Append($"Covenant: {TextUtilities.StripRichTextTags(levelLabelText)}");
                }
                else
                {
                    sb.Append($"Covenant level {covenantLevel}");
                }

                sb.Append($". Maximum unlocked: {maxLevel}");

                if (!string.IsNullOrEmpty(descriptionText))
                {
                    sb.Append(". ");
                    sb.Append(TextUtilities.StripRichTextTags(descriptionText));
                }

                sb.Append(". Use Left and Right arrows to change level.");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting covenant selector text: {ex.Message}");
            }
            return null;
        }


        /// <summary>
        /// Get text for DLC toggle (Last Divinity / Hellforged toggle on main menu)
        /// </summary>
        internal static string GetDLCToggleText(GameObject go)
        {
            try
            {
                // Check ancestors for DLC toggle indicators - walk up to 4 levels
                bool isDLCToggle = false;
                Component dlcToggleComponent = null;
                Transform current = go.transform;
                for (int i = 0; i < 5 && current != null; i++)
                {
                    string name = current.name.ToLower();
                    if (name.Contains("dlc") || name.Contains("divinity") ||
                        name.Contains("hellforged") || name.Contains("pact") ||
                        name.Contains("expansion"))
                    {
                        isDLCToggle = true;
                    }

                    // Also check for DLCToggle component
                    if (dlcToggleComponent == null)
                    {
                        foreach (var comp in current.GetComponents<Component>())
                        {
                            if (comp != null && comp.GetType().Name == "DLCToggle")
                            {
                                isDLCToggle = true;
                                dlcToggleComponent = comp;
                                break;
                            }
                        }
                    }

                    if (isDLCToggle) break;
                    current = current.parent;
                }

                if (!isDLCToggle)
                    return null;

                // Get the checkbox state from GameUISelectableCheckbox on the focused element
                bool? isOn = null;
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    string typeName = type.Name;

                    if (typeName.Contains("Checkbox") || typeName.Contains("Toggle"))
                    {
                        // Try isChecked property (GameUISelectableCheckbox has this)
                        var isCheckedProp = type.GetProperty("isChecked");
                        if (isCheckedProp != null)
                        {
                            isOn = (bool)isCheckedProp.GetValue(component);
                            break;
                        }

                        // Try _isChecked field
                        var isCheckedField = type.GetField("_isChecked", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (isCheckedField != null && isCheckedField.FieldType == typeof(bool))
                        {
                            isOn = (bool)isCheckedField.GetValue(component);
                            break;
                        }

                        // Try isOn property
                        var isOnProp = type.GetProperty("isOn");
                        if (isOnProp != null && isOnProp.PropertyType == typeof(bool))
                        {
                            isOn = (bool)isOnProp.GetValue(component);
                            break;
                        }
                    }
                }

                // If still no state, try getting it from the DLCToggle component's checkboxButton field
                if (!isOn.HasValue && dlcToggleComponent != null)
                {
                    var cbField = dlcToggleComponent.GetType().GetField("checkboxButton",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (cbField != null)
                    {
                        var checkbox = cbField.GetValue(dlcToggleComponent);
                        if (checkbox != null)
                        {
                            var isCheckedProp = checkbox.GetType().GetProperty("isChecked");
                            if (isCheckedProp != null)
                            {
                                isOn = (bool)isCheckedProp.GetValue(checkbox);
                            }
                        }
                    }
                }

                string state = isOn.HasValue ? (isOn.Value ? "enabled" : "disabled") : "toggle";
                return $"The Last Divinity: {state}";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting DLC toggle text: {ex.Message}");
            }
            return null;
        }

    }
}
