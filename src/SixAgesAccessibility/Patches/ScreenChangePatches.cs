using System;
using HarmonyLib;
using UnityEngine;

namespace SixAgesAccessibility.Patches
{
    /// <summary>Harmony patches for screen change announcements.</summary>
    [HarmonyPatch]
    public static class ScreenChangePatches
    {
        private static bool _subscribed;
        private static GamePhase _lastPhase = GamePhase.Unknown;
        private static string _lastScreenLabel = null;

        /// <summary>
        /// High-level game phases the user wants announced. The phase is derived from the
        /// active <see cref="ScreenController"/> class, since Six Ages 2 has no explicit
        /// phase enum — every phase corresponds to a different controller hierarchy.
        /// </summary>
        public enum GamePhase
        {
            Unknown,
            Menu,         // MainMenu, ChooseGame, ControlsOverlay
            Management,   // Clan, War, Wealth, Relations, Magic, Lore, Saga, Map
            Dialog,       // Reorganize, Venture, Emissary, Ritual, Sacrifice, ...
            Story,        // Scene, News, Intro, Quest
            Battle,       // BattleController, BattleResultsController
            SacredTime,   // SacredTime (year-end forecast)
            GameOver      // VictoryController, GameOverController
        }

        /// <summary>
        /// Subscribe to <c>ScreenManager.onScreenChanged</c>. Idempotent — safe to call
        /// from <see cref="Plugin.SubscribeWhenScreenManagerReady"/> and from the Harmony
        /// postfix as a fallback. The plugin coroutine subscribes early (before the first
        /// screen activation), so we always catch the very first management-screen swap
        /// after a saved-game load.
        /// </summary>
        public static void SubscribeOnScreenChanged(ScreenManager sm)
        {
            if (_subscribed) return;
            try
            {
                if (sm == null || sm.onScreenChanged == null) return;
                sm.onScreenChanged.AddListener(OnScreenChangedListener);
                _subscribed = true;
                DebugLogger.Log("ScreenChange", "Subscribed to onScreenChanged");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScreenChangePatches.Subscribe", ex);
            }
        }

        /// <summary>
        /// Diagnostic prefix on ShowManagementScreen. Pairs with the postfix log so we can
        /// see in the log whether we entered the method at all (vs. hanging in the
        /// UIToggleGroup setter cascade upstream).
        /// </summary>
        [HarmonyPatch(typeof(GameManager), "ShowManagementScreen", new Type[] { typeof(GameScreen), typeof(bool) })]
        [HarmonyPrefix]
        public static void ManagementScreen_Prefix(GameScreen aScreen)
        {
            try
            {
                DebugLogger.Log("ScreenChange", "ShowManagementScreen prefix: " + GameScreens.NameOf(aScreen));
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScreenChangePatches.Prefix", ex);
            }
        }

        /// <summary>Announce when a management screen is shown.</summary>
        [HarmonyPatch(typeof(GameManager), "ShowManagementScreen", new Type[] { typeof(GameScreen), typeof(bool) })]
        [HarmonyPostfix]
        public static void ManagementScreen_Postfix(GameScreen aScreen)
        {
            try
            {
                string name = GameScreens.NameOf(aScreen);
                DebugLogger.Log("ScreenChange", "ShowManagementScreen postfix: " + name);
                LogShowScreenCaller(name);

                // Fallback subscription in case the Plugin.Awake coroutine hasn't run yet.
                EnsureSubscribedFallback();
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScreenChangePatches", ex);
            }
        }

        private static void EnsureSubscribedFallback()
        {
            if (_subscribed) return;
            try
            {
                if (Singleton<ScreenManager>.isShuttingDown) return;
                var sm = Singleton<ScreenManager>.instance;
                if (sm != null) SubscribeOnScreenChanged(sm);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScreenChangePatches.SubscribeFallback", ex);
            }
        }

        // A screen change waiting for the one-frame "is it still active?" recheck.
        private static ScreenController _pendingScreen;
        private static int _pendingFrame;

