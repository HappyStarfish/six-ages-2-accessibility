# Six Ages 2 — Game API Reference

## Architecture Overview

- Unity 2018.3.9f1, 32-bit (x86), Mono-Backend (Legacy)
- Assembly-CSharp.dll = thin C# wrapper (445 KB)
- Assembly-CSharp-firstpass.dll = PluginImport (P/Invoke bridge) + all enums/delegates
- StormAgePlugin.dll = native game logic (3250 KB, Objective-C via WinObjC, 563 exports)
- libobjc2.dll = ObjC runtime (GNUstep/Microsoft fork)

Decompiled source: `decompiled/` (Assembly-CSharp), `decompiled-firstpass/` (firstpass)

---

## P/Invoke Bridge (PluginImport.cs)

All calls go through `PluginImport` (in Assembly-CSharp-firstpass.dll).
DLL target: `"StormAgePlugin"`.

String pattern: native `_C()` returns `IntPtr`, C# wrapper calls `Marshal.PtrToStringAnsi()`.

### Initialization

- `InitializeSixAges(CallbackInt screenDispatch, CallbackStringInt scriptDispatch, CallbackInt messageDispatch, CallbackStringInt commandDispatch, CallbackStringIntInt hitTest)` — registers 5 callbacks
- `DeinitializeSixAges()`
- `SetAppVersion(string)`
- `NewGame(string chapterID)`
- `LoadGame(string url)`
- `ContinueGame(string chapterID, string url)`

### Callback Delegates

- `CallbackInt(int value)` — screen dispatch, script messages
- `CallbackStringInt(string name, int type)` — script dispatch, command dispatch
- `CallbackStringIntInt(string name, int arg1, int arg2) -> int` — hit test (zone contains point)

---

## Game State (Game_ functions)

### Current State

- `Game_CurrentScreen() -> int` (GameScreen enum)
- `Game_SetCurrentScreen(int)`
- `Game_Season() -> int` (Season enum)
- `Game_SeasonName() -> string`
- `Game_ShortSeasonName() -> string`
- `Game_Year() -> int`
- `Game_YearName() -> string`
- `Game_Time() -> int`
- `Game_TurnInYear() -> int`
- `Game_WinLose() -> int`
- `Game_DidLose() -> bool`
- `Game_DidWin() -> bool`

### Variables (read/write game state)

- `Game_BooleanVariable(string name) -> bool`
- `Game_IntegerVariable(string name) -> int`
- `Game_StringVariable(string name) -> string`
- `Game_SetVariableToInteger(string name, int value)`
- `Game_SetVariableToString(string name, string value)`
- `Game_SetVariableToPerson(string name, int person)`
- `Game_SetVariableToClan(string name, int clan)`
- `Game_ClanVariable(string name) -> int`
- `Game_ClanPersonVariable(string name) -> int`
- `Game_ReplacePlaceholdersIn(string) -> string` — substitutes template variables
- `Game_TextFromRunningScript(string name) -> string`

### Save/Load

- `Game_SaveAsSnapshot()`
- `Game_SaveAsYearlySave()`
- `Game_LoadGameYear(int year)`
- `Game_AtLeastOneSavedGame() -> bool`
- `Game_DeleteSavedGame(string folderURL) -> bool`
- `Game_SaveSlot() -> string`
- `Game_CanBeRestored() -> bool`
- `Game_Restores() -> int`

### Scripts

- `Game_QueueScript(string name, int delay)`
- `Game_RunScript(string name)`
- `Game_TriggerScene(string sceneName, int type)`
- `Game_ScriptHasTag(string script, string tag) -> bool`
- `Game_ScriptExists(string script) -> bool`

### Deities / Myths

- `Game_CurrentDeity() -> Deity`
- `Game_SetCurrentDeity(Deity)`
- `Game_NameOfDeity(Deity) -> string`
- `Game_DescriptionOfDeity(Deity) -> string`
- `Game_GenderOfDeity(Deity) -> int`
- `Game_RuneForDeity(Deity) -> string`
- `Game_MythName(int) -> string`
- `Game_MythQuestName(int) -> string`
- `Game_MythQuestSummary(int) -> string`
- `Game_MythCallsFor(int) -> string`
- `Game_MythForTopic(string topic) -> int (Myth)` — 0 / `myth_None` wenn kein Myth zum Topic
- `Game_MythPicture(int myth) -> string` — Sprite-Name oder leer
- `Game_CanShowDetailForTopic(string topic) -> bool` — steuert Detail-Toggle (`_mini` ↔ Voll)
- `Game_LoreList_Name(int idx) -> string`, `Game_LoreList_Filename(int idx) -> string`
- `Game_InitList_AllLore() -> int (count)`

### Saga

- `Game_SagaForYear(int year) -> string`
- `Game_NameOfYear(int year) -> string`
- `Game_NameOfRulerInYear(int year) -> string`
- `Game_RulerCount() -> int`
- `Game_SagaFinishEntry()`
- `Game_AddSagaBreak()`

### Chapters

- `Game_AppChapter() -> string`
- `Game_ChapterCount() -> int`
- `Game_ChapterName(int index) -> string`
- `Game_ChapterID(int index) -> string`

---

## Script / Scene API (Script_ functions)

These are the most important functions for accessibility — they provide the current scene text, responses, and advice.

### Scene Text (KEY FOR ACCESSIBILITY)

- `Script_Text() -> string` — **current scene/event text**
- `Script_Caption() -> string` — scene heading/caption
- `Script_ResultText() -> string` — result text after choice
- `Script_Picture() -> string` — current picture name
- `Script_Music() -> string` — current music name
- `Script_Position() -> int` — text panel position (Position enum)

### Response Options (KEY FOR ACCESSIBILITY)

