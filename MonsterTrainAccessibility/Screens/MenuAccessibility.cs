using MonsterTrainAccessibility.Core;
using System;
using System.Collections.Generic;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Handles accessibility for main menu and settings screens
    /// </summary>
    public class MenuAccessibility
    {
        private ListFocusContext _mainMenuContext;
        private ListFocusContext _settingsContext;
        private ListFocusContext _clanSelectContext;

        public MenuAccessibility()
        {
            // Contexts will be created when screens are entered
        }

        /// <summary>
        /// Called when the main menu screen is shown
        /// </summary>
        public void OnMainMenuEntered(object screen)
        {
            MonsterTrainAccessibility.LogInfo("Main menu entered");

            _mainMenuContext = new ListFocusContext("Main Menu", OnMainMenuBack);

            // Add menu items - these will be populated from actual game UI
            // For now, we create placeholder items that represent typical menu options
            _mainMenuContext.AddItem(new FocusableMenuItem
            {
                Id = "new_game",
                Label = "New Game",
                Description = "Start a new run",
                OnActivate = () => ActivateMenuItem("new_game", screen)
            });

            _mainMenuContext.AddItem(new FocusableMenuItem
            {
                Id = "continue",
                Label = "Continue",
                Description = "Resume your current run",
                OnActivate = () => ActivateMenuItem("continue", screen)
            });

            _mainMenuContext.AddItem(new FocusableMenuItem
            {
                Id = "logbook",
                Label = "Logbook",
                Description = "View your collection and stats",
                OnActivate = () => ActivateMenuItem("logbook", screen)
            });

            _mainMenuContext.AddItem(new FocusableMenuItem
            {
                Id = "settings",
                Label = "Settings",
                Description = "Game options",
                OnActivate = () => ActivateMenuItem("settings", screen)
            });

            _mainMenuContext.AddItem(new FocusableMenuItem
            {
                Id = "quit",
                Label = "Quit",
                Description = "Exit the game",
                OnActivate = () => ActivateMenuItem("quit", screen)
            });

            MonsterTrainAccessibility.FocusManager.SetContext(_mainMenuContext, false);
        }

        /// <summary>
        /// Called when clan/class selection screen is entered
        /// </summary>
        public void OnClanSelectionEntered(object screen, List<ClanInfo> clans, bool isPrimary)
        {
            string contextName = isPrimary ? "Primary Clan Selection" : "Allied Clan Selection";
            MonsterTrainAccessibility.LogInfo($"{contextName} entered");

            _clanSelectContext = new ListFocusContext(contextName, () => OnClanSelectBack(screen));

            foreach (var clan in clans)
            {
                _clanSelectContext.AddItem(new FocusableMenuItem
                {
                    Id = clan.Id,
                    Label = clan.Name,
                    Description = clan.Description,
                    OnActivate = () => SelectClan(clan, screen)
                });
            }

            MonsterTrainAccessibility.FocusManager.SetContext(_clanSelectContext);

            // Announce helpful instructions
            string instructions = isPrimary
                ? "Choose your primary clan. Use Up and Down arrows to browse, Enter to select."
                : "Choose your allied clan. Use Up and Down arrows to browse, Enter to select.";

            MonsterTrainAccessibility.ScreenReader?.Queue(instructions);
        }

        /// <summary>
        /// Called when settings menu is entered
        /// </summary>
        public void OnSettingsEntered(object screen)
        {
            MonsterTrainAccessibility.LogInfo("Settings entered");

            _settingsContext = new ListFocusContext("Settings", () => OnSettingsBack(screen));

            // Settings menu items would be populated from actual settings
            _settingsContext.AddItem(new FocusableMenuItem
            {
                Id = "audio",
                Label = "Audio Settings",
                OnActivate = () => ActivateMenuItem("audio", screen)
            });

            _settingsContext.AddItem(new FocusableMenuItem
            {
                Id = "video",
                Label = "Video Settings",
                OnActivate = () => ActivateMenuItem("video", screen)
            });

            _settingsContext.AddItem(new FocusableMenuItem
            {
                Id = "gameplay",
                Label = "Gameplay Settings",
                OnActivate = () => ActivateMenuItem("gameplay", screen)
            });

            _settingsContext.AddItem(new FocusableMenuItem
            {
                Id = "accessibility",
                Label = "Accessibility Settings",
                Description = "Configure screen reader and navigation options",
                OnActivate = () => OpenAccessibilitySettings()
            });

            _settingsContext.AddItem(new FocusableMenuItem
            {
                Id = "back",
                Label = "Back",
                OnActivate = () => OnSettingsBack(screen)
            });

            MonsterTrainAccessibility.FocusManager.SetContext(_settingsContext);
        }

        /// <summary>
        /// Handle game over / results screen
        /// </summary>
        public void OnResultsScreenEntered(object screen, RunResults results)
        {
            MonsterTrainAccessibility.LogInfo("Results screen entered");

            var context = new ListFocusContext("Run Complete", () => OnResultsBack(screen));

            // Announce results
            string resultAnnouncement = results.Won
                ? $"Victory! You reached covenant {results.CovenantLevel}."
                : "Defeat. The pyre was destroyed.";

            MonsterTrainAccessibility.ScreenReader?.Speak(resultAnnouncement, true);

            // Add navigation options
            context.AddItem(new FocusableMenuItem
            {
                Id = "stats",
                Label = "View Statistics",
                Description = $"Score: {results.Score}. Floors cleared: {results.FloorsCleared}",
                OnActivate = () => ReadDetailedStats(results)
            });

            context.AddItem(new FocusableMenuItem
            {
                Id = "main_menu",
                Label = "Return to Main Menu",
                OnActivate = () => ReturnToMainMenu(screen)
            });

            MonsterTrainAccessibility.FocusManager.SetContext(context);
        }

        #region Helper Methods

        private void ActivateMenuItem(string itemId, object screen)
        {
            // This will be implemented to actually trigger the game's UI
            MonsterTrainAccessibility.LogInfo($"Activating menu item: {itemId}");

            // For now, announce the action
            MonsterTrainAccessibility.ScreenReader?.Speak($"Selected {itemId}", true);
        }

        private void SelectClan(ClanInfo clan, object screen)
        {
            MonsterTrainAccessibility.LogInfo($"Selected clan: {clan.Name}");
            MonsterTrainAccessibility.ScreenReader?.Speak($"Selected {clan.Name}", true);
            // Actual clan selection would be triggered here
        }

        private void OnMainMenuBack()
        {
            // At main menu, confirm quit
            MonsterTrainAccessibility.ScreenReader?.Speak(
                "Press Escape again to quit the game", true);
        }

        private void OnClanSelectBack(object screen)
        {
            MonsterTrainAccessibility.ScreenReader?.Speak("Going back", true);
            // Would trigger actual back navigation
        }

        private void OnSettingsBack(object screen)
        {
            MonsterTrainAccessibility.FocusManager.GoBack();
        }

        private void OnResultsBack(object screen)
        {
            ReturnToMainMenu(screen);
        }

        private void OpenAccessibilitySettings()
        {
            // Open BepInEx config or custom accessibility settings UI
            MonsterTrainAccessibility.ScreenReader?.Speak(
                "Accessibility settings can be configured in the BepInEx config file", true);
        }

        private void ReadDetailedStats(RunResults results)
        {
            string stats = $"Score: {results.Score}. " +
                          $"Floors cleared: {results.FloorsCleared}. " +
                          $"Enemies defeated: {results.EnemiesDefeated}. " +
                          $"Cards played: {results.CardsPlayed}.";

            MonsterTrainAccessibility.ScreenReader?.Speak(stats, true);
        }

        private void ReturnToMainMenu(object screen)
        {
            MonsterTrainAccessibility.ScreenReader?.Speak("Returning to main menu", true);
            // Would trigger actual navigation
        }

        #endregion
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
