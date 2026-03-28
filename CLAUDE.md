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

### Data Flow

1. **Harmony patches** (`Patches/`) detect game events (screen changes, combat events, card plays)
2. Patches update **ScreenStateTracker** and call **screen handlers** (`Screens/`)
3. Screen handlers use **readers** (`Screens/Readers/`) to extract text from game objects
4. All speech output goes through **ScreenReaderOutput** (`Core/`) → Tolk library → screen reader

For menu screens, **MenuAccessibility** polls `EventSystem.current.currentSelectedGameObject` and delegates to the appropriate reader. For battle state, **BattleAccessibility** uses **BattleManagerCache** to access game managers via reflection.

### Module Organization

```
MonsterTrainAccessibility/
├── Core/              # Input, config, screen reader, keywords, focus system
├── Battle/            # Battle-specific readers and targeting systems
├── Screens/           # Screen handler coordinators (MenuAccessibility, BattleAccessibility)
├── Screens/Readers/   # Text extractors for specific UI types (one per screen/component type)
├── Patches/Combat/    # Combat event patches (damage, death, status, turns)
├── Patches/Screens/   # Screen transition patches (one per screen)
├── Patches/           # Card event & targeting patches
├── Help/              # Context-sensitive help system
├── Help/Contexts/     # Individual help providers (priority-based)
└── Utilities/         # Shared helpers (text, localization, reflection, UI)
```

### Core Components (`Core/`)

- **ScreenReaderOutput**: Wrapper for Tolk library - handles speech output, braille, and screen reader detection.
  - **IMPORTANT: Never use `interrupt = true`** - it cuts off previous announcements. Always use `Speak(text, false)` or just `Speak(text)`.
- **InputInterceptor**: Unity MonoBehaviour that handles accessibility hotkeys (F1, C, T, H, L, U, R, V).
- **AccessibilityConfig**: BepInEx configuration - verbosity levels, keybindings, announcement toggles.
- **KeywordManager**: Centralized keyword dictionary built from game localization at runtime (~107 keywords). Sources: `StatusEffectManager.StatusIdToLocalizationExpression`, `CharacterTriggerData.TriggerToLocalizationExpression`, known card trait names, plus a hardcoded fallback dict for mechanics not in the game's formal systems.
- **FocusableItem / FocusContext / VirtualFocusManager**: Focus management and navigation context stacking.

### Screen Handlers (`Screens/`)

- **MenuAccessibility**: MonoBehaviour that polls `EventSystem.current.currentSelectedGameObject` and reads text from selected UI elements. `GetTextFromGameObject()` tries readers in priority order, falling through to `GetTextWithContext()` and then `CleanGameObjectName()` as final fallback.
- **BattleAccessibility**: Coordinator for battle screen. Uses `BattleManagerCache` for reflection-cached game state access.
- **CardDraftAccessibility**: Handles card/relic draft, upgrade, purge screen transitions.
- **MapAccessibility**: Handles map screen transitions.

### Screen Readers (`Screens/Readers/`)

Each reader extracts text from a specific UI domain. Called from `MenuAccessibility.GetTextFromGameObject()`:

| Reader | Handles |
|--------|---------|
| `CardTextReader` | CardUI, card details, status effects, upgrades |
| `ClanSelectionTextReader` | Clan icons, champion choice buttons, covenant selector, DLC toggle |
| `ShopTextReader` | MerchantGoodDetailsUI, MerchantServiceUI |
| `BattleIntroTextReader` | Pre-battle boss info, run opening screen |
| `MapTextReader` | Map nodes, branch choices |
| `RelicTextReader` | RelicInfoUI for artifact selection |
| `CompendiumTextReader` | Logbook items, relics grid, stats, clan checklists, sort buttons |
| `SettingsTextReader` | Settings dropdowns, sliders, toggles |
| `EventTextReader` | Story event elements, continue button, choices |
| `DialogTextReader` | Dialog/popup text |
| `TooltipTextReader` | TooltipProviderComponent text, map node status |

### Battle Systems (`Battle/`)