- `Script_ResponseCount() -> int` — number of available responses
- `Script_ResponseText(int index) -> string` — text of response (1-based index!)
- `Script_DoResponse(int response)` — execute a response
- `Script_IsDone() -> bool` — scene completed?
- `Script_Result() -> int` — ScriptResult enum value
- `Script_WhatChoice() -> int`
- `Script_Completed()`

### Advisor Advice (KEY FOR ACCESSIBILITY)

- `Script_HasAdvice() -> bool`
- `Script_AdviceForAdvisor(int position) -> string` — advisor's advice text for current scene
- `Script_SuggestionsForAdvisor(int position) -> int` — suggested response index

### Script Control

- `Script_Init(string name)`
- `Script_ChangeScriptTo(string name)`
- `Script_SaveScript()`
- `Script_RestoreScript()`
- `Script_Restart()`
- `Script_ClearText()`
- `Script_SetNestedUI(bool)`
- `Script_IsNestedUI() -> bool`
- `Script_SetVariableToInteger(string name, int value)`
- `Script_SetChoiceValue(int value)`
- `Script_SetChoiceString(string value)`
- `Script_SetMinAndMax(int min, int max)`
- `Script_Speaker() -> int`
- `Script_NumericArgument() -> int`

### Clan Info in Scripts

- `Script_ClanArgument() -> string`
- `Script_OtherClanHerds() -> int`
- `Script_OtherClanHasHorses() -> bool`
- `Script_InitClanArray() -> int`
- `Script_ClanArrayClanIndex(int index) -> int`

### Treasures in Scripts

- `Script_InitTreasureList() -> int`
- `Script_TreasureListItemKey(int index) -> int`
- `Script_TreasureListItemName(int index) -> string`
- `Script_TreasureListItemSummaryWithParens(int index) -> string`

---

## Player Clan (PC_ functions)

- `PC_Name() -> string`
- `PC_Food() -> int`
- `PC_FoodText() -> string`
- `PC_Forecast() -> string`
- `PC_Goods() -> int`
- `PC_Herds() -> int`
- `PC_Horses() -> int`
- `PC_Magic() -> int`
- `PC_Mood() -> int`
- `PC_MoodText() -> string`
- `PC_Children() -> int`
- `PC_Commoners() -> int`
- `PC_MaxPossibleWarriors() -> int`
- `PC_MarketSize() -> int`
- `PC_MarketFrequencyText() -> string`
- `PC_ExoticGoodsText() -> string`
- `PC_LikeCount() -> int`
- `PC_HateCount() -> int`
- `PC_FearCount() -> int`
- `PC_MockCount() -> int`
- `PC_BestPerson() -> int`
- `PC_CowEquivalent() -> int`

---

## GameController (GC_ functions)

- `GC_SetCurrentScreen(int)`
- `GC_SetLastManagementScreen(int)`
- `GC_AdvanceTurn()`
- `GC_AdvanceFromSacredTime()`
- `GC_AdvanceFromQuestionnaire()`
- `GC_DispatchSceneIfAny() -> bool`
- `GC_DispatchScriptCurrentlyInteracting()`
- `GC_EndScriptWithWinLose(int)`
- `GC_SetAdvancingEntireSeason(bool)`

---

## C# Controller Classes (Harmony Patch Targets)

### GameManager (Singleton)

- `OnDispatchScript(string, int)` — receives scene events, dispatches to ShowScene/ShowNews/ShowRaid
- `OnDispatchScreen(int)` — receives screen change events
- `OnDispatchCommand(string, int)` — receives command events (tutorial, delta text, etc.)
- `ChangeScreen(int)` — public screen navigation
- `ShowManagementScreen(GameScreen, bool)` — shows management screens
- `GetResponses() -> List<string>` — reads response options
- `ShowScene(string, QType)` — shows interactive scene
- `ShowNews(string, QType)` — shows news/report

### ScreenManager (Singleton)

- `activeScreen` — currently active ScreenController
- `currentScreen` — current screen reference
- `onScreenChanged` — UnityEvent, fires on screen changes
- `Show(string name)` / `Show(ScreenController, bool)`
- `ShowDialog(ScreenController, bool)`
- `HideDialog(ScreenController, bool, bool)`

### SceneController (extends InteractiveController)

- `InitializeFromScript()` — sets up scene text, picture, responses
- `ShowAdviceForNewScene()` — shows advisor advice (private)
- `TappedDone()` — scene completion
- `AddResponses()` — adds response buttons (inherited)

### InteractiveController (extends BaseController)

- `textPanel` — TextPanel component with scene text
- `scriptName` — current script name
- `AddResponses()` — populates response buttons
- `DoResponseNumber(int)` — executes numbered response
- `DeltaText(string)` — delta/change text display

---

## Key Enums

### GameScreen

- Management: Clan=1, Magic=2, Map=3, Relations=4, War=5, Wealth=6, Lore=7, Saga=8, Controls=9, SacredTime=10
- Dialogs: Reorganize=20, Venture=21, Emissary=22, Ritual=24, Sacrifice=25, Spirit=26, Temple=27, Raid=31, Warriors=32, Caravan=33
- Menu: MainMenu=50, ChooseGame=51
- Scenes: Scene=103, Heroquest=104, Intro=101
- End: Lost=106, Victory=107, Won=108

### Season

- SeaSeason=1, FireSeason=2, EarthSeason=3, DarkSeason=4, StormSeason=5, SacredTime=6

### QType (Script/Event Types)

- Scene=1, News=-2, Quest=-3, Raid=-4, CattleRaid=-9, Intro=-14

### ScriptResult

- SceneDone=0, TriggerScript=2, SceneContinues=3, NewChoice=4, DoOffensiveBattle=5, DoCattleRaid=22

