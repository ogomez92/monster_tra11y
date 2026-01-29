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
- **InputInterceptor**: Unity MonoBehaviour that handles accessibility hotkeys (F1, C, T, H, L, N, R, V). Navigation is handled by the game's native EventSystem.
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

- **FloorTargetingSystem**: Keyboard-based floor selection for playing cards. When a card requires floor placement, use Page Up/Down to select floor (clamped 1-3, doesn't wrap), Enter to confirm, Escape to cancel.
  - **IMPORTANT for Combat Patches**: Check `FloorTargetingSystem.IsTargeting` before announcing damage/deaths - the game calculates preview damage when selecting floors, and those shouldn't be announced.

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
| Tab | Read train stats (pyre health, gold, deck size) |
| V | Cycle verbosity level |

#### Battle Keys
| Key | Action |
|-----|--------|
| H | Read hand (all cards) |
| L | Read floors (all units) - L for Levels |
| N | Read all units (your monsters and enemies) |
| R | Read resources (ember, pyre, cards) |

Note: F and E are avoided because they conflict with the game's native shortcuts (F = Toggle Unit Details, E = End Turn).

#### Floor Targeting Keys (when playing a card)
| Key | Action |
|-----|--------|
| Page Up/Down | Cycle between floors (same as game's native keys) |
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

## Localization

Monster Train uses a `Localize` extension method for all text localization.

**Method Location:**
- Class: `LocalizationExtensions` (static class in `Assembly-CSharp`)
- Method: `Localize(this string key, bool toUpper = false)`
- Returns: Localized string

**Key Format:**
Localization keys follow this pattern:
```
{TypeName}_{fieldName}-{guid1}-{guid2}-v2
```
Example: `SinsData_descriptionKey-d23d6de33eeeeebb-5a268a87653a9064ba547b1444b4c668-v2`

**How to Call via Reflection:**
```csharp
// Cache the method once
private static MethodInfo _localizeMethod;

// Find it in Assembly-CSharp
foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
{
    if (!assembly.GetName().Name.Contains("Assembly-CSharp"))
        continue;

    foreach (var type in assembly.GetTypes())
    {
        if (!type.IsClass || !type.IsAbstract || !type.IsSealed)
            continue;

        var method = type.GetMethod("Localize",
            BindingFlags.Public | BindingFlags.Static);
        if (method != null && method.ReturnType == typeof(string))
        {
            var parameters = method.GetParameters();
            if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(string))
            {
                _localizeMethod = method;
                break;
            }
        }
    }
}

// Call it (handles optional second parameter)
var args = new object[_localizeMethod.GetParameters().Length];
args[0] = key;
for (int i = 1; i < args.Length; i++)
{
    var p = _localizeMethod.GetParameters()[i];
    args[i] = p.HasDefaultValue ? p.DefaultValue : null;
}
string localized = (string)_localizeMethod.Invoke(null, args);
```

**Common Localizable Types:**
- `CardData`: `GetName()`, `GetDescription()` - already return localized text
- `RelicData` / `SinsData`: `GetName()` returns localized, but `GetDescriptionKey()` returns the key that needs localization
- `RewardData`: Has `_rewardTitleKey` field - needs localization
- `ScenarioData`: `GetBattleName()` returns localized text

**Best Practice:**
1. Try `GetName()` / `GetDescription()` methods first - they usually return localized text
2. If those return keys (contain `-` and `_`), use `GetDescriptionKey()` and localize the result
3. Fall back to type-name-based display names if localization fails

## Floor/Room Index Mapping

The game's internal room indices are **reversed** from user-facing floor numbers:

```
Room Index 0 = Floor 3 (Top)
Room Index 1 = Floor 2 (Middle)
Room Index 2 = Floor 1 (Bottom)
Room Index 3 = Pyre Room
```

**Conversion formula:** `roomIndex = 3 - userFloor` (for floors 1-3)

## Keyword Dictionaries

Keywords (status effects, card mechanics) need explanations for screen reader users. The mod maintains dictionaries mapping keyword names to explanations.

**Keyword Dictionary Locations:**
- `MenuAccessibility.cs`: `ExtractKeywordsFromDescription()` method (~line 8653)
- `BattleAccessibility.cs`: `knownKeywords` dictionary (~line 746)

**Current Keywords:**
```csharp
{ "Armor", "Armor: Reduces damage taken by the armor amount" },
{ "Rage", "Rage: Increases attack damage by the rage amount" },
{ "Regen", "Regen: Restores health each turn equal to regen amount" },
{ "Frostbite", "Frostbite: Deals damage at end of turn, then decreases by 1" },
{ "Sap", "Sap: Reduces attack by the sap amount" },
{ "Dazed", "Dazed: Unit cannot attack this turn" },
{ "Rooted", "Rooted: Unit cannot move to another floor" },
{ "Quick", "Quick: Attacks before other units" },
{ "Multistrike", "Multistrike: Attacks multiple times" },
{ "Sweep", "Sweep: Attacks all enemies on floor" },
{ "Trample", "Trample: Excess damage hits the next enemy" },
{ "Lifesteal", "Lifesteal: Heals for damage dealt" },
{ "Spikes", "Spikes: Deals damage to attackers" },
{ "Damage Shield", "Damage Shield: Blocks damage from next attack" },
{ "Stealth", "Stealth: Cannot be targeted until it attacks" },
{ "Burnout", "Burnout: Dies at end of turn" },
{ "Endless", "Endless: Returns to hand when killed" },
{ "Fragile", "Fragile: Dies when damaged" },
{ "Heartless", "Heartless: Cannot be healed" },
{ "Consume", "Consume: Removed from deck after playing" },
{ "Holdover", "Holdover: Returns to hand at end of turn" },
{ "Purge", "Purge: Removed from deck permanently" },
{ "Intrinsic", "Intrinsic: Always drawn on first turn" },
{ "Spell Weakness", "Spell Weakness: Takes extra damage from spells" },
// ... and more
```

**Adding New Keywords:**
1. Search log for unrecognized keywords (text in `<b>tags</b>` that isn't explained)
2. Add to BOTH dictionaries in MenuAccessibility.cs and BattleAccessibility.cs
3. Format: `{ "KeywordName", "KeywordName: Brief explanation" }`

**Where Keywords Are Used:**
- Cards: `GetCardUIText()` calls `ExtractKeywordsFromDescription()`
- Artifacts: `GetRelicInfoText()` calls `ExtractKeywordsFromDescription()`
- Battle units: `BattleAccessibility` uses its own keyword dictionary

## Debugging UI Text Extraction

When text isn't reading correctly, the log shows helpful debug info:

**Log Location:** `C:\Program Files (x86)\Steam\steamapps\common\Monster Train\BepInEx\LogOutput.log`

**What to Look For:**
1. `Components on 'GameObjectName':` - shows component hierarchy
2. `=== Fields on TypeName ===` - lists all fields on a component
3. `TooltipProvider type:` / `Tooltip.fieldName =` - tooltip data structure
4. Text extraction results like `BossDetailsUI texts found: [...]`

**Common Patterns:**
- If text shows placeholder/debug content, check for `IsPlaceholderText()` filter
- If text is missing, check if it's in a tooltip rather than direct TMP text
- If localization keys appear instead of text, use `TryLocalize()` or `LocalizeKey()`

**Adding Debug Logging:**
```csharp
// Log all fields on an unknown component
foreach (var field in componentType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
{
    var val = field.GetValue(component);
    MonsterTrainAccessibility.LogInfo($"  {field.Name} = {val?.GetType().Name ?? "null"}");
}
```
