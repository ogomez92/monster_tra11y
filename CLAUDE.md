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

### Core Components (MonsterTrainAccessibility/Core/)

- **ScreenReaderOutput**: Wrapper for Tolk library - handles speech output, braille, and screen reader detection. All accessibility output goes through this.
- **InputInterceptor**: Unity MonoBehaviour that handles accessibility hotkeys (C, T, H, F, E, R, V). Navigation is handled by the game's native EventSystem.
- **AccessibilityConfig**: BepInEx configuration - verbosity levels, keybindings, announcement settings.

### Screen Handlers (MonsterTrainAccessibility/Screens/)

- **MenuAccessibility**: MonoBehaviour that polls `EventSystem.current.currentSelectedGameObject` and reads text from selected UI elements. Handles all menu screens, card drafts, map, shop, events. Has `ReadAllScreenText()` for reading patch notes and long text areas.
- **BattleAccessibility**: Uses reflection to access game managers (`CardManager`, `SaveManager`, `RoomManager`, `PlayerManager`) and read actual game state for hand, floors, units, resources.
- **CardDraftAccessibility**: Announces screen transitions (simplified - UI handled by MenuAccessibility).
- **MapAccessibility**: Announces screen transitions (simplified - UI handled by MenuAccessibility).

### Hotkeys

| Key | Action |
|-----|--------|
| C | Re-read current focused item |
| T | Read all text on screen |
| H | Read hand (battle) |
| F | Read floors (battle) |
| E | Read enemies (battle) |
| R | Read resources (battle) |
| V | Cycle verbosity |

### Harmony Patches (MonsterTrainAccessibility/Patches/)

Manual patches (no `[HarmonyPatch]` attributes - use `TryPatch()` methods):
- **ScreenTransitionPatches**: Hooks screen changes to announce transitions
- **CombatEventPatches**: Turn changes, damage, deaths, status effects
- **CardEventPatches**: Draw, play, discard events

Patches use runtime reflection to find game methods - see `PATCH_TARGETS.md` for verified targets.

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
2. Check `BepInEx/LogOutput.log` for errors
3. Verify screen reader announcements with NVDA running

## Key Game Types

```csharp
Team.Type.Monsters  // Player's units
Team.Type.Heroes    // Enemy units (confusing naming)
CardType.Monster / CardType.Spell / CardType.Blight
```

Localization: use `string.Localize()` extension method.
