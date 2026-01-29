using MonsterTrainAccessibility.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
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

                // Also check for any new large text panels appearing
                CheckForNewTextPanels();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error checking text changes: {ex.Message}");
            }
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
                dialogText = BattleAccessibility.StripRichTextTags(dialogText.Trim());

                // Get the button label
                string buttonLabel = GetDirectText(go);
                // If text is short (1-2 chars like icon "A"), use the cleaned GameObject name instead
                if (string.IsNullOrEmpty(buttonLabel) || buttonLabel.Length <= 2)
                {
                    buttonLabel = CleanGameObjectName(go.name);
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
        /// Check if text appears to be placeholder/debug text that shouldn't be read.
        /// </summary>
        private bool IsPlaceholderText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            string lower = text.ToLower();

            // Check for common placeholder patterns
            if (lower.Contains("placeholder"))
                return true;
            if (lower.Contains("(should"))  // Developer comments like "(should layer below cards)"
                return true;
            if (lower.Contains("todo"))
                return true;
            if (lower.Contains("fixme"))
                return true;

            return false;
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
                if (!string.IsNullOrEmpty(dataContent) && !IsPlaceholderText(dataContent))
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

                    string text = GetTMPTextDirect(child.gameObject);
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
        /// Check if the given GameObject is inside a Dialog context (has a Dialog component in parent hierarchy).
        /// </summary>
        private bool IsInDialogContext(GameObject go)
        {
            if (go == null) return false;

            Transform current = go.transform;
            int depth = 0;
            while (current != null && depth < 8)
            {
                foreach (var comp in current.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == "Dialog")
                    {
                        return true;
                    }
                }
                current = current.parent;
                depth++;
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
                string text = GetTMPText(transform.gameObject);
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

                if (currentSelected != null && IsActuallyVisible(currentSelected))
                {
                    // Check if this is floor targeting mode (Tower Selectable = floor selection for playing cards)
                    if (currentSelected.name.Contains("Tower") && currentSelected.name.Contains("Selectable"))
                    {
                        // Activate floor targeting system
                        ActivateFloorTargeting();
                        return;
                    }

                    // Check if this is unit targeting mode (targeting a specific character)
                    if (IsUnitTargetingElement(currentSelected))
                    {
                        ActivateUnitTargeting();
                        return;
                    }

                    // Check if we're still in a dialog context - if not, clear the tracking
                    // so the dialog text will be announced again if the same dialog appears
                    if (!IsInDialogContext(currentSelected))
                    {
                        _lastAnnouncedDialogText = null;
                    }

                    string text = GetTextFromGameObject(currentSelected);
                    if (!string.IsNullOrEmpty(text))
                    {
                        // Clean sprite tags before announcing
                        text = CleanSpriteTagsForSpeech(text);
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
            text = GetRunOpeningScreenText(go);
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
            text = GetCardUIText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // Check for shop items (MerchantGoodDetailsUI, MerchantServiceUI)
            text = GetShopItemText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 1. Check for Fight button on BattleIntro screen - get battle name
            text = GetBattleIntroText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 1.6. Check for RelicInfoUI (artifact selection on RelicDraftScreen)
            text = GetRelicInfoText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 2. Check for map node (battle/event/shop nodes on the map)
            text = GetMapNodeText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 2. Check for settings screen elements (dropdowns, sliders, toggles with SettingsEntry parent)
            text = GetSettingsElementText(go);
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

            // 3. Check for logbook/compendium items
            text = GetLogbookItemText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 3.5 Check for clan selection icons
            text = GetClanSelectionText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 3.6 Check for champion choice buttons
            text = GetChampionChoiceText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 3.7 Check for buttons with LocalizedTooltipProvider (mutator options, etc.)
            text = GetLocalizedTooltipButtonText(go);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 4. Check for map branch choice elements
            text = GetBranchChoiceText(go);
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
            text = CleanGameObjectName(go.name);

            return text?.Trim();
        }

        /// <summary>
        /// Get text for map branch choice elements (when choosing between paths)
        /// </summary>
        private string GetBranchChoiceText(GameObject go)
        {
            try
            {
                // Skip ClassSelectionIcon and ChampionChoiceButton elements - they have their own handlers
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    string typeName = component.GetType().Name;
                    if (typeName == "ClassSelectionIcon" || typeName == "ChampionChoiceButton")
                        return null;

                    // Handle StoryChoiceItem (random event choices)
                    if (typeName == "StoryChoiceItem")
                    {
                        return GetStoryChoiceText(go, component);
                    }
                }

                // Check if this is a branch choice element or map navigation arrow
                string goName = go.name.ToLower();
                bool isBranchChoice = goName.Contains("branch") || goName.Contains("choice");
                bool isMapArrow = goName.Contains("arrow") || goName.Contains("navigation") ||
                                  goName.Contains("left") || goName.Contains("right");

                // Also check parent names
                if (!isBranchChoice && !isMapArrow && go.transform.parent != null)
                {
                    string parentName = go.transform.parent.name.ToLower();
                    isBranchChoice = parentName.Contains("branch") || parentName.Contains("choice");
                    isMapArrow = parentName.Contains("arrow") || parentName.Contains("navigation");
                }

                if (!isBranchChoice && !isMapArrow)
                    return null;

                MonsterTrainAccessibility.LogInfo($"Found map choice element: {go.name}");

                // First, try to find the currently visible tooltip on screen
                string visibleTooltip = GetVisibleMapTooltip();
                if (!string.IsNullOrEmpty(visibleTooltip))
                {
                    // Determine direction if it's an arrow
                    string direction = "";
                    if (goName.Contains("left"))
                        direction = "Left path: ";
                    else if (goName.Contains("right"))
                        direction = "Right path: ";

                    return direction + visibleTooltip;
                }

                // Try to get destination node info from the button component
                string destInfo = GetMapArrowDestination(go);
                if (!string.IsNullOrEmpty(destInfo))
                {
                    return destInfo;
                }

                // Look for node type indicators in children (icons, labels)
                var nodeType = GetBranchNodeType(go);

                // Look for enemy names or battle info in tooltip or children
                string enemyInfo = GetBranchEnemyInfo(go);

                if (!string.IsNullOrEmpty(nodeType))
                {
                    if (!string.IsNullOrEmpty(enemyInfo))
                    {
                        return $"{nodeType}: {enemyInfo}";
                    }
                    return nodeType;
                }

                // Fallback: try to find any meaningful text in children
                string childText = GetFirstMeaningfulChildText(go);
                if (!string.IsNullOrEmpty(childText))
                {
                    return $"Path: {childText}";
                }

                return "Map path option";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting branch choice text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get text from StoryChoiceItem (random event choices like "The Doors. (Get Trap Chute.)")
        /// </summary>
        private string GetStoryChoiceText(GameObject go, Component storyChoiceComponent)
        {
            try
            {
                var texts = new List<string>();

                // Look for TMP text components in children
                foreach (var component in go.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    string typeName = component.GetType().Name;
                    if (typeName.Contains("TMP_Text") || typeName.Contains("TextMeshPro"))
                    {
                        if (!component.gameObject.activeInHierarchy) continue;
                        var textProp = component.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            string text = textProp.GetValue(component) as string;
                            text = text?.Trim();
                            if (!string.IsNullOrEmpty(text) && !IsPlaceholderText(text))
                            {
                                texts.Add(text);
                            }
                        }
                    }
                }

                if (texts.Count > 0)
                {
                    string result = string.Join(" ", texts);
                    MonsterTrainAccessibility.LogInfo($"StoryChoiceItem text: {result}");
                    return result;
                }

                // Try reflection to get choice data from the component
                var componentType = storyChoiceComponent.GetType();

                // Try to get title/name
                var getTitleMethod = componentType.GetMethod("GetTitle") ??
                                     componentType.GetMethod("GetName") ??
                                     componentType.GetMethod("GetChoiceTitle");
                if (getTitleMethod != null)
                {
                    var title = getTitleMethod.Invoke(storyChoiceComponent, null) as string;
                    if (!string.IsNullOrEmpty(title))
                    {
                        texts.Add(title);
                    }
                }

                // Try to get description
                var getDescMethod = componentType.GetMethod("GetDescription") ??
                                    componentType.GetMethod("GetResultDescription");
                if (getDescMethod != null)
                {
                    var desc = getDescMethod.Invoke(storyChoiceComponent, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                    {
                        texts.Add($"({desc})");
                    }
                }

                if (texts.Count > 0)
                {
                    return string.Join(" ", texts);
                }

                // Log available fields for debugging
                var fields = componentType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var fieldNames = fields.Select(f => f.Name).Take(20);
                MonsterTrainAccessibility.LogInfo($"StoryChoiceItem fields: {string.Join(", ", fieldNames)}");

                return "Event choice";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting story choice text: {ex.Message}");
                return "Event choice";
            }
        }

        /// <summary>
        /// Find and read the currently visible tooltip on the map screen
        /// </summary>
        private string GetVisibleMapTooltip()
        {
            try
            {
                // Find all active tooltip displays in the scene
                var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                foreach (var obj in allObjects)
                {
                    if (!obj.activeInHierarchy) continue;

                    string name = obj.name.ToLower();
                    // Look for tooltip display objects
                    if (name.Contains("tooltip") && (name.Contains("display") || name.Contains("panel") || name.Contains("popup")))
                    {
                        // Check if it has visible text
                        string title = null;
                        string body = null;

                        // Try to get TMP text from children
                        var children = obj.GetComponentsInChildren<Transform>(true);
                        foreach (var child in children)
                        {
                            if (!child.gameObject.activeInHierarchy) continue;

                            string childName = child.name.ToLower();
                            string text = GetTMPTextDirect(child.gameObject);

                            if (!string.IsNullOrEmpty(text))
                            {
                                text = BattleAccessibility.StripRichTextTags(text.Trim());
                                if (string.IsNullOrEmpty(text)) continue;

                                // Title is usually shorter and comes first
                                if (childName.Contains("title") || childName.Contains("header") || childName.Contains("name"))
                                {
                                    title = text;
                                }
                                else if (childName.Contains("body") || childName.Contains("description") || childName.Contains("desc"))
                                {
                                    body = text;
                                }
                                else if (title == null && text.Length < 50)
                                {
                                    title = text;
                                }
                                else if (body == null)
                                {
                                    body = text;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(title))
                        {
                            MonsterTrainAccessibility.LogInfo($"Found visible tooltip: {title} - {body}");
                            if (!string.IsNullOrEmpty(body))
                                return $"{title}. {body}";
                            return title;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error finding visible tooltip: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Try to get destination info from a map arrow button
        /// </summary>
        private string GetMapArrowDestination(GameObject go)
        {
            try
            {
                // Determine if this is left or right button
                bool isLeft = go.name.ToLower().Contains("left");
                bool isRight = go.name.ToLower().Contains("right");
                string direction = isLeft ? "Left" : (isRight ? "Right" : "");
                int buttonIndex = isLeft ? 0 : (isRight ? 1 : -1);

                // Look for BranchChoiceUI component on this object or parent
                Component branchChoiceUI = null;
                Transform current = go.transform;

                while (current != null && branchChoiceUI == null)
                {
                    foreach (var comp in current.GetComponents<Component>())
                    {
                        if (comp != null && comp.GetType().Name == "BranchChoiceUI")
                        {
                            branchChoiceUI = comp;
                            break;
                        }
                    }
                    current = current.parent;
                }

                if (branchChoiceUI != null)
                {
                    var bcType = branchChoiceUI.GetType();
                    var allFields = bcType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    // lastHighlightedBranch is an int index, we need to find MapSection for actual data
                    int branchIndex = buttonIndex; // Default to our position-based index
                    var highlightedField = allFields.FirstOrDefault(f => f.Name == "lastHighlightedBranch");
                    if (highlightedField != null)
                    {
                        var highlightedValue = highlightedField.GetValue(branchChoiceUI);
                        if (highlightedValue is int idx)
                        {
                            branchIndex = idx;
                            MonsterTrainAccessibility.LogInfo($"lastHighlightedBranch index: {branchIndex}");
                        }
                    }

                    // Find the MapSection component which has the actual branch node data
                    Component mapSection = FindMapSectionComponent(branchChoiceUI.transform);
                    if (mapSection != null)
                    {
                        string branchInfo = GetBranchInfoFromMapSection(mapSection, branchIndex);
                        if (!string.IsNullOrEmpty(branchInfo))
                        {
                            return $"{direction} path: {branchInfo}";
                        }
                    }
                }

                // Fallback: look at direct components on the GO
                var components = go.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    var type = comp.GetType();

                    var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var field in fields)
                    {
                        string fieldName = field.Name.ToLower();
                        if (fieldName.Contains("node") || fieldName.Contains("destination") || fieldName.Contains("target") || fieldName.Contains("branch"))
                        {
                            var value = field.GetValue(comp);
                            if (value != null)
                            {
                                string info = ExtractMapNodeInfo(value);
                                if (!string.IsNullOrEmpty(info))
                                {
                                    return $"{direction} path: {info}";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting map arrow destination: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Extract info from a branch button item (from branchButtons list)
        /// </summary>
        private string ExtractBranchButtonInfo(object buttonItem)
        {
            if (buttonItem == null) return null;

            try
            {
                var itemType = buttonItem.GetType();
                var itemFields = itemType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                MonsterTrainAccessibility.LogInfo($"Extracting from {itemType.Name}, fields: {string.Join(", ", itemFields.Select(f => f.Name))}");

                // Look for node data, map node, or reward data fields
                foreach (var field in itemFields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("node") || fieldName.Contains("data") || fieldName.Contains("reward") ||
                        fieldName.Contains("branch") || fieldName.Contains("encounter"))
                    {
                        var value = field.GetValue(buttonItem);
                        if (value != null && !value.GetType().IsPrimitive && value.GetType() != typeof(string))
                        {
                            MonsterTrainAccessibility.LogInfo($"Found potential node data in field: {field.Name} ({value.GetType().Name})");
                            string info = ExtractMapNodeInfo(value);
                            if (!string.IsNullOrEmpty(info))
                            {
                                return info;
                            }
                        }
                    }
                }

                // Try methods on the button item itself
                var methods = itemType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetParameters().Length == 0)
                    .ToArray();

                foreach (var method in methods)
                {
                    string methodName = method.Name;
                    if (methodName.StartsWith("Get") &&
                        (methodName.Contains("Node") || methodName.Contains("Name") || methodName.Contains("Description") ||
                         methodName.Contains("Reward") || methodName.Contains("Encounter")))
                    {
                        try
                        {
                            var result = method.Invoke(buttonItem, null);
                            if (result is string str && !string.IsNullOrEmpty(str))
                            {
                                MonsterTrainAccessibility.LogInfo($"Got string from {methodName}: {str}");
                                return BattleAccessibility.StripRichTextTags(str);
                            }
                            else if (result != null && !result.GetType().IsPrimitive)
                            {
                                string info = ExtractMapNodeInfo(result);
                                if (!string.IsNullOrEmpty(info))
                                {
                                    return info;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting branch button info: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Find the MapSection component in the hierarchy
        /// </summary>
        private Component FindMapSectionComponent(Transform startFrom)
        {
            if (startFrom == null) return null;

            // Look up the hierarchy for MapSection
            Transform current = startFrom;
            while (current != null)
            {
                foreach (var comp in current.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == "MapSection")
                    {
                        MonsterTrainAccessibility.LogInfo($"Found MapSection on {current.name}");
                        return comp;
                    }
                }
                current = current.parent;
            }

            // Also look down the hierarchy
            foreach (var comp in startFrom.GetComponentsInChildren<Component>(true))
            {
                if (comp != null && comp.GetType().Name == "MapSection")
                {
                    MonsterTrainAccessibility.LogInfo($"Found MapSection in children: {comp.gameObject.name}");
                    return comp;
                }
            }

            return null;
        }

        /// <summary>
        /// Get branch info from a MapSection component
        /// </summary>
        private string GetBranchInfoFromMapSection(Component mapSection, int branchIndex)
        {
            if (mapSection == null) return null;

            try
            {
                var sectionType = mapSection.GetType();
                var allFields = sectionType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var allMethods = sectionType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetParameters().Length == 0 && m.Name.StartsWith("Get"))
                    .Select(m => m.Name)
                    .Take(30)
                    .ToArray();

                MonsterTrainAccessibility.LogInfo($"MapSection fields: {string.Join(", ", allFields.Select(f => f.Name))}");
                MonsterTrainAccessibility.LogInfo($"MapSection methods: {string.Join(", ", allMethods)}");

                // Look for branch data fields - common patterns
                string[] branchFieldNames = new[] {
                    "branches", "branchNodes", "branchData", "nodes",
                    "nextNodes", "choices", "rewards", "encounters",
                    "mapNodes", "sectionNodes", "nodeData"
                };

                foreach (var fieldName in branchFieldNames)
                {
                    var field = allFields.FirstOrDefault(f =>
                        f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase) ||
                        f.Name.ToLower().Contains(fieldName.ToLower()));

                    if (field != null)
                    {
                        var value = field.GetValue(mapSection);
                        MonsterTrainAccessibility.LogInfo($"Found field {field.Name}: {value?.GetType().Name ?? "null"}");

                        if (value is System.Collections.IList list && list.Count > 0)
                        {
                            MonsterTrainAccessibility.LogInfo($"Field {field.Name} is list with {list.Count} items");

                            // Get item at branchIndex or first/last based on index
                            int index = branchIndex >= 0 && branchIndex < list.Count ? branchIndex : 0;
                            var nodeData = list[index];

                            if (nodeData != null)
                            {
                                string info = ExtractMapNodeInfo(nodeData);
                                if (!string.IsNullOrEmpty(info))
                                {
                                    return info;
                                }
                            }
                        }
                        else if (value != null && !value.GetType().IsPrimitive && value.GetType() != typeof(string))
                        {
                            string info = ExtractMapNodeInfo(value);
                            if (!string.IsNullOrEmpty(info))
                            {
                                return info;
                            }
                        }
                    }
                }

                // Try methods that might return branch/node data
                string[] nodeMethodNames = new[] {
                    "GetBranches", "GetNodes", "GetNextNodes", "GetRewards",
                    "GetMapNodes", "GetNodeData", "GetCurrentNode", "GetSectionData"
                };

                foreach (var methodName in nodeMethodNames)
                {
                    var method = sectionType.GetMethod(methodName, Type.EmptyTypes);
                    if (method != null)
                    {
                        try
                        {
                            var result = method.Invoke(mapSection, null);
                            MonsterTrainAccessibility.LogInfo($"{methodName} returned: {result?.GetType().Name ?? "null"}");

                            if (result is System.Collections.IList list && list.Count > 0)
                            {
                                int index = branchIndex >= 0 && branchIndex < list.Count ? branchIndex : 0;
                                var nodeData = list[index];
                                if (nodeData != null)
                                {
                                    string info = ExtractMapNodeInfo(nodeData);
                                    if (!string.IsNullOrEmpty(info))
                                    {
                                        return info;
                                    }
                                }
                            }
                            else if (result != null && !result.GetType().IsPrimitive && result.GetType() != typeof(string))
                            {
                                string info = ExtractMapNodeInfo(result);
                                if (!string.IsNullOrEmpty(info))
                                {
                                    return info;
                                }
                            }
                        }
                        catch { }
                    }
                }

                // Look for specific data types that might contain node info
                foreach (var field in allFields)
                {
                    var value = field.GetValue(mapSection);
                    if (value == null) continue;

                    var valueType = value.GetType();
                    string typeName = valueType.Name.ToLower();

                    // Look for types that sound like they contain node/reward data
                    if (typeName.Contains("node") || typeName.Contains("reward") ||
                        typeName.Contains("encounter") || typeName.Contains("branch"))
                    {
                        MonsterTrainAccessibility.LogInfo($"Found potential data type: {field.Name} = {valueType.Name}");

                        if (value is System.Collections.IList list && list.Count > 0)
                        {
                            int index = branchIndex >= 0 && branchIndex < list.Count ? branchIndex : 0;
                            string info = ExtractMapNodeInfo(list[index]);
                            if (!string.IsNullOrEmpty(info))
                            {
                                return info;
                            }
                        }
                        else
                        {
                            string info = ExtractMapNodeInfo(value);
                            if (!string.IsNullOrEmpty(info))
                            {
                                return info;
                            }
                        }
                    }
                }

                // Look for branch node arrays/lists with left/right entries
                string[] branchArrayNames = new[] { "branchNodeUIs", "branchNodes", "nodeUIs", "leftNode", "rightNode" };
                foreach (var arrayName in branchArrayNames)
                {
                    var field = allFields.FirstOrDefault(f => f.Name.ToLower().Contains(arrayName.ToLower()));
                    if (field != null)
                    {
                        var value = field.GetValue(mapSection);
                        if (value is System.Collections.IList list && list.Count > branchIndex && branchIndex >= 0)
                        {
                            var nodeUI = list[branchIndex];
                            string info = ExtractMapNodeUIInfo(nodeUI);
                            if (!string.IsNullOrEmpty(info))
                            {
                                return info;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting branch info from MapSection: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract info from a MapBattleNodeUI or similar UI component
        /// </summary>
        private string ExtractMapNodeUIInfo(object nodeUI)
        {
            if (nodeUI == null) return null;

            try
            {
                var uiType = nodeUI.GetType();
                var uiMethods = uiType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                var uiFields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // First priority: Try GetMapNodeDataName() method - this should give us the actual name
                var getNameMethod = uiMethods.FirstOrDefault(m => m.Name == "GetMapNodeDataName" && m.GetParameters().Length == 0);
                if (getNameMethod != null)
                {
                    try
                    {
                        var nodeName = getNameMethod.Invoke(nodeUI, null) as string;
                        if (!string.IsNullOrEmpty(nodeName))
                        {
                            MonsterTrainAccessibility.LogInfo($"GetMapNodeDataName returned: {nodeName}");
                            return BattleAccessibility.StripRichTextTags(nodeName);
                        }
                    }
                    catch { }
                }

                // Second priority: Try GetData() method to get the actual node data
                var getDataMethod = uiMethods.FirstOrDefault(m => m.Name == "GetData" && m.GetParameters().Length == 0);
                if (getDataMethod != null)
                {
                    try
                    {
                        var data = getDataMethod.Invoke(nodeUI, null);
                        if (data != null)
                        {
                            MonsterTrainAccessibility.LogInfo($"GetData returned: {data.GetType().Name}");
                            string dataInfo = ExtractNodeDataInfo(data);
                            if (!string.IsNullOrEmpty(dataInfo))
                            {
                                return dataInfo;
                            }
                        }
                    }
                    catch { }
                }

                // Third priority: Try "data" field
                var dataField = uiFields.FirstOrDefault(f => f.Name == "data");
                if (dataField != null)
                {
                    var data = dataField.GetValue(nodeUI);
                    if (data != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"data field contains: {data.GetType().Name}");
                        string dataInfo = ExtractNodeDataInfo(data);
                        if (!string.IsNullOrEmpty(dataInfo))
                        {
                            return dataInfo;
                        }
                    }
                }

                // Fourth priority: Try tooltipProvider component
                var providerField = uiFields.FirstOrDefault(f => f.Name == "tooltipProvider");
                if (providerField != null)
                {
                    var provider = providerField.GetValue(nodeUI);
                    if (provider != null)
                    {
                        var providerInfo = ExtractTooltipProviderInfo(provider);
                        if (!string.IsNullOrEmpty(providerInfo))
                        {
                            MonsterTrainAccessibility.LogInfo($"Got info from tooltipProvider: {providerInfo}");
                            return providerInfo;
                        }
                    }
                }

                // Fifth priority: Check if boss and get basic info
                bool isBoss = false;
                var bossField = uiFields.FirstOrDefault(f => f.Name == "isBoss");
                if (bossField != null)
                {
                    var bossValue = bossField.GetValue(nodeUI);
                    if (bossValue is bool b)
                        isBoss = b;
                }

                if (isBoss)
                {
                    return "Boss Battle";
                }

                // Last resort: tooltip title/content (localization keys)
                string title = null;
                var titleField = uiFields.FirstOrDefault(f => f.Name == "defaultTooltipTitle");
                if (titleField != null)
                {
                    title = titleField.GetValue(nodeUI) as string;
                    if (!string.IsNullOrEmpty(title))
                    {
                        // Try to localize
                        string localized = LocalizeString(title);
                        if (!string.IsNullOrEmpty(localized) && localized != title)
                        {
                            return BattleAccessibility.StripRichTextTags(localized);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting map node UI info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract info from node data (the actual game data, not UI)
        /// </summary>
        private string ExtractNodeDataInfo(object data)
        {
            if (data == null) return null;

            try
            {
                var dataType = data.GetType();
                MonsterTrainAccessibility.LogInfo($"Extracting node data from: {dataType.Name}");

                // Log available methods
                var methods = dataType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetParameters().Length == 0 && m.Name.StartsWith("Get"))
                    .Select(m => m.Name)
                    .Take(20)
                    .ToArray();
                MonsterTrainAccessibility.LogInfo($"Data methods: {string.Join(", ", methods)}");

                // Try GetName first
                var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(data, null) as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        MonsterTrainAccessibility.LogInfo($"GetName returned: {name}");
                        return BattleAccessibility.StripRichTextTags(name);
                    }
                }

                // Try GetTooltipTitle
                var getTitleMethod = dataType.GetMethod("GetTooltipTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    var title = getTitleMethod.Invoke(data, null) as string;
                    if (!string.IsNullOrEmpty(title))
                    {
                        MonsterTrainAccessibility.LogInfo($"GetTooltipTitle returned: {title}");
                        return BattleAccessibility.StripRichTextTags(title);
                    }
                }

                // Try GetDescription
                var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    var desc = getDescMethod.Invoke(data, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                    {
                        MonsterTrainAccessibility.LogInfo($"GetDescription returned: {desc}");
                        return BattleAccessibility.StripRichTextTags(desc);
                    }
                }

                // Try to get reward info for reward nodes
                var getRewardMethod = dataType.GetMethod("GetReward", Type.EmptyTypes) ??
                                     dataType.GetMethod("GetRewardData", Type.EmptyTypes);
                if (getRewardMethod != null)
                {
                    var reward = getRewardMethod.Invoke(data, null);
                    if (reward != null)
                    {
                        string rewardInfo = ExtractRewardInfo(reward);
                        if (!string.IsNullOrEmpty(rewardInfo))
                        {
                            return rewardInfo;
                        }
                    }
                }

                // Look at fields
                var fields = dataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MonsterTrainAccessibility.LogInfo($"Data fields: {string.Join(", ", fields.Select(f => f.Name).Take(15))}");

                // Try name field
                var nameField = fields.FirstOrDefault(f => f.Name.ToLower() == "name" || f.Name.ToLower().Contains("nodename"));
                if (nameField != null)
                {
                    var name = nameField.GetValue(data) as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        return BattleAccessibility.StripRichTextTags(LocalizeString(name));
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting node data info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Localize a string using the game's localization system
        /// </summary>
        private string LocalizeString(string key)
        {
            if (string.IsNullOrEmpty(key))
                return key;

            // Use the shared TryLocalize method which caches the localization method
            return TryLocalize(key) ?? key;
        }

        /// <summary>
        /// Extract info from a tooltip provider component
        /// </summary>
        private string ExtractTooltipProviderInfo(object provider)
        {
            if (provider == null) return null;

            try
            {
                var providerType = provider.GetType();

                // Try GetTitle/GetDescription methods
                var getTitleMethod = providerType.GetMethod("GetTitle", Type.EmptyTypes) ??
                                    providerType.GetMethod("GetTooltipTitle", Type.EmptyTypes);
                var getDescMethod = providerType.GetMethod("GetDescription", Type.EmptyTypes) ??
                                   providerType.GetMethod("GetTooltipDescription", Type.EmptyTypes) ??
                                   providerType.GetMethod("GetTooltipBody", Type.EmptyTypes);

                string title = null;
                string desc = null;

                if (getTitleMethod != null)
                {
                    title = getTitleMethod.Invoke(provider, null) as string;
                }
                if (getDescMethod != null)
                {
                    desc = getDescMethod.Invoke(provider, null) as string;
                }

                // Also try fields
                var providerFields = providerType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (string.IsNullOrEmpty(title))
                {
                    var titleField = providerFields.FirstOrDefault(f => f.Name.ToLower().Contains("title"));
                    if (titleField != null)
                        title = titleField.GetValue(provider) as string;
                }
                if (string.IsNullOrEmpty(desc))
                {
                    var descField = providerFields.FirstOrDefault(f =>
                        f.Name.ToLower().Contains("desc") || f.Name.ToLower().Contains("content") || f.Name.ToLower().Contains("body"));
                    if (descField != null)
                        desc = descField.GetValue(provider) as string;
                }

                List<string> parts = new List<string>();
                if (!string.IsNullOrEmpty(title))
                    parts.Add(TryLocalize(title));
                if (!string.IsNullOrEmpty(desc) && desc != title)
                    parts.Add(TryLocalize(desc));

                if (parts.Count > 0)
                {
                    return BattleAccessibility.StripRichTextTags(string.Join(". ", parts));
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Extract readable info from a map node data object
        /// </summary>
        private string ExtractMapNodeInfo(object nodeData)
        {
            try
            {
                var nodeType = nodeData.GetType();
                MonsterTrainAccessibility.LogInfo($"Extracting info from node type: {nodeType.Name}");

                // If this is a UI component (MapBattleNodeUI, etc.), use specialized extraction
                if (nodeType.Name.Contains("NodeUI") || nodeType.Name.Contains("MapNode"))
                {
                    string uiInfo = ExtractMapNodeUIInfo(nodeData);
                    if (!string.IsNullOrEmpty(uiInfo))
                    {
                        return uiInfo;
                    }
                }

                // Log methods available
                var methods = nodeType.GetMethods()
                    .Where(m => m.GetParameters().Length == 0 && m.Name.StartsWith("Get"))
                    .Select(m => m.Name)
                    .Take(30)
                    .ToArray();
                MonsterTrainAccessibility.LogInfo($"Node methods: {string.Join(", ", methods)}");

                // Also log fields
                var fields = nodeType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Select(f => f.Name)
                    .Take(20)
                    .ToArray();
                MonsterTrainAccessibility.LogInfo($"Node fields: {string.Join(", ", fields)}");

                string name = null;
                string description = null;
                string nodeTypeStr = null;

                // Try to get node type first (battle, event, shop, etc.)
                var getTypeMethod = nodeType.GetMethod("GetNodeType", Type.EmptyTypes) ??
                                   nodeType.GetMethod("GetMapNodeType", Type.EmptyTypes) ??
                                   nodeType.GetMethod("GetRewardType", Type.EmptyTypes);
                if (getTypeMethod != null)
                {
                    var nodeTypeValue = getTypeMethod.Invoke(nodeData, null);
                    if (nodeTypeValue != null)
                    {
                        nodeTypeStr = nodeTypeValue.ToString();
                        MonsterTrainAccessibility.LogInfo($"Node type: {nodeTypeStr}");
                    }
                }

                // Try various methods to get name
                string[] nameMethodNames = new[] {
                    "GetName", "GetTitle", "GetDisplayName", "GetNodeName",
                    "GetNameKey", "GetTitleKey", "GetTooltipTitle",
                    "GetRewardName", "GetEncounterName"
                };

                foreach (var methodName in nameMethodNames)
                {
                    var getNameMethod = nodeType.GetMethod(methodName, Type.EmptyTypes);
                    if (getNameMethod != null)
                    {
                        var result = getNameMethod.Invoke(nodeData, null);
                        if (result is string str && !string.IsNullOrEmpty(str))
                        {
                            MonsterTrainAccessibility.LogInfo($"{methodName} returned: {str}");
                            // Try to localize if it looks like a key
                            name = TryLocalize(str);
                            if (!string.IsNullOrEmpty(name))
                                break;
                        }
                    }
                }

                // Try to get description
                string[] descMethodNames = new[] {
                    "GetDescription", "GetTooltipDescription", "GetDescriptionKey",
                    "GetRewardDescription", "GetTooltipBody"
                };

                foreach (var methodName in descMethodNames)
                {
                    var getDescMethod = nodeType.GetMethod(methodName, Type.EmptyTypes);
                    if (getDescMethod != null)
                    {
                        var result = getDescMethod.Invoke(nodeData, null);
                        if (result is string str && !string.IsNullOrEmpty(str))
                        {
                            MonsterTrainAccessibility.LogInfo($"{methodName} returned: {str}");
                            description = TryLocalize(str);
                            if (!string.IsNullOrEmpty(description))
                                break;
                        }
                    }
                }

                // Try to get reward data (for reward nodes)
                var getRewardMethod = nodeType.GetMethod("GetRewardData", Type.EmptyTypes) ??
                                     nodeType.GetMethod("GetReward", Type.EmptyTypes);
                if (getRewardMethod != null)
                {
                    var reward = getRewardMethod.Invoke(nodeData, null);
                    if (reward != null)
                    {
                        string rewardInfo = ExtractRewardInfo(reward);
                        if (!string.IsNullOrEmpty(rewardInfo))
                        {
                            if (string.IsNullOrEmpty(name))
                                name = rewardInfo;
                            else
                                description = rewardInfo;
                        }
                    }
                }

                // Build result
                List<string> parts = new List<string>();

                if (!string.IsNullOrEmpty(nodeTypeStr) && nodeTypeStr != name)
                {
                    // Convert enum-style type to readable name
                    string readableType = FormatNodeType(nodeTypeStr);
                    if (!string.IsNullOrEmpty(readableType))
                        parts.Add(readableType);
                }

                if (!string.IsNullOrEmpty(name))
                {
                    parts.Add(BattleAccessibility.StripRichTextTags(name));
                }

                if (!string.IsNullOrEmpty(description) && description != name)
                {
                    parts.Add(BattleAccessibility.StripRichTextTags(description));
                }

                if (parts.Count > 0)
                {
                    return string.Join(". ", parts);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting map node info: {ex.Message}");
            }
            return null;
        }

        // Cached localization method
        private static MethodInfo _cachedLocalizeMethod = null;
        private static bool _localizeMethodSearched = false;

        /// <summary>
        /// Try to localize a string (if it's a localization key)
        /// </summary>
        private string TryLocalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            try
            {
                // Check if it looks like a localization key (typically contains _ or is UPPERCASE)
                if (!text.Contains("_") && text != text.ToUpperInvariant())
                    return text;

                // Find and cache the Localize method once
                if (!_localizeMethodSearched)
                {
                    _localizeMethodSearched = true;
                    FindLocalizeMethod();
                }

                if (_cachedLocalizeMethod != null)
                {
                    // Build args array with default values for optional params
                    var parameters = _cachedLocalizeMethod.GetParameters();
                    var args = new object[parameters.Length];
                    args[0] = text;
                    for (int i = 1; i < parameters.Length; i++)
                    {
                        args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                    }

                    var localized = _cachedLocalizeMethod.Invoke(null, args) as string;
                    if (!string.IsNullOrEmpty(localized) && localized != text)
                    {
                        return localized;
                    }
                }
            }
            catch { }

            return text;
        }

        private void FindLocalizeMethod()
        {
            try
            {
                // Search in Assembly-CSharp for static extension classes
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var asmName = assembly.GetName().Name;
                    if (!asmName.Contains("Assembly-CSharp") && !asmName.Contains("Trainworks"))
                        continue;

                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            // Extension classes are static (abstract + sealed)
                            if (!type.IsClass)
                                continue;

                            var method = type.GetMethod("Localize", BindingFlags.Public | BindingFlags.Static);
                            if (method != null && method.ReturnType == typeof(string))
                            {
                                var parameters = method.GetParameters();
                                if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(string))
                                {
                                    _cachedLocalizeMethod = method;
                                    MonsterTrainAccessibility.LogInfo($"Found Localize method in {type.FullName}");
                                    return;
                                }
                            }
                        }
                    }
                    catch { }
                }
                MonsterTrainAccessibility.LogWarning("Could not find Localize method");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error finding Localize method: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract info from a reward data object
        /// </summary>
        private string ExtractRewardInfo(object reward)
        {
            if (reward == null) return null;

            try
            {
                var rewardType = reward.GetType();

                // Try to get name
                var getNameMethod = rewardType.GetMethod("GetName", Type.EmptyTypes) ??
                                   rewardType.GetMethod("GetDisplayName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(reward, null) as string;
                    if (!string.IsNullOrEmpty(name))
                        return TryLocalize(name);
                }

                // Try to get description
                var getDescMethod = rewardType.GetMethod("GetDescription", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    var desc = getDescMethod.Invoke(reward, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                        return TryLocalize(desc);
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Format a node type enum value to readable text
        /// </summary>
        private string FormatNodeType(string nodeType)
        {
            if (string.IsNullOrEmpty(nodeType))
                return null;

            // Common node type mappings
            switch (nodeType.ToLower())
            {
                case "battle":
                case "combat":
                    return "Battle";
                case "event":
                case "randomchoice":
                    return "Event";
                case "shop":
                case "merchant":
                    return "Shop";
                case "upgrade":
                case "forge":
                    return "Upgrade";
                case "artifact":
                case "relic":
                    return "Artifact";
                case "healing":
                case "rest":
                    return "Healing";
                case "boss":
                case "bossbattle":
                    return "Boss";
                case "unitreward":
                case "unit":
                    return "Unit Reward";
                case "cardreward":
                case "card":
                    return "Card Reward";
                default:
                    // Add spaces before capitals and clean up
                    return System.Text.RegularExpressions.Regex.Replace(nodeType, "([a-z])([A-Z])", "$1 $2");
            }
        }

        /// <summary>
        /// Determine the type of node in a branch (Battle, Event, Shop, etc.)
        /// </summary>
        private string GetBranchNodeType(GameObject go)
        {
            try
            {
                // Look for components or child objects that indicate node type
                var allChildren = go.GetComponentsInChildren<Transform>(true);
                foreach (var child in allChildren)
                {
                    string name = child.name.ToLower();

                    // Check for type indicators in object names
                    if (name.Contains("battle") || name.Contains("combat") || name.Contains("fight"))
                        return "Battle";
                    if (name.Contains("event") || name.Contains("cavern"))
                        return "Event";
                    if (name.Contains("shop") || name.Contains("merchant") || name.Contains("store"))
                        return "Shop";
                    if (name.Contains("upgrade") || name.Contains("forge"))
                        return "Upgrade";
                    if (name.Contains("heal") || name.Contains("restore") || name.Contains("rest"))
                        return "Heal";
                    if (name.Contains("artifact") || name.Contains("relic"))
                        return "Artifact";
                    if (name.Contains("boss"))
                        return "Boss Battle";
                }

                // Check components for type info
                foreach (var child in allChildren)
                {
                    var components = child.GetComponents<Component>();
                    foreach (var comp in components)
                    {
                        if (comp == null) continue;
                        string typeName = comp.GetType().Name.ToLower();

                        if (typeName.Contains("battle"))
                            return "Battle";
                        if (typeName.Contains("event"))
                            return "Event";
                        if (typeName.Contains("shop"))
                            return "Shop";
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get enemy info from a branch node (for battle nodes)
        /// </summary>
        private string GetBranchEnemyInfo(GameObject go)
        {
            try
            {
                // Look for tooltip data that might contain enemy names
                string tooltip = GetTooltipTextWithBody(go);
                if (!string.IsNullOrEmpty(tooltip) && !tooltip.Contains("Enemy_Tooltip"))
                {
                    return BattleAccessibility.StripRichTextTags(tooltip);
                }

                // Look for TMP text in children that might be enemy names
                var allChildren = go.GetComponentsInChildren<Transform>(true);
                foreach (var child in allChildren)
                {
                    string text = GetTMPTextDirect(child.gameObject);
                    if (!string.IsNullOrEmpty(text) && text.Length > 1)
                    {
                        // Filter out single letters and generic labels
                        text = text.Trim();
                        if (text.Length > 2 && !text.Equals("A", StringComparison.OrdinalIgnoreCase) &&
                            !text.Equals("B", StringComparison.OrdinalIgnoreCase))
                        {
                            return BattleAccessibility.StripRichTextTags(text);
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get first meaningful text from child elements
        /// </summary>
        private string GetFirstMeaningfulChildText(GameObject go)
        {
            try
            {
                var allChildren = go.GetComponentsInChildren<Transform>(true);
                foreach (var child in allChildren)
                {
                    if (child == go.transform) continue;

                    string text = GetTMPTextDirect(child.gameObject);
                    if (!string.IsNullOrEmpty(text))
                    {
                        text = text.Trim();
                        // Skip single letters and very short text
                        if (text.Length > 2)
                        {
                            return BattleAccessibility.StripRichTextTags(text);
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get text for map nodes (battles, events, shops, etc.)
        /// Extracts proper encounter names instead of just button labels like "Fight!"
        /// </summary>
        private string GetMapNodeText(GameObject go)
        {
            try
            {
                // Debug: Log all components on this object and parents to understand structure
                LogMapNodeComponents(go);

                // Check for Minimap components first (Monster Train's map system)
                var mapInfo = GetMinimapNodeInfo(go);
                if (mapInfo != null)
                {
                    return mapInfo;
                }

                // Look for MapNodeIcon or similar component on this object or parents
                Component mapNodeComponent = null;
                Transform current = go.transform;

                while (current != null && mapNodeComponent == null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;

                        // Look for various map-related component names
                        if (typeName.Contains("MapNode") ||
                            typeName.Contains("NodeIcon") ||
                            typeName.Contains("MapIcon") ||
                            typeName.Contains("RouteNode"))
                        {
                            mapNodeComponent = component;
                            MonsterTrainAccessibility.LogInfo($"Found map component: {typeName}");
                            break;
                        }
                    }
                    current = current.parent;
                }

                if (mapNodeComponent == null)
                {
                    // Try finding tooltip data directly from the selected object
                    string tooltipText = GetTooltipTextWithBody(go);
                    if (!string.IsNullOrEmpty(tooltipText) && !tooltipText.Contains("Enemy_Tooltip"))
                    {
                        return tooltipText;
                    }
                    return null;
                }

                // Try to get the MapNodeData from the component
                var iconType = mapNodeComponent.GetType();
                object mapNodeData = null;

                // Try common field/property names for the node data
                var dataField = iconType.GetField("mapNodeData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (dataField != null)
                {
                    mapNodeData = dataField.GetValue(mapNodeComponent);
                }
                else
                {
                    var dataProp = iconType.GetProperty("MapNodeData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (dataProp != null)
                    {
                        mapNodeData = dataProp.GetValue(mapNodeComponent);
                    }
                }

                // Also try _mapNodeData (common naming convention)
                if (mapNodeData == null)
                {
                    dataField = iconType.GetField("_mapNodeData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (dataField != null)
                    {
                        mapNodeData = dataField.GetValue(mapNodeComponent);
                    }
                }

                if (mapNodeData == null)
                {
                    // MapNodeUI found but no mapNodeData - try tooltip fallback
                    MonsterTrainAccessibility.LogInfo($"MapNodeUI found but mapNodeData is null, trying tooltip fallback");
                    string tooltipText = GetTooltipTextWithBody(go);
                    MonsterTrainAccessibility.LogInfo($"Tooltip fallback result: '{tooltipText ?? "null"}'");
                    if (!string.IsNullOrEmpty(tooltipText) && !tooltipText.Contains("Enemy_Tooltip"))
                    {
                        // Clean sprite tags from tooltip
                        return CleanSpriteTagsForSpeech(tooltipText);
                    }
                    // Log available fields for debugging
                    LogMapNodeUIFields(mapNodeComponent);
                    return null;
                }

                var nodeDataType = mapNodeData.GetType();
                string nodeName = nodeDataType.Name;

                // Check if this is a ScenarioData (battle node)
                if (nodeName == "ScenarioData" || nodeDataType.BaseType?.Name == "ScenarioData")
                {
                    return GetBattleNodeName(mapNodeData, nodeDataType);
                }

                // For other node types (rewards, merchants, etc.), try tooltipTitleKey
                return GetGenericNodeName(mapNodeData, nodeDataType);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting map node text: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get comprehensive info from Minimap nodes (MinimapNodeMarker, MinimapBattleNode)
        /// </summary>
        private string GetMinimapNodeInfo(GameObject go)
        {
            try
            {
                Transform current = go.transform;
                Component minimapComponent = null;
                string componentType = null;
                string pathPosition = null; // left, right, center

                // Find minimap component
                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;

                        if (typeName == "MinimapNodeMarker" || typeName == "MinimapBattleNode")
                        {
                            minimapComponent = component;
                            componentType = typeName;
                            break;
                        }
                    }
                    if (minimapComponent != null) break;
                    current = current.parent;
                }

                if (minimapComponent == null)
                    return null;

                var sb = new StringBuilder();

                // Check if this is the current player position
                bool isCurrentPosition = IsCurrentMapPosition(minimapComponent);

                // Determine path position from parent hierarchy
                pathPosition = DeterminePathPosition(minimapComponent.transform);

                // Get ring/section info and section index for coordinates
                string ringInfo = GetRingInfo(minimapComponent.transform, out int ringIndex);

                // Get tooltip info (title and body)
                string title = null;
                string body = null;
                GetTooltipTitleAndBody(go, out title, out body);

                // Build the announcement with coordinate first
                string coordinate = BuildCoordinate(ringIndex, pathPosition);
                if (!string.IsNullOrEmpty(coordinate))
                {
                    sb.Append(coordinate);
                    sb.Append(": ");
                }

                // Mark current position
                if (isCurrentPosition)
                {
                    sb.Append("Current position. ");
                }

                if (componentType == "MinimapBattleNode")
                {
                    sb.Append("Battle");
                    if (!string.IsNullOrEmpty(title) && title != "Battle")
                    {
                        sb.Append($" - {title}");
                    }
                }
                else if (!string.IsNullOrEmpty(title))
                {
                    sb.Append(title);
                }
                else
                {
                    sb.Append("Unknown node");
                }

                // Add body/description if available
                if (!string.IsNullOrEmpty(body))
                {
                    sb.Append($". {body}");
                }

                // Find available directions from sibling nodes
                string availableDirections = GetAvailableDirections(minimapComponent.transform, pathPosition);
                if (!string.IsNullOrEmpty(availableDirections))
                {
                    sb.Append($". {availableDirections}");
                }

                string result = sb.ToString();
                MonsterTrainAccessibility.LogInfo($"Map node info: {result}");
                return result;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting minimap node info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Check if a map node represents the player's current position
        /// </summary>
        private bool IsCurrentMapPosition(Component minimapComponent)
        {
            try
            {
                var type = minimapComponent.GetType();

                // Check for "isCurrent", "isCurrentNode", "current", "isActive" type fields
                string[] currentFieldNames = { "isCurrent", "_isCurrent", "isCurrentNode", "_isCurrentNode",
                                                 "current", "_current", "isActive", "_isActive",
                                                 "isCompleted", "_isCompleted", "completed", "_completed" };

                foreach (var fieldName in currentFieldNames)
                {
                    var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null && field.FieldType == typeof(bool))
                    {
                        bool value = (bool)field.GetValue(minimapComponent);
                        // "isCurrent" means current position, "isCompleted" means past position
                        if (fieldName.ToLower().Contains("current") && value)
                            return true;
                    }

                    var prop = type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null && prop.PropertyType == typeof(bool) && prop.CanRead)
                    {
                        bool value = (bool)prop.GetValue(minimapComponent);
                        if (fieldName.ToLower().Contains("current") && value)
                            return true;
                    }
                }

                // Check the GameObject name or children for "current" indicator
                string goName = minimapComponent.gameObject.name.ToLower();
                if (goName.Contains("current") || goName.Contains("active") || goName.Contains("player"))
                    return true;

                // Check for a child object that might indicate current position
                var transform = minimapComponent.transform;
                foreach (Transform child in transform)
                {
                    if (child == null || !child.gameObject.activeInHierarchy)
                        continue;

                    string childName = child.name.ToLower();
                    if (childName.Contains("current") || childName.Contains("indicator") ||
                        childName.Contains("player") || childName.Contains("marker"))
                    {
                        // Check if this indicator is actually visible/active
                        if (child.gameObject.activeInHierarchy)
                            return true;
                    }
                }

            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error checking current map position: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Build a coordinate string like "Ring 3, Left" for position identification
        /// </summary>
        private string BuildCoordinate(int ringIndex, string pathPosition)
        {
            var parts = new List<string>();

            if (ringIndex >= 0)
            {
                parts.Add($"Ring {ringIndex + 1}");
            }

            if (!string.IsNullOrEmpty(pathPosition))
            {
                // Simplify "Left path" to "Left" for coordinate
                string pos = pathPosition.Replace(" path", "");
                parts.Add(pos);
            }

            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        /// <summary>
        /// Find available directions by scanning sibling nodes in the same ring/section
        /// </summary>
        private string GetAvailableDirections(Transform currentNodeTransform, string currentPosition)
        {
            try
            {
                // Find the parent section that contains map nodes
                Transform sectionParent = FindMapSection(currentNodeTransform);
                if (sectionParent == null)
                    return null;

                var availablePositions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Scan all descendants for other map nodes
                ScanForMapNodes(sectionParent, currentNodeTransform, availablePositions);

                // Remove current position
                if (!string.IsNullOrEmpty(currentPosition))
                {
                    availablePositions.Remove(currentPosition);
                    availablePositions.Remove(currentPosition.Replace(" path", ""));
                }

                if (availablePositions.Count == 0)
                    return null;

                // Build direction string
                var directions = new List<string>();
                if (availablePositions.Contains("Left") || availablePositions.Contains("Left path"))
                    directions.Add("left");
                if (availablePositions.Contains("Center"))
                    directions.Add("center");
                if (availablePositions.Contains("Right") || availablePositions.Contains("Right path"))
                    directions.Add("right");

                if (directions.Count == 0)
                    return null;

                return $"Can go {string.Join(", ", directions)}";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting available directions: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Find the parent section/container that holds map nodes for this ring
        /// </summary>
        private Transform FindMapSection(Transform nodeTransform)
        {
            Transform current = nodeTransform.parent;
            while (current != null)
            {
                // Check if this is a section container
                string name = current.name.ToLower();
                if (name.Contains("section") || name.Contains("ring") || name.Contains("row"))
                {
                    return current;
                }

                // Check for MinimapSection component
                foreach (var component in current.GetComponents<Component>())
                {
                    if (component != null && component.GetType().Name == "MinimapSection")
                    {
                        return current;
                    }
                }

                current = current.parent;
            }

            // Fallback: use grandparent or parent
            if (nodeTransform.parent != null)
                return nodeTransform.parent.parent ?? nodeTransform.parent;

            return null;
        }

        /// <summary>
        /// Recursively scan for map nodes and collect their positions
        /// </summary>
        private void ScanForMapNodes(Transform parent, Transform excludeNode, HashSet<string> positions)
        {
            if (parent == null)
                return;

            foreach (Transform child in parent)
            {
                if (child == null || !child.gameObject.activeInHierarchy)
                    continue;

                // Skip the node we're currently on
                if (child == excludeNode || IsDescendantOf(excludeNode, child))
                    continue;

                // Check if this is a map node
                bool isMapNode = false;
                foreach (var component in child.GetComponents<Component>())
                {
                    if (component == null) continue;
                    string typeName = component.GetType().Name;
                    if (typeName == "MinimapNodeMarker" || typeName == "MinimapBattleNode")
                    {
                        isMapNode = true;
                        break;
                    }
                }

                if (isMapNode)
                {
                    string pos = DeterminePathPosition(child);
                    if (!string.IsNullOrEmpty(pos))
                    {
                        positions.Add(pos.Replace(" path", ""));
                    }
                }

                // Recurse into children
                ScanForMapNodes(child, excludeNode, positions);
            }
        }

        /// <summary>
        /// Check if excludeNode is a descendant of potentialAncestor
        /// </summary>
        private bool IsDescendantOf(Transform node, Transform potentialAncestor)
        {
            if (node == null || potentialAncestor == null)
                return false;

            Transform current = node;
            while (current != null)
            {
                if (current == potentialAncestor)
                    return true;
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Determine if this node is on the left path, right path, or center
        /// </summary>
        private string DeterminePathPosition(Transform nodeTransform)
        {
            try
            {
                // Walk up the hierarchy looking for position indicators
                Transform current = nodeTransform;
                while (current != null)
                {
                    string name = current.name.ToLower();

                    if (name.Contains("left"))
                        return "Left path";
                    if (name.Contains("right"))
                        return "Right path";
                    if (name.Contains("center") || name.Contains("shared"))
                        return "Center";

                    // Check if parent is a node layout
                    if (current.parent != null)
                    {
                        string parentName = current.parent.name.ToLower();
                        if (parentName.Contains("left"))
                            return "Left path";
                        if (parentName.Contains("right"))
                            return "Right path";
                        if (parentName.Contains("center"))
                            return "Center";
                    }

                    current = current.parent;
                }

                // Check position relative to screen center as fallback
                var rectTransform = nodeTransform.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    float xPos = rectTransform.position.x;
                    float screenCenter = Screen.width / 2f;
                    float threshold = Screen.width * 0.1f;

                    if (xPos < screenCenter - threshold)
                        return "Left path";
                    if (xPos > screenCenter + threshold)
                        return "Right path";
                    return "Center";
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error determining path position: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the ring/section info for a map node
        /// </summary>
        private string GetRingInfo(Transform nodeTransform, out int ringIndex)
        {
            ringIndex = -1;

            try
            {
                // Look for MinimapSection in parents
                Transform current = nodeTransform;
                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "MinimapSection")
                        {
                            // Try to get ring number from the section
                            var sectionType = component.GetType();

                            // Try various field names
                            string[] ringFieldNames = { "ringIndex", "_ringIndex", "sectionIndex", "_sectionIndex", "index", "_index" };
                            foreach (var fieldName in ringFieldNames)
                            {
                                var field = sectionType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (field != null)
                                {
                                    var value = field.GetValue(component);
                                    if (value != null)
                                    {
                                        ringIndex = Convert.ToInt32(value);
                                        return $"Ring {ringIndex + 1}";
                                    }
                                }
                            }

                            // Try to extract from the section's name or label
                            var labelField = sectionType.GetField("ringLabel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (labelField == null)
                                labelField = sectionType.GetField("_ringLabel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                            if (labelField != null)
                            {
                                var labelObj = labelField.GetValue(component);
                                if (labelObj != null)
                                {
                                    string labelText = GetTextFromComponent(labelObj);
                                    if (!string.IsNullOrEmpty(labelText))
                                    {
                                        return labelText;
                                    }
                                }
                            }
                        }
                    }

                    // Also check the object's name for ring number
                    if (current.name.Contains("section") || current.name.Contains("Section"))
                    {
                        // Try to extract number from name like "Minimap section(Clone)"
                        var match = System.Text.RegularExpressions.Regex.Match(current.name, @"(\d+)");
                        if (match.Success)
                        {
                            if (int.TryParse(match.Groups[1].Value, out int parsed))
                            {
                                ringIndex = parsed - 1; // Adjust to 0-based
                            }
                            return $"Ring {match.Groups[1].Value}";
                        }
                    }

                    current = current.parent;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting ring info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get both title and body from tooltip
        /// </summary>
        private void GetTooltipTitleAndBody(GameObject go, out string title, out string body)
        {
            title = null;
            body = null;

            try
            {
                Transform current = go.transform;
                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "TooltipProviderComponent")
                        {
                            var type = component.GetType();
                            var tooltipsField = type.GetField("_tooltips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (tooltipsField != null)
                            {
                                var tooltipsList = tooltipsField.GetValue(component) as System.Collections.IList;
                                if (tooltipsList != null && tooltipsList.Count > 0)
                                {
                                    var tooltip = tooltipsList[0];
                                    var tooltipType = tooltip.GetType();

                                    // Get title and localize if it's a key
                                    var titleField = tooltipType.GetField("title", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (titleField != null)
                                    {
                                        string rawTitle = titleField.GetValue(tooltip) as string;
                                        if (!string.IsNullOrEmpty(rawTitle))
                                        {
                                            // Try to localize - if it looks like a key
                                            title = TryLocalize(rawTitle);
                                            if (string.IsNullOrEmpty(title))
                                                title = rawTitle;
                                        }
                                    }

                                    // Get body and localize if it's a key
                                    var bodyField = tooltipType.GetField("body", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (bodyField != null)
                                    {
                                        string rawBody = bodyField.GetValue(tooltip) as string;
                                        if (!string.IsNullOrEmpty(rawBody))
                                        {
                                            // Try to localize - if it looks like a key
                                            body = TryLocalize(rawBody);
                                            if (string.IsNullOrEmpty(body))
                                                body = rawBody;
                                        }
                                    }

                                    return;
                                }
                            }
                        }
                    }
                    current = current.parent;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting tooltip title and body: {ex.Message}");
            }
        }

        /// <summary>
        /// Get tooltip text including body/description
        /// </summary>
        private string GetTooltipTextWithBody(GameObject go)
        {
            string title, body;
            GetTooltipTitleAndBody(go, out title, out body);

            if (!string.IsNullOrEmpty(title))
            {
                if (!string.IsNullOrEmpty(body))
                {
                    return $"{title}. {body}";
                }
                return title;
            }

            return null;
        }

        /// <summary>
        /// Get battle info when on the Fight button of BattleIntro screen
        /// </summary>
        private string GetBattleIntroText(GameObject go)
        {
            try
            {
                // Check if this is the Fight button
                string goName = go.name.ToLower();
                if (!goName.Contains("fight"))
                    return null;

                // Look for BattleIntroScreen component in parents
                Component battleIntroScreen = null;
                Transform current = go.transform;

                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;
                        if (typeName == "BattleIntroScreen")
                        {
                            battleIntroScreen = component;
                            break;
                        }
                    }
                    if (battleIntroScreen != null) break;
                    current = current.parent;
                }

                if (battleIntroScreen == null)
                    return null;

                // Try to get the scenario/battle info from BattleIntroScreen
                var screenType = battleIntroScreen.GetType();

                // Log all fields on BattleIntroScreen to find scenario data
                LogScreenFields(screenType, battleIntroScreen);

                // Try to find scenario-specific text - look for labels that might contain wave info
                string scenarioName = null;
                string scenarioDescription = null;
                string battleMetadata = null;

                // Try to get ScenarioData from BattleIntroScreen
                scenarioName = GetScenarioNameFromScreen(battleIntroScreen, screenType);
                if (!string.IsNullOrEmpty(scenarioName))
                {
                    // Also try to get description and metadata from ScenarioData
                    scenarioDescription = GetScenarioDescriptionFromScreen(battleIntroScreen, screenType);
                    battleMetadata = GetBattleMetadataFromScreen(battleIntroScreen, screenType);
                }

                // If we found a scenario name, use it
                if (!string.IsNullOrEmpty(scenarioName))
                {
                    var sb = new StringBuilder();
                    sb.Append("Fight: ");

                    // Add battle type/metadata first if available
                    if (!string.IsNullOrEmpty(battleMetadata))
                    {
                        sb.Append($"{battleMetadata} - ");
                    }

                    sb.Append(scenarioName);

                    if (!string.IsNullOrEmpty(scenarioDescription))
                    {
                        sb.Append($". {scenarioDescription}");
                    }

                    return sb.ToString();
                }

                // Fallback to battleNameLabel if no scenario-specific name found
                string battleName = null;
                var nameField = screenType.GetField("battleNameLabel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (nameField != null)
                {
                    var nameLabel = nameField.GetValue(battleIntroScreen);
                    if (nameLabel != null)
                    {
                        battleName = GetTextFromComponent(nameLabel);
                    }
                }

                if (!string.IsNullOrEmpty(battleName))
                {
                    return $"Fight: {battleName}";
                }

                // Fallback to enemy names if we couldn't get scenario info
                string enemyNames = GetEnemyNamesFromSiblings(go);
                if (!string.IsNullOrEmpty(enemyNames))
                {
                    return $"Fight: {enemyNames}";
                }

                // Fallback - at least indicate it's a battle
                return "Fight: Start Battle";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle intro text: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get text for RunOpeningScreen (Boss Battles screen shown at start of run)
        /// </summary>
        private string GetRunOpeningScreenText(GameObject go)
        {
            try
            {
                // Look for RunOpeningScreen component in parents
                Component runOpeningScreen = null;
                Transform current = go.transform;

                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;
                        if (typeName == "RunOpeningScreen")
                        {
                            runOpeningScreen = component;
                            break;
                        }
                    }
                    if (runOpeningScreen != null) break;
                    current = current.parent;
                }

                if (runOpeningScreen == null)
                    return null;

                var screenType = runOpeningScreen.GetType();
                MonsterTrainAccessibility.LogInfo($"Found RunOpeningScreen component");

                // Build the boss battles text from bossDetailsUIs
                var sb = new StringBuilder();
                sb.Append("Boss Battles. ");

                // Get bossDetailsUIs field - List<BossDetailsUI>
                var bossDetailsField = screenType.GetField("bossDetailsUIs", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MonsterTrainAccessibility.LogInfo($"bossDetailsField found: {bossDetailsField != null}");
                if (bossDetailsField != null)
                {
                    var bossDetailsList = bossDetailsField.GetValue(runOpeningScreen) as System.Collections.IList;
                    MonsterTrainAccessibility.LogInfo($"bossDetailsList count: {bossDetailsList?.Count ?? 0}");
                    if (bossDetailsList != null && bossDetailsList.Count > 0)
                    {
                        for (int i = 0; i < bossDetailsList.Count; i++)
                        {
                            var bossDetailsUI = bossDetailsList[i];
                            if (bossDetailsUI == null) continue;

                            MonsterTrainAccessibility.LogInfo($"Processing BossDetailsUI[{i}], type: {bossDetailsUI.GetType().Name}");

                            // Log all fields of BossDetailsUI
                            var uiType = bossDetailsUI.GetType();
                            MonsterTrainAccessibility.LogInfo($"BossDetailsUI fields:");
                            foreach (var field in uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                            {
                                try
                                {
                                    var val = field.GetValue(bossDetailsUI);
                                    MonsterTrainAccessibility.LogInfo($"  {field.Name} = {val?.GetType().Name ?? "null"}");
                                }
                                catch { }
                            }

                            string bossInfo = GetBossDetailsUIText(bossDetailsUI);
                            MonsterTrainAccessibility.LogInfo($"BossDetailsUI[{i}] text: '{bossInfo}'");
                            if (!string.IsNullOrEmpty(bossInfo))
                            {
                                sb.Append(bossInfo);
                                if (i < bossDetailsList.Count - 1)
                                    sb.Append(". ");
                            }
                        }

                        string result = sb.ToString().Trim();
                        MonsterTrainAccessibility.LogInfo($"Final boss battles text: '{result}'");
                        if (result.Length > 15) // More than just "Boss Battles. "
                        {
                            // Add button hint
                            sb.Append(". Press Enter to confirm.");
                            return sb.ToString();
                        }
                    }
                }

                // Fallback - try to get text from children
                var screenGo = (runOpeningScreen as MonoBehaviour)?.gameObject;
                if (screenGo != null)
                {
                    var texts = GetAllTextFromChildren(screenGo);
                    if (texts != null && texts.Count > 0)
                    {
                        var meaningfulTexts = texts.Where(t =>
                            !string.IsNullOrWhiteSpace(t) &&
                            t.Length > 2 &&
                            !t.Equals("OK", StringComparison.OrdinalIgnoreCase) &&
                            !t.Equals("Confirm", StringComparison.OrdinalIgnoreCase) &&
                            !t.ToLower().Contains("placeholder")
                        ).ToList();

                        if (meaningfulTexts.Count > 0)
                        {
                            return "Boss Battles. " + string.Join(". ", meaningfulTexts) + ". Press Enter to confirm.";
                        }
                    }
                }

                return "Boss Battles. Press Enter to confirm.";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting run opening screen text: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract text from a BossDetailsUI component
        /// </summary>
        private string GetBossDetailsUIText(object bossDetailsUI)
        {
            if (bossDetailsUI == null) return null;

            try
            {
                var uiType = bossDetailsUI.GetType();
                var sb = new StringBuilder();

                // Get the title (Ring X: or Final Boss:) from titleLabel
                var titleField = uiType.GetField("titleLabel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (titleField != null)
                {
                    var labelObj = titleField.GetValue(bossDetailsUI);
                    if (labelObj != null)
                    {
                        string titleText = GetTMPTextFromObject(labelObj);
                        if (!string.IsNullOrEmpty(titleText) && !titleText.ToLower().Contains("placeholder"))
                        {
                            sb.Append(titleText);
                        }
                    }
                }

                // Get the boss name from tooltipProvider
                var tooltipField = uiType.GetField("tooltipProvider", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (tooltipField != null)
                {
                    var tooltipProvider = tooltipField.GetValue(bossDetailsUI);
                    if (tooltipProvider != null)
                    {
                        string bossName = GetBossNameFromTooltip(tooltipProvider);
                        MonsterTrainAccessibility.LogInfo($"Boss name from tooltip: '{bossName}'");
                        if (!string.IsNullOrEmpty(bossName) && !bossName.ToLower().Contains("placeholder"))
                        {
                            if (sb.Length > 0) sb.Append(" ");
                            sb.Append(bossName);
                        }
                    }
                }

                string result = sb.ToString();
                MonsterTrainAccessibility.LogInfo($"BossDetailsUI final text: '{result}'");
                return !string.IsNullOrEmpty(result) ? result : null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting boss details UI text: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extract boss name from TooltipProviderComponent
        /// </summary>
        private string GetBossNameFromTooltip(object tooltipProvider)
        {
            if (tooltipProvider == null) return null;

            try
            {
                var tooltipType = tooltipProvider.GetType();

                // Log all fields to see what's available
                MonsterTrainAccessibility.LogInfo($"TooltipProvider type: {tooltipType.Name}");
                foreach (var field in tooltipType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    try
                    {
                        var val = field.GetValue(tooltipProvider);
                        string valStr = val?.ToString() ?? "null";
                        if (valStr.Length > 100) valStr = valStr.Substring(0, 100) + "...";
                        MonsterTrainAccessibility.LogInfo($"  Tooltip.{field.Name} = {valStr}");
                    }
                    catch { }
                }

                // Try common tooltip title field names
                string[] titleFieldNames = { "tooltipTitleKey", "_tooltipTitleKey", "titleKey", "title", "tooltipTitle" };
                foreach (var fieldName in titleFieldNames)
                {
                    var field = tooltipType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var val = field.GetValue(tooltipProvider);
                        if (val is string key && !string.IsNullOrEmpty(key))
                        {
                            // Try to localize the key
                            string localized = TryLocalize(key);
                            if (!string.IsNullOrEmpty(localized) && !localized.Contains("_") && !localized.Contains("-"))
                            {
                                return localized;
                            }
                            // If localization fails, return the key if it looks like a name
                            if (!key.Contains("_") && !key.Contains("-"))
                            {
                                return key;
                            }
                        }
                    }
                }

                // Try to get tooltips list/array
                var tooltipsField = tooltipType.GetField("_tooltips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                                   tooltipType.GetField("tooltips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (tooltipsField != null)
                {
                    var tooltips = tooltipsField.GetValue(tooltipProvider);
                    if (tooltips is System.Collections.IList list && list.Count > 0)
                    {
                        var firstTooltip = list[0];
                        if (firstTooltip != null)
                        {
                            // Try to get title from the tooltip data
                            var ttType = firstTooltip.GetType();
                            var ttTitleField = ttType.GetField("title", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                                              ttType.GetField("titleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (ttTitleField != null)
                            {
                                var title = ttTitleField.GetValue(firstTooltip) as string;
                                if (!string.IsNullOrEmpty(title))
                                {
                                    string localized = TryLocalize(title);
                                    return !string.IsNullOrEmpty(localized) ? localized : title;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting boss name from tooltip: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get text from a TMP label object
        /// </summary>
        private string GetTMPTextFromObject(object labelObj)
        {
            if (labelObj == null) return null;

            try
            {
                var labelType = labelObj.GetType();

                // Try text property
                var textProp = labelType.GetProperty("text");
                if (textProp != null)
                {
                    var text = textProp.GetValue(labelObj) as string;
                    if (!string.IsNullOrEmpty(text))
                        return BattleAccessibility.StripRichTextTags(text);
                }

                // Try GetText method
                var getTextMethod = labelType.GetMethod("GetText", Type.EmptyTypes);
                if (getTextMethod != null)
                {
                    var text = getTextMethod.Invoke(labelObj, null) as string;
                    if (!string.IsNullOrEmpty(text))
                        return BattleAccessibility.StripRichTextTags(text);
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Get text for RelicInfoUI (artifact selection on RelicDraftScreen)
        /// </summary>
        private string GetRelicInfoText(GameObject go)
        {
            try
            {
                // Check if this has a RelicInfoUI component
                Component relicInfoUI = null;
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    if (component.GetType().Name == "RelicInfoUI")
                    {
                        relicInfoUI = component;
                        break;
                    }
                }

                if (relicInfoUI == null)
                    return null;

                var relicType = relicInfoUI.GetType();
                MonsterTrainAccessibility.LogInfo($"Found RelicInfoUI, extracting relic data...");

                // Log all fields first to see what's available
                var sbFields = new StringBuilder();
                sbFields.AppendLine($"=== Fields on RelicInfoUI ===");
                foreach (var field in relicType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    try
                    {
                        var value = field.GetValue(relicInfoUI);
                        string valueStr = value?.ToString() ?? "null";
                        if (valueStr.Length > 80) valueStr = valueStr.Substring(0, 80) + "...";
                        sbFields.AppendLine($"  {field.FieldType.Name} {field.Name} = {valueStr}");
                    }
                    catch { }
                }
                MonsterTrainAccessibility.LogInfo(sbFields.ToString());

                // Try to get RelicData from the backing field (C# auto-property)
                string relicName = null;
                string relicDescription = null;

                // Access <relicData>k__BackingField - the backing field for the relicData property
                var backingField = relicType.GetField("<relicData>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField != null)
                {
                    var relicData = backingField.GetValue(relicInfoUI);
                    if (relicData != null)
                    {
                        var dataType = relicData.GetType();
                        MonsterTrainAccessibility.LogInfo($"Found RelicData: {dataType.Name}");

                        // Log available methods to find description
                        var methods = dataType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                        var descMethods = methods.Where(m => m.Name.ToLower().Contains("desc") || m.Name.ToLower().Contains("effect") || m.Name.ToLower().Contains("text")).ToList();
                        MonsterTrainAccessibility.LogInfo($"Potential description methods: {string.Join(", ", descMethods.Select(m => m.Name))}");

                        // Try GetName()
                        var getNameMethod = dataType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                        if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                        {
                            relicName = getNameMethod.Invoke(relicData, null) as string;
                            MonsterTrainAccessibility.LogInfo($"GetName() returned: '{relicName}'");
                        }

                        // Try various description method names
                        string[] descMethodNames = { "GetDescription", "GetEffectText", "GetDescriptionText", "GetRelicEffectText", "GetEffectDescription" };
                        foreach (var methodName in descMethodNames)
                        {
                            var method = dataType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                            if (method != null && method.GetParameters().Length == 0)
                            {
                                relicDescription = method.Invoke(relicData, null) as string;
                                if (!string.IsNullOrEmpty(relicDescription))
                                {
                                    MonsterTrainAccessibility.LogInfo($"{methodName}() returned: '{relicDescription}'");
                                    break;
                                }
                            }
                        }

                        // If still no description, try GetDescriptionKey and localize
                        if (string.IsNullOrEmpty(relicDescription))
                        {
                            var descKeyMethod = dataType.GetMethod("GetDescriptionKey", BindingFlags.Public | BindingFlags.Instance);
                            if (descKeyMethod != null && descKeyMethod.GetParameters().Length == 0)
                            {
                                var key = descKeyMethod.Invoke(relicData, null) as string;
                                if (!string.IsNullOrEmpty(key))
                                {
                                    relicDescription = LocalizeString(key);
                                    MonsterTrainAccessibility.LogInfo($"GetDescriptionKey() -> Localized: '{relicDescription}'");
                                }
                            }
                        }

                        // Resolve effect placeholders like {[effect0.power]} using the relic's effects
                        if (!string.IsNullOrEmpty(relicDescription) && relicDescription.Contains("{[effect"))
                        {
                            relicDescription = ResolveRelicEffectPlaceholders(relicDescription, relicData, dataType);
                        }
                    }
                }

                // If description looks like a localization key, try getting it from RelicState instead
                if (!string.IsNullOrEmpty(relicDescription) && relicDescription.Contains("_descriptionKey"))
                {
                    MonsterTrainAccessibility.LogInfo("Description is a loc key, trying RelicState...");
                    relicDescription = null; // Clear it, will try relicState
                }

                // Try relicState for name and/or description if we don't have them yet
                if (string.IsNullOrEmpty(relicName) || string.IsNullOrEmpty(relicDescription))
                {
                    var relicStateField = relicType.GetField("relicState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (relicStateField != null)
                    {
                        var relicState = relicStateField.GetValue(relicInfoUI);
                        if (relicState != null)
                        {
                            var stateType = relicState.GetType();

                            // Log available methods on RelicState
                            var stateMethods = stateType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                            var descStateMethods = stateMethods.Where(m => m.Name.ToLower().Contains("desc") || m.Name.ToLower().Contains("effect") || m.Name.ToLower().Contains("text") || m.Name.ToLower().Contains("name")).ToList();
                            MonsterTrainAccessibility.LogInfo($"RelicState methods: {string.Join(", ", descStateMethods.Select(m => m.Name))}");

                            // Try to get name from RelicState if we don't have it
                            if (string.IsNullOrEmpty(relicName))
                            {
                                // Try GetName method
                                var getNameMethod = stateType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                                if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                                {
                                    relicName = getNameMethod.Invoke(relicState, null) as string;
                                    MonsterTrainAccessibility.LogInfo($"RelicState.GetName() returned: '{relicName}'");
                                }

                                // Try to get RelicData from RelicState and then get name
                                if (string.IsNullOrEmpty(relicName))
                                {
                                    var getDataMethod = stateType.GetMethod("GetRelicData", BindingFlags.Public | BindingFlags.Instance);
                                    if (getDataMethod == null)
                                        getDataMethod = stateType.GetProperty("RelicData", BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod();

                                    if (getDataMethod != null)
                                    {
                                        var stateRelicData = getDataMethod.Invoke(relicState, null);
                                        if (stateRelicData != null)
                                        {
                                            var dataType = stateRelicData.GetType();
                                            var nameMethod = dataType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                                            if (nameMethod != null && nameMethod.GetParameters().Length == 0)
                                            {
                                                relicName = nameMethod.Invoke(stateRelicData, null) as string;
                                                MonsterTrainAccessibility.LogInfo($"RelicState.RelicData.GetName() returned: '{relicName}'");
                                            }
                                        }
                                    }
                                }
                            }

                            // Try GetDescription on RelicState
                            foreach (var methodName in new[] { "GetDescription", "GetEffectText", "GetDescriptionText" })
                            {
                                var method = stateType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                                if (method != null)
                                {
                                    var parameters = method.GetParameters();
                                    var paramCount = parameters.Length;
                                    MonsterTrainAccessibility.LogInfo($"Found {methodName} with {paramCount} params");

                                    try
                                    {
                                        if (paramCount == 0)
                                        {
                                            relicDescription = method.Invoke(relicState, null) as string;
                                        }
                                        else if (paramCount == 1)
                                        {
                                            // Try calling with null or default value
                                            var paramType = parameters[0].ParameterType;
                                            MonsterTrainAccessibility.LogInfo($"  Param type: {paramType.Name}");

                                            object arg = null;
                                            if (paramType.IsValueType)
                                            {
                                                arg = Activator.CreateInstance(paramType);
                                            }
                                            relicDescription = method.Invoke(relicState, new[] { arg }) as string;
                                        }

                                        MonsterTrainAccessibility.LogInfo($"RelicState.{methodName}() returned: '{relicDescription ?? "null"}'");
                                        if (!string.IsNullOrEmpty(relicDescription) && !relicDescription.Contains("_descriptionKey"))
                                        {
                                            break;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        MonsterTrainAccessibility.LogError($"Error calling {methodName}: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                }

                // Build result
                if (!string.IsNullOrEmpty(relicName))
                {
                    var sb = new StringBuilder();
                    sb.Append("Artifact: ");
                    sb.Append(relicName);

                    if (!string.IsNullOrEmpty(relicDescription) && !relicDescription.Contains("_descriptionKey"))
                    {
                        // Clean up sprite tags like <sprite name=Gold> -> "gold"
                        string cleanDesc = CleanSpriteTagsForSpeech(relicDescription);
                        sb.Append(". ");
                        sb.Append(cleanDesc);

                        // Extract and append keyword explanations
                        var keywords = new List<string>();
                        ExtractKeywordsFromDescription(relicDescription, keywords);
                        if (keywords.Count > 0)
                        {
                            sb.Append(" Keywords: ");
                            sb.Append(string.Join(". ", keywords));
                            sb.Append(".");
                        }
                    }

                    string result = sb.ToString();
                    MonsterTrainAccessibility.LogInfo($"Relic text: {result}");
                    return result;
                }

                MonsterTrainAccessibility.LogInfo("Could not extract relic info");
                return null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting relic info text: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Clean sprite tags like <sprite name=Gold> and convert to readable text like "gold"
        /// Also handles game-specific tags like <gold>, <power>, upgrade highlights, etc.
        /// </summary>
        private string CleanSpriteTagsForSpeech(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Log original text for debugging
            bool hadSpecialTags = text.Contains("sprite") || text.Contains("<gold") || text.Contains("power>") || text.Contains("Highlight>");
            if (hadSpecialTags)
            {
                MonsterTrainAccessibility.LogInfo($"CleanSpriteTagsForSpeech input: '{text}'");
            }

            // Convert sprite tags to readable text
            // Handles: <sprite name=Gold>, <sprite name="Gold">, <sprite name='Gold'>, <sprite="Gold">
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<sprite\s+name\s*=\s*[""']?([^""'>\s]+)[""']?\s*/?>",
                match => " " + match.Groups[1].Value.ToLower() + " ",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Also handle <sprite=X> or <sprite="X"> format
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<sprite\s*=\s*[""']?([^""'>\s]+)[""']?\s*/?>",
                match => " " + match.Groups[1].Value.ToLower() + " ",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Handle <gold>X</gold> -> "X gold"
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<gold>([^<]*)</gold>",
                match => match.Groups[1].Value + " gold",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Handle <+Xpower> or <power>X</power> formats
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<\+?(\d+)power>",
                match => "+" + match.Groups[1].Value + " power",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<power>([^<]*)</power>",
                match => match.Groups[1].Value + " power",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Handle <ember>X</ember> format
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<ember>([^<]*)</ember>",
                match => match.Groups[1].Value + " ember",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Handle <health>X</health> format
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<health>([^<]*)</health>",
                match => match.Groups[1].Value + " health",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Handle <damage>X</damage> or <attack>X</attack> format
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<(?:damage|attack)>([^<]*)</(?:damage|attack)>",
                match => match.Groups[1].Value + " damage",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Handle <capacity>X</capacity> format
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<capacity>([^<]*)</capacity>",
                match => match.Groups[1].Value + " capacity",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Remove upgrade highlight tags but keep their content (card upgrade info)
            // This is important - we want to keep "Armor 10" from "<upgradeHighlight>Armor 10</upgradeHighlight>"
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"</?(?:temp)?[Uu]pgrade[Hh]ighlight>",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Strip any remaining rich text tags (but after extracting useful info)
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "");

            // Clean up double spaces
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

            string result = text.Trim();
            if (hadSpecialTags)
            {
                MonsterTrainAccessibility.LogInfo($"CleanSpriteTagsForSpeech output: '{result}'");
            }
            return result;
        }

        /// <summary>
        /// Check if the current UI element is in a battle intro context (NOT during actual combat)
        /// Only add keyword explanations for battle intro enemy/boss tooltips
        /// </summary>
        private bool IsBattleRelatedContext(GameObject go)
        {
            if (go == null) return false;

            // Don't add keywords during actual battle - only during battle intro
            if (Help.ScreenStateTracker.CurrentScreen == Help.GameScreen.Battle)
                return false;

            // Only trigger for battle intro screen
            if (Help.ScreenStateTracker.CurrentScreen != Help.GameScreen.BattleIntro)
                return false;

            string goName = go.name.ToLower();

            // Check for enemy tooltip targets specifically
            if (goName.Contains("tooltiptarget") || goName.Contains("enemy_tooltip"))
                return true;

            // Check parent hierarchy for enemy tooltip context
            Transform current = go.transform;
            int depth = 0;
            while (current != null && depth < 5)
            {
                string parentName = current.name.ToLower();
                if (parentName.Contains("enemy_tooltip") || parentName.Contains("tooltiptarget"))
                    return true;
                current = current.parent;
                depth++;
            }

            return false;
        }

        /// <summary>
        /// Append keyword explanations to text containing known game keywords
        /// </summary>
        private string AppendKeywordExplanations(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var keywords = new List<string>();
            ExtractKeywordsFromDescription(text, keywords);

            if (keywords.Count > 0)
            {
                return text + " Keywords: " + string.Join(". ", keywords) + ".";
            }

            return text;
        }

        /// <summary>
        /// Resolve placeholders like {[effect0.power]} or {[effect0.status0.power]} in relic descriptions
        /// </summary>
        private string ResolveRelicEffectPlaceholders(string text, object relicData, Type relicType)
        {
            if (string.IsNullOrEmpty(text) || relicData == null) return text;

            MonsterTrainAccessibility.LogInfo($"ResolveRelicEffectPlaceholders called with text: {text}");

            try
            {
                // Get effects from the relic data
                var getEffectsMethod = relicType.GetMethod("GetEffects", Type.EmptyTypes);
                if (getEffectsMethod == null)
                {
                    // Try base types
                    var baseType = relicType.BaseType;
                    while (baseType != null && getEffectsMethod == null)
                    {
                        getEffectsMethod = baseType.GetMethod("GetEffects", Type.EmptyTypes);
                        baseType = baseType.BaseType;
                    }
                }

                MonsterTrainAccessibility.LogInfo($"GetEffects method found: {getEffectsMethod != null}");

                if (getEffectsMethod != null)
                {
                    var effects = getEffectsMethod.Invoke(relicData, null) as System.Collections.IList;
                    MonsterTrainAccessibility.LogInfo($"Effects count: {effects?.Count ?? 0}");
                    if (effects != null && effects.Count > 0)
                    {
                        // Log first effect's fields for debugging
                        var firstEffect = effects[0];
                        if (firstEffect != null)
                        {
                            var effectType = firstEffect.GetType();
                            MonsterTrainAccessibility.LogInfo($"Effect[0] type: {effectType.Name}");
                            foreach (var field in effectType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                            {
                                if (field.Name.ToLower().Contains("param") || field.Name.ToLower().Contains("power"))
                                {
                                    try
                                    {
                                        var val = field.GetValue(firstEffect);
                                        MonsterTrainAccessibility.LogInfo($"  {field.Name} = {val}");
                                    }
                                    catch { }
                                }
                            }
                        }
                        // Match patterns like {[effect0.power]} or {[effect0.status0.power]}
                        var regex = new System.Text.RegularExpressions.Regex(@"\{\[effect(\d+)\.(?:status(\d+)\.)?(\w+)\]\}");
                        text = regex.Replace(text, match =>
                        {
                            int effectIndex = int.Parse(match.Groups[1].Value);
                            string property = match.Groups[3].Value;
                            int statusIndex = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : -1;

                            if (effectIndex < effects.Count)
                            {
                                var effect = effects[effectIndex];
                                if (effect != null)
                                {
                                    var effectType = effect.GetType();

                                    // If status index specified, get from paramStatusEffects array
                                    if (statusIndex >= 0 && property.ToLower() == "power")
                                    {
                                        var statusEffectsField = effectType.GetField("paramStatusEffects",
                                            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                                        if (statusEffectsField != null)
                                        {
                                            var statusEffects = statusEffectsField.GetValue(effect) as Array;
                                            if (statusEffects != null && statusIndex < statusEffects.Length)
                                            {
                                                var statusEffect = statusEffects.GetValue(statusIndex);
                                                if (statusEffect != null)
                                                {
                                                    // StatusEffectStackData has 'count' field for the power
                                                    var countField = statusEffect.GetType().GetField("count",
                                                        BindingFlags.Public | BindingFlags.Instance);
                                                    if (countField != null)
                                                    {
                                                        var count = countField.GetValue(statusEffect);
                                                        return count?.ToString() ?? match.Value;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Map placeholder property names to actual field names
                                        // "power" in placeholders maps to "paramInt" in the effect data
                                        string fieldName;
                                        if (property.ToLower() == "power")
                                        {
                                            fieldName = "paramInt";
                                        }
                                        else
                                        {
                                            fieldName = "param" + char.ToUpper(property[0]) + property.Substring(1);
                                        }

                                        var propField = effectType.GetField(fieldName,
                                            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                                        if (propField != null)
                                        {
                                            var value = propField.GetValue(effect);
                                            MonsterTrainAccessibility.LogInfo($"Resolved {match.Value} -> {value} (field: {fieldName})");
                                            return value?.ToString() ?? match.Value;
                                        }
                                    }
                                }
                            }
                            return match.Value;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"ResolveRelicEffectPlaceholders error: {ex.Message}");
            }

            return text;
        }

        /// <summary>
        /// Get text from a UI text component (TMP_Text, Text, etc.)
        /// </summary>
        private string GetTextFromComponent(object textComponent)
        {
            if (textComponent == null) return null;

            try
            {
                var type = textComponent.GetType();

                // Try 'text' property (common for both TMP_Text and Unity Text)
                var textProp = type.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                if (textProp != null)
                {
                    var text = textProp.GetValue(textComponent) as string;
                    if (!string.IsNullOrEmpty(text))
                        return text.Trim();
                }

                // Try GetParsedText for TMP (returns text without rich text tags)
                var getParsedMethod = type.GetMethod("GetParsedText", BindingFlags.Public | BindingFlags.Instance);
                if (getParsedMethod != null && getParsedMethod.GetParameters().Length == 0)
                {
                    var text = getParsedMethod.Invoke(textComponent, null) as string;
                    if (!string.IsNullOrEmpty(text))
                        return text.Trim();
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting text from component: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get all text strings from child text components of a GameObject
        /// </summary>
        private List<string> GetAllTextFromChildren(GameObject go)
        {
            var texts = new List<string>();

            if (go == null) return texts;

            try
            {
                // Get all components in children
                var components = go.GetComponentsInChildren<Component>(false);

                foreach (var comp in components)
                {
                    if (comp == null || !comp.gameObject.activeInHierarchy) continue;

                    string typeName = comp.GetType().Name;

                    // Look for text components (TMP_Text, Text, TextMeshProUGUI, etc.)
                    if (typeName.Contains("Text"))
                    {
                        string text = GetTextFromComponent(comp);
                        if (!string.IsNullOrEmpty(text) && text.Length > 1)
                        {
                            // Filter out common UI noise
                            string lowerText = text.ToLower();
                            if (!lowerText.Contains("view") &&
                                !lowerText.StartsWith("$") &&
                                !text.All(c => char.IsDigit(c) || c == ' '))
                            {
                                texts.Add(BattleAccessibility.StripRichTextTags(text));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting text from children: {ex.Message}");
            }

            return texts;
        }

        /// <summary>
        /// Debug: Log all text content in a UI hierarchy
        /// </summary>
        private void LogAllTextInHierarchy(Transform root, string prefix)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"=== All text in {prefix} hierarchy ===");
                LogTextRecursive(root, sb, 0);
                MonsterTrainAccessibility.LogInfo(sb.ToString());
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error logging text hierarchy: {ex.Message}");
            }
        }

        private void LogTextRecursive(Transform node, StringBuilder sb, int depth)
        {
            if (node == null || depth > 10) return;

            string indent = new string(' ', depth * 2);

            // Get text from this node
            string text = GetTMPTextDirect(node.gameObject);
            if (string.IsNullOrEmpty(text))
            {
                var uiText = node.GetComponent<Text>();
                text = uiText?.text;
            }

            // Log if there's text
            if (!string.IsNullOrEmpty(text) && text.Trim().Length > 0)
            {
                sb.AppendLine($"{indent}[{node.name}]: \"{text.Trim()}\"");
            }

            // Recurse to children
            foreach (Transform child in node)
            {
                if (child.gameObject.activeInHierarchy)
                {
                    LogTextRecursive(child, sb, depth + 1);
                }
            }
        }

        /// <summary>
        /// Find the scenario/wave name in children of BattleIntroScreen
        /// The battleNameLabel shows the boss name, but we want the wave/scenario name
        /// </summary>
        private string FindScenarioTextInChildren(Transform root)
        {
            try
            {
                // Look for common patterns that might contain the scenario name
                // Often there's a "waveText", "scenarioText", "encounterText" or similar
                string[] namePatternsToFind = { "wave", "scenario", "encounter", "mission", "stage", "title" };
                string[] namePatternsToExclude = { "boss", "champion" };

                // Collect all text labels with their names
                var textLabels = new Dictionary<string, string>();
                CollectTextLabels(root, textLabels);

                // Log what we found
                foreach (var kvp in textLabels)
                {
                    MonsterTrainAccessibility.LogInfo($"Label [{kvp.Key}]: \"{kvp.Value}\"");
                }

                // First, try to find by label name patterns
                foreach (var pattern in namePatternsToFind)
                {
                    foreach (var kvp in textLabels)
                    {
                        string labelName = kvp.Key.ToLower();
                        if (labelName.Contains(pattern))
                        {
                            // Make sure it's not an excluded pattern
                            bool excluded = false;
                            foreach (var excludePattern in namePatternsToExclude)
                            {
                                if (labelName.Contains(excludePattern))
                                {
                                    excluded = true;
                                    break;
                                }
                            }

                            if (!excluded && !string.IsNullOrEmpty(kvp.Value))
                            {
                                MonsterTrainAccessibility.LogInfo($"Found scenario name via pattern '{pattern}': {kvp.Value}");
                                return kvp.Value;
                            }
                        }
                    }
                }

                // If no pattern match, try to find by looking for text that's NOT the boss name
                // The boss name typically appears in "battleNameLabel" or similar
                // Look for another substantial text that might be the wave name
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error finding scenario text: {ex.Message}");
            }

            return null;
        }

        private void CollectTextLabels(Transform node, Dictionary<string, string> labels)
        {
            if (node == null) return;

            // Get text from this node
            string text = GetTMPTextDirect(node.gameObject);
            if (string.IsNullOrEmpty(text))
            {
                var uiText = node.GetComponent<Text>();
                text = uiText?.text;
            }

            if (!string.IsNullOrEmpty(text) && text.Trim().Length > 0)
            {
                labels[node.name] = text.Trim();
            }

            // Recurse to children
            foreach (Transform child in node)
            {
                if (child.gameObject.activeInHierarchy)
                {
                    CollectTextLabels(child, labels);
                }
            }
        }

        /// <summary>
        /// Log all fields on a screen type for debugging
        /// </summary>
        private void LogScreenFields(Type screenType, object screen)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"=== Fields on {screenType.Name} ===");

                var fields = screenType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    try
                    {
                        var value = field.GetValue(screen);
                        string valueStr = value?.ToString() ?? "null";
                        // Truncate long values
                        if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                        sb.AppendLine($"  {field.FieldType.Name} {field.Name} = {valueStr}");
                    }
                    catch { }
                }

                MonsterTrainAccessibility.LogInfo(sb.ToString());
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error logging screen fields: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the scenario name from BattleIntroScreen's ScenarioData or SaveManager
        /// </summary>
        private string GetScenarioNameFromScreen(object screen, Type screenType)
        {
            try
            {
                // First try to get SaveManager and get scenario from there
                var saveManagerField = screenType.GetField("saveManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (saveManagerField != null)
                {
                    var saveManager = saveManagerField.GetValue(screen);
                    if (saveManager != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found SaveManager of type {saveManager.GetType().Name}");
                        string name = GetScenarioNameFromSaveManager(saveManager);
                        if (!string.IsNullOrEmpty(name))
                            return name;
                    }
                }

                // Look for scenario-related fields
                string[] scenarioFieldNames = { "scenarioData", "_scenarioData", "scenario", "_scenario",
                    "currentScenario", "_currentScenario", "battleData", "_battleData" };

                foreach (var fieldName in scenarioFieldNames)
                {
                    var field = screenType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var scenarioData = field.GetValue(screen);
                        if (scenarioData != null)
                        {
                            MonsterTrainAccessibility.LogInfo($"Found scenario field: {fieldName} of type {scenarioData.GetType().Name}");
                            string name = GetBattleNameFromScenario(scenarioData);
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }

                // Also check properties
                string[] scenarioPropNames = { "ScenarioData", "Scenario", "CurrentScenario", "BattleData" };
                foreach (var propName in scenarioPropNames)
                {
                    var prop = screenType.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null)
                    {
                        var scenarioData = prop.GetValue(screen);
                        if (scenarioData != null)
                        {
                            MonsterTrainAccessibility.LogInfo($"Found scenario property: {propName} of type {scenarioData.GetType().Name}");
                            string name = GetBattleNameFromScenario(scenarioData);
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scenario name from screen: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the scenario name from SaveManager's run state
        /// </summary>
        private string GetScenarioNameFromSaveManager(object saveManager)
        {
            try
            {
                var saveManagerType = saveManager.GetType();

                // Log SaveManager fields/methods for debugging
                var methods = saveManagerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                var scenarioMethods = methods.Where(m => m.Name.Contains("Scenario") || m.Name.Contains("Battle") || m.Name.Contains("Wave")).ToList();
                MonsterTrainAccessibility.LogInfo($"SaveManager scenario-related methods: {string.Join(", ", scenarioMethods.Select(m => m.Name))}");

                // Try GetCurrentScenarioData method
                var getCurrentScenarioMethod = saveManagerType.GetMethod("GetCurrentScenarioData", BindingFlags.Public | BindingFlags.Instance);
                if (getCurrentScenarioMethod != null && getCurrentScenarioMethod.GetParameters().Length == 0)
                {
                    var scenarioData = getCurrentScenarioMethod.Invoke(saveManager, null);
                    if (scenarioData != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Got ScenarioData from GetCurrentScenarioData(): {scenarioData.GetType().Name}");
                        string name = GetBattleNameFromScenario(scenarioData);
                        if (!string.IsNullOrEmpty(name))
                            return name;
                    }
                }

                // Try GetScenario method
                var getScenarioMethod = saveManagerType.GetMethod("GetScenario", BindingFlags.Public | BindingFlags.Instance);
                if (getScenarioMethod != null && getScenarioMethod.GetParameters().Length == 0)
                {
                    var scenarioData = getScenarioMethod.Invoke(saveManager, null);
                    if (scenarioData != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Got ScenarioData from GetScenario(): {scenarioData.GetType().Name}");
                        string name = GetBattleNameFromScenario(scenarioData);
                        if (!string.IsNullOrEmpty(name))
                            return name;
                    }
                }

                // Try to access run state
                string[] runStateFields = { "runState", "_runState", "currentRun", "_currentRun", "activeRun" };
                foreach (var fieldName in runStateFields)
                {
                    var field = saveManagerType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var runState = field.GetValue(saveManager);
                        if (runState != null)
                        {
                            MonsterTrainAccessibility.LogInfo($"Found run state: {fieldName} of type {runState.GetType().Name}");
                            string name = GetScenarioNameFromRunState(runState);
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }

                // Try GetBalanceData for current scenario info
                var getBalanceDataMethod = saveManagerType.GetMethod("GetBalanceData", BindingFlags.Public | BindingFlags.Instance);
                if (getBalanceDataMethod != null && getBalanceDataMethod.GetParameters().Length == 0)
                {
                    var balanceData = getBalanceDataMethod.Invoke(saveManager, null);
                    if (balanceData != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Got BalanceData: {balanceData.GetType().Name}");
                        // BalanceData might have scenario info
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scenario from SaveManager: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get scenario name from run state object
        /// </summary>
        private string GetScenarioNameFromRunState(object runState)
        {
            try
            {
                var runStateType = runState.GetType();
                MonsterTrainAccessibility.LogInfo($"RunState type: {runStateType.Name}");

                // Log fields for debugging
                var fields = runStateType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var scenarioFields = fields.Where(f => f.Name.ToLower().Contains("scenario") || f.Name.ToLower().Contains("battle") || f.Name.ToLower().Contains("wave")).ToList();
                MonsterTrainAccessibility.LogInfo($"RunState scenario-related fields: {string.Join(", ", scenarioFields.Select(f => f.Name))}");

                // Try to get current scenario
                string[] scenarioFieldNames = { "currentScenario", "_currentScenario", "scenario", "_scenario", "battleScenario" };
                foreach (var fieldName in scenarioFieldNames)
                {
                    var field = runStateType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var scenarioData = field.GetValue(runState);
                        if (scenarioData != null)
                        {
                            MonsterTrainAccessibility.LogInfo($"Found scenario in run state: {fieldName}");
                            string name = GetBattleNameFromScenario(scenarioData);
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }

                // Try GetScenario method on run state
                var getScenarioMethod = runStateType.GetMethod("GetScenario", BindingFlags.Public | BindingFlags.Instance);
                if (getScenarioMethod != null && getScenarioMethod.GetParameters().Length == 0)
                {
                    var scenarioData = getScenarioMethod.Invoke(runState, null);
                    if (scenarioData != null)
                    {
                        string name = GetBattleNameFromScenario(scenarioData);
                        if (!string.IsNullOrEmpty(name))
                            return name;
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scenario from run state: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the battle name from a ScenarioData object
        /// </summary>
        private string GetBattleNameFromScenario(object scenarioData)
        {
            try
            {
                var dataType = scenarioData.GetType();
                MonsterTrainAccessibility.LogInfo($"ScenarioData type: {dataType.Name}");

                // Log fields for debugging
                var fields = dataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MonsterTrainAccessibility.LogInfo($"ScenarioData fields: {string.Join(", ", fields.Select(f => f.Name))}");

                // Try GetBattleName method first
                var getBattleNameMethod = dataType.GetMethod("GetBattleName", BindingFlags.Public | BindingFlags.Instance);
                if (getBattleNameMethod != null && getBattleNameMethod.GetParameters().Length == 0)
                {
                    var result = getBattleNameMethod.Invoke(scenarioData, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                    {
                        MonsterTrainAccessibility.LogInfo($"Got battle name via GetBattleName(): {name}");
                        return name;
                    }
                }

                // Try GetName method
                var getNameMethod = dataType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                {
                    var result = getNameMethod.Invoke(scenarioData, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                    {
                        MonsterTrainAccessibility.LogInfo($"Got battle name via GetName(): {name}");
                        return name;
                    }
                }

                // Try battleNameKey field
                string[] nameFieldNames = { "battleNameKey", "_battleNameKey", "nameKey", "_nameKey" };
                foreach (var fieldName in nameFieldNames)
                {
                    var field = dataType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var key = field.GetValue(scenarioData) as string;
                        if (!string.IsNullOrEmpty(key))
                        {
                            string localized = LocalizeKey(key);
                            if (!string.IsNullOrEmpty(localized))
                            {
                                MonsterTrainAccessibility.LogInfo($"Got battle name from {fieldName}: {localized}");
                                return localized;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle name from scenario: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the scenario description from BattleIntroScreen's ScenarioData or SaveManager
        /// </summary>
        private string GetScenarioDescriptionFromScreen(object screen, Type screenType)
        {
            try
            {
                // First try to get SaveManager and get scenario description from there
                var saveManagerField = screenType.GetField("saveManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (saveManagerField != null)
                {
                    var saveManager = saveManagerField.GetValue(screen);
                    if (saveManager != null)
                    {
                        string desc = GetScenarioDescriptionFromSaveManager(saveManager);
                        if (!string.IsNullOrEmpty(desc))
                            return desc;
                    }
                }

                // Look for scenario-related fields
                string[] scenarioFieldNames = { "scenarioData", "_scenarioData", "scenario", "_scenario" };

                foreach (var fieldName in scenarioFieldNames)
                {
                    var field = screenType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var scenarioData = field.GetValue(screen);
                        if (scenarioData != null)
                        {
                            return GetBattleDescriptionFromScenario(scenarioData);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scenario description: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get battle metadata (type, difficulty, ring, etc.) from screen
        /// </summary>
        private string GetBattleMetadataFromScreen(object screen, Type screenType)
        {
            try
            {
                var saveManagerField = screenType.GetField("saveManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (saveManagerField == null) return null;

                var saveManager = saveManagerField.GetValue(screen);
                if (saveManager == null) return null;

                var parts = new List<string>();

                // Get current ring/floor
                string ringInfo = GetCurrentRingInfo(saveManager);
                if (!string.IsNullOrEmpty(ringInfo))
                {
                    parts.Add(ringInfo);
                }

                // Get battle type (boss, elite, normal)
                string battleType = GetBattleType(saveManager, screen, screenType);
                if (!string.IsNullOrEmpty(battleType))
                {
                    parts.Add(battleType);
                }

                // Get difficulty info from scenario
                string difficultyInfo = GetScenarioDifficulty(saveManager);
                if (!string.IsNullOrEmpty(difficultyInfo))
                {
                    parts.Add(difficultyInfo);
                }

                if (parts.Count > 0)
                {
                    return string.Join(", ", parts);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle metadata: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get current ring/floor number
        /// </summary>
        private string GetCurrentRingInfo(object saveManager)
        {
            try
            {
                var saveManagerType = saveManager.GetType();

                // Try GetCurrentRing, GetRing, GetFloor, etc.
                string[] ringMethods = { "GetCurrentRing", "GetRing", "GetCurrentFloor", "GetFloor", "GetCurrentLevel" };
                foreach (var methodName in ringMethods)
                {
                    var method = saveManagerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                    if (method != null && method.GetParameters().Length == 0)
                    {
                        var result = method.Invoke(saveManager, null);
                        if (result != null)
                        {
                            int ring = Convert.ToInt32(result);
                            // Monster Train has rings 1-8 typically
                            if (ring >= 0 && ring <= 10)
                            {
                                MonsterTrainAccessibility.LogInfo($"Got ring from {methodName}: {ring}");
                                return $"Ring {ring + 1}"; // Convert 0-based to 1-based
                            }
                        }
                    }
                }

                // Try fields
                string[] ringFields = { "currentRing", "_currentRing", "ring", "_ring", "currentFloor", "_currentFloor" };
                foreach (var fieldName in ringFields)
                {
                    var field = saveManagerType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var value = field.GetValue(saveManager);
                        if (value != null)
                        {
                            int ring = Convert.ToInt32(value);
                            if (ring >= 0 && ring <= 10)
                            {
                                MonsterTrainAccessibility.LogInfo($"Got ring from field {fieldName}: {ring}");
                                return $"Ring {ring + 1}";
                            }
                        }
                    }
                }

                // Try to get from RunState
                var runStateField = saveManagerType.GetField("runState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (runStateField != null)
                {
                    var runState = runStateField.GetValue(saveManager);
                    if (runState != null)
                    {
                        var runStateType = runState.GetType();
                        foreach (var methodName in ringMethods)
                        {
                            var method = runStateType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                            if (method != null && method.GetParameters().Length == 0)
                            {
                                var result = method.Invoke(runState, null);
                                if (result != null)
                                {
                                    int ring = Convert.ToInt32(result);
                                    if (ring >= 0 && ring <= 10)
                                    {
                                        return $"Ring {ring + 1}";
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting ring info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Determine battle type (Boss, Elite, Normal, etc.)
        /// </summary>
        private string GetBattleType(object saveManager, object screen, Type screenType)
        {
            try
            {
                // Check if this is a boss battle by looking at bigBossDisplay visibility
                // The bigBossDisplay is only visible/active during actual boss fights
                var bigBossField = screenType.GetField("bigBossDisplay", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (bigBossField != null)
                {
                    var bigBoss = bigBossField.GetValue(screen);
                    if (bigBoss != null && bigBoss is Component comp)
                    {
                        // Check if the boss display GameObject is active
                        if (comp.gameObject.activeInHierarchy)
                        {
                            MonsterTrainAccessibility.LogInfo("Boss display is active - this is a boss battle");
                            return "Boss Battle";
                        }
                        else
                        {
                            MonsterTrainAccessibility.LogInfo("Boss display exists but is not active - not a boss battle");
                        }
                    }
                }

                // Check ScenarioData for more info
                var saveManagerType = saveManager.GetType();
                var getCurrentScenarioMethod = saveManagerType.GetMethod("GetCurrentScenarioData", BindingFlags.Public | BindingFlags.Instance);
                if (getCurrentScenarioMethod != null && getCurrentScenarioMethod.GetParameters().Length == 0)
                {
                    var scenarioData = getCurrentScenarioMethod.Invoke(saveManager, null);
                    if (scenarioData != null)
                    {
                        var scenarioType = scenarioData.GetType();

                        // Check if there's a GetIsBoss or IsBossBattle method
                        var isBossMethod = scenarioType.GetMethod("GetIsBoss", BindingFlags.Public | BindingFlags.Instance) ??
                                          scenarioType.GetMethod("IsBossBattle", BindingFlags.Public | BindingFlags.Instance);
                        if (isBossMethod != null && isBossMethod.GetParameters().Length == 0)
                        {
                            var isBoss = isBossMethod.Invoke(scenarioData, null);
                            if (isBoss is bool b && b)
                            {
                                return "Boss Battle";
                            }
                        }

                        // Check difficulty field - higher values might indicate harder battles
                        var difficultyField = scenarioType.GetField("difficulty", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (difficultyField != null)
                        {
                            var difficulty = difficultyField.GetValue(scenarioData);
                            if (difficulty != null)
                            {
                                int diffValue = Convert.ToInt32(difficulty);
                                MonsterTrainAccessibility.LogInfo($"Scenario difficulty: {diffValue}");
                                // Could indicate elite/hard battle at certain thresholds
                            }
                        }

                        // Log bossVariant for reference (but don't use it to determine boss status)
                        var bossVariantField = scenarioType.GetField("bossVariant", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (bossVariantField != null)
                        {
                            var bossVariant = bossVariantField.GetValue(scenarioData);
                            if (bossVariant != null)
                            {
                                MonsterTrainAccessibility.LogInfo($"Ring boss pool: {bossVariant} (not current battle type)");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle type: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get difficulty info from current scenario
        /// </summary>
        private string GetScenarioDifficulty(object saveManager)
        {
            try
            {
                var saveManagerType = saveManager.GetType();

                // Try to get covenant level (difficulty modifier)
                var getCovenantMethod = saveManagerType.GetMethod("GetCovenantLevel", BindingFlags.Public | BindingFlags.Instance);
                if (getCovenantMethod == null)
                {
                    getCovenantMethod = saveManagerType.GetMethod("GetAscensionLevel", BindingFlags.Public | BindingFlags.Instance);
                }
                if (getCovenantMethod != null && getCovenantMethod.GetParameters().Length == 0)
                {
                    var covenant = getCovenantMethod.Invoke(saveManager, null);
                    if (covenant != null)
                    {
                        int level = Convert.ToInt32(covenant);
                        if (level > 0)
                        {
                            MonsterTrainAccessibility.LogInfo($"Covenant level: {level}");
                            return $"Covenant {level}";
                        }
                    }
                }

                // Check fields
                string[] covenantFields = { "covenantLevel", "_covenantLevel", "ascensionLevel", "_ascensionLevel" };
                foreach (var fieldName in covenantFields)
                {
                    var field = saveManagerType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var value = field.GetValue(saveManager);
                        if (value != null)
                        {
                            int level = Convert.ToInt32(value);
                            if (level > 0)
                            {
                                return $"Covenant {level}";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scenario difficulty: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the scenario description from SaveManager's current scenario
        /// </summary>
        private string GetScenarioDescriptionFromSaveManager(object saveManager)
        {
            try
            {
                var saveManagerType = saveManager.GetType();

                // Try GetCurrentScenarioData method
                var getCurrentScenarioMethod = saveManagerType.GetMethod("GetCurrentScenarioData", BindingFlags.Public | BindingFlags.Instance);
                if (getCurrentScenarioMethod != null && getCurrentScenarioMethod.GetParameters().Length == 0)
                {
                    var scenarioData = getCurrentScenarioMethod.Invoke(saveManager, null);
                    if (scenarioData != null)
                    {
                        string desc = GetBattleDescriptionFromScenario(scenarioData);
                        if (!string.IsNullOrEmpty(desc))
                        {
                            MonsterTrainAccessibility.LogInfo($"Got battle description: {desc}");
                            return desc;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting scenario description from SaveManager: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the battle description from a ScenarioData object
        /// </summary>
        private string GetBattleDescriptionFromScenario(object scenarioData)
        {
            try
            {
                var dataType = scenarioData.GetType();

                // Try GetBattleDescription method
                var getDescMethod = dataType.GetMethod("GetBattleDescription", BindingFlags.Public | BindingFlags.Instance);
                if (getDescMethod != null && getDescMethod.GetParameters().Length == 0)
                {
                    var result = getDescMethod.Invoke(scenarioData, null);
                    if (result is string desc && !string.IsNullOrEmpty(desc))
                    {
                        return desc;
                    }
                }

                // Try battleDescriptionKey field
                string[] descFieldNames = { "battleDescriptionKey", "_battleDescriptionKey", "descriptionKey", "_descriptionKey" };
                foreach (var fieldName in descFieldNames)
                {
                    var field = dataType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var key = field.GetValue(scenarioData) as string;
                        if (!string.IsNullOrEmpty(key))
                        {
                            string localized = LocalizeKey(key);
                            if (!string.IsNullOrEmpty(localized))
                            {
                                return localized;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle description: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract battle name and description from ScenarioData
        /// </summary>
        private string GetBattleNameAndDescription(object scenarioData)
        {
            if (scenarioData == null) return null;

            try
            {
                var dataType = scenarioData.GetType();
                string battleName = null;
                string battleDescription = null;

                // Debug: log fields
                var fields = dataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MonsterTrainAccessibility.LogInfo($"Scenario fields: {string.Join(", ", fields.Select(f => f.Name))}");

                // Try to get battle name
                // Method: GetBattleName()
                var getBattleNameMethod = dataType.GetMethod("GetBattleName", BindingFlags.Public | BindingFlags.Instance);
                if (getBattleNameMethod != null && getBattleNameMethod.GetParameters().Length == 0)
                {
                    var result = getBattleNameMethod.Invoke(scenarioData, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                    {
                        battleName = name;
                        MonsterTrainAccessibility.LogInfo($"Got battle name from GetBattleName(): {battleName}");
                    }
                }

                // Try field: battleNameKey
                if (string.IsNullOrEmpty(battleName))
                {
                    string[] nameFieldNames = { "battleNameKey", "_battleNameKey", "nameKey", "_nameKey", "titleKey", "_titleKey" };
                    foreach (var fieldName in nameFieldNames)
                    {
                        var field = dataType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            var key = field.GetValue(scenarioData) as string;
                            if (!string.IsNullOrEmpty(key))
                            {
                                string localized = LocalizeKey(key);
                                if (!string.IsNullOrEmpty(localized))
                                {
                                    battleName = localized;
                                    MonsterTrainAccessibility.LogInfo($"Got battle name from {fieldName}: {battleName}");
                                    break;
                                }
                            }
                        }
                    }
                }

                // Try to get battle description
                // Method: GetBattleDescription()
                var getBattleDescMethod = dataType.GetMethod("GetBattleDescription", BindingFlags.Public | BindingFlags.Instance);
                if (getBattleDescMethod != null && getBattleDescMethod.GetParameters().Length == 0)
                {
                    var result = getBattleDescMethod.Invoke(scenarioData, null);
                    if (result is string desc && !string.IsNullOrEmpty(desc))
                    {
                        battleDescription = desc;
                        MonsterTrainAccessibility.LogInfo($"Got battle description from GetBattleDescription(): {battleDescription}");
                    }
                }

                // Try field: battleDescriptionKey
                if (string.IsNullOrEmpty(battleDescription))
                {
                    string[] descFieldNames = { "battleDescriptionKey", "_battleDescriptionKey", "descriptionKey", "_descriptionKey" };
                    foreach (var fieldName in descFieldNames)
                    {
                        var field = dataType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            var key = field.GetValue(scenarioData) as string;
                            if (!string.IsNullOrEmpty(key))
                            {
                                string localized = LocalizeKey(key);
                                if (!string.IsNullOrEmpty(localized))
                                {
                                    battleDescription = localized;
                                    MonsterTrainAccessibility.LogInfo($"Got battle description from {fieldName}: {battleDescription}");
                                    break;
                                }
                            }
                        }
                    }
                }

                // Build the result
                if (!string.IsNullOrEmpty(battleName))
                {
                    if (!string.IsNullOrEmpty(battleDescription))
                    {
                        return $"Fight: {battleName}. {battleDescription}";
                    }
                    return $"Fight: {battleName}";
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle name/description: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Look for TooltipTarget siblings to get enemy names for the Fight button
        /// </summary>
        private string GetEnemyNamesFromSiblings(GameObject go)
        {
            try
            {
                // Navigate up to find Enemy_Tooltips or similar container
                Transform searchRoot = go.transform.parent;
                while (searchRoot != null)
                {
                    // Look for a container that might have TooltipTargets
                    var tooltipContainer = FindChildByNameContains(searchRoot, "Tooltip");
                    if (tooltipContainer != null)
                    {
                        var enemyNames = new List<string>();

                        // Get all TooltipTarget children
                        foreach (Transform child in tooltipContainer)
                        {
                            if (!child.gameObject.activeInHierarchy) continue;
                            if (!child.name.Contains("TooltipTarget")) continue;

                            // Get the tooltip provider and extract the name
                            foreach (var component in child.GetComponents<Component>())
                            {
                                if (component == null) continue;
                                if (component.GetType().Name == "TooltipProviderComponent")
                                {
                                    string name = GetTooltipProviderTitle(component, component.GetType());
                                    if (!string.IsNullOrEmpty(name))
                                    {
                                        enemyNames.Add(name);
                                    }
                                    break;
                                }
                            }
                        }

                        if (enemyNames.Count > 0)
                        {
                            return string.Join(", ", enemyNames);
                        }
                    }

                    // Also check siblings at this level
                    if (searchRoot.parent != null)
                    {
                        foreach (Transform sibling in searchRoot.parent)
                        {
                            if (sibling.name.Contains("Tooltip") || sibling.name.Contains("Enemy"))
                            {
                                var names = GetTooltipNamesFromContainer(sibling);
                                if (names.Count > 0)
                                {
                                    return string.Join(", ", names);
                                }
                            }
                        }
                    }

                    searchRoot = searchRoot.parent;

                    // Don't go too far up
                    if (searchRoot != null && searchRoot.name.Contains("BattleIntro"))
                        break;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting enemy names from siblings: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Find a child transform by partial name match
        /// </summary>
        private Transform FindChildByNameContains(Transform parent, string partialName)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Contains(partialName))
                    return child;

                var found = FindChildByNameContains(child, partialName);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Get all tooltip names from a container
        /// </summary>
        private List<string> GetTooltipNamesFromContainer(Transform container)
        {
            var names = new List<string>();

            foreach (Transform child in container)
            {
                if (!child.gameObject.activeInHierarchy) continue;

                foreach (var component in child.GetComponents<Component>())
                {
                    if (component == null) continue;
                    if (component.GetType().Name == "TooltipProviderComponent")
                    {
                        string name = GetTooltipProviderTitle(component, component.GetType());
                        if (!string.IsNullOrEmpty(name))
                        {
                            names.Add(name);
                        }
                        break;
                    }
                }
            }

            return names;
        }

        /// <summary>
        /// Debug: Log all components on a GameObject and its parents
        /// </summary>
        private void LogMapNodeComponents(GameObject go)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Components on '{go.name}':");

                Transform current = go.transform;
                int depth = 0;
                while (current != null && depth < 5)
                {
                    sb.Append($"  [{depth}] {current.name}: ");
                    var components = current.GetComponents<Component>();
                    foreach (var comp in components)
                    {
                        if (comp != null)
                        {
                            sb.Append(comp.GetType().Name + ", ");
                        }
                    }
                    sb.AppendLine();
                    current = current.parent;
                    depth++;
                }

                MonsterTrainAccessibility.LogInfo(sb.ToString());
            }
            catch { }
        }

        /// <summary>
        /// Log all fields on a MapNodeUI component for debugging
        /// </summary>
        private void LogMapNodeUIFields(Component mapNodeComponent)
        {
            try
            {
                var type = mapNodeComponent.GetType();
                var sb = new StringBuilder();
                sb.AppendLine($"=== Fields on {type.Name} ===");

                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    try
                    {
                        var value = field.GetValue(mapNodeComponent);
                        string valueStr = value?.ToString() ?? "null";
                        if (valueStr.Length > 80) valueStr = valueStr.Substring(0, 80) + "...";
                        sb.AppendLine($"  {field.FieldType.Name} {field.Name} = {valueStr}");
                    }
                    catch { }
                }

                MonsterTrainAccessibility.LogInfo(sb.ToString());
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error logging MapNodeUI fields: {ex.Message}");
            }
        }

        /// <summary>
        /// Try to get tooltip text from a GameObject's tooltip components
        /// </summary>
        private string GetTooltipText(GameObject go)
        {
            try
            {
                Transform current = go.transform;
                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        var type = component.GetType();
                        string typeName = type.Name;

                        // Look for TooltipProviderComponent specifically (Monster Train's tooltip system)
                        if (typeName == "TooltipProviderComponent" || typeName.Contains("TooltipProvider"))
                        {
                            string tooltipTitle = GetTooltipProviderTitle(component, type);
                            if (!string.IsNullOrEmpty(tooltipTitle))
                            {
                                MonsterTrainAccessibility.LogInfo($"Found tooltip title: {tooltipTitle}");
                                return tooltipTitle;
                            }
                        }

                        // Look for other tooltip-related components
                        if (typeName.Contains("Tooltip") || typeName.Contains("TooltipDisplay"))
                        {
                            // Try to get tooltip title/text
                            var titleField = type.GetField("titleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (titleField == null)
                                titleField = type.GetField("_titleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (titleField == null)
                                titleField = type.GetField("tooltipTitleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                            if (titleField != null)
                            {
                                string titleKey = titleField.GetValue(component) as string;
                                if (!string.IsNullOrEmpty(titleKey))
                                {
                                    string localized = LocalizeKey(titleKey);
                                    if (!string.IsNullOrEmpty(localized))
                                        return localized;
                                }
                            }

                            // Try GetTitle method
                            var getTitleMethod = type.GetMethod("GetTitle", BindingFlags.Public | BindingFlags.Instance);
                            if (getTitleMethod != null && getTitleMethod.GetParameters().Length == 0)
                            {
                                var result = getTitleMethod.Invoke(component, null);
                                if (result is string title && !string.IsNullOrEmpty(title))
                                    return title;
                            }
                        }

                        // Look for scenario/battle data reference
                        if (typeName.Contains("Scenario") || typeName.Contains("Battle"))
                        {
                            // Try GetName method
                            var getNameMethod = type.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                            if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                            {
                                var result = getNameMethod.Invoke(component, null);
                                if (result is string name && !string.IsNullOrEmpty(name))
                                    return "Battle: " + name;
                            }
                        }
                    }
                    current = current.parent;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting tooltip text: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract title from TooltipProviderComponent or LocalizedTooltipProvider
        /// </summary>
        private string GetTooltipProviderTitle(Component tooltipProvider, Type type)
        {
            try
            {
                string typeName = type.Name;

                // Handle LocalizedTooltipProvider specifically
                if (typeName == "LocalizedTooltipProvider")
                {
                    // Try to get titleKey field
                    var titleKeyField = type.GetField("titleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                     ?? type.GetField("_titleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                     ?? type.GetField("tooltipTitleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (titleKeyField != null)
                    {
                        string titleKey = titleKeyField.GetValue(tooltipProvider) as string;
                        if (!string.IsNullOrEmpty(titleKey))
                        {
                            string localized = LocalizeString(titleKey);
                            if (!string.IsNullOrEmpty(localized))
                            {
                                MonsterTrainAccessibility.LogInfo($"LocalizedTooltipProvider title: {localized}");
                                return localized;
                            }
                        }
                    }

                    // Log fields for debugging
                    var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    MonsterTrainAccessibility.LogInfo($"LocalizedTooltipProvider fields: {string.Join(", ", fields.Select(f => f.Name))}");
                }

                // The TooltipProviderComponent has a _tooltips field which is a list of tooltip data
                var tooltipsField = type.GetField("_tooltips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (tooltipsField == null)
                {
                    // Try the property
                    var tooltipsProp = type.GetProperty("tooltips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (tooltipsProp != null)
                    {
                        var tooltipsList = tooltipsProp.GetValue(tooltipProvider) as System.Collections.IList;
                        if (tooltipsList != null && tooltipsList.Count > 0)
                        {
                            return ExtractTitleFromTooltip(tooltipsList[0]);
                        }
                    }
                }
                else
                {
                    var tooltipsList = tooltipsField.GetValue(tooltipProvider) as System.Collections.IList;
                    if (tooltipsList != null && tooltipsList.Count > 0)
                    {
                        return ExtractTitleFromTooltip(tooltipsList[0]);
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting tooltip provider title: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract the title from a tooltip data object
        /// </summary>
        private string ExtractTitleFromTooltip(object tooltip)
        {
            if (tooltip == null) return null;

            try
            {
                var tooltipType = tooltip.GetType();
                MonsterTrainAccessibility.LogInfo($"Tooltip type: {tooltipType.Name}");

                // Log fields for debugging
                var fields = tooltipType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MonsterTrainAccessibility.LogInfo($"Tooltip fields: {string.Join(", ", fields.Select(f => f.Name))}");

                // Try GetTitle method first
                var getTitleMethod = tooltipType.GetMethod("GetTitle", BindingFlags.Public | BindingFlags.Instance);
                if (getTitleMethod != null && getTitleMethod.GetParameters().Length == 0)
                {
                    var result = getTitleMethod.Invoke(tooltip, null);
                    if (result is string title && !string.IsNullOrEmpty(title))
                    {
                        MonsterTrainAccessibility.LogInfo($"Got title from GetTitle(): {title}");
                        return title;
                    }
                }

                // Try common title field names
                string[] titleFieldNames = { "title", "_title", "titleKey", "_titleKey", "tooltipTitleKey", "_tooltipTitleKey", "name", "_name" };
                foreach (var fieldName in titleFieldNames)
                {
                    var field = tooltipType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var value = field.GetValue(tooltip);
                        if (value is string str && !string.IsNullOrEmpty(str))
                        {
                            MonsterTrainAccessibility.LogInfo($"Found title in field {fieldName}: {str}");
                            // Try to localize if it looks like a key
                            string localized = LocalizeKey(str);
                            if (!string.IsNullOrEmpty(localized) && localized != str)
                            {
                                MonsterTrainAccessibility.LogInfo($"Localized to: {localized}");
                                return localized;
                            }
                            return str;
                        }
                    }
                }

                // Try title properties
                string[] titlePropNames = { "Title", "TitleKey", "Name" };
                foreach (var propName in titlePropNames)
                {
                    var prop = tooltipType.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null)
                    {
                        var value = prop.GetValue(tooltip);
                        if (value is string str && !string.IsNullOrEmpty(str))
                        {
                            MonsterTrainAccessibility.LogInfo($"Found title in property {propName}: {str}");
                            string localized = LocalizeKey(str);
                            if (!string.IsNullOrEmpty(localized) && localized != str)
                                return localized;
                            return str;
                        }
                    }
                }

                // Check if it has a nested data object (like CharacterData, ScenarioData)
                string[] dataFieldNames = { "data", "_data", "characterData", "_characterData", "scenarioData", "_scenarioData" };
                foreach (var fieldName in dataFieldNames)
                {
                    var field = tooltipType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var dataObj = field.GetValue(tooltip);
                        if (dataObj != null)
                        {
                            string name = GetNameFromDataObject(dataObj);
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting title from tooltip: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get name from a game data object (CharacterData, ScenarioData, etc.)
        /// </summary>
        private string GetNameFromDataObject(object dataObj)
        {
            if (dataObj == null) return null;

            try
            {
                var dataType = dataObj.GetType();
                MonsterTrainAccessibility.LogInfo($"Data object type: {dataType.Name}");

                // Try GetName method
                var getNameMethod = dataType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                {
                    var result = getNameMethod.Invoke(dataObj, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                        return name;
                }

                // Try GetNameKey for localized names
                var getNameKeyMethod = dataType.GetMethod("GetNameKey", BindingFlags.Public | BindingFlags.Instance);
                if (getNameKeyMethod != null && getNameKeyMethod.GetParameters().Length == 0)
                {
                    var result = getNameKeyMethod.Invoke(dataObj, null);
                    if (result is string key && !string.IsNullOrEmpty(key))
                    {
                        string localized = LocalizeKey(key);
                        return !string.IsNullOrEmpty(localized) ? localized : key;
                    }
                }

                // Try name fields
                string[] nameFields = { "name", "_name", "nameKey", "_nameKey" };
                foreach (var fieldName in nameFields)
                {
                    var field = dataType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var value = field.GetValue(dataObj);
                        if (value is string str && !string.IsNullOrEmpty(str))
                        {
                            string localized = LocalizeKey(str);
                            return !string.IsNullOrEmpty(localized) ? localized : str;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting name from data object: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the name of a battle node from ScenarioData
        /// </summary>
        private string GetBattleNodeName(object scenarioData, Type dataType)
        {
            try
            {
                // Try battleNameKey field
                var battleNameField = dataType.GetField("battleNameKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (battleNameField == null)
                {
                    battleNameField = dataType.GetField("_battleNameKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                string battleNameKey = null;
                if (battleNameField != null)
                {
                    battleNameKey = battleNameField.GetValue(scenarioData) as string;
                }

                // Also try GetBattleName method
                if (string.IsNullOrEmpty(battleNameKey))
                {
                    var getBattleNameMethod = dataType.GetMethod("GetBattleName", BindingFlags.Public | BindingFlags.Instance);
                    if (getBattleNameMethod != null && getBattleNameMethod.GetParameters().Length == 0)
                    {
                        var result = getBattleNameMethod.Invoke(scenarioData, null);
                        if (result is string name && !string.IsNullOrEmpty(name))
                        {
                            return "Battle: " + name;
                        }
                    }
                }

                // Localize the key if found
                if (!string.IsNullOrEmpty(battleNameKey))
                {
                    string localized = LocalizeKey(battleNameKey);
                    if (!string.IsNullOrEmpty(localized))
                    {
                        return "Battle: " + localized;
                    }
                }

                // Fallback to GetName or name property
                return GetFallbackNodeName(scenarioData, dataType, "Battle");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting battle node name: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the name of a generic node (reward, merchant, event, etc.)
        /// </summary>
        private string GetGenericNodeName(object nodeData, Type dataType)
        {
            try
            {
                // Try tooltipTitleKey field
                var titleField = dataType.GetField("tooltipTitleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (titleField == null)
                {
                    titleField = dataType.GetField("_tooltipTitleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                string titleKey = null;
                if (titleField != null)
                {
                    titleKey = titleField.GetValue(nodeData) as string;
                }

                // Localize the key if found
                if (!string.IsNullOrEmpty(titleKey))
                {
                    string localized = LocalizeKey(titleKey);
                    if (!string.IsNullOrEmpty(localized))
                    {
                        return localized;
                    }
                }

                // Determine node type for prefix
                string typeName = dataType.Name;
                string prefix = "";
                if (typeName.Contains("Merchant") || typeName.Contains("Shop"))
                    prefix = "Shop";
                else if (typeName.Contains("Event"))
                    prefix = "Event";
                else if (typeName.Contains("Reward"))
                    prefix = "Reward";

                return GetFallbackNodeName(nodeData, dataType, prefix);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting generic node name: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Try fallback methods to get node name (GetName, name property, etc.)
        /// </summary>
        private string GetFallbackNodeName(object nodeData, Type dataType, string prefix)
        {
            try
            {
                // Try GetName method
                var getNameMethod = dataType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                {
                    var result = getNameMethod.Invoke(nodeData, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                    {
                        return string.IsNullOrEmpty(prefix) ? name : $"{prefix}: {name}";
                    }
                }

                // Try name property
                var nameProp = dataType.GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                if (nameProp != null)
                {
                    var result = nameProp.GetValue(nodeData);
                    if (result is string name && !string.IsNullOrEmpty(name))
                    {
                        // Clean up asset names (remove underscores, etc.)
                        name = CleanAssetName(name);
                        return string.IsNullOrEmpty(prefix) ? name : $"{prefix}: {name}";
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Localize a string key using the game's localization system
        /// </summary>
        private string LocalizeKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            try
            {
                // Try to call the Localize extension method
                var stringType = typeof(string);

                // Look for the Localize extension method in all assemblies
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!type.IsClass || !type.IsSealed || !type.IsAbstract)
                            continue;

                        var method = type.GetMethod("Localize",
                            BindingFlags.Public | BindingFlags.Static,
                            null,
                            new Type[] { typeof(string) },
                            null);

                        if (method != null && method.ReturnType == typeof(string))
                        {
                            var result = method.Invoke(null, new object[] { key });
                            if (result is string localized && !string.IsNullOrEmpty(localized) && localized != key)
                            {
                                return localized;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error localizing key '{key}': {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get a type by name from all loaded assemblies
        /// </summary>
        private Type GetTypeFromAssemblies(string typeName)
        {
            try
            {
                // Try direct Type.GetType first
                var type = Type.GetType(typeName + ", Assembly-CSharp");
                if (type != null) return type;

                // Search all assemblies
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(typeName);
                    if (type != null) return type;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Clean up asset names to be more readable
        /// </summary>
        private string CleanAssetName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            // Remove common prefixes/suffixes
            name = name.Replace("_", " ");
            name = name.Replace("Data", "");
            name = name.Replace("Scenario", "");

            // Add spaces before capital letters
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");

            return name.Trim();
        }

        /// <summary>
        /// Get text for settings screen elements (dropdowns, sliders, toggles)
        /// These have a parent with SettingsEntry component containing the label
        /// </summary>
        private string GetSettingsElementText(GameObject go)
        {
            try
            {
                // Find SettingsEntry component in parent hierarchy
                string settingLabel = null;
                Transform current = go.transform;

                for (int i = 0; i < 3 && current.parent != null; i++)
                {
                    Transform parent = current.parent;

                    // Check if parent has SettingsEntry component
                    foreach (var component in parent.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "SettingsEntry")
                        {
                            // Found it - get the label from parent's name
                            settingLabel = CleanSettingsLabel(parent.name);
                            break;
                        }
                    }

                    if (settingLabel != null) break;
                    current = parent;
                }

                if (string.IsNullOrEmpty(settingLabel))
                    return null;

                // Now get the current value based on the control type
                string value = null;

                // Check for dropdown (GameUISelectableDropdown)
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    string typeName = type.Name;

                    if (typeName.Contains("Dropdown"))
                    {
                        value = GetDropdownValue(component);
                        break;
                    }
                    else if (typeName.Contains("Slider"))
                    {
                        value = GetSliderValue(go);
                        break;
                    }
                    else if (typeName.Contains("Toggle"))
                    {
                        value = GetToggleValue(go);
                        break;
                    }
                }

                // If we couldn't get a specific value, try to get text from children
                if (string.IsNullOrEmpty(value))
                {
                    value = GetTMPText(go);
                    if (!string.IsNullOrEmpty(value))
                    {
                        value = BattleAccessibility.StripRichTextTags(value.Trim());
                    }
                }

                if (!string.IsNullOrEmpty(value))
                {
                    return $"{settingLabel}: {value}";
                }

                return settingLabel;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting settings element text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Clean up settings label names (e.g., "ResolutionDropdown" -> "Resolution")
        /// </summary>
        private string CleanSettingsLabel(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            // Remove common suffixes
            name = name.Replace("Dropdown", "");
            name = name.Replace("dropdown", "");
            name = name.Replace("Toggle", "");
            name = name.Replace("toggle", "");
            name = name.Replace("Slider", "");
            name = name.Replace("slider", "");
            name = name.Replace("Control", "");
            name = name.Replace("control", "");
            name = name.Replace("Option", "");
            name = name.Replace("option", "");
            name = name.Replace("Setting", "");
            name = name.Replace("setting", "");
            name = name.Replace("Entry", "");
            name = name.Replace("entry", "");
            name = name.Replace("input", "");
            name = name.Replace("Input", "");

            // Add spaces before capital letters
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");

            // Handle specific labels
            name = name.Replace("BG", "Background");
            name = name.Replace("SFX", "Sound Effects");
            name = name.Replace("VSync", "V-Sync");
            name = name.Replace("Vsync", "V-Sync");
            name = name.Replace("UI", "Interface");

            return name.Trim();
        }

        /// <summary>
        /// Get the current value from a dropdown component
        /// </summary>
        private string GetDropdownValue(Component dropdown)
        {
            try
            {
                var type = dropdown.GetType();

                // Try to get the current selected text
                var getCurrentTextMethod = type.GetMethod("GetCurrentText") ??
                                           type.GetMethod("GetText") ??
                                           type.GetMethod("GetSelectedText");
                if (getCurrentTextMethod != null)
                {
                    var result = getCurrentTextMethod.Invoke(dropdown, null);
                    if (result != null)
                        return result.ToString();
                }

                // Try currentText or text property
                var textProp = type.GetProperty("currentText") ??
                               type.GetProperty("text") ??
                               type.GetProperty("captionText");
                if (textProp != null)
                {
                    var result = textProp.GetValue(dropdown);
                    if (result != null)
                    {
                        // It might be a TMP_Text component
                        var textComponent = result as Component;
                        if (textComponent != null)
                        {
                            var tmpText = GetTMPTextDirect(textComponent.gameObject);
                            if (!string.IsNullOrEmpty(tmpText))
                                return tmpText;
                        }
                        return result.ToString();
                    }
                }

                // Try to find the label/caption child
                var dropdownGO = dropdown.gameObject;
                foreach (Transform child in dropdownGO.transform)
                {
                    string childName = child.name.ToLower();
                    if (childName.Contains("label") || childName.Contains("caption") || childName.Contains("text"))
                    {
                        string text = GetTMPTextDirect(child.gameObject);
                        if (!string.IsNullOrEmpty(text))
                            return text.Trim();
                    }
                }

                // Last resort - get any TMP text in children
                string anyText = GetTMPText(dropdownGO);
                if (!string.IsNullOrEmpty(anyText))
                    return anyText.Trim();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting dropdown value: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get the current value from a slider
        /// </summary>
        private string GetSliderValue(GameObject go)
        {
            try
            {
                var slider = go.GetComponent<Slider>();
                if (slider != null)
                {
                    // Return as percentage
                    int percent = Mathf.RoundToInt(slider.normalizedValue * 100);
                    return $"{percent}%";
                }

                // Try reflection for custom slider types
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();

                    var valueProp = type.GetProperty("value") ?? type.GetProperty("normalizedValue");
                    if (valueProp != null)
                    {
                        var val = valueProp.GetValue(component);
                        if (val is float f)
                        {
                            int percent = Mathf.RoundToInt(f * 100);
                            return $"{percent}%";
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get the current value from a toggle (on/off)
        /// </summary>
        private string GetToggleValue(GameObject go)
        {
            try
            {
                var toggle = go.GetComponent<Toggle>();
                if (toggle != null)
                {
                    return toggle.isOn ? "on" : "off";
                }

                // Try reflection for custom toggle types
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();

                    var isOnProp = type.GetProperty("isOn") ?? type.GetProperty("IsOn");
                    if (isOnProp != null)
                    {
                        var val = isOnProp.GetValue(component);
                        if (val is bool b)
                            return b ? "on" : "off";
                    }

                    // Check underlying Unity Toggle if it's a wrapper
                    var toggleField = type.GetField("toggle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (toggleField != null)
                    {
                        var innerToggle = toggleField.GetValue(component) as Toggle;
                        if (innerToggle != null)
                            return innerToggle.isOn ? "on" : "off";
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get text for toggle/checkbox controls with their label
        /// </summary>
        private string GetToggleText(GameObject go)
        {
            try
            {
                // First check if this is the Trial toggle on BattleIntroScreen
                string trialText = GetTrialToggleText(go);
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
        /// Special handling for the Trial toggle on BattleIntroScreen
        /// Returns full trial info: name, description, reward, and toggle state
        /// </summary>
        private string GetTrialToggleText(GameObject go)
        {
            try
            {
                // Check if this might be a trial toggle by looking at hierarchy
                bool isBattleIntroToggle = false;
                Component battleIntroScreen = null;
                Transform current = go.transform;

                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "BattleIntroScreen")
                        {
                            battleIntroScreen = component;
                            isBattleIntroToggle = true;
                            break;
                        }
                    }
                    if (isBattleIntroToggle) break;
                    current = current.parent;
                }

                if (!isBattleIntroToggle || battleIntroScreen == null)
                    return null;

                // Get the BattleIntroScreen's trial data
                var screenType = battleIntroScreen.GetType();

                // Get trialEnabled field
                var trialEnabledField = screenType.GetField("trialEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                bool trialEnabled = false;
                if (trialEnabledField != null)
                {
                    var val = trialEnabledField.GetValue(battleIntroScreen);
                    if (val is bool b) trialEnabled = b;
                }

                // Get trialData field
                var trialDataField = screenType.GetField("trialData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                object trialData = trialDataField?.GetValue(battleIntroScreen);

                if (trialData == null)
                    return null;

                // Extract trial information
                var trialType = trialData.GetType();
                string ruleName = null;
                string ruleDescription = null;
                string rewardName = null;

                // The rule comes from the 'sin' field (SinsData), which is a RelicData subclass
                var sinField = trialType.GetField("sin", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (sinField != null)
                {
                    var sinData = sinField.GetValue(trialData);
                    if (sinData != null)
                    {
                        var sinType = sinData.GetType();

                        // Get the rule name from sin
                        var getNameMethod = sinType.GetMethod("GetName");
                        if (getNameMethod != null)
                        {
                            ruleName = getNameMethod.Invoke(sinData, null) as string;
                        }

                        // Get the rule description - try GetDescriptionKey() and localize
                        var getDescKeyMethod = sinType.GetMethod("GetDescriptionKey");
                        if (getDescKeyMethod != null && getDescKeyMethod.GetParameters().Length == 0)
                        {
                            var descKey = getDescKeyMethod.Invoke(sinData, null) as string;
                            if (!string.IsNullOrEmpty(descKey))
                            {
                                ruleDescription = LocalizeKey(descKey);
                            }
                        }
                    }
                }

                // Get reward info from the 'reward' field
                var rewardField = trialType.GetField("reward", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (rewardField != null)
                {
                    var rewardData = rewardField.GetValue(trialData);
                    if (rewardData != null)
                    {
                        rewardName = GetRewardName(rewardData);
                    }
                }

                // Build the announcement
                var sb = new StringBuilder();
                sb.Append("Trial toggle: ");
                sb.Append(trialEnabled ? "ON" : "OFF");
                sb.Append(". ");

                if (trialEnabled)
                {
                    if (!string.IsNullOrEmpty(ruleName))
                    {
                        sb.Append("Additional rule: ");
                        sb.Append(ruleName);
                        sb.Append(". ");
                    }

                    if (!string.IsNullOrEmpty(ruleDescription))
                    {
                        sb.Append(BattleAccessibility.StripRichTextTags(ruleDescription));
                        sb.Append(" ");
                    }

                    if (!string.IsNullOrEmpty(rewardName))
                    {
                        sb.Append("You will gain additional reward: ");
                        sb.Append(rewardName);
                        sb.Append(". ");
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(ruleName))
                    {
                        sb.Append("If enabled, additional rule: ");
                        sb.Append(ruleName);
                        sb.Append(". ");
                    }

                    if (!string.IsNullOrEmpty(ruleDescription))
                    {
                        sb.Append(BattleAccessibility.StripRichTextTags(ruleDescription));
                        sb.Append(" ");
                    }

                    if (!string.IsNullOrEmpty(rewardName))
                    {
                        sb.Append("Enable to gain additional reward: ");
                        sb.Append(rewardName);
                        sb.Append(". ");
                    }
                }

                sb.Append("Press Enter to toggle.");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting trial toggle text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Extract the name from a RewardData object
        /// </summary>
        private string GetRewardName(object rewardData)
        {
            if (rewardData == null) return null;

            try
            {
                var rewardType = rewardData.GetType();
                string typeName = rewardType.Name;

                // Special handling for GoldRewardData - extract the gold amount
                if (typeName == "GoldRewardData" || typeName.Contains("Gold"))
                {
                    var amountField = rewardType.GetField("_amount", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    if (amountField != null)
                    {
                        var amount = amountField.GetValue(rewardData);
                        if (amount is int goldAmount && goldAmount > 0)
                        {
                            return $"{goldAmount} Gold";
                        }
                    }
                    // Try GetAmount method
                    var getAmountMethod = rewardType.GetMethod("GetAmount", Type.EmptyTypes);
                    if (getAmountMethod != null)
                    {
                        var amount = getAmountMethod.Invoke(rewardData, null);
                        if (amount is int goldAmount && goldAmount > 0)
                        {
                            return $"{goldAmount} Gold";
                        }
                    }
                }

                // Try GetTitle method first (if it exists)
                var getTitleMethod = rewardType.GetMethod("GetTitle");
                if (getTitleMethod != null)
                {
                    var title = getTitleMethod.Invoke(rewardData, null) as string;
                    // Only use if it looks like a real name (not a key)
                    if (!string.IsNullOrEmpty(title) && !title.Contains("_") && !title.Contains("-"))
                        return title;
                }

                // Try to get the title key and localize it
                var titleKeyField = rewardType.GetField("_rewardTitleKey", BindingFlags.NonPublic | BindingFlags.Instance);
                if (titleKeyField != null)
                {
                    var titleKey = titleKeyField.GetValue(rewardData) as string;
                    if (!string.IsNullOrEmpty(titleKey))
                    {
                        // Try to localize the key
                        string localized = LocalizeKey(titleKey);
                        // Only use if localization succeeded (not same as key and looks like real text)
                        if (!string.IsNullOrEmpty(localized) && localized != titleKey && !localized.Contains("-"))
                            return localized;
                    }
                }

                // Fall back to type name - this is the most reliable approach
                return GetRewardTypeDisplayName(rewardType);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting reward name: {ex.Message}");
                return "Reward";
            }
        }

        /// <summary>
        /// Get a human-readable display name from the reward type
        /// </summary>
        private string GetRewardTypeDisplayName(Type rewardType)
        {
            string typeName = rewardType.Name;
            if (typeName.EndsWith("RewardData"))
                typeName = typeName.Substring(0, typeName.Length - "RewardData".Length);

            // Convert type name to readable format
            switch (typeName)
            {
                case "RelicPool": return "Random Artifact";
                case "Relic": return "Artifact";
                case "CardPool": return "Random Card";
                case "Card": return "Card";
                case "Gold": return "Gold";
                case "Health": return "Pyre Health";
                case "Crystal": return "Crystal";
                case "EnhancerPool": return "Random Upgrade";
                case "Enhancer": return "Upgrade";
                case "Draft": return "Card Draft";
                case "RelicDraft": return "Artifact Choice";
                default: return typeName;
            }
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

                        string sibText = GetTMPTextDirect(sibling.gameObject);
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
                    string parentName = CleanGameObjectName(parent.name);
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

                            string uncleText = GetTMPTextDirect(uncle.gameObject);
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
        /// Get text for shop items (cards, relics, services/upgrades)
        /// </summary>
        private string GetShopItemText(GameObject go)
        {
            try
            {
                // Look for MerchantGoodDetailsUI (cards/relics for sale)
                Component goodDetailsUI = FindComponentInHierarchy(go, "MerchantGoodDetailsUI");
                if (goodDetailsUI != null)
                {
                    string goodText = ExtractMerchantGoodInfo(goodDetailsUI);
                    if (!string.IsNullOrEmpty(goodText))
                    {
                        return goodText;
                    }
                }

                // Look for MerchantServiceUI (services/upgrades)
                Component serviceUI = FindComponentInHierarchy(go, "MerchantServiceUI");
                if (serviceUI != null)
                {
                    string serviceText = ExtractMerchantServiceInfo(serviceUI);
                    if (!string.IsNullOrEmpty(serviceText))
                    {
                        return serviceText;
                    }
                }

                // Look for BuyButton component to get price
                Component buyButton = FindComponentInHierarchy(go, "BuyButton");
                if (buyButton != null)
                {
                    // Try to find the associated good or service
                    var buyType = buyButton.GetType();
                    var buyFields = buyType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    MonsterTrainAccessibility.LogInfo($"BuyButton fields: {string.Join(", ", buyFields.Select(f => f.Name))}");

                    // Look for good/service reference
                    foreach (var field in buyFields)
                    {
                        var value = field.GetValue(buyButton);
                        if (value == null) continue;

                        string typeName = value.GetType().Name;
                        if (typeName.Contains("Good") || typeName.Contains("Service") ||
                            typeName.Contains("Card") || typeName.Contains("Relic"))
                        {
                            MonsterTrainAccessibility.LogInfo($"Found {field.Name}: {typeName}");
                            string info = ExtractShopItemInfo(value);
                            if (!string.IsNullOrEmpty(info))
                            {
                                return info;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting shop item text: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Find a component by type name in the hierarchy (up and down)
        /// </summary>
        private Component FindComponentInHierarchy(GameObject go, string typeName)
        {
            // Check this object and children
            foreach (var comp in go.GetComponentsInChildren<Component>(true))
            {
                if (comp != null && comp.GetType().Name == typeName)
                    return comp;
            }

            // Check parents
            Transform current = go.transform.parent;
            while (current != null)
            {
                foreach (var comp in current.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == typeName)
                        return comp;
                }
                current = current.parent;
            }

            return null;
        }

        /// <summary>
        /// Extract info from MerchantGoodDetailsUI (card/relic for sale)
        /// </summary>
        private string ExtractMerchantGoodInfo(Component goodUI)
        {
            try
            {
                var uiType = goodUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                MonsterTrainAccessibility.LogInfo($"MerchantGoodDetailsUI fields: {string.Join(", ", fields.Select(f => f.Name).Take(15))}");

                // Look for rewardUI field - this contains the actual reward data
                var rewardUIField = fields.FirstOrDefault(f => f.Name == "rewardUI");
                if (rewardUIField != null)
                {
                    var rewardUI = rewardUIField.GetValue(goodUI);
                    if (rewardUI != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found rewardUI: {rewardUI.GetType().Name}");
                        string rewardInfo = ExtractRewardUIInfo(rewardUI);
                        if (!string.IsNullOrEmpty(rewardInfo))
                        {
                            // Try to get price from parent BuyButton
                            string price = GetPriceFromBuyButton(goodUI.gameObject);
                            if (!string.IsNullOrEmpty(price))
                            {
                                return $"{rewardInfo}. {price}";
                            }
                            return rewardInfo;
                        }
                    }
                }

                // Fallback: look for card data directly
                foreach (var field in fields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("card") || fieldName.Contains("good") || fieldName.Contains("data"))
                    {
                        var value = field.GetValue(goodUI);
                        if (value != null)
                        {
                            string cardInfo = ExtractCardInfo(value);
                            if (!string.IsNullOrEmpty(cardInfo))
                            {
                                string price = GetPriceFromBuyButton(goodUI.gameObject);
                                if (!string.IsNullOrEmpty(price))
                                {
                                    return $"{cardInfo}. {price}";
                                }
                                return cardInfo;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting merchant good info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract info from a RewardUI component
        /// </summary>
        private string ExtractRewardUIInfo(object rewardUI)
        {
            if (rewardUI == null) return null;

            try
            {
                var uiType = rewardUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var methods = uiType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                MonsterTrainAccessibility.LogInfo($"RewardUI type: {uiType.Name}");

                // Priority 1: Check rewardData backing field first - this has the actual data
                var rewardDataField = fields.FirstOrDefault(f =>
                    f.Name == "<rewardData>k__BackingField" || f.Name == "rewardData");
                if (rewardDataField != null)
                {
                    var rewardData = rewardDataField.GetValue(rewardUI);
                    if (rewardData != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found rewardData: {rewardData.GetType().Name}");
                        string info = ExtractRewardDataInfo(rewardData);
                        if (!string.IsNullOrEmpty(info))
                            return info;
                    }
                }

                // Priority 2: Try GetRewardData method
                var getDataMethod = methods.FirstOrDefault(m =>
                    (m.Name == "GetRewardData" || m.Name == "GetData" || m.Name == "GetReward") &&
                    m.GetParameters().Length == 0);
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(rewardUI, null);
                    if (data != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"GetRewardData returned: {data.GetType().Name}");
                        string info = ExtractRewardDataInfo(data);
                        if (!string.IsNullOrEmpty(info))
                            return info;
                    }
                }

                // Priority 3: Check cardUI field for card rewards
                var cardUIField = fields.FirstOrDefault(f => f.Name == "cardUI");
                if (cardUIField != null)
                {
                    var cardUI = cardUIField.GetValue(rewardUI);
                    if (cardUI != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found cardUI: {cardUI.GetType().Name}");
                        string cardInfo = ExtractCardUIInfo(cardUI);
                        if (!string.IsNullOrEmpty(cardInfo))
                            return cardInfo;
                    }
                }

                // Priority 4: Check relicUI field
                var relicUIField = fields.FirstOrDefault(f => f.Name == "relicUI");
                if (relicUIField != null)
                {
                    var relicUI = relicUIField.GetValue(rewardUI);
                    if (relicUI != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found relicUI: {relicUI.GetType().Name}");
                        string relicInfo = ExtractRelicUIInfo(relicUI);
                        if (!string.IsNullOrEmpty(relicInfo))
                            return relicInfo;
                    }
                }

                // Priority 5: Check genericRewardUI field
                var genericField = fields.FirstOrDefault(f => f.Name == "genericRewardUI");
                if (genericField != null)
                {
                    var genericUI = genericField.GetValue(rewardUI);
                    if (genericUI != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found genericRewardUI: {genericUI.GetType().Name}");
                        string genericInfo = ExtractGenericRewardUIInfo(genericUI);
                        if (!string.IsNullOrEmpty(genericInfo))
                            return genericInfo;
                    }
                }

                // Fallback: If rewardUI is a Component, check its GameObject for text
                if (rewardUI is Component comp)
                {
                    string textInfo = GetFirstMeaningfulChildText(comp.gameObject);
                    if (!string.IsNullOrEmpty(textInfo))
                        return textInfo;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting reward UI info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract info from a CardUI component
        /// </summary>
        private string ExtractCardUIInfo(object cardUI)
        {
            if (cardUI == null) return null;

            try
            {
                var uiType = cardUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // Look for cardState field
                var cardStateField = fields.FirstOrDefault(f =>
                    f.Name.ToLower().Contains("cardstate") || f.Name.ToLower().Contains("card"));
                if (cardStateField != null)
                {
                    var cardState = cardStateField.GetValue(cardUI);
                    if (cardState != null)
                    {
                        return ExtractCardInfo(cardState);
                    }
                }

                // Try GetCardState method
                var getCardMethod = uiType.GetMethod("GetCardState", Type.EmptyTypes);
                if (getCardMethod != null)
                {
                    var cardState = getCardMethod.Invoke(cardUI, null);
                    if (cardState != null)
                    {
                        return ExtractCardInfo(cardState);
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Extract info from a RelicUI component
        /// </summary>
        private string ExtractRelicUIInfo(object relicUI)
        {
            if (relicUI == null) return null;

            try
            {
                var uiType = relicUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // Look for relicData field
                foreach (var field in fields)
                {
                    if (field.Name.ToLower().Contains("data") || field.Name.ToLower().Contains("relic"))
                    {
                        var data = field.GetValue(relicUI);
                        if (data != null)
                        {
                            string info = ExtractRewardDataInfo(data);
                            if (!string.IsNullOrEmpty(info))
                                return info;
                        }
                    }
                }

                // Try GetRelicData method
                var getDataMethod = uiType.GetMethod("GetRelicData", Type.EmptyTypes) ??
                                   uiType.GetMethod("GetData", Type.EmptyTypes);
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(relicUI, null);
                    if (data != null)
                    {
                        return ExtractRewardDataInfo(data);
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Extract info from a generic reward UI (RewardIconUI)
        /// </summary>
        private string ExtractGenericRewardUIInfo(object genericUI)
        {
            if (genericUI == null) return null;

            try
            {
                var uiType = genericUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // Look for data field
                foreach (var field in fields)
                {
                    if (field.Name.ToLower().Contains("data") || field.Name.ToLower().Contains("reward"))
                    {
                        var data = field.GetValue(genericUI);
                        if (data != null && !data.GetType().Name.Contains("Transform"))
                        {
                            string info = ExtractRewardDataInfo(data);
                            if (!string.IsNullOrEmpty(info))
                                return info;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Extract info from reward data (CardData, RelicData, etc.)
        /// </summary>
        private string ExtractRewardDataInfo(object data)
        {
            if (data == null) return null;

            try
            {
                var dataType = data.GetType();
                string typeName = dataType.Name;

                MonsterTrainAccessibility.LogInfo($"Extracting reward data from: {typeName}");

                // Special handling for EnhancerRewardData (upgrade stones like Surgestone)
                if (typeName == "EnhancerRewardData")
                {
                    string enhancerInfo = ExtractEnhancerInfo(data);
                    if (!string.IsNullOrEmpty(enhancerInfo))
                        return enhancerInfo;
                }

                // Try GetName method
                var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(data, null) as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        MonsterTrainAccessibility.LogInfo($"GetName returned: {name}");

                        // Try to get description too
                        string desc = null;
                        var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes);
                        if (getDescMethod != null)
                        {
                            desc = getDescMethod.Invoke(data, null) as string;
                        }

                        // Try to get cost for cards
                        int cost = -1;
                        var getCostMethod = dataType.GetMethod("GetCost", Type.EmptyTypes);
                        if (getCostMethod != null)
                        {
                            var costResult = getCostMethod.Invoke(data, null);
                            if (costResult is int c)
                                cost = c;
                        }

                        List<string> parts = new List<string>();
                        parts.Add(BattleAccessibility.StripRichTextTags(name));

                        if (cost >= 0)
                            parts.Add($"{cost} ember");

                        if (!string.IsNullOrEmpty(desc))
                            parts.Add(BattleAccessibility.StripRichTextTags(desc));

                        return string.Join(". ", parts);
                    }
                }

                // For CardState, get CardData first
                if (typeName == "CardState")
                {
                    return ExtractCardInfo(data);
                }

                // Try fields
                var fields = dataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var nameField = fields.FirstOrDefault(f => f.Name.ToLower().Contains("name"));
                if (nameField != null)
                {
                    var name = nameField.GetValue(data) as string;
                    if (!string.IsNullOrEmpty(name))
                        return BattleAccessibility.StripRichTextTags(name);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting reward data: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract info from EnhancerRewardData (upgrade stones like Surgestone, Emberstone, etc.)
        /// </summary>
        private string ExtractEnhancerInfo(object enhancerRewardData)
        {
            if (enhancerRewardData == null) return null;

            try
            {
                var rewardType = enhancerRewardData.GetType();
                var fields = rewardType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // Look for the enhancerData field (the actual EnhancerData object)
                object enhancerData = null;
                var enhancerDataField = fields.FirstOrDefault(f =>
                    f.Name.ToLower().Contains("enhancerdata") ||
                    f.Name == "enhancer" ||
                    f.Name == "_enhancerData");

                if (enhancerDataField != null)
                {
                    enhancerData = enhancerDataField.GetValue(enhancerRewardData);
                }

                // Try GetEnhancerData method
                if (enhancerData == null)
                {
                    var getEnhancerMethod = rewardType.GetMethod("GetEnhancerData", Type.EmptyTypes)
                                         ?? rewardType.GetMethod("GetEnhancer", Type.EmptyTypes);
                    if (getEnhancerMethod != null)
                    {
                        enhancerData = getEnhancerMethod.Invoke(enhancerRewardData, null);
                    }
                }

                // Check backing field
                if (enhancerData == null)
                {
                    var backingField = fields.FirstOrDefault(f => f.Name == "<enhancerData>k__BackingField");
                    if (backingField != null)
                    {
                        enhancerData = backingField.GetValue(enhancerRewardData);
                    }
                }

                if (enhancerData != null)
                {
                    MonsterTrainAccessibility.LogInfo($"Found EnhancerData: {enhancerData.GetType().Name}");
                    return ExtractEnhancerDataInfo(enhancerData);
                }

                // If no enhancerData found, log available fields for debugging
                MonsterTrainAccessibility.LogInfo($"EnhancerRewardData fields: {string.Join(", ", fields.Select(f => f.Name).Take(15))}");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting enhancer info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract info from EnhancerData (the actual upgrade stone data)
        /// </summary>
        private string ExtractEnhancerDataInfo(object enhancerData)
        {
            if (enhancerData == null) return null;

            try
            {
                var dataType = enhancerData.GetType();
                var fields = dataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var methods = dataType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                string name = null;
                string description = null;

                // Try GetName method
                var getNameMethod = methods.FirstOrDefault(m => m.Name == "GetName" && m.GetParameters().Length == 0);
                if (getNameMethod != null)
                {
                    name = getNameMethod.Invoke(enhancerData, null) as string;
                }

                // Try GetDescription method
                var getDescMethod = methods.FirstOrDefault(m => m.Name == "GetDescription" && m.GetParameters().Length == 0);
                if (getDescMethod != null)
                {
                    description = getDescMethod.Invoke(enhancerData, null) as string;
                }

                // Try GetID and localize
                if (string.IsNullOrEmpty(name))
                {
                    var getIdMethod = methods.FirstOrDefault(m => m.Name == "GetID" && m.GetParameters().Length == 0);
                    if (getIdMethod != null)
                    {
                        string id = getIdMethod.Invoke(enhancerData, null) as string;
                        if (!string.IsNullOrEmpty(id))
                        {
                            // Try standard localization keys
                            name = LocalizeKey($"{id}_EnhancerData_NameKey")
                                ?? LocalizeKey($"EnhancerData_{id}_Name");
                            if (string.IsNullOrEmpty(description))
                            {
                                description = LocalizeKey($"{id}_EnhancerData_DescriptionKey")
                                           ?? LocalizeKey($"EnhancerData_{id}_Description");
                            }
                        }
                    }
                }

                // Try to get upgrade info from the CardUpgradeData
                if (string.IsNullOrEmpty(description))
                {
                    // EnhancerData stores upgrade in effects[0].GetParamCardUpgradeData()
                    var getEffectsMethod = methods.FirstOrDefault(m => m.Name == "GetEffects" && m.GetParameters().Length == 0);
                    if (getEffectsMethod != null)
                    {
                        var effects = getEffectsMethod.Invoke(enhancerData, null) as System.Collections.IList;
                        if (effects != null && effects.Count > 0)
                        {
                            var effect = effects[0];
                            var effectType = effect.GetType();
                            var getUpgradeMethod = effectType.GetMethod("GetParamCardUpgradeData", Type.EmptyTypes);
                            if (getUpgradeMethod != null)
                            {
                                var upgradeData = getUpgradeMethod.Invoke(effect, null);
                                if (upgradeData != null)
                                {
                                    description = ExtractCardUpgradeDescription(upgradeData);
                                }
                            }
                        }
                    }
                }

                // Build result
                if (!string.IsNullOrEmpty(name))
                {
                    var parts = new List<string>();
                    parts.Add(BattleAccessibility.StripRichTextTags(name));
                    parts.Add("Upgrade");

                    if (!string.IsNullOrEmpty(description))
                    {
                        parts.Add(BattleAccessibility.StripRichTextTags(description));
                    }

                    // Add helper instruction
                    parts.Add("After selecting a card, press Enter to apply the upgrade");

                    // Extract and append keyword explanations
                    string fullText = string.Join(". ", parts);
                    var keywords = new List<string>();
                    ExtractKeywordsFromDescription(fullText, keywords);
                    if (keywords.Count > 0)
                    {
                        parts.AddRange(keywords);
                    }

                    MonsterTrainAccessibility.LogInfo($"Enhancer result: {string.Join(". ", parts)}");
                    return string.Join(". ", parts);
                }

                // Fallback: try name field
                var nameField = fields.FirstOrDefault(f => f.Name == "name");
                if (nameField != null)
                {
                    name = nameField.GetValue(enhancerData) as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        return BattleAccessibility.StripRichTextTags(name) + " (Upgrade)";
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting enhancer data: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract description from CardUpgradeData
        /// </summary>
        private string ExtractCardUpgradeDescription(object upgradeData)
        {
            if (upgradeData == null) return null;

            try
            {
                var dataType = upgradeData.GetType();
                var parts = new List<string>();

                // Get upgrade title/name
                var getTitleMethod = dataType.GetMethod("GetUpgradeTitleForCardText", Type.EmptyTypes)
                                  ?? dataType.GetMethod("GetUpgradeTitle", Type.EmptyTypes)
                                  ?? dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    var title = getTitleMethod.Invoke(upgradeData, null) as string;
                    if (!string.IsNullOrEmpty(title))
                    {
                        parts.Add(BattleAccessibility.StripRichTextTags(title));
                    }
                }

                // Get upgrade description
                var getDescMethod = dataType.GetMethod("GetUpgradeDescription", Type.EmptyTypes)
                                 ?? dataType.GetMethod("GetDescription", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    var desc = getDescMethod.Invoke(upgradeData, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                    {
                        parts.Add(BattleAccessibility.StripRichTextTags(desc));
                    }
                }

                // If no description, try to extract stat bonuses
                if (parts.Count <= 1)
                {
                    var bonuses = ExtractUpgradeBonuses(upgradeData);
                    if (!string.IsNullOrEmpty(bonuses))
                    {
                        parts.Add(bonuses);
                    }
                }

                if (parts.Count > 0)
                {
                    return string.Join(". ", parts);
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Extract stat bonuses from CardUpgradeData
        /// </summary>
        private string ExtractUpgradeBonuses(object upgradeData)
        {
            if (upgradeData == null) return null;

            try
            {
                var dataType = upgradeData.GetType();
                var bonuses = new List<string>();

                // Check common bonus methods/fields
                var getBonusDamageMethod = dataType.GetMethod("GetBonusDamage", Type.EmptyTypes);
                if (getBonusDamageMethod != null)
                {
                    var damage = getBonusDamageMethod.Invoke(upgradeData, null);
                    if (damage is int d && d != 0)
                    {
                        bonuses.Add($"{(d > 0 ? "+" : "")}{d} Magic Power");
                    }
                }

                var getBonusHPMethod = dataType.GetMethod("GetBonusHP", Type.EmptyTypes);
                if (getBonusHPMethod != null)
                {
                    var hp = getBonusHPMethod.Invoke(upgradeData, null);
                    if (hp is int h && h != 0)
                    {
                        bonuses.Add($"{(h > 0 ? "+" : "")}{h} Health");
                    }
                }

                var getCostReductionMethod = dataType.GetMethod("GetCostReduction", Type.EmptyTypes);
                if (getCostReductionMethod != null)
                {
                    var reduction = getCostReductionMethod.Invoke(upgradeData, null);
                    if (reduction is int r && r != 0)
                    {
                        bonuses.Add($"-{r} Ember cost");
                    }
                }

                // Check for added traits
                var getTraitsMethod = dataType.GetMethod("GetTraitDataUpgradeList", Type.EmptyTypes)
                                   ?? dataType.GetMethod("GetTraitDataUpgrades", Type.EmptyTypes);
                if (getTraitsMethod != null)
                {
                    var traits = getTraitsMethod.Invoke(upgradeData, null) as System.Collections.IList;
                    if (traits != null && traits.Count > 0)
                    {
                        foreach (var trait in traits)
                        {
                            var traitType = trait.GetType();

                            // Log trait type methods once for debugging
                            MonsterTrainAccessibility.LogInfo($"Trait type: {traitType.Name}");
                            foreach (var m in traitType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                            {
                                if (m.GetParameters().Length == 0 && m.ReturnType == typeof(string))
                                {
                                    var result = m.Invoke(trait, null) as string;
                                    MonsterTrainAccessibility.LogInfo($"  {m.Name}() = {result}");
                                }
                            }

                            // Try to get localized trait name first
                            string traitName = null;

                            // Try GetTraitStateName which might return localized name
                            var getLocalizedMethod = traitType.GetMethod("GetTraitStateName", Type.EmptyTypes);
                            if (getLocalizedMethod != null)
                            {
                                traitName = getLocalizedMethod.Invoke(trait, null) as string;
                            }

                            // Fallback to GetName
                            if (string.IsNullOrEmpty(traitName))
                            {
                                var getNameMethod = traitType.GetMethod("GetName", Type.EmptyTypes);
                                if (getNameMethod != null)
                                {
                                    traitName = getNameMethod.Invoke(trait, null) as string;
                                }
                            }

                            if (!string.IsNullOrEmpty(traitName))
                            {
                                // Format trait name - strip internal prefixes
                                traitName = traitName.Replace("CardTraitState", "").Replace("CardTrait", "").Replace("State", "");
                                // Map internal names to display names
                                traitName = MapTraitToDisplayName(traitName);
                                bonuses.Add($"Gain {traitName}");
                            }
                        }
                    }
                }

                if (bonuses.Count > 0)
                {
                    return string.Join(" and ", bonuses);
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Map internal trait names to player-facing display names
        /// </summary>
        private string MapTraitToDisplayName(string internalName)
        {
            if (string.IsNullOrEmpty(internalName)) return internalName;

            // Dictionary of internal trait names to display names
            var traitMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Juice", "Doublestack" },
                { "DoubleStack", "Doublestack" },
                // Add more mappings as discovered
            };

            if (traitMappings.TryGetValue(internalName, out string displayName))
            {
                return displayName;
            }

            // If no mapping, return the cleaned-up internal name
            return internalName;
        }

        /// <summary>
        /// Get price from the BuyButton component
        /// </summary>
        private string GetPriceFromBuyButton(GameObject go)
        {
            try
            {
                // Find BuyButton in hierarchy
                Component buyButton = FindComponentInHierarchy(go, "BuyButton");
                if (buyButton != null)
                {
                    var btnType = buyButton.GetType();
                    var fields = btnType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    // Look for cost field
                    var costField = fields.FirstOrDefault(f => f.Name == "cost");
                    if (costField != null)
                    {
                        var costValue = costField.GetValue(buyButton);
                        if (costValue is int cost && cost > 0)
                        {
                            return $"{cost} gold";
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Extract info from MerchantServiceUI (upgrade/service)
        /// </summary>
        private string ExtractMerchantServiceInfo(Component serviceUI)
        {
            try
            {
                var uiType = serviceUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var methods = uiType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                // Log the GameObject hierarchy - the name often contains the service type
                var go = serviceUI.gameObject;
                string hierarchyPath = go.name;
                var parent = go.transform.parent;
                while (parent != null)
                {
                    hierarchyPath = parent.name + "/" + hierarchyPath;
                    parent = parent.parent;
                    if (hierarchyPath.Length > 200) break; // Safety limit
                }
                MonsterTrainAccessibility.LogInfo($"MerchantServiceUI hierarchy: {hierarchyPath}");
                MonsterTrainAccessibility.LogInfo($"MerchantServiceUI fields: {string.Join(", ", fields.Select(f => f.Name).Take(20))}");

                // Log all components on this GameObject
                var components = go.GetComponents<Component>();
                MonsterTrainAccessibility.LogInfo($"Components on GO: {string.Join(", ", components.Select(c => c?.GetType().Name ?? "null"))}");

                string serviceName = null;
                string serviceDesc = null;

                // Priority 0: Check if GameObject name contains service type
                string goName = go.name.ToLower();
                if (goName.Contains("reroll"))
                {
                    serviceName = "Reroll";
                }
                else if (goName.Contains("purge") || goName.Contains("remove"))
                {
                    serviceName = "Purge Card";
                }
                else if (goName.Contains("duplicate") || goName.Contains("copy"))
                {
                    serviceName = "Duplicate Card";
                }
                else if (goName.Contains("upgrade") || goName.Contains("enhance"))
                {
                    serviceName = "Upgrade Card";
                }
                else if (goName.Contains("heal") || goName.Contains("repair"))
                {
                    serviceName = "Heal";
                }
                else if (goName.Contains("unleash"))
                {
                    serviceName = "Unleash";
                }

                if (!string.IsNullOrEmpty(serviceName))
                {
                    MonsterTrainAccessibility.LogInfo($"Got service name from GO name: {serviceName}");
                }

                // Priority 1: Extract service index from GO name and get data from MerchantScreen
                if (string.IsNullOrEmpty(serviceName))
                {
                    // Parse service sign index from name like "Service sign 1", "Service sign 2"
                    int serviceIndex = -1;
                    var match = System.Text.RegularExpressions.Regex.Match(go.name, @"Service sign (\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        serviceIndex = int.Parse(match.Groups[1].Value) - 1; // Convert to 0-based
                        MonsterTrainAccessibility.LogInfo($"Service sign index: {serviceIndex}");
                    }

                    // Find MerchantScreen or MerchantScreenContent parent and get services list
                    var parentTransform = go.transform.parent;
                    while (parentTransform != null && string.IsNullOrEmpty(serviceName))
                    {
                        var parentGO = parentTransform.gameObject;
                        var parentComponents = parentGO.GetComponents<Component>();

                        foreach (var comp in parentComponents)
                        {
                            if (comp == null) continue;
                            var compType = comp.GetType();
                            var compName = compType.Name;

                            // Look for MerchantScreen or MerchantScreenContent
                            if (compName == "MerchantScreen" || compName == "MerchantScreenContent")
                            {
                                MonsterTrainAccessibility.LogInfo($"Found parent component: {compType.Name}");

                                // Look for services list/array field
                                var compFields = compType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                MonsterTrainAccessibility.LogInfo($"{compName} fields: {string.Join(", ", compFields.Select(f => f.Name).Take(20))}");

                                // First check sourceMerchantData which should contain the actual service definitions
                                var merchantDataField = compFields.FirstOrDefault(f => f.Name == "sourceMerchantData");
                                if (merchantDataField != null)
                                {
                                    var merchantData = merchantDataField.GetValue(comp);
                                    if (merchantData != null)
                                    {
                                        var mdType = merchantData.GetType();
                                        MonsterTrainAccessibility.LogInfo($"sourceMerchantData type: {mdType.Name}");

                                        // Log all fields on merchant data
                                        var mdFields = mdType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                        MonsterTrainAccessibility.LogInfo($"MerchantData fields: {string.Join(", ", mdFields.Select(f => f.Name).Take(20))}");

                                        // Look for services list in merchant data
                                        foreach (var mdField in mdFields)
                                        {
                                            string mdFieldName = mdField.Name.ToLower();
                                            if (mdFieldName.Contains("service"))
                                            {
                                                var servicesValue = mdField.GetValue(merchantData);
                                                if (servicesValue != null)
                                                {
                                                    MonsterTrainAccessibility.LogInfo($"Found {mdField.Name}: {servicesValue.GetType().Name}");

                                                    if (servicesValue is System.Collections.IList servicesList && serviceIndex >= 0 && serviceIndex < servicesList.Count)
                                                    {
                                                        var svcData = servicesList[serviceIndex];
                                                        if (svcData != null)
                                                        {
                                                            var svcType = svcData.GetType();
                                                            MonsterTrainAccessibility.LogInfo($"Service[{serviceIndex}] type: {svcType.Name}");

                                                            // Log service data fields
                                                            var svcFields = svcType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                                            MonsterTrainAccessibility.LogInfo($"Service fields: {string.Join(", ", svcFields.Select(f => f.Name).Take(15))}");

                                                            var getNameMethod = svcType.GetMethod("GetName", Type.EmptyTypes);
                                                            if (getNameMethod != null)
                                                            {
                                                                serviceName = getNameMethod.Invoke(svcData, null) as string;
                                                                MonsterTrainAccessibility.LogInfo($"Service name from GetName(): {serviceName}");
                                                            }

                                                            // Try GetDescription
                                                            var getDescMethod = svcType.GetMethod("GetDescription", Type.EmptyTypes) ??
                                                                               svcType.GetMethod("GetTooltipDescription", Type.EmptyTypes);
                                                            if (getDescMethod != null)
                                                            {
                                                                serviceDesc = getDescMethod.Invoke(svcData, null) as string;
                                                            }

                                                            if (!string.IsNullOrEmpty(serviceName)) break;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                if (!string.IsNullOrEmpty(serviceName)) break;

                                foreach (var field in compFields)
                                {
                                    string fieldName = field.Name.ToLower();
                                    var value = field.GetValue(comp);
                                    if (value == null) continue;

                                    // Look for services list
                                    if (fieldName.Contains("service"))
                                    {
                                        MonsterTrainAccessibility.LogInfo($"Found field {field.Name}: {value.GetType().Name}");

                                        // If it's a list/array, try to get item by index
                                        if (value is System.Collections.IList list && serviceIndex >= 0 && serviceIndex < list.Count)
                                        {
                                            var serviceData = list[serviceIndex];
                                            if (serviceData != null)
                                            {
                                                var dataType = serviceData.GetType();
                                                MonsterTrainAccessibility.LogInfo($"Service data type: {dataType.Name}");

                                                var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                                                if (getNameMethod != null)
                                                {
                                                    serviceName = getNameMethod.Invoke(serviceData, null) as string;
                                                    MonsterTrainAccessibility.LogInfo($"Service name from list[{serviceIndex}]: {serviceName}");
                                                }

                                                var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes) ??
                                                                   dataType.GetMethod("GetTooltipDescription", Type.EmptyTypes);
                                                if (getDescMethod != null)
                                                {
                                                    serviceDesc = getDescMethod.Invoke(serviceData, null) as string;
                                                }

                                                if (!string.IsNullOrEmpty(serviceName)) break;
                                            }
                                        }

                                        // If it's a single service data, try GetName
                                        var valueType = value.GetType();
                                        var nameMethod = valueType.GetMethod("GetName", Type.EmptyTypes);
                                        if (nameMethod != null)
                                        {
                                            serviceName = nameMethod.Invoke(value, null) as string;
                                            if (!string.IsNullOrEmpty(serviceName))
                                            {
                                                MonsterTrainAccessibility.LogInfo($"Service name from {field.Name}: {serviceName}");
                                                break;
                                            }
                                        }
                                    }
                                }

                                if (!string.IsNullOrEmpty(serviceName)) break;
                            }
                        }

                        parentTransform = parentTransform.parent;
                    }
                }

                // Priority 2: Look for service data via properties on MerchantServiceUI
                if (string.IsNullOrEmpty(serviceName))
                {
                    // Check all properties on MerchantServiceUI
                    var props = uiType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    MonsterTrainAccessibility.LogInfo($"MerchantServiceUI properties: {string.Join(", ", props.Select(p => p.Name).Take(15))}");

                    // Check GoodState property specifically - this likely contains the service data
                    var goodStateProp = props.FirstOrDefault(p => p.Name == "GoodState");
                    if (goodStateProp != null)
                    {
                        try
                        {
                            var goodState = goodStateProp.GetValue(serviceUI);
                            if (goodState != null)
                            {
                                var gsType = goodState.GetType();
                                MonsterTrainAccessibility.LogInfo($"GoodState type: {gsType.Name}");

                                // Log GoodState fields
                                var gsFields = gsType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                MonsterTrainAccessibility.LogInfo($"GoodState fields: {string.Join(", ", gsFields.Select(f => f.Name).Take(15))}");

                                // Log GoodState properties
                                var gsProps = gsType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                MonsterTrainAccessibility.LogInfo($"GoodState properties: {string.Join(", ", gsProps.Select(p => p.Name).Take(15))}");

                                // Check RewardData property - this should have the actual service info
                                var rewardDataProp = gsProps.FirstOrDefault(p => p.Name == "RewardData");
                                if (rewardDataProp != null)
                                {
                                    try
                                    {
                                        var rewardData = rewardDataProp.GetValue(goodState);
                                        if (rewardData != null)
                                        {
                                            var rdType = rewardData.GetType();
                                            MonsterTrainAccessibility.LogInfo($"RewardData type: {rdType.Name}");

                                            // Map RewardData type name to friendly service name and description
                                            (serviceName, serviceDesc) = rdType.Name switch
                                            {
                                                "PurgeRewardData" => ("Purge Card", "Remove a card from your deck"),
                                                "RerollMerchantRewardData" => ("Re-roll", "Randomize and refresh the offered goods"),
                                                "DuplicateRewardData" => ("Duplicate Card", "Create a copy of a card in your deck"),
                                                "HealRewardData" => ("Heal", "Restore health to your Pyre"),
                                                "TrainRepairRewardData" => ("Train Repair", "Repair your train"),
                                                "UnleashRewardData" => ("Unleash", "Choose a Branded unit and unleash its power"),
                                                "UpgradeRewardData" => ("Upgrade", "Upgrade a card"),
                                                "EnhancerRewardData" => ("Upgrade Stone", null),
                                                _ => (null, null)
                                            };

                                            if (!string.IsNullOrEmpty(serviceName))
                                            {
                                                MonsterTrainAccessibility.LogInfo($"Service name from type mapping: {serviceName}");
                                            }

                                            // If mapping didn't work, try GetName method
                                            if (string.IsNullOrEmpty(serviceName))
                                            {
                                                var getNameMethod = rdType.GetMethod("GetName", Type.EmptyTypes);
                                                if (getNameMethod != null)
                                                {
                                                    serviceName = getNameMethod.Invoke(rewardData, null) as string;
                                                    MonsterTrainAccessibility.LogInfo($"Service name from RewardData.GetName(): {serviceName}");
                                                }
                                            }

                                            // Only try to get description from game if we don't have one from mapping
                                            if (string.IsNullOrEmpty(serviceDesc))
                                            {
                                                // Try various methods for getting the description
                                                var rdMethods = rdType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                                                var descMethodNames = new[] { "GetDescription", "GetTooltipDescription", "GetRewardDescription", "GetLocalizedDescription" };

                                                foreach (var methodName in descMethodNames)
                                                {
                                                    var descMethod = rdMethods.FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == 0);
                                                    if (descMethod != null)
                                                    {
                                                        try
                                                        {
                                                            var desc = descMethod.Invoke(rewardData, null) as string;
                                                            if (!string.IsNullOrEmpty(desc) && !desc.Contains("__") && !desc.Contains("-v2"))
                                                            {
                                                                serviceDesc = desc;
                                                                MonsterTrainAccessibility.LogInfo($"Got description from {methodName}: {serviceDesc}");
                                                                break;
                                                            }
                                                        }
                                                        catch { }
                                                    }
                                                }
                                            }

                                            // Don't use description if it looks like a raw localization key
                                            if (!string.IsNullOrEmpty(serviceDesc) && (serviceDesc.Contains("__") || serviceDesc.Contains("-v2") || serviceDesc.StartsWith("$")))
                                            {
                                                serviceDesc = null;
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        MonsterTrainAccessibility.LogError($"Error reading RewardData: {ex.Message}");
                                    }
                                }

                                // Try GetName method on GoodState itself
                                if (string.IsNullOrEmpty(serviceName))
                                {
                                    var getNameMethod = gsType.GetMethod("GetName", Type.EmptyTypes);
                                    if (getNameMethod != null)
                                    {
                                        serviceName = getNameMethod.Invoke(goodState, null) as string;
                                        MonsterTrainAccessibility.LogInfo($"Service name from GoodState.GetName(): {serviceName}");
                                    }
                                }

                                // Try GetDescription method
                                if (string.IsNullOrEmpty(serviceDesc))
                                {
                                    var getDescMethod = gsType.GetMethod("GetDescription", Type.EmptyTypes) ??
                                                       gsType.GetMethod("GetTooltipDescription", Type.EmptyTypes);
                                    if (getDescMethod != null)
                                    {
                                        serviceDesc = getDescMethod.Invoke(goodState, null) as string;
                                        MonsterTrainAccessibility.LogInfo($"Service desc from GoodState: {serviceDesc}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MonsterTrainAccessibility.LogError($"Error reading GoodState: {ex.Message}");
                        }
                    }

                    foreach (var prop in props)
                    {
                        string propName = prop.Name.ToLower();
                        if (propName.Contains("service") || propName.Contains("data"))
                        {
                            try
                            {
                                var value = prop.GetValue(serviceUI);
                                if (value != null)
                                {
                                    MonsterTrainAccessibility.LogInfo($"Property {prop.Name}: {value.GetType().Name}");

                                    var valueType = value.GetType();
                                    var getNameMethod = valueType.GetMethod("GetName", Type.EmptyTypes);
                                    if (getNameMethod != null)
                                    {
                                        serviceName = getNameMethod.Invoke(value, null) as string;
                                        MonsterTrainAccessibility.LogInfo($"Service name from property: {serviceName}");
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    // Check all methods for GetServiceData or similar
                    var allMethods = uiType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var getDataMethods = allMethods.Where(m =>
                        (m.Name.Contains("Service") || m.Name.Contains("Data")) &&
                        m.GetParameters().Length == 0 &&
                        m.ReturnType != typeof(void)).Take(5);

                    foreach (var method in getDataMethods)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found method: {method.Name} returns {method.ReturnType.Name}");
                        try
                        {
                            var result = method.Invoke(serviceUI, null);
                            if (result != null)
                            {
                                MonsterTrainAccessibility.LogInfo($"Method {method.Name} returned: {result.GetType().Name}");

                                var resultType = result.GetType();
                                var getNameMethod = resultType.GetMethod("GetName", Type.EmptyTypes);
                                if (getNameMethod != null)
                                {
                                    serviceName = getNameMethod.Invoke(result, null) as string;
                                    MonsterTrainAccessibility.LogInfo($"Service name from method result: {serviceName}");
                                    if (!string.IsNullOrEmpty(serviceName)) break;
                                }
                            }
                        }
                        catch { }
                    }
                }

                // Priority 2: Search all fields for data objects
                if (string.IsNullOrEmpty(serviceName))
                {
                    foreach (var field in fields)
                    {
                        string fieldName = field.Name.ToLower();
                        var value = field.GetValue(serviceUI);
                        if (value == null) continue;

                        // Check for service/data objects
                        if (fieldName.Contains("service") || fieldName.Contains("data"))
                        {
                            MonsterTrainAccessibility.LogInfo($"Checking field {field.Name}: {value.GetType().Name}");

                            // Try to get name/description from data object
                            var dataType = value.GetType();

                            var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                            if (getNameMethod != null)
                            {
                                serviceName = getNameMethod.Invoke(value, null) as string;
                                MonsterTrainAccessibility.LogInfo($"Service name from {field.Name}: {serviceName}");
                            }

                            var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes) ??
                                               dataType.GetMethod("GetTooltipDescription", Type.EmptyTypes);
                            if (getDescMethod != null)
                            {
                                serviceDesc = getDescMethod.Invoke(value, null) as string;
                            }

                            if (!string.IsNullOrEmpty(serviceName))
                                break;
                        }
                    }
                }

                // Priority 3: Try methods on the UI component itself
                if (string.IsNullOrEmpty(serviceName))
                {
                    var getNameMethod = methods.FirstOrDefault(m =>
                        m.Name == "GetServiceName" || m.Name == "GetName");
                    if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                    {
                        serviceName = getNameMethod.Invoke(serviceUI, null) as string;
                        MonsterTrainAccessibility.LogInfo($"Service name from method: {serviceName}");
                    }
                }

                // Priority 4: Search child transforms directly for text
                if (string.IsNullOrEmpty(serviceName))
                {
                    var serviceGO = serviceUI.gameObject;

                    // Log all immediate children
                    var childNames = new List<string>();
                    for (int i = 0; i < serviceGO.transform.childCount; i++)
                    {
                        var child = serviceGO.transform.GetChild(i);
                        childNames.Add(child.name);

                        // Try to get text from each immediate child
                        string childText = GetTMPTextDirect(child.gameObject);
                        if (!string.IsNullOrEmpty(childText))
                        {
                            MonsterTrainAccessibility.LogInfo($"Found text in child '{child.name}': {childText}");
                        }

                        // Also check grandchildren
                        for (int j = 0; j < child.childCount; j++)
                        {
                            var grandchild = child.GetChild(j);
                            string gcText = GetTMPTextDirect(grandchild.gameObject);
                            if (!string.IsNullOrEmpty(gcText))
                            {
                                MonsterTrainAccessibility.LogInfo($"Found text in grandchild '{child.name}/{grandchild.name}': {gcText}");
                            }
                        }
                    }
                    MonsterTrainAccessibility.LogInfo($"MerchantServiceUI children: {string.Join(", ", childNames)}");

                    // Look for specific named children that might contain the title
                    var titleChildNames = new[] { "Title", "TitleLabel", "Name", "ServiceName", "TitleText", "Label", "Text" };
                    foreach (var childName in titleChildNames)
                    {
                        var titleChild = serviceGO.transform.Find(childName);
                        if (titleChild != null)
                        {
                            serviceName = GetTMPTextDirect(titleChild.gameObject);
                            if (!string.IsNullOrEmpty(serviceName))
                            {
                                MonsterTrainAccessibility.LogInfo($"Got service name from child '{childName}': {serviceName}");
                                break;
                            }
                        }
                    }

                    // If still not found, get all text from children
                    if (string.IsNullOrEmpty(serviceName))
                    {
                        var childTexts = GetAllTextFromChildren(serviceGO);
                        MonsterTrainAccessibility.LogInfo($"Child texts found: {string.Join(", ", childTexts.Take(5))}");

                        if (childTexts.Count > 0)
                        {
                            serviceName = childTexts[0];
                            MonsterTrainAccessibility.LogInfo($"Got service name from children: {serviceName}");

                            if (childTexts.Count > 1)
                            {
                                serviceDesc = childTexts[1];
                            }
                        }
                    }
                }

                // Priority 5: Try titleLabel and descriptionLabel fields as last resort
                if (string.IsNullOrEmpty(serviceName))
                {
                    var titleLabelField = fields.FirstOrDefault(f => f.Name == "titleLabel");
                    var descLabelField = fields.FirstOrDefault(f => f.Name == "descriptionLabel");

                    if (titleLabelField != null)
                    {
                        var titleLabel = titleLabelField.GetValue(serviceUI);
                        if (titleLabel != null)
                        {
                            serviceName = GetTextFromComponent(titleLabel);
                            MonsterTrainAccessibility.LogInfo($"Got title from titleLabel field: {serviceName}");
                        }
                    }

                    if (descLabelField != null)
                    {
                        var descLabel = descLabelField.GetValue(serviceUI);
                        if (descLabel != null)
                        {
                            serviceDesc = GetTextFromComponent(descLabel);
                        }
                    }
                }

                // Priority 6: Check for text/label fields (as strings)
                if (string.IsNullOrEmpty(serviceName))
                {
                    foreach (var field in fields)
                    {
                        string fieldName = field.Name.ToLower();
                        var value = field.GetValue(serviceUI);

                        if (value is string str && !string.IsNullOrEmpty(str))
                        {
                            if (fieldName.Contains("name") || fieldName.Contains("title"))
                            {
                                serviceName = str;
                                MonsterTrainAccessibility.LogInfo($"Got service name from string field {field.Name}: {serviceName}");
                            }
                            else if (fieldName.Contains("desc"))
                            {
                                serviceDesc = str;
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(serviceName))
                {
                    serviceName = BattleAccessibility.StripRichTextTags(serviceName);
                    string price = GetShopItemPrice(serviceUI);

                    List<string> parts = new List<string> { serviceName };

                    if (!string.IsNullOrEmpty(serviceDesc) && serviceDesc != serviceName)
                    {
                        parts.Add(BattleAccessibility.StripRichTextTags(serviceDesc));
                    }

                    if (!string.IsNullOrEmpty(price))
                    {
                        parts.Add(price);
                    }

                    return string.Join(". ", parts);
                }

                MonsterTrainAccessibility.LogWarning("Could not extract service name from MerchantServiceUI");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting merchant service info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get price from a shop item component
        /// </summary>
        private string GetShopItemPrice(Component shopItem)
        {
            try
            {
                var itemType = shopItem.GetType();
                var fields = itemType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (var field in fields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("price") || fieldName.Contains("cost") || fieldName.Contains("gold"))
                    {
                        var value = field.GetValue(shopItem);
                        if (value is int intPrice && intPrice > 0)
                        {
                            return $"{intPrice} gold";
                        }
                    }
                }

                // Try methods
                var methods = itemType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                var getPriceMethod = methods.FirstOrDefault(m =>
                    (m.Name == "GetPrice" || m.Name == "GetCost" || m.Name == "GetGoldCost") &&
                    m.GetParameters().Length == 0);

                if (getPriceMethod != null)
                {
                    var result = getPriceMethod.Invoke(shopItem, null);
                    if (result is int price && price > 0)
                    {
                        return $"{price} gold";
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Extract card info from CardState or CardData
        /// </summary>
        private string ExtractCardInfo(object cardObj)
        {
            if (cardObj == null) return null;

            try
            {
                var cardType = cardObj.GetType();

                // If this is CardState, get CardData first
                object cardData = cardObj;
                if (cardType.Name == "CardState")
                {
                    var getDataMethod = cardType.GetMethod("GetCardDataRead", Type.EmptyTypes);
                    if (getDataMethod != null)
                    {
                        cardData = getDataMethod.Invoke(cardObj, null);
                        if (cardData == null) return null;
                    }
                }

                var dataType = cardData.GetType();
                string name = null;
                string description = null;
                int cost = -1;

                // Get name
                var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    name = getNameMethod.Invoke(cardData, null) as string;
                }

                // Get description
                var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    description = getDescMethod.Invoke(cardData, null) as string;
                }

                // Get cost
                var getCostMethod = dataType.GetMethod("GetCost", Type.EmptyTypes);
                if (getCostMethod != null)
                {
                    var costResult = getCostMethod.Invoke(cardData, null);
                    if (costResult is int c)
                        cost = c;
                }

                if (!string.IsNullOrEmpty(name))
                {
                    List<string> parts = new List<string>();
                    parts.Add(BattleAccessibility.StripRichTextTags(name));

                    if (cost >= 0)
                    {
                        parts.Add($"{cost} ember");
                    }

                    if (!string.IsNullOrEmpty(description))
                    {
                        parts.Add(BattleAccessibility.StripRichTextTags(description));
                    }

                    // Add keyword definitions
                    string keywords = GetCardKeywordTooltips(cardObj, cardData, description);
                    if (!string.IsNullOrEmpty(keywords))
                    {
                        parts.Add($"Keywords: {keywords}");
                    }

                    return string.Join(". ", parts);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting card info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract info from a generic shop item
        /// </summary>
        private string ExtractShopItemInfo(object item)
        {
            if (item == null) return null;

            try
            {
                var itemType = item.GetType();

                // Try GetName
                var getNameMethod = itemType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(item, null) as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        return BattleAccessibility.StripRichTextTags(name);
                    }
                }

                // Try to find card data inside
                var fields = itemType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("card") || fieldName.Contains("data"))
                    {
                        var value = field.GetValue(item);
                        if (value != null)
                        {
                            string info = ExtractCardInfo(value);
                            if (!string.IsNullOrEmpty(info))
                                return info;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Get full card details when arrowing over a card in the hand (CardUI component)
        /// </summary>
        private string GetCardUIText(GameObject go)
        {
            try
            {
                // Find CardUI component on this object or in hierarchy
                Component cardUIComponent = null;

                // Check this object and its children first
                foreach (var component in go.GetComponentsInChildren<Component>())
                {
                    if (component == null) continue;
                    if (component.GetType().Name == "CardUI")
                    {
                        cardUIComponent = component;
                        break;
                    }
                }

                // If not found, check parents
                if (cardUIComponent == null)
                {
                    Transform current = go.transform;
                    while (current != null && cardUIComponent == null)
                    {
                        foreach (var component in current.GetComponents<Component>())
                        {
                            if (component == null) continue;
                            if (component.GetType().Name == "CardUI")
                            {
                                cardUIComponent = component;
                                break;
                            }
                        }
                        current = current.parent;
                    }
                }

                if (cardUIComponent == null)
                    return null;

                // Get CardState from CardUI
                var cardUIType = cardUIComponent.GetType();
                object cardState = null;

                // Try common field/property names for the card state reference
                var cardStateField = cardUIType.GetField("cardState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (cardStateField != null)
                {
                    cardState = cardStateField.GetValue(cardUIComponent);
                }

                // Try _cardState
                if (cardState == null)
                {
                    cardStateField = cardUIType.GetField("_cardState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (cardStateField != null)
                    {
                        cardState = cardStateField.GetValue(cardUIComponent);
                    }
                }

                // Try CardState property
                if (cardState == null)
                {
                    var cardStateProp = cardUIType.GetProperty("CardState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (cardStateProp != null)
                    {
                        cardState = cardStateProp.GetValue(cardUIComponent);
                    }
                }

                // Try GetCard or GetCardState method
                if (cardState == null)
                {
                    var getCardMethod = cardUIType.GetMethod("GetCard", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (getCardMethod != null && getCardMethod.GetParameters().Length == 0)
                    {
                        cardState = getCardMethod.Invoke(cardUIComponent, null);
                    }
                }

                if (cardState == null)
                {
                    var getCardStateMethod = cardUIType.GetMethod("GetCardState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (getCardStateMethod != null && getCardStateMethod.GetParameters().Length == 0)
                    {
                        cardState = getCardStateMethod.Invoke(cardUIComponent, null);
                    }
                }

                if (cardState == null)
                {
                    MonsterTrainAccessibility.LogInfo("CardUI found but couldn't get CardState");
                    return null;
                }

                // Format the card details
                string cardDetails = FormatCardDetails(cardState);

                // Look for an upgrade path name/title above the card (e.g., "Wrathful", "Brawler")
                // Only do this outside of battle (e.g., on Dark Forge screen)
                if (Help.ScreenStateTracker.CurrentScreen != Help.GameScreen.Battle)
                {
                    string upgradePath = FindUpgradePathName(cardUIComponent);

                    // Prepend upgrade path if found
                    if (!string.IsNullOrEmpty(upgradePath) && !string.IsNullOrEmpty(cardDetails))
                    {
                        return upgradePath + ": " + cardDetails;
                    }
                }

                return cardDetails;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting card UI text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Find upgrade path name (like "Wrathful", "Brawler") for champion upgrade screens
        /// Looks for title text elements above or near the CardUI
        /// </summary>
        private string FindUpgradePathName(Component cardUIComponent)
        {
            try
            {
                if (cardUIComponent == null) return null;

                Transform cardTransform = cardUIComponent.transform;

                // Look for a parent that might contain the title
                // Common patterns: the card is in a container with a sibling title element
                Transform parent = cardTransform.parent;
                while (parent != null)
                {
                    // Look for TMP_Text components in siblings or parent's direct children
                    foreach (Transform sibling in parent)
                    {
                        // Skip the card itself and its descendants
                        if (sibling == cardTransform || sibling.IsChildOf(cardTransform))
                            continue;

                        // Look for text components
                        foreach (var component in sibling.GetComponentsInChildren<Component>())
                        {
                            if (component == null) continue;
                            string typeName = component.GetType().Name;

                            if (typeName == "TextMeshProUGUI" || typeName == "ExtendedTextMeshProUGUI" ||
                                typeName == "TextMeshPro" || typeName == "Text")
                            {
                                // Get the text property
                                var textProp = component.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                                if (textProp != null)
                                {
                                    string text = textProp.GetValue(component) as string;
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        text = text.Trim();
                                        // Upgrade path names are typically short (1-2 words)
                                        // and don't contain numbers, colons, or card-like content
                                        if (text.Length > 0 && text.Length < 30 &&
                                            !text.Contains(":") && !text.Contains(".") &&
                                            !System.Text.RegularExpressions.Regex.IsMatch(text, @"\d") &&
                                            !text.ToLower().Contains("champion") &&
                                            !text.ToLower().Contains("cost") &&
                                            !text.ToLower().Contains("ember"))
                                        {
                                            // Check if the text element is positioned above the card
                                            // by comparing Y positions (higher Y = above in UI)
                                            RectTransform textRect = component.transform as RectTransform;
                                            RectTransform cardRect = cardTransform as RectTransform;

                                            if (textRect != null && cardRect != null)
                                            {
                                                // If text is above the card (higher world position Y)
                                                if (textRect.position.y > cardRect.position.y)
                                                {
                                                    MonsterTrainAccessibility.LogInfo($"Found upgrade path name: '{text}'");
                                                    return text;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Move up the hierarchy
                    parent = parent.parent;

                    // Don't go too far up
                    if (parent != null && (parent.name.Contains("Canvas") || parent.name.Contains("Screen")))
                        break;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error finding upgrade path name: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Format card details into a readable string (name, type, clan, cost, description)
        /// </summary>
        private string FormatCardDetails(object cardState)
        {
            try
            {
                var sb = new StringBuilder();
                var type = cardState.GetType();

                MonsterTrainAccessibility.LogInfo($"FormatCardDetails called for type: {type.Name}");

                // Get card name
                string name = "Unknown Card";
                var getTitleMethod = type.GetMethod("GetTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    name = getTitleMethod.Invoke(cardState, null) as string ?? "Unknown Card";
                    MonsterTrainAccessibility.LogInfo($"Card name: {name}");
                }

                // Get card type
                string cardType = null;
                var getCardTypeMethod = type.GetMethod("GetCardType");
                if (getCardTypeMethod != null)
                {
                    var cardTypeObj = getCardTypeMethod.Invoke(cardState, null);
                    if (cardTypeObj != null)
                    {
                        cardType = cardTypeObj.ToString();
                        if (cardType == "Monster") cardType = "Unit";
                    }
                }

                // Get ember cost
                int cost = 0;
                var getCostMethod = type.GetMethod("GetCostWithoutAnyModifications", Type.EmptyTypes)
                                  ?? type.GetMethod("GetCost", Type.EmptyTypes);
                if (getCostMethod != null)
                {
                    var costResult = getCostMethod.Invoke(cardState, null);
                    if (costResult is int c) cost = c;
                }

                // Get CardData to access linked class (clan) and better descriptions
                object cardData = null;
                string clanName = null;
                string description = null;

                var getCardDataMethod = type.GetMethod("GetCardDataRead", Type.EmptyTypes)
                                     ?? type.GetMethod("GetCardData", Type.EmptyTypes);
                MonsterTrainAccessibility.LogInfo($"GetCardData method: {(getCardDataMethod != null ? getCardDataMethod.Name : "NOT FOUND")}");

                // Log all methods that might be related to card data
                var cardDataMethods = type.GetMethods()
                    .Where(m => m.Name.Contains("CardData") || m.Name.Contains("Data"))
                    .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
                    .Distinct()
                    .ToArray();
                MonsterTrainAccessibility.LogInfo($"CardState data methods: {string.Join(", ", cardDataMethods)}");

                if (getCardDataMethod != null)
                {
                    cardData = getCardDataMethod.Invoke(cardState, null);
                    MonsterTrainAccessibility.LogInfo($"CardData result: {(cardData != null ? cardData.GetType().Name : "null")}");
                }

                if (cardData != null)
                {
                    var cardDataType = cardData.GetType();

                    // Get linked class (clan) from CardData
                    var linkedClassField = cardDataType.GetField("linkedClass", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (linkedClassField != null)
                    {
                        var linkedClass = linkedClassField.GetValue(cardData);
                        if (linkedClass != null)
                        {
                            var classType = linkedClass.GetType();
                            // Try GetTitle() for localized name
                            var getClassTitleMethod = classType.GetMethod("GetTitle", Type.EmptyTypes);
                            if (getClassTitleMethod != null)
                            {
                                clanName = getClassTitleMethod.Invoke(linkedClass, null) as string;
                            }
                            // Fallback to GetName()
                            if (string.IsNullOrEmpty(clanName))
                            {
                                var getClassNameMethod = classType.GetMethod("GetName", Type.EmptyTypes);
                                if (getClassNameMethod != null)
                                {
                                    clanName = getClassNameMethod.Invoke(linkedClass, null) as string;
                                }
                            }
                        }
                    }

                    // Try GetDescription from CardData for effect text
                    var getDescMethod = cardDataType.GetMethod("GetDescription", Type.EmptyTypes);
                    if (getDescMethod != null)
                    {
                        description = getDescMethod.Invoke(cardData, null) as string;
                    }

                    // If no parameterless GetDescription, try with RelicManager parameter
                    if (string.IsNullOrEmpty(description))
                    {
                        var allDescMethods = cardDataType.GetMethods().Where(m => m.Name.Contains("Description")).ToArray();
                        foreach (var descMethod in allDescMethods)
                        {
                            var ps = descMethod.GetParameters();
                            // Log available description methods for debugging
                            MonsterTrainAccessibility.LogInfo($"CardData has description method: {descMethod.Name}({string.Join(", ", ps.Select(p => p.ParameterType.Name))})");
                        }
                    }
                }

                // Try GetCardText on CardState - this is the main method for card effect text
                if (string.IsNullOrEmpty(description))
                {
                    // Log all GetCardText methods for debugging
                    var cardTextMethods = type.GetMethods().Where(m => m.Name == "GetCardText").ToArray();
                    MonsterTrainAccessibility.LogInfo($"Found {cardTextMethods.Length} GetCardText methods");
                    foreach (var method in cardTextMethods)
                    {
                        var ps = method.GetParameters();
                        MonsterTrainAccessibility.LogInfo($"  GetCardText({string.Join(", ", ps.Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
                    }

                    // Try GetCardText with no parameters first
                    var getCardTextMethod = type.GetMethod("GetCardText", Type.EmptyTypes);
                    if (getCardTextMethod != null)
                    {
                        description = getCardTextMethod.Invoke(cardState, null) as string;
                        MonsterTrainAccessibility.LogInfo($"GetCardText() returned: '{description}'");
                    }

                    // If no parameterless version, try with parameters
                    if (string.IsNullOrEmpty(description))
                    {
                        foreach (var method in cardTextMethods)
                        {
                            var ps = method.GetParameters();
                            try
                            {
                                var args = new object[ps.Length];
                                for (int i = 0; i < ps.Length; i++)
                                {
                                    if (ps[i].ParameterType == typeof(bool))
                                        args[i] = true;
                                    else if (ps[i].ParameterType.IsValueType)
                                        args[i] = Activator.CreateInstance(ps[i].ParameterType);
                                    else
                                        args[i] = null;
                                }
                                description = method.Invoke(cardState, args) as string;
                                MonsterTrainAccessibility.LogInfo($"GetCardText with {ps.Length} params returned: '{description}'");
                                if (!string.IsNullOrEmpty(description)) break;
                            }
                            catch (Exception ex)
                            {
                                MonsterTrainAccessibility.LogInfo($"GetCardText failed: {ex.Message}");
                            }
                        }
                    }
                }

                // Fallback: try GetAssetDescription
                if (string.IsNullOrEmpty(description))
                {
                    var getAssetDescMethod = type.GetMethod("GetAssetDescription", Type.EmptyTypes);
                    if (getAssetDescMethod != null)
                    {
                        description = getAssetDescMethod.Invoke(cardState, null) as string;
                    }
                }

                // Log if we still have no description
                if (string.IsNullOrEmpty(description))
                {
                    var cardTextMethods = type.GetMethods().Where(m => m.Name == "GetCardText").ToArray();
                    MonsterTrainAccessibility.LogInfo($"GetCardText methods: {string.Join(", ", cardTextMethods.Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})"))}");
                }

                // Build announcement: Name (Type), Clan, Cost. Effect.
                sb.Append(name);
                if (!string.IsNullOrEmpty(cardType))
                {
                    sb.Append($" ({cardType})");
                }
                if (!string.IsNullOrEmpty(clanName))
                {
                    sb.Append($", {clanName}");
                }
                sb.Append($", {cost} ember");

                if (!string.IsNullOrEmpty(description))
                {
                    // Strip rich text tags for screen reader output
                    description = BattleAccessibility.StripRichTextTags(description);
                    sb.Append($". {description}");
                }

                // For unit cards, try to get attack and health stats
                if (cardType == "Unit" || cardType == "Monster")
                {
                    MonsterTrainAccessibility.LogInfo($"Unit card detected, looking for stats. cardData is {(cardData != null ? "not null" : "NULL")}");
                    int attack = -1;
                    int health = -1;

                    // Try to get stats from CardState
                    var getAttackMethod = type.GetMethod("GetAttackDamage", Type.EmptyTypes);
                    MonsterTrainAccessibility.LogInfo($"GetAttackDamage on CardState: {(getAttackMethod != null ? "found" : "not found")}");
                    if (getAttackMethod != null)
                    {
                        var attackResult = getAttackMethod.Invoke(cardState, null);
                        if (attackResult is int a) attack = a;
                        MonsterTrainAccessibility.LogInfo($"Attack from CardState: {attack}");
                    }

                    var getHPMethod = type.GetMethod("GetHP", Type.EmptyTypes)
                                   ?? type.GetMethod("GetHealth", Type.EmptyTypes)
                                   ?? type.GetMethod("GetMaxHP", Type.EmptyTypes);
                    MonsterTrainAccessibility.LogInfo($"GetHP/Health on CardState: {(getHPMethod != null ? getHPMethod.Name : "not found")}");
                    if (getHPMethod != null)
                    {
                        var hpResult = getHPMethod.Invoke(cardState, null);
                        if (hpResult is int h) health = h;
                        MonsterTrainAccessibility.LogInfo($"Health from CardState: {health}");
                    }

                    // If not found on CardState, try GetSpawnCharacterData directly on CardState
                    MonsterTrainAccessibility.LogInfo($"Stats after CardState check: attack={attack}, health={health}");
                    if (attack < 0 || health < 0)
                    {
                        // GetSpawnCharacterData is directly on CardState, not CardData
                        var getSpawnCharMethod = type.GetMethod("GetSpawnCharacterData", Type.EmptyTypes);
                        MonsterTrainAccessibility.LogInfo($"GetSpawnCharacterData on CardState: {(getSpawnCharMethod != null ? "found" : "not found")}");
                        if (getSpawnCharMethod != null)
                        {
                            var charData = getSpawnCharMethod.Invoke(cardState, null);
                            MonsterTrainAccessibility.LogInfo($"SpawnCharacterData result: {(charData != null ? charData.GetType().Name : "null")}");
                            if (charData != null)
                            {
                                var charDataType = charData.GetType();

                                // Log all methods on character data
                                var charMethods = charDataType.GetMethods()
                                    .Where(m => m.Name.Contains("Attack") || m.Name.Contains("Damage") || m.Name.Contains("HP") || m.Name.Contains("Health") || m.Name.Contains("Size"))
                                    .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
                                    .Distinct()
                                    .ToArray();
                                MonsterTrainAccessibility.LogInfo($"CharacterData stat methods available: {string.Join(", ", charMethods)}");

                                if (attack < 0)
                                {
                                    var charAttackMethod = charDataType.GetMethod("GetAttackDamage", Type.EmptyTypes);
                                    if (charAttackMethod != null)
                                    {
                                        var attackResult = charAttackMethod.Invoke(charData, null);
                                        if (attackResult is int a) attack = a;
                                        MonsterTrainAccessibility.LogInfo($"Attack from CharacterData: {attack}");
                                    }
                                }

                                if (health < 0)
                                {
                                    var charHPMethod = charDataType.GetMethod("GetHealth", Type.EmptyTypes)
                                                   ?? charDataType.GetMethod("GetHP", Type.EmptyTypes)
                                                   ?? charDataType.GetMethod("GetMaxHP", Type.EmptyTypes);
                                    if (charHPMethod != null)
                                    {
                                        var hpResult = charHPMethod.Invoke(charData, null);
                                        if (hpResult is int h) health = h;
                                        MonsterTrainAccessibility.LogInfo($"Health from CharacterData: {health}");
                                    }
                                }

                                // Log what methods are available if still not found
                                if (attack < 0 || health < 0)
                                {
                                    var statMethods = charDataType.GetMethods()
                                        .Where(m => m.Name.Contains("Attack") || m.Name.Contains("Damage") || m.Name.Contains("HP") || m.Name.Contains("Health") || m.Name.Contains("Size"))
                                        .Select(m => m.Name)
                                        .Distinct()
                                        .ToArray();
                                    MonsterTrainAccessibility.LogInfo($"CharacterData stat methods: {string.Join(", ", statMethods)}");
                                }
                            }
                        }
                    }

                    // Append unit stats
                    if (attack >= 0 || health >= 0)
                    {
                        var stats = new List<string>();
                        if (attack >= 0) stats.Add($"{attack} attack");
                        if (health >= 0) stats.Add($"{health} health");
                        sb.Append($". {string.Join(", ", stats)}");
                    }
                }

                // Get keyword tooltips (Permafrost, Frozen, Regen, etc.)
                // Pass the description we already have to avoid re-fetching
                string keywordTooltips = GetCardKeywordTooltips(cardState, cardData, description);
                if (!string.IsNullOrEmpty(keywordTooltips))
                {
                    sb.Append($". Keywords: {keywordTooltips}");
                }

                var result = sb.ToString();
                MonsterTrainAccessibility.LogInfo($"FormatCardDetails result: {result}");
                return result;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error formatting card details: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get keyword tooltip definitions from a card (Permafrost, Frozen, Regen, etc.)
        /// Returns formatted string of keyword definitions
        /// </summary>
        private string GetCardKeywordTooltips(object cardState, object cardData, string cardDescription = null)
        {
            try
            {
                var tooltips = new List<string>();

                // Method 1: Try to get linked tooltips directly from CardState
                if (cardState != null)
                {
                    var stateType = cardState.GetType();

                    // Try GetEffectTooltipData or similar methods
                    var getTooltipsMethod = stateType.GetMethods()
                        .FirstOrDefault(m => m.Name.Contains("Tooltip") && m.GetParameters().Length == 0);
                    if (getTooltipsMethod != null)
                    {
                        var tooltipResult = getTooltipsMethod.Invoke(cardState, null);
                        if (tooltipResult is System.Collections.IList tooltipList)
                        {
                            foreach (var tooltip in tooltipList)
                            {
                                string tooltipText = ExtractTooltipText(tooltip);
                                if (!string.IsNullOrEmpty(tooltipText))
                                    tooltips.Add(tooltipText);
                            }
                        }
                    }
                }

                // Method 2: Get tooltips from CardData's effects
                if (cardData != null && tooltips.Count == 0)
                {
                    var dataType = cardData.GetType();

                    // Get card effects - each effect can have additionalTooltips
                    var getEffectsMethod = dataType.GetMethod("GetEffects", Type.EmptyTypes);
                    if (getEffectsMethod != null)
                    {
                        var effects = getEffectsMethod.Invoke(cardData, null) as System.Collections.IList;
                        if (effects != null)
                        {
                            foreach (var effect in effects)
                            {
                                // Get additionalTooltips from each effect
                                ExtractTooltipsFromEffect(effect, tooltips);

                                // Also get status effect tooltips from paramStatusEffects
                                ExtractStatusEffectTooltips(effect, tooltips);
                            }
                        }
                    }

                    // Also check card traits for tooltips
                    var getTraitsMethod = dataType.GetMethod("GetTraits", Type.EmptyTypes);
                    if (getTraitsMethod != null)
                    {
                        var traits = getTraitsMethod.Invoke(cardData, null) as System.Collections.IList;
                        if (traits != null)
                        {
                            foreach (var trait in traits)
                            {
                                ExtractTraitTooltip(trait, tooltips);
                            }
                        }
                    }
                }

                // Method 3: Parse keywords from card description and look up definitions
                // Use the passed description if available, otherwise try to fetch it
                if (tooltips.Count == 0)
                {
                    string desc = cardDescription;
                    if (string.IsNullOrEmpty(desc))
                    {
                        desc = GetCardDescriptionForKeywordParsing(cardState, cardData);
                    }
                    if (!string.IsNullOrEmpty(desc))
                    {
                        ExtractKeywordsFromDescription(desc, tooltips);
                    }
                }

                if (tooltips.Count > 0)
                {
                    return string.Join(". ", tooltips.Distinct());
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting keyword tooltips: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Extract tooltip text from a tooltip data object
        /// </summary>
        private string ExtractTooltipText(object tooltip)
        {
            if (tooltip == null) return null;

            try
            {
                var tooltipType = tooltip.GetType();
                string title = null;
                string body = null;

                // Try GetTitle/GetBody methods
                var getTitleMethod = tooltipType.GetMethod("GetTitle", Type.EmptyTypes);
                var getBodyMethod = tooltipType.GetMethod("GetBody", Type.EmptyTypes)
                                 ?? tooltipType.GetMethod("GetDescription", Type.EmptyTypes);

                if (getTitleMethod != null)
                    title = getTitleMethod.Invoke(tooltip, null) as string;
                if (getBodyMethod != null)
                    body = getBodyMethod.Invoke(tooltip, null) as string;

                // Try title/body fields
                if (string.IsNullOrEmpty(title))
                {
                    var titleField = tooltipType.GetField("title", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                  ?? tooltipType.GetField("_title", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (titleField != null)
                        title = titleField.GetValue(tooltip) as string;
                }

                if (string.IsNullOrEmpty(body))
                {
                    var bodyField = tooltipType.GetField("body", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                 ?? tooltipType.GetField("description", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (bodyField != null)
                        body = bodyField.GetValue(tooltip) as string;
                }

                // Localize if needed
                title = LocalizeIfNeeded(title);
                body = LocalizeIfNeeded(body);

                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(body))
                {
                    return $"{BattleAccessibility.StripRichTextTags(title)}: {BattleAccessibility.StripRichTextTags(body)}";
                }
                else if (!string.IsNullOrEmpty(title))
                {
                    return BattleAccessibility.StripRichTextTags(title);
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Extract tooltips from a card effect (CardEffectData)
        /// </summary>
        private void ExtractTooltipsFromEffect(object effect, List<string> tooltips)
        {
            if (effect == null) return;

            try
            {
                var effectType = effect.GetType();

                // Get additionalTooltips field
                var tooltipsField = effectType.GetField("additionalTooltips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (tooltipsField != null)
                {
                    var additionalTooltips = tooltipsField.GetValue(effect) as System.Collections.IList;
                    if (additionalTooltips != null)
                    {
                        foreach (var tooltip in additionalTooltips)
                        {
                            string text = ExtractAdditionalTooltipData(tooltip);
                            if (!string.IsNullOrEmpty(text) && !tooltips.Contains(text))
                                tooltips.Add(text);
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Extract tooltip from AdditionalTooltipData
        /// </summary>
        private string ExtractAdditionalTooltipData(object tooltipData)
        {
            if (tooltipData == null) return null;

            try
            {
                var type = tooltipData.GetType();

                // AdditionalTooltipData has titleKey, descriptionKey, or title/description
                string title = null;
                string description = null;

                // Try titleKey/descriptionKey first
                var titleKeyField = type.GetField("titleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var descKeyField = type.GetField("descriptionKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (titleKeyField != null)
                {
                    string key = titleKeyField.GetValue(tooltipData) as string;
                    title = LocalizeKey(key);
                }

                if (descKeyField != null)
                {
                    string key = descKeyField.GetValue(tooltipData) as string;
                    description = LocalizeKey(key);
                }

                // Also try direct title/description
                if (string.IsNullOrEmpty(title))
                {
                    var titleField = type.GetField("title", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (titleField != null)
                        title = titleField.GetValue(tooltipData) as string;
                }

                if (string.IsNullOrEmpty(description))
                {
                    var descField = type.GetField("description", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (descField != null)
                        description = descField.GetValue(tooltipData) as string;
                }

                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(description))
                {
                    return $"{BattleAccessibility.StripRichTextTags(title)}: {BattleAccessibility.StripRichTextTags(description)}";
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Extract status effect tooltips from a card effect's paramStatusEffects
        /// </summary>
        private void ExtractStatusEffectTooltips(object effect, List<string> tooltips)
        {
            if (effect == null) return;

            try
            {
                var effectType = effect.GetType();

                // Get paramStatusEffects field
                var statusField = effectType.GetField("paramStatusEffects", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (statusField != null)
                {
                    var statusEffects = statusField.GetValue(effect) as System.Collections.IList;
                    if (statusEffects != null)
                    {
                        foreach (var statusEffect in statusEffects)
                        {
                            string tooltip = GetStatusEffectTooltip(statusEffect);
                            if (!string.IsNullOrEmpty(tooltip) && !tooltips.Contains(tooltip))
                                tooltips.Add(tooltip);
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Get tooltip for a status effect stack/application
        /// </summary>
        private string GetStatusEffectTooltip(object statusEffectParam)
        {
            if (statusEffectParam == null) return null;

            try
            {
                var type = statusEffectParam.GetType();

                // Get the statusId
                string statusId = null;
                var statusIdField = type.GetField("statusId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (statusIdField != null)
                {
                    statusId = statusIdField.GetValue(statusEffectParam) as string;
                }

                if (!string.IsNullOrEmpty(statusId))
                {
                    // Look up the status effect data
                    return GetStatusEffectDefinition(statusId);
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get the definition of a status effect by its ID
        /// </summary>
        private string GetStatusEffectDefinition(string statusId)
        {
            if (string.IsNullOrEmpty(statusId)) return null;

            try
            {
                // Try to get from StatusEffectManager
                var managerType = GetTypeFromAssemblies("StatusEffectManager");
                if (managerType != null)
                {
                    var instanceProp = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProp != null)
                    {
                        var manager = instanceProp.GetValue(null);
                        if (manager != null)
                        {
                            var getAllMethod = managerType.GetMethod("GetAllStatusEffectsData", Type.EmptyTypes);
                            if (getAllMethod != null)
                            {
                                var allData = getAllMethod.Invoke(manager, null);
                                if (allData != null)
                                {
                                    var getDataMethod = allData.GetType().GetMethod("GetStatusEffectData", Type.EmptyTypes);
                                    if (getDataMethod != null)
                                    {
                                        var dataList = getDataMethod.Invoke(allData, null) as System.Collections.IList;
                                        if (dataList != null)
                                        {
                                            foreach (var data in dataList)
                                            {
                                                var dataType = data.GetType();
                                                var getIdMethod = dataType.GetMethod("GetStatusId", Type.EmptyTypes);
                                                if (getIdMethod != null)
                                                {
                                                    string id = getIdMethod.Invoke(data, null) as string;
                                                    if (id == statusId)
                                                    {
                                                        // Found it - get name and description
                                                        return GetStatusEffectNameAndDescription(data, dataType);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Fallback: try localization directly
                string locKey = GetStatusEffectLocKey(statusId);
                string name = LocalizeKey($"{locKey}_CardText");
                string desc = LocalizeKey($"{locKey}_CardTooltipText");

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(desc) && name != $"{locKey}_CardText")
                {
                    return $"{BattleAccessibility.StripRichTextTags(name)}: {BattleAccessibility.StripRichTextTags(desc)}";
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get name and description from StatusEffectData
        /// </summary>
        private string GetStatusEffectNameAndDescription(object statusData, Type dataType)
        {
            try
            {
                string name = null;
                string description = null;

                // Try GetDisplayName or similar
                var getNameMethod = dataType.GetMethod("GetDisplayName", Type.EmptyTypes)
                                 ?? dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    name = getNameMethod.Invoke(statusData, null) as string;
                }

                // Try GetDescription
                var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes)
                                 ?? dataType.GetMethod("GetTooltipText", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    description = getDescMethod.Invoke(statusData, null) as string;
                }

                // Fallback to localization
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(description))
                {
                    var getIdMethod = dataType.GetMethod("GetStatusId", Type.EmptyTypes);
                    if (getIdMethod != null)
                    {
                        string statusId = getIdMethod.Invoke(statusData, null) as string;
                        string locKey = GetStatusEffectLocKey(statusId);

                        if (string.IsNullOrEmpty(name))
                            name = LocalizeKey($"{locKey}_CardText");
                        if (string.IsNullOrEmpty(description))
                            description = LocalizeKey($"{locKey}_CardTooltipText");
                    }
                }

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(description))
                {
                    return $"{BattleAccessibility.StripRichTextTags(name)}: {BattleAccessibility.StripRichTextTags(description)}";
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get localization key prefix for a status effect ID
        /// </summary>
        private string GetStatusEffectLocKey(string statusId)
        {
            if (string.IsNullOrEmpty(statusId)) return null;

            // Standard format: StatusEffect_[StatusId] with first letter capitalized
            if (statusId.Length == 1)
                return "StatusEffect_" + char.ToUpper(statusId[0]);
            else if (statusId.Length > 1)
                return "StatusEffect_" + char.ToUpper(statusId[0]) + statusId.Substring(1);

            return null;
        }

        /// <summary>
        /// Extract trait tooltips from CardTraitData
        /// </summary>
        private void ExtractTraitTooltip(object trait, List<string> tooltips)
        {
            if (trait == null) return;

            try
            {
                var traitType = trait.GetType();

                // Get trait name
                var getNameMethod = traitType.GetMethod("GetTraitStateName", Type.EmptyTypes)
                                 ?? traitType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    string traitName = getNameMethod.Invoke(trait, null) as string;
                    if (!string.IsNullOrEmpty(traitName))
                    {
                        // Look up trait definition
                        string tooltip = GetTraitDefinition(traitName);
                        if (!string.IsNullOrEmpty(tooltip) && !tooltips.Contains(tooltip))
                            tooltips.Add(tooltip);
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Get tooltip definition for a card trait
        /// </summary>
        private string GetTraitDefinition(string traitName)
        {
            // Common card traits with their definitions
            var traitDefinitions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "permafrost", "Permafrost: Card remains in hand when drawn" },
                { "frozen", "Frozen: Cannot be played until unfrozen" },
                { "consume", "Consume: Removed from deck after playing" },
                { "holdover", "Holdover: Returns to hand at end of turn" },
                { "purge", "Purge: Removed from deck permanently" },
                { "exhaust", "Exhaust: Removed from deck for this battle" },
                { "intrinsic", "Intrinsic: Always drawn on first turn" },
                { "etch", "Etch: Permanently upgrade this card" }
            };

            if (traitDefinitions.TryGetValue(traitName, out string definition))
            {
                return definition;
            }

            // Try localization
            string key = $"CardTrait_{traitName}_Tooltip";
            string localized = LocalizeKey(key);
            if (!string.IsNullOrEmpty(localized) && localized != key)
            {
                return $"{FormatTraitName(traitName)}: {BattleAccessibility.StripRichTextTags(localized)}";
            }

            return null;
        }

        /// <summary>
        /// Format a trait name for display
        /// </summary>
        private string FormatTraitName(string traitName)
        {
            if (string.IsNullOrEmpty(traitName)) return traitName;

            // Remove "State" suffix and format
            traitName = traitName.Replace("State", "");
            return System.Text.RegularExpressions.Regex.Replace(traitName, "([a-z])([A-Z])", "$1 $2");
        }

        /// <summary>
        /// Get card description for keyword parsing
        /// </summary>
        private string GetCardDescriptionForKeywordParsing(object cardState, object cardData)
        {
            string desc = null;

            try
            {
                if (cardState != null)
                {
                    var type = cardState.GetType();
                    var getCardTextMethod = type.GetMethod("GetCardText", Type.EmptyTypes);
                    if (getCardTextMethod != null)
                    {
                        desc = getCardTextMethod.Invoke(cardState, null) as string;
                    }
                }

                if (string.IsNullOrEmpty(desc) && cardData != null)
                {
                    var dataType = cardData.GetType();
                    var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes);
                    if (getDescMethod != null)
                    {
                        desc = getDescMethod.Invoke(cardData, null) as string;
                    }
                }
            }
            catch { }

            return desc;
        }

        /// <summary>
        /// Extract keywords from card description text and look up their definitions
        /// </summary>
        private void ExtractKeywordsFromDescription(string description, List<string> tooltips)
        {
            if (string.IsNullOrEmpty(description)) return;

            // Known keywords to look for (case-insensitive)
            var knownKeywords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Trigger abilities
                { "Slay", "Slay: Triggers after dealing a killing blow" },
                { "Revenge", "Revenge: Triggers when this unit takes damage" },
                { "Strike", "Strike: Triggers when this unit attacks" },
                { "Extinguish", "Extinguish: Triggers when this unit dies" },
                { "Summon", "Summon: Triggers when this unit is played" },
                { "Incant", "Incant: Triggers when you play a spell on this floor" },
                { "Resolve", "Resolve: Triggers after combat" },
                { "Rally", "Rally: Triggers when you play a non-Morsel unit on this floor" },
                { "Harvest", "Harvest: Triggers when any unit on this floor dies" },
                { "Gorge", "Gorge: Triggers when this unit eats a Morsel" },
                { "Inspire", "Inspire: Triggers when gaining Echo on this floor" },
                { "Rejuvenate", "Rejuvenate: Triggers when healed, even at full health" },
                { "Action", "Action: Triggers at start of this unit's turn" },
                { "Hatch", "Hatch: Unit dies and triggers hatching ability" },
                { "Hunger", "Hunger: Triggers when an Eaten unit is summoned" },
                { "Armored", "Armored: Triggers when Armor is added" },
                // Buffs
                { "Armor", "Armor: Blocks damage before health, each point blocks one damage" },
                { "Rage", "Rage: +2 Attack per stack, decreases every turn" },
                { "Regen", "Regen: Restores 1 health per stack at end of turn" },
                { "Damage Shield", "Damage Shield: Nullifies the next source of damage" },
                { "Lifesteal", "Lifesteal: Heals for damage dealt when attacking" },
                { "Spikes", "Spikes: Attackers take 1 damage per stack" },
                { "Stealth", "Stealth: Not targeted in combat, loses 1 stack per turn" },
                { "Spell Shield", "Spell Shield: Absorbs the next damage spell" },
                { "Spellshield", "Spellshield: Absorbs the next damage spell" },
                { "Soul", "Soul: Powers Devourer of Death's Extinguish ability" },
                // Debuffs
                { "Frostbite", "Frostbite: Takes 1 damage per stack at end of turn" },
                { "Sap", "Sap: -2 Attack per stack, decreases every turn" },
                { "Dazed", "Dazed: Cannot attack or use Action/Resolve abilities" },
                { "Rooted", "Rooted: Prevents the next floor movement" },
                { "Emberdrain", "Emberdrain: Lose Ember at turn start, decreases each turn" },
                { "Heartless", "Heartless: Cannot be healed" },
                { "Melee Weakness", "Melee Weakness: Takes extra damage from next melee attack" },
                { "Spell Weakness", "Spell Weakness: Takes extra damage from next spell" },
                { "Reap", "Reap: Takes 1 damage per stack of Echo after combat" },
                // Unit effects
                { "Quick", "Quick: Attacks before enemy units" },
                { "Multistrike", "Multistrike: Attacks an additional time each turn" },
                { "Sweep", "Sweep: Attacks all enemy units" },
                { "Trample", "Trample: Excess damage hits the next enemy" },
                { "Burnout", "Burnout: Dies when counter reaches 0" },
                { "Endless", "Endless: Returns card to top of draw pile when killed" },
                { "Fragile", "Fragile: Dies if it loses any health" },
                { "Immobile", "Immobile: Cannot move between floors" },
                { "Inert", "Inert: Cannot attack unless it has Fuel" },
                { "Fuel", "Fuel: Allows Inert units to attack, loses 1 per turn" },
                { "Phased", "Phased: Cannot attack or be damaged/targeted" },
                { "Relentless", "Relentless: Combat continues until all enemies defeated, cannot be rooted" },
                { "Haste", "Haste: Moves directly from first to third floor" },
                { "Cardless", "Cardless: Not from a card, won't go to Consume pile" },
                { "Buffet", "Buffet: Can be eaten multiple times" },
                { "Shell", "Shell: Consumes Echo to remove stacks, triggers Hatch when depleted" },
                { "Silence", "Silence: Disables triggered abilities" },
                { "Silenced", "Silenced: Triggered abilities are disabled" },
                { "Purify", "Purify: Removes all debuffs at end of turn" },
                { "Enchant", "Enchant: Other friendly units on floor gain a bonus" },
                { "Shard", "Shard: Powers Solgard the Martyr's abilities" },
                { "Eaten", "Eaten: Will be eaten by front unit after combat" },
                // Card effects
                { "Consume", "Consume: Can only be played once per battle" },
                { "Doublestack", "Doublestack: Status effect stacks added by this card are doubled" },
                { "Frozen", "Frozen: Not discarded at end of turn" },
                { "Permafrost", "Permafrost: Gains Frozen when drawn" },
                { "Purge", "Purge: Removed from deck for the rest of the run" },
                { "Intrinsic", "Intrinsic: Starts in your opening hand" },
                { "Holdover", "Holdover: Returns to hand at end of turn" },
                { "Etch", "Etch: Permanently upgrade this card when consumed" },
                { "Offering", "Offering: Played automatically if discarded" },
                { "Reserve", "Reserve: Triggers if card remains in hand at end of turn" },
                { "Pyrebound", "Pyrebound: Only playable in Pyre Room or floor below" },
                { "Piercing", "Piercing: Damage ignores Armor and shields" },
                { "Magic Power", "Magic Power: Boosts spell damage and healing" },
                { "Attuned", "Attuned: Multiplies Magic Power effects by 5" },
                { "Infused", "Infused: Floor gains 1 Echo when played" },
                { "Extract", "Extract: Removes charged echoes when played" },
                { "Spellchain", "Spellchain: Creates a copy with +1 cost and Purge" },
                { "X Cost", "X Cost: Spends all remaining Ember, effect scales with amount" },
                { "Unplayable", "Unplayable: This card cannot be played" },
                // Unit actions
                { "Ascend", "Ascend: Move up a floor to the back" },
                { "Descend", "Descend: Move down a floor to the back" },
                { "Reform", "Reform: Return a defeated friendly unit to hand" },
                { "Sacrifice", "Sacrifice: Kill a friendly unit to play this card" },
                { "Cultivate", "Cultivate: Increase stats of lowest health friendly unit" },
                // Enemy effects
                { "Recover", "Recover: Restores health to friendly units after combat" }
            };

            foreach (var keyword in knownKeywords)
            {
                // Check if keyword appears in description (as whole word)
                if (System.Text.RegularExpressions.Regex.IsMatch(description,
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(keyword.Key)}\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    if (!tooltips.Contains(keyword.Value))
                    {
                        tooltips.Add(keyword.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Localize a string if it looks like a localization key
        /// </summary>
        private string LocalizeIfNeeded(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // If it looks like a localization key, try to localize
            if (text.Contains("_") && !text.Contains(" "))
            {
                string localized = LocalizeKey(text);
                if (!string.IsNullOrEmpty(localized) && localized != text)
                    return localized;
            }

            return text;
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
                string cleanedName = CleanGameObjectName(go.name);
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
            string text = GetTMPText(go);
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

                        string sibText = GetTMPTextDirect(sibling.gameObject);
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
                        string cleaned = CleanGameObjectName(parentName);
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
        /// Get text for logbook/compendium items
        /// </summary>
        private string GetLogbookItemText(GameObject go)
        {
            try
            {
                // Check if this or a parent is part of the compendium
                if (!IsInCompendiumContext(go))
                    return null;

                // Look for count labels (format like "25/250" or "X/Y")
                string countText = FindCountLabelText(go);
                string itemName = GetItemNameFromHierarchy(go);

                if (!string.IsNullOrEmpty(countText) && !string.IsNullOrEmpty(itemName))
                {
                    return $"{itemName}: {countText}";
                }
                else if (!string.IsNullOrEmpty(countText))
                {
                    // Try to make the count more readable
                    return FormatCountText(countText);
                }
                else if (!string.IsNullOrEmpty(itemName))
                {
                    return itemName;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Check if we're in a compendium/logbook context
        /// </summary>
        private bool IsInCompendiumContext(GameObject go)
        {
            Transform current = go.transform;
            while (current != null)
            {
                string name = current.name.ToLower();
                if (name.Contains("compendium") || name.Contains("logbook") ||
                    name.Contains("collection") || name.Contains("cardlist"))
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Find count label text (X/Y format) in the hierarchy
        /// </summary>
        private string FindCountLabelText(GameObject go)
        {
            try
            {
                Transform parent = go.transform.parent;
                if (parent == null) return null;

                // Collect all text from siblings to find count patterns
                var allTexts = new List<string>();
                foreach (Transform sibling in parent)
                {
                    string text = GetTMPTextDirect(sibling.gameObject);
                    if (!string.IsNullOrEmpty(text))
                        allTexts.Add(text.Trim());

                    var uiText = sibling.GetComponent<Text>();
                    if (uiText != null && !string.IsNullOrEmpty(uiText.text))
                        allTexts.Add(uiText.text.Trim());

                    // Also check children
                    foreach (Transform child in sibling)
                    {
                        string childText = GetTMPTextDirect(child.gameObject);
                        if (!string.IsNullOrEmpty(childText))
                            allTexts.Add(childText.Trim());
                    }
                }

                // Look for X/Y pattern
                foreach (var text in allTexts)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d+/\d+$"))
                    {
                        return text;
                    }
                }

                // Look for separate number that could be part of count
                string number = null;
                string total = null;
                foreach (var text in allTexts)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d+$"))
                    {
                        if (number == null)
                            number = text;
                        else if (total == null)
                            total = text;
                    }
                }

                if (number != null && total != null)
                {
                    return $"{number}/{total}";
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get item name from hierarchy for logbook items
        /// </summary>
        private string GetItemNameFromHierarchy(GameObject go)
        {
            try
            {
                // Look for title/name in the hierarchy
                Transform current = go.transform;
                for (int i = 0; i < 3 && current != null; i++)
                {
                    if (current.parent != null)
                    {
                        foreach (Transform sibling in current.parent)
                        {
                            string sibName = sibling.name.ToLower();
                            if (sibName.Contains("title") || sibName.Contains("name") ||
                                sibName.Contains("label") || sibName.Contains("header"))
                            {
                                string text = GetTMPTextDirect(sibling.gameObject);
                                if (string.IsNullOrEmpty(text))
                                {
                                    var uiText = sibling.GetComponent<Text>();
                                    text = uiText?.text;
                                }
                                if (!string.IsNullOrEmpty(text) && text.Length > 2)
                                {
                                    // Make sure it's not a number
                                    if (!System.Text.RegularExpressions.Regex.IsMatch(text.Trim(), @"^\d+$"))
                                    {
                                        return text.Trim();
                                    }
                                }
                            }
                        }
                    }
                    current = current.parent;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Format count text to be more readable
        /// </summary>
        private string FormatCountText(string countText)
        {
            if (string.IsNullOrEmpty(countText))
                return null;

            // If it's already in X/Y format, make it more readable
            var match = System.Text.RegularExpressions.Regex.Match(countText, @"^(\d+)/(\d+)$");
            if (match.Success)
            {
                return $"{match.Groups[1].Value} of {match.Groups[2].Value} discovered";
            }

            return countText;
        }

        /// <summary>
        /// Get text for clan selection icons (ClassSelectionIcon component)
        /// </summary>
        private string GetClanSelectionText(GameObject go)
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
                            clanName = LocalizeString(titleLoc);
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
                        result.Append(" (Locked)");
                    }
                }
                else
                {
                    result.Append(isLocked ? "Locked clan" : "Unknown clan");
                }

                if (!string.IsNullOrEmpty(clanDescription))
                {
                    result.Append(". ");
                    result.Append(BattleAccessibility.StripRichTextTags(clanDescription));
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting clan selection text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get text for champion choice buttons on the clan selection screen
        /// </summary>
        private string GetChampionChoiceText(GameObject go)
        {
            try
            {
                // Look for ChampionChoiceButton component
                Component championButton = null;
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component != null && component.GetType().Name == "ChampionChoiceButton")
                    {
                        championButton = component;
                        break;
                    }
                }

                if (championButton == null)
                    return null;

                var buttonType = championButton.GetType();

                // Check for locked tooltip key - if this exists and has a value, the champion is locked
                string lockedTooltipKey = null;
                var lockedKeyField = buttonType.GetField("lockedTooltipTitleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (lockedKeyField != null)
                {
                    lockedTooltipKey = lockedKeyField.GetValue(championButton) as string;
                }

                // Try to find ChampionSelectionUI in parent to get champion data
                Component championSelectionUI = null;
                Transform current = go.transform.parent;
                while (current != null && championSelectionUI == null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component != null && component.GetType().Name == "ChampionSelectionUI")
                        {
                            championSelectionUI = component;
                            break;
                        }
                    }
                    current = current.parent;
                }

                object championData = null;
                bool isLocked = false;

                if (championSelectionUI != null)
                {
                    var uiType = championSelectionUI.GetType();

                    // Check if locked
                    var lockedField = uiType.GetField("locked", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (lockedField != null)
                    {
                        isLocked = (bool)lockedField.GetValue(championSelectionUI);
                    }

                    // Get classData from ChampionSelectionUI
                    var classDataField = uiType.GetField("classData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (classDataField != null)
                    {
                        var classData = classDataField.GetValue(championSelectionUI);
                        if (classData != null)
                        {
                            var classDataType = classData.GetType();

                            // Get champions array from classData
                            var championsField = classDataType.GetField("champions", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (championsField != null)
                            {
                                var champions = championsField.GetValue(classData) as System.Collections.IList;
                                if (champions != null && champions.Count > 0)
                                {
                                    // Get button index from name (e.g., "Champion choice button 1" -> index 0)
                                    int buttonIndex = 0;
                                    string buttonName = go.name;
                                    if (buttonName.Contains("1")) buttonIndex = 0;
                                    else if (buttonName.Contains("2")) buttonIndex = 1;

                                    if (buttonIndex < champions.Count)
                                    {
                                        championData = champions[buttonIndex];
                                        MonsterTrainAccessibility.LogInfo($"Found champion at index {buttonIndex}, type: {championData?.GetType().Name}");
                                    }
                                }
                            }
                        }
                    }
                }

                // Try to find tooltip on this object
                string tooltipText = GetTooltipText(go);

                // If we have champion data, try to get the name
                if (championData != null)
                {
                    var champDataType = championData.GetType();

                    // Log methods for debugging
                    var champMethods = champDataType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object))
                        .Select(m => m.Name).Distinct().Take(15);
                    MonsterTrainAccessibility.LogInfo($"ChampionData methods: {string.Join(", ", champMethods)}");

                    // Try GetTitle or GetName methods
                    string championName = null;
                    var getTitleMethod = champDataType.GetMethod("GetTitle", Type.EmptyTypes);
                    if (getTitleMethod != null)
                    {
                        championName = getTitleMethod.Invoke(championData, null) as string;
                    }

                    if (string.IsNullOrEmpty(championName))
                    {
                        var getNameMethod = champDataType.GetMethod("GetName", Type.EmptyTypes);
                        if (getNameMethod != null)
                        {
                            championName = getNameMethod.Invoke(championData, null) as string;
                        }
                    }

                    if (!string.IsNullOrEmpty(championName))
                    {
                        MonsterTrainAccessibility.LogInfo($"Champion name: {championName}, locked: {isLocked}");
                        string result = "Champion: " + championName;
                        if (isLocked)
                            result += " (Locked)";
                        return result;
                    }
                }

                // Use tooltip if available
                if (!string.IsNullOrEmpty(tooltipText))
                {
                    string result = "Champion: " + tooltipText;
                    if (isLocked)
                        result += " (Locked)";
                    return result;
                }

                // Check if locked via tooltip key or isLocked flag
                if (isLocked || !string.IsNullOrEmpty(lockedTooltipKey))
                {
                    if (!string.IsNullOrEmpty(lockedTooltipKey))
                    {
                        string lockedText = LocalizeString(lockedTooltipKey);
                        if (!string.IsNullOrEmpty(lockedText) && lockedText != lockedTooltipKey)
                        {
                            return "Champion: " + lockedText;
                        }
                    }
                    return "Champion: Locked. Win a run to unlock.";
                }

                return "Champion option";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting champion choice text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get text for buttons with LocalizedTooltipProvider (mutator options, challenges, etc.)
        /// </summary>
        private string GetLocalizedTooltipButtonText(GameObject go)
        {
            try
            {
                // Check if this button has LocalizedTooltipProvider
                Component tooltipProvider = null;
                bool hasButtonToggle = false;

                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    string typeName = component.GetType().Name;

                    if (typeName == "LocalizedTooltipProvider")
                    {
                        tooltipProvider = component;
                    }
                    if (typeName == "ButtonStateBehaviourToggle")
                    {
                        hasButtonToggle = true;
                    }
                }

                // Only handle if we have LocalizedTooltipProvider
                if (tooltipProvider == null)
                    return null;

                var type = tooltipProvider.GetType();

                // Log fields for debugging
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MonsterTrainAccessibility.LogInfo($"LocalizedTooltipProvider fields: {string.Join(", ", fields.Select(f => f.Name))}");

                // Try to get the tooltip title
                string tooltipTitle = null;
                string tooltipBody = null;

                // Try various field names for title
                var titleFieldNames = new[] { "titleKey", "_titleKey", "tooltipTitleKey", "title", "_title" };
                foreach (var fieldName in titleFieldNames)
                {
                    var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        string titleKey = field.GetValue(tooltipProvider) as string;
                        if (!string.IsNullOrEmpty(titleKey))
                        {
                            tooltipTitle = LocalizeString(titleKey);
                            MonsterTrainAccessibility.LogInfo($"Found tooltip title key: {titleKey} -> {tooltipTitle}");
                            break;
                        }
                    }
                }

                // Try various field names for body
                var bodyFieldNames = new[] { "bodyKey", "_bodyKey", "tooltipBodyKey", "body", "_body", "descriptionKey" };
                foreach (var fieldName in bodyFieldNames)
                {
                    var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        string bodyKey = field.GetValue(tooltipProvider) as string;
                        if (!string.IsNullOrEmpty(bodyKey))
                        {
                            tooltipBody = LocalizeString(bodyKey);
                            MonsterTrainAccessibility.LogInfo($"Found tooltip body key: {bodyKey} -> {tooltipBody}");
                            break;
                        }
                    }
                }

                // Build result from button name and tooltip
                var result = new StringBuilder();

                // Use clean button name
                string buttonName = CleanGameObjectName(go.name);
                if (!string.IsNullOrEmpty(buttonName))
                {
                    result.Append(buttonName);
                }

                // Add tooltip title if different from button name
                if (!string.IsNullOrEmpty(tooltipTitle) && tooltipTitle != buttonName)
                {
                    if (result.Length > 0)
                        result.Append(": ");
                    result.Append(tooltipTitle);
                }

                // Add tooltip body
                if (!string.IsNullOrEmpty(tooltipBody))
                {
                    if (result.Length > 0)
                        result.Append(". ");
                    result.Append(BattleAccessibility.StripRichTextTags(tooltipBody));
                }

                // Check if button shows locked state
                if (hasButtonToggle)
                {
                    // Check interactable state
                    var button = go.GetComponent<Button>();
                    if (button != null && !button.interactable)
                    {
                        result.Append(" (Locked)");
                    }
                }

                if (result.Length > 0)
                    return result.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting localized tooltip button text: {ex.Message}");
            }
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
                        return GetAllTextFromTransform(scrollRect.content);
                    }

                    // Also check siblings
                    foreach (Transform sibling in parent)
                    {
                        var siblingScrollRect = sibling.GetComponent<ScrollRect>();
                        if (siblingScrollRect != null && siblingScrollRect.content != null)
                        {
                            return GetAllTextFromTransform(siblingScrollRect.content);
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
        /// Get all text from a transform and its children
        /// </summary>
        private string GetAllTextFromTransform(Transform root)
        {
            var sb = new StringBuilder();
            CollectAllText(root, sb);
            return sb.ToString().Trim();
        }

        /// <summary>
        /// Recursively collect text from all Text and TMP components
        /// </summary>
        private void CollectAllText(Transform transform, StringBuilder sb)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy) return;

            // Get Text component on this object
            var uiText = transform.GetComponent<Text>();
            if (uiText != null && !string.IsNullOrEmpty(uiText.text))
            {
                string cleaned = uiText.text.Trim();
                if (!string.IsNullOrEmpty(cleaned))
                {
                    sb.AppendLine(cleaned);
                }
            }

            // Get TMP text
            string tmpText = GetTMPTextDirect(transform.gameObject);
            if (!string.IsNullOrEmpty(tmpText))
            {
                string cleaned = tmpText.Trim();
                if (!string.IsNullOrEmpty(cleaned))
                {
                    sb.AppendLine(cleaned);
                }
            }

            // Recurse into children
            foreach (Transform child in transform)
            {
                CollectAllText(child, sb);
            }
        }

        /// <summary>
        /// Get TMP text from a specific GameObject (not children)
        /// </summary>
        private string GetTMPTextDirect(GameObject go)
        {
            try
            {
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                    {
                        var textProperty = type.GetProperty("text");
                        if (textProperty != null)
                        {
                            return textProperty.GetValue(component) as string;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Clean up GameObject name to be more readable
        /// </summary>
        private string CleanGameObjectName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            name = name.Replace("(Clone)", "");
            name = name.Replace("Button", "");
            name = name.Replace("Btn", "");
            name = name.Trim();

            // Add spaces before capital letters (CamelCase to words)
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");

            return name;
        }

        /// <summary>
        /// Try to get TextMeshPro text using reflection
        /// </summary>
        private string GetTMPText(GameObject go)
        {
            try
            {
                // Look for TMP_Text component (base class for both TextMeshProUGUI and TextMeshPro)
                var components = go.GetComponentsInChildren<Component>();
                foreach (var component in components)
                {
                    if (component == null)
                        continue;

                    var type = component.GetType();

                    // Check if it's a TextMeshPro type
                    if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                    {
                        var textProperty = type.GetProperty("text");
                        if (textProperty != null)
                        {
                            string text = textProperty.GetValue(component) as string;
                            if (!string.IsNullOrEmpty(text))
                            {
                                return text;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting TMP text: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Read all visible text on the current screen (press T)
        /// </summary>
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
                        string cleanText = textComp.text.Trim();
                        if (!string.IsNullOrEmpty(cleanText) && !collectedTexts.Contains(cleanText))
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
                        string cleanText = text.Trim();
                        if (!string.IsNullOrEmpty(cleanText) && !collectedTexts.Contains(cleanText))
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
                string cleaned = uiText.text.Trim();
                if (!string.IsNullOrEmpty(cleaned) && !collected.Contains(cleaned))
                {
                    collected.Add(cleaned);
                    sb.AppendLine(cleaned);
                }
            }

            // Get TMP text
            string tmpText = GetTMPTextDirect(transform.gameObject);
            if (!string.IsNullOrEmpty(tmpText))
            {
                string cleaned = tmpText.Trim();
                if (!string.IsNullOrEmpty(cleaned) && !collected.Contains(cleaned))
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
            string text = GetTMPTextDirect(transform.gameObject);
            if (!string.IsNullOrEmpty(text))
            {
                text = BattleAccessibility.StripRichTextTags(text.Trim());

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
                                text = BattleAccessibility.StripRichTextTags(text.Trim());
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
