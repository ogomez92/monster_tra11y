using MonsterTrainAccessibility.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using MonsterTrainAccessibility.Utilities;
using UnityEngine.UI;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Handles accessibility for menus by reading Unity's EventSystem selection.
    /// Instead of maintaining a fake menu, we track what the game has selected
    /// and read the text from the actual UI elements.
    /// </summary>
    public class MenuAccessibility : MonoBehaviour
    {
        private GameObject _lastSelectedObject;
        private float _pollInterval = 0.1f;
        private float _pollTimer = 0f;
        private bool _isActive = true;

        // Text content monitoring
        private string _lastScrollContent = null;
        private string _lastScreenTextHash = null;
        private float _textCheckInterval = 0.5f;
        private float _textCheckTimer = 0f;

        // Upgrade selection screen tracking
        private bool _upgradeScreenHelperAnnounced = false;
        private string _lastUpgradeScreenCheck = null;

        // Dialog tracking - to avoid re-announcing dialog text when navigating between buttons
        private string _lastAnnouncedDialogText = null;
        // Track last screen to clear dialog cache when screen changes
        private Help.GameScreen _lastTrackedScreen = Help.GameScreen.Unknown;

        // Covenant level tracking - to detect value changes on same focused element
        private int _lastCovenantLevel = -1;
        private Component _lastCovenantUI = null;

        // Blacklist of panel names that should be ignored when scanning for text
        private static readonly HashSet<string> _panelBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "quitconfirmation", "exitdialog", "confirmdialog", "confirmationpopup",
            "quitpanel", "exitpanel", "confirmquit", "quitgame", "exitgame"
        };

        private void Update()
        {
            if (!_isActive)
                return;

            // Clear dialog cache when screen changes to avoid stale dialog text
            var currentScreen = Help.ScreenStateTracker.CurrentScreen;
            if (currentScreen != _lastTrackedScreen)
            {
                MonsterTrainAccessibility.LogInfo($"Screen changed from {_lastTrackedScreen} to {currentScreen} - clearing dialog cache");
                _lastAnnouncedDialogText = null;
                _lastDialogComponent = null;
                _lastTrackedScreen = currentScreen;
            }

            _pollTimer += Time.unscaledDeltaTime;
            _textCheckTimer += Time.unscaledDeltaTime;

            // Check selection changes frequently
            if (_pollTimer >= _pollInterval)
            {
                _pollTimer = 0f;
                CheckForSelectionChange();
            }

            // Check for text content changes less frequently
            if (_textCheckTimer >= _textCheckInterval)
            {
                _textCheckTimer = 0f;
                CheckForTextChanges();
            }
        }

        /// <summary>
        /// Check if any monitored text content has changed
        /// </summary>
        private void CheckForTextChanges()
        {
            try
            {
                // If we're focused on a scrollbar or scroll area, monitor its content
                if (_lastSelectedObject != null && IsScrollbar(_lastSelectedObject))
                {
                    string currentContent = GetScrollViewContentText(_lastSelectedObject);
                    if (!string.IsNullOrEmpty(currentContent) && currentContent != _lastScrollContent)
                    {
                        _lastScrollContent = currentContent;

                        // Announce the new content (truncated for initial announcement)
                        string announcement = currentContent;
                        if (announcement.Length > 500)
                        {
                            announcement = announcement.Substring(0, 500) + "...";
                        }
                        MonsterTrainAccessibility.ScreenReader?.Speak(announcement, false);
                    }
                }

                // Check for covenant level changes (value changes without focus change)
                CheckForCovenantLevelChange();

                // Also check for any new large text panels appearing
                CheckForNewTextPanels();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error checking text changes: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if the covenant level has changed while the same element stays focused.
        /// The game changes the level on Left/Right but doesn't change focus.
        /// </summary>
        private void CheckForCovenantLevelChange()
        {
            try
            {
                if (_lastSelectedObject == null) return;

                // Find CovenantSelectionUI on or above the focused element
                Component covenantUI = FindCovenantUI(_lastSelectedObject);
                if (covenantUI == null)
                {
                    _lastCovenantUI = null;
                    _lastCovenantLevel = -1;
                    return;
                }

                // Read current level via GetLevel() method or currentLevel field
                int currentLevel = -1;
                var uiType = covenantUI.GetType();

                var getLevelMethod = uiType.GetMethod("GetLevel", Type.EmptyTypes);
                if (getLevelMethod != null)
                {
                    var result = getLevelMethod.Invoke(covenantUI, null);
                    if (result is int lvl) currentLevel = lvl;
                }

                if (currentLevel < 0)
                {
                    var levelField = uiType.GetField("currentLevel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (levelField != null)
                    {
                        var val = levelField.GetValue(covenantUI);
                        if (val != null) currentLevel = Convert.ToInt32(val);
                    }
                }

                // If the level changed, announce
                if (currentLevel >= 0 && (covenantUI != _lastCovenantUI || currentLevel != _lastCovenantLevel))
                {
                    if (_lastCovenantUI != null && _lastCovenantLevel >= 0 && _lastCovenantLevel != currentLevel)
                    {
                        // Read the full covenant text and announce
                        string text = ClanSelectionTextReader.GetCovenantSelectorText(_lastSelectedObject);
                        if (!string.IsNullOrEmpty(text))
                        {
                            text = TextUtilities.CleanSpriteTagsForSpeech(text);
                            MonsterTrainAccessibility.ScreenReader?.Speak(text, false);
                        }
                    }
                    _lastCovenantUI = covenantUI;
                    _lastCovenantLevel = currentLevel;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error checking covenant level: {ex.Message}");
            }
        }

        /// <summary>
        /// Find a CovenantSelectionUI component on or above the given GameObject
        /// </summary>
        private Component FindCovenantUI(GameObject go)
        {
            Transform current = go.transform;
            while (current != null)
            {
                foreach (var component in current.GetComponents<Component>())
                {
                    if (component != null && component.GetType().Name == "CovenantSelectionUI")
                        return component;
                }
                current = current.parent;
            }
            return null;
        }

        /// <summary>
        /// Check if new text panels have appeared (dialogs, popups, etc.)
        /// </summary>
        private void CheckForNewTextPanels()
        {
            try
            {
                // First, check for tutorial panels specifically (highest priority)
                CheckForTutorialText();

                // Check for upgrade selection screen
                CheckForUpgradeSelectionScreen();

                // Get a hash of current visible text to detect changes
                var sb = new StringBuilder();
                var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

                foreach (var root in rootObjects)
                {
                    if (root.activeInHierarchy)
                    {
                        // Look for popup/dialog/panel objects that might contain text
                        FindLargeTextContent(root.transform, sb);
                    }
                }

                string currentHash = sb.ToString();

                // If content changed significantly, announce it
                if (!string.IsNullOrEmpty(currentHash) && currentHash != _lastScreenTextHash)
                {
                    // Only announce if there's substantial new content
                    if (_lastScreenTextHash == null ||
                        currentHash.Length > _lastScreenTextHash.Length + 50)
                    {
                        _lastScreenTextHash = currentHash;

                        // Find what's new and announce it
                        string newContent = currentHash;
                        if (_lastScreenTextHash != null && currentHash.StartsWith(_lastScreenTextHash))
                        {
                            newContent = currentHash.Substring(_lastScreenTextHash.Length);
                        }

                        if (newContent.Length > 50) // Only announce substantial text
                        {
                            if (newContent.Length > 500)
                            {
                                newContent = newContent.Substring(0, 500) + "...";
                            }
                            MonsterTrainAccessibility.ScreenReader?.Queue(newContent.Trim());
                        }
                    }
                    _lastScreenTextHash = currentHash;
                }
            }
            catch { }
        }

        /// <summary>
        /// Check for and announce tutorial text when it appears
        /// </summary>
        private void CheckForTutorialText()
        {
            try
            {
                string newTutorialText = Help.Contexts.TutorialHelp.CheckForNewTutorialText();
                if (!string.IsNullOrEmpty(newTutorialText))
                {
                    // Announce tutorial text with "Tutorial:" prefix
                    string announcement = "Tutorial: " + newTutorialText;
                    if (announcement.Length > 600)
                    {
                        announcement = announcement.Substring(0, 600) + "...";
                    }
                    MonsterTrainAccessibility.ScreenReader?.Queue(announcement);
                    MonsterTrainAccessibility.LogInfo("Tutorial text announced");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error checking tutorial text: {ex.Message}");
            }
        }

        /// <summary>
        /// Check for upgrade selection screen (when player needs to select a card to upgrade)
        /// </summary>
        private void CheckForUpgradeSelectionScreen()
        {
            try
            {
                // Look for upgrade selection screen indicators
                var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

                foreach (var root in rootObjects)
                {
                    if (!root.activeInHierarchy) continue;

                    // Look for screens that indicate card upgrade selection
                    string screenId = FindUpgradeSelectionScreen(root.transform);
                    if (!string.IsNullOrEmpty(screenId))
                    {
                        // Check if this is a new screen (not the same we already announced)
                        if (screenId != _lastUpgradeScreenCheck)
                        {
                            _lastUpgradeScreenCheck = screenId;
                            _upgradeScreenHelperAnnounced = false;
                        }

                        // Announce helper if not already done
                        if (!_upgradeScreenHelperAnnounced)
                        {
                            _upgradeScreenHelperAnnounced = true;
                            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Select Card to Upgrade");
                            MonsterTrainAccessibility.ScreenReader?.Queue("Use arrow keys to browse cards and press Enter to apply the upgrade.");
                            MonsterTrainAccessibility.LogInfo("Upgrade selection screen detected");
                        }
                        return;
                    }
                }

                // If no upgrade screen found, reset the tracking
                if (_lastUpgradeScreenCheck != null)
                {
                    _lastUpgradeScreenCheck = null;
                    _upgradeScreenHelperAnnounced = false;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error checking upgrade selection screen: {ex.Message}");
            }
        }

        /// <summary>
        /// Find upgrade selection screen by looking for characteristic UI elements
        /// </summary>
        private string FindUpgradeSelectionScreen(Transform root)
        {
            // Look for GameObjects with names indicating upgrade selection
            var upgradeIndicators = new[] {
                "UpgradeSelectionScreen", "EnhancerSelectionScreen", "CardUpgradeScreen",
                "UpgradeScreen", "CardSelection", "SelectCardScreen",
                "UpgradeCardSelection", "EnhancerCardList", "CardListScreen",
                "UpgradeCards", "CardPicker", "CardChoiceScreen"
            };

            foreach (var indicator in upgradeIndicators)
            {
                var found = FindChildRecursive(root, indicator);
                if (found != null && found.gameObject.activeInHierarchy)
                {
                    MonsterTrainAccessibility.LogInfo($"Found upgrade indicator: {indicator}");
                    return indicator + "_" + found.GetInstanceID();
                }
            }

            // Check all components to find CardUI elements and upgrade-related text
            var allComponents = root.GetComponentsInChildren<Component>(false);

            // Count active CardUI elements
            var cardUIs = allComponents
                .Where(c => c.GetType().Name == "CardUI" && c.gameObject.activeInHierarchy)
                .ToList();

            // If we have multiple cards visible and we're not in battle, this might be upgrade selection
            if (cardUIs.Count >= 3)
            {
                // Check we're not in battle (don't have floor/room elements active)
                bool inBattle = allComponents.Any(c =>
                    (c.GetType().Name.Contains("RoomState") || c.GetType().Name.Contains("CombatManager"))
                    && c.gameObject.activeInHierarchy);

                if (!inBattle)
                {
                    // Look for any upgrade-related text
                    foreach (var comp in allComponents)
                    {
                        if (!comp.gameObject.activeInHierarchy) continue;

                        string typeName = comp.GetType().Name;
                        if (typeName.Contains("Text"))
                        {
                            string content = null;
                            var textProp = comp.GetType().GetProperty("text");
                            if (textProp != null)
                            {
                                content = textProp.GetValue(comp) as string;
                            }

                            if (!string.IsNullOrEmpty(content))
                            {
                                string lowerContent = content.ToLower();
                                // Look for upgrade-related keywords
                                if (lowerContent.Contains("upgrade") ||
                                    lowerContent.Contains("choose") ||
                                    lowerContent.Contains("select") ||
                                    lowerContent.Contains("apply") ||
                                    lowerContent.Contains("spell") && lowerContent.Contains("unit"))
                                {
                                    MonsterTrainAccessibility.LogInfo($"Found upgrade text: {content.Substring(0, Math.Min(50, content.Length))}");
                                    return "UpgradeScreen_Cards_" + cardUIs.Count;
                                }
                            }
                        }
                    }

                    // Even without specific text, multiple cards outside battle suggests upgrade selection
                    // Check if we're in shop context (GameScreen.Shop or just left shop)
                    if (Help.ScreenStateTracker.CurrentScreen == Help.GameScreen.Shop ||
                        Help.ScreenStateTracker.CurrentScreen == Help.GameScreen.Map)
                    {
                        MonsterTrainAccessibility.LogInfo($"Multiple cards ({cardUIs.Count}) detected outside battle - likely upgrade screen");
                        return "UpgradeScreen_MultiCard_" + cardUIs.Count;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Find a child transform by name (case-insensitive, partial match)
        /// </summary>
        private Transform FindChildRecursive(Transform parent, string nameContains)
        {
            string lowerName = nameContains.ToLower();
            foreach (Transform child in parent)
            {
                if (child.name.ToLower().Contains(lowerName))
                    return child;

                var found = FindChildRecursive(child, nameContains);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Check if a GameObject is actually visible (not hidden by CanvasGroup or blacklisted)
        /// </summary>
        private bool IsActuallyVisible(GameObject go)
        {
            if (go == null || !go.activeInHierarchy)
                return false;

            // Check if this object or any parent is blacklisted
            Transform current = go.transform;
            while (current != null)
            {
                string objName = current.name.Replace(" ", "").Replace("_", "");
                if (_panelBlacklist.Contains(objName))
                    return false;

                // Check for CanvasGroup with alpha = 0 (hidden) via reflection
                if (IsHiddenByCanvasGroup(current.gameObject))
                    return false;

                // Check for Canvas that's disabled via reflection
                if (IsCanvasDisabled(current.gameObject))
                    return false;

                // Check for Dialog component that's not showing
                if (IsHiddenDialog(current.gameObject))
                    return false;

                current = current.parent;
            }

            return true;
        }

        /// <summary>
        /// Check if a GameObject is hidden by CanvasGroup (alpha = 0) via reflection
        /// </summary>
        private bool IsHiddenByCanvasGroup(GameObject go)
        {
            try
            {
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.Name == "CanvasGroup")
                    {
                        var alphaProp = type.GetProperty("alpha");
                        if (alphaProp != null)
                        {
                            float alpha = (float)alphaProp.GetValue(component);
                            if (alpha <= 0.01f)
                                return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if a Canvas component is disabled via reflection
        /// </summary>
        private bool IsCanvasDisabled(GameObject go)
        {
            try
            {
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.Name == "Canvas")
                    {
                        var enabledProp = type.GetProperty("enabled");
                        if (enabledProp != null)
                        {
                            bool enabled = (bool)enabledProp.GetValue(component);
                            if (!enabled)
                                return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if a GameObject has a Dialog component that's not currently showing
        /// </summary>
        private bool IsHiddenDialog(GameObject go)
        {
            try
            {
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.Name == "Dialog")
                    {
                        // Log all fields and properties to discover the visibility indicator
                        var sb = new StringBuilder();
                        sb.AppendLine($"=== Dialog component fields/properties on {go.name} ===");

                        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            try
                            {
                                var value = field.GetValue(component);
                                sb.AppendLine($"  Field: {field.Name} ({field.FieldType.Name}) = {value}");
                            }
                            catch { }
                        }

                        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            try
                            {
                                if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                                {
                                    var value = prop.GetValue(component);
                                    sb.AppendLine($"  Property: {prop.Name} ({prop.PropertyType.Name}) = {value}");
                                }
                            }
                            catch { }
                        }

                        MonsterTrainAccessibility.LogInfo(sb.ToString());

                        // Try common visibility properties/methods on Dialog
                        // Check for IsShowing property
                        var isShowingProp = type.GetProperty("IsShowing", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (isShowingProp != null)
                        {
                            bool isShowing = (bool)isShowingProp.GetValue(component);
                            if (!isShowing)
                                return true;
                        }

                        // Check for isShowing field
                        var isShowingField = type.GetField("isShowing", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (isShowingField != null)
                        {
                            bool isShowing = (bool)isShowingField.GetValue(component);
                            if (!isShowing)
                                return true;
                        }

                        // Check for showing field
                        var showingField = type.GetField("showing", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (showingField != null)
                        {
                            bool showing = (bool)showingField.GetValue(component);
                            if (!showing)
                                return true;
                        }

                        // Check for isOpen property
                        var isOpenProp = type.GetProperty("isOpen", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (isOpenProp != null)
                        {
                            bool isOpen = (bool)isOpenProp.GetValue(component);
                            if (!isOpen)
                                return true;
                        }

                        // Check if the overlay Image has alpha = 0 (dialog hidden)
                        var overlayField = type.GetField("overlay", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (overlayField != null)
                        {
                            var overlay = overlayField.GetValue(component);
                            if (overlay != null)
                            {
                                var overlayType = overlay.GetType();
                                var colorProp = overlayType.GetProperty("color", BindingFlags.Public | BindingFlags.Instance);
                                if (colorProp != null)
                                {
                                    var color = colorProp.GetValue(overlay);
                                    if (color != null)
                                    {
                                        var aField = color.GetType().GetField("a", BindingFlags.Public | BindingFlags.Instance);
                                        if (aField != null)
                                        {
                                            float alpha = (float)aField.GetValue(color);
                                            MonsterTrainAccessibility.LogInfo($"Dialog overlay alpha: {alpha}");
                                            if (alpha <= 0.01f)
                                                return true;
                                        }
                                    }
                                }

                                // Also check if the overlay GameObject is inactive
                                var overlayGoProp = overlayType.GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance);
                                if (overlayGoProp != null)
                                {
                                    var overlayGo = overlayGoProp.GetValue(overlay) as GameObject;
                                    if (overlayGo != null && !overlayGo.activeInHierarchy)
                                    {
                                        MonsterTrainAccessibility.LogInfo("Dialog overlay is inactive");
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Get text for buttons inside Dialog popups (Yes/No confirmation dialogs).
        /// Returns the dialog content text along with the button label.
        /// </summary>
        private string GetDialogButtonText(GameObject go)
        {
            try
            {
                // Check if this looks like a dialog button
                string goName = go.name.ToLower();
                bool isDialogButton = goName.Contains("button") &&
                    (goName.Contains("yes") || goName.Contains("no") || goName.Contains("ok") ||
                     goName.Contains("cancel") || goName.Contains("confirm"));

                if (!isDialogButton)
                    return null;

                // Find the root dialog/popup container by walking up
                Transform dialogRoot = FindDialogRoot(go.transform);
                if (dialogRoot == null)
                {
                    MonsterTrainAccessibility.LogInfo("Could not find dialog root");
                    return null;
                }

                MonsterTrainAccessibility.LogInfo($"Found dialog root: {dialogRoot.name}");

                // Search for the dialog question text - look for visible TMP text that's NOT on buttons
                string dialogText = FindVisibleDialogText(dialogRoot.gameObject, go);

                if (string.IsNullOrEmpty(dialogText))
                {
                    MonsterTrainAccessibility.LogInfo("Could not find dialog content text");
                    return null;
                }

                // Strip rich text tags
                dialogText = TextUtilities.StripRichTextTags(dialogText.Trim());

                // Get the button label
                string buttonLabel = GetDirectText(go);
                // If text is short (1-2 chars like icon "A"), use the cleaned GameObject name instead
                if (string.IsNullOrEmpty(buttonLabel) || buttonLabel.Length <= 2)
                {
                    buttonLabel = UITextHelper.CleanGameObjectName(go.name);
                }

                // Check if this is the same dialog we already announced
                if (_lastAnnouncedDialogText == dialogText)
                {
                    MonsterTrainAccessibility.LogInfo($"Dialog button (same dialog): '{buttonLabel}'");
                    return buttonLabel;
                }

                // New dialog - announce full text and remember it
                _lastAnnouncedDialogText = dialogText;
                MonsterTrainAccessibility.LogInfo($"Dialog button detected (new): '{dialogText}' - Button: '{buttonLabel}'");

                return $"Dialog: {dialogText}. {buttonLabel}";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting dialog button text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Find the root container of a dialog by walking up from a button.
        /// Searches for the nearest ancestor that contains the dialog question text.
        /// </summary>
        private Transform FindDialogRoot(Transform buttonTransform)
        {
            // Find Dialog component and read from data.content
            Transform searchParent = buttonTransform;
            while (searchParent != null)
            {
                foreach (var comp in searchParent.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == "Dialog")
                    {
                        // Found Dialog - store it and return this transform
                        _lastDialogComponent = comp;
                        return searchParent;
                    }
                }
                searchParent = searchParent.parent;
            }

            // Fallback: return the button's grandparent
            return buttonTransform.parent?.parent;
        }

        // Cache the last Dialog component found
        private Component _lastDialogComponent = null;

        /// <summary>
        /// Get the dialog content text from Dialog.data.content field.
        /// </summary>
        private string GetDialogDataContent(Component dialogComponent)
        {
            if (dialogComponent == null)
                return null;

            try
            {
                var dataField = dialogComponent.GetType().GetField("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (dataField == null)
                    return null;

                var data = dataField.GetValue(dialogComponent);
                if (data == null)
                    return null;

                var contentField = data.GetType().GetField("content", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (contentField != null)
                {
                    var content = contentField.GetValue(data);
                    if (content != null)
                    {
                        return content.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting dialog data content: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Find visible dialog text - first try Dialog.data.content, then search children.
        /// </summary>
        private string FindVisibleDialogText(GameObject dialogRoot, GameObject excludeButton)
        {
            // First, try to get text from Dialog.data.content (the actual current dialog content)
            if (_lastDialogComponent != null)
            {
                string dataContent = GetDialogDataContent(_lastDialogComponent);
                if (!string.IsNullOrEmpty(dataContent) && !DialogTextReader.IsPlaceholderText(dataContent))
                {
                    MonsterTrainAccessibility.LogInfo($"Got dialog text from data.content: '{dataContent}'");
                    return dataContent;
                }
            }

            // Fallback: search TMP text in children
            try
            {
                string bestText = null;
                int bestLength = 0;

                var allTransforms = dialogRoot.GetComponentsInChildren<Transform>(false);

                foreach (var child in allTransforms)
                {
                    if (!child.gameObject.activeInHierarchy)
                        continue;

                    if (IsInsideButton(child, dialogRoot.transform))
                        continue;

                    string text = UITextHelper.GetTMPTextDirect(child.gameObject);
                    if (string.IsNullOrEmpty(text))
                        continue;

                    text = text.Trim();
                    if (text.Length < 10)
                        continue;

                    string lower = text.ToLower();
                    if (lower == "yes" || lower == "no" || lower == "ok" || lower == "cancel")
                        continue;

                    bool hasQuestion = text.Contains("?");
                    int score = text.Length + (hasQuestion ? 1000 : 0);

                    if (score > bestLength)
                    {
                        bestText = text;
                        bestLength = score;
                    }
                }

                return bestText;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error finding visible dialog text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Check if a transform is inside a button element.
        /// </summary>
        private bool IsInsideButton(Transform t, Transform root)
        {
            Transform current = t;
            while (current != null && current != root)
            {
                string name = current.name.ToLower();
                if (name.Contains("button") || name.Contains("yes") || name.Contains("no") ||
                    name.Contains("ok") || name.Contains("cancel") || name.Contains("confirm"))
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Find large text content in dialogs/popups
        /// </summary>
        private void FindLargeTextContent(Transform transform, StringBuilder sb)
        {
            if (transform == null || !IsActuallyVisible(transform.gameObject)) return;

            string name = transform.name.ToLower();

            // Look for common dialog/popup/panel names
            bool isTextPanel = name.Contains("dialog") || name.Contains("popup") ||
                              name.Contains("panel") || name.Contains("modal") ||
                              name.Contains("tooltip") || name.Contains("description") ||
                              name.Contains("content") || name.Contains("text");

            if (isTextPanel)
            {
                // Get all text from this panel
                string text = UITextHelper.GetTMPText(transform.gameObject);
                if (!string.IsNullOrEmpty(text) && text.Length > 20)
                {
                    sb.AppendLine(text);
                }

                var uiText = transform.GetComponent<Text>();
                if (uiText != null && !string.IsNullOrEmpty(uiText.text) && uiText.text.Length > 20)
                {
                    sb.AppendLine(uiText.text);
                }
            }

            // Recurse into children
            foreach (Transform child in transform)
            {
                FindLargeTextContent(child, sb);
            }
        }

        /// <summary>
        /// Check if the game's UI selection has changed and announce it
        /// </summary>
        private void CheckForSelectionChange()
        {
            if (EventSystem.current == null)
                return;

            GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

            // Selection changed?
            if (currentSelected != _lastSelectedObject)
            {
                _lastSelectedObject = currentSelected;

                // Reset scroll content tracking when selection changes
                _lastScrollContent = null;

                // Check if we should deactivate targeting systems
                DeactivateTargetingIfNeeded(currentSelected);

                if (currentSelected != null && IsActuallyVisible(currentSelected))
                {
                    // Check if this is floor targeting mode (Tower Selectable = floor selection for playing cards)
                    if (currentSelected.name.Contains("Tower") && currentSelected.name.Contains("Selectable"))
                    {
                        // Activate floor targeting system
                        ActivateFloorTargeting();
                        return;
                    }

                    // Unit targeting is handled by CardTargetingPatches which hooks into
                    // the game's native CardSelectionBehaviour.MoveTargetWithKeyboard and
                    // SelectCardInternal methods. No need to activate a separate system here.

                    // Check if we're still in a dialog context - if not, clear the tracking
                    // so the dialog text will be announced again if the same dialog appears
                    if (!DialogTextReader.IsInDialogContext(currentSelected))
                    {
                        _lastAnnouncedDialogText = null;
                    }

                    string text = GetTextFromGameObject(currentSelected);
                    if (!string.IsNullOrEmpty(text))
                    {
                        // Clean sprite tags before announcing
                        text = TextUtilities.CleanSpriteTagsForSpeech(text);
                        MonsterTrainAccessibility.ScreenReader?.AnnounceFocus(text);
                    }

                    // If this is a scroll area, remember the initial content
                    if (IsScrollbar(currentSelected))
                    {
                        _lastScrollContent = GetScrollViewContentText(currentSelected);
                    }
                }
            }
        }

        /// <summary>
        /// Check if the current element indicates unit targeting mode
        /// </summary>
        private bool IsUnitTargetingElement(GameObject go)
        {
            string name = go.name.ToLower();

            // Common patterns for unit targeting UI elements
            if (name.Contains("character") && name.Contains("select"))
                return true;
            if (name.Contains("target") && (name.Contains("select") || name.Contains("overlay")))
                return true;
            if (name.Contains("unit") && name.Contains("select"))
                return true;

            // Check for CharacterUI or similar components that might indicate targeting
            var components = go.GetComponents<Component>();
            foreach (var comp in components)
            {
                string compName = comp.GetType().Name.ToLower();
                if (compName.Contains("charactertarget") || compName.Contains("targetselect"))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Activate floor targeting mode when the game enters floor selection
        /// </summary>
        private void ActivateFloorTargeting()
        {
            var targeting = MonsterTrainAccessibility.FloorTargeting;
            if (targeting != null && !targeting.IsTargeting)
            {
                MonsterTrainAccessibility.LogInfo("Detected floor selection mode, activating FloorTargetingSystem");
                // Start targeting without callbacks - we'll let the game handle actual card playing
                // Our system just provides audio feedback for floor navigation
                targeting.StartTargeting(null, (floor) => {
                    // User confirmed floor - do nothing, game handles it
                    MonsterTrainAccessibility.LogInfo($"Floor {floor} confirmed by user");
                }, () => {
                    // User cancelled - do nothing, game handles it
                    MonsterTrainAccessibility.LogInfo("Floor targeting cancelled by user");
                });
            }
        }

        /// <summary>
        /// Activate unit targeting mode when the game enters unit selection
        /// </summary>
        private void ActivateUnitTargeting()
        {
            var targeting = MonsterTrainAccessibility.UnitTargeting;
            if (targeting != null && !targeting.IsTargeting)
            {
                MonsterTrainAccessibility.LogInfo("Detected unit selection mode, activating UnitTargetingSystem");
                targeting.StartTargeting(null, (index) => {
                    MonsterTrainAccessibility.LogInfo($"Unit {index} confirmed by user");
                }, () => {
                    MonsterTrainAccessibility.LogInfo("Unit targeting cancelled by user");
                });
            }
        }

        /// <summary>
        /// Deactivate targeting systems if the selection has moved away from targeting elements
        /// </summary>
        private void DeactivateTargetingIfNeeded(GameObject currentSelected)
        {
            // Check floor targeting
            var floorTargeting = MonsterTrainAccessibility.FloorTargeting;
            if (floorTargeting != null && floorTargeting.IsTargeting)
            {
                // If we're no longer on a tower selectable, deactivate floor targeting
                if (currentSelected == null ||
                    !(currentSelected.name.Contains("Tower") && currentSelected.name.Contains("Selectable")))
                {
                    floorTargeting.ForceCancel();
                    MonsterTrainAccessibility.LogInfo("Floor targeting deactivated - selection moved away");
                }
            }

            // Unit targeting is now handled by CardTargetingPatches - no manual deactivation needed
        }

        /// <summary>
        /// Get information about a targeted unit from the game's targeting UI
        /// </summary>
        private string GetUnitTargetInfo(GameObject targetElement)
        {
            if (targetElement == null) return null;

            try
            {
                MonsterTrainAccessibility.LogInfo($"Getting unit target info from: {targetElement.name}");

                // Try to find CharacterState component or reference in the targeting UI
                var components = targetElement.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    var compType = comp.GetType();
                    MonsterTrainAccessibility.LogInfo($"  Component: {compType.Name}");

                    // Look for CharacterState field/property
                    var characterField = compType.GetField("character", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                      ?? compType.GetField("_character", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                      ?? compType.GetField("characterState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                      ?? compType.GetField("_characterState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (characterField != null)
                    {
                        var characterState = characterField.GetValue(comp);
                        if (characterState != null)
                        {
                            return GetUnitDescription(characterState);
                        }
                    }

                    // Try GetCharacter method
                    var getCharacterMethod = compType.GetMethod("GetCharacter", Type.EmptyTypes)
                                          ?? compType.GetMethod("GetCharacterState", Type.EmptyTypes);
                    if (getCharacterMethod != null)
                    {
                        var characterState = getCharacterMethod.Invoke(comp, null);
                        if (characterState != null)
                        {
                            return GetUnitDescription(characterState);
                        }
                    }
                }

                // Check parent objects for character state
                Transform parent = targetElement.transform.parent;
                while (parent != null)
                {
                    var parentComponents = parent.GetComponents<Component>();
                    foreach (var comp in parentComponents)
                    {
                        if (comp == null) continue;
                        var compType = comp.GetType();

                        // Check if this is a CharacterUI or similar
                        string typeName = compType.Name.ToLower();
                        if (typeName.Contains("character") || typeName.Contains("unit"))
                        {
                            var characterField = compType.GetField("character", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                              ?? compType.GetField("_character", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                              ?? compType.GetField("characterState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                            if (characterField != null)
                            {
                                var characterState = characterField.GetValue(comp);
                                if (characterState != null)
                                {
                                    return GetUnitDescription(characterState);
                                }
                            }

                            // Try method
                            var getMethod = compType.GetMethod("GetCharacter", Type.EmptyTypes)
                                         ?? compType.GetMethod("GetCharacterState", Type.EmptyTypes);
                            if (getMethod != null)
                            {
                                var characterState = getMethod.Invoke(comp, null);
                                if (characterState != null)
                                {
                                    return GetUnitDescription(characterState);
                                }
                            }
                        }
                    }
                    parent = parent.parent;
                }

                // Fallback: try to read any text from the targeting element
                string text = GetDirectText(targetElement);
                if (!string.IsNullOrEmpty(text))
                {
                    return $"Target: {TextUtilities.StripRichTextTags(text)}";
                }

                // Last fallback: use element name
                string cleanName = UITextHelper.CleanGameObjectName(targetElement.name);
                return $"Target: {cleanName}";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting unit target info: {ex.Message}");
            }

            return "Target unit";
        }

        /// <summary>
        /// Get a description of a unit from its CharacterState
        /// </summary>
        private string GetUnitDescription(object characterState)
        {
            if (characterState == null) return null;

            try
            {
                var battle = MonsterTrainAccessibility.BattleHandler;
                if (battle != null)
                {
                    // Use BattleAccessibility's detailed description method
                    return battle.GetDetailedUnitDescription(characterState);
                }

                // Fallback: extract basic info manually
                var type = characterState.GetType();
                string name = null;
                int hp = -1;
                int attack = -1;

                // Get name
                var getNameMethod = type.GetMethod("GetName", Type.EmptyTypes)
                                 ?? type.GetMethod("GetLocName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    name = getNameMethod.Invoke(characterState, null) as string;
                }

                // Get HP
                var getHPMethod = type.GetMethod("GetHP", Type.EmptyTypes);
                if (getHPMethod != null)
                {
                    var result = getHPMethod.Invoke(characterState, null);
                    if (result is int h) hp = h;
                }

                // Get Attack
                var getAttackMethod = type.GetMethod("GetAttackDamage", Type.EmptyTypes);
                if (getAttackMethod != null)
                {
                    var result = getAttackMethod.Invoke(characterState, null);
                    if (result is int a) attack = a;
                }

                if (!string.IsNullOrEmpty(name))
                {
                    if (attack >= 0 && hp >= 0)
                    {
                        return $"{TextUtilities.StripRichTextTags(name)}: {attack} attack, {hp} health";
                    }
                    return TextUtilities.StripRichTextTags(name);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting unit description: {ex.Message}");
            }

            return "Unit";
        }

        /// <summary>
        /// Extract readable text from a UI GameObject.
        /// Tries multiple approaches to find text.
        /// </summary>
        private string GetTextFromGameObject(GameObject go)
        {
            if (go == null)
                return null;

            string text = null;

            // Check if this is a scrollbar - if so, try to find the scroll view's content
            if (IsScrollbar(go))
            {
                text = GetScrollViewContentText(go);
                if (!string.IsNullOrEmpty(text))
                {
                    // Truncate very long text for the focus announcement
                    if (text.Length > 300)
                    {
                        text = text.Substring(0, 300) + "... Press T to read full text.";
                    }
                    return text;
                }
            }

            // Check for RunOpeningScreen (Boss Battles screen at start of run) - before dialog check
            text = BattleIntroTextReader.GetRunOpeningScreenText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // Check for dialog buttons (Yes/No buttons inside Dialog popups)
            text = GetDialogButtonText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // Check for cards in hand (CardUI component) - full card details
            text = CardTextReader.GetCardUIText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // Check for shop items (MerchantGoodDetailsUI, MerchantServiceUI)
            text = ShopTextReader.GetShopItemText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 1. Check for Fight button on BattleIntro screen - get battle name
            text = BattleIntroTextReader.GetBattleIntroText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 1.6. Check for RelicInfoUI (artifact selection on RelicDraftScreen)
            text = RelicTextReader.GetRelicInfoText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 2. Check for map node (battle/event/shop nodes on the map)
            text = MapTextReader.GetMapNodeText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 2.4. Check for DLC toggle (Last Divinity / Hellforged) - before settings/generic toggle
            text = ClanSelectionTextReader.GetDLCToggleText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 2. Check for settings screen elements (dropdowns, sliders, toggles with SettingsEntry parent)
            text = SettingsTextReader.GetSettingsElementText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 2.5. Check for toggle/checkbox components
            text = GetToggleText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 2.7. Check for compendium relic grid items
            text = CompendiumTextReader.GetCompendiumRelicText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 2.8. Check for compendium upgrade level nodes
            text = CompendiumTextReader.GetUpgradeLevelNodeText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 2.9. Check for compendium leaderboard player stat rows
            text = CompendiumTextReader.GetPlayerStatRowText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 2.92. Check for compendium lifetime run stat rows
            text = CompendiumTextReader.GetRunStatRowText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 2.91. Check for clan checklist section (clan progress with unlock conditions/meter)
            text = CompendiumTextReader.GetClanChecklistText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 2.93. Check for subclan victory items in compendium checklist
            text = CompendiumTextReader.GetSubclanVictoryItemText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 2.95. Check for compendium sort/filter buttons
            text = CompendiumTextReader.GetCompendiumSortButtonText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 3. Check for logbook/compendium items
            text = CompendiumTextReader.GetLogbookItemText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 3.5 Check for clan selection icons
            text = ClanSelectionTextReader.GetClanSelectionText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 3.6 Check for champion choice buttons
            text = ClanSelectionTextReader.GetChampionChoiceText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 3.7 Check for covenant selector UI
            text = ClanSelectionTextReader.GetCovenantSelectorText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 3.9 Check for buttons with LocalizedTooltipProvider (mutator options, etc.)
            text = TooltipTextReader.GetLocalizedTooltipButtonText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 3.95 Check for event screen elements (Continue button, choice items)
            text = EventTextReader.GetEventScreenElementText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 4. Check for map branch choice elements
            text = MapTextReader.GetBranchChoiceText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 5. Try to get text with context (handles short button labels)
            text = GetTextWithContext(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 6. Try the GameObject name as fallback (but make it more readable)
            text = UITextHelper.CleanGameObjectName(go.name);

            return text?.Trim();
        }

        /// <summary>
        /// Get text for toggle/checkbox controls with their label
        /// </summary>
        private string GetToggleText(GameObject go)
        {
            try
            {
                // First check if this is the Trial toggle on BattleIntroScreen
                string trialText = SettingsTextReader.GetTrialToggleText(go);
                if (!string.IsNullOrEmpty(trialText))
                {
                    return trialText;
                }

                // Check for Unity UI Toggle component first
                var unityToggle = go.GetComponent<Toggle>();
                if (unityToggle != null)
                {
                    string label = GetToggleLabelFromHierarchy(go);
                    string state = unityToggle.isOn ? "on" : "off";
                    return string.IsNullOrEmpty(label) ? state : $"{label}: {state}";
                }

                // Check for game-specific toggle types via reflection
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    string typeName = type.Name;

                    // Look for GameUISelectableToggle, GameUISelectableCheckbox, etc.
                    if (typeName.Contains("Toggle") || typeName.Contains("Checkbox"))
                    {
                        // Try to get isOn or isChecked property
                        bool? isOn = null;
                        var isOnProp = type.GetProperty("isOn");
                        if (isOnProp != null)
                        {
                            isOn = isOnProp.GetValue(component) as bool?;
                        }
                        if (isOn == null)
                        {
                            var isCheckedProp = type.GetProperty("isChecked");
                            if (isCheckedProp != null)
                            {
                                isOn = isCheckedProp.GetValue(component) as bool?;
                            }
                        }
                        // Also try m_IsOn field
                        if (isOn == null)
                        {
                            var isOnField = type.GetField("m_IsOn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (isOnField != null)
                            {
                                isOn = isOnField.GetValue(component) as bool?;
                            }
                        }

                        if (isOn.HasValue)
                        {
                            string label = GetToggleLabelFromHierarchy(go);
                            string state = isOn.Value ? "on" : "off";
                            return string.IsNullOrEmpty(label) ? state : $"{label}: {state}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting toggle text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Find the label for a toggle by looking at hierarchy
        /// </summary>
        private string GetToggleLabelFromHierarchy(GameObject go)
        {
            try
            {
                // Check siblings for label text
                Transform parent = go.transform.parent;
                if (parent != null)
                {
                    foreach (Transform sibling in parent)
                    {
                        if (sibling == go.transform) continue;

                        // Skip on/off labels
                        string sibName = sibling.name.ToLower();
                        if (sibName.Contains("onlabel") || sibName.Contains("offlabel") ||
                            sibName == "on" || sibName == "off")
                            continue;

                        string sibText = UITextHelper.GetTMPTextDirect(sibling.gameObject);
                        if (string.IsNullOrEmpty(sibText))
                        {
                            var uiText = sibling.GetComponent<Text>();
                            sibText = uiText?.text;
                        }

                        // Skip very short or on/off text
                        if (!string.IsNullOrEmpty(sibText) && sibText.Length > 2)
                        {
                            string lower = sibText.ToLower().Trim();
                            if (lower != "on" && lower != "off")
                            {
                                return sibText.Trim();
                            }
                        }
                    }

                    // Check parent's name for context
                    string parentName = UITextHelper.CleanGameObjectName(parent.name);
                    if (!string.IsNullOrEmpty(parentName) && parentName.Length > 2)
                    {
                        return parentName;
                    }

                    // Check grandparent siblings
                    if (parent.parent != null)
                    {
                        foreach (Transform uncle in parent.parent)
                        {
                            if (uncle == parent) continue;

                            string uncleText = UITextHelper.GetTMPTextDirect(uncle.gameObject);
                            if (string.IsNullOrEmpty(uncleText))
                            {
                                var uiText = uncle.GetComponent<Text>();
                                uncleText = uiText?.text;
                            }

                            if (!string.IsNullOrEmpty(uncleText) && uncleText.Length > 2)
                            {
                                string lower = uncleText.ToLower().Trim();
                                if (lower != "on" && lower != "off")
                                {
                                    return uncleText.Trim();
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get text with additional context for buttons with short labels
        /// </summary>
        private string GetTextWithContext(GameObject go)
        {
            string directText = GetDirectText(go);

            // If text is very short (1-2 chars), it's likely an icon character - use cleaned name instead
            if (!string.IsNullOrEmpty(directText) && directText.Length <= 2)
            {
                string cleanedName = UITextHelper.CleanGameObjectName(go.name);
                if (!string.IsNullOrEmpty(cleanedName) && cleanedName.Length > 2)
                {
                    return cleanedName;
                }
            }

            // If text is short (3-4 chars) or empty, look for context
            if (string.IsNullOrEmpty(directText) || directText.Length <= 4)
            {
                // Check parent for label
                string contextLabel = GetContextLabelFromHierarchy(go);
                if (!string.IsNullOrEmpty(contextLabel))
                {
                    if (string.IsNullOrEmpty(directText))
                    {
                        return contextLabel;
                    }
                    return $"{contextLabel}: {directText}";
                }
            }

            return directText;
        }

        /// <summary>
        /// Get direct text from a GameObject (immediate text components)
        /// </summary>
        private string GetDirectText(GameObject go)
        {
            // Try TMP text first
            string text = UITextHelper.GetTMPText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text.Trim();
            }

            // Try Unity UI Text
            var uiText = go.GetComponentInChildren<Text>();
            if (uiText != null && !string.IsNullOrEmpty(uiText.text))
            {
                return uiText.text.Trim();
            }

            return null;
        }

        /// <summary>
        /// Get context label from hierarchy (parent/sibling/grandparent)
        /// </summary>
        private string GetContextLabelFromHierarchy(GameObject go)
        {
            try
            {
                Transform current = go.transform;

                // Walk up the hierarchy looking for meaningful labels
                for (int depth = 0; depth < 4 && current.parent != null; depth++)
                {
                    Transform parent = current.parent;

                    // Check siblings of current
                    foreach (Transform sibling in parent)
                    {
                        if (sibling == current) continue;

                        string sibText = UITextHelper.GetTMPTextDirect(sibling.gameObject);
                        if (string.IsNullOrEmpty(sibText))
                        {
                            var uiText = sibling.GetComponent<Text>();
                            sibText = uiText?.text;
                        }

                        // Accept text that's longer than what we already have
                        if (!string.IsNullOrEmpty(sibText) && sibText.Trim().Length > 4)
                        {
                            return sibText.Trim();
                        }
                    }

                    // Check parent's name
                    string parentName = parent.name;
                    if (!string.IsNullOrEmpty(parentName))
                    {
                        // Clean up common UI suffixes
                        string cleaned = UITextHelper.CleanGameObjectName(parentName);
                        if (!string.IsNullOrEmpty(cleaned) && cleaned.Length > 4)
                        {
                            // Make sure it's not just a generic container name
                            string lower = cleaned.ToLower();
                            if (!lower.Contains("container") && !lower.Contains("panel") &&
                                !lower.Contains("holder") && !lower.Contains("group") &&
                                !lower.Contains("content") && !lower.Contains("root") &&
                                !lower.Contains("options") && !lower.Contains("input area") &&
                                !lower.Contains("input") && !lower.Contains("area") &&
                                !lower.Contains("section") && !lower.Contains("buttons") &&
                                !lower.Contains("layout") && !lower.Contains("wrapper"))
                            {
                                return cleaned;
                            }
                        }
                    }

                    current = parent;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Check if a GameObject is a scrollbar
        /// </summary>
        private bool IsScrollbar(GameObject go)
        {
            if (go == null) return false;

            string name = go.name.ToLower();
            if (name.Contains("scrollbar") || name.Contains("scroll bar"))
                return true;

            return go.GetComponent<Scrollbar>() != null;
        }

        /// <summary>
        /// Try to find and read text from a scroll view's content area
        /// </summary>
        private string GetScrollViewContentText(GameObject scrollbarGo)
        {
            try
            {
                // Try to find the ScrollRect in parent or siblings
                Transform parent = scrollbarGo.transform.parent;
                while (parent != null)
                {
                    var scrollRect = parent.GetComponent<ScrollRect>();
                    if (scrollRect != null && scrollRect.content != null)
                    {
                        return UITextHelper.GetAllTextFromTransform(scrollRect.content);
                    }

                    // Also check siblings
                    foreach (Transform sibling in parent)
                    {
                        var siblingScrollRect = sibling.GetComponent<ScrollRect>();
                        if (siblingScrollRect != null && siblingScrollRect.content != null)
                        {
                            return UITextHelper.GetAllTextFromTransform(siblingScrollRect.content);
                        }
                    }

                    parent = parent.parent;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scroll content: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Read all visible text on the current screen (press T)
        /// </summary>
        /// <summary>
        /// Read the train stats panel that appears when pressing TAB
        /// </summary>
        public void ReadTrainStatsPanel()
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Reading train stats panel...");

                // Look for the stats panel in the scene
                var allObjects = FindObjectsOfType<GameObject>();
                GameObject statsPanel = null;

                foreach (var obj in allObjects)
                {
                    if (!obj.activeInHierarchy) continue;
                    string name = obj.name.ToLower();

                    // Look for stats panel, tower info, or run summary panel
                    if ((name.Contains("stats") && (name.Contains("panel") || name.Contains("display"))) ||
                        name.Contains("runinfo") || name.Contains("towerinfo") ||
                        name.Contains("statspanel") || name.Contains("statsoverlay") ||
                        name.Contains("traininfo") || name.Contains("towerstats"))
                    {
                        statsPanel = obj;
                        MonsterTrainAccessibility.LogInfo($"Found potential stats panel: {obj.name}");
                        break;
                    }
                }

                if (statsPanel != null)
                {
                    // Collect all text from the stats panel
                    var texts = BattleIntroTextReader.GetAllTextFromChildren(statsPanel);
                    if (texts != null && texts.Count > 0)
                    {
                        // Filter out very short or meaningless text
                        var meaningfulTexts = texts.Where(t =>
                            !string.IsNullOrWhiteSpace(t) &&
                            t.Length > 1 &&
                            !DialogTextReader.IsGarbageText(t)
                        ).Distinct().ToList();

                        if (meaningfulTexts.Count > 0)
                        {
                            string result = "Train Stats: " + string.Join(". ", meaningfulTexts);
                            MonsterTrainAccessibility.ScreenReader?.Speak(result, false);
                            return;
                        }
                    }
                }

                // Fallback: read basic stats from BattleAccessibility and SaveManager
                var battle = MonsterTrainAccessibility.BattleHandler;
                if (battle != null)
                {
                    var sb = new StringBuilder();
                    sb.Append("Train Stats: ");

                    // Get pyre health
                    int pyreHP = battle.GetPyreHealth();
                    int maxPyreHP = battle.GetMaxPyreHealth();
                    if (pyreHP >= 0 && maxPyreHP > 0)
                    {
                        sb.Append($"Pyre: {pyreHP} of {maxPyreHP} health. ");
                    }

                    // Get gold
                    int gold = Core.InputInterceptor.GetCurrentGold();
                    if (gold >= 0)
                    {
                        sb.Append($"Gold: {gold}. ");
                    }

                    // Get deck size if possible
                    int deckSize = battle.GetDeckSize();
                    if (deckSize >= 0)
                    {
                        sb.Append($"Deck size: {deckSize} cards. ");
                    }

                    // Get additional stats from SaveManager via reflection
                    AppendSaveManagerStats(sb);

                    string result = sb.ToString();
                    if (result.Length > 14) // More than just "Train Stats: "
                    {
                        MonsterTrainAccessibility.ScreenReader?.Speak(result, false);
                        return;
                    }
                }

                MonsterTrainAccessibility.ScreenReader?.Speak("Stats panel not visible. Press TAB again to toggle.", false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading train stats: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read train stats", false);
            }
        }

        /// <summary>
        /// Append additional stats from SaveManager (covenant, crystals, etc.)
        /// </summary>
        private void AppendSaveManagerStats(StringBuilder sb)
        {
            try
            {
                // Find SaveManager instance
                Type saveManagerType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    saveManagerType = assembly.GetType("SaveManager");
                    if (saveManagerType != null) break;
                }

                if (saveManagerType == null) return;

                object saveManager = FindObjectOfType(saveManagerType);
                if (saveManager == null) return;

                var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;

                // Covenant/Ascension level
                var getAscensionMethod = saveManagerType.GetMethod("GetAscensionLevel", bindingFlags);
                if (getAscensionMethod != null && getAscensionMethod.GetParameters().Length == 0)
                {
                    var result = getAscensionMethod.Invoke(saveManager, null);
                    if (result is int covenant && covenant > 0)
                    {
                        sb.Append($"Covenant {covenant}. ");
                    }
                }

                // Crystal/Shard count (DLC) - use GetDlcSaveData<HellforgedSaveData>(DLC.Hellforged).GetCrystals()
                try
                {
                    // Check if DLC crystals should be shown
                    var showMethod = saveManagerType.GetMethod("ShowPactCrystals", Type.EmptyTypes);
                    bool showCrystals = true;
                    if (showMethod != null)
                    {
                        showCrystals = (bool)showMethod.Invoke(saveManager, null);
                    }

                    if (showCrystals)
                    {
                        int crystals = -1;
                        var getDlcMethod = saveManagerType.GetMethod("GetDlcSaveData");
                        if (getDlcMethod != null && getDlcMethod.IsGenericMethod)
                        {
                            // Find HellforgedSaveData type and DLC enum
                            Type hellforgedType = null;
                            Type dlcType = null;
                            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                            {
                                if (hellforgedType == null)
                                    hellforgedType = asm.GetType("HellforgedSaveData");
                                if (dlcType == null)
                                {
                                    var t = asm.GetType("DLC");
                                    if (t != null && t.IsEnum) dlcType = t;
                                }
                                if (hellforgedType != null && dlcType != null) break;
                            }

                            if (hellforgedType != null && dlcType != null)
                            {
                                var genericMethod = getDlcMethod.MakeGenericMethod(hellforgedType);
                                var hellforgedValue = Enum.ToObject(dlcType, 1); // Hellforged = 1
                                var dlcSaveData = genericMethod.Invoke(saveManager, new object[] { hellforgedValue });
                                if (dlcSaveData != null)
                                {
                                    var getCrystalsMethod = dlcSaveData.GetType().GetMethod("GetCrystals", Type.EmptyTypes);
                                    if (getCrystalsMethod != null)
                                    {
                                        crystals = (int)getCrystalsMethod.Invoke(dlcSaveData, null);
                                    }
                                }
                            }
                        }

                        if (crystals > 0)
                        {
                            sb.Append($"Shards: {crystals}. ");
                        }
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting SaveManager stats: {ex.Message}");
            }
        }

        public void ReadAllScreenText()
        {
            try
            {
                var collectedTexts = new HashSet<string>();
                var sb = new StringBuilder();

                // Find all root objects and search for text
                var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var root in rootObjects)
                {
                    if (root.activeInHierarchy)
                    {
                        CollectAllTextUnique(root.transform, collectedTexts, sb);
                    }
                }

                // Also find all TMP components directly (they may be in DontDestroyOnLoad)
                FindAllTMPText(collectedTexts, sb);

                // Also check Unity UI Text components
                var allTextComponents = FindObjectsOfType<Text>();
                foreach (var textComp in allTextComponents)
                {
                    if (textComp.gameObject.activeInHierarchy && !string.IsNullOrEmpty(textComp.text))
                    {
                        string cleanText = TextUtilities.StripRichTextTags(textComp.text.Trim());
                        if (!string.IsNullOrEmpty(cleanText) && !collectedTexts.Contains(cleanText) && !DialogTextReader.IsGarbageText(cleanText))
                        {
                            collectedTexts.Add(cleanText);
                            sb.AppendLine(cleanText);
                        }
                    }
                }

                string allText = sb.ToString().Trim();

                if (string.IsNullOrEmpty(allText))
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak("No text found on screen", false);
                }
                else
                {
                    // Clean up - remove duplicate empty lines
                    allText = System.Text.RegularExpressions.Regex.Replace(allText, @"(\r?\n){3,}", "\n\n");
                    MonsterTrainAccessibility.ScreenReader?.Speak(allText, false);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading screen text: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read screen text", false);
            }
        }

        /// <summary>
        /// Find all TextMeshPro components using reflection
        /// </summary>
        private void FindAllTMPText(HashSet<string> collectedTexts, StringBuilder sb)
        {
            try
            {
                // Find the TMP_Text type
                Type tmpTextType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    tmpTextType = assembly.GetType("TMPro.TMP_Text");
                    if (tmpTextType != null) break;
                }

                if (tmpTextType == null) return;

                // Find all instances using FindObjectsOfType
                var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", new Type[0]);
                var genericMethod = findMethod.MakeGenericMethod(tmpTextType);
                var allTMP = genericMethod.Invoke(null, null) as Array;

                if (allTMP == null) return;

                var textProperty = tmpTextType.GetProperty("text");
                if (textProperty == null) return;

                foreach (var tmp in allTMP)
                {
                    if (tmp == null) continue;

                    // Check if active
                    var component = tmp as Component;
                    if (component == null || !component.gameObject.activeInHierarchy) continue;

                    string text = textProperty.GetValue(tmp) as string;
                    if (!string.IsNullOrEmpty(text))
                    {
                        string cleanText = TextUtilities.StripRichTextTags(text.Trim());
                        if (!string.IsNullOrEmpty(cleanText) && !collectedTexts.Contains(cleanText) && !DialogTextReader.IsGarbageText(cleanText))
                        {
                            collectedTexts.Add(cleanText);
                            sb.AppendLine(cleanText);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error finding TMP text: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively collect text, avoiding duplicates
        /// </summary>
        private void CollectAllTextUnique(Transform transform, HashSet<string> collected, StringBuilder sb)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy) return;

            // Get Text component on this object
            var uiText = transform.GetComponent<Text>();
            if (uiText != null && !string.IsNullOrEmpty(uiText.text))
            {
                string cleaned = TextUtilities.StripRichTextTags(uiText.text.Trim());
                if (!string.IsNullOrEmpty(cleaned) && !collected.Contains(cleaned) && !DialogTextReader.IsGarbageText(cleaned))
                {
                    collected.Add(cleaned);
                    sb.AppendLine(cleaned);
                }
            }

            // Get TMP text
            string tmpText = UITextHelper.GetTMPTextDirect(transform.gameObject);
            if (!string.IsNullOrEmpty(tmpText))
            {
                string cleaned = TextUtilities.StripRichTextTags(tmpText.Trim());
                if (!string.IsNullOrEmpty(cleaned) && !collected.Contains(cleaned) && !DialogTextReader.IsGarbageText(cleaned))
                {
                    collected.Add(cleaned);
                    sb.AppendLine(cleaned);
                }
            }

            // Recurse into children
            foreach (Transform child in transform)
            {
                CollectAllTextUnique(child, collected, sb);
            }
        }

        /// <summary>
        /// Called when the main menu screen is shown
        /// </summary>
        public void OnMainMenuEntered(object screen)
        {
            MonsterTrainAccessibility.LogInfo("Main menu entered");
            _isActive = true;
            _lastSelectedObject = null;

            // Announce that we're at the main menu
            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Main Menu");

            // Read the currently selected item if any
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                string text = GetTextFromGameObject(EventSystem.current.currentSelectedGameObject);
                if (!string.IsNullOrEmpty(text))
                {
                    MonsterTrainAccessibility.ScreenReader?.Queue(text);
                }
            }
        }

        /// <summary>
        /// Called when the game over screen (victory/defeat) is shown
        /// </summary>
        public void OnGameOverScreenEntered(object screen)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Game over screen handler called");

                // Small delay to let UI populate
                StartCoroutine(ReadGameOverScreenDelayed(screen));
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in OnGameOverScreenEntered: {ex.Message}");
            }
        }

        private System.Collections.IEnumerator ReadGameOverScreenDelayed(object screen)
        {
            // Wait for UI to populate
            yield return new UnityEngine.WaitForSeconds(0.5f);

            try
            {
                var sb = new StringBuilder();

                // Try to extract data from the screen object via reflection
                if (screen != null)
                {
                    var screenType = screen.GetType();
                    MonsterTrainAccessibility.LogInfo($"Game over screen type: {screenType.Name}");

                    // Log all fields for debugging
                    foreach (var field in screenType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        try
                        {
                            var value = field.GetValue(screen);
                            MonsterTrainAccessibility.LogInfo($"  Field: {field.Name} = {value}");
                        }
                        catch { }
                    }
                }

                // Find all text elements in active game over screen
                var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var root in rootObjects)
                {
                    if (!root.activeInHierarchy) continue;

                    // Look for game over screen components
                    string rootName = root.name.ToLower();
                    if (rootName.Contains("gameover") || rootName.Contains("summary") ||
                        rootName.Contains("victory") || rootName.Contains("defeat") ||
                        rootName.Contains("result") || rootName.Contains("endrun"))
                    {
                        CollectGameOverText(root.transform, sb);
                    }
                    else
                    {
                        // Also check children with these names
                        foreach (Transform child in root.transform)
                        {
                            if (!child.gameObject.activeInHierarchy) continue;
                            string childName = child.name.ToLower();
                            if (childName.Contains("gameover") || childName.Contains("summary") ||
                                childName.Contains("victory") || childName.Contains("defeat") ||
                                childName.Contains("result") || childName.Contains("endrun"))
                            {
                                CollectGameOverText(child, sb);
                            }
                        }
                    }
                }

                // If we didn't find structured data, try to read all visible TMP text
                if (sb.Length == 0)
                {
                    sb.Append(ReadAllVisibleTextOnScreen());
                }

                string announcement = sb.ToString().Trim();
                if (!string.IsNullOrEmpty(announcement))
                {
                    MonsterTrainAccessibility.LogInfo($"Game over announcement: {announcement}");
                    MonsterTrainAccessibility.ScreenReader?.Speak(announcement, false);
                }
                else
                {
                    // Fallback: just announce we're on the game over screen
                    MonsterTrainAccessibility.ScreenReader?.Speak("Run complete. Press T to read stats.", false);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading game over screen: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Run complete.", false);
            }
        }

        private void CollectGameOverText(Transform root, StringBuilder sb)
        {
            try
            {
                // Collect all meaningful text in order
                var textElements = new List<(int order, string label, string value)>();

                CollectTextRecursive(root, textElements, 0);

                // Sort by order and format
                textElements.Sort((a, b) => a.order.CompareTo(b.order));

                foreach (var elem in textElements)
                {
                    if (!string.IsNullOrEmpty(elem.label) && !string.IsNullOrEmpty(elem.value))
                    {
                        sb.Append($"{elem.label}: {elem.value}. ");
                    }
                    else if (!string.IsNullOrEmpty(elem.value))
                    {
                        sb.Append($"{elem.value}. ");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error collecting game over text: {ex.Message}");
            }
        }

        private void CollectTextRecursive(Transform transform, List<(int order, string label, string value)> textElements, int depth)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy || depth > 15)
                return;

            string objName = transform.name.ToLower();

            // Skip certain elements
            if (objName.Contains("button") && !objName.Contains("label"))
                return;

            // Get text from this element
            string text = UITextHelper.GetTMPTextDirect(transform.gameObject);
            if (!string.IsNullOrEmpty(text))
            {
                text = TextUtilities.StripRichTextTags(text.Trim());

                // Determine if this is a label or value based on name/position
                bool isLabel = objName.Contains("label") || objName.Contains("title") ||
                              objName.Contains("header") || objName.Contains("name");

                // Calculate order based on position (y position inverted since UI goes top-down)
                int order = 0;
                if (transform is RectTransform rt)
                {
                    order = (int)(-rt.anchoredPosition.y * 10 + rt.anchoredPosition.x);
                }

                if (!string.IsNullOrEmpty(text) && text.Length > 0)
                {
                    // Filter out very short or noise text
                    if (text.Length >= 1 && !text.All(c => c == '/' || c == ',' || char.IsWhiteSpace(c)))
                    {
                        textElements.Add((order, isLabel ? text : null, isLabel ? null : text));
                    }
                }
            }

            // Recurse into children
            foreach (Transform child in transform)
            {
                CollectTextRecursive(child, textElements, depth + 1);
            }
        }

        private string ReadAllVisibleTextOnScreen()
        {
            var sb = new StringBuilder();
            var seenTexts = new HashSet<string>();

            try
            {
                // Find all TMP components in the scene
                var tmpComponents = UnityEngine.Object.FindObjectsOfType<Component>();
                var textList = new List<(float y, string text)>();

                foreach (var comp in tmpComponents)
                {
                    if (comp == null || !comp.gameObject.activeInHierarchy)
                        continue;

                    var type = comp.GetType();
                    if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                    {
                        var textProp = type.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                        if (textProp != null)
                        {
                            string text = textProp.GetValue(comp) as string;
                            if (!string.IsNullOrEmpty(text))
                            {
                                text = TextUtilities.StripRichTextTags(text.Trim());
                                if (!string.IsNullOrEmpty(text) && text.Length > 1 && !seenTexts.Contains(text))
                                {
                                    seenTexts.Add(text);

                                    // Get Y position for ordering
                                    float yPos = 0;
                                    if (comp.transform is RectTransform rt)
                                    {
                                        yPos = rt.position.y;
                                    }

                                    textList.Add((yPos, text));
                                }
                            }
                        }
                    }
                }

                // Sort by Y position (top to bottom)
                textList.Sort((a, b) => b.y.CompareTo(a.y));

                foreach (var item in textList)
                {
                    sb.Append(item.text);
                    sb.Append(". ");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading all visible text: {ex.Message}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Force re-read the current selection
        /// </summary>
        public void RereadCurrentSelection()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                string text = GetTextFromGameObject(EventSystem.current.currentSelectedGameObject);
                if (!string.IsNullOrEmpty(text))
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak(text, false);
                }
                else
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak("Unknown item", false);
                }
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Speak("Nothing selected", false);
            }
        }

        /// <summary>
        /// Pause menu reading (e.g., during loading)
        /// </summary>
        public void Pause()
        {
            _isActive = false;
        }

        /// <summary>
        /// Resume menu reading
        /// </summary>
        public void Resume()
        {
            _isActive = true;
            _lastSelectedObject = null; // Force re-announce
        }
    }

    /// <summary>
    /// Info about a clan/class for selection
    /// </summary>
    public class ClanInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Results from a completed run
    /// </summary>
    public class RunResults
    {
        public bool Won { get; set; }
        public int Score { get; set; }
        public int CovenantLevel { get; set; }
        public int FloorsCleared { get; set; }
        public int EnemiesDefeated { get; set; }
        public int CardsPlayed { get; set; }
    }
}