### Position

- Left=1, Center=2, Right=3

### BlessingLevel (decompiled-firstpass/BlessingLevel.cs)

The single per-blessing state variable. `PlayerClan.LevelOfMagic(blessingID)` returns it; `PlayerClan.SetMagicToLevel(blessingID, level)` sets it (toggling a checkbox writes 200 or 0).

- `kUnknownBlessing = -10` — clan does not know the blessing → not shown in the UI at all
- `kUnlearnedBlessing = -1` — known of, but not learned → shown, checkbox greyed out (non-interactable)
- `kBlessingKnown = 0` / `kNoBlessing = 0` — learned, currently not in effect
- `kTransientBlessing = 100` — in effect temporarily (recent sacrifice in Gods view, bargain in Spirits view)
- `kPermanentBlessing = 200` — in effect permanently via a temple slot

Ordering holds for `>=` comparisons (DashboardMenuItem uses them).

**Checkbox is derived, not independent.** `MagicScreenController.UpdateCheckbox` computes the UI from the level every redraw: `toggle.isOn = (level == kPermanentBlessing)`; `toggle.interactable = (level != kUnlearnedBlessing)`; `recentSacrifice/recentBargain = (level == kTransientBlessing)`. So checkbox-ON means exactly level 200, but checkbox-OFF conflates levels -1, 0 and 100. A dead deity is a special case: all toggles forced off + non-interactable regardless of level.

To recover the real level from a `UIDeityEffectItem` without the private blessing list: not interactable → unlearned; isOn → permanent; recentSacrifice/recentBargain → transient; else → known-not-in-effect. (See `MagicScreenNavigator.DescribeBlessingState`.)

### Temple size = blessing slots

`TempleSize` (kNoTemple/kShrine/kTemple/kGreatTemple) determines how many *permanent* blessings a deity may hold: shrine 1, temple 2, great temple 3 (`BuildDialogController.OnInit`). `PluginImport.PC_TempleSize(deity)` returns the count as an int (0/1/2/3). Building one tier calls `GC_AdvanceTurn`; upgrading needs enough worshippers + mysteries; `Game.DeityMaxSize(deity)` caps some deities below great temple.

---

## Safe Mod Keys

Keys NOT used by the game (safe for mod hotkeys):

**Available (no game binding found):**
- F2, F4, F5-F12 (F1 = show mode only, F3 = QA debug only)
- Most letter keys (game uses mouse-only interaction)
- Tab, Insert, Home, End, Page Up, Page Down

**Used by game:**
- F1 — show mode main menu (only in Utils.IsShowMode())
- F3 — debug dialog (only when QA features enabled)
- Mouse — all primary interaction

**Recommendation for accessibility mod:**
- F5 = read current scene text
- F6 = read responses
- F3 = cycle advisors (Shift+F3 = advisor info)
- F2 = cycle stat-panel categories (season explanation → time → resources → reputation)
- F4 = dashboard concerns cycle (Shift+F4 = backward, Ctrl+F4 = current screen one-shot)
- F12 = debug mode toggle
- Arrow keys = navigate responses
- Enter = select response
- Escape = back/cancel

---

## Keyboard Shortcuts (ShortcutManager)

Static class, called from `GameManager.Update()` every frame.

- `RegisterShortcut(KeyCode key, Object obj, Func<bool> action)` — registers a shortcut
- `UnregisterShortcut(KeyCode key, Object obj)` — removes shortcut
- `Update()` — processes all registered shortcuts per frame
- `ShortcutsEnabled(Component comp)` — returns false if screen not active or input field active
- `IsModifierKey(KeyCode key)` — checks if key is modifier (Alt, Ctrl, Shift, Win, AltGr)
- `AnyModifierKey()` — true if any modifier held

**Behavior:** Shortcuts only fire when NO modifier key is held (unless the shortcut IS a modifier). Last-registered shortcut wins (stack-based). Shortcuts disabled when `InputFieldMonitor.isInputFieldActive` is true.

**Game uses:** ResponseButton registers 1-9 shortcuts, UIButton registers its `shortcut`/`shortcut2` fields via OnEnable.

---

## Event Flow (How Scenes Work)

1. StormAgePlugin calls `scriptDispatch` callback with script name + QType
2. `GameManager.OnDispatchScript()` receives it
3. For scenes: creates `SceneController`, calls `Init()` + `InitializeFromScript()`
4. `InitializeFromScript()` reads `Script_Text()` for body text, `Script_ResponseCount()`/`Script_ResponseText()` for options
5. Player taps response → `Script_DoResponse(index)` → plugin processes → may callback with new script or screen change
6. Scene done → `Script_Completed()` or `TappedDone()`

**Best Harmony patch points for accessibility:**
- `SceneController.InitializeFromScript()` — announce scene text + responses
- `GameManager.OnDispatchScreen(int)` — announce screen changes
- `GameManager.ShowManagementScreen(GameScreen)` — announce which management screen
- `InteractiveController.DeltaText(string)` — announce text changes/deltas
- `ScreenManager.ActivateScreen(ScreenController)` — general screen activation

---

## Scene Speaker & Resource Deltas (Session 39 analysis)

### Speaker portrait — News only, not Scenes

- `NewsController` has a `PersonCard speaker` field. `InitializeFromScript` calls
  `SetupSpeaker()` → `speaker.SetPerson(PluginImport.Script_Speaker(), …)`.
  `BattleResultsController : NewsController` also calls `SetupSpeaker()`.
- `SceneController` (regular story events) has **no speaker field at all** — no
  portrait, no name. Absence of a speaker name in a story scene is original game
  design, not a missing feature.