- **BattleManagerCache**: Reflection-based caching for game manager references (`CardManager`, `SaveManager`, `RoomManager`, `PlayerManager`, `CombatManager`) and their methods.
- **HandReader**: Reads cards in hand with cost, type, playability.
- **FloorReader**: Reads floor capacity, units (yours front-to-back, then enemies), corruption, enchantments.
- **EnemyReader**: Detailed unit descriptions with triggers, abilities, status effects, boss actions.
- **ResourceReader**: Ember, gold, pyre health, DLC crystals/threat.
- **FloorTargetingSystem**: Keyboard floor selection (PageUp/Down, Enter, Escape).
  - **IMPORTANT for Combat Patches**: Check `FloorTargetingSystem.IsTargeting` before announcing damage/deaths - the game calculates preview damage when selecting floors.
- **UnitTargetingSystem**: Keyboard unit targeting (arrows, number keys 1-9, Enter, Escape).

### Harmony Patches

All patches use manual patching via `TryPatch()` methods (no `[HarmonyPatch]` attributes). They use runtime reflection to find game methods. See `PATCH_TARGETS.md` for verified targets.

**Screen patches** (`Patches/Screens/`): One file per screen. Each patches the screen's `Initialize`/`Setup`/`Show` method to call `ScreenStateTracker.SetScreen()` and announce the transition. ~20 screen patches.

**Combat patches** (`Patches/Combat/`): Detect battle events and call `BattleAccessibility` methods.
- `PlayerTurnPatches` - Turn start/end
- `DamagePatches` - Damage application
- `StatusEffectPatches` - Status add/remove
- `UnitLifecyclePatches` - Spawn, death
- `EnemyMovementPatches` - Ascend/descend
- `BattleFlowPatches` - Victory, pyre damage
- `CombatPhaseChangePatch` - Phase transitions
- `CombatMiscPatches` - Relics, healing, max HP buffs
- `PreviewModeDetector` - Filters phantom damage from preview mode
- `CharacterStateHelper` - Shared reflection helpers for CharacterState/CardState

**Card patches** (`Patches/`): `CardEventPatches.cs` (draw, play, discard, shuffle, exhaust, upgrade), `CardTargetingPatches.cs` (target selection, card selection).

**Harmony Patch Timing Pitfall:** Some game methods call other patchable methods synchronously (e.g., `OnContentReady` → `AdvanceStory()` → `OnChoicesPresented`). In these cases, **postfixes run in reverse call order**. Use **prefixes** to capture state before modification. See `StoryEventScreenPatch` for a worked example.

### Shared Utilities (`Utilities/`)

- **TextUtilities**: `StripRichTextTags()` - converts Unity rich text and game `<sprite>` tags to readable words.
- **LocalizationHelper**: `TryLocalize()` / `LocalizeOrNull()` - single localization entry point via cached reflection.
- **ReflectionHelper**: `FindType()` / `FindManager()` - type and manager discovery with caching.
- **UITextHelper**: `GetTMPText()` / `CleanGameObjectName()` - Unity UI text extraction.

### Help System (`Help/`)

Priority-based context-sensitive help. `HelpSystem` selects the highest-priority active `IHelpContext`. 18 contexts from `GlobalHelp` (priority 0) through `DialogHelp` (priority 110). Each context's `IsActive()` checks `ScreenStateTracker.CurrentScreen`.

### Entry Point

`MonsterTrainAccessibility.cs` is the BepInEx plugin entry. `Awake()` initializes all systems, `ApplyPatches()` registers ~55 Harmony patches, `CreateHandlers()` creates persistent MonoBehaviour GameObjects, `RegisterHelpContexts()` registers all 18 help contexts.

## Text Extraction Chain

`MenuAccessibility.GetTextFromGameObject()` tries readers in this order:
1. Scrollbar content, run opening screen, dialog buttons
2. CardUI, shop items, battle intro, relic info
3. Map nodes, DLC toggles, settings elements, generic toggles
4. Compendium items (relics, upgrades, stats, checklists, sort buttons, logbook)
5. Clan selection, champion choice, covenant selector
6. Tooltip buttons, event elements, map branch choices
7. `GetTextWithContext()` - handles short/icon button labels
8. `CleanGameObjectName()` - final fallback

To fix text extraction for a new UI element, add a reader method and insert it at the right priority in this chain.

