using UnityEngine;

namespace SixAgesAccessibility.Lore
{
    /// <summary>
    /// Singleton state for the in-game lore reader. Owns one LoreReader and
    /// the currently-active ILoreHost. Harmony patches push topic-change events
    /// in; KeyboardNavigationHandler dispatch pulls per-frame input out.
    ///
    /// Why a static class: the reader has no per-frame Update — it only runs
    /// while a Lore dialog is the active screen, and the dispatcher only
    /// touches its state from the main thread (Harmony postfixes + KeyboardNav
    /// dispatch all run on the Unity main thread).
    /// </summary>
    public static class LoreDialogDispatcher
    {
        private static readonly LoreReader _reader = new LoreReader();
        private static ILoreHost _host;
        private static MonoBehaviour _activeController;

        /// <summary>True while a Lore dialog (Myth/Info/Manual) owns the keyboard.</summary>
        public static bool IsActive { get { return _host != null; } }

        /// <summary>
        /// Hook for MythDialogController.ShowTopic_Postfix — fires on initial
        /// open, link-follow, and detail-toggle. Always reload + announce.
        /// </summary>
        public static void OnMythTopic(MythDialogController mdc, string filename, bool detail)
        {
            if (mdc == null) return;
            try
            {
                var host = new MythDialogLoreHost(mdc);
                _host = host;
                _activeController = mdc;

                var doc = LoreSource.LoadByTopic(filename, detail, LoreSource.ResolveChapter());
                _reader.SetDocument(doc, filename);
                _reader.AnnounceOpening(host);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("LoreDialogDispatcher.OnMythTopic", ex);
            }
        }

        /// <summary>
        /// Hook for InfoDialogController.OnShow_Postfix. Loads Info-DarkAge.html
        /// from the browser archive and announces. The Info dialog is single-
        /// page so we don't track topic state.
        /// </summary>
        public static void OnInfoShown(InfoDialogController idc)
        {
            if (idc == null) return;
            try
            {
                var actions = BuildInfoActions(idc);
                var host = new HtmlPageLoreHost(idc, Loc.Get("Intro"), actions);
                _host = host;
                _activeController = idc;
                var doc = LoreSource.LoadByPath("Manual/Info-DarkAge.html");
                _reader.SetDocument(doc, Loc.Get("Intro"));
                _reader.AnnounceOpening(host);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("LoreDialogDispatcher.OnInfoShown", ex);
            }
        }

        /// <summary>
        /// Hook for ManualDialogController.OnShow_Postfix. Loads the entire
        /// manual document — the in-game version uses a TOC sidebar to scroll
        /// the same big page, but for TTS it's easier to surface the whole
        /// thing as a flat node list and let the user read with arrow keys.
        /// </summary>
        public static void OnManualShown(ManualDialogController mdc)
        {
            if (mdc == null) return;
            try
            {
                var actions = BuildManualActions(mdc);
                var host = new HtmlPageLoreHost(mdc, Loc.Get("Manual"), actions);
                _host = host;
                _activeController = mdc;
                var doc = LoreSource.LoadByPath("Manual/Manual-DarkAge-Unity.html");
                _reader.SetDocument(doc, Loc.Get("Manual"));
                _reader.AnnounceOpening(host);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("LoreDialogDispatcher.OnManualShown", ex);
            }
        }

        /// <summary>
        /// Build the action list for InfoDialog (Tutorial / Manual / Close).
        /// StartTutorial and ShowManual take a UIButton argument they never
        /// use, so we pass null. Close routes through ScreenManager.HideDialog
        /// via the host's standard Close path so the dialog teardown is
        /// identical to pressing the Esc key.
        /// </summary>
        private static LoreAction[] BuildInfoActions(InfoDialogController idc)
        {
            return new[]
            {
                new LoreAction
                {
                    Label = Loc.Get("Start tutorial"),
                    Invoke = () =>
                    {
                        try { idc.StartTutorial(null); }
                        catch (System.Exception ex) { DebugLogger.Error("LoreAction.StartTutorial", ex); }
                    }
                },
                new LoreAction
                {
                    Label = Loc.Get("Open manual"),
                    Invoke = () =>
                    {
                        try { idc.ShowManual(null); }
                        catch (System.Exception ex) { DebugLogger.Error("LoreAction.ShowManual", ex); }
                    }
                },
                new LoreAction
                {
                    Label = Loc.Get("Close"),
                    Invoke = () =>
                    {
                        if (_host != null) _host.Close();
                    }
                },
            };
        }

        /// <summary>
        /// Build the action list for ManualDialog. The dialog has no extra
        /// buttons beyond the close X, so we expose only Close — Escape would
        /// already do the same, but a navigable button keeps the reading flow
        /// (End → Close) symmetric with the Info dialog.
        /// </summary>
        private static LoreAction[] BuildManualActions(ManualDialogController mdc)
        {
            return new[]
            {
                new LoreAction
                {
                    Label = Loc.Get("Close"),
                    Invoke = () =>
                    {
                        if (_host != null) _host.Close();
                    }
                },
            };
        }

        /// <summary>Hook for OnHide-style postfixes. Clears active state.</summary>
        public static void OnDialogHide(MonoBehaviour controller)
        {
            if (controller == null || _activeController == controller || _activeController == null)
            {
                _host = null;
                _activeController = null;
            }
        }

        /// <summary>
        /// Per-frame entry point. Returns true if the dispatcher consumed
        /// dispatch — caller should `return` from its own dispatch loop in that
        /// case so other handlers don't double-fire on the same key press.
        /// </summary>
        public static bool HandleInput(ScreenController screen)
        {
            if (_host == null) return false;
            // Defensive: if the active screen changed without OnHide firing
            // (e.g. transition glitch), drop ownership rather than route input
            // to a controller that may have been destroyed.
            if (_activeController == null || (object)screen != (object)_activeController)
            {
                _host = null;
                _activeController = null;
                return false;
            }

            _reader.HandleInput(_host);
            return true;
        }
    }
}