- `Script_Speaker()` returns a person index; a negative value = no speaker
  (`PersonCard.SetPerson` treats `-1` as a cleared card). Resolve the name with
  `PC_PersonName(index)` — the same path the game's `PersonCard` uses.

### Resource deltas — `InteractiveController.deltaInfo`

The dictionary that drives the floating delta labels over a scene. Per-key flags:

- `vague: true` — show only `+`/`–`, **no number**. Only `food` and `mood`.
  The plugin still exposes the absolute state as text (`PC_FoodText()`,
  `PC_MoodText()`).
- `negative: true` — `sick`, `hate`, `mock` (a `+` is bad). NOT vague — these
  still show an exact number.
- Exact-number resources: `goods`, `herds`, `horses`, `magic`, `population`,
  `warriors`, `treasureTotal`, `likeCount`, `fearCount`, `hateCount`, `mockCount`,
  `sick`.
- **Cattle is NOT in `deltaInfo`** — no floating delta is ever shown for cattle.

There are no "small/moderate/large" gradations: a delta is either an exact number
or (food/mood only) a bare direction.

### Management-screen stat changes — colour only

`UIStatText.SetValue(value, animated:true)` animates the number and tints it
green (`GameUI.deltaPlusColor`) / red (`GameUI.deltaMinusColor`) during the
transition — but renders **no `+N` text**. A resource change on a management
screen has no textual delta even for sighted players.

`StatPanel` holds eight `UIStatText` fields (herds, goods, swords, magic, like,
hate, fear, mock); `StatPanel.UpdateStats(animated, hidePrologStats)` pushes the
PC_ values into them. `UpdateStats` is called with `animated:true` from
`GuidanceController.UpdateUI` (the `kUpdateUINotification` observer) and with
`animated:false` from `GuidanceController.Show` (panel slide-in). The mod
snapshots and diffs these eight values itself — see `Patches/StatPanelPatches.cs`
(Session 39) — since the game exposes no textual delta.

---

## Complete UI Class Hierarchy

### Base Classes

- **ScreenController** (MonoBehaviour) — base for ALL screens/dialogs. Show(), Close(), OnInit(), OnShow(), OnHide()
- **BaseController** (ScreenController) — adds advisor support, description boxes, clan selection
- **ManagementController** (BaseController) — base for management screens. Has `guidance` (GuidanceController), `advisors`, `leftSide`/`rightSide`
- **InteractiveController** (BaseController) — base for script-driven scenes. Has `textPanel`, responses, sliders, deltas
- **MapController** (BaseController) — base for map-based screens. Has `mapAnnotations`, `mapScroller`, `MapTapped()`
- **ManagementDialogController** (MapController) — base for dialog-overlays with leader selection. Has `personCard`, `chooseLeaderButton`, `actionButton`, `mapToggle`
- **ScreenComponent** (MonoBehaviour) — sub-component within screens (Show/Hide), used by GuidanceController, ManagementMenuController

### Interactive Scene Controllers (extend InteractiveController)

- **SceneController** — standard story events
- **NewsController** — news/reports with speaker portrait. Has `closeButton`, `speaker` (PersonCard)
- **IntroController** — prologue scenes with name entry. Has `nameField`, `skipButton`
- **QuestController** — heroquests with advisor panel
- **BattleController** — combat UI. Has `status` (CombatStatusView), combat options, hero selection
- **BattleResultsController** (extends NewsController) — battle outcome stats. Has captionEK/EW/RK/RW, themLabel, usLabel

### Management Screens (extend ManagementController)

- **ClanScreenController** — GameScreen=1
- **MagicScreenController** — GameScreen=2
- **ControlsScreenController** — GameScreen=9
- **LoreScreenController** — GameScreen=7
- **SagaScreenController** — GameScreen=8
- **WarScreenController** — GameScreen=5
- **WealthScreenController** — GameScreen=6

### Map-Based Screens

- **RelationsScreenController** (extends MapController) — GameScreen=4
- **MapScreenController** (extends ManagementDialogController) — GameScreen=3, exploration missions

### Dialog Controllers (extend ManagementDialogController)

- **RaidDialogController** — GameScreen=31
- **RitualDialogController** — GameScreen=24
- **SacrificeDialogController** (extends EffectsDialogController) — GameScreen=25
- **SpiritDialogController** (extends EffectsDialogController) — GameScreen=26
- **EmissaryDialogController** — GameScreen=22
- **CaravanDialogController** — GameScreen=33
- **VentureDialogController** — GameScreen=21
- **ReorganizeDialogController** — GameScreen=20
- **WarriorsDialogController** — GameScreen=32
- **BuildDialogController** — GameScreen=27. GOTCHA: does NOT wire the inherited `actionButton` — its primary action is the own field `buildButton` (upgrade) plus `reduceButton`/`closeButton`. Resolve the action button per-type, not blindly via `ManagementDialogController.actionButton`.
- **FortifyDialogController** — GameScreen=(via WarScreen)
- **EffectsDialogController** (extends ManagementDialogController) — base for Sacrifice/Spirit

### Special Screens (extend ScreenController directly)

- **MainMenu** — main menu with Play/Intro/Controls/Quit
- **ChooseGameController** (extends BaseController) — save/load game list
- **SixAgesDialog** — generic modal confirmation (title, message, 2 buttons)
- **ControlsOverlay** — settings overlay (toggles, volume slider)
- **ChooseLeaderDialog** — person picker with skill filter
- **InfoDialogController** (extends BaseController) — game intro (HTML browser)
- **MythDialogController** — lore/myth display (HTML browser)
- **ManualDialogController** — game manual (HTML browser)
- **ResultsOverlay** — result text overlay
- **TutorialController** (extends BaseController) — full-screen tutorial

### View Components (MonoBehaviour, sub-parts of screens)