**`GetTextWithContext()` logic:**
- If text is 1-2 chars (likely icon), uses cleaned GameObject name instead
- If text is 3-4 chars or empty, looks for context from hierarchy
- `GetContextLabelFromHierarchy()` skips container names: container, panel, holder, group, content, root, options, input area, section, buttons, layout, wrapper

## Hotkeys

### Global Keys (all screens)
| Key | Action |
|-----|--------|
| F1 | Context-sensitive help |
| C | Re-read current focused item |
| T | Read all text on screen |
| Tab | Read train stats (pyre health, gold, deck size) |
| V | Cycle verbosity level |

### Battle Keys
| Key | Action |
|-----|--------|
| H | Read hand (all cards) |
| L | Read floors (capacity and units) |
| U | Read all units with detail |
| R | Read resources (ember, pyre, cards) |

Note: F, E, and N are avoided (F = Toggle Unit Details, E = End Turn, N = Combat Speed Toggle).

### Floor Targeting (when playing a card)
| Key | Action |
|-----|--------|
| Page Up/Down | Cycle between floors |
| Enter | Confirm floor selection |
| Escape | Cancel card play |

### Unit Targeting (when playing spells)
| Key | Action |
|-----|--------|
| Left/Right arrows | Select target unit |
| Number keys 1-5 | Select target directly |
| Enter | Confirm target |
| Escape | Cancel spell |

**Targeting Order:** Your frontmost unit is far right. Go LEFT for your other units, RIGHT for enemies. Floor announcements list: your units (front-to-back), then enemies (front-to-back).

### Combat Log
All battle events written to `BepInEx\plugins\accessibility_combat_log.txt` (overwritten each launch).

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

## Floor/Room Index Mapping

The game's internal room indices are **reversed** from user-facing floor numbers:

```
Room Index 0 = Floor 3 (Top)
Room Index 1 = Floor 2 (Middle)
Room Index 2 = Floor 1 (Bottom)
Room Index 3 = Pyre Room
```

**Conversion formula:** `roomIndex = 3 - userFloor` (for floors 1-3)

## Localization

Monster Train uses a `Localize` extension method. Use `LocalizationHelper.TryLocalize(key)` which caches the reflection lookup.

**Best Practice:**
1. Try `GetName()` / `GetDescription()` methods first - they usually return localized text
2. If those return keys (contain `-` and `_`), use `GetDescriptionKey()` and localize the result
3. Fall back to type-name-based display names if localization fails

**`KEY>>...<<` Pattern:** The game wraps unresolved localization keys as `KEY>>keyName<<`. `MenuAccessibility.ResolveInlineKeys()` handles this by extracting the key and calling `KeywordManager.TryLocalize()`. This runs in `CleanSpriteTagsForSpeech()` so all speech output is cleaned automatically.

## Reading Game Data: UI Labels vs Game State

Prefer reading from **game state objects** (SaveManager, CardManager, etc.) over **UI labels** (TMP_Text fields). UI labels can contain:
- Placeholder text from Unity prefabs (never overwritten if feature is locked)
- Stale text from previous screens (not yet updated)
- Rich text / custom formatting that needs stripping

To find a manager instance via reflection, locate the screen component with `FindObjectOfType`, then access its private manager fields (e.g., `ClassSelectionScreen.saveManager`). Or use `ReflectionHelper.FindManager()`.

## Debugging UI Text Extraction

**Log Location:** `C:\Program Files (x86)\Steam\steamapps\common\Monster Train\BepInEx\LogOutput.log`

**What to Look For:**
1. `Components on 'GameObjectName':` - shows component hierarchy
2. `=== Fields on TypeName ===` - lists all fields on a component
3. `TooltipProvider type:` / `Tooltip.fieldName =` - tooltip data structure

**Common Patterns:**
- If text shows placeholder/debug content, check for `IsPlaceholderText()` filter
- If text is missing, check if it's in a tooltip rather than direct TMP text
- If localization keys appear instead of text, use `TryLocalize()` or `LocalizeKey()`

**Adding Debug Logging:**
```csharp
foreach (var field in componentType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
{
    var val = field.GetValue(component);
    MonsterTrainAccessibility.LogInfo($"  {field.Name} = {val?.GetType().Name ?? "null"}");
}
```