        /// <summary>
        /// Fires after <c>ScreenManager.activeScreen</c> has been set. It does NOT
        /// announce immediately: a screen the trainer opens is often covered the same
        /// frame by a full-screen <see cref="TutorialController"/>, and describing a
        /// screen the user cannot act on yet is just noise. Instead the screen is
        /// stashed and <see cref="FlushPendingScreenChange"/> re-checks it one frame
        /// later — if a tutorial (or any other screen) took over, the announcement is
        /// dropped. It then fires correctly when the user later lands on the screen.
        /// </summary>
        private static void OnScreenChangedListener()
        {
            try
            {
                ScreenController active = GetActiveScreen();
                if (active == null) return;

                // TutorialController and ResultsOverlay get their full content announced
                // by their own dedicated hooks (MenuPatches.AnnounceTutorialContent and
                // DialogPatches.ResultsOverlay_Shown). Never stash them — and because we
                // skip them here, a screen they cover stays the pending screen and the
                // one-frame recheck below sees it is no longer active and drops it.
                if (active is TutorialController || active is ResultsOverlay)
                {
                    DebugLogger.Log("ScreenChange", "Skipping header for "
                        + active.GetType().Name + " (own announce path covers it)");
                    return;
                }

                // Sacred Time, once the player has pressed Proceed, is re-activated
                // transiently by ScreenManager every time a year-advance dialog
                // closes on top of it. That is not a real return to Sacred Time —
                // suppress the header until next year's genuine SacredTime.OnShow.
                if (active is SacredTime && SacredTimePatches.SuppressSacredTimeAnnouncements)
                {
                    DebugLogger.Log("ScreenChange",
                        "Skipping Sacred Time header - transient re-activation during year-advance");
                    return;
                }

                // Only management screens have the "trainer opens it, then a tutorial
                // covers it the same frame" problem — so only they are deferred for the
                // one-frame recheck. Dialogs, menus, battle etc. keep their immediate
                // announcement and exact prior ordering.
                if (ClassifyPhase(active) == GamePhase.Management)
                {
                    _pendingScreen = active;
                    _pendingFrame = Time.frameCount;
                    return;
                }

                AnnounceScreen(active);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScreenChangePatches.OnChanged", ex);
            }
        }

        /// <summary>
        /// Announce a stashed screen change, but only if that screen is still the
        /// active screen a frame later. Called once per frame from
        /// <see cref="KeyboardNavigationHandler"/>'s Update. When a tutorial covered
        /// the screen, the active screen is now the TutorialController and the stale
        /// announcement is dropped — the screen re-announces itself when the user
        /// dismisses the tutorial and actually lands on it.
        /// </summary>
        public static void FlushPendingScreenChange()
        {
            try
            {
                if (_pendingScreen == null) return;
                if (Time.frameCount <= _pendingFrame) return;   // give it one full frame

                ScreenController pending = _pendingScreen;
                _pendingScreen = null;

                ScreenController active = GetActiveScreen();
                if (active == null || active != pending)
                {
                    DebugLogger.Log("ScreenChange", "Pending screen change dropped — "
                        + "screen was covered or replaced before it settled");
                    return;
                }

                AnnounceScreen(active);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScreenChangePatches.FlushPending", ex);
            }
        }