- **AdvisorsView** — 7 advisor slots (or 3 for quests), manages advice display
- **SceneInfoView** — side panel: horses, food, treasures, clan list with filter
- **SagaView** — saga text by year with ruler headers
- **CombatStatusView** — battle stats grid (them vs us, elite vs regular)
- **TutorialView** — floating hint card with multi-page support
- **GuidanceController** (ScreenComponent) — stat panel + advisors, slide animation
- **ManagementMenuController** (ScreenComponent) — side menu with dashboard items, season advance
- **StatPanel** — season, year, herds, goods, swords, magic, like/hate/fear/mock
- **MapView** — hex map rendering with zones, labels, trade routes
- **DashboardMenuItem** — dashboard concern text + magic blessing grid

### UI Widgets

- **UIButton** (Selectable) — button with label, icon, shortcuts, unavailable state
- **ResponseButton** (MonoBehaviour) — scene response with 1-9 shortcuts, NOT a UIButton
- **FaceButton** (MonoBehaviour) — person portrait button
- **DashboardMagicButton** (UIButton) — blessing icon with tooltip
- **UIList** (MonoBehaviour) — scrollable list with selection, pooling, double-click
- **UIListItem** (MonoBehaviour) — list item with label, selection highlight (selectionImage can be null!)
- **UIListItemWithIcons** (UIListItem) — list item with up to 3 icons
- **PersonListItem** (UIListItem) — person with portrait, name, info, deity rune, circle membership toggles
- **SaveListItem** (UIListItem) — save game entry with image, info, delete button (NO selectionImage)
- **SacretTimeListItem** (UIListItem) — sacred time entry with checkboxes
- **UIToggleListItem** (UIListItem) — list item with toggle checkbox
- **UISlider** (Selectable) — numeric slider with label/value, linked slider support, game variable binding
- **UIToggle** (Selectable) — toggle/checkbox with on/off values, group support
- **UIToggleGroup** — radio button group
- **UIToggleLine** — row of sequential toggles
- **UILabel** (MonoBehaviour) — formatted text label with value
- **UILabelWithIcon** — label with icon sprite
- **UIStatText** (MonoBehaviour) — animated stat value with color feedback
- **TextPanel** (MonoBehaviour) — central text/response display hub
- **DescriptionBox** (UIBehaviour) — tooltip popup with arrow
- **TextRollover** (MonoBehaviour) — hover tooltip trigger
- **PersonCard** (MonoBehaviour) — person face + name + deity rune + chief badge
- **MoodDisplay** — mood indicator

### System Classes

- **NotificationCenter** — singleton observer pattern. Post/AddObserver/RemoveObserver
- **AdvisorHelper** — singleton managing advice/info popups. ShowAdvice(), ShowInfoFor(), ShowInfo()
- **ShortcutManager** — static keyboard shortcut registry
- **InputFieldMonitor** — tracks if text input is active
- **SixAgesSound** — audio management
- **CursorManager** — mouse cursor management
- **BrowserManager** — embedded HTML browser

### Data Sources (for UIList.Fill())

- **ListDataSource** — base data provider
- **ClanDataSource** / **ClanDataList** — clan list data
- **PersonDataList** — person list data
- **DeityDataSource** — deity list
- **TreasureDataSource** / **TreasureDataList** — treasure list
- **MythDataSource** / **MythDataList** — myth/lore list
- **LoreDataSource** — history entries
- **RaidDataSource** / **RaidDataList** — raid history
- **FortificationDataSource** / **FortificationDataList** — fortifications
- **BlessingDataList** — deity blessings
- **ForayGoalDataList** — exploration goals

---

## Accessibility Coverage Analysis

### COVERED (has patches/announcements)

- Scene text + responses (ScenePatches — all 6 controller types)
- Scene continue / final / new choice / delta text
- Response selection feedback (prefix capture)
- Screen change announcements (ScreenChangePatches)
- Main menu announcement (MenuPatches)
- ChooseGame list reading (MenuPatches)
- Tutorial full-screen + hint cards (MenuPatches)
- SixAgesDialog modal reading (MenuPatches)
- ControlsOverlay settings reading (MenuPatches)
- Keyboard navigation: Tab/Arrow/Enter for UIButtons (KeyboardNavigationHandler)
- Slider navigation: Up/Down between sliders, Left/Right change value
- List navigation: Up/Down through items, Enter selects
- F5 = repeat scene text, F6 = repeat responses/slider summary
- F12 = debug toggle
- TutorialView hint: Enter collapse, H toggle

### NOT COVERED — Management Screens (Tier 1 Priority)

These are the 7 main management screens. The screen NAME is announced, but NO content is read.

**ClanScreenController** (GameScreen=1)
- Population by caste: commoners, warriors, nobles
- Health per caste: healthy, sick, wounded
- Totals: totalHealthy, totalSick, totalWounded
- MoodDisplay: mood level
- familyInfo: family ring coverage text
- ventureInfo: active venture info
- Buttons: reorganizeButton, ventureButton
- Data: All via PluginImport PC_ functions

**MagicScreenController** (GameScreen=2)
- DeityName + description
- Filter dropdown: Gods, Spirits, Sacred Time, Other
- Deity/spirit list (UIList)
- 4 effect checkboxes (UIDeityEffectItem) for blessings
- templeSizeInfo label
- Buttons: buildButton, bargainButton, sacrificeButton, ritualButton
- BlessingDataList for current deity
- Data: PluginImport Game_CurrentDeity, Game_NameOfDeity, etc.

**MapScreenController** (GameScreen=3)
- Goal list (Explore, Search for Regalia, etc.)
- Elite/regular warrior sliders
- Map with exploration cursor
- CRITICAL: Map is spatial/visual — needs text alternative for blind users
- Data: ForayGoalDataList, map zones via MapView