## Game Source Reference (`game/` folder)

The `game/` folder contains decompiled game source classes from `Assembly-CSharp`. Reference these directly instead of guessing method signatures for reflection/Harmony patches. **Note:** These files are not compiled into the mod — they exist purely as reference.

### Core Managers

| Class | Role | Key Methods/Fields |
|-------|------|-------------------|
| `CardManager` | Deck, hand, draw, play, discard | `DrawCards(int)`, `PlayCard(int, SpawnPoint, ref SelectionError)`, `DiscardCard(DiscardCardParams)`, `ShuffleDeck()`, `GetHand()`, `GetHandCard(int)`, `GetDiscardPile()`, `AddCard()` |
| `CombatManager` | Combat phases, damage, turns | `StartPlayerTurn()`, `EndPlayerTurn()`, `StartCombat()`, `EndCombat()`, `ApplyDamageToTarget()`. Enum `Phase`: Start, Placement, PreCombat, MonsterTurn, Combat, HeroTurn, EndOfCombat |
| `SaveManager` | Save/load, game state, preview mode | `PreviewMode` property, `GetTowerHP()`, `AdjustTowerHP()`, `GetCurrentScenarioData()`, `GetBalanceData()` |
| `RoomManager` | Floor management (3 floors + pyre) | `GetRoom()`, `GetRoomState()`, `NumRooms = 4`, `currentSelectedRoom`, `rooms` list |
| `PlayerManager` | Player resources | `GetEnergy()`, `AddEnergy()`, `RemoveEnergy()`, `GetTowerHP()`, `AdjustTowerHP()` |
| `MonsterManager` | Player unit management | `GetTeamType()` → `Team.Type.Monsters` |
| `HeroManager` | Enemy unit management | `GetTeamType()` → `Team.Type.Heroes` |
| `ScreenManager` | Screen transitions | `ChangeScreen()`, `LoadScreen()`, `ShowScreen()` |
| `StatusEffectManager` | Status effect tracking | Global status effect registry |

### State Classes (Runtime Game Objects)

| Class | Role | Key Methods |
|-------|------|-------------|
| `CharacterState` | Unit instance | `GetName()`, `GetHP()`, `GetAttackDamage()`, `GetTeamType()`, `GetCurrentRoomIndex()`, `GetStatusEffectStacks(string)`, `ApplyDamage()`. Property: `PreviewMode` |
| `CardState` | Card instance | `GetTitle()`, `GetCost(...)`, `GetStatusEffects()`. Fields: `cardType`, `cost`, `targetsRoom`, `targetless` |
| `RoomState` | Floor state | `IsRoomEnabled()`, `GetRoomIndex()`. When `!IsRoomEnabled()`, the floor is frozen/destroyed. |
| `BossState` | Boss state | `GetNextBossAction()` → `BossActionState`. `AttackPhase.Relentless` triggers room destroy. |
| `BossActionState` | Boss action | `GetTooltipDescription()`, `GetTargetedRoomIndex()`, `IsRoomDestroyAction()`. Description in internal `stringBuilder` field. |

### Data Classes (Definitions/Templates)

| Class | Role | Key Methods |
|-------|------|-------------|
| `CardData` | Card definition | `GetName()`, `GetDescription()`, `GetCost()`, `GetSpawnCharacterData()`. Fields: `cardType`, `traits`, `effects` |
| `CharacterData` | Unit template | `GetName()`, `GetAttackDamage()`, `GetHealth()`, `GetSize()` |
| `ClassData` | Clan definition | `GetTitle()`, `GetChampionData(int)`, `GetChampionCard(int)`. Field: `champions` (List\<ChampionData\>) |
| `ChampionData` | Champion definition | Fields: `championCardData` (CardData), `starterCardData` (CardData), `upgradeTree` (CardUpgradeTreeData) |
| `RelicData` | Artifact definition | `GetName()`, `GetDescriptionKey()` (needs localization) |
| `ScenarioData` | Battle/boss definition | `GetBattleName()`, `GetBossAtIndex(int)` |
| `BalanceData` | Balance constants | `GetMaxEnergy()`, `GetAlternateChampionUnlockLevel()` |

### Screen Classes

