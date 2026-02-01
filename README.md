# Monster Train Accessibility Mod

A comprehensive accessibility mod for Monster Train that enables totally blind players to fully enjoy the game through screen reader integration and complete keyboard navigation.

## Features

- **Full Screen Reader Support**: Works with NVDA, JAWS, Window-Eyes, and Windows Narrator (SAPI)
- **Complete Keyboard Navigation**: Navigate all game elements without a mouse
- **Battle Accessibility**: Read cards in hand, floor status, enemy info and intents
- **Menu Navigation**: Full access to main menu, settings, clan selection
- **Card Draft Support**: Browse and select cards with full descriptions
- **Map Navigation**: Choose your path through events, shops, and battles
- **Configurable Verbosity**: Choose how much detail you want announced
- **Braille Display Support**: Text sent to braille display if available

## Requirements

- Monster Train (Steam version)
- BepInEx 5.4.x mod loader
- A screen reader (NVDA recommended) or Windows Narrator
- Windows operating system

## Installation

### Step 1: Subscribe to the Monster Train Mod Loader

1. Open Steam and go to the [Monster Train Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=2187468759) on Steam Workshop
2. Click **Subscribe** to download the mod loader
3. For detailed modding instructions, see the [Official Monster Train Modding Guide](https://steamcommunity.com/sharedfiles/filedetails/?id=2257843164)

### Step 2: Enable the Mod Loader In-Game

After subscribing, you must enable the mod loader inside the game.

#### Instructions for Screen Reader Users

1. Launch Monster Train
2. Press **Enter** to skip the intro cinematic
3. You are now at the main menu (the menu does NOT wrap around)
4. Press **Down Arrow** approximately **7 times** to reach the bottom area
5. Press **Right Arrow** approximately **4 times** (this reaches the lower-right corner where "Mod Settings" is)
6. Press **Enter** to open Mod Settings
7. Press **Down Arrow** twice to reach the "Enable Mod Loader" checkbox
8. Press **Enter** to toggle it on (you can use AI with a screenshot to verify it's selected)
9. Press escape - a dialog will ask if you want to apply and quit
10. The leftmost option is selected by default (Apply & Quit) - press **Enter** to confirm
11. The game will exit automatically

### install the mod

Once you have enabled the mod loader, do the following:

1. Download te latest release of this mod
2. Extract **all contents** directly to your Monster Train game folder:
   - Default location: `C:\Program Files (x86)\Steam\steamapps\common\Monster Train`
3. The folder structure should look like:
   ```
   Monster Train/
   ├── BepInEx/
   │   ├── core/
   │   ├── plugins/    (create if not exists)
   │   └── config/     (created automatically)
   ├── MonsterTrain.exe
   ├── winhttp.dll     (BepInEx loader - must be here)
   └── doorstop_config.ini
   └── tolk.dll, NVDAControllerClient64.dll, etc
   ```
4. Launch Monster Train once to let BepInEx initialize

### Step 3: Launch the Game

1. Start your screen reader (NVDA, JAWS, or enable Windows Narrator)
2. Launch Monster Train
3. You should hear "Monster Train Accessibility loaded" when the game starts

## Keyboard Controls

### Navigation
| Key | Action |
|-----|--------|
| Arrow Keys | Navigate between items |
| Enter | Select / Activate current item |
| Space | Alternate select key |
| Escape | Go back / Cancel |

### Information Hotkeys
| Key | Action |
|-----|--------|
| F1 | Context-sensitive help (shows available keys for current screen) |
| C | Re-read current focused item |
| T | Read all text on screen (patch notes, descriptions, etc.) |
| Tab | Read train stats (pyre health, gold, deck size) |
| V | Cycle verbosity level (Minimal/Normal/Verbose) |

### Automatic Text Reading

The mod automatically reads text when:
- **Scroll views**: When you focus on a scrollable area (like patch notes), the content is read automatically
- **Dialogs/popups**: New dialogs, tooltips, and text panels are announced when they appear
- **Content changes**: If text content updates while you're focused on it, the new content is announced

### Battle Hotkeys
| Key | Action |
|-----|--------|
| H | Read all cards in hand |
| L | Read floor status (L for Levels) - floor capacity and all units |
| U | Read all units with detail (U for Units) - your monsters front-to-back, then enemies |
| R | Read resources (ember, pyre health) |
| N | Toggle combat speed (game's native key) - announces the new speed |
| Tab | Read train stats (pyre health, gold, deck size) |
**Note**: F and E are avoided because they conflict with the game's native shortcuts (F = Toggle Unit Details, E = End Turn).

### Unit Targeting (when playing spells that target units)
| Key | Action |
|-----|--------|
| Left/Right arrows | Select target unit |
| Number keys 1-5 | Select target directly |
| Enter | Confirm target |
| Escape | Cancel spell |

**Targeting Order**: Your first summoned unit is on the far right (front). Go LEFT to target your other units. Go RIGHT to target enemies. Floor announcements list your units front-to-back, then enemies.

### Floor Targeting (when playing a card that requires floor selection)
| Key | Action |
|-----|--------|
| Page Up/Down | Cycle between floors |
| Enter | Confirm and play card on selected floor |
| Escape | Cancel card play |

## Monster Train Native Keyboard Shortcuts

These are the game's built-in keyboard shortcuts that work with or without the accessibility mod.

### Navigation & Floor Management

| Action | Keyboard Shortcut |
|--------|-------------------|
| Move Floor Up | `Page Up` / `W` / `Up Arrow` |
| Move Floor Down | `Page Down` / `S` / `Down Arrow` |
| View Top Floor | `Home` |
| View Bottom Floor | `End` |
| Zoom In / Out | `Scroll Wheel` |

### Combat & Card Actions

| Action | Keyboard Shortcut |
|--------|-------------------|
| End Turn | `E` / `Enter` (Hold for safety if enabled in options) |
| Undo Action | `Z` / `Ctrl + Z` (Only works for certain non-random actions) |
| Speed Up Gameplay | `Spacebar` (Toggles between speeds during combat) |
| Toggle Unit Details | `F` (on a unit) |
| Show Draw Pile | `D` |
| Show Discard Pile | `G` |
| Show Exhaust/Consumed Pile | `X` |

### Menu & System

| Action | Keyboard Shortcut |
|--------|-------------------|
| Open Menu / Settings | `Esc` |
| Open Map | `M` |
| Full Screen Toggle | `Alt + Enter` |

### Note for Controller Users

If you are playing on a PC with a controller, the game has a "Keyboard Mode" that activates when you press a key. If your cursor behaves unexpectedly, check your input settings (Hybrid/Mouse/Controller modes in the options menu).

## Configuration

After first launch, a configuration file is created at:
`BepInEx/config/com.accessibility.monstertrain.cfg`

You can edit this file to customize:
- Key bindings for all controls
- Verbosity level (Minimal, Normal, Verbose)
- Which events to announce (card draws, damage, status effects)
- SAPI fallback settings
- Braille display options

### Verbosity Levels
- **Minimal**: Card names and numbers only
- **Normal**: Standard descriptions with key stats
- **Verbose**: Full details including flavor text

## Battle Navigation

The battle screen uses the game's native navigation. Use hotkeys for information:

- Press **H** to hear all cards in your hand
- Press **L** to hear floor status, capacity, and units (L for Levels)
- Press **U** to hear all units with detail - your monsters front-to-back, then enemies (U for Units)
- Press **R** to hear ember and pyre health
- Press **N** to toggle combat speed (announces the new speed)
- Press **Tab** to hear train stats (pyre health, gold, deck size)
- Press **F1** for context-sensitive help listing all available keys

### Playing Cards
- Navigate to cards using the game's controls
- Press **Enter** to play the selected card
- When a card requires floor placement (like monster cards), floor targeting mode activates:
  - Use **Page Up/Down** to cycle through floors (same as game's native floor navigation)
  - The current floor's units will be announced as you select
  - Press **Enter** to confirm and play the card
  - Press **Escape** to cancel

## Tips for Blind Players

1. **Press F1 for Help**: On any screen, press F1 to hear all available keyboard shortcuts for that context
2. **Start Simple**: Begin with the tutorial to learn the game flow
3. **Read Text**: Press T to read patch notes, event descriptions, or any screen text
4. **Use Battle Hotkeys**: H, L, U, R provide quick status updates during battle
5. **Check Ember**: Press R regularly to know your available resources
6. **Enemy Intents**: Press U to hear what enemies plan to do next turn
7. **Re-read Items**: Press C to re-read the currently focused menu item
8. **Combat Speed**: Press N to toggle combat speed - the mod announces the new speed

## Troubleshooting

### No Speech Output
1. Verify your screen reader is running
2. Check that `Tolk.dll` is in the plugins folder
3. Try enabling SAPI fallback in the config file

### Mod Not Loading
1. Verify BepInEx is installed correctly
2. Check `BepInEx/LogOutput.log` for error messages
3. Ensure .NET Framework is installed

### Keys Not Responding
1. Make sure the game window has focus
2. Check for key conflicts in the config file
3. Try the alternate keys (Space instead of Enter)

## Known Limitations

- Some UI animations may cause brief delays in announcements
- Complex card effects may require verbose mode for full details
- Multiplayer modes have limited accessibility support

## Support

Report issues or request features at:
[GitHub Issues](https://github.com/yourusername/MonsterTrainAccessibility/issues)

## Credits

- **Tolk Library**: Davy Kager (screen reader integration)
- **BepInEx Team**: Mod loading framework
- **Trainworks**: Community modding toolkit reference
- **Shiny Shoe**: Monster Train developers

## License

This mod is provided free of charge for accessibility purposes.
Tolk library is licensed under LGPLv3.

---

*Making games accessible, one train ride at a time.*