**RelationsScreenController** (GameScreen=4)
- Filter dropdown: 9 options (Debuted Clans, Like Us, Hate Us, Fear Us, Mock Us, Favors We Owe, Favors Due Us, Believers, Rejecters)
- Clan list (UIList) with filter
- Map annotations showing clan locations
- emissaryButton
- Data: ClanDataSource with various filters

**WarScreenController** (GameScreen=5)
- Elite warriors: count, absent, sick, wounded
- Regular warriors: count, absent, sick, wounded
- Fortifications list (UIList)
- Clans that raided us list (UIList)
- Clans we raided list (UIList)
- Buttons: fortify, raid, herd raid, warriors, honor raid
- Data: FortificationDataList, RaidDataList

**WealthScreenController** (GameScreen=6)
- Cattle, goats, horses counts
- Goods, food supply labels
- Exotic goods text (with color indicator)
- Market frequency label
- Treasures list (UIList)
- Trade clan list (UIList)
- Data: TreasureDataList, ClanDataSource

**LoreScreenController** (GameScreen=7)
- History list (UIList via LoreDataSource: `LoreDataList.AllLore()` → `Game_InitList_AllLore`)
- Myth list (UIList via MythDataSource: `MythDataList.MythsKnownPerformable(performable: false)` — bekannte, nicht-durchfuehrbare Mythen)
  - `showPerformedStatus = false` → Eintrag zeigt `item.name` + Wissens-Icon (`Rune_Details_` = familiar, `Rune_Secret_` = well-known, sonst keins)
- Manual-Button (`ShowManual`) → `screen_Manual`
- Eintrags-Klick (beide Listen) → `MythDialogController.ShowDialogWithTopic(filename)` → `screen_Myth`

**SagaScreenController** (GameScreen=8)
- SagaView: year list with ruler headers
- Saga text per year
- Restore button (conditional)
- Data: Game_SagaForYear, Game_NameOfYear, Game_NameOfRulerInYear

**ControlsScreenController** (GameScreen=9)
- Same settings as ControlsOverlay: music/effects/tutorial toggles, volume slider
- Plus: credits text, debug mail button, version label

### NOT COVERED — Dialog Screens (Tier 2 Priority)

These are opened from management screens. Screen name announced but no content read.

**ReorganizeDialogController** (GameScreen=20)
- Person list with skill filter (7 skills)
- 7 FaceButton ring positions (chief + 6 members)
- Chief/ring toggle checkboxes per person
- Family coverage text (ringInfo)
- Data: PersonDataList, PlayerClan.Ring()

**VentureDialogController** (GameScreen=21)
- Venture list with descriptions
- Running indicator icon per venture
- Leader selection via ChooseLeaderDialog
- Skill requirement info

**EmissaryDialogController** (GameScreen=22)
- Clan list for diplomatic missions
- 5 sliders: elite, regular, goods, herds, horses
- Leader selection
- Map toggle
- Visit indicator per clan

**RitualDialogController** (GameScreen=24)
- Performable myths list
- Ritual description text
- Interval text (when last performed, who can perform)
- Leader selection

**SacrificeDialogController** (GameScreen=25)
- Goods/herds sliders
- 5 blessing radio buttons with descriptions
- Deity rune image
- Health info for Healer deity (sick/wounded counts)

**SpiritDialogController** (GameScreen=26)
- 2 blessing radio buttons
- Bargain approach: Persuade, Offer Magic, Release (bigger/longer)
- Spirit name/rune
- Magic availability check

**BuildDialogController** (GameScreen=27, Temple)
- Deity name + rune
- Temple size explanation
- Info list: shrine/temple/great temple with costs
- Build/reduce buttons
- Maintenance costs

**RaidDialogController** (GameScreen=31)
- Raidable clans list
- Helper clans list with filter (Alliances, Favors Due)
- Elite/regular sliders
- Raid type caption (Raid vs Herd Raid)

**WarriorsDialogController** (GameScreen=32)
- Warrior count slider
- Recruit/dismiss toggle
- Gift/severance toggles
- Max recruitable calculation

**CaravanDialogController** (GameScreen=33)
- Clan list for trade
- Treasure list
- Buy/sell toggles (food, goods, herds, horses, treasure)
- Establish route toggle
- Caravan size radio buttons
- Elite/regular sliders

**FortifyDialogController** (from War screen)
- Buildable fortifications list with costs
- Existing fortifications list
- Cost label with builder discount

### NOT COVERED — Special Dialogs (Tier 2)

**ChooseLeaderDialog**
- Person list (PersonListItem) with skill filter (7 skills)
- Caption describing what to choose
- Choose/close buttons
- List shows: name, skills, circle membership
- CRITICAL for gameplay: many dialogs trigger this for leader selection

### NOT COVERED — Key Views (Tier 2)

**AdvisorsView** — advisor ring navigation
- 7 positions (3 in quest mode)
- Advice text display via AdvisorHelper
- Person info on click
- CRITICAL: Script_AdviceForAdvisor(position) provides advice text
- CRITICAL: Script_SuggestionsForAdvisor(position) shows suggested response

**SceneInfoView** — scene side panel
- Horses count, food supply
- Treasures list
- Clan list with 9 filter options
- Opened via toggle during scenes

**CombatStatusView** — battle state
- Them vs Us: elite/regular warrior counts
- Win/lose pictures
- Clan names
- CRITICAL: must announce during battles

**StatPanel** — resource overview
- Season, year, herds, goods, swords, magic
- Like/hate/fear/mock counts
- Already text-based, just needs hotkey to read

### NOT COVERED — System Features (Tier 3)

**Season Advance (ManagementMenuController)**
- ToggleSeason() shows confirmation slide
- Advance() advances turn
- Needs keyboard trigger + confirmation announcement