| Class | Entry Method | Notes |
|-------|-------------|-------|
| `ClassSelectionScreen` | `Initialize()` | Has `mainChampionSelectionUI` / `subChampionSelectionUI` fields. Calls `SetLocked(!saveManager.IsUnlocked(classData, 1), unlockLevel)` on champion UIs. |
| `ChampionSelectionUI` | `SetLocked(bool, int)` | `championChoiceButtons` list, `classData` field, `locked` field. `Refresh()` sets button states. |
| `ChampionChoiceButton` | `SetState(bool, bool)` | `lockedTooltipProvider.enabled = locked`. Navigation target is the child `GameUISelectableButton` (`Button` property). |
| `StoryEventScreen` | `Initialize()` | Uses Ink engine. `OnContentReady` accumulates text, `OnChoicesPresented` renders choices. |
| `CardDraftScreen` | `Setup(List<CardData>, ...)` | Card draft selection |
| `MerchantScreen` | `Initialize()` | Shop/merchant |
| `RewardScreen` | `Show(List<RewardState>, ...)` | Reward selection |
| `BattleIntroScreen` | `Initialize()` / `Setup()` / `Show()` | Pre-battle boss info |

### Key Structs for Patch Parameters

```csharp
CharacterState.ApplyDamageParams {
    CharacterState attacker, CardState damageSourceCard, bool damageSourceCardFinishingResolution,
    RelicState damageSourceRelic, Damage.Type damageType, bool fromAttractDamageTrigger
}

CharacterState.AddStatusEffectParams {
    bool spawnEffect, bool overrideImmunity, RelicState sourceRelicState,
    CardState sourceCardState, CardManager cardManager, Type fromEffectType, bool sourceIsHero
}

CardManager.DiscardCardParams {
    CardState discardCard, bool wasPlayed, bool handDiscarded, float effectDelay,
    CharacterState characterSummoned, Type outSuppressTraitOnDiscard
}
```

## Adding New Keywords

Keywords are centralized in `Core/KeywordManager.cs`. Add fallback entries to `LoadFallbackKeywords()` for mechanics not in the game's status/trigger/trait systems:
```csharp
{ "KeywordName", "KeywordName: Brief explanation" }
```
Keywords from the game's localization are loaded automatically; only add to fallbacks what the game doesn't provide.

## Common Pitfalls

- **EventSystem focuses child selectables**: When searching for a game component on a focused UI element, always search parents too (the component may be on an ancestor). The game's `GameUISelectableButton` is often a child of the behavior component. Use `FindComponentInSelfOrParents()` pattern (see `ClanSelectionTextReader`).
- **Serialized fields point to children, not parents**: `FindComponentInHierarchy` only searches UP. When a parent component has a serialized reference to a child (e.g., `MerchantGoodUIBase.buyButton`), searching up from the parent won't find the child. Search children too with `GetComponentsInChildren`, or read the field directly via reflection.
- **Preview mode phantom events**: The game simulates damage during floor targeting. Check `FloorTargetingSystem.IsTargeting` or `PreviewMode` before announcing combat events.
- **Game state changes silently**: The game can change the selected room/floor through many mechanisms (card play resolution, combat phase transitions, `SelectCardInternal(reselect: true)`). Don't rely solely on key detection — poll game state to catch all changes. See `FloorTargetingSystem.PollGameFloor()`.
- **Tooltip text persists when disabled**: `TooltipProviderComponent` retains its text even when `enabled = false`. Check the `enabled` property before using tooltip text.
- **Game names are confusing**: `Team.Type.Heroes` = enemies, `Team.Type.Monsters` = player's units.

## Key Integration Points

- **Tolk.cs** (in `../tolk/`): P/Invoke wrapper for Tolk.dll screen reader library
- **Trainworks2/**: Reference modding toolkit (not directly used, but useful for finding patch targets)

## Known Limitations / TODO

### The Last Divinity DLC (Hellforged)

- **Hellpact Shards**: Collectible shards that power special abilities
- **Divine Boon/Divine Horde/Divine Temple**: Special reward nodes - may not be fully readable
- DLC content uses "Pact" terminology internally (e.g., `DarkPactTempleMerchant`, `PactAllNodesPool`)
