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

### Step 1: Install BepInEx

1. Open Steam and go to the [Monster Train Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=2187468759) on Steam Workshop
2. Click **Subscribe** to download the mod loader
3. For detailed modding instructions, see the [Official Monster Train Modding Guide](https://steamcommunity.com/sharedfiles/filedetails/?id=2257843164)

### Step 2: Install the Mod

1. Download the latest release of this mod
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

### Step 3: Launch the game

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

### Main Menu: The Last Divinity Toggle

If you own The Last Divinity DLC, the main menu has a toggle that enables or disables it for your next run. From the menu options, press the **Right arrow**, then **Up**, to reach the toggle. Press **Enter** to switch it - the toggle announces whether the DLC is on or off. Press the **Left arrow** to return to the menu options.

### Information Hotkeys
| Key | Action |
|-----|--------|
| F1 | Open the help list for the current screen (browse with Up/Down, close with F1, Enter, or Escape) |
| C | Re-read current focused item (for cards, shop items, and artifacts this reads the full details, not just the short focus announcement) |
| T | Read all text on screen (patch notes, descriptions, etc.) |
| Tab | Read train stats (pyre health, gold, deck size) |
| V | Cycle verbosity level (Minimal/Normal/Verbose) |

### Run Information (works on any screen)
| Key | Action |
|-----|--------|
| Ctrl + G | Read current gold |
| Ctrl + H | Read current Pyre health |
| Ctrl + R | Read pact shards and threat level (The Last Divinity runs) |

### Review Buffers (Ctrl + Arrow Keys)

Buffers are review lists for extra information, working the same way as in the Monster Train 2 accessibility mod. They are separate from normal focus navigation: moving focus chooses what information is available, and the buffer commands let you inspect that information without moving focus away.

Focus announcements stay short on purpose: cards announce their name, cost, and stats; shop items announce their name, price, and whether you can afford them; artifacts announce just their name. The full details - rarity, type, descriptions, and keyword explanations - are always waiting in the buffers below.

| Key | Action |
|-----|--------|
| Ctrl + Up | Read the next item in the current buffer |
| Ctrl + Down | Move back toward the top of the buffer |
| Ctrl + Right | Switch to the next available buffer |
| Ctrl + Left | Switch to the previous available buffer |

The current item sits at the Ctrl+Down side of a buffer: from the starting point, the first Ctrl+Up reads the current/top item (the newest event, or the first detail line), and further presses move up through the buffer. Ctrl+Down brings you back down toward the current item.

Available buffers:
- **UI**: Text and details for the currently focused UI element.
- **Events**: Every combat announcement (damage, deaths, status effects, card plays, turns) is recorded here. If several things were announced at once, step through them at your own pace; the first Ctrl+Up reads the newest event and further presses go back in history.
- **Card**: Full details for the focused card (name, cost, effects, keywords).
- **Creature**: Full details for the unit currently selected while targeting a spell.
- **Artifact**: Full details for the focused artifact.
- **Reward**: Details for the focused reward element.
- **Story**: The current story event's narrative text, one piece at a time.
- **Hand** (battle only): One item per card, with cost, type, and description.
- **Floors** (battle only): One item per floor, with capacity, corruption, and units.
- **Units** (battle only): One detailed item per unit on the train, floor by floor.
- **Resources** (battle only): Ember, gold, pyre health, cards in hand, and DLC info as separate items.

When the game's **Lore Tooltips** setting is enabled (game settings), the flavor text the game shows for cards, units, and artifacts is included as a final "Lore:" item in the Card, Creature, and Artifact details.

Only buffers that currently have relevant information are available; the others are skipped when cycling. While Ctrl is held, the arrow keys never move the game's real selection, so reviewing is always safe. The buffer position is remembered per buffer while you switch around, and resets when the underlying content changes.

### Map Browsing (Ctrl + Arrow Keys on the Map)

On the map screen, the Ctrl+arrows drive a **virtual map cursor** instead, so you can explore the whole run without moving your real selection:

| Key | Action |
|-----|--------|
| Ctrl + Up | Move forward one ring (toward the final boss). The first press announces your current ring. |
| Ctrl + Down | Move back one ring |
| Ctrl + Right | Next stop on this ring |
| Ctrl + Left | Previous stop on this ring |

Each ring announces its number ("Ring 3 of 8"), whether it's your current position or already traveled, which path was taken on branching rings, the battle waiting at the end of the ring (including boss battles), and how many stops it has. Each stop announces its name, which path it's on (left, right, or both), whether you've visited it, whether it's available right now, and what it does.

The map cursor is review-only. To actually travel, use the normal arrow keys and Enter on the real map selection.

### Automatic Text Reading

The mod automatically reads text when:
- **Scroll views**: When you focus on a scrollable area (like patch notes), the content is read automatically
- **Dialogs/popups**: New dialogs, tooltips, and text panels are announced when they appear
- **Content changes**: If text content updates while you're focused on it, the new content is announced
- **Chatter**: The speech bubbles units show in battle ("These chains would suit you!") and the merchant's lines in shops are announced as "Name says: ..." (toggle with the AnnounceDialogue config setting)

### Battle Hotkeys
| Key | Action |
|-----|--------|
| H | Read all cards in hand |
| K | Read the selected floor - capacity and units (the MT2 mod uses B, taken here by the game's eaten pile) |
| Shift + K or L | Read all floors |
| U | Read all units with detail (U for Units) - your monsters front-to-back, then enemies |
| R | Read current ember |
| Up arrow | Open floor review - a cursor to walk the floors and their units (see below) |
| Ctrl + G | Read gold |
| Ctrl + H | Read Pyre health |
| Ctrl + R | Read pact shards (The Last Divinity) |
| N | Toggle combat speed (game's native key) - announces the new speed |
| Tab | Read train stats (pyre health, gold, deck size) |

### Floor Review (Up arrow in battle)

Monster Train 1 has no keyboard way to inspect units in place - hovering with the mouse is the game's only option. The floor review fills that gap: press the **Up arrow** during battle to open a virtual cursor over the floors. Opening it announces the current floor and reminds you that Escape closes it.

| Key | Action |
|-----|--------|
| Up/Down arrows | Move between floors (bottom floor, middle, top, then the Pyre room) |
| Left/Right arrows | Step through the units on the floor in their battle positions |
| Enter | Read the focused floor or unit with full details |
| Escape | Close the review |

Each floor first announces an overview: capacity (or pyre health in the Pyre room), frozen state, corruption, and the unit names. Pressing Right then walks the lineup in spatial order - your units from back to front, then the enemies from front to back, so the middle of the lineup is where the frontmost fighters meet. Each unit announces its name and stats, with its position ("Your unit 2 of 3", "Enemy 1 of 2, front") at the end. Pressing Left past the first unit returns to the floor overview.

Full details are always waiting in the buffers: the focused floor or unit feeds the **UI** buffer (and units also the **Creature** buffer), so Ctrl+Up/Down can step through status effects with keyword explanations, abilities, enemy intents, and floor corruption or enchantments piece by piece.

The review cursor is review-only - it never moves the game's real selection, and the view is re-read on every keypress so it stays current while units move or die. It closes automatically when a card starts targeting, when a pile view or menu opens, or when the battle ends. Other battle hotkeys (H, K, U, R, F1...) keep working while it is open.

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

These are the game's built-in default key mappings (from the game's own input setup), which work with or without the accessibility mod. They can be rebound in the game's settings.

### Navigation

| Action | Keyboard Shortcut |
|--------|-------------------|
| Submit / Activate | `E` / `Enter` |
| Cancel / Settings | `Esc` |
| Close panel | `Q` / `Backspace` |
| Move | `Arrow keys` / `WASD` |
| Scroll / Floor up and down | `Page Up` / `Page Down` |
| Jump to start / end | `Home` / `End` |

### Combat & Card Actions

| Action | Keyboard Shortcut |
|--------|-------------------|
| Context action (e.g. end turn) | `F` |
| Game speed toggle | `N` |
| Hold for preview mode | `Left Shift` |
| Select card slot 1-10 | `1` - `0` |
| Show Deck | `Z` |
| Show Draw Pile | `X` |
| Show Discard Pile | `C` |
| Show Exhaust Pile | `V` |
| Show Eaten Pile | `B` |
| Toggle synthesis tooltips | `H` |

### Menu & System

| Action | Keyboard Shortcut |
|--------|-------------------|
| Open Minimap | `M` |
| Access HUD | `Tab` |
| Emotes (multiplayer) | `Y` / `U` / `I` / `O` / `P` |
| Feedback | `F8` |
| Full Screen Toggle | `Alt + Enter` |

Several mod hotkeys deliberately share letters with game controls that are inert on most screens (for example C also opens the discard pile in battle, and V the exhaust pile). The mod avoids claiming keys whose game action is disruptive - that is why the floor key is K rather than MT2's B (eaten pile), and gold is Ctrl+G.

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
- Press **K** to hear the selected floor, **Shift+K** or **L** for all floors
- Press **U** to hear all units with detail - your monsters front-to-back, then enemies (U for Units)
- Press the **Up arrow** to open the floor review and walk the floors and units with the arrow keys (see Floor Review above)
- Press **R** to hear your current ember
- Press **Ctrl+G**, **Ctrl+H**, or **Ctrl+R** for gold, pyre health, or pact shards
- Press **N** to toggle combat speed (announces the new speed)
- Press **Tab** to hear train stats (pyre health, gold, deck size)
- Press **F1** for a browsable help list of all available keys

### Playing Cards
- Navigate to cards using the game's controls
- Press **Enter** to play the selected card
- When a card requires floor placement (like monster cards), floor targeting mode activates:
  - Use **Page Up/Down** to cycle through floors (same as game's native floor navigation)
  - The current floor's units will be announced as you select
  - Press **Enter** to confirm and play the card
  - Press **Escape** to cancel

## Tips for Blind Players

1. **Press F1 for Help**: On any screen, press F1 to open the help list, then browse it with Up/Down
2. **Start Simple**: Begin with the tutorial to learn the game flow
3. **Read Text**: Press T to read patch notes, event descriptions, or any screen text
4. **Use Battle Hotkeys**: H, K, U, R provide quick status updates during battle
5. **Check Ember**: Press R regularly to know how much you can play; Ctrl+G and Ctrl+H cover gold and pyre health
6. **Enemy Intents**: Press U to hear what enemies plan to do next turn
7. **Re-read Items**: Press C to re-read the currently focused menu item - for cards, shop items, and artifacts it reads the complete details in one go. Or step through the same details piece by piece with the UI and Card buffers (Ctrl+arrows)
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
- Unlike the Monster Train 2 accessibility mod, there are no combat outcome predictions (I / Ctrl+I), no jump-to-hand key, and no in-game mod settings screen - configuration is done through the BepInEx config file

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