**Dashboard (ManagementMenuController + DashboardMenuItem)**
- 6 dashboard items for management screens
- Concerns text (truncated to 3 lines)
- Magic blessings grid with tooltip descriptions
- Dashboard view toggle (DashboardView enum)

**DescriptionBox / Rollover / TextRollover (Tooltips)** — covered by TooltipPatches.cs
- Single render entry point: DescriptionBox.Show(description, sprite, point, pointerDirection)
  is called for ALL hover popups in the game. Patching its postfix catches every tooltip
  in one place.
- Trigger paths that route through DescriptionBox.Show:
  - AdvisorHelper.ShowAdvice → adviceBox.Show (advisor speech bubbles, owned by AdvisorPatches "Advisor:" prefix — skipped in TooltipPatches to avoid duplication)
  - AdvisorHelper.ShowInfoFor → infoBox.Show (advisor / person hover card, content is Person.AttributedTextFor)
  - AdvisorHelper.ShowInfo → infoBox.Show (treasure / blessing / goal / clan synopsis on list selection; called from BaseController/SceneInfoView/CaravanDialog/WealthScreen/MapScreen/BattleController/ChooseGameController.ShowSynopsis)
  - AdvisorHelper.ShowSeasonInfo → infoBox.Show (Sea / Storm / Earth / Fire season indicator hover; "Early/Late" prefix added per turnInYear)
  - Rollover.ShowRollover → static, instantiates a clone of infoBox and calls Show(rollover.message, rollover.sprite, ...). Each UI element with a Rollover component contributes its own static .message string set in the prefab.
  - TextRollover.OnPointerEnter → AdvisorHelper.ShowInfo(info, ...) → ultimately Show. .info is per-component string.
- IDescriptionBoxHandler interface: BaseController + SceneInfoView implement it; their .explanation property is non-null while a tooltip is open. Useful for state checks, not needed for capture.
- All ShowInfo* paths wrap the actual Show() in GameManager.Callback(..., 0.1f), so the postfix fires ~100ms after the trigger; that's still inside the speech queue and works fine with interrupt:false.
- Keyboard-triggered alternative: KeyboardNav HandleFocusedTooltipKey reads Rollover.message / TextRollover.info on the focused Selectable (or its children) when D is pressed and no specialized D-handler claimed the key.

**Person Info (PersonCard + Person)**
- Person.AttributedTextFor(flags) generates description
- Bit flags: 1=name, 2=deity, 4=skills, 8=age, 0x10=location
- AdvisorHelper.ShowInfoFor() / ToggleInfoFor() shows person popup

**NotificationCenter Events**
- "kUpdateUINotification" — UI refresh
- "AdviceView:HideDescription" — close advice popup
- "6A:HideDescription" — close info popup
- Useful for hooking state changes

### Browser-Resource-Archiv (zfbRes_v1)

Statisches HTML/CSS/Bilder fuer InfoDialog/ManualDialog/MythDialog liegen gepackt in:

`Lights Going Out_Data/Resources/browser_assets` (~6,7 MB, 113 Eintraege)

ZenFulcrum-Format, Layout (alle Integer Little-Endian):

```
Header:
  uint8  magicLen        = 9
  char[] magic           = "zfbRes_v1"
  uint32 entryCount

Per Eintrag (entryCount × variable Laenge):
  uint8  pathLen
  char[] path            (UTF-8, fuehrendes "/")
  uint64 dataOffset      (Datei-Offset zum Datenblock)
  uint32 dataSize        (Bytes)

Anschliessend rohe Datenbloecke an den genannten Offsets.
```

Verifiziert 2026-05-10 per Hexdump + Python. Format-RE komplett — externer Extractor unnoetig, ~30 Zeilen `BinaryReader` reichen.

Inhalte (Stand 2026-05-10): 65 × `/DarkAge-Lore/...` (HTML + Bilder), 47 × `/Manual/...` (HTMLs, CSS, TOC, Info), 1 × Mac-`.DS_Store` (ignorieren).

`BrowserNative.webResources.Exists("https://game.local/{path}")` ist im Spielcode (`MythDialogController.URLForResource`) bereits genutzt — vermutlich gibt es auch eine Bytes-API in `ZFBrowser.dll` (`WebResources` / `StandaloneWebResources`-Klassen, `LoadNative`-Methode). Robuster fuer den Mod ist aber, das Archiv direkt aus dem Resources-Ordner zu lesen.

### Lore-Inhalte: Quellen pro Topic

`MythDialogController.ShowTopic(filename, detail, anchor, animated)` waehlt zwischen zwei Quellen:

1. OSL-dynamisch — wenn `Game.ScriptExists("lore_{name}")` true:
   - `Game.TextFromRunningScript("lore_{name}")` liefert Body-HTML-Schnipsel (Skript laeuft frisch, kann Spielzustand reflektieren — nicht cachen).
   - Wird mit Boilerplate (`<!DOCTYPE>`, `<head>` + `parchment.css`-Link) umschlossen, dann via `webView.LoadHTML(html, url)`. Default-URL: `localGame://{chapter}-Lore/dynamic.html`.
2. Statisches HTML — sonst:
   - `URLForResource("{chapter}-Lore/{name}.html")` mit Fallback auf `{chapter}-Lore/{filename}.html` falls `_mini` fehlt. Anchor (`#fragment`) wird angehaengt.
   - `chapter` = `Utils.IsShowMode() ? "DarkAge" : Game.AppChapter()`.
   - Detail-Toggle: `name = filename + "_mini"` bei kompakter Ansicht, `name = filename` bei Voll. Sichtbar nur wenn `Game_CanShowDetailForTopic(filename)` true.

