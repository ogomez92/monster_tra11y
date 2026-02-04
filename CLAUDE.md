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
| L | Read floors (capacity and units) - L for Levels |
| U | Read all units with detail (your monsters front-to-back, then enemies) |
| R | Read resources (ember, pyre, cards) |

Note: F, E, and N are avoided because they conflict with game shortcuts (F = Toggle Unit Details, E = End Turn, N = Combat Speed Toggle).

#### Floor Targeting Keys (when playing a card)
| Key | Action |
|-----|--------|
| Page Up/Down | Cycle between floors (same as game's native keys) |
| Enter | Confirm floor selection |
| Escape | Cancel card play |

#### Unit Targeting (when playing spells)
| Key | Action |
|-----|--------|
| Left/Right arrows | Select target unit |
| Number keys 1-5 | Select target directly |
| Enter | Confirm target |
| Escape | Cancel spell |

**Targeting Order Notes:**
- Your first summoned unit is on the far right (front of your board)
- Go LEFT to target your other units (toward back of board)
- Go RIGHT to target enemy units
- Spells like Restore default to your frontmost unit; go left for others
- Spells like Torch default to first enemy; go right for other enemies
- Floor announcements list: your units (front-to-back), then enemies (front-to-back)
- When placing units: default position is next to existing units; use right arrow before confirming to place at front

### Harmony Patches (MonsterTrainAccessibility/Patches/)

Manual patches (no `[HarmonyPatch]` attributes - use `TryPatch()` methods):
- **ScreenTransitionPatches**: Hooks screen changes to announce transitions
  - `MainMenuScreenPatch`, `BattleIntroScreenPatch`, `CombatStartPatch`
  - `CardDraftScreenPatch`, `ClassSelectionScreenPatch`, `MapScreenPatch`
  - `MerchantScreenPatch`, `EnhancerSelectionScreenPatch`, `GameOverScreenPatch`
  - `SettingsScreenPatch`: Announces "Settings. Press Tab to switch between tabs."
  - `CompendiumScreenPatch`: Announces "Logbook" with navigation keys (Page Up/Down for sections, arrows for pages)
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

## Game Source Reference (`game/` folder)

The `game/` folder contains decompiled game source classes from `Assembly-CSharp`. These can be referenced directly instead of guessing method signatures for reflection/Harmony patches. **Note:** The mod still uses runtime reflection since these files are not compiled into the mod — they exist purely as reference.

### Core Managers

| Class | Role | Key Methods/Fields |
|-------|------|-------------------|
| `CardManager` | Deck, hand, draw, play, discard | `DrawCards(int)`, `PlayCard(int, SpawnPoint, ref SelectionError)`, `DiscardCard(DiscardCardParams)`, `ShuffleDeck()`, `GetHand()`, `GetHandCard(int)`, `GetDiscardPile()`, `AddCard()` |
| `CombatManager` | Combat phases, damage, turns | `StartPlayerTurn()`, `EndPlayerTurn()`, `StartCombat()`, `EndCombat()`, `ApplyDamageToTarget()`. Enum `Phase`: Start, Placement, PreCombat, MonsterTurn, Combat, HeroTurn, EndOfCombat |
| `SaveManager` | Save/load, game state, preview mode | `PreviewMode` property, `GetTowerHP()`, `AdjustTowerHP()`, `GetCurrentScenarioData()`, `GetBalanceData()`. Enums: `GameSpeed`, `VictoryType`, `VictorySectionState` |
| `RoomManager` | Floor management (3 floors + pyre) | `GetRoom()`, `GetRoomState()`, `NumRooms = 4`, `currentSelectedRoom`, `rooms` list |
| `PlayerManager` | Player resources | `GetEnergy()`, `AddEnergy()`, `RemoveEnergy()`, `GetTowerHP()`, `AdjustTowerHP()`. Signals: `energyChangedSignal`, `healPlayerSignal` |
| `MonsterManager` | Player unit management | `InstantiateCharacter()`, `AddCharacter()`, `SpawnCharacter()`, `GetTeamType()` → `Team.Type.Monsters` |
| `HeroManager` | Enemy unit management | `InstantiateCharacter()`, `AddCharacter()`, `OnSpawnPointChanged()`, `PostAscensionDescensionSingularCharacterTrigger()`, `GetTeamType()` → `Team.Type.Heroes` |
| `RelicManager` | Artifact management | `GetRelicState()`, `AddRelic()`, `RemoveRelic()` |
| `ScreenManager` | Screen transitions | `ChangeScreen()`, `LoadScreen()`, `ShowScreen()` |
| `StatusEffectManager` | Status effect tracking | Global status effect registry |
| `ScenarioManager` | Battle scenario/boss | Manages current scenario |
| `StoryManager` | Story events | Event progression |
| `InputManager` | Input handling | Keyboard/gamepad input |
| `SoundManager` | Audio | Sound playback |
| `RandomManager` | RNG | Random number generation |

### State Classes (Runtime Game Objects)

| Class | Role | Key Methods/Fields |
|-------|------|-------------------|
| `CharacterState` | Unit instance (health, position, status) | `GetName()`, `GetHP()`, `GetAttackDamage()`, `GetTeamType()`, `GetCurrentRoomIndex()`, `GetStatusEffectStacks(string)`, `GetStatusEffect(string)`, `ApplyDamage()`, `ApplyHeal()`, `AddStatusEffect()`, `Setup()`. Enums: `MovementState`, `CombatPreviewState`, `DestroyedState`. Inner: `StatusEffectStack`, `ApplyDamageParams`, `AddStatusEffectParams`. Property: `PreviewMode` |
| `CardState` | Card instance | `GetTitle()`, `GetTitleKey()`, `GetCost(...)`, `GetCostWithoutTraits()`, `GetStatusEffects()`, `Setup(CardData, ...)`. Fields: `cardType`, `cost`, `costType`, `targetsRoom`, `targetless`, `rarityType` |
| `RoomState` | Floor state and capacity | Floor state including units, capacity, enchantments |
| `PyreRoomState` | Pyre health tracking | Pyre-specific room state |
| `BossState` | Boss-specific state | Boss action tracking |
| `RelicState` | Artifact instance | Individual artifact runtime state |
| `CardUpgradeState` | Card upgrade instance | `GetAttackDamage()`, `GetAttackDamageBuff()`, `GetCostReduction()`, `GetStatusEffectUpgrades()`, `Setup(CardUpgradeData)` |
| `CardEffectState` | Card effect instance | `GetStatusEffectStackMultiplier()`, `GetDescriptionAsTrait()`, `Setup(CardEffectData, ...)` |
| `RunState` | Current run state | Run progression data |
| `NodeState` | Map node instance | Individual map node |
| `RewardState` | Reward instance | Reward data |
| `MerchantGoodState` | Shop item instance | Merchant item data |
| `CovenantState` | Difficulty level | Covenant tracking |
| `MutatorState` | Mutator modifier | Mutator tracking |

### Data Classes (Definitions/Templates)

| Class | Role | Key Methods/Fields |
|-------|------|-------------------|
| `CardData` | Card definition | `GetName()`, `GetNameKey()`, `GetCost()`, `GetCostType()`, `GetStatusEffects()`. Fields: `nameKey`, `cost`, `cardType`, `traits`, `effects`, `startingUpgrades`. Enum `CostType`: Default, ConsumeRemainingEnergy, NonPlayable |
| `CharacterData` | Unit template | `GetName()`, `GetNameKey()`, `GetAttackDamage()`, `GetStatusEffectImmunities()` |
| `CardEffectData` | Card effect definition | `GetStatusEffectStackMultiplier()` |
| `CardTraitData` | Card trait definition | `Setup(string traitStateName)` |
| `CardUpgradeData` | Upgrade definition | `GetCostReduction()`, `GetStatusEffectUpgrades()` |
| `CardUpgradeTreeData` | Upgrade tree structure | Champion upgrade paths |
| `CollectableRelicData` | Artifact definition | Extends `RelicData` with collectability |
| `RelicData` | Base artifact definition | `GetName()`, `GetNameKey()`, `GetDescriptionKey()` |
| `SinsData` | Trial modifier | Trial relics with special effects |
| `ScenarioData` | Battle/boss definition | `GetBattleName()`, `GetBossIcon()`, `GetBossAtIndex(int)` |
| `ClassData` | Clan definition | `GetTitle()`, `GetTitleKey()`, `GetDescription()`, `GetDescriptionKey()` |
| `ChampionData` | Champion definition | Champion template |
| `CovenantData` | Covenant/difficulty | Difficulty level definition |
| `MutatorData` | Mutator definition | Game modifier definition |
| `AllGameData` | Master data container | All cards, relics, classes, etc. |
| `BalanceData` | Balance constants | `GetMaxEnergy()`, `GetStatusEffectsDisplayData()` |

### Screen Classes

| Class | Entry Method | Notes |
|-------|-------------|-------|
| `MainMenuScreen` | `Initialize()` | Main menu — patched for screen transition |
| `BattleIntroScreen` | `Initialize()` / `Setup()` / `Show()` | Pre-battle boss info |
| `MapScreen` | `Initialize()` | Map/node navigation |
| `CardDraftScreen` | `Setup(List<CardData>, string, bool, Action)` | Card draft selection |
| `RelicDraftScreen` | `Setup(List<CollectableRelicData>, string, bool, Action)` | Artifact draft |
| `ClassSelectionScreen` | `Initialize()` | Clan/class selection |
| `ChampionUpgradeScreen` | `Setup(CardUpgradeTreeData, Source, Action)` | Champion upgrades |
| `MerchantScreen` | — | Shop/merchant |
| `RewardScreen` | `Show(List<RewardState>, Source, Action, ...)` | Reward selection |
| `DeckScreen` | `Setup(Params)` | Deck builder |
| `GameOverScreen` | — | Victory/defeat |
| `StoryEventScreen` | — | Story events |
| `SynthesisScreen` | `Setup(Source, Action)` | Unit synthesis |
| `SettingsScreen` | — | Settings |
| `CompendiumScreen` | — | Card/relic compendium |
| `RunSummaryScreen` | `Setup(RunAggregateData, Action, ...)` | Run summary |
| `DialogScreen` | — | Dialogue/text boxes |
| `MinimapScreen` | — | Minimap display |

### Card Effects (`CardEffect*.cs`, 40+ classes)

All extend `CardEffectBase`. Key pattern:
- `Setup(CardEffectState)` — initialization
- `GetDescriptionAsTrait(CardEffectState)` — tooltip text

**Common effects:** `CardEffectDamage`, `CardEffectHeal`, `CardEffectHealTrain`, `CardEffectAddStatusEffect`, `CardEffectRemoveStatusEffect`, `CardEffectSpawnMonster`, `CardEffectSpawnHero`, `CardEffectBuffDamage`, `CardEffectBuffMaxHealth`, `CardEffectDraw`, `CardEffectAdjustEnergy`, `CardEffectGainEnergy`, `CardEffectBump`, `CardEffectKill`, `CardEffectSacrifice`, `CardEffectRecruit`, `CardEffectTransform`, `CardEffectRandomDiscard`, `CardEffectDiscardHand`, `CardEffectFreezeCard`, `CardEffectModifyCardCost`, `CardEffectAdjustRoomCapacity`

### Card Traits (`CardTrait*.cs`, 40+ classes)

Trait state/data pairs. Key ones:
- `CardTraitExhaustState` (Consume), `CardTraitIntrinsicState` (Intrinsic), `CardTraitFreeze` (Freeze), `CardTraitPermafrost` (Permafrost), `CardTraitRetain` (Holdover), `CardTraitSelfPurge` (Purge), `CardTraitCopyOnPlay`, `CardTraitCorruptRestricted`, `CardTraitIgnoreArmor`, `CardTraitUnplayable`
- **Scaling traits** (20+): `CardTraitScalingAddDamage`, `CardTraitScalingAddStatusEffect`, `CardTraitScalingBuffDamage`, `CardTraitScalingHeal`, `CardTraitScalingReduceCost`, `CardTraitScalingUpgradeUnitAttack/Health/Size/StatusEffect`

### Relic Effects (`RelicEffect*.cs`, 150+ classes)

All extend `RelicEffectBase` and implement various `IRelicEffect` interfaces. The interfaces determine when the effect triggers:
- `IStartOfCombatRelicEffect`, `IEndOfCombatRelicEffect`, `IEndOfTurnRelicEffect`
- `ICardPlayedRelicEffect`, `ICardDrawnRelicEffect`, `IOnDiscardRelicEffect`
- `ICharacterStatAdjustmentRelicEffect`, `IOnStatusEffectAddedRelicEffect`
- `IStartOfRunRelicEffect`, `IMerchantRelicEffect`, `ICardModifierRelicEffect`

### Status Effects (`StatusEffect*State.cs`, 40+ classes)

All extend `StatusEffectState`. Named by mechanic:
- **Buffs:** `Armor`, `Regen`, `DamageShield`, `Lifesteal`, `Haste`, `Multistrike`, `Stealth`, `SpellShield`, `Buff`
- **Debuffs:** `Poison`, `Dazed`, `Rooted`, `Fragile`, `SpellWeakness`, `MeleeWeakness`, `Sap`, `Debuff`, `Silenced`
- **Mechanics:** `Endless`, `Ephemeral`, `Immobile`, `Immune`, `Inedible`, `Inert`, `Cardless`, `HealImmunity`
- **Combat:** `Spikes`, `Trample`, `Splash`, `Ambush`, `Hunter`, `AttractDamage`
- **Special:** `Scorch`, `Spark`, `HealMultiplier`, `Revive`, `Hatch`, `EatMany`, `CorruptPoison`, `CorruptRegen`
- **DLC:** `ShardUpgrade`, `StygianBlessing`, `PyreLock`

### UI Classes (Key ones for text extraction)

| Class | Role |
|-------|------|
| `CardUI` | Card display. Enums: `CardUIState` (Hand, FaceDown, Screen, Locked), `MasteryType` |
| `CharacterUI` | Unit health/status display |
| `BossDetailsUI` | Boss information panel |
| `HandUI` | Hand display, card animations |
| `RoomUI` | Floor display |
| `RoomTargetingUI` | Floor targeting indicator |
| `SpawnPointUI` | Unit placement slots |
| `BranchChoiceUI` | Choice branching |
| `CardChoiceItem` | Draft card item |
| `RelicChoiceItemUI` | Artifact choice item |
| `MerchantGoodDetailsUI` | Shop item details |
| `MerchantServiceUI` | Shop service display |
| `CovenantSelectionUI` | Difficulty selector |
| `ChampionSelectionUI` | Champion choice |
| `TooltipUI` / `TooltipContainer` | Tooltip system |
| `EndTurnUI` | End turn button |
| `EnergyUI` | Ember display |
| `GoldUI` | Gold display |
| `TowerHPUI` | Pyre health display |
| `DeckCountUI` | Deck counter |

### Currently Patched Methods

These are the game methods the mod hooks via Harmony (see `Patches/`):

| Patch | Target | Method |
|-------|--------|--------|
| `MainMenuScreenPatch` | `MainMenuScreen` | `Initialize` (postfix) |
| `BattleIntroScreenPatch` | `BattleIntroScreen` | `Initialize`/`Setup`/`Show` (postfix) |
| `CombatStartPatch` | `CombatManager` | `StartCombat` (postfix) |
| `CardDraftScreenPatch` | `CardDraftScreen` | `Setup` (postfix) |
| `ClassSelectionScreenPatch` | `ClassSelectionScreen` | `Initialize` (postfix) |
| `MapScreenPatch` | `MapScreen` | `Initialize` (postfix) |
| `MerchantScreenPatch` | `MerchantScreen` | `Initialize` (postfix) |
| `GameOverScreenPatch` | `GameOverScreen` | `Initialize` (postfix) |
| `SettingsScreenPatch` | `SettingsScreen` | `Initialize` (postfix) |
| `CompendiumScreenPatch` | `CompendiumScreen` | `Initialize` (postfix) |
| `ScreenManagerPatch` | `ScreenManager` | Generic transition detection |
| `CardDrawPatch` | `CardManager` | `DrawCards` (postfix) |
| `CardPlayedPatch` | `CardManager` | `PlayCard` (postfix) |
| `CardTargetingPatches` | `CardSelectionBehaviour` | `MoveTargetWithKeyboard` (postfix), `SelectCardInternal(bool, bool)` (postfix) |
| `CombatEventPatches` | Various | Turn changes, damage, deaths, status effects |

### Reward System Classes

| Class | Role |
|-------|------|
| `RewardData` | Base reward (has `_rewardTitleKey` field) |
| `CardRewardData` | Card reward |
| `CardPoolRewardData` | Random card pool reward |
| `GoldRewardData` | Gold reward |
| `HealthRewardData` | Pyre health reward |
| `CrystalRewardData` | Crystal/shard reward (Hellforged DLC) |
| `DraftRewardData` | Draft choice reward |
| `RelicDraftRewardData` | Artifact choice reward |
| `ChampionUpgradeRewardData` | Champion upgrade |
| `EnhancerRewardData` | Card upgrade reward |
| `PurgeRewardData` | Card purge option |
| `MerchantRewardData` | Shop reward |
| `UnitSynthesisRewardData` | Unit merge reward |
| `MapSkipRewardData` | Map skip reward |

### Map & Progression

| Class | Role |
|-------|------|
| `MapNodeData` | Node definition (battle, merchant, event, etc.) |
| `MapNodeBucketData` | Node pool configuration |
| `MapNodeUI` / `MapNodeUIBase` | Node display |
| `MapBattleNodeUI` | Battle node icon |
| `MapPath` | Path between nodes |
| `MapSection` | Map region |
| `MapPlayerTrain` | Player train on map |
| `RewardNodeData` | Reward node configuration |

### Signals (Event System)

The game uses `Signal<T>` for pub/sub events:
- `CardManager.cardPlayedSignal` — `Signal<CardState>`
- `CardManager.cardPilesChangedSignal` — `Signal<CardPileInformation>`
- `CardManager.deckShuffledSignal` — `Signal<bool>`
- `PlayerManager.energyChangedSignal` — `Signal<int>`
- `PlayerManager.healPlayerSignal` — `Signal<int>` (static)

### Key Structs for Patch Parameters

```csharp
// CombatManager damage parameters
CombatManager.ApplyDamageToTargetParameters {
    CardState playedCard, bool finalEffectInSequence, RelicState relicState,
    Damage.Type damageType, CharacterState selfTarget, VfxAtLoc vfxAtLoc, bool showDamageVfx
}

// CharacterState damage parameters
CharacterState.ApplyDamageParams {
    CharacterState attacker, CardState damageSourceCard, bool damageSourceCardFinishingResolution,
    RelicState damageSourceRelic, Damage.Type damageType, bool fromAttractDamageTrigger
}

// CharacterState status effect parameters
CharacterState.AddStatusEffectParams {
    bool spawnEffect, bool overrideImmunity, RelicState sourceRelicState,
    CardState sourceCardState, CardManager cardManager, Type fromEffectType, bool sourceIsHero
}

// CardManager discard parameters
CardManager.DiscardCardParams {
    CardState discardCard, bool wasPlayed, bool handDiscarded, float effectDelay,
    CharacterState characterSummoned, Type outSuppressTraitOnDiscard
}

// CardManager pile counts
CardManager.CardPileInformation {
    int deckCount, int handCount, int discardCount, int exhaustedCount, int eatenCount
}
```

## Known Limitations / TODO

### The Last Divinity DLC (Hellforged)

The DLC adds several features that may not be fully accessible yet:

**DLC Features:**
- **Hellpact Shards**: Collectible shards that power special abilities
- **Divine Boon/Divine Horde/Divine Temple**: Special reward nodes for collecting shards
- **Covenant Selector**: UI for selecting difficulty level (shows as "CovenantSelectorUI" - may not be reading properly)
- **Dark Pact Temple Merchant**: Special DLC merchant

**What needs work:**
1. Divine reward nodes (Boon/Horde/Temple) may not be appearing or readable in the first artifact selection screen
2. The Covenant selector interface may just show the component name instead of readable options
3. Shard-related effects and rewards may need specific handling

**Investigation notes:**
- The DLC content uses "Pact" terminology internally (e.g., `DarkPactTempleMerchant`, `PactAllNodesPool`)
- Covenant/difficulty selection may require unlocking higher levels first
- UI types to investigate: `CovenantSelectorUI`, any Pact-related UI components

### Key Conflicts Avoided

These keys conflict with game shortcuts and are NOT used by the mod:
- **N**: Combat speed toggle (was previously Read Units, now U)
- **F**: Toggle Unit Details
- **E**: End Turn (game handles this)