        /// <summary>
        /// Speak the phase header + management content summary + concerns for a screen
        /// that has settled (confirmed still active one frame after the change).
        /// </summary>
        private static void AnnounceScreen(ScreenController active)
        {
            // Speak the phase header (queued). When the active screen is a management
            // screen, the content summary and concerns are queued behind it. Dialog and
            // scene controllers run their own auto-announce hooks; for scenes the
            // scene-init path already consumed the header via GetPhaseHeaderIfNew, so
            // this returns null there and only the dedicated path speaks.
            string header = GetPhaseHeaderIfNew(active);
            if (header != null)
            {
                DebugLogger.Log("ScreenChange", "Announcing header: " + header);
                ScreenReader.Say(header, interrupt: false);
            }

            if (ManagementScreenReader.TryReadSummary(active, interrupt: false))
                DebugLogger.Log("ScreenChange", "Management screen summary queued");

            // Concerns + currently active magic for management screens with a dashboard
            // tile (Clan/Magic/Map/Relations/War/Wealth). The known/unlearned roster is
            // intentionally excluded — it sounded like the available blessings were
            // answering the warnings, and the roster is one Ctrl+F4 away anyway. Queued
            // behind the summary so order stays: phase header → screen summary →
            // "what's affecting us now". Lore/Saga/SacredTime have no dashboard tile.
            if (active is BaseController bc2 && ConcernReader.HasDashboardEntry(bc2.screenIndex))
            {
                string concerns = ConcernReader.BuildCurrentScreenReport(bc2.screenIndex);
                if (!string.IsNullOrEmpty(concerns))
                {
                    DebugLogger.Log("ScreenChange", "Concerns queued for " + GameScreens.NameOf(bc2.screenIndex));
                    ScreenReader.Say(concerns, interrupt: false);
                }
            }
        }

        /// <summary>
        /// If the given active screen represents a phase/screen combo we haven't yet
        /// announced this turn, return the header string ("Management phase. Clan
        /// screen.") and update the last-announced state so the listener won't repeat
        /// it. Returns <c>null</c> when the same combo was just announced — the caller
        /// should then skip its header emit.
        /// </summary>
        /// <remarks>
        /// Public so <see cref="ScenePatches.AnnounceSceneInit"/> can prepend the header
        /// to the scene caption + text in a single Say call. Scenes are tricky because
        /// <c>InitializeFromScript</c> runs synchronously inside <c>GameManager.ShowScene</c>
        /// BEFORE the show coroutine activates the new controller, so by the time
        /// <c>onScreenChanged</c> fires the scene text has already been queued and a
        /// listener-emitted header would land last instead of first.
        /// </remarks>
        public static string GetPhaseHeaderIfNew(ScreenController active)
        {
            if (active == null) return null;
            GamePhase phase = ClassifyPhase(active);
            string screenLabel = ScreenLabelFor(active);

            if (phase == _lastPhase && screenLabel == _lastScreenLabel)
            {
                DebugLogger.Log("ScreenChange", "Same phase+screen as last announce, skipping header");
                return null;
            }

            // Phase prefix is only useful when crossing a phase boundary (e.g.
            // Story → Management). When the user just hops between screens
            // inside the same phase via Ctrl+Number or Ctrl+Tab, the prefix is
            // redundant ("Management phase. Clan screen. ... Management phase.
            // Magic screen. ...") and clutters every screen swap. Suppress it
            // and announce just the screen label in that case.
            bool samePhase = phase == _lastPhase && _lastPhase != GamePhase.Unknown;

            _lastPhase = phase;
            _lastScreenLabel = screenLabel;

            if (samePhase)
                return Loc.Get(screenLabel) + Loc.Get(" screen.");
            return Loc.Get(PhaseLabel(phase)) + Loc.Get(" phase. ")
                + Loc.Get(screenLabel) + Loc.Get(" screen.");
        }

        /// <summary>Map an active controller to its high-level phase category.</summary>
        private static GamePhase ClassifyPhase(ScreenController active)
        {
            if (active == null) return GamePhase.Unknown;
            // Specific subclasses first — InteractiveController is the parent of
            // BattleController, so the battle check has to run before the story check.
            if (active is BattleController || active is BattleResultsController)
                return GamePhase.Battle;
            if (active is SacredTime)
                return GamePhase.SacredTime;
            if (active is VictoryController || active is GameOverController)
                return GamePhase.GameOver;
            if (active is InteractiveController)
                return GamePhase.Story;
            if (active is ManagementDialogController)
                return GamePhase.Dialog;
            if (active is ChooseLeaderDialog)
                return GamePhase.Dialog;
            if (active is ManagementController)
                return GamePhase.Management;
            if (active is MainMenu || active is ChooseGameController || active is ControlsOverlay)
                return GamePhase.Menu;
            return GamePhase.Unknown;
        }