Hyperlinks im HTML: ZenFulcrum injiziert per JS einen Klick-Handler, der `<a href>` durch `openLink(href)`-Callback in C# ersetzt → `MythDialogController.OpenLink(link)` (private) → `ShowTopic(neuesTopic, detail:false, anchor, animated:true)`. Fuer den Mod folgt Link-Folgen am cleanesten per Reflection auf `OpenLink`. Fallback: `MythDialogController.ShowDialogWithTopic(topic)` (statisch, public, verliert Anchor + Detail-Refresh).

Spezialfaelle in `ShowTopic`:
- `textContainerAnimation.Show` wenn `detail` ODER (`myth==myth_None && !hasDetail`) ODER `filename.StartsWith("Deities")`.
- `Tutorial.ShowNote` kontextuell: `StoryDetailsAvailable` / `StoryDetailsNeeded` fuer Topics in `{Humakt, Nontraya, Osara, River}`, sonst `StoryNoRitual` falls Myth, plus `RamHistory` falls `filename=="RamHistory" && IntegerVariable("ch1Victory")!=0`.
- Space (ohne Modifier, `ShortcutManager.ShortcutsEnabled(this)`) → `ToggleText` blendet Text gegen Hintergrund-Bild aus/ein. Bild gesetzt nur wenn `Game_MythForTopic(filename) != myth_None && !string.IsNullOrEmpty(Game_MythPicture(myth))`.

### NOT COVERED — HTML-Browser-Dialoge (Tier 4, Plan in project_status.md)

Drei Dialoge mit ZenFulcrum-Browser:

**InfoDialogController** — Spiel-Intro (`Info-DarkAge.html`)
**ManualDialogController** — Handbuch (`Manual-DarkAge-Unity.html` + `TOC.html`)
**MythDialogController** — Lore/Mythen (OSL oder statisch, siehe oben)

Hybrid-Plan (Details siehe `project_status.md` Tier 4):
- Phase 1: In-Game-Reader mit eigenem Mini-HTML-Tokenizer + Knoten-Modell + Hotkey-Navigation (Tab fuer Links etc.). Wiederverwendbar fuer alle drei Dialoge.
- Phase 2: optionaler externer Browser-Mirror — `browser_assets` einmalig extrahieren, im Spiel per Hotkey HTML-Datei im Standardbrowser oeffnen, NVDA macht den Rest nativ.

### NOT COVERED — Map (Tier 4, Requires Rethink)

**MapView** is a hex grid map with visual-only interaction:
- Clan positions shown as labels on map
- Zones highlighted on selection
- Trade routes as lines
- Mission markers
- Exploration cursor for forays

A text-based alternative is needed:
- List all known clans with location/attitude
- Text-based mission target selection
- Replace spatial interaction with menus

---

## P/Invoke Functions for Management Screen Data

### Clan Screen Data

- `PC_Name() -> string` — clan name
- `PC_Commoners() -> int`, `PC_Children() -> int`
- `PC_Mood() -> int`, `PC_MoodText() -> string`
- `PC_MaxPossibleWarriors() -> int`
- Person data via `PlayerClan.PersonWithIndex(id)`
- Ring data via `PlayerClan.Ring(position)` (1=chief, 2-7=members)

### War Screen Data

- `PC_EliteWarriors/RegularWarriors()` — warrior counts (verify exact function names in PluginImport)
- Fortification/raid data via FortificationDataList, RaidDataList (fill UIList)

### Wealth Screen Data

- `PC_Herds() -> int`, `PC_Horses() -> int`, `PC_Goods() -> int`
- `PC_Food() -> int`, `PC_FoodText() -> string`
- `PC_MarketFrequencyText() -> string`, `PC_ExoticGoodsText() -> string`
- Treasure data via TreasureDataList

### Relations Screen Data

- Clan list via ClanDataSource with filters:
  - filter_DebutedClans, filter_LikeUs, filter_HateUs, filter_FearUs, filter_MockUs
  - filter_FavorsWeOwe, filter_FavorsDueUs, filter_Believers, filter_Rejecters
- `PC_LikeCount/HateCount/FearCount/MockCount() -> int`

### Magic Screen Data

- `Game_CurrentDeity() -> Deity`
- `Game_NameOfDeity(Deity) -> string`
- `Game_DescriptionOfDeity(Deity) -> string`
- BlessingDataList for blessing info

### Advisor Data (for all screens)

- `Script_HasAdvice() -> bool`
- `Script_AdviceForAdvisor(int position) -> string`
- `Script_SuggestionsForAdvisor(int position) -> int`
- Person data: `Person.name`, `Person.deity`, `Person.age`, `Person.ringPosition`
- `Person.AttributedTextFor(int flags) -> string` — formatted person description

### Saga Data

- `Game_SagaForYear(int year) -> string`
- `Game_NameOfYear(int year) -> string`
- `Game_NameOfRulerInYear(int year) -> string`
- `Game_RulerCount() -> int`

---

## Implementation Priority for Remaining Accessibility

**Tier 1 — Core Gameplay (must have for playability)**
1. Management screen content reading (F5 hotkey or auto-announce)
2. Advisor advice reading (F3 hotkey)
3. StatPanel reading (F2 cycle or auto with screen)
4. ChooseLeaderDialog (keyboard list navigation + skill reading)

**Tier 2 — Full Feature Access**
5. All dialog controllers content reading
6. SceneInfoView toggle + reading
7. CombatStatusView battle state
8. Season advance keyboard support
9. Dashboard concerns/magic reading

**Tier 3 — Polish**
10. Person info via hotkey
11. Tooltip alternatives
12. Saga/Lore navigation
13. Notification hooking for state changes

**Tier 4 — Special Solutions Required**
14. HTML browser content extraction
15. Map text-based alternative
