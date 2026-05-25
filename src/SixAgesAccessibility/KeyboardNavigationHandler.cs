using System;
using System.Collections.Generic;
using SixAgesAccessibility.Patches;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SixAgesAccessibility
{
    /// <summary>Adds keyboard navigation to all game screens including slider control.</summary>
    public class KeyboardNavigationHandler : MonoBehaviour
    {
        private List<Selectable> _buttons = new List<Selectable>();
        private int _focusIndex = -1;
        private ScreenController _lastScreen;
        private UIList _activeList;
        private int _listFocusIndex = -1;
        private bool _inListMode;
        private float _lastRefreshTime;
        private const float RefreshInterval = 0.5f;

        // Slider navigation state. _sliderFocusIndex points at the slider that
        // Left/Right currently adjusts; it stays valid even when _focusOnSlider is
        // false (so Left/Right works even before the user has cycled into a slider
        // via Up/Down — e.g. immediately on a kChooseEscort scene).
        private List<UISlider> _sliders = new List<UISlider>();
        private int _sliderFocusIndex = -1;
        // True when Up/Down has cycled focus onto a slider. Sliders sit between
        // the question header and the regular nav-order in the virtual cycle, so
        // for Scene-based slider choices (kChooseEscort and the dozen siblings)
        // arrow keys reach the sliders directly instead of getting stuck on the
        // header. Mirrors _focusOnHeader.
        private bool _focusOnSlider;

        // Magic-screen zone navigation. State and dispatch live in MagicScreenNavigator;
        // we only hold the instance and forward the Update tick when the active screen
        // is a MagicScreenController.
        private MagicScreenNavigator _magicNav;
        // Single-zone navigator for the Saga screen — exposes the year list to
        // arrow keys and a blank Enter for the Restore action. Required because
        // SagaView.years is a UIList whose rows are not Selectables, and the
        // restore button is invisible to flat Tab nav.
        private SagaNavigator _sagaNav;

        // Reorganize dialog navigation. The dialog has filter / list / ring / action
        // zones with per-person toggles, which the flat Tab nav cannot model — see
        // ReorganizeNavigator for the rationale.
        private ReorganizeNavigator _reorganizeNav;

        // Sacrifice dialog navigation. Effects (blessing radio toggles) + 2 sliders
        // + action; the toggles' label is on a sibling text element so the flat nav
        // can't read it. See SacrificeNavigator.
        private SacrificeNavigator _sacrificeNav;

        // Spirit Bargain dialog navigation. Two zones: 4 approach radios on
        // bargainToggleGroup (Persuade / Offer Magic / Release Larger / Release
        // Longer) + the inherited blessing list. Flat Tab only sees the BARGAIN
        // action button + Close, so the user would silently auto-Persuade on
        // the first blessing without knowing the choices existed. See
        // SpiritNavigator.
        private SpiritNavigator _spiritNav;

        // Game Over screen handling: announces sagaCaption.text on enter, and
        // owns input while the embedded SagaView overlay is open (year nav,
        // D for full text, a blank Enter restores, Escape closes). See
        // GameOverNavigator.
        private GameOverNavigator _gameOverNav;

        // Emissary dialog navigation. Clan list + 5 gift/escort sliders + leader
        // chooser + Send. Mirrors the Reorganize/Sacrifice zone pattern; the clan
        // list is otherwise unreachable (UIList isn't a Selectable).
        private EmissaryNavigator _emissaryNav;

        // Caravan dialog navigation. The most complex dialog: clan list + sell/buy
        // mode toggles + 10 commodity toggles + treasure list + 2 sliders + caravan
        // size radio + leader + Send. Six zones grouped by purpose.
        private CaravanNavigator _caravanNav;

        // ChooseLeaderDialog navigation. Game-blocking on every kChooseLeader scene
        // and as a sub-picker from Emissary/Caravan/Raid/Venture/Reorganize. The
        // PersonListItem rows have no Selectable surface the flat nav can read, so
        // arrow keys would only announce GameObject names. See ChooseLeaderNavigator.
        private ChooseLeaderNavigator _chooseLeaderNav;

        // MapScreen (Foray / Exploration) zone navigation. Five zones — goals,
        // sliders, leader, destination (synthesised clan list standing in for the
        // hex map), action — stitched together so the screen is usable without
        // mouse-driven hex tapping. See MapScreenNavigator.
        private MapScreenNavigator _mapScreenNav;

        // SacredTime (year-end) zone navigation. Three zones — Forecast text,
        // 10-line magic allocation, Saga + Proceed buttons — plus an overlay
        // mode for the SagaDialog opened from the same screen. Without this
        // navigator the toggle lines (UIToggleLine: a row of N Toggles acting
        // as an integer slider) are inaccessible to flat Tab nav, locking
        // blind users out of the year-end blessing distribution.
        private SacredTimeNavigator _sacredTimeNav;

        // WarriorsDialog: slider + 4 conditional toggles. The slider's value vs
        // PlayerClan.warriors flips actionButton between RECRUIT and DISMISS,
        // and gates which of the four toggles can be flipped. Flat Tab can't
        // model that — Navigator owns Slider + Toggles zones with mode-aware
        // announcements after each adjust.
        private WarriorsNavigator _warriorsNav;

        // RaidDialog (general Raid + CattleRaid via screenIndex): two UILists
        // (raidableClans + helperClans), 2 sliders, filter dropdown, leader
        // picker. CattleRaid hides the helperGroup; the navigator skips the
        // Helpers zone in that case so Tab doesn't dead-end on an empty zone.
        private RaidNavigator _raidNav;

        private FortifyNavigator _fortifyNav;

        // Wealth-screen zone navigation. Two UILists (treasures + tradeClans)
        // plus a single CARAVAN button. UIList rows aren't Selectables so flat
        // Tab nav can never reach them; the navigator exposes them as zones and
        // routes the Caravan dialog through hotkey C.
        private WealthScreenNavigator _wealthNav;

        // Lore-screen zone navigation. Same shape as Wealth: two UILists
        // (historyList + mythList) and a single MANUAL button. Exposed via
        // Tab + Up/Down with hotkey M for the Manual screen.
        private LoreScreenNavigator _loreNav;

        // ChooseGame delete confirmation state (two-press to confirm)
        private SaveListItem _pendingDeleteItem;
        private float _pendingDeleteTime;
        private const float DeleteConfirmTimeout = 3f;

        // D-key clan description cycling: 0=full, 1..N=paragraph N. Reset on clan change or timeout.
        private int _clanDescCycleIndex = -1;
        private int _clanDescLastClanIndex = -1;
        private float _clanDescTime;
        private string[] _clanDescParagraphs;
        private const float ClanDescCycleResetTimeout = 2f;

        // ResponseButtons (scene answer options, plus start/proceed) â€” not Selectables,
        // so they live in their own list. Both _buttons and _responseButtons are
        // collection sources; navigation uses _navOrder, which interleaves them in
        // visual top-to-bottom order so Tab/arrow keys match what's on screen.
        // The IntroController @nameentry/@restore screen mixes a name field, restore
        // toggles, a Start button (ResponseButton) and precondition response buttons
        // â€” without Y-sorted interleaving, Tab visits all Selectables first and only
        // then the response buttons, which puts Start AFTER the preconditions even
        // though it sits above them visually.
        private List<ResponseButton> _responseButtons = new List<ResponseButton>();

        // The merged Selectable + ResponseButton list, sorted by visual position. NavSlot
        // and the comparator live in NavSlotCollector.
        private List<NavSlot> _navOrder = new List<NavSlot>();
        private int NavCount { get { return _navOrder.Count; } }

        // Virtual "question header" navigation slot.
        // On scene/dialog screens (InteractiveController) the question text and any
        // current scene caption are exposed as an extra position above the numbered
        // response options, so the user can revisit the question by arrowing up from
        // response 1 (or wrapping past the last option). Internal representation:
        // _focusOnHeader == true is the header position; _focusIndex is irrelevant
        // (kept at -1) while it's true. False otherwise â€” _focusIndex carries the
        // index into the buttons / responses range as usual.
        private bool _focusOnHeader;

        // ChooseGame chapter announcement state. Tracks the last chapter the user
        // was sitting in so an arrow press that crosses from "Six Ages" to "Lights
        // Going Out" can prepend the new chapter heading before the save item.
        // Reset to null on screen change so the first focused save in any new
        // ChooseGame visit also gets its chapter announced.
        private string _lastAnnouncedChapter;

        // Deltas-header slot. A dedicated re-readable line for the resource
        // changes ("Resource changes: Goods plus 27, Mood down.") that the
        // latest ShowDeltas pass produced. Sits between the question/result
        // header (pos -2) and the buttons (pos 0+) so arrow-up from the first
        // response lands on it first, arrow-up again on the question header.
        // Only active when ScenePatches.LastDeltaAnnouncement is non-empty;
        // cleared on new scene init by ScenePatches.AnnounceSceneInit.
        private bool _focusOnDeltas;

        // Whether the active TMP_InputField focus was started by the user
        // (pressing Enter on the nav slot) versus by the game itself.
        // IntroController auto-activates textPanel.nameField on entry, which
        // would otherwise lock keyboard navigation into text-edit mode before
        // the user has a chance to navigate. We use this flag to silently
        // release game-initiated focus so arrow keys work immediately.
        private bool _userActivatedTextField;

        // External refresh request flag.
        // When InitializeFromScript / SceneContinues fires inside the same SceneController
        // instance (e.g. choosing a response that leads to a follow-up question), the
        // active screen reference doesn't change, so the in-Update screenChanged path
        // doesn't run. Without an explicit signal, our cached ResponseButton list keeps
        // pointing at destroyed Unity objects until the periodic soft refresh ticks,
        // and Tab/Enter can hit ghost buttons. Scene patches set this flag to force a
        // fresh button collection on the next Update tick.
        private static bool _externalRefreshRequested;
        public static void RequestRefresh() { _externalRefreshRequested = true; }

        // Set by ScenePatches.NewChoice_Postfix when the script returns kChooseClan,
        // kChooseDeity, kChooseSpirit etc. The next Update tick auto-enters list mode
        // on textPanel.list so arrow keys actually navigate the choices instead of
        // the user being stuck on the question header with nothing to select.
        private static bool _enterSceneListRequested;
        public static void RequestEnterSceneList() { _enterSceneListRequested = true; _externalRefreshRequested = true; }

        // Singleton-style reference so static patches (DialogPatches, etc.) can route
        // calls into the live handler instance attached to GameManager.
        private static KeyboardNavigationHandler _instance;

        private void Awake()
        {
            _instance = this;
            _magicNav = new MagicScreenNavigator(this);
            _reorganizeNav = new ReorganizeNavigator();
            _sacrificeNav = new SacrificeNavigator();
            _spiritNav = new SpiritNavigator();
            _gameOverNav = new GameOverNavigator();
            _emissaryNav = new EmissaryNavigator();
            _caravanNav = new CaravanNavigator();
            _chooseLeaderNav = new ChooseLeaderNavigator();
            _mapScreenNav = new MapScreenNavigator();
            _sagaNav = new SagaNavigator();
            _sacredTimeNav = new SacredTimeNavigator(this);
            _warriorsNav = new WarriorsNavigator();
            _raidNav = new RaidNavigator();
            _fortifyNav = new FortifyNavigator();
            _wealthNav = new WealthScreenNavigator();
            _loreNav = new LoreScreenNavigator();
        }

        private void Update()
        {
            try
            {
                // Announce any screen change that has settled for one frame. Deferred
                // here so a screen the trainer covers with a tutorial gets dropped
                // instead of described while the user is on the tutorial.
                Patches.ScreenChangePatches.FlushPendingScreenChange();

                // Deliver a scene's text once a covering tutorial has been dismissed
                // and the user is back on the scene.
                Patches.ScenePatches.TryDeliverPendingSceneAnnounce();

                // Emit a buffered tutorial block once the chain of topics has settled.
                Patches.MenuPatches.FlushPendingTutorialAnnounce();

                ScreenController screen = GetActiveScreen();
                if (screen == null) return;

                // If a text input field is currently being edited, let Unity handle
                // the keypresses so the user can type. Only Tab/Escape leave the field;
                // after leaving, the user presses Tab again to continue navigating.
                if (IsTextInputActive())
                {
                    // The game auto-activates textPanel.nameField on intro entry
                    // (IntroController.cs uses a delayed GameManager.Callback), which
                    // would lock the user into text-edit mode before they can navigate.
                    // If we didn't initiate the focus ourselves, drop it silently so
                    // arrow keys are immediately usable. The user re-enters edit mode
                    // by pressing Enter on the input field's nav slot.
                    if (!_userActivatedTextField)
                    {
                        SilentReleaseTextInput();
                        // Fall through so the rest of this Update tick runs normally.
                    }
                    else
                    {
                        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape))
                            DeactivateTextInput();
                        return;
                    }
                }

                bool screenChanged = screen != _lastScreen;
                bool externalRefresh = _externalRefreshRequested;
                if (externalRefresh) _externalRefreshRequested = false;

                if (screenChanged)
                {
                    _lastScreen = screen;
                    RefreshButtons(screen);
                    RefreshSliders(screen);
                    _inListMode = false;
                    _activeList = null;
                    _listFocusIndex = -1;
                    _sliderFocusIndex = -1;
                    _focusOnHeader = false;
                    _focusOnSlider = false;
                    _focusOnDeltas = false;
                    _userActivatedTextField = false;
                    _lastAnnouncedChapter = null;

                    // Initialize magic screen zones
                    if (screen is MagicScreenController)
                        _magicNav.ResetForNewScreen();

                    // Reorganize dialog: reset zone state. We deliberately do NOT
                    // auto-announce a candidate here — the dialog re-sorts the list
                    // 100 ms later via a GameManager.Callback for FilterChanged, so
                    // any speech now would name the wrong first person. The dialog's
                    // own Reorganize_Shown postfix already speaks the header; first
                    // arrow / L keypress will read the (post-sort) candidate.
                    if (screen is ReorganizeDialogController)
                        _reorganizeNav.ResetForNewScreen();

                    if (screen is SacrificeDialogController)
                        _sacrificeNav.ResetForNewScreen();

                    if (screen is SpiritDialogController)
                        _spiritNav.ResetForNewScreen();

                    if (screen is GameOverController)
                        _gameOverNav.ResetForNewScreen();

                    if (screen is EmissaryDialogController)
                        _emissaryNav.ResetForNewScreen();

                    if (screen is CaravanDialogController)
                        _caravanNav.ResetForNewScreen();

                    if (screen is ChooseLeaderDialog)
                        _chooseLeaderNav.ResetForNewScreen();

                    if (screen is MapScreenController msc0)
                    {
                        _mapScreenNav.ResetForNewScreen();
                        _mapScreenNav.AnnounceOpening(msc0);
                    }

                    if (screen is SagaScreenController)
                        _sagaNav.ResetForNewScreen();

                    if (screen is SacredTime)
                        _sacredTimeNav.ResetForNewScreen();

                    if (screen is WarriorsDialogController)
                        _warriorsNav.ResetForNewScreen();

                    if (screen is RaidDialogController)
                        _raidNav.ResetForNewScreen();

                    if (screen is FortifyDialogController)
                        _fortifyNav.ResetForNewScreen();

                    if (screen is WealthScreenController)
                        _wealthNav.ResetForNewScreen();

                    if (screen is LoreScreenController)
                        _loreNav.ResetForNewScreen();

                    // RelationsScreen: auto-enter clan-list mode on arrival.
                    // The clan list is the primary interactive surface here â€” making the
                    // user press L first is busywork. Tab still leaves list mode (e.g. to
                    // cycle map mission markers), F cycles the filter regardless of mode.
                    if (screen is RelationsScreenController)
                    {
                        AutoEnterRelationsClanList(screen as RelationsScreenController);
                    }

                    // VentureDialog: auto-enter the venture list. The list is a UIList,
                    // not a Selectable, so Tab can't reach it â€” and in tutorials the only
                    // remaining interactable button is "Start", leaving keyboard users with
                    // nothing audible except the word "Start" no matter which key they hit.
                    // Auto-enter so arrows immediately cycle through the available ventures.
                    if (screen is VentureDialogController)
                    {
                        AutoEnterVentureList(screen as VentureDialogController);
                    }

                    // RitualDialog: same shape as Venture — ritualList is a UIList, not a
                    // Selectable, so without auto-enter the user would have to press L
                    // before arrows do anything useful. Auto-enter puts focus on the list
                    // immediately; Tab still leaves list mode to reach the action +
                    // close + choose-leader buttons.
                    if (screen is RitualDialogController)
                    {
                        AutoEnterRitualList(screen as RitualDialogController);
                    }

                    // Menus (main menu, settings overlay, choose game) — auto-focus the
                    // first item and queue it after the screen-change header so the user
                    // lands directly on an actionable element. Without this the user has
                    // to press an arrow first to hear anything beyond the screen name,
                    // which is busy-work in a short menu list. interrupt:false keeps the
                    // overview ("Hauptmenü.", "Einstellungen Bildschirm.", "Choose game. …
                    // 3 entries…") intact and tucks the first item in right after it.
                    if (_buttons.Count > 0 &&
                        (screen is MainMenu || screen is ControlsOverlay || screen is ChooseGameController))
                    {
                        _focusIndex = 0;
                        AnnounceInitialMenuFocus();
                    }
                }
                else if (externalRefresh)
                {
                    // Same screen, but the responseButtons collection has been replaced
                    // (a follow-up scene loaded via InitializeFromScript / SceneContinues
                    // inside the same controller, or NewChoice has just added sliders for
                    // a kChooseEscort-style choice). Drop focus and re-collect — the old
                    // ResponseButton instances are about to be destroyed, and freshly
                    // added sliders aren't visible to GetComponentsInChildren until the
                    // parent panel finishes its layout pass.
                    RefreshButtons(screen);
                    RefreshSliders(screen);
                    _focusOnHeader = false;
                    _focusOnSlider = false;
                    _focusOnDeltas = false;
                }

                // Periodic refresh for dynamic buttons
                if (Time.unscaledTime - _lastRefreshTime > RefreshInterval)
                {
                    _lastRefreshTime = Time.unscaledTime;
                    RefreshButtonsSoft(screen);
                    RefreshSlidersSoft(screen);
                }

                // Scene list-choice (kChooseClan / kChooseDeity / kChooseSpirit / ...):
                // run AFTER RefreshButtons so the list state and proceedButton are
                // freshly populated before we enter list mode.
                if (_enterSceneListRequested)
                {
                    _enterSceneListRequested = false;
                    AutoEnterSceneListChoice(screen);
                }

                // --- Hotkeys (always active, no cooldown needed) ---

                // Reset advisor cycle on any non-F3 key press (exclude Shift so Shift+F3 works)
                if (AdvisorReader.HasCycleState && !Input.GetKeyDown(KeyCode.F3)
                    && !Input.GetKeyDown(KeyCode.LeftShift) && !Input.GetKeyDown(KeyCode.RightShift)
                    && Input.anyKeyDown)
                    AdvisorReader.ResetCycle();

                // Reset dashboard cycle on any non-F4 key press (exclude modifier
                // keydowns so Shift+F4 / Ctrl+F4 don't pre-reset before F4 fires).
                if (ConcernReader.HasCycleState && !Input.GetKeyDown(KeyCode.F4)
                    && !Input.GetKeyDown(KeyCode.LeftShift) && !Input.GetKeyDown(KeyCode.RightShift)
                    && !Input.GetKeyDown(KeyCode.LeftControl) && !Input.GetKeyDown(KeyCode.RightControl)
                    && Input.anyKeyDown)
                    ConcernReader.ResetCycle();

                // Reset stat-panel cycle on any non-F2 key press (same pattern as
                // the F3 advisor + F4 dashboard cycles — hold position until the
                // user actually moves on, instead of a wall-clock timeout).
                if (StatPanelReader.HasCycleState && !Input.GetKeyDown(KeyCode.F2)
                    && Input.anyKeyDown)
                    StatPanelReader.ResetCycle();

                // F12 â€” toggle debug mode
                if (Input.GetKeyDown(KeyCode.F12))
                {
                    DebugLogger.Toggle();
                    return;
                }

                // Shift+F12 â€” speak recent debug entries
                if (Input.GetKeyDown(KeyCode.F11) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                {
                    DebugLogger.SpeakRecent(5);
                    return;
                }

                // Shift+F1: announce the mod's keys for the current screen.
                // F1 itself is the game's own help overlay (below) — the universal
                // help convention — so the mod's extra key list lives on Shift+F1.
                if (Input.GetKeyDown(KeyCode.F1) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                {
                    AnnounceScreenShortcuts(screen);
                    return;
                }

                // F1: open the game's per-screen help overlay (HelpController).
                // The game normally shows it on click of a visual button; without
                // this hotkey the overlay is unreachable from the keyboard.
                if (Input.GetKeyDown(KeyCode.F1))
                {
                    TryOpenHelpOverlay(screen);
                    return;
                }

                // F4 family — dashboard concerns and magic (management only).
                //   Plain F4: step forward through the dashboard cycle (overview,
                //             then every item across all six screens grouped by
                //             Stress/Advantage/Warning/Omen/Active/Known/Unlearned,
                //             then "End of dashboard" sentinel, then wrap).
                //   Shift+F4: same cycle, one step backward.
                //   Ctrl+F4:  one-shot "what matters here" — current screen's
                //             concerns + active magic only, identical to the
                //             auto-announce on screen entry.
                // Season + season-explanation moved into the F2 cycle.
                if (Input.GetKeyDown(KeyCode.F4))
                {
                    ManagementController mgmtF4 = screen as ManagementController;
                    if (mgmtF4 == null)
                    {
                        ScreenReader.Say("Dashboard is only available during management.");
                        return;
                    }

                    bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    if (ctrl)
                        DialogContentReader.ReadCurrentScreenRelevant(mgmtF4.screenIndex);
                    else if (shift)
                        ConcernReader.Cycle(-1);
                    else
                        ConcernReader.Cycle(+1);
                    return;
                }

                // F5 â€” repeat scene text / management / dialog screen content
                if (Input.GetKeyDown(KeyCode.F5))
                {
                    if (screen is HelpController && HelpScreenReader.TryRepeat())
                        return;
                    // Reorganize uses the navigator's working-ring view (not committed
                    // state) so F5 reflects what the user is about to apply.
                    if (screen is ReorganizeDialogController reorgFull)
                    {
                        _reorganizeNav.AnnounceFullStatus(reorgFull);
                        return;
                    }
                    if (screen is SacrificeDialogController sacrificeFull)
                    {
                        _sacrificeNav.AnnounceFullStatus(sacrificeFull);
                        return;
                    }
                    if (screen is SpiritDialogController spiritFull)
                    {
                        _spiritNav.AnnounceFullStatus(spiritFull);
                        return;
                    }
                    if (screen is GameOverController gameOverFull)
                    {
                        _gameOverNav.AnnounceFullStatus(gameOverFull);
                        return;
                    }
                    if (screen is EmissaryDialogController emissaryFull)
                    {
                        _emissaryNav.AnnounceFullStatus(emissaryFull);
                        return;
                    }
                    if (screen is CaravanDialogController caravanFull)
                    {
                        _caravanNav.AnnounceFullStatus(caravanFull);
                        return;
                    }
                    if (screen is WarriorsDialogController warriorsFull)
                    {
                        _warriorsNav.AnnounceFullStatus(warriorsFull);
                        return;
                    }
                    if (screen is RaidDialogController raidFull)
                    {
                        _raidNav.AnnounceFullStatus(raidFull);
                        return;
                    }
                    if (screen is ChooseLeaderDialog chooseLeaderFull)
                    {
                        _chooseLeaderNav.AnnounceFullStatus(chooseLeaderFull);
                        return;
                    }
                    if (screen is MapScreenController mapScreenFull)
                    {
                        _mapScreenNav.AnnounceFullStatus(mapScreenFull);
                        return;
                    }
                    if (screen is SagaScreenController sagaFull)
                    {
                        _sagaNav.AnnounceFullStatus(sagaFull);
                        return;
                    }
                    if (screen is SacredTime sacredFull)
                    {
                        _sacredTimeNav.AnnounceFullStatus(sacredFull);
                        return;
                    }
                    if (screen is WealthScreenController wealthFull)
                    {
                        _wealthNav.AnnounceFullStatus(wealthFull);
                        return;
                    }
                    if (screen is LoreScreenController loreFull)
                    {
                        _loreNav.AnnounceFullStatus(loreFull);
                        return;
                    }
                    if (ManagementScreenReader.TryReadFull(screen))
                        return;
                    if (DialogContentReader.TryReadFull(screen))
                        return;
                    ScenePatches.RepeatSceneText();
                    return;
                }

                // F6 â€” repeat response options / slider summary
                if (Input.GetKeyDown(KeyCode.F6))
                {
                    if (_sliders.Count > 0)
                        AnnounceAllSliders();
                    else
                        ScenePatches.RepeatResponses();
                    return;
                }

                // Shift+F3 â€” read advisor info (person details)
                if (Input.GetKeyDown(KeyCode.F3) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                {
                    AdvisorReader.ReadInfo(screen);
                    return;
                }

                // F3 â€” read advisor advice (first press = all, then cycle individually)
                if (Input.GetKeyDown(KeyCode.F3))
                {
                    AdvisorReader.ReadAdviceOrCycle(screen);
                    return;
                }

                // F2 â€” cycle stat-panel categories: season-explanation, time, resources, reputation
                if (Input.GetKeyDown(KeyCode.F2))
                {
                    StatPanelReader.Cycle();
                    return;
                }

                // F10 — read full combat status (battles only). Falls back to a "not in
                // combat" message instead of staying silent so pressing F10 outside a
                // battle is self-explaining rather than confusing.
                if (Input.GetKeyDown(KeyCode.F10))
                {
                    BattleController bc = screen as BattleController;
                    if (bc == null || !CombatScreenReader.AnnounceFullStatus(bc))
                        ScreenReader.Say("No combat status available.");
                    return;
                }

                // S â€” toggle season advance (management only, no modifier keys).
                // Suppressed while the panel is already open (modal handler below
                // takes care of confirm/cancel).
                if (Input.GetKeyDown(KeyCode.S) && !AnyModifier() && !_seasonModeActive)
                {
                    if (screen is ManagementController)
                    {
                        TryToggleSeasonAdvance();
                        return;
                    }
                }

                // Season advance panel is shown on the SideMenu, not the active
                // management screen. While it's open we present a virtual question +
                // 2-options layout (Header / Advance / Cancel) and swallow all other
                // keys so the user can't accidentally trigger management buttons that
                // are visually behind the slide-in panel.
                if (_seasonModeActive)
                {
                    HandleSeasonAdvanceMode();
                    return;
                }

                // I â€” toggle scene info view (scenes only, no modifier keys)
                if (Input.GetKeyDown(KeyCode.I) && !AnyModifier())
                {
                    if (screen is InteractiveController)
                    {
                        TryToggleSceneInfo();
                        return;
                    }
                }

                // P â€” toggle scene picture / text (scenes only, no modifier keys)
                // Mirrors the mouse-only "click the picture" interaction so trainer
                // tutorials that gate on this action (e.g. "Clicking the picture hides
                // any text covering it") can be satisfied from the keyboard.
                if (Input.GetKeyDown(KeyCode.P) && !AnyModifier())
                {
                    if (screen is InteractiveController)
                    {
                        TryTogglePicture(screen as InteractiveController);
                        return;
                    }
                }

                // Ctrl+1..9 â€” switch management screens
                // Ctrl+Tab / Ctrl+Shift+Tab â€” cycle through available management screens
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                {
                    int screenNum = GetCtrlNumberKey();
                    if (screenNum > 0)
                    {
                        TrySwitchManagementScreen(screen, screenNum, "Ctrl+" + screenNum);
                        return;
                    }

                    if (Input.GetKeyDown(KeyCode.Tab))
                    {
                        int dir = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? -1 : 1;
                        TryCycleManagementScreen(screen, dir);
                        return;
                    }
                }

                // H â€” read current tutorial hint page (global hotkey).
                // Hoisted above the per-screen navigator dispatch so navigators that
                // own all input (Wealth, Lore, Reorganize, Sacrifice, Emissary,
                // Caravan, Warriors, Raid, Saga, Spirit, SacredTime, ChooseLeader,
                // GameOver, Map) don't silently swallow H. Modifier keys are excluded
                // so Shift+H / Ctrl+H stay free.
                if (Input.GetKeyDown(KeyCode.H) && !AnyModifier())
                {
                    TutorialHintHandler.Instance.HandleHKey();
                    return;
                }

                // --- Magic screen zone navigation ---
                if (screen is MagicScreenController)
                {
                    _magicNav.HandleInput(screen as MagicScreenController);
                    return;
                }

                // --- Wealth screen: treasures + trade-partners zones, C opens Caravan ---
                if (screen is WealthScreenController wealthScreen)
                {
                    _wealthNav.HandleInput(wealthScreen);
                    return;
                }

                // --- Lore screen: history + myths zones, M opens Manual ---
                if (screen is LoreScreenController loreScreen)
                {
                    _loreNav.HandleInput(loreScreen);
                    return;
                }

                // --- Reorganize dialog: zone navigation owns ALL input here ---
                // The dialog has filter / list / ring / action zones with semantic
                // toggles per person; the flat HandleActivationKeys / HandleButtonNav
                // would just announce GameObject names like "leaderToggle, on".
                if (screen is ReorganizeDialogController reorganize)
                {
                    _reorganizeNav.HandleInput(reorganize);
                    return;
                }

                // --- Sacrifice dialog: blessing radio + sliders + action ---
                if (screen is SacrificeDialogController sacrifice)
                {
                    _sacrificeNav.HandleInput(sacrifice);
                    return;
                }

                // --- Emissary dialog: clan list + sliders + leader + send ---
                if (screen is EmissaryDialogController emissary)
                {
                    _emissaryNav.HandleInput(emissary);
                    return;
                }

                // --- Caravan dialog: list + mode + goods + treasures + escort + action ---
                if (screen is CaravanDialogController caravan)
                {
                    _caravanNav.HandleInput(caravan);
                    return;
                }

                // --- Warriors dialog: slider + 4 mode-conditional toggles ---
                // The slider's value vs PlayerClan.warriors picks RECRUIT vs DISMISS
                // mode and selectively gates the toggles. Flat Tab would reach the
                // four toggles uniformly without telling the user three of them are
                // currently locked, and would never speak the mode change after the
                // user moved the slider across PlayerClan.warriors.
                if (screen is WarriorsDialogController warriors)
                {
                    _warriorsNav.HandleInput(warriors);
                    return;
                }

                // --- Raid dialog: 2 UILists + 2 sliders + filter + leader ---
                // Same controller backs general Raid and CattleRaid (distinguished
                // via screenIndex); the navigator detects helperGroup visibility and
                // skips the Helpers zone on cattle raids. UILists aren't Selectables
                // so flat Tab can never reach the raidable or helper rows.
                if (screen is RaidDialogController raid)
                {
                    _raidNav.HandleInput(raid);
                    return;
                }

                // --- Fortify dialog: buildable + existing UILists ---
                // The two UILists aren't Selectables either, so flat Tab leaves the
                // user stuck on the Close button. The dedicated navigator owns input
                // for browsing/picking/building, including the cost auto-readout.
                if (screen is FortifyDialogController fortify)
                {
                    _fortifyNav.HandleInput(fortify);
                    return;
                }

                // --- ChooseLeader dialog: candidate list + Choose / Close ---
                // Owns ALL input here for the same reason as Reorganize — the
                // PersonListItem rows are not Selectables, the filter is a Filter
                // (UIToggleGroup wrapper), and the flat nav cannot model any of it.
                if (screen is ChooseLeaderDialog chooseLeader)
                {
                    _chooseLeaderNav.HandleInput(chooseLeader);
                    return;
                }

                // --- MapScreen: 5 zones for the foray/exploration setup ---
                // Owns ALL input on this screen. The destination "list" is synthetic
                // (clan centers fed through MapTapped) and there's no flat-nav way
                // to discover it. The goal list and slider pair also live in regions
                // that flat Tab can't navigate semantically.
                if (screen is MapScreenController mapScreen)
                {
                    _mapScreenNav.HandleInput(mapScreen);
                    return;
                }

                // --- Saga screen: arrow keys cycle years, Enter restores ---
                // Owns ALL input here so flat Tab can't accidentally activate the
                // Restore button (it's not a navigable Selectable anyway, but the
                // dedicated dispatch keeps behavior consistent with our other
                // single-purpose navigators).
                if (screen is SagaScreenController saga)
                {
                    _sagaNav.HandleInput(saga);
                    return;
                }

                // --- Spirit Bargain dialog: 4 approach radios + N blessings ---
                // Owns ALL input. The radio toggles aren't visible to the flat
                // Tab nav (their label sits on a sibling element), and the
                // generic ManagementDialog dispatch below would otherwise win
                // and silently auto-Persuade with the default blessing.
                if (screen is SpiritDialogController spirit)
                {
                    _spiritNav.HandleInput(spirit);
                    return;
                }

                // --- Game Over screen: caption + saga-overlay handling ---
                // When the SagaView overlay is closed, this returns false so
                // flat Tab nav still drives REVIEW SAGA / MAIN MENU. When the
                // overlay is open it owns input (years, D, Enter restore,
                // Escape close) so the flat nav doesn't double-handle keys.
                if (screen is GameOverController gameOver
                    && _gameOverNav.TryHandle(gameOver))
                {
                    return;
                }

                // --- SacredTime: year-end forecast + 10-line magic allocation ---
                // Owns ALL input here. The toggle lines aren't reachable via flat
                // Tab nav (UIToggleLine isn't a Selectable; only its individual
                // child Toggles are, and there are up to 100 of them — meaningless
                // without the per-line semantic). Also routes input into the
                // SagaDialog overlay when SacredTime.ShowSaga reparents the saga
                // view into a slide-in dialog over this same screen.
                if (screen is SacredTime sacredTime)
                {
                    _sacredTimeNav.HandleInput(sacredTime);
                    return;
                }

                // --- Tutorial (full-screen): Up/Down review the topic text
                //     paragraph by paragraph, like the Sacred Time forecast.
                //     Only the arrow keys are consumed — Enter on Continue and
                //     Tab fall through to the generic nav, so the button keeps
                //     working without this navigator re-implementing it.
                if (screen is TutorialController tutorialScreen
                    && TutorialScreenNavigator.Instance.HandleInput(tutorialScreen))
                {
                    return;
                }

                // --- Help overlay (game's HelpController, opened with F1): Up/Down
                //     review the help text section by section, like the tutorial.
                //     Only the arrow keys are consumed — Escape (close) and F5
                //     (repeat) fall through to their own handlers.
                if (screen is HelpController helpScreen
                    && HelpScreenNavigator.Instance.HandleInput(helpScreen))
                {
                    return;
                }

                // --- MythDialog (Lore): in-game HTML reader for myths and lore ---
                // Owns ALL input while open. The game's lore content lives inside
                // an embedded ZenFulcrum browser that's invisible to screen
                // readers; we re-parse the same HTML through LoreSource and
                // expose it as a flat node/link list that LoreReader navigates.
                // Runs before HandleManagementDialogShortcuts because MythDialog
                // inherits ManagementDialogController — the generic Esc handler
                // would otherwise close it before the reader saw the key.
                if ((screen is MythDialogController
                        || screen is InfoDialogController
                        || screen is ManualDialogController)
                    && Lore.LoreDialogDispatcher.HandleInput(screen))
                {
                    return;
                }

                // --- ChooseGame: Delete-key handling for save deletion ---
                if (screen is ChooseGameController)
                {
                    if (HandleChooseGameKeys(screen as ChooseGameController))
                        return;
                }

                // --- Relations: F to cycle the clan filter ---
                // Runs before activation/list handlers so F works in any mode.
                if (screen is RelationsScreenController)
                {
                    if (HandleRelationsKeys(screen as RelationsScreenController))
                        return;
                }

                // --- War: F/R/O/C/W open Fortify/Raid/HonorRaid/CattleRaid/Warriors
                //     dialogs. Tab cycle is empty by design here — the five action
                //     buttons are filtered out in NavSlotCollector because their
                //     labels are inconsistent and disabled ones disappear silently.
                if (screen is WarScreenController)
                {
                    if (WarScreenHandler.HandleKeys(screen as WarScreenController))
                        return;
                }

                // --- Venture: D for venture description ---
                // Runs before list-mode handler so D in the venture list reads the
                // venture explanation (PC_VentureListItemExplanation) instead of
                // falling into the generic clan-description path, which only knows
                // clan indices and would say "No description available".
                if (screen is VentureDialogController)
                {
                    if (HandleVentureKeys(screen as VentureDialogController))
                        return;
                }

                // --- Generic ManagementDialog handler (Model Y): a blank Enter
                //     completes the dialog, Space acts on the focused element,
                //     Escape closes. Catches the four dialogs that inherit
                //     ManagementDialogController but have no dedicated navigator —
                //     Fortify / Build / Ritual / Venture. Runs FIRST so Enter and
                //     Escape win over the list-mode handler from inside any zone.
                if (HandleManagementDialogShortcuts(screen))
                    return;

                // --- List mode navigation (handled exclusively when active) ---
                // Must run BEFORE HandleActivationKeys: otherwise pressing Enter on a
                // focused list item also triggers TryActivatePrimaryButton, which fires
                // the only available button (e.g. SEND in EmissaryDialog) at the same
                // time as the list selection. Result: emissary sent to the wrong clan
                // and the list selection collides with mid-transition state.
                if (_inListMode && _activeList != null)
                {
                    HandleListNavigation(screen);
                    return;
                }

                // D fallback: read the focused element's Rollover/TextRollover tooltip.
                // Specialized D-handlers (clan list, venture, magic screen) already returned
                // above; this catches every OTHER button that has a hover-only tooltip
                // attached in the prefab so keyboard users can hear it on demand.
                if (HandleFocusedTooltipKey())
                    return;

                // --- Activation keys ---
                HandleActivationKeys(screen);

                // --- Standard button navigation ---
                HandleButtonNavigation(screen);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav", ex);
            }
        }

        private void ChangeSliderValue(int direction)
        {
            if (_sliderFocusIndex < 0 || _sliderFocusIndex >= _sliders.Count) return;

            UISlider slider = _sliders[_sliderFocusIndex];
            if (slider == null || !slider.IsInteractable()) return;

            float step = slider.wholeNumbers ? 1f : (slider.maxValue - slider.minValue) * 0.1f;

            // Hold Shift for larger steps (5x)
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                step *= 5f;

            // Hold Ctrl for max/min jump
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                slider.value = direction > 0 ? slider.maxValue : slider.minValue;
            }
            else
            {
                slider.value = Mathf.Clamp(slider.value + direction * step, slider.minValue, slider.maxValue);
            }

            AnnounceSliderValue(slider);
        }

        /// <summary>Speak the slider currently held in _sliderFocusIndex (the one Left/Right
        /// adjusts). Called from AnnounceFocusedButton when MoveFocus has cycled focus
        /// onto a slider via Up/Down.</summary>
        private void AnnounceFocusedSlider()
        {
            if (_sliderFocusIndex < 0 || _sliderFocusIndex >= _sliders.Count) return;

            UISlider slider = _sliders[_sliderFocusIndex];
            if (slider == null) return;
            string label = slider.label != null ? Loc.Get(slider.label.text) : Loc.Get("Amount");

            ScreenReader.Say(label + ". " + (int)slider.value + Loc.Get(" of ") + (int)slider.maxValue + Loc.Get(". Left and Right to change."));
        }

        private void AnnounceSliderValue(UISlider slider)
        {
            string label = slider.label != null ? Loc.Get(slider.label.text) : Loc.Get("Amount");
            ScreenReader.Say(label + " " + (int)slider.value + Loc.Get(" of ") + (int)slider.maxValue);
        }

        private void AnnounceAllSliders()
        {
            _sliders.RemoveAll(s => s == null || !s.gameObject.activeSelf);
            if (_sliders.Count == 0)
            {
                ScreenReader.Say(Loc.Get("No sliders."));
                return;
            }

            string output = _sliders.Count + Loc.Get(" sliders: ");
            foreach (var slider in _sliders)
            {
                string label = slider.label != null ? Loc.Get(slider.label.text) : Loc.Get("Amount");
                output += label + " " + (int)slider.value + Loc.Get(" of ") + (int)slider.maxValue + ", ";
            }
            ScreenReader.Say(output.TrimEnd(',', ' '));
        }

        /// <summary>Collect the active sliders for the current screen into a buffer.
        /// On InteractiveController screens (the scene-side slider choices like
        /// kChooseEscort) the canonical source is textPanel.sliders — TextPanel.AddSlider
        /// pushes there, and that list survives parent-active-state quirks that hide
        /// sliders from GetComponentsInChildren. Other screen types (dialogs without
        /// a TextPanel) fall back to the component scan.</summary>
        private void CollectSliders(ScreenController screen, List<UISlider> dest)
        {
            dest.Clear();
            if (screen == null) return;

            var ic = screen as InteractiveController;
            if (ic != null && ic.textPanel != null && ic.textPanel.sliders != null)
            {
                foreach (var s in ic.textPanel.sliders)
                {
                    if (s != null && s.gameObject.activeSelf && s.IsInteractable())
                        dest.Add(s);
                }
                return;
            }

            UISlider[] found = screen.GetComponentsInChildren<UISlider>(false);
            foreach (var s in found)
            {
                if (s != null && s.gameObject.activeSelf && s.IsInteractable())
                    dest.Add(s);
            }
        }

        private void RefreshSliders(ScreenController screen)
        {
            _sliderFocusIndex = -1;
            CollectSliders(screen, _sliders);
            if (_sliders.Count > 0)
                DebugLogger.Log("KeyboardNav", $"Found {_sliders.Count} sliders on {screen.name}");
        }

        private void RefreshSlidersSoft(ScreenController screen)
        {
            if (screen == null) return;

            // Reuse a cached buffer to avoid allocating per-frame in the soft-refresh tick.
            if (_softSliderBuffer == null) _softSliderBuffer = new List<UISlider>(8);
            CollectSliders(screen, _softSliderBuffer);

            if (_softSliderBuffer.Count == _sliders.Count)
            {
                bool same = true;
                for (int i = 0; i < _sliders.Count; i++)
                {
                    if (_sliders[i] != _softSliderBuffer[i]) { same = false; break; }
                }
                if (same) return;
            }

            int prevCount = _sliders.Count;
            _sliders.Clear();
            for (int i = 0; i < _softSliderBuffer.Count; i++) _sliders.Add(_softSliderBuffer[i]);

            if (_sliderFocusIndex >= _sliders.Count)
                _sliderFocusIndex = _sliders.Count - 1;
            if (_focusOnSlider && _sliderFocusIndex < 0)
                _focusOnSlider = false;

            if (_sliders.Count != prevCount)
                DebugLogger.Log("KeyboardNav", $"Soft slider refresh — now {_sliders.Count} sliders on {screen.name}");
        }
        private List<UISlider> _softSliderBuffer;

        // ============================================================
        // List navigation
        // ============================================================

        private void HandleListNavigation(ScreenController screen)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {

                MoveListFocus(-1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {

                MoveListFocus(1);
                return;
            }

            // Space — Model Y: select the focused list row. On the generic
            // ManagementDialogs (Fortify / Ritual / Venture) this is the explicit
            // "pick this one" key — Enter then completes the dialog. Handled here
            // because HandleManagementDialogShortcuts only takes Space outside a
            // list; inside a list, selecting the row is the right Space action.
            if (Input.GetKeyDown(KeyCode.Space) && screen is ManagementDialogController)
            {
                ActivateListItem();
                return;
            }

            // Enter — the list's main action for screens that still commit on
            // Enter (scene list choices, ChooseGame saves). On Relations it opens
            // the emissary dialog for the focused clan. The generic
            // ManagementDialogs never reach here — HandleManagementDialogShortcuts
            // consumes their Enter before the list-mode handler runs.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (screen is RelationsScreenController relForEnter)
                {
                    SelectFocusedListItemSilently();
                    TryOpenEmissaryDialog(relForEnter);
                    return;
                }
                ActivateListItem();
                return;
            }

            // D â€” read clan description (full first, then cycle paragraphs)
            if (Input.GetKeyDown(KeyCode.D))
            {
                CycleFocusedClanDescription();
                return;
            }

            // FortifyDialog has TWO interactive lists (buildable + existing). Tab
            // here means "switch to the other list" rather than the generic
            // "leave list mode" — neither list is reachable through the flat Tab
            // navigation otherwise. Escape still leaves; Shift+Tab also rotates
            // back the same way (only two lists, so direction doesn't matter).
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                FortifyDialogController fd = GetActiveScreen() as FortifyDialogController;
                if (fd != null && fd.buildable != null && fd.fortifications != null
                    && fd.buildable.count > 0 && fd.fortifications.count > 0)
                {
                    UIList other = System.Object.ReferenceEquals(_activeList, fd.buildable)
                        ? fd.fortifications : fd.buildable;
                    _activeList = other;
                    _listFocusIndex = other.selectedIndex >= 0 ? other.selectedIndex : 0;
                    string zoneName = System.Object.ReferenceEquals(other, fd.buildable)
                        ? "Buildable fortifications" : "Existing fortifications";
                    ScreenReader.Say(zoneName + ".");
                    AnnounceListItem();
                    return;
                }
            }

            // Escape / Tab — leave list mode (default behaviour for everything that
            // isn't a special-case list-pair like Fortify above).
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))
            {
                _inListMode = false;
                _clanDescLastClanIndex = -1; // reset cycle so next D on a clan starts fresh
                if (NavCount > 0)
                {
                    _focusIndex = 0;
                    AnnounceFocusedButton();
                }
                else
                {
                    ScreenReader.Say("Left list. No buttons available.");
                }
            }
        }

        /// <summary>
        /// Read the description of the currently focused clan item.
        /// First press reads the full text; subsequent presses cycle paragraphs.
        /// Resets when focusing a different clan or after inactivity.
        /// </summary>
        private void CycleFocusedClanDescription()
        {
            if (_activeList == null || _listFocusIndex < 0 || _listFocusIndex >= _activeList.count)
                return;

            UIListItem item = _activeList[_listFocusIndex];
            UIListItemWithIcons icons = item as UIListItemWithIcons;
            if (icons == null || icons.key <= 0)
            {
                ScreenReader.Say("No description available.");
                return;
            }

            int clanIndex = icons.key;
            Clan clan;
            try { clan = Clan.ClanWithIndex(clanIndex); }
            catch
            {
                ScreenReader.Say("No description available.");
                return;
            }

            if (clan.isNull)
            {
                ScreenReader.Say("No description available.");
                return;
            }

            // Reset on different clan or inactivity
            bool resetCycle = clanIndex != _clanDescLastClanIndex
                || Time.unscaledTime - _clanDescTime > ClanDescCycleResetTimeout;

            if (resetCycle)
            {
                string explanation = null;
                try { explanation = clan.ExplanationWithDetail(2); }
                catch (Exception ex) { DebugLogger.Error("KeyboardNav.ExplanationWithDetail", ex); }
                if (string.IsNullOrEmpty(explanation))
                {
                    ScreenReader.Say(clan.name + ": no description available.");
                    return;
                }
                _clanDescParagraphs = SplitParagraphs(explanation);
                _clanDescLastClanIndex = clanIndex;
                _clanDescCycleIndex = -1;
            }

            _clanDescTime = Time.unscaledTime;
            int total = _clanDescParagraphs.Length;

            _clanDescCycleIndex++;
            if (_clanDescCycleIndex > total) _clanDescCycleIndex = 0;

            // Single paragraph: never cycle, always speak the whole text
            if (total <= 1 || _clanDescCycleIndex == 0)
            {
                string joined = string.Join(" ", _clanDescParagraphs);
                if (total > 1)
                    ScreenReader.Say(joined + " Press D again for paragraph by paragraph, " + total + " in total.");
                else
                    ScreenReader.Say(joined);
                return;
            }

            string paragraph = _clanDescParagraphs[_clanDescCycleIndex - 1];
            ScreenReader.Say(paragraph);
        }

        /// <summary>Split text into paragraphs at blank lines. Empty entries removed.</summary>
        private static string[] SplitParagraphs(string text)
        {
            if (string.IsNullOrEmpty(text)) return new string[0];

            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] raw = normalized.Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

            var result = new List<string>();
            foreach (var p in raw)
            {
                string trimmed = p.Trim(new char[] { '\n', ' ', '\t' });
                trimmed = StringHelpers.StripTags(trimmed);
                if (!string.IsNullOrEmpty(trimmed))
                    result.Add(trimmed);
            }

            // Fallback: if nothing split (no blank lines), treat whole text as one paragraph
            if (result.Count == 0)
            {
                string trimmed = StringHelpers.StripTags(normalized.Trim());
                if (!string.IsNullOrEmpty(trimmed))
                    result.Add(trimmed);
            }

            return result.ToArray();
        }

        // ============================================================
        // Activation keys (Enter, Space, Escape â€” no cooldown)
        // ============================================================

        private void HandleActivationKeys(ScreenController screen)
        {
            // Enter â€” activate focused element, or collapse hint, or find primary button
            // Note: Toggles are NEVER activated by Enter â€” use Space to toggle.
            // Enter on a focused toggle falls through to find a primary/proceed button.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                // Question-header slot has no action â€” re-announce instead so the user
                // can dwell on the question without losing focus by pressing Enter.
                if (_focusOnHeader)
                {
                    AnnounceQuestionHeader();
                    return;
                }

                // Deltas-header slot likewise has no action; re-announce the
                // resource changes so the user can hear them again without
                // moving focus.
                if (_focusOnDeltas)
                {
                    AnnounceDeltasHeader();
                    return;
                }

                // Focused ResponseButton has priority
                if (IsResponseFocus)
                {
                    ActivateResponseButton(GetFocusedResponse());
                    return;
                }

                // Focused non-toggle Selectable
                Selectable focused = GetFocusedSelectable();
                if (focused != null && !(focused is UIToggle))
                {
                    ActivateButton(focused);
                    return;
                }

                // Otherwise: find a primary/proceed button to activate.
                TryActivatePrimaryButton(screen);
                return;
            }

            // Space â€” toggle focused element (toggles only). The conflicting
            // game-side Space â†’ HandleBackgroundToggle binding is dropped by
            // Patches/BackgroundTogglePatches; this handler doesn't need to
            // coordinate with it.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Selectable focused = GetFocusedSelectable();
                UIToggle toggle = focused as UIToggle;
                if (toggle != null)
                    ActivateButton(toggle);
                return;
            }

            // Escape â€” go back / close dialog
            if (Input.GetKeyDown(KeyCode.Escape))
            {

                TryGoBack(screen);
                return;
            }
        }

        // ============================================================
        // ChooseGame: Delete hotkey with two-press confirmation
        // ============================================================

        /// <summary>
        /// Handles ChooseGame-specific keys. Returns true if the key was consumed.
        /// First press of Delete on a save item asks for confirmation; a second
        /// press within DeleteConfirmTimeout actually deletes the save.
        /// </summary>
        private bool HandleChooseGameKeys(ChooseGameController cg)
        {
            // Expire any pending confirmation that wasn't followed up in time
            if (_pendingDeleteItem != null && Time.unscaledTime - _pendingDeleteTime > DeleteConfirmTimeout)
                _pendingDeleteItem = null;

            if (Input.GetKeyDown(KeyCode.Delete))
            {
                TryDeleteFocusedSave(cg);
                return true;
            }

            return false;
        }

        private void TryDeleteFocusedSave(ChooseGameController cg)
        {
            Selectable focused = GetFocusedSelectable();
            if (focused == null)
            {
                ScreenReader.Say("No save selected. Use arrow keys to choose one first.");
                return;
            }

            SaveListItem item = focused.GetComponentInParent<SaveListItem>();
            if (item == null || string.IsNullOrEmpty(item.folderUrl))
            {
                ScreenReader.Say("This entry cannot be deleted.");
                _pendingDeleteItem = null;
                return;
            }

            // Second press within the timeout â€” actually delete
            if (_pendingDeleteItem == item && Time.unscaledTime - _pendingDeleteTime < DeleteConfirmTimeout)
            {
                DebugLogger.Log("KeyboardNav", "Deleting save: " + item.folderUrl);
                cg.DeleteSave(item);
                ScreenReader.Say("Save deleted.");
                _pendingDeleteItem = null;
                RefreshButtons(cg);
                if (_focusIndex >= NavCount) _focusIndex = NavCount - 1;
                if (_focusIndex >= 0 && _focusIndex < NavCount)
                    AnnounceFocusedButton();
                return;
            }

            // First press â€” ask for confirmation
            _pendingDeleteItem = item;
            _pendingDeleteTime = Time.unscaledTime;

            string label = "this save";
            if (item.info != null && !string.IsNullOrEmpty(item.info.text))
                label = item.info.text;
            else if (item.button != null && item.button.label != null && !string.IsNullOrEmpty(item.button.label.text))
                label = item.button.label.text;

            ScreenReader.Say("Really delete: " + label + "? Press Delete again to confirm.");
        }

        // ============================================================
        // RelationsScreen: filter cycle (F) and emissary launch (E)
        // ============================================================

        /// <summary>
        /// Handle RelationsScreen-specific hotkeys. Returns true when a key was consumed.
        ///
        /// F (or Shift+F backwards) cycles the clan-filter dropdown. The dropdown's
        /// onValueChanged handler refills the clan list, so the user hears the new
        /// filter name and clan count after each press.
        ///
        /// Tab (when not already in list mode) enters the clan list. Opening the
        /// emissary dialog is Model Y's main action — Enter inside the clan list,
        /// handled in HandleListNavigation — so there is no separate hotkey here.
        /// </summary>
        private bool HandleRelationsKeys(RelationsScreenController rs)
        {
            // Skip when Ctrl/Alt is held â€” those are reserved for Ctrl+1-9 screen
            // switching and OS shortcuts. Shift is allowed (Shift+F = backward cycle).
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                return false;

            if (Input.GetKeyDown(KeyCode.F))
            {
                CycleRelationsFilter(rs);
                return true;
            }

            // Tab / Shift+Tab — toggle between the two zones (clan list and Emissary
            // button). RelationsScreen has exactly one list and one button, so a
            // generic Selectable Tab cycle gets stuck on the lone Emissary button
            // with no way back to the list. Handled here only when NOT in list mode;
            // HandleListNavigation owns Tab in the other direction.
            if (!_inListMode && Input.GetKeyDown(KeyCode.Tab)
                && rs.list != null && rs.list.isActiveAndEnabled && rs.list.count > 0)
            {
                _activeList = rs.list;
                _inListMode = true;
                _listFocusIndex = rs.list.selectedIndex >= 0 ? rs.list.selectedIndex : 0;
                _clanDescLastClanIndex = -1;
                ScreenReader.Say(Loc.Get("Clan list."));
                AnnounceListItem();
                return true;
            }

            return false;
        }

        private void CycleRelationsFilter(RelationsScreenController rs)
        {
            if (rs.filter == null || rs.filter.options == null || rs.filter.options.Count <= 1)
            {
                ScreenReader.Say(Loc.Get("No filter available on this screen."));
                return;
            }

            int dir = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? -1 : 1;
            int count = rs.filter.options.Count;
            int newVal = rs.filter.value + dir;
            if (newVal < 0) newVal = count - 1;
            if (newVal >= count) newVal = 0;

            // Setting filter.value triggers OnFilterChanged â†’ list.Fill() repopulates the list.
            rs.filter.value = newVal;

            string filterName = "";
            if (newVal >= 0 && newVal < rs.filter.options.Count)
                filterName = rs.filter.options[newVal].text;

            int clanCount = rs.list != null ? rs.list.count : 0;
            ScreenReader.Say(Loc.Get("Filter: ") + filterName + ". " + clanCount + Loc.Get(" clans listed."));
        }

        private void TryOpenEmissaryDialog(RelationsScreenController rs)
        {
            // ShowEmissaryDialog itself transitions to the EmissaryDialog screen â€” call it
            // even when the on-screen button is greyed out. The interactable flag of
            // emissaryButton is driven by ManagementController.UpdateAvailabilityForTutorial,
            // which disables every element except the one named in Tutorial.topicAvailableUI
            // while a trainer tutorial is active. Our own keyboard path does not need that
            // visual gating; the game's screen transition handles invalid states downstream.
            //
            // A clan must be selected â€” the dialog is meaningless without one, and the
            // game stores that selection on the list, not on the button. The Model Y
            // Enter path selects the focused clan first, so this guard only trips on
            // a genuinely empty list.
            if (rs.list == null || rs.list.selectedIndex < 0)
            {
                ScreenReader.Say(Loc.Get("No clans available."));
                return;
            }

            // If a trainer tutorial is currently steering the user toward a different element,
            // tell them what's expected â€” opening the emissary dialog anyway would just
            // dead-end inside the dialog with the same restriction.
            if (Tutorial.isTrainer)
            {
                string expected = Tutorial.topicAvailableUI;
                if (!string.IsNullOrEmpty(expected) && expected != "Emissary")
                {
                    ScreenReader.Say(Loc.Get("Tutorial expects you to use: ") + expected
                        + Loc.Get(". The emissary action is locked until then."));
                    return;
                }
            }

            DebugLogger.Log("KeyboardNav", "Opening Emissary dialog from Relations");
            rs.ShowEmissaryDialog();
        }

        // ============================================================
        // Generic ManagementDialog handler — Model Y (Enter / Space / Escape)
        // ============================================================

        /// <summary>
        /// Unified Model-Y input for the four ManagementDialogController-derived
        /// dialogs that do NOT have their own zone navigator — Fortify, Build,
        /// Ritual and Venture. The navigator-managed dialogs (Sacrifice, Emissary,
        /// Caravan, Reorganize, ChooseLeader, Map, Raid, Warriors, Spirit) handle
        /// their own keys inside their navigator dispatch and return before this
        /// runs, so in practice only those four ever reach here.
        ///
        /// <para>Keys, matching every dedicated navigator: a blank <b>Enter</b>
        /// completes the dialog (the primary action), <b>Space</b> acts on the
        /// focused element, <b>Escape</b> closes. Enter and Escape are handled
        /// globally — before the list-mode handler — so they work from inside a
        /// list. Space only fires here when NOT in a list; the list handler owns
        /// Space inside a list (it selects the focused row).</para>
        ///
        /// <para>The primary action is <c>actionButton</c> for Fortify / Ritual /
        /// Venture; Build has no <c>actionButton</c> so its primary action is the
        /// explicit <c>buildButton</c> (Reduce stays a flat-nav button reached by
        /// Tab + Space). The Close button is named per-dialog ("closeButton") and
        /// resolved via reflection, with a hierarchy-scan fallback for dialogs
        /// that wire their X icon purely in the prefab (Fortify).</para>
        /// </summary>
        private bool HandleManagementDialogShortcuts(ScreenController screen)
        {
            ManagementDialogController mdc = screen as ManagementDialogController;
            if (mdc == null) return false;

            // Enter â€” Model Y: the single key that completes the dialog. Handled
            // before the list-mode handler so it works from inside a list; a held
            // Ctrl is neither needed nor blocked. When in a list, commit the
            // focused row first so Enter always acts on the entry the user is
            // hearing rather than a stale default selection.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (_inListMode && _activeList != null)
                    SelectFocusedListItemSilently();

                UIButton ab = ResolveManagementActionButton(mdc);
                if (ab != null && ab.gameObject.activeSelf && ab.IsInteractable())
                {
                    DebugLogger.Log("KeyboardNav", "Enter -> action on " + mdc.GetType().Name);
                    ActivateButton(ab);
                }
                else if (ab != null && ab.label != null && !string.IsNullOrEmpty(ab.label.text))
                {
                    ScreenReader.Say(StringHelpers.StripTags(ab.label.text)
                        + Loc.Get(" is not available right now."));
                }
                else
                {
                    ScreenReader.Say(Loc.Get("Action is not available right now."));
                }
                return true;
            }

            // Space â€” Model Y: act on the focused element. Inside a list the
            // list-mode handler selects the focused row; here we cover the flat
            // button zone so Close / Choose Leader / Reduce stay reachable now
            // that Enter is reserved for the primary action.
            if (!_inListMode && Input.GetKeyDown(KeyCode.Space))
            {
                Selectable focused = GetFocusedSelectable();
                if (focused != null)
                {
                    DebugLogger.Log("KeyboardNav", "Space -> focused button on " + mdc.GetType().Name);
                    ActivateButton(focused);
                    return true;
                }
                // Nothing focused â€” fall through; nobody else acts on Space here.
            }

            // Escape â€” close, from any zone. Runs before the list-mode handler so
            // the user never has to press Escape twice (once to leave the list,
            // once to close); Model Y makes Escape the single close key.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // First try the C# field by name. Build and Venture expose
                // closeButton as a public UIButton field, which makes detection
                // trivial and guaranteed.
                UIButton closeBtn = FindUIButtonField(mdc, "closeButton");
                // Fallback: hierarchy scan keyed off onClick method name. Catches
                // Fortify, which wires the X-icon close purely in the prefab
                // without a C# reference. Same heuristic UIRoleResolver uses to
                // label the focused button "Close".
                if (closeBtn == null)
                    closeBtn = UIRoleResolver.FindCloseButton(mdc);

                if (closeBtn != null && closeBtn.gameObject.activeSelf && closeBtn.IsInteractable())
                {
                    DebugLogger.Log("KeyboardNav", "Escape -> close on " + mdc.GetType().Name);
                    ActivateButton(closeBtn);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolve the button that a blank Enter should fire on a generic
        /// ManagementDialog. Fortify / Ritual / Venture all wire the inherited
        /// <c>actionButton</c>; Build never does — its primary action is the
        /// explicit <c>buildButton</c> (the upgrade direction). Reducing a temple
        /// stays a separate flat-nav button reached by Tab + Space.
        /// </summary>
        private static UIButton ResolveManagementActionButton(ManagementDialogController mdc)
        {
            BuildDialogController build = mdc as BuildDialogController;
            if (build != null) return build.buildButton;
            return mdc.actionButton;
        }

        /// <summary>
        /// Walk the controller's inheritance chain looking for a public or
        /// non-public UIButton field with the given name. Returns the live value
        /// or null. Mirrors UIRoleResolver's reflection pattern but for a single
        /// targeted field name rather than a full sweep.
        /// </summary>
        private static UIButton FindUIButtonField(UnityEngine.MonoBehaviour controller, string fieldName)
        {
            if (controller == null || string.IsNullOrEmpty(fieldName)) return null;
            try
            {
                System.Type t = controller.GetType();
                System.Type uiButtonType = typeof(UIButton);
                // Mono 2.0 lacks Type.op_Inequality / FieldInfo.op_Inequality. Cast
                // both sides to object so the comparison goes through the universal
                // reference-equality operator instead of the missing overloads.
                System.Type monoBehaviourType = typeof(UnityEngine.MonoBehaviour);
                System.Type objectType = typeof(object);
                while ((object)t != null
                    && (object)t != (object)monoBehaviourType
                    && (object)t != (object)objectType)
                {
                    System.Reflection.FieldInfo f = t.GetField(fieldName,
                        System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.DeclaredOnly);
                    if ((object)f != null && uiButtonType.Equals(f.FieldType))
                        return f.GetValue(controller) as UIButton;
                    t = t.BaseType;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.FindUIButtonField", ex);
            }
            return null;
        }

        // ============================================================
        // VentureDialog: D reads the explanation for the focused venture
        // ============================================================

        /// <summary>
        /// Handle VentureDialog-specific hotkeys. Returns true when a key was consumed.
        ///
        /// D speaks the explanation text for the venture currently in focus. The text
        /// comes from PluginImport.PC_VentureListItemExplanation(index) â€” the same
        /// string the game writes into ventureDescription.text on selection. If the
        /// venture can't be performed right now, OnItemClicked replaces that text with
        /// "(reason)" via PC_CanPerformVentureWhyNot, so we read the live UI label
        /// (ventureDescription.text) which already carries whichever variant applies.
        /// </summary>
        private bool HandleVentureKeys(VentureDialogController vd)
        {
            // Skip when modifier is held â€” Ctrl/Alt+D may be used elsewhere.
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                return false;

            if (Input.GetKeyDown(KeyCode.D))
            {
                ReadVentureDescription(vd);
                return true;
            }
            return false;
        }

        private void ReadVentureDescription(VentureDialogController vd)
        {
            if (vd == null || vd.ventureList == null || vd.ventureList.count == 0)
            {
                ScreenReader.Say(Loc.Get("No ventures available."));
                return;
            }

            // Prefer the keyboard-focused item when the user is arrowing through the list,
            // otherwise the in-game selection (whatever was last Enter'd or default-selected).
            int index = -1;
            if (_inListMode && _activeList == vd.ventureList
                && _listFocusIndex >= 0 && _listFocusIndex < vd.ventureList.count)
            {
                index = _listFocusIndex;
            }
            else if (vd.ventureList.selectedIndex >= 0)
            {
                index = vd.ventureList.selectedIndex;
            }

            if (index < 0)
            {
                ScreenReader.Say(Loc.Get("Select a venture first."));
                return;
            }

            string name = null;
            string explanation = null;
            try { name = PluginImport.PC_VentureListItemName(index); }
            catch (Exception ex) { DebugLogger.Error("KeyboardNav.PC_VentureListItemName", ex); }
            try { explanation = PluginImport.PC_VentureListItemExplanation(index); }
            catch (Exception ex) { DebugLogger.Error("KeyboardNav.PC_VentureListItemExplanation", ex); }

            // If the focused venture happens to be the currently selected one, the on-screen
            // description box may already hold a "(why-not)" reason from OnItemClicked when
            // the venture is currently blocked â€” surface that in addition to the explanation.
            string whyNot = null;
            if (vd.ventureList.selectedIndex == index && vd.ventureDescription != null)
            {
                string live = vd.ventureDescription.text;
                if (!string.IsNullOrEmpty(live) && live.StartsWith("("))
                    whyNot = live;
            }

            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(name)) sb.Append(name).Append(". ");
            if (!string.IsNullOrEmpty(explanation)) sb.Append(explanation);
            if (!string.IsNullOrEmpty(whyNot))
            {
                if (sb.Length > 0) sb.Append(" ");
                sb.Append(whyNot);
            }

            string text = sb.ToString().Trim(' ', '.', '\t', '\r', '\n');
            if (string.IsNullOrEmpty(text))
            {
                ScreenReader.Say(Loc.Get("No description available."));
                return;
            }

            ScreenReader.Say(text);
        }

        /// <summary>
        /// Put the user straight into venture-list navigation when VentureDialog opens.
        /// Without this, Tab finds only the Start button (in tutorials it's the sole
        /// interactable Selectable) and arrows do nothing â€” the venture options are a
        /// UIList, invisible to Tab/arrow navigation outside list mode.
        /// </summary>
        private void AutoEnterVentureList(VentureDialogController vd)
        {
            try
            {
                if (vd == null || vd.ventureList == null) return;
                if (!vd.ventureList.isActiveAndEnabled || vd.ventureList.count == 0) return;

                _activeList = vd.ventureList;
                _inListMode = true;
                _listFocusIndex = vd.ventureList.selectedIndex >= 0 ? vd.ventureList.selectedIndex : 0;

                DebugLogger.Log("KeyboardNav", "Auto-entered venture list (" + vd.ventureList.count + " ventures)");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.AutoEnterVenture", ex);
            }
        }

        /// <summary>
        /// On a RitualDialog Tab press, intercept the wrap-around so the
        /// ritualList behaves like one virtual position in the cycle.
        /// Forward Tab at the last button enters list mode (positioned on the
        /// most recently focused ritual); Shift+Tab from the first button
        /// enters list mode at the tail. Returns true when the dispatcher
        /// should stop — MoveFocus is then skipped.
        /// </summary>
        private bool TryEnterRitualListOnTabWrap(ScreenController screen, bool shift)
        {
            RitualDialogController rd = screen as RitualDialogController;
            if (rd == null || rd.ritualList == null) return false;
            if (!rd.ritualList.isActiveAndEnabled || rd.ritualList.count == 0) return false;

            int navCount = NavCount;
            if (navCount == 0) return false;

            // Forward wrap: the user is at the last button (or past) — Tab
            // would loop back to button 0; route into the list instead.
            if (!shift && _focusIndex >= navCount - 1)
            {
                _activeList = rd.ritualList;
                _inListMode = true;
                int sel = rd.ritualList.selectedIndex;
                _listFocusIndex = sel >= 0 ? sel : 0;
                AnnounceListItem();
                return true;
            }
            // Backward wrap: Shift+Tab from the first button — enter the list
            // at its tail so the cycle order matches forward Tab in reverse.
            if (shift && _focusIndex <= 0)
            {
                _activeList = rd.ritualList;
                _inListMode = true;
                _listFocusIndex = rd.ritualList.count - 1;
                AnnounceListItem();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Drop the user straight into the Ritual dialog's list of myths so
        /// arrow keys are immediately useful — Tab still leaves list mode to
        /// reach the action / choose-leader / close buttons. Skipped silently
        /// if the list isn't ready yet (the next periodic soft refresh will
        /// pick it up; manual L still works as a fallback).
        /// </summary>
        private void AutoEnterRitualList(RitualDialogController rd)
        {
            try
            {
                if (rd == null || rd.ritualList == null) return;
                if (!rd.ritualList.isActiveAndEnabled || rd.ritualList.count == 0) return;

                _activeList = rd.ritualList;
                _inListMode = true;
                _listFocusIndex = rd.ritualList.selectedIndex >= 0 ? rd.ritualList.selectedIndex : 0;

                DebugLogger.Log("KeyboardNav", "Auto-entered ritual list (" + rd.ritualList.count + " rituals)");
                // No AnnounceListItem here — the dialog open hook
                // (DialogPatches.Ritual_Shown → ReadRitual) is queueing the
                // full announcement at roughly the same moment. Letting it
                // play first is friendlier; the user hears the per-ritual
                // detail as soon as they press an arrow key.
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.AutoEnterRitual", ex);
            }
        }

        /// <summary>
        /// Put the user straight into clan-list navigation when RelationsScreen activates.
        /// Up/Down then cycles clans, D reads description, Enter selects, Tab leaves list mode.
        /// Skipped silently if the list isn't ready yet (the next periodic soft refresh will
        /// pick it up â€” actual entry can also be triggered manually via L).
        /// </summary>
        private void AutoEnterRelationsClanList(RelationsScreenController rs)
        {
            try
            {
                if (rs == null || rs.list == null) return;
                if (!rs.list.isActiveAndEnabled || rs.list.count == 0) return;

                _activeList = rs.list;
                _inListMode = true;
                _listFocusIndex = rs.list.selectedIndex >= 0 ? rs.list.selectedIndex : 0;

                DebugLogger.Log("KeyboardNav", $"Auto-entered Relations clan list ({rs.list.count} clans)");
                // Don't AnnounceListItem here â€” the management-screen summary is being
                // announced from onScreenChanged at roughly the same moment. Letting the
                // summary play first is friendlier; the user hears the first clan as soon
                // as they press an arrow key.
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.AutoEnterRelations", ex);
            }
        }

        /// <summary>
        /// Auto-enter list mode on the active scene's textPanel.list. Called via
        /// <see cref="RequestEnterSceneList"/> when ScenePatches.NewChoice_Postfix
        /// detects a list-style ScriptChoice (kChooseClan, kChooseClans, kChooseDeity,
        /// kChooseSpirit, kChooseTreasure, kChooseLeader and friends).
        ///
        /// The game places the choices in textPanel.list (a UIList — not a Selectable)
        /// plus a proceedButton ResponseButton for committing. Without this hook the
        /// user reaches neither: UIList isn't in our flat _buttons collection and the
        /// proceedButton wasn't yet active during the prior refresh tick. Entering
        /// list mode lets Up/Down navigate the list; a subsequent Tab leaves list
        /// mode and exposes the now-active Proceed button for confirmation.
        /// </summary>
        private void AutoEnterSceneListChoice(ScreenController screen)
        {
            try
            {
                InteractiveController ic = screen as InteractiveController;
                if (ic == null || ic.textPanel == null) return;
                UIList list = ic.textPanel.list;
                if (list == null || !list.isActiveAndEnabled || list.count == 0)
                {
                    DebugLogger.Log("KeyboardNav", "AutoEnterSceneListChoice: list not ready (null/inactive/empty)");
                    return;
                }

                _activeList = list;
                _inListMode = true;
                _listFocusIndex = list.selectedIndex >= 0 ? list.selectedIndex : 0;
                _focusOnHeader = false;
                _focusOnSlider = false;
                _focusOnDeltas = false;

                DebugLogger.Log("KeyboardNav", $"Auto-entered scene choice list ({list.count} items)");
                // Don't announce here — the question caption was just spoken from
                // ScenePatches.NewChoice_Postfix and includes the navigation hint.
                // The first arrow press will trigger AnnounceListItem naturally.
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.AutoEnterSceneListChoice", ex);
            }
        }

        // ============================================================
        // Standard button navigation
        // ============================================================

        private void HandleButtonNavigation(ScreenController screen)
        {
            // Tab / Shift+Tab â€” cycle buttons
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (NavCount == 0 && !HasQuestionHeader() && !HasDeltasHeader())
                {
                    RefreshButtons(screen);
                    if (NavCount == 0 && !HasQuestionHeader() && !HasDeltasHeader()) return;
                }

                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                // RitualDialog: weave the ritualList into the Tab cycle so the
                // user can reach it without pressing L. Forward Tab at the last
                // button enters the list (preserving the most recently focused
                // ritual); Shift+Tab from the first button enters the list at
                // its tail. List-mode Tab handling in HandleListNavigation
                // already drops the user back into buttons, completing the loop.
                if (TryEnterRitualListOnTabWrap(screen, shift)) return;

                if (shift)
                    MoveFocus(-1);
                else
                    MoveFocus(1);
                return;
            }

            // Up/Down â€” always navigate buttons/toggles/responses/sliders, or fall back to list
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                int dir = Input.GetKeyDown(KeyCode.DownArrow) ? 1 : -1;

                // Sliders are part of the virtual cycle alongside the question header
                // and nav-order; without including _sliders.Count here, scenes that
                // only expose sliders (kChooseEscort and the dozen sibling slider
                // choices) leave the user stuck on the question header forever.
                if (NavCount > 0 || HasQuestionHeader() || HasDeltasHeader() || _sliders.Count > 0)
                {
                    MoveFocus(dir);
                    return;
                }

                // If no buttons/toggles/responses, try list mode
                UIList[] lists = screen.GetComponentsInChildren<UIList>(false);
                foreach (var l in lists)
                {
                    if (l.isActiveAndEnabled && l.count > 0)
                    {
                        _activeList = l;
                        _inListMode = true;
                        _listFocusIndex = l.selectedIndex >= 0 ? l.selectedIndex : 0;
                        AnnounceListItem();
                        return;
                    }
                }
                return;
            }

            // Left/Right â€” always adjust slider (no mode switch needed).
            // Also marks the slider zone as the active focus zone so a subsequent
            // Up/Down moves from the focused slider rather than from whatever
            // selectable was previously focused. Without this, pressing Down after
            // adjusting Goods landed on the wrapped Proceed→Goods cycle instead of
            // continuing to Herds — the Sacrifice screen lossy-cycle bug.
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (_sliders.Count > 0)
                {
                    if (_sliderFocusIndex < 0) _sliderFocusIndex = 0;
                    _focusOnSlider = true;
                    _focusOnHeader = false;
                    _focusOnDeltas = false;
                    _focusIndex = -1;
                    int dir = Input.GetKeyDown(KeyCode.RightArrow) ? 1 : -1;
                    ChangeSliderValue(dir);
                    return;
                }
            }

            // L â€” enter list mode explicitly
            if (Input.GetKeyDown(KeyCode.L))
            {

                TryEnterListMode(screen);
                return;
            }

            // H — read current tutorial hint page; subsequent presses cycle pages.
            if (Input.GetKeyDown(KeyCode.H))
            {
                TutorialHintHandler.Instance.HandleHKey();
                return;
            }
        }

        // ============================================================
        // Proceed button helper (for slider/list mode)
        // ============================================================

        private void TryActivateProceedButton(ScreenController screen)
        {
            // Look for a Proceed button
            RefreshButtons(screen);
            foreach (var btn in _buttons)
            {
                if (btn == null || !btn.isActiveAndEnabled) continue;
                string label = GetElementLabel(btn).ToLower();
                string name = btn.gameObject.name.ToLower();
                if (label.Contains("proceed") || name.Contains("proceed")
                    || label.Contains("done") || name.Contains("done")
                    || label.Contains("ok") || name.Contains("ok"))
                {
                    ActivateButton(btn);
                    return;
                }
            }

            // If only one button, press it
            _buttons.RemoveAll(b => b == null || !b.isActiveAndEnabled || !b.IsInteractable());
            if (_buttons.Count == 1)
            {
                ActivateButton(_buttons[0]);
                return;
            }

            ScreenReader.Say("No proceed button found. Press Tab to navigate buttons.");
        }


        // ============================================================
        // Core navigation helpers
        // ============================================================

        private ScreenController GetActiveScreen()
        {
            if (Singleton<ScreenManager>.isShuttingDown) return null;

            // Gate on Exists, not just isShuttingDown, before touching .instance.
            // The Game's Singleton<T>.instance getter has a broken auto-create
            // path (Singleton.cs line 30 dereferences instance_.gameObject before
            // the null-check) that throws NRE on the very first Update tick when
            // ScreenManager hasn't been instantiated yet. Exists is a cheap field
            // read that returns true only once instance_ has been validly set,
            // so we avoid triggering the broken path on cold start without
            // paying for FindObjectOfType every frame.
            if (!Singleton<ScreenManager>.Exists) return null;

            try
            {
                var sm = Singleton<ScreenManager>.instance;
                if (sm == null) return null;

                if (sm.dialogStack != null && sm.dialogStack.Count > 0)
                    return sm.dialogStack[sm.dialogStack.Count - 1];

                return sm.activeScreen;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.GetActiveScreen", ex);
                return null;
            }
        }

        private void RefreshButtons(ScreenController screen)
        {
            _buttons.Clear();
            _responseButtons.Clear();
            _navOrder.Clear();
            _focusIndex = -1;

            if (screen == null) return;

            NavSlotCollector.CollectButtonsInto(screen, _buttons);
            NavSlotCollector.SortByPosition(_buttons);
            NavSlotCollector.CollectResponseButtonsInto(screen, _responseButtons);
            RebuildNavOrder();

            DebugLogger.Log("KeyboardNav", $"Found {_buttons.Count} buttons + {_responseButtons.Count} responses on {screen.name}");
        }

        private void RefreshButtonsSoft(ScreenController screen)
        {
            if (screen == null) return;

            var newButtons = new List<Selectable>();
            NavSlotCollector.CollectButtonsInto(screen, newButtons);
            NavSlotCollector.SortByPosition(newButtons);

            var newResponses = new List<ResponseButton>();
            NavSlotCollector.CollectResponseButtonsInto(screen, newResponses);

            // Refresh on ANY change, compared by object identity — not just a change
            // in the total count. A count-only check is blind to "ABA" swaps where the
            // game destroys the old buttons and builds the same number of new ones in
            // one frame (e.g. answering a yes/no that leads straight into another
            // yes/no: 2 → 0 → 2). The stale _navOrder would then point at destroyed
            // objects and the user gets stuck on the question header. This mirrors how
            // RefreshSlidersSoft already detects slider changes.
            if (SameElements(_buttons, newButtons) && SameElements(_responseButtons, newResponses))
                return;

            _buttons = newButtons;
            _responseButtons = newResponses;

            RebuildNavOrder();

            if (_focusIndex >= NavCount)
                _focusIndex = NavCount - 1;

            DebugLogger.Log("KeyboardNav", $"Soft refresh â€” now {_buttons.Count} buttons + {_responseButtons.Count} responses");
        }

        /// <summary>
        /// True if both lists hold the same Unity objects in the same order. Used by the
        /// soft refresh to detect button-set changes by identity rather than by count,
        /// so a same-count swap (old set destroyed, equal-sized new set built) is still
        /// caught. Unity's overloaded == treats a destroyed object as unequal to a live
        /// one, so a stale cache entry always registers as a change.
        /// </summary>
        private static bool SameElements<T>(List<T> a, List<T> b) where T : UnityEngine.Object
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        /// <summary>
        /// Rebuild <see cref="_navOrder"/> from the current <see cref="_buttons"/>
        /// and <see cref="_responseButtons"/>, sorted by transform Y (top-first) so
        /// Tab and arrow navigation follows the visible top-to-bottom layout â€”
        /// regardless of which collection an element lives in.
        ///
        /// Preserves focus on the previously-focused element if it still exists,
        /// otherwise clamps the index to the new range.
        /// </summary>
        private void RebuildNavOrder()
        {
            NavSlot prev = (_focusIndex >= 0 && _focusIndex < _navOrder.Count)
                ? _navOrder[_focusIndex]
                : default(NavSlot);

            _navOrder.Clear();
            foreach (var s in _buttons)
            {
                if (s != null)
                    _navOrder.Add(new NavSlot { Selectable = s });
            }
            foreach (var r in _responseButtons)
            {
                if (r != null)
                    _navOrder.Add(new NavSlot { Response = r });
            }

            _navOrder.Sort(NavSlotCollector.CompareNavSlotsByPosition);

            if ((object)prev.Selectable != null || (object)prev.Response != null)
            {
                int newIdx = -1;
                for (int i = 0; i < _navOrder.Count; i++)
                {
                    if (_navOrder[i].Selectable == prev.Selectable
                        && _navOrder[i].Response == prev.Response)
                    {
                        newIdx = i;
                        break;
                    }
                }
                if (newIdx >= 0)
                    _focusIndex = newIdx;
                else if (_focusIndex >= _navOrder.Count)
                    _focusIndex = _navOrder.Count - 1;
            }
        }

        private Selectable GetFocusedSelectable()
        {
            if (_focusOnHeader || _focusOnDeltas) return null;
            if (_focusIndex < 0 || _focusIndex >= _navOrder.Count) return null;
            return _navOrder[_focusIndex].Selectable;
        }

        private void MoveFocus(int direction)
        {
            _buttons.RemoveAll(b => b == null || !b.isActiveAndEnabled || !b.IsInteractable());
            _responseButtons.RemoveAll(rb => !NavSlotCollector.IsResponseButtonUsable(rb));
            _sliders.RemoveAll(s => s == null || !s.gameObject.activeSelf || !s.IsInteractable());
            RebuildNavOrder();

            bool hasHeader = HasQuestionHeader();
            bool hasDeltas = HasDeltasHeader();
            int sliderCount = _sliders.Count;
            int navCount = NavCount;
            int total = sliderCount + navCount;

            if (total == 0)
            {
                // No items at all — only the header slots (if available) are
                // reachable. Prefer the question header; deltas alone is fine too.
                if (hasHeader)
                {
                    _focusOnHeader = true;
                    _focusOnDeltas = false;
                    _focusOnSlider = false;
                    _focusIndex = -1;
                    AnnounceFocusedButton();
                }
                else if (hasDeltas)
                {
                    _focusOnHeader = false;
                    _focusOnDeltas = true;
                    _focusOnSlider = false;
                    _focusIndex = -1;
                    AnnounceFocusedButton();
                }
                return;
            }

            // Virtual cycle layout (top→bottom in the UI):
            //   pos = -2 → question/result header (only when hasHeader)
            //   pos = -1 → deltas header           (only when hasDeltas)
            //   pos = 0..sliderCount-1   → slider at that index
            //   pos = sliderCount..total-1 → _navOrder[pos - sliderCount]
            int currentPos;
            if (_focusOnHeader)
                currentPos = -2;
            else if (_focusOnDeltas)
                currentPos = -1;
            else if (_focusOnSlider)
                currentPos = (_sliderFocusIndex >= 0 && _sliderFocusIndex < sliderCount) ? _sliderFocusIndex : 0;
            else if (_focusIndex >= 0 && _focusIndex < navCount)
                currentPos = sliderCount + _focusIndex;
            else
                currentPos = hasHeader ? -2 : (hasDeltas ? -1 : 0);

            int minPos = hasHeader ? -2 : (hasDeltas ? -1 : 0);
            int maxPos = total - 1;

            int newPos = currentPos + direction;
            if (newPos > maxPos) newPos = minPos;
            if (newPos < minPos) newPos = maxPos;

            // Skip empty header slots so the user doesn't land on a position
            // that has no content. Both slots can be absent independently;
            // walk in the current direction until we find a valid slot. With
            // total > 0 there's always at least one valid landing point.
            if (newPos == -2 && !hasHeader)
            {
                newPos = hasDeltas ? -1 : 0;
                if (direction < 0) newPos = maxPos;
            }
            if (newPos == -1 && !hasDeltas)
            {
                // Coming from below (direction -1) we wanted the header; if
                // that doesn't exist either, wrap to the last item.
                newPos = (direction < 0)
                    ? (hasHeader ? -2 : maxPos)
                    : 0;
            }

            if (newPos == -2)
            {
                _focusOnHeader = true;
                _focusOnDeltas = false;
                _focusOnSlider = false;
                _focusIndex = -1;
            }
            else if (newPos == -1)
            {
                _focusOnHeader = false;
                _focusOnDeltas = true;
                _focusOnSlider = false;
                _focusIndex = -1;
            }
            else if (newPos < sliderCount)
            {
                _focusOnHeader = false;
                _focusOnDeltas = false;
                _focusOnSlider = true;
                _sliderFocusIndex = newPos;
                _focusIndex = -1;
            }
            else
            {
                _focusOnHeader = false;
                _focusOnDeltas = false;
                _focusOnSlider = false;
                _focusIndex = newPos - sliderCount;
            }

            AnnounceFocusedButton();
        }

        /// <summary>True if _focusIndex points to a ResponseButton (rather than a Selectable).</summary>
        private bool IsResponseFocus
        {
            get
            {
                if (_focusOnHeader || _focusOnDeltas) return false;
                if (_focusIndex < 0 || _focusIndex >= _navOrder.Count) return false;
                return _navOrder[_focusIndex].IsResponse;
            }
        }

        private ResponseButton GetFocusedResponse()
        {
            if (!IsResponseFocus) return null;
            return _navOrder[_focusIndex].Response;
        }

        /// <summary>
        /// Find the chapter heading the given SaveListItem sits under by walking the
        /// game-list and tracking the most recent non-save header row before it. The
        /// chooseGame list interleaves UIListItem headers (no SaveListItem cast) with
        /// per-chapter SaveListItem entries; section breaks aren't stored on the item
        /// itself, so we re-derive them on demand. Returns null if the controller or
        /// the item couldn't be resolved.
        /// </summary>
        private static string GetChapterFor(SaveListItem target)
        {
            if (target == null) return null;
            ChooseGameController cg = target.controller;
            if (cg == null || cg.gameList == null) return null;

            string current = null;
            for (int i = 0; i < cg.gameList.count; i++)
            {
                UIListItem row = cg.gameList[i];
                if (row == null) continue;
                if (row is SaveListItem save)
                {
                    if (save == target) return current;
                }
                else
                {
                    current = string.IsNullOrEmpty(row.text) ? null : row.text;
                }
            }
            return null;
        }

        /// <summary>
        /// If the focused SaveListItem belongs to a different chapter than the one
        /// last announced, prepend "Kapitel X." to the parts list and update the
        /// tracker. No-op when the user stays inside the same chapter.
        /// </summary>
        private void MaybePrependChapterTransition(List<string> parts, SaveListItem saveItem)
        {
            string chapter = GetChapterFor(saveItem);
            if (string.IsNullOrEmpty(chapter)) return;
            if (chapter == _lastAnnouncedChapter) return;
            _lastAnnouncedChapter = chapter;
            parts.Add(Loc.Get("Chapter ") + chapter + ".");
        }

        /// <summary>
        /// Queue an announcement of the currently focused menu item after a screen-
        /// change header. Used for short flat menus — MainMenu, Settings overlay,
        /// Choose Game — where the user should hear the first interactive option
        /// without having to press an arrow key first. Queued (interrupt:false) so
        /// the preceding screen overview / change header is preserved.
        ///
        /// Mirrors the SaveListItem and UIToggle cases of <see cref="AnnounceFocusedButton"/>
        /// but with queued speech instead of an interrupt. The complex paths
        /// (response buttons, deltas/header focus modes, per-screen specials)
        /// don't apply on these flat menus and aren't replicated here.
        /// </summary>
        private void AnnounceInitialMenuFocus()
        {
            Selectable item = GetFocusedSelectable();
            if (item == null) return;
            string label = GetElementLabel(item);
            DebugLogger.Log("KeyboardNav", "Auto-focus -> index " + _focusIndex + "/" + NavCount + ": " + label);

            // ChooseGame: SaveListItem wraps the focused button. Speak clan/save name
            // (label.text), date+season (info.text) and the action button label —
            // same shape as the arrow-key path, just queued behind the overview.
            // Chapter heading is prepended for the very first save the auto-focus
            // lands on so the user immediately knows which chapter they're in.
            SaveListItem saveItem = item.GetComponentInParent<SaveListItem>();
            if (saveItem != null)
            {
                var parts = new List<string>();
                MaybePrependChapterTransition(parts, saveItem);
                if (saveItem.label != null && !string.IsNullOrEmpty(saveItem.label.text))
                    parts.Add(StringHelpers.FlattenWhitespace(saveItem.label.text));
                if (saveItem.info != null && !string.IsNullOrEmpty(saveItem.info.text))
                    parts.Add(StringHelpers.FlattenWhitespace(saveItem.info.text));
                parts.Add(Loc.Get(label));
                ScreenReader.Say(string.Join(", ", parts.ToArray()), interrupt: false);
                return;
            }

            UIToggle toggle = item as UIToggle;
            if (toggle != null)
            {
                ScreenReader.Say(Loc.Get(label) + ", " + Loc.Get(toggle.isOn ? "on" : "off"),
                    interrupt: false);
                return;
            }
            ScreenReader.Say(Loc.Get(label), interrupt: false);
        }

        private void AnnounceFocusedButton()
        {
            if (_focusOnHeader)
            {
                DebugLogger.Log("KeyboardNav", "Focus -> question header");
                AnnounceQuestionHeader();
                return;
            }

            if (_focusOnDeltas)
            {
                DebugLogger.Log("KeyboardNav", "Focus -> deltas header");
                AnnounceDeltasHeader();
                return;
            }

            if (_focusOnSlider)
            {
                DebugLogger.Log("KeyboardNav", "Focus -> slider " + _sliderFocusIndex + "/" + _sliders.Count);
                AnnounceFocusedSlider();
                return;
            }

            if (_focusIndex < 0 || _focusIndex >= NavCount)
            {
                DebugLogger.Log("KeyboardNav", "Focus out of range: index=" + _focusIndex + ", nav=" + NavCount);
                return;
            }
            DebugLogger.Log("KeyboardNav", "Focus -> index " + _focusIndex + "/" + NavCount);

            // ResponseButton (scene answer / start / proceed)
            if (IsResponseFocus)
            {
                ResponseButton rb = GetFocusedResponse();
                string text = GetResponseButtonLabel(rb);
                // Loc.Get translates static labels like "Proceed", "Done", "START" that
                // are baked into the Unity button and never flow through the translation
                // mod's PluginImport patches. Scene response text (e.g. "1.Wahrheit.") is
                // already German and won't match a Loc key, so Loc.Get returns it unchanged.
                string spoken = Loc.Get(text);
                // Combat options that carry a "chance" get the risk marker spoken first:
                // a trailing marker would be cut off when the user arrows on quickly, so
                // the most important word leads. CombatScreenReader captures which options
                // are risky when the combat-option list is built.
                if (CombatScreenReader.IsRiskyOption(rb))
                    spoken = Loc.Get("Risk: ") + spoken;
                ScreenReader.Say(spoken);
                return;
            }

            Selectable item = GetFocusedSelectable();
            if (item == null) return;
            string label = GetElementLabel(item);

            // Save list item â€” combine date/header (label.text), details (info.text), action button label.
            // Both label and info can contain literal "\n" — the info field always splits
            // "Iverlantho 10, Späte Seesaison" from "Zuletzt gespielt: …" on a new line.
            // The screen reader pauses awkwardly on embedded breaks; flatten them.
            SaveListItem saveItem = item.GetComponentInParent<SaveListItem>();
            if (saveItem != null)
            {
                var parts = new List<string>();
                // Prepend the chapter heading when the user has just crossed from one
                // chapter into another (or focused the first save). Without this cue
                // the section break ("Six Ages" → "Lights Going Out") is silent —
                // the arrow path otherwise reads only the per-save line, leaving the
                // user unsure which chapter they're scrolling through.
                MaybePrependChapterTransition(parts, saveItem);
                if (saveItem.label != null && !string.IsNullOrEmpty(saveItem.label.text))
                    parts.Add(StringHelpers.FlattenWhitespace(saveItem.label.text));
                if (saveItem.info != null && !string.IsNullOrEmpty(saveItem.info.text))
                    parts.Add(StringHelpers.FlattenWhitespace(saveItem.info.text));
                // Action button label ("RESUME", "TUTORIAL", "NEW GAME") is a static
                // Unity label, not routed through the translation pipeline — localize here.
                parts.Add(Loc.Get(label));

                ScreenReader.Say(string.Join(", ", parts.ToArray()));
                return;
            }

            // Clan name input field
            TMP_InputField inputField = item as TMP_InputField;
            if (inputField != null)
            {
                string current = string.IsNullOrEmpty(inputField.text) ? "empty" : "'" + inputField.text + "'";
                ScreenReader.Say("Clan name input, current value " + current + ". Press Enter to edit.");
                return;
            }

            UIToggle toggle = item as UIToggle;
            if (toggle != null)
            {
                string state = toggle.isOn ? "on" : "off";
                ScreenReader.Say(Loc.Get(label) + ", " + Loc.Get(state));
                return;
            }

            UIButton uiBtn = item as UIButton;

            // Relations Emissary button: speak the action plus current selection state
            // instead of the bare label "Emissary". Tab landing on this button without
            // a clan picked yet would otherwise be silent about why Enter does nothing.
            RelationsScreenController rs = GetActiveScreen() as RelationsScreenController;
            if (uiBtn != null && rs != null && rs.emissaryButton != null
                && System.Object.ReferenceEquals(uiBtn, rs.emissaryButton))
            {
                int sel = rs.list != null ? rs.list.selectedIndex : -1;
                string clanText = null;
                if (sel >= 0 && rs.list != null && rs.list[sel] != null)
                    clanText = StringHelpers.StripTags(rs.list[sel].text ?? "");

                if (!string.IsNullOrEmpty(clanText))
                    ScreenReader.Say(Loc.Get("Send emissary to ") + clanText + Loc.Get(". Press Enter to open the emissary dialog."));
                else
                    ScreenReader.Say(Loc.Get("Send emissary, disabled. Select a clan first."));
                return;
            }

            // War-screen action buttons: use a normalised label since the on-screen
            // ones are inconsistent ("FortifyButton" GameObject name vs "HERD RAID"
            // ALLCAPS vs Pascal-case "Warriors"). The five buttons all share the same
            // onClick handler shape, so WarScreenHandler can identify them and return
            // a clean label like "Fortify" / "Cattle raid".
            WarScreenController war = GetActiveScreen() as WarScreenController;
            if (uiBtn != null && war != null)
            {
                string warLabel = WarScreenHandler.GetActionLabel(uiBtn, war);
                if (!string.IsNullOrEmpty(warLabel))
                {
                    ScreenReader.Say(warLabel);
                    return;
                }
            }

            // Map mission marker (Dot2(Clone)): UIButton attached to a MapElement,
            // representing a clan that has an available mission. Announce the mission
            // explanation text so the user knows which clan/mission this marker is for.
            if (uiBtn != null && Patches.MapPatches.IsMissionMarker(uiBtn))
            {
                MapController mc = GetActiveScreen() as MapController;
                if (mc != null)
                {
                    string explain = Patches.MapPatches.GetMissionExplanation(mc, uiBtn);
                    if (!string.IsNullOrEmpty(explain))
                    {
                        ScreenReader.Say(explain);
                        return;
                    }
                }
            }

            // Icon-only buttons (X-icon close, action button, leader-picker, ...) have
            // no UILabel text, so GetElementLabel falls back to GameObject.name —
            // which yields prefab-internal names like "CloseButton2". Resolve via the
            // game's actual semantics: controller field role first ("closeButton" →
            // "Close", "actionButton" → its live label like RECRUIT/DISMISS), then
            // the button's Rollover tooltip (the same text shown to sighted users
            // on hover). Only when both come up empty do we keep the GameObject name.
            if (uiBtn != null)
            {
                string resolved = UIRoleResolver.ResolveButtonLabel(uiBtn, GetActiveScreen());
                if (!string.IsNullOrEmpty(resolved))
                {
                    ScreenReader.Say(Loc.Get(resolved));
                    return;
                }
            }

            ScreenReader.Say(Loc.Get(label));
        }

        private string GetResponseButtonLabel(ResponseButton rb)
        {
            if (rb == null) return "(unknown)";
            // The label is enriched with rich-text and shortcut prefixes ("1.<indent>...");
            // strip tags to keep speech clean.
            if (rb.label != null && !string.IsNullOrEmpty(rb.label.text))
                return StringHelpers.StripTags(rb.label.text);
            return rb.gameObject.name;
        }

        private string GetElementLabel(Selectable item)
        {
            UIButton btn = item as UIButton;
            if (btn != null && btn.label != null && !string.IsNullOrEmpty(btn.label.text))
                return Loc.Get(StringHelpers.FlattenWhitespace(btn.label.text));

            UIToggle toggle = item as UIToggle;
            if (toggle != null && toggle.label != null && !string.IsNullOrEmpty(toggle.label.text))
                return Loc.Get(StringHelpers.FlattenWhitespace(toggle.label.text));

            return item.gameObject.name;
        }

        // ============================================================
        // Question header â€” virtual nav slot above the response options
        // ============================================================

        /// <summary>
        /// Whether the active screen exposes a "question header" slot above the
        /// response options. Currently true for any InteractiveController-derived
        /// scene/intro/quest screen with non-empty Script_Text or Script_Caption
        /// â€” the same text auto-announced at scene entry.
        /// </summary>
        private bool HasQuestionHeader()
        {
            if (!(_lastScreen is InteractiveController)) return false;
            return !string.IsNullOrEmpty(GetQuestionHeaderText());
        }

        /// <summary>
        /// Body text for the question-header slot. Two states:
        /// <list type="bullet">
        /// <item>Pre-answer (scene still has interactive responses): Script_Caption + Script_Text â€” the question and any subtitle.</item>
        /// <item>Post-answer (SceneFinal has fired, only Proceed remains): Script_ResultText only. Script_Text at this point also contains the original question (textPanel.AddText appended the result onto the body), so reading it would echo the answered question back at the user.</item>
        /// </list>
        /// </summary>
        private string GetQuestionHeaderText()
        {
            try
            {
                if (Patches.ScenePatches.IsSceneFinalized)
                {
                    string result = PluginImport.Script_ResultText();
                    if (!string.IsNullOrEmpty(result)) return result;
                    // Finalized but no result text â€” nothing meaningful to re-read in
                    // the header slot. Returning null disables the slot.
                    return null;
                }

                string caption = PluginImport.Script_Caption();
                string text = PluginImport.Script_Text();
                bool hasCaption = !string.IsNullOrEmpty(caption);
                bool hasText = !string.IsNullOrEmpty(text);
                if (hasCaption && hasText) return caption + ". " + text;
                if (hasCaption) return caption;
                if (hasText) return text;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.HeaderText", ex);
            }
            return null;
        }

        private void AnnounceQuestionHeader()
        {
            string text = GetQuestionHeaderText();
            if (string.IsNullOrEmpty(text))
            {
                ScreenReader.Say("No question text.");
                return;
            }
            // BattleResults always shows a narrative outcome, never a question
            // — its InitializeFromScript clears _isSceneFinalized so the
            // generic prefix logic would otherwise treat it as a question.
            // Force "Result:" there to match what the screen actually contains.
            // The question prefix is intentionally empty: user feedback —
            // story texts read better without a "Frage:" lead-in.
            string prefix;
            if (_lastScreen is BattleResultsController)
                prefix = Loc.Get("Result: ");
            else
                prefix = Patches.ScenePatches.IsSceneFinalized ? Loc.Get("Result: ") : string.Empty;
            ScreenReader.Say(prefix + text);
        }

        /// <summary>
        /// Is the deltas-header slot currently populated? True when the most
        /// recent ShowDeltas pass produced any resource-change text and we
        /// haven't yet moved to a new scene that cleared it.
        /// </summary>
        private bool HasDeltasHeader()
        {
            if (!(_lastScreen is InteractiveController)) return false;
            return !string.IsNullOrEmpty(Patches.ScenePatches.LastDeltaAnnouncement);
        }

        /// <summary>Speak the latest "Resource changes: ..." line as a re-readable slot.</summary>
        private void AnnounceDeltasHeader()
        {
            string text = Patches.ScenePatches.LastDeltaAnnouncement;
            if (string.IsNullOrEmpty(text))
            {
                ScreenReader.Say("No resource changes.");
                return;
            }
            ScreenReader.Say(text);
        }

        /// <summary>Activate a button on behalf of an extracted helper (e.g. MagicScreenNavigator).</summary>
        internal void ActivateButtonExternal(Selectable item) { ActivateButton(item); }

        private void ActivateButton(Selectable item)
        {
            if (item == null || !item.isActiveAndEnabled) return;

            string label = GetElementLabel(item);
            DebugLogger.Log("KeyboardNav", $"Activating '{label}'");

            // TMP_InputField: enter edit mode instead of triggering submit.
            // The user types the clan name, then presses Tab/Escape to leave the field.
            TMP_InputField inputField = item as TMP_InputField;
            if (inputField != null)
            {
                EventSystem.current.SetSelectedGameObject(inputField.gameObject);
                inputField.ActivateInputField();
                _userActivatedTextField = true;
                ScreenReader.Say("Editing clan name. Type the name, then press Tab to leave the field.");
                return;
            }

            // Capture toggle state before submit so we can detect a no-op flip.
            // UIToggle.Set silently reverts deselection when the toggle is the only
            // "on" member of its UIToggleGroup and the group disallows switch-off.
            // Without this check the post-submit announcement would falsely repeat
            // the unchanged state, leaving the user thinking the keypress was lost.
            UIToggle toggle = item as UIToggle;
            bool wasOn = toggle != null && toggle.isOn;
            UIToggleGroup group = toggle != null ? toggle.group : null;

            ISubmitHandler handler = item as ISubmitHandler;
            if (handler != null)
            {
                var eventData = new BaseEventData(EventSystem.current);
                handler.OnSubmit(eventData);
            }

            // Announce new toggle state after activation
            if (toggle != null)
            {
                bool isOn = toggle.isOn;
                if (wasOn == isOn && group != null && wasOn)
                {
                    // Radio-button group, current toggle is the active one and the
                    // game refused to deselect it â€” there's no sibling "on", so a Space
                    // press would always bounce back. Tell the user this explicitly.
                    ScreenReader.Say(Loc.Get(label) + Loc.Get(" stays on. This is a radio option — to switch, press Tab to find a different option in the same group, then Space there."));
                }
                else
                {
                    ScreenReader.Say(Loc.Get(label) + " " + Loc.Get(isOn ? "on" : "off"));
                }
            }
        }

        private void TryActivatePrimaryButton(ScreenController screen)
        {
            string[] primaryNames = { "continue", "ok", "accept", "play", "start", "done", "proceed" };

            foreach (var btn in _buttons)
            {
                if (btn == null || !btn.isActiveAndEnabled) continue;
                if (btn is UIToggle) continue; // never auto-activate toggles via Enter

                string label = GetElementLabel(btn).ToLower();
                string name = btn.gameObject.name.ToLower();

                foreach (string primary in primaryNames)
                {
                    if (label.Contains(primary) || name.Contains(primary))
                    {
                        ActivateButton(btn);
                        return;
                    }
                }
            }

            // Look for active ResponseButtons on the scene's textPanel.
            // These are not part of _buttons (ResponseButton is not a Selectable),
            // so we have to check them explicitly. Used for the Restore selector,
            // SceneFinal/Proceed screens, etc.
            InteractiveController ic = screen as InteractiveController;
            if (ic != null && ic.textPanel != null)
            {
                if (TryClickResponseButton(ic.textPanel.startButton, "Start")) return;
                if (TryClickResponseButton(ic.textPanel.proceedButton, "Proceed")) return;
            }

            // Fallback: if exactly one non-toggle button is available, activate it.
            Selectable singleButton = null;
            int nonToggleCount = 0;
            foreach (var b in _buttons)
            {
                if (b == null || !b.isActiveAndEnabled || !b.IsInteractable()) continue;
                if (b is UIToggle) continue;
                singleButton = b;
                nonToggleCount++;
            }
            if (nonToggleCount == 1)
            {
                ActivateButton(singleButton);
                return;
            }

            ScreenReader.Say(_buttons.Count + " buttons available. Press Tab to navigate.");
        }

        /// <summary>Click a ResponseButton if it is active and interactable. Returns true if clicked.</summary>
        private bool TryClickResponseButton(ResponseButton rb, string fallbackLabel)
        {
            if (!NavSlotCollector.IsResponseButtonUsable(rb)) return false;

            string label = (rb.label != null && !string.IsNullOrEmpty(rb.label.text)) ? rb.label.text : fallbackLabel;
            DebugLogger.Log("KeyboardNav", "Activating ResponseButton '" + label + "'");
            rb.OnClick();
            return true;
        }

        private void ActivateResponseButton(ResponseButton rb)
        {
            if (!NavSlotCollector.IsResponseButtonUsable(rb)) return;
            DebugLogger.Log("KeyboardNav", "Activating ResponseButton '" + GetResponseButtonLabel(rb) + "'");
            rb.OnClick();
        }

        // --- List helpers ---

        private void TryEnterListMode(ScreenController screen)
        {
            UIList[] lists = screen.GetComponentsInChildren<UIList>(false);
            UIList list = null;

            foreach (var l in lists)
            {
                if (l.isActiveAndEnabled && l.count > 0)
                {
                    list = l;
                    break;
                }
            }

            if (list == null)
            {
                ScreenReader.Say("No list on this screen.");
                return;
            }

            _activeList = list;
            _inListMode = true;
            _listFocusIndex = list.selectedIndex >= 0 ? list.selectedIndex : 0;

            AnnounceListItem();
        }

        private void MoveListFocus(int direction)
        {
            if (_activeList == null || _activeList.count == 0) return;

            _listFocusIndex += direction;
            if (_listFocusIndex >= _activeList.count) _listFocusIndex = 0;
            if (_listFocusIndex < 0) _listFocusIndex = _activeList.count - 1;

            // Don't set selection visually â€” just track the focus index.
            // selectionImage can be null on some item types (e.g. SaveListItem).

            AnnounceListItem();
        }

        private void AnnounceListItem()
        {
            if (_activeList == null || _listFocusIndex < 0 || _listFocusIndex >= _activeList.count)
                return;

            UIListItem item = _activeList[_listFocusIndex];

            SaveListItem saveItem = item as SaveListItem;
            if (saveItem != null)
            {
                string saveText = "";
                if (saveItem.button != null && saveItem.button.label != null)
                    saveText = saveItem.button.label.text;
                if (saveItem.info != null && !string.IsNullOrEmpty(saveItem.info.text))
                    saveText += ", " + saveItem.info.text;
                // Most-recent marker — ChooseGameController.OnShow assigns the
                // IconResumeGame_ sprite to the button icon for the save whose
                // chapterID matches Game_AppChapter (line 101 in decompiled).
                // The button label changes from "CONTINUE" to "OPEN" too, but
                // that wording on its own doesn't say "this is where you'll
                // resume" to a screen-reader user; an explicit suffix does.
                if (saveItem.button != null && saveItem.button.icon != null
                    && saveItem.button.icon.sprite != null
                    && (saveItem.button.icon.sprite.name ?? "").IndexOf("Resume", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    saveText += ", most recent";
                }
                ScreenReader.Say(saveText);
                return;
            }

            // Ritual list — fire the click so the game's OnItemSelected refills
            // ritualDescription / intervalText / actionButton.interactable for the
            // newly focused myth, then read the rich per-ritual state. Without
            // this, arrow keys only speak the ritual name and the dialog's
            // description panel stays frozen on whatever was selected last.
            ScreenController active = GetActiveScreen();
            RitualDialogController ritualDialog = active as RitualDialogController;
            if (ritualDialog != null && System.Object.ReferenceEquals(_activeList, ritualDialog.ritualList))
            {
                AnnounceRitualListItem(ritualDialog, item);
                return;
            }

            // Clan list item â€” read culture, proximity and visit/feud status as sighted players see via icons
            bool itemSelected = _activeList != null && _activeList.selectedIndex == _listFocusIndex;
            string clanText = TryFormatClanItem(item, itemSelected);
            if (clanText != null)
            {
                ScreenReader.Say(clanText);
                return;
            }

            // Venture list item — VentureDialogController.OnShow sets the
            // VentureRunningMan_ icon on entries where PC_VentureListIsInEffect
            // is true. Sighted players see the icon; surface it as "(running)"
            // so the user knows that picking this venture won't start a fresh
            // one (the game's Start button is disabled in that case).
            VentureDialogController ventureDialog = active as VentureDialogController;
            if (ventureDialog != null && System.Object.ReferenceEquals(_activeList, ventureDialog.ventureList))
            {
                string vtext = item.text ?? "";
                bool running = false;
                try { running = PluginImport.PC_VentureListIsInEffect(_listFocusIndex); }
                catch (Exception ex) { DebugLogger.Error("KeyboardNav.VentureInEffect", ex); }
                if (running) vtext += Loc.Get(" (running)");
                ScreenReader.Say(vtext);
                return;
            }

            string text = item.text ?? item.gameObject.name;
            ScreenReader.Say(text);
        }

        /// <summary>
        /// Build the Ritual-list announcement: ritual name, the per-myth
        /// quest summary, the why-not / interval text, and whether Start
        /// Ritual is currently available. Mirrors what
        /// <see cref="RitualDialogController.OnItemSelected"/> writes into
        /// the right-hand panel — values that would otherwise be silent.
        /// </summary>
        private void AnnounceRitualListItem(RitualDialogController d, UIListItem item)
        {
            try
            {
                // Drive the same path a mouse click would: fires
                // OnItemSelected → updates ritualDescription, intervalText
                // and actionButton.interactable for the newly focused myth.
                if (d.ritualList != null)
                    d.ritualList.OnItemClicked(item);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.AnnounceRitualListItem.OnItemClicked", ex);
            }

            var sb = new System.Text.StringBuilder();
            string name = StringHelpers.StripTags(item.text ?? item.gameObject.name);
            sb.Append(name);

            string desc = d.ritualDescription != null ? d.ritualDescription.text : "";
            if (!string.IsNullOrEmpty(desc))
                sb.Append(". ").Append(StringHelpers.StripTags(desc));

            string interval = d.intervalText != null ? d.intervalText.text : "";
            if (!string.IsNullOrEmpty(interval))
                sb.Append(". ").Append(StringHelpers.StripTags(interval));

            // The interval text already carries why-not reasons when the
            // button is disabled (e.g. "We performed a ritual this year",
            // "No leader is qualified..."); when the button is enabled we
            // surface the affirmative so the user knows Enter will fire.
            if (d.actionButton != null)
            {
                if (d.actionButton.interactable)
                    sb.Append(". Enter to perform.");
                else if (string.IsNullOrEmpty(interval))
                    sb.Append(". Cannot be performed right now.");
            }

            ScreenReader.Say(sb.ToString());
        }

        /// <summary>
        /// If the item represents a clan (UIListItemWithIcons with a valid clan key),
        /// return a spoken summary mirroring what sighted players see: name, selected
        /// marker, culture, "near" indicator, and visit/feud status. Returns null for
        /// non-clan items. <paramref name="isSelected"/> drives the "selected" marker,
        /// placed right after the name so fast arrow browsing can't cut it off.
        /// </summary>
        private string TryFormatClanItem(UIListItem item, bool isSelected)
        {
            UIListItemWithIcons icons = item as UIListItemWithIcons;
            if (icons == null || icons.key <= 0) return null;

            Clan clan;
            try { clan = Clan.ClanWithIndex(icons.key); }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.ClanWithIndex", ex);
                return null;
            }

            if (clan.isNull || string.IsNullOrEmpty(clan.name)) return null;

            // Lead with the bare name so the trainer-expected-marker (if any)
            // attaches directly after the name and is unambiguous about which
            // item the trainer wants. ClanDataSource's OnClanSelected passes
            // clan.name (not the prettified label) to CheckForAdvanceFromElement,
            // so comparing against clan.name is the right key.
            string headline = clan.name + TrainerInfo.MarkIfRequired(clan.name);
            var parts = new List<string> { headline };

            // Selected marker right after the name — heard even when fast arrow
            // browsing interrupts the announcement.
            if (isSelected) parts.Add(Loc.Get("selected"));

            // Attitude (hostile/unfriendly/neutral/friendly/allied/tribe/our) — the
            // map's clan-name colour, surfaced for screen-reader users. The helper
            // also folds in clan.inOurTribe (underline on the map) so we don't
            // re-emit it as a separate part below.
            parts.Add(StringHelpers.AttitudeLabel(clan.attitudeColor, clan.inOurTribe));

            switch (clan.culture)
            {
                case Culture.culture_Chariot:   parts.Add(Loc.Get("Chariot")); break;
                case Culture.culture_Hyaloring: parts.Add(Loc.Get("Hyaloring")); break;
                case Culture.culture_Orlanthi:  parts.Add(Loc.Get("Orlanthi")); break;
            }

            if (clan.isClose) parts.Add(Loc.Get("near"));
            if (clan.haveFeud) parts.Add(Loc.Get("feud"));
            if (clan.haveTrade) parts.Add(Loc.Get("trading"));
            if (clan.visitsByEmissary > 0) parts.Add(Loc.Get("visited by emissary"));
            if (clan.visitsByCaravan > 0) parts.Add(Loc.Get("visited by caravan"));

            return string.Join(", ", parts.ToArray());
        }

        /// <summary>
        /// Click the focused list item so it becomes the list's selected item,
        /// without announcing anything. Used when the follow-up action (e.g.
        /// opening a dialog) produces its own announcement and a "X selected."
        /// in between would just be noise.
        /// </summary>
        private void SelectFocusedListItemSilently()
        {
            if (_activeList == null || _listFocusIndex < 0 || _listFocusIndex >= _activeList.count)
                return;
            UIListItem item = _activeList[_listFocusIndex];
            if (item != null) _activeList.OnItemClicked(item);
        }

        private void ActivateListItem()
        {
            if (_activeList == null || _listFocusIndex < 0 || _listFocusIndex >= _activeList.count)
                return;

            UIListItem item = _activeList[_listFocusIndex];

            SaveListItem saveItem = item as SaveListItem;
            if (saveItem != null && saveItem.button != null)
            {
                DebugLogger.Log("KeyboardNav", $"Activating save item at index {_listFocusIndex}");
                var eventData = new BaseEventData(EventSystem.current);
                saveItem.button.OnSubmit(eventData);
                return;
            }

            DebugLogger.Log("KeyboardNav", $"Clicking list item at index {_listFocusIndex}");
            _activeList.OnItemClicked(item);

            string text = item.text ?? item.gameObject.name;
            ScreenController active = GetActiveScreen();

            // FortifyDialog buildable list: OnItemClicked has just run ValidateFortifyButton,
            // which fills costLabel.text ("Cost: N goods") and toggles actionButton.interactable
            // based on PlayerClan.goods. Read both back so the user knows whether Enter
            // will fire and at what price â€” without this hook the announcement is just the
            // bare item name and the cost label is invisible to the screen reader.
            FortifyDialogController fd = active as FortifyDialogController;
            if (fd != null && System.Object.ReferenceEquals(_activeList, fd.buildable))
            {
                string clean = StringHelpers.StripTags(text);
                string cost = (fd.costLabel != null && !string.IsNullOrEmpty(fd.costLabel.text))
                    ? StringHelpers.StripTags(fd.costLabel.text) : "";
                var sb = new System.Text.StringBuilder();
                sb.Append(clean).Append(Loc.Get(" selected"));
                if (!string.IsNullOrEmpty(cost)) sb.Append(", ").Append(cost);
                if (fd.actionButton != null && !fd.actionButton.IsInteractable())
                    sb.Append(Loc.Get(". Build is disabled (insufficient goods or invalid choice)"));
                else
                    sb.Append(Loc.Get(". Press Enter to build"));
                ScreenReader.Say(sb.ToString());
                return;
            }

            ScreenReader.Say(text + Loc.Get(" selected."));
        }

        private void TryGoBack(ScreenController screen)
        {
            Selectable closeBtn = null;
            foreach (var b in _buttons)
            {
                if (b == null || !b.isActiveAndEnabled) continue;
                // Only consider buttons, not toggles
                if (!(b is UIButton)) continue;
                string name = b.gameObject.name.ToLower();
                string label = GetElementLabel(b).ToLower();
                if (name.Contains("close") || name.Contains("back") || name.Contains("cancel")
                    || label.Contains("close") || label.Contains("back") || label.Contains("cancel")
                    || label.Contains("done") || label.Contains("ok"))
                {
                    closeBtn = b;
                    break;
                }
            }

            if (closeBtn != null)
            {
                ActivateButton(closeBtn);
            }
            else
            {
                ScreenReader.Say("No back button found.");
            }
        }


        // ============================================================
        // Season advance toggle (S key)
        // ============================================================

        /// <summary>
        /// Open the game's per-screen help overlay (HelpController) via F1.
        /// The overlay is normally invoked by clicking the helpButtonContainer button,
        /// which is only shown when the active screen has helpButtonPosition &gt; -1
        /// and the tutorial is not running. We mirror those conditions so we don't
        /// pop an empty overlay or interfere with the tutorial flow.
        /// </summary>
        private void TryOpenHelpOverlay(ScreenController screen)
        {
            try
            {
                if (Tutorial.isTrainer)
                {
                    ScreenReader.Say("Help is not available during the tutorial.");
                    return;
                }

                BaseController bc = screen as BaseController;
                if (bc == null || bc.helpButtonPosition < 0)
                {
                    ScreenReader.Say("No help available for this screen.");
                    return;
                }

                if (Singleton<GameManager>.isShuttingDown) return;
                var gm = Singleton<GameManager>.instance;
                if (gm == null)
                {
                    ScreenReader.Say("Help not available.");
                    return;
                }

                gm.ShowHelp(null);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.HelpOverlay", ex);
                ScreenReader.Say("Could not open help.");
            }
        }

        /// <summary>Toggle the season advance panel via ManagementMenuController.</summary>
        private void TryToggleSeasonAdvance()
        {
            try
            {
                if (Singleton<GameManager>.isShuttingDown) return;
                var gm = Singleton<GameManager>.instance;
                if (gm == null || gm.sideMenu == null)
                {
                    ScreenReader.Say("Season advance not available.");
                    return;
                }
                gm.sideMenu.ToggleSeason();
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.SeasonAdvance", ex);
                ScreenReader.Say("Could not toggle season advance.");
            }
        }

        // ============================================================
        // Season Advance modal mode â€” virtual Question + 2 options
        // ============================================================
        //
        // The season-advance slide panel is keyboard-modal: while it's open we
        // present three virtual focus slots (header / Advance / Cancel) and
        // swallow every other input so Tab can't reach management buttons that
        // sit underneath the panel. Mirrors the scene Question-header pattern
        // the user is already used to (header at -1, options at 0..N-1).

        private bool _seasonModeActive;
        private bool _seasonHintShown;
        private const int SeasonHeaderIndex = -1;
        private const int SeasonAdvanceIndex = 0;
        private const int SeasonCancelIndex = 1;
        private int _seasonFocus = SeasonAdvanceIndex;

        /// <summary>Called from DialogPatches.Season_Shown. Activates modal mode and
        /// announces the question + options + initial focus on Advance. The full
        /// hotkey hint plays only on the first open per session â€” afterwards we
        /// trust the user remembers the controls (F5 still recites them).</summary>
        public static void EnterSeasonMode()
        {
            if (_instance == null) return;
            _instance._seasonModeActive = true;
            _instance._seasonFocus = SeasonAdvanceIndex;
            string season = SafeSeasonName();
            string hint = _instance._seasonHintShown
                ? ""
                : Loc.Get(" Use Up and Down to choose, Enter to confirm, Escape to cancel, D for season details.");
            _instance._seasonHintShown = true;
            ScreenReader.Say(Loc.Get("Advance season? Current: ") + season + "." + hint + Loc.Get(" Advance."));
        }

        /// <summary>Called from DialogPatches.Season_Hidden when the panel actually
        /// closes (genuine close, not the no-op OnDisable follow-up).</summary>
        public static void ExitSeasonMode()
        {
            if (_instance == null) return;
            _instance._seasonModeActive = false;
        }

        private void HandleSeasonAdvanceMode()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))      { CycleSeasonFocus(-1); return; }
            if (Input.GetKeyDown(KeyCode.DownArrow))    { CycleSeasonFocus(+1); return; }
            if (Input.GetKeyDown(KeyCode.Tab))          { CycleSeasonFocus(+1); return; }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ActivateSeasonFocus();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelSeasonAdvance();
                return;
            }

            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceSeasonDetails();
                return;
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                AnnounceSeasonStatus();
                return;
            }

            // F12 (debug toggle) is harmless and stays available; everything
            // else (Ctrl+1..9, S, F-keys, letters) is intentionally swallowed.
        }

        private void CycleSeasonFocus(int direction)
        {
            int next = _seasonFocus + direction;
            if (next > SeasonCancelIndex) next = SeasonHeaderIndex;
            if (next < SeasonHeaderIndex) next = SeasonCancelIndex;
            _seasonFocus = next;
            AnnounceSeasonFocus();
        }

        private void AnnounceSeasonFocus()
        {
            switch (_seasonFocus)
            {
                case SeasonHeaderIndex:
                    ScreenReader.Say(Loc.Get("Advance season? Current: ") + SafeSeasonName() + ".");
                    break;
                case SeasonAdvanceIndex:
                    ScreenReader.Say(Loc.Get("Advance."));
                    break;
                case SeasonCancelIndex:
                    ScreenReader.Say(Loc.Get("Cancel."));
                    break;
            }
        }

        private void AnnounceSeasonStatus()
        {
            string focus = _seasonFocus == SeasonHeaderIndex ? Loc.Get("question")
                : _seasonFocus == SeasonAdvanceIndex ? Loc.Get("Advance") : Loc.Get("Cancel");
            ScreenReader.Say(Loc.Get("Advance season? Current: ") + SafeSeasonName()
                + Loc.Get(". Options: Advance, Cancel. Currently on ") + focus
                + Loc.Get(". Enter to confirm, Escape to cancel, D for details."));
        }

        private void AnnounceSeasonDetails()
        {
            try
            {
                string shortName = PluginImport.Game_ShortSeasonName();
                int turnInYear = PluginImport.Game_TurnInYear();
                string body = Localized.StringFromTable(shortName + "Explanation", "Text");
                if (string.IsNullOrEmpty(body))
                {
                    ScreenReader.Say("No details available for this season.");
                    return;
                }
                string prefix;
                if (turnInYear == 11) prefix = "Sacred Time";
                else if ((turnInYear % 2) == 1) prefix = "Early " + shortName + " Season";
                else prefix = "Late " + shortName + " Season";
                ScreenReader.Say(prefix + ". " + body);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.SeasonDetails", ex);
                ScreenReader.Say("Could not read season details.");
            }
        }

        private void ActivateSeasonFocus()
        {
            switch (_seasonFocus)
            {
                case SeasonHeaderIndex:
                    AnnounceSeasonFocus();
                    return;
                case SeasonAdvanceIndex:
                    ConfirmSeasonAdvance();
                    return;
                case SeasonCancelIndex:
                    CancelSeasonAdvance();
                    return;
            }
        }

        private void ConfirmSeasonAdvance()
        {
            try
            {
                if (Singleton<GameManager>.isShuttingDown) return;
                var gm = Singleton<GameManager>.instance;
                if (gm == null || gm.sideMenu == null)
                {
                    ScreenReader.Say("Season advance not available.");
                    return;
                }
                gm.sideMenu.Advance();
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.SeasonConfirm", ex);
                ScreenReader.Say("Could not advance the season.");
            }
        }

        private void CancelSeasonAdvance()
        {
            try
            {
                if (Singleton<GameManager>.isShuttingDown) return;
                var gm = Singleton<GameManager>.instance;
                if (gm == null || gm.sideMenu == null) return;
                gm.sideMenu.HideSeason(false);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.SeasonCancel", ex);
            }
        }

        private static string SafeSeasonName()
        {
            try { return PluginImport.Game_SeasonName(); }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.SafeSeasonName", ex);
                return "current season";
            }
        }

        // ============================================================
        // Scene Info View toggle (I key)
        // ============================================================

        /// <summary>Toggle the scene info side panel and read its content.</summary>
        private void TryToggleSceneInfo()
        {
            try
            {
                GuidanceController gc = UnityEngine.Object.FindObjectOfType<GuidanceController>();
                if (gc == null || gc.currentSceneInfo == null)
                {
                    ScreenReader.Say("Scene info not available.");
                    return;
                }

                SceneInfoView view = gc.currentSceneInfo;
                view.Toggle();

                if (view.showing)
                    DialogContentReader.ReadSceneInfo(view);
                else
                    ScreenReader.Say("Scene info closed.");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.SceneInfo", ex);
                ScreenReader.Say("Could not toggle scene info.");
            }
        }

        /// <summary>Check if any modifier key is held.</summary>
        private static bool AnyModifier()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)
                || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        // ============================================================
        // Tooltip lookup (D) — read Rollover/TextRollover on the focused element
        // ============================================================

        /// <summary>
        /// When D is pressed and no specialized handler claimed it, read whatever tooltip
        /// the focused button advertises via Rollover.message or TextRollover.info. The
        /// game attaches these in prefabs; mouse users see the popup on hover, keyboard
        /// users hear nothing without this hook.
        ///
        /// Returns true when the keypress was consumed (so the outer Update can skip
        /// HandleActivationKeys / HandleButtonNavigation, which would otherwise treat D
        /// as an unknown key).
        /// </summary>
        private bool HandleFocusedTooltipKey()
        {
            if (!Input.GetKeyDown(KeyCode.D)) return false;
            if (AnyModifier()) return false;

            Selectable focused = GetFocusedSelectable();
            if (focused == null) return false;

            string tooltip = ExtractTooltipText(focused.gameObject);
            if (string.IsNullOrEmpty(tooltip))
            {
                ScreenReader.Say("No description for this element.");
                return true; // still consume D so it doesn't fall through to activation
            }

            ScreenReader.Say(tooltip);
            return true;
        }

        /// <summary>
        /// Look for a Rollover or TextRollover anywhere on the focused button — the
        /// component may sit on the Selectable itself, on a child label, or on a parent
        /// container. Returns the cleaned tooltip text or null.
        /// </summary>
        private static string ExtractTooltipText(GameObject go)
        {
            if (go == null) return null;

            Rollover r = go.GetComponent<Rollover>();
            if (r != null && !string.IsNullOrEmpty(r.message))
                return StringHelpers.StripTags(r.message);

            TextRollover tr = go.GetComponent<TextRollover>();
            if (tr != null && !string.IsNullOrEmpty(tr.info))
                return StringHelpers.StripTags(tr.info);

            // Search children (label-with-tooltip pattern is common on icon buttons).
            Rollover rChild = go.GetComponentInChildren<Rollover>(includeInactive: false);
            if (rChild != null && !string.IsNullOrEmpty(rChild.message))
                return StringHelpers.StripTags(rChild.message);

            TextRollover trChild = go.GetComponentInChildren<TextRollover>(includeInactive: false);
            if (trChild != null && !string.IsNullOrEmpty(trChild.info))
                return StringHelpers.StripTags(trChild.info);

            return null;
        }

        // ============================================================
        // Scene picture toggle (P) â€” mouse-equivalent of clicking the scene picture
        // ============================================================

        private static readonly System.Reflection.FieldInfo BackgroundRevealedField =
            typeof(InteractiveController).GetField("backgroundRevealed",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        /// <summary>
        /// Simulate the mouse click on the scene picture. The game wires that click to
        /// InteractiveController.HandleBackgroundToggle (decompiled, line 1280): it calls
        /// ToggleText() to slide the text panel out and the background image in (or vice
        /// versa) and then ShowNote("TextHidden") which lets the trainer advance past
        /// tutorials that teach this gesture. Both effects are reproduced verbatim.
        /// </summary>
        private void TryTogglePicture(InteractiveController ic)
        {
            try
            {
                if (ic == null)
                {
                    ScreenReader.Say("No scene to toggle.");
                    return;
                }

                ic.HandleBackgroundToggle();

                // Read the new state via reflection (backgroundRevealed is protected on
                // InteractiveController; we mirror the same naming used by ToggleText).
                bool revealed = false;
                // Cast to object: Mono 2.0 lacks FieldInfo.op_Inequality, so a direct
                // `Field != null` would throw MissingMethodException at runtime.
                if ((object)BackgroundRevealedField != null)
                {
                    object raw = BackgroundRevealedField.GetValue(ic);
                    if (raw is bool b) revealed = b;
                }

                ScreenReader.Say(revealed ? "Picture shown, text hidden." : "Text shown.");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.TogglePicture", ex);
                ScreenReader.Say("Could not toggle picture.");
            }
        }

        // ============================================================
        // Management screen switching (Ctrl+Number)
        // ============================================================

        /// <summary>Check if a Ctrl+Number key (1-9) is pressed. Returns the number, or 0 if none.</summary>
        private int GetCtrlNumberKey()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha2)) return 2;
            if (Input.GetKeyDown(KeyCode.Alpha3)) return 3;
            if (Input.GetKeyDown(KeyCode.Alpha4)) return 4;
            if (Input.GetKeyDown(KeyCode.Alpha5)) return 5;
            if (Input.GetKeyDown(KeyCode.Alpha6)) return 6;
            if (Input.GetKeyDown(KeyCode.Alpha7)) return 7;
            if (Input.GetKeyDown(KeyCode.Alpha8)) return 8;
            if (Input.GetKeyDown(KeyCode.Alpha9)) return 9;
            return 0;
        }

        /// <summary>Check if management screens are currently accessible.</summary>
        private bool IsInManagementPhase(ScreenController screen)
        {
            return screen is ManagementController;
        }

        /// <summary>Check if a specific screen is allowed (respects tutorial restrictions).</summary>
        private bool IsScreenAllowed(int screenNum)
        {
            try
            {
                var gm = Singleton<GameManager>.instance;
                if (gm == null || gm.sideMenu == null) return false;

                UIToggle[] buttons = gm.sideMenu.menuButtons;
                if (buttons == null) return false;

                foreach (var toggle in buttons)
                {
                    if (toggle != null && toggle.onValue == screenNum)
                        return toggle.interactable;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.IsScreenAllowed", ex);
            }
            return false;
        }

        /// <summary>Try to switch to a management screen.</summary>
        /// <param name="screen">Current active screen controller.</param>
        /// <param name="screenNum">Target side-menu index (1..9).</param>
        /// <param name="trigger">Source of the request for diagnostic logs (e.g. "Ctrl+2", "Ctrl+Tab"). Loud about the actual key the user pressed because Ctrl+Tab routes through here too via TryCycleManagementScreen, and a log line saying "via Ctrl+2" when the user pressed Ctrl+Tab is actively misleading.</param>
        private void TrySwitchManagementScreen(ScreenController screen, int screenNum, string trigger)
        {
            if (!IsInManagementPhase(screen))
            {
                DebugLogger.Log("KeyboardNav", trigger + " ignored - not in management phase");
                return;
            }

            if (screenNum < 1 || screenNum > 9) return;

            // Tutorial trainer mode pins the side menu to a single topic screen
            // (ManagementMenuController.UpdateAvailabilityForTutorial sets every
            // toggle except Tutorial.topicAllow / topicScreen to interactable=false).
            // Calling ChangeScreen anyway makes the game reopen the locked screen
            // briefly and then revert — which is what the user observed. Respect the
            // gating: if a target topic is set and we're not heading there, refuse
            // with a spoken explanation rather than overriding the game.
            if (Tutorial.isTrainer)
            {
                GameScreen forced = (Tutorial.topicAllow != (GameScreen)0)
                    ? Tutorial.topicAllow : Tutorial.topicScreen;
                if (forced != (GameScreen)0 && (int)forced != screenNum)
                {
                    DebugLogger.Log("KeyboardNav", trigger + " blocked by tutorial. forced=" + forced
                        + ", requested=" + GameScreens.NameOfMenuIndex(screenNum));
                    ScreenReader.Say("Tutorial restricts navigation. Only "
                        + GameScreens.NameOf(forced) + " is available right now.");
                    return;
                }
            }

            if (!IsScreenAllowed(screenNum))
            {
                ScreenReader.Say(GameScreens.NameOfMenuIndex(screenNum) + " screen is not available right now.");
                return;
            }

            try
            {
                DebugLogger.Log("KeyboardNav", "Switching to screen " + GameScreens.NameOfMenuIndex(screenNum)
                    + " via " + trigger);

                // Route via the side-menu's UIToggleGroup instead of calling
                // GameManager.ChangeScreen directly. Both end up firing the same
                // ChangeManagementScreen → ChangeScreen path, but the order of
                // operations matters:
                //
                //   group.value = num      → target toggle.isOn=true → NotifyToggleOn
                //                             deactivates the previous toggle WITH
                //                             AnyTogglesOn() already true → group's
                //                             "rescue" branch is silent → clean.
                //
                //   gm.ChangeScreen(num)   → ShowManagementScreen → UpdateButtons
                //                             deactivates the previous toggle FIRST
                //                             (no toggle on yet) → rescue branch
                //                             fires onValueChanged with the OLD
                //                             value mid-transition → that lands as
                //                             deferredScreen, reverting on the
                //                             coroutine's completion.
                //
                // The first path is the one the game itself uses when the user
                // clicks a side-menu button (OnPointerClick → InternalToggle →
                // isOn = !isOn → group setter). We're entering at the same Set
                // call, just skipping the pointer event boilerplate.
                var gm = Singleton<GameManager>.instance;
                if (gm == null || gm.sideMenu == null || gm.sideMenu.menuButtons == null)
                {
                    DebugLogger.Warn("KeyboardNav.SwitchScreen", "GameManager / sideMenu not ready");
                    return;
                }
                DebugLogger.Log("KeyboardNav.SwitchScreen", "gm + sideMenu ready, menuButtons=" + gm.sideMenu.menuButtons.Length);

                UIToggleGroup group = null;
                int currentGroupValue = -1;
                foreach (var toggle in gm.sideMenu.menuButtons)
                {
                    if (toggle != null && toggle.group != null) { group = toggle.group; break; }
                }
                if (group != null) currentGroupValue = group.value;
                DebugLogger.Log("KeyboardNav.SwitchScreen", "group resolved: " + (group != null ? "yes" : "NULL")
                    + ", current value=" + currentGroupValue + ", target=" + screenNum);

                if (group != null)
                {
                    DebugLogger.Log("KeyboardNav.SwitchScreen", "BEFORE group.value = " + screenNum);
                    group.value = screenNum;
                    DebugLogger.Log("KeyboardNav.SwitchScreen", "AFTER  group.value = " + screenNum + " (returned)");
                }
                else
                {
                    // No group reference — degrade gracefully to the direct call.
                    DebugLogger.Warn("KeyboardNav.SwitchScreen", "Side-menu UIToggleGroup not found, falling back to ChangeScreen");
                    DebugLogger.Log("KeyboardNav.SwitchScreen", "BEFORE gm.ChangeScreen(" + screenNum + ")");
                    gm.ChangeScreen(screenNum);
                    DebugLogger.Log("KeyboardNav.SwitchScreen", "AFTER  gm.ChangeScreen(" + screenNum + ") (returned)");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.SwitchScreen", ex);
            }
        }

        /// <summary>
        /// Cycle to the next available management screen via Ctrl+Tab / Ctrl+Shift+Tab.
        /// Skips screens locked by tutorial or otherwise disabled. If no other screen is
        /// reachable (e.g. trainer pins the side menu to one topic) the user is told.
        /// </summary>
        private void TryCycleManagementScreen(ScreenController screen, int direction)
        {
            if (!IsInManagementPhase(screen)) return;
            if (direction == 0) direction = 1;

            int current = 0;
            ManagementController mgmt = screen as ManagementController;
            if (mgmt != null) current = (int)mgmt.screenIndex;
            if (current < 1 || current > 9) current = 1;

            DebugLogger.Log("KeyboardNav.Cycle", "current=" + current + " ("
                + GameScreens.NameOfMenuIndex(current) + "), direction=" + direction);

            int candidate = current;
            for (int i = 0; i < 9; i++)
            {
                candidate += direction;
                if (candidate < 1) candidate = 9;
                if (candidate > 9) candidate = 1;
                if (candidate == current) break;
                bool allowed = IsScreenAllowed(candidate);
                DebugLogger.Log("KeyboardNav.Cycle", "  candidate=" + candidate + " ("
                    + GameScreens.NameOfMenuIndex(candidate) + "), allowed=" + allowed);
                if (allowed)
                {
                    TrySwitchManagementScreen(screen, candidate, direction > 0 ? "Ctrl+Tab" : "Ctrl+Shift+Tab");
                    return;
                }
            }

            ScreenReader.Say("No other management screens are currently available.");
        }

        /// <summary>
        /// Announce the keys relevant to the current screen (the F1 help).
        /// Dispatches on the screen's controller type and composes the text from the
        /// central <see cref="Hints"/> atoms plus one short screen-specific literal.
        /// Short-sentence style; F5/F6/L are intentionally not advertised. The screen
        /// inventory this mirrors lives in <c>docs/keybindings.md</c>.
        ///
        /// Dispatch order matters: every dialog below is also a <c>ManagementController</c>
        /// (ManagementDialogController : MapController : ManagementController), so the
        /// specific types must be tested before the catch-all branches.
        /// </summary>
        private void AnnounceScreenShortcuts(ScreenController screen)
        {
            var sb = new System.Text.StringBuilder();

            // --- Screens outside the management phase ---
            if (screen is ChooseGameController)
            {
                sb.Append(Loc.Get("Keys for the game selection. "))
                  .Append(Loc.Get("Up and Down choose a save, Enter loads it. Delete removes a save; press it twice within three seconds to confirm. Escape goes back. "));
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is BattleController)   // before InteractiveController — Battle is one
            {
                sb.Append(Loc.Get("Keys for combat. "))
                  .Append(Loc.Get("Arrow keys move between the combat options, Enter activates the focused option. "))
                  .Append(Hints.Combat).Append(Hints.FunctionKeysScene);
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is InteractiveController)
            {
                sb.Append(Loc.Get("Keys for the scene. "))
                  .Append(Loc.Get("Up and Down move through the response options, Enter selects a response. "))
                  .Append(Hints.SceneInfo).Append(Hints.ScenePicture)
                  .Append(Hints.Hint).Append(Hints.FunctionKeysScene);
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is TutorialController)
            {
                sb.Append(Loc.Get("Keys for the tutorial. "))
                  .Append(Loc.Get("Up and Down re-read the tutorial paragraphs, Enter continues. "));
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is GameOverController)
            {
                sb.Append(Loc.Get("Keys for the game over screen. "))
                  .Append(Loc.Get("Arrow keys and Tab move the focus, Enter activates. In the saga overview, Up and Down choose a year and Enter restores it. "));
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is ChooseLeaderDialog)
            {
                sb.Append(Loc.Get("Keys for choosing a leader. "))
                  .Append(Loc.Get("Up and Down move between the candidates. Space selects a person, Enter confirms. D reads the full biography. Escape closes. "))
                  .Append(Hints.Filter);
                ScreenReader.Say(sb.ToString());
                return;
            }

            // --- Management screens with their own navigator ---
            if (screen is MagicScreenController)
            {
                sb.Append(Loc.Get("Keys for Magic. "))
                  .Append(Hints.TabZones)
                  .Append(Loc.Get("Space toggles the focused blessing. "))
                  .Append(Hints.Arrows).Append(Hints.Describe)
                  .Append(Hints.ManagementSwitch).Append(Hints.FunctionKeysManagement).Append(Hints.Season);
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is RelationsScreenController)
            {
                sb.Append(Loc.Get("Keys for Relations. "))
                  .Append(Hints.Filter)
                  .Append(Loc.Get("Tab reaches the map markers for clans with available missions, Enter opens the emissary dialog. "))
                  .Append(Hints.Arrows).Append(Hints.Describe)
                  .Append(Hints.ManagementSwitch).Append(Hints.FunctionKeysManagement).Append(Hints.Season);
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is WarScreenController)
            {
                sb.Append(Loc.Get("Keys for War. "))
                  .Append(Loc.Get("W opens Warriors, R starts a raid, C a cattle raid, O an honor raid, F a fortify. Each key says so if the action is unavailable. "))
                  .Append(Hints.ManagementSwitch).Append(Hints.FunctionKeysManagement).Append(Hints.Season);
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is WealthScreenController)
            {
                sb.Append(Loc.Get("Keys for Wealth. "))
                  .Append(Hints.TabZones)
                  .Append(Loc.Get("Space activates the focused item. Enter or C opens the caravan dialog. "))
                  .Append(Hints.Arrows).Append(Hints.Describe)
                  .Append(Hints.ManagementSwitch).Append(Hints.FunctionKeysManagement).Append(Hints.Season);
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is LoreScreenController)
            {
                sb.Append(Loc.Get("Keys for Lore. "))
                  .Append(Hints.TabZones)
                  .Append(Loc.Get("Enter opens the focused entry, M opens the manual. "))
                  .Append(Hints.Arrows).Append(Hints.Describe)
                  .Append(Hints.ManagementSwitch).Append(Hints.FunctionKeysManagement).Append(Hints.Season);
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is SagaScreenController)
            {
                sb.Append(Loc.Get("Keys for the Saga. "))
                  .Append(Loc.Get("Up and Down choose a year, D reads the full text, Enter restores the chosen year. "))
                  .Append(Hints.ManagementSwitch).Append(Hints.FunctionKeysManagement).Append(Hints.Season);
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is SacredTime)
            {
                sb.Append(Loc.Get("Keys for Sacred Time. "))
                  .Append(Loc.Get("Tab cycles between the forecast and the allocation. Left and Right adjust a value, D re-reads the line. G opens the saga chronicle, Enter continues. "))
                  .Append(Hints.Arrows);
                ScreenReader.Say(sb.ToString());
                return;
            }

            // --- Management dialogs (own navigator; before the ManagementDialogController catch-all) ---
            if (screen is MapScreenController)
            {
                sb.Append(Loc.Get("Keys for the Map. "))
                  .Append(Hints.TabZones)
                  .Append(Loc.Get("G focuses the target zone, K the target list, X the foray panel. Enter sends the mission, Escape cancels. "))
                  .Append(Loc.Get("In the hex cursor zone, Page Up and Down jump between points of interest, Ctrl with them switches category. "))
                  .Append(Hints.Arrows).Append(Hints.Describe)
                  .Append(Hints.ManagementSwitch).Append(Hints.Season);
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is ReorganizeDialogController)
            {
                sb.Append(Loc.Get("Keys for reorganizing the clan. "))
                  .Append(Hints.TabZones).Append(Hints.Arrows).Append(Hints.Describe)
                  .Append(Loc.Get("In the list, Space toggles ring membership and C makes the person chieftain. F changes the sort order. Enter applies the reorganization, Escape discards it. "));
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is CaravanDialogController)
            {
                sb.Append(Loc.Get("Keys for the caravan dialog. "))
                  .Append(Hints.TabZones).Append(Hints.Arrows).Append(Hints.Describe)
                  .Append(Loc.Get("R lists the active trade routes. Enter sends the caravan, Escape closes. "));
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is EmissaryDialogController)
            {
                sb.Append(Loc.Get("Keys for the emissary dialog. "))
                  .Append(Hints.TabZones).Append(Hints.Arrows).Append(Hints.Describe)
                  .Append(Loc.Get("Enter sends the emissary, Escape closes. "));
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is RaidDialogController)
            {
                sb.Append(Loc.Get("Keys for the raid dialog. "))
                  .Append(Hints.TabZones).Append(Hints.Arrows).Append(Hints.Describe)
                  .Append(Loc.Get("In the helpers list, F changes the filter. Enter starts the raid, Escape closes. "));
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is WarriorsDialogController)
            {
                sb.Append(Loc.Get("Keys for the warriors dialog. "))
                  .Append(Hints.TabZones).Append(Hints.Arrows).Append(Hints.Describe)
                  .Append(Loc.Get("Enter carries out the main action, Escape closes. "));
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is SacrificeDialogController)
            {
                sb.Append(Loc.Get("Keys for the sacrifice dialog. "))
                  .Append(Hints.TabZones).Append(Hints.Arrows).Append(Hints.Slider).Append(Hints.Describe)
                  .Append(Loc.Get("Enter confirms the sacrifice, Escape closes. "));
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is SpiritDialogController)
            {
                sb.Append(Loc.Get("Keys for the spirit dialog. "))
                  .Append(Hints.TabZones).Append(Hints.Arrows).Append(Hints.Describe)
                  .Append(Loc.Get("Enter carries out the main action, Escape closes. "));
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is ManagementDialogController)   // Fortify / Build / Ritual / Venture
            {
                sb.Append(Loc.Get("Keys for this dialog. "))
                  .Append(Hints.TabButtons).Append(Hints.Arrows)
                  .Append(Loc.Get("Enter carries out the main action, Space activates a button, Escape closes. "));
                ScreenReader.Say(sb.ToString());
                return;
            }

            if (screen is ManagementController)   // Clan, and any other management screen
            {
                sb.Append(Loc.Get("Keys for the clan screen. "))
                  .Append(Hints.TabButtons).Append(Hints.Arrows)
                  .Append(Loc.Get("D reads a clan description; press D again to cycle the paragraphs. "))
                  .Append(Hints.ManagementSwitch).Append(Hints.FunctionKeysManagement).Append(Hints.Season);
                ScreenReader.Say(sb.ToString());
                return;
            }

            // --- Generic fallback (intro, menus, anything unrecognized) ---
            sb.Append(Loc.Get("Keys for this screen. "))
              .Append(Hints.TabButtons).Append(Hints.Arrows).Append(Hints.Slider)
              .Append(Loc.Get("Enter activates the focused item, Space toggles a switch, Escape goes back. "));
            ScreenReader.Say(sb.ToString());
        }

        // ============================================================
        // Text input helpers
        // ============================================================

        /// <summary>True if a TMP_InputField is currently focused for text entry.</summary>
        private bool IsTextInputActive()
        {
            try
            {
                if (EventSystem.current == null) return false;
                GameObject sel = EventSystem.current.currentSelectedGameObject;
                if (sel == null) return false;
                TMP_InputField field = sel.GetComponent<TMP_InputField>();
                return field != null && field.isFocused;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.IsTextInputActive", ex);
                return false;
            }
        }

        /// <summary>Release focus from any active TMP_InputField and announce the entered value.</summary>
        private void DeactivateTextInput()
        {
            try
            {
                if (EventSystem.current == null) return;
                GameObject sel = EventSystem.current.currentSelectedGameObject;
                if (sel == null) return;
                TMP_InputField field = sel.GetComponent<TMP_InputField>();
                if (field == null) return;

                field.DeactivateInputField();
                EventSystem.current.SetSelectedGameObject(null);
                _userActivatedTextField = false;

                string entered = string.IsNullOrEmpty(field.text) ? "empty" : "'" + field.text + "'";
                ScreenReader.Say("Clan name set to " + entered + ".");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.DeactivateTextInput", ex);
            }
        }

        /// <summary>
        /// Release focus from a game-activated TMP_InputField without speech.
        /// Used when IntroController auto-activates the clan-name field on entry;
        /// the user hasn't initiated text entry yet, so silently dropping focus
        /// keeps arrow-key navigation available and avoids a misleading
        /// "Clan name set to empty" announcement.
        /// </summary>
        private void SilentReleaseTextInput()
        {
            try
            {
                if (EventSystem.current == null) return;
                GameObject sel = EventSystem.current.currentSelectedGameObject;
                if (sel == null) return;
                TMP_InputField field = sel.GetComponent<TMP_InputField>();
                if (field == null) return;

                field.DeactivateInputField();
                EventSystem.current.SetSelectedGameObject(null);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.SilentReleaseTextInput", ex);
            }
        }

        // ============================================================
        // Screen announce
        // ============================================================

        private void AnnounceScreen(ScreenController screen)
        {
            if (screen == null) return;

            RefreshButtons(screen);
            RefreshSliders(screen);

            string info = screen.name + ". ";

            // Announce sliders
            if (_sliders.Count > 0)
            {
                info += _sliders.Count + " sliders: ";
                foreach (var s in _sliders)
                {
                    string label = s.label != null ? s.label.text : "Amount";
                    info += label + " " + (int)s.value + " of " + (int)s.maxValue + ", ";
                }
            }

            // Announce buttons and toggles
            info += _buttons.Count + " controls";
            if (_buttons.Count > 0)
            {
                info += ": ";
                foreach (var b in _buttons)
                {
                    string label = GetElementLabel(b);
                    UIToggle toggle = b as UIToggle;
                    if (toggle != null)
                        info += label + " (" + (toggle.isOn ? "on" : "off") + "), ";
                    else
                        info += label + ", ";
                }
            }
            info += ".";

            // Check for lists
            UIList[] lists = screen.GetComponentsInChildren<UIList>(false);
            foreach (var l in lists)
            {
                if (l.isActiveAndEnabled && l.count > 0)
                    info += " List with " + l.count + " items. Press L or arrow keys to navigate.";
            }

            // Check for TutorialView
            try
            {
                TutorialView tv = TutorialView.instance;
                if (tv != null && tv.gameObject.activeSelf)
                {
                    string tutText = TutorialHintHandler.Instance.BuildFullHintText();
                    if (!string.IsNullOrEmpty(tutText))
                        info += " Tutorial: " + tutText;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("KeyboardNav.AnnounceShortcuts.Tutorial", ex);
            }

            ScreenReader.Say(info);
        }
    }
}
