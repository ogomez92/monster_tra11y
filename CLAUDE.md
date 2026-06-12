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
- **InputInterceptor**: Unity MonoBehaviour that handles accessibility hotkeys (F1, C, T, H, K, L, U, R, V, plus Ctrl+G/H/R). Routes input by mode: help browsing first, then Ctrl combos, then targeting, then plain hotkeys.
- **AccessibilityConfig**: BepInEx configuration - verbosity levels, keybindings, announcement toggles. `ReadGoldKey`/`ReadPyreHealthKey`/`ReadPactShardsKey` are pressed together with Ctrl.
- **RunInfoReader**: Cache-free SaveManager reads (gold, pyre health, pact shards) so the Ctrl resource hotkeys work on every screen, not just battle.
- **KeywordManager**: Centralized keyword dictionary built from game localization at runtime (~107 keywords). Sources: `StatusEffectManager.StatusIdToLocalizationExpression`, `CharacterTriggerData.TriggerToLocalizationExpression`, known card trait names, plus a hardcoded fallback dict for mechanics not in the game's formal systems.
- **FocusableItem / FocusContext / VirtualFocusManager**: Focus management and navigation context stacking.
- **Buffers (`Core/Buffers/`)**: Review buffers navigated with Ctrl+arrows, modeled on the MT2 accessibility mod's conventions. `AnnouncementBuffer` stores items in review order: index 0 is the current/top item (newest event or first detail line) and the cursor starts at -1 (a "starting point" before the top), so the first Ctrl+Up reads the current item; Ctrl+Up moves deeper (`MoveDeeper`: older events, further detail lines), Ctrl+Down moves back toward the top (`MoveTowardTop`), no wrap. Refreshing resets the cursor only when content actually changed. `BufferManager` owns the buffer list (cycle order = registration order) and handles Ctrl+Down/Up (items) and Ctrl+Left/Right (buffer switching; empty buffers are skipped; switching announces "Name, N items"). Registration order (MT2): **UI, Events, Card, Creature, Artifact, Reward, Story** (from `FocusBuffers`), then **Hand, Floors, Units, Resources** (from `BattleBuffers`, battle-only). The **Events** buffer is fed by `ScreenReaderOutput.LogCombatEvent` (every combat event), capped at 200 items, and resets to the newest item when focused. `FocusBuffers` is fed from `MenuAccessibility.CheckForSelectionChange` (UI/Card/Artifact/Reward via the domain readers), `CardTargetingPatches` (Creature, from the selected target's detailed description), and `StoryEventScreenPatch` (Story, the captured event narrative); content clears on screen change (`ScreenStateTracker.SetScreen`) and battle exit. **Focus announcements are concise, details go to buffers**: the card/shop/relic readers return a `FocusReadout` (Summary spoken on focus + Details items for the buffer + FullText for non-focus callers) — cards announce "Name, cost ember, stats" with rarity/type/clan/description/keywords in the Card buffer; shop items announce "Name, price, can afford/not enough gold"; relics announce "Artifact: Name". **Keyword explanations always live in buffer content and never in live announcements** — the old "seen this session/battle" HashSet tracking is gone; `GetUnitBriefDescription`/`GetDetailedEnemyDescription`/`GetFloorSummary` take `bool includeKeywords` (buffers pass true, hotkey reads and combat events pass false). `InputSuppressionPatch` (formerly CtrlNavigationSuppressionPatch) suppresses arrows while Ctrl is held — and arrows/Submit/Cancel while the F1 help list is open — on BOTH game input paths: `BaseInput.GetAxisRaw`/`GetButtonDown` (EventSystem UI focus) and `ScreenManager.OnInputMappingSignaled` (the game's `Controls.Left/Right/Up/Down` enum mappings that screens like `HandUI` consume to cycle cards — keyboard mappings only, WASD/gamepad pass through).

### Screen Handlers (`Screens/`)

- **MenuAccessibility**: MonoBehaviour that polls `EventSystem.current.currentSelectedGameObject` and reads text from selected UI elements. `GetTextFromGameObject()` tries readers in priority order, falling through to `GetTextWithContext()` and then `CleanGameObjectName()` as final fallback.
- **BattleAccessibility**: Coordinator for battle screen. Uses `BattleManagerCache` for reflection-cached game state access.
- **CardDraftAccessibility**: Handles card/relic draft, upgrade, purge screen transitions.
- **MapAccessibility**: Handles map screen transitions.
- **MapNavigator**: Virtual map cursor for the map screen (review-only, doesn't move real selection). Ctrl+Up/Down moves between rings (distances), Ctrl+Left/Right steps through a ring's stops. Reads `RunState`/`NodeState` via reflection, using the game's static `MapSection.GetMapNodeDataForBranch(distance, branch, saveManager)` so node `Location`s match the game's `CanBeTriggered`/`HasBeenVisited` indexing (per-type index: merchants, events, then rewards). Nodes appearing in both branch lists are the same `MapNodeData` asset (reference equality) = shared "both paths" stops. Battle per ring via `SaveManager.GetScenarioData(distance)`.

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
| `CompendiumTextReader` | Logbook items, relics grid, stats, clan checklists (FocusReadout: level spoken, XP/champions/victories/cards to UI buffer), leaderboard rows ("you" marker), sort/filter buttons with active state, pagination buttons, tooltip fallback for meters |
| `MetaProgressReader` | Logbook meta progression summaries from SaveManager state (not UI): T-key full summary for Checklist (covenant rank, win streaks, challenges, per-clan level/XP/champions/victories/collections, clanless and DLC extras) and Statistics (leaderboard rows or personal records); also feeds clan row focus details |
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

The logbook gets extra patches in `SimpleScreenPatches.cs`: `CompendiumSectionPatch` (section changes, with per-section key hints for Checklist/Statistics), `CompendiumPageTurnPatch` (page turns), `ChecklistPageTogglePatch` (postfix on `CompendiumSectionChecklist.RefreshPage`, which only runs on real page changes; skips the initial set by checking `currentSection` is still NONE during screen init), and `StatsPageTogglePatch` (prefix captures `currentPage` into `__state`, postfix announces Leaderboard vs Personal Records only when it changed).

**Combat patches** (`Patches/Combat/`): Detect battle events and call `BattleAccessibility` methods.
- `PlayerTurnPatches` - Turn start/end
- `DamagePatches` - Damage application
- `StatusEffectPatches` - Status add/remove
- `UnitLifecyclePatches` - Spawn, death
- `EnemyMovementPatches` - Ascend/descend
- `BattleFlowPatches` - Victory, pyre damage
- `CombatPhaseChangePatch` - Phase transitions
- `CombatMiscPatches` - Relics, healing, max HP buffs; `EnemyDialoguePatch` announces chatter speech bubbles via `ChatterExpression.Express` postfix (`__4` = final localized text) and merchant lines via `MerchantCharacterUI.ShowChatter` (gated by the `AnnounceDialogue` config)
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

Priority-based context-sensitive help. `HelpSystem` selects the highest-priority active `IHelpContext`. 19 contexts from `GlobalHelp` (priority 0) through `DialogHelp` (priority 110). Each context's `IsActive()` checks `ScreenStateTracker.CurrentScreen`.

F1 opens a **browsable help list** (MT2-style): the context's help text is split into entries (`TextUtilities.SplitIntoSpeechItems`), Up/Down read one entry at a time, and F1/Enter/Escape/Space close it. While `HelpSystem.IsBrowsing` is true, `InputInterceptor` routes all input to the browser, the targeting systems skip their input handling, and `InputSuppressionPatch` keeps arrows/Submit/Cancel away from the game.

### Entry Point

`MonsterTrainAccessibility.cs` is the BepInEx plugin entry. `Awake()` initializes all systems, `ApplyPatches()` registers ~57 Harmony patches, `CreateHandlers()` creates persistent MonoBehaviour GameObjects, `RegisterHelpContexts()` registers all 19 help contexts.

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
| F1 | Browsable help list for the current screen (Up/Down read entries; F1/Enter/Escape/Space close) |
| C | Re-read current focused item (cards/shop items/artifacts: reads the FullText details, not the concise summary) |
| T | Read all text on screen (on the logbook Checklist/Statistics sections: structured meta progression summary via `MetaProgressReader` instead of the raw dump) |
| Tab | Read train stats (pyre health, gold, deck size) |
| V | Cycle verbosity level |
| Ctrl+G | Read gold |
| Ctrl+H | Read pyre health |
| Ctrl+R | Read pact shards and threat (The Last Divinity) |
| Ctrl+Up | Review buffer: next item (first press reads the current/top item, further presses go deeper/older) |
| Ctrl+Down | Review buffer: back toward the top |
| Ctrl+Left/Right | Switch between review buffers (UI, Events, Card, Creature, Artifact, Reward, Story; in battle also Hand, Floors, Units, Resources) |

On the map screen, Ctrl+arrows drive the virtual map cursor instead: Ctrl+Up/Down = forward/back one ring, Ctrl+Left/Right = stops within the ring. While Ctrl is held, arrows never reach the game (see `InputSuppressionPatch`).

### Battle Keys
| Key | Action |
|-----|--------|
| H | Read hand (all cards) |
| K | Read the selected floor (capacity and units). MT2 uses B, but B is this game's eaten pile key |
| Shift+K or L | Read all floors |
| U | Read all units with detail |
| R | Read ember |

**Game keyboard defaults** (ground truth: `game/UserKeyMappingHelper.cs:130-172`): E/Enter = Submit, F = Context action, N = game speed, Z = deck, X = draw pile, C = discard pile, V = exhaust pile, B = eaten pile, M = minimap, H = synthesis tooltips, R = live presence, T = cheat, Y/U/I/O/P = emotes, Shift = preview mode, Q/Backspace = close. Mod letter keys may share letters with game controls that are inert on most screens, but avoid keys whose game action is disruptive (that is why the floor key is K, not MT2's B). The game's mapping system ignores modifiers, so `InputSuppressionPatch` swallows the keyboard mappings for the mod's Ctrl+G/H/R keys while Ctrl is held.

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
- **TMP `.text` returns prefab placeholders**: The game's `SetTextSafe` extension calls `TMP_Text.SetText()`, which updates the rendered char buffer but NOT the `text` property — reading `.text` returns the prefab's design-time placeholder (e.g. leaderboard values reading "12345" while the screen shows "0"). Use `UITextHelper.GetRenderedTMPLabelText()` (tries `GetParsedText()`, falls back to `.text`) for any label the game fills at runtime.
- **Game names are confusing**: `Team.Type.Heroes` = enemies, `Team.Type.Monsters` = player's units.

## Key Integration Points

- **Tolk.cs** (in `../tolk/`): P/Invoke wrapper for Tolk.dll screen reader library
- **Trainworks2/**: Reference modding toolkit (not directly used, but useful for finding patch targets)

## Known Limitations / TODO

### MT2 parity features intentionally not ported

The buffers, map review, help browser, and resource keys follow the MT2 accessibility mod's conventions (docs: `t:\repos\amerikrainian\mt2-access\docs_src\src`). Skipped on purpose:

- **Outcome predictions (I / Ctrl+I / Ctrl+Shift+I)**: MT1 only simulates combat inside its floor-targeting preview mode (`SaveManager.PreviewMode` + `CombatManager.DoRoomCombat`); invoking it ourselves would fire all combat patches with phantom events (see `PreviewModeDetector`).
- **Jump to hand (G)**: MT1's hand focus lives in `CardSelectionBehaviour` (`FocusCard(int)`), not the EventSystem; G left untouched.
- **Mod settings screen (Ctrl+M)**: configuration stays in the BepInEx config file.
- **Controller bindings**: this mod is keyboard-only.

### The Last Divinity DLC (Hellforged)

- **Hellpact Shards**: Collectible shards that power special abilities
- **Divine Boon/Divine Horde/Divine Temple**: Special reward nodes - may not be fully readable
- DLC content uses "Pact" terminology internally (e.g., `DarkPactTempleMerchant`, `PactAllNodesPool`)