        private static string PhaseLabel(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Menu: return "Menu";
                case GamePhase.Management: return "Management";
                case GamePhase.Dialog: return "Dialog";
                case GamePhase.Story: return "Story";
                case GamePhase.Battle: return "Battle";
                case GamePhase.SacredTime: return "Sacred time";
                case GamePhase.GameOver: return "Game over";
                default: return "Unknown";
            }
        }

        /// <summary>
        /// Build a short human-readable label for the active screen. For controllers
        /// derived from <see cref="BaseController"/> the <c>screenIndex</c> field gives
        /// us the matching <see cref="GameScreen"/> enum value, which we already render
        /// via <see cref="GameScreens.NameOf"/>. For others fall back to a class-name
        /// based label so the user still hears something meaningful.
        /// </summary>
        private static string ScreenLabelFor(ScreenController active)
        {
            try
            {
                if (active is BaseController bc && bc.screenIndex != 0)
                    return GameScreens.NameOf(bc.screenIndex);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScreenChangePatches.ScreenLabelFor", ex);
            }

            if (active is MainMenu) return "Main menu";
            if (active is ChooseGameController) return "Choose game";
            if (active is ControlsOverlay) return "Settings";
            if (active is BattleController) return "Battle";
            if (active is BattleResultsController) return "Battle results";
            if (active is VictoryController) return "Victory";
            if (active is GameOverController) return "Game over";
            if (active is SacredTime) return "Sacred time";
            if (active is ChooseLeaderDialog) return "Choose leader";

            // Last resort: scrub the trailing "Controller" suffix off the class name.
            string n = active.GetType().Name;
            if (n.EndsWith("Controller", StringComparison.Ordinal))
                n = n.Substring(0, n.Length - "Controller".Length);
            if (n.EndsWith("Dialog", StringComparison.Ordinal))
                n = n.Substring(0, n.Length - "Dialog".Length);
            return n;
        }

        /// <summary>
        /// Capture the managed call stack and log the first few non-mod frames.
        /// Used to find the caller behind unexpected screen reverts.
        ///
        /// Implementation note: <c>System.Diagnostics.StackTrace.GetMethod()</c> returns
        /// a MethodBase; comparing it against null on Unity 2018 Mono throws
        /// MissingMethodException because op_Equality on MemberInfo isn't present.
        /// Use <see cref="System.Environment.StackTrace"/> instead — it returns a
        /// pre-formatted string, so no Reflection operator overloads are involved.
        /// </summary>
        private static void LogShowScreenCaller(string screenName)
        {
            try
            {
                if (!DebugLogger.IsActive) return;
                string trace = System.Environment.StackTrace;
                if (string.IsNullOrEmpty(trace)) return;

                var sb = new System.Text.StringBuilder();
                string[] lines = trace.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                int taken = 0;
                for (int i = 0; i < lines.Length && taken < 8; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0) continue;
                    // Strip noise frames so the actual caller is visible.
                    if (line.IndexOf("Environment", StringComparison.Ordinal) >= 0) continue;
                    if (line.IndexOf("ScreenChangePatches", StringComparison.Ordinal) >= 0) continue;
                    if (line.IndexOf("LogShowScreenCaller", StringComparison.Ordinal) >= 0) continue;
                    if (line.IndexOf("DynamicMethod", StringComparison.Ordinal) >= 0) continue;
                    sb.Append(line).Append(" | ");
                    taken++;
                }
                DebugLogger.Log("ScreenChange", "  caller chain for " + screenName + ": " + sb.ToString());
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScreenChangePatches.LogShowScreenCaller", ex);
            }
        }

        /// <summary>Get the currently active screen from ScreenManager.</summary>
        private static ScreenController GetActiveScreen()
        {
            try
            {
                if (Singleton<ScreenManager>.isShuttingDown) return null;
                var sm = Singleton<ScreenManager>.instance;
                if (sm == null) return null;
                return sm.activeScreen;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScreenChangePatches.GetActiveScreen", ex);
                return null;
            }
        }
    }
}
