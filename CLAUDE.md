# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Monster Train Accessibility Mod - a BepInEx plugin that enables blind players to play Monster Train through screen reader integration (Tolk library) and keyboard navigation.

## Build Commands

```bash
# Build the mod (auto-copies to game plugins folder)
cd MonsterTrainAccessibility
dotnet build -c Release

# Output locations:
# - bin/Release/MonsterTrainAccessibility.dll
# - C:\Program Files (x86)\Steam\steamapps\common\Monster Train\BepInEx\plugins\MonsterTrainAccessibility.dll
```

The csproj automatically copies the built DLL to the game's plugins folder after build.

## Architecture

All game data access uses **runtime reflection** since there's no public API. Game types are discovered at runtime and methods are cached for performance.

### Core Components (MonsterTrainAccessibility/Core/)

- **ScreenReaderOutput**: Wrapper for Tolk library - handles speech output, braille, and screen reader detection. All accessibility output goes through this.
  - **IMPORTANT: Never use `interrupt = true`** - it cuts off previous announcements. Always use `Speak(text, false)` or just `Speak(text)`.
- **InputInterceptor**: Unity MonoBehaviour that handles accessibility hotkeys (F1, C, T, H, L, N, R, V, 1-9). Navigation is handled by the game's native EventSystem.
- **AccessibilityConfig**: BepInEx configuration - verbosity levels, keybindings, announcement settings.

### Help System (MonsterTrainAccessibility/Help/)

Context-sensitive help that announces available keys based on current game screen.

- **IHelpContext**: Interface for help providers - each screen/mode implements this
- **HelpSystem**: Coordinator that selects the active context and speaks help text
- **ScreenStateTracker**: Static enum tracking current game screen (MainMenu, Battle, etc.)
- **Contexts/**: Individual help providers (higher priority wins):
  - `GlobalHelp` (priority 0): Fallback for any screen
  - `MainMenuHelp` (40): Main menu navigation
  - `ClanSelectionHelp` (50): Clan/class selection
  - `MapHelp` (60): Map navigation
  - `ShopHelp` (70): Shop purchases
  - `EventHelp` (70): Event choices
  - `CardDraftHelp` (80): Card draft selection
  - `BattleIntroHelp` (85): Pre-battle screen
  - `BattleHelp` (90): Battle information keys
  - `TutorialHelp` (95): Tutorial popups
  - `BattleTargetingHelp` (100): Floor targeting mode

### Battle Systems (MonsterTrainAccessibility/Battle/)

- **FloorTargetingSystem**: Keyboard-based floor selection for playing cards. When a card requires floor placement, allows 1/2/3 keys or arrows to select floor, Enter to confirm, Escape to cancel.

### Screen Handlers (MonsterTrainAccessibility/Screens/)

- **MenuAccessibility**: MonoBehaviour that polls `EventSystem.current.currentSelectedGameObject` and reads text from selected UI elements. Handles all menu screens, card drafts, map, shop, events. Key methods:
  - `GetTextFromGameObject()`: Main entry point for extracting readable text from UI elements
  - `GetCardUIText()`: Extracts full card details (name, type, cost, description) from CardUI components
  - `ReadAllScreenText()`: For reading patch notes and long text areas
- **BattleAccessibility**: Uses reflection to access game managers (`CardManager`, `SaveManager`, `RoomManager`, `PlayerManager`) and read actual game state for hand, floors, units, resources.
- **CardDraftAccessibility**: Announces screen transitions (simplified - UI handled by MenuAccessibility).
- **MapAccessibility**: Announces screen transitions (simplified - UI handled by MenuAccessibility).

### Hotkeys

#### Global Keys (all screens)
| Key | Action |
|-----|--------|
| F1 | Context-sensitive help |
| C | Re-read current focused item |
| T | Read all text on screen |
| V | Cycle verbosity level |

#### Battle Keys
| Key | Action |
|-----|--------|
| H | Read hand (all cards) |
| L | Read floors (all units) - L for Levels |
| N | Read enemies (enemy positions) - N for eNemies |
| R | Read resources (ember, pyre, cards) |
| 1-9 | Select card by position in hand |

Note: F and E are avoided because they conflict with the game's native shortcuts (F = Toggle Unit Details, E = End Turn).

#### Floor Targeting Keys (when playing a card)
| Key | Action |
|-----|--------|
| 1 | Select floor 1 (bottom) |
| 2 | Select floor 2 (middle) |
| 3 | Select floor 3 (top) |
| Up/Down | Cycle between floors |
| Enter | Confirm floor selection |
| Escape | Cancel card play |

### Harmony Patches (MonsterTrainAccessibility/Patches/)

Manual patches (no `[HarmonyPatch]` attributes - use `TryPatch()` methods):
- **ScreenTransitionPatches**: Hooks screen changes to announce transitions
  - `MainMenuScreenPatch`, `BattleIntroScreenPatch`, `CombatStartPatch`
  - `CardDraftScreenPatch`, `ClassSelectionScreenPatch`, `MapScreenPatch`
  - `MerchantScreenPatch`, `EnhancerSelectionScreenPatch`, `GameOverScreenPatch`
  - `SettingsScreenPatch`: Announces "Settings. Press Tab to switch between tabs."
  - `ScreenManagerPatch`: Generic screen transition detection
- **CombatEventPatches**: Turn changes, damage, deaths, status effects
- **CardEventPatches**: Draw, play, discard events

Patches use runtime reflection to find game methods - see `PATCH_TARGETS.md` for verified targets.

### Text Extraction (MenuAccessibility.cs)

The `GetTextFromGameObject()` method tries multiple extractors in order:
1. Dialog buttons, CardUI, shop items, battle intro, map nodes
2. Toggles, logbook items, clan selection, champion choices
3. Localized tooltip buttons, branch choices
4. **`GetTextWithContext()`** - handles short button labels

**`GetTextWithContext()` logic:**
- If text is 1-2 chars (likely icon), uses cleaned GameObject name instead
- If text is 3-4 chars or empty, looks for context from hierarchy
- Falls back to direct text

**`GetContextLabelFromHierarchy()` excluded container names:**
These parent names are skipped when looking for context labels:
- container, panel, holder, group, content, root
- options, input area, input, area
- section, buttons, layout, wrapper

To fix "ParentName: X" announcements, add the parent name pattern to this exclusion list.

### Key Integration Points

- **Tolk.cs** (in ../tolk/): P/Invoke wrapper for Tolk.dll screen reader library
- **Trainworks2/**: Reference modding toolkit (not directly used, but useful for finding patch targets)

## Game Path Configuration

The csproj uses `$(MonsterTrainPath)` which defaults to Steam's common location. Override via:
- Environment variable: `MONSTER_TRAIN_PATH`
- MSBuild property: `-p:MonsterTrainPath="path"`

## Testing

No automated tests. Test by:
1. Building and launching Monster Train
2. Check log for errors: `C:\Program Files (x86)\Steam\steamapps\common\Monster Train\BepInEx\LogOutput.log`
3. Verify screen reader announcements with NVDA running

The log shows component hierarchies when UI elements are focused - useful for debugging text extraction issues.

## Key Game Types

```csharp
Team.Type.Monsters  // Player's units
Team.Type.Heroes    // Enemy units (confusing naming)
CardType.Monster / CardType.Spell / CardType.Blight
```

Localization: use `string.Localize()` extension method.
