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
9. Press **Enter** again - a dialog will ask if you want to apply and quit
10. The leftmost option is selected by default (Apply & Quit) - press **Enter** to confirm
11. The game will exit automatically
12. Launch Monster Train again - BepInEx will now be installed

After restarting, the `BepInEx` folder will appear in your game directory.

### Alternative: Manual BepInEx Installation

If you prefer to install BepInEx manually (or if the Workshop method doesn't work):

1. Download BepInEx 5.4.x (x64) from [BepInEx Releases](https://github.com/BepInEx/BepInEx/releases)
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
   ```
4. Launch Monster Train once to let BepInEx initialize

### Step 3: Install the Accessibility Mod

**Important**: Complete Step 2 first (enable mod loader and restart the game) before proceeding. BepInEx must be installed in your game folder.

#### Option A: Use the Build Script (Recommended for Developers)

1. Run `build.ps1` or `build.bat` from the `MonsterTrainAccessibility` folder
2. Enter your game path when prompted (or press Enter for default)
3. When asked to deploy, type `Y` to copy all files automatically

The build script will automatically detect and copy files to the correct location:
- **Steam Workshop**: `Steam\steamapps\workshop\content\1102190\2187468759\BepInEx\plugins\`
- **Or game folder**: `Monster Train\BepInEx\plugins\` (if using manual BepInEx install)

#### Option B: Manual Installation

1. Download `MonsterTrainAccessibility.dll` from the releases
2. Copy `MonsterTrainAccessibility.dll` to the BepInEx plugins folder:
   - **Steam Workshop location** (if you enabled mod loader in-game):
     `C:\Program Files (x86)\Steam\steamapps\workshop\content\1102190\2187468759\BepInEx\plugins\`
   - **Or game folder** (if you installed BepInEx manually):
     `C:\Program Files (x86)\Steam\steamapps\common\Monster Train\BepInEx\plugins\`

### Step 4: Install Tolk Screen Reader Library

The mod requires the Tolk library for screen reader communication.

**Monster Train is a 64-bit application**, so you need the **x64 versions** of all DLLs.

1. Download Tolk from [GitHub Releases](https://github.com/dkager/tolk/releases)
2. From the **x64** folder, copy these files to `BepInEx/plugins/`:
   - `Tolk.dll` (required - main library)
   - `nvdaControllerClient64.dll` (required for NVDA)
   - `SAAPI64.dll` (optional - for Windows SAPI/Narrator fallback)
   - `jfwapi64.dll` (optional - for JAWS support)

**Important**: You MUST use the 64-bit (x64) versions. The 32-bit versions will not work.

### Step 5: Launch the Game

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

### Information Hotkeys (During Battle)
| Key | Action |
|-----|--------|
| H | Read all cards in hand |
| F | Read all floor information |
| E | Read enemy information and intents |
| R | Read resources (ember, pyre health) |
| C | Re-read current focused item |
| V | Cycle verbosity level |
| 1-9 | Quick-select card by position in hand |

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

The battle screen has multiple navigation modes:

### Hand Mode (Default)
- Use Left/Right arrows to browse cards
- Press Enter to play the selected card
- Number keys (1-9) quick-select cards by position

### Floor Selection
- When placing a monster, use Up/Down to select floor
- Press Enter to confirm, Escape to cancel

### Targeting
- When a spell needs a target, browse valid targets
- Press Enter to select target, Escape to cancel

## Tips for Blind Players

1. **Start Simple**: Begin with the tutorial to learn the game flow
2. **Use Hotkeys**: H, F, E, R provide quick status updates during battle
3. **Listen for Events**: The mod announces card draws, damage, and deaths
4. **Check Ember**: Press R regularly to know your available resources
5. **Enemy Intents**: Press E to hear what enemies plan to do next turn

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
