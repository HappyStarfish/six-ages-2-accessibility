using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Handles the Game Over screen and its embedded Saga overlay.
    ///
    /// Game over has two states. In the main state the screen shows
    /// <c>sagaCaption.text</c> — the localized Reread/Restore status string set in
    /// <see cref="GameOverController.OnShow"/> — plus REVIEW SAGA and MAIN MENU
    /// buttons. The flat Tab nav handles those buttons fine, but the caption
    /// itself is silently invisible to the screen reader: it carries the only
    /// hint that the player can (or cannot) restore the run, which matters more
    /// for blind players than sighted ones.
    ///
    /// Pressing REVIEW SAGA reparents the singleton <see cref="SagaView"/> into
    /// <c>sagaContainer</c> and shows it as an overlay above the GameOver
    /// buttons. The overlay carries the chronicle of the run (clan name, per-year
    /// saga text, year list, optional Restore button). Without this navigator the
    /// year list and saga text are unreachable — flat Tab only sees the saga's
    /// Close button. We mirror SacredTimeNavigator's saga-overlay handling:
    /// arrow keys step through years, D reads full text, a blank Enter triggers
    /// Restore, Escape calls HideSaga() to hand input back to the main buttons.
    /// </summary>
    public class GameOverNavigator
    {
        // Year-row state mirrors SagaNavigator. SagaView.years contains both
        // section headers (plain UIListItem) and selectable year rows
        // (UIListItemWithIcons); we only navigate the latter.
        private readonly List<int> _yearRowIndexes = new List<int>();
        private int _yearNavIndex = -1;
        private int _lastBuiltYearCount = -1;

        // Tracks the saga-overlay state across ticks so we can announce
        // "Saga opened" exactly once per opening, and reset year state.
        private bool _sagaOverlayWasOpen;

        // Tracks whether we've already announced the caption for the current
        // GameOver controller instance — re-announcing on every tick would be
        // noisy, but ResetForNewScreen clears this so a fresh GameOver run
        // (e.g. after a Restore that puts you back into the same situation)
        // gets the caption read again.
        private bool _captionAnnounced;

        // Reflection handle for the private SagaView field on GameOverController
        // (`saga`). Resolved lazily so a renamed field doesn't crash the ctor.
        private static FieldInfo _sagaField;
        private static bool _sagaFieldResolved;

        public void ResetForNewScreen()
        {
            _yearRowIndexes.Clear();
            _yearNavIndex = -1;
            _lastBuiltYearCount = -1;
            _sagaOverlayWasOpen = false;
            _captionAnnounced = false;
        }

        /// <summary>
        /// Top-level dispatch. Returns true when input was consumed (saga
        /// overlay handled the keypress) and the dispatcher should stop. When
        /// the overlay is closed this returns false so flat Tab nav still owns
        /// REVIEW SAGA / MAIN MENU.
        /// </summary>
        public bool TryHandle(GameOverController c)
        {
            if (c == null) return false;

            // Late-bound caption announce. GameOverController.OnShow runs
            // before our screen-changed listener queues the phase header, so
            // saying the caption from here lands AFTER "Game over phase. Game
            // over screen." and after the RequestReview tutorial note —
            // exactly where the user expects the screen-specific orientation
            // to appear.
            if (!_captionAnnounced)
            {
                _captionAnnounced = true;
                AnnounceCaption(c);
            }

            bool overlayOpen = IsSagaOverlayOpen(c);
            if (overlayOpen && !_sagaOverlayWasOpen)
            {
                _sagaOverlayWasOpen = true;
                _yearRowIndexes.Clear();
                _yearNavIndex = -1;
                _lastBuiltYearCount = -1;
                AnnounceSagaOpened(c);
            }
            else if (!overlayOpen && _sagaOverlayWasOpen)
            {
                _sagaOverlayWasOpen = false;
            }

            if (!overlayOpen) return false;

            return HandleSagaOverlayInput(c);
        }

        // ============================================================
        // Caption (sagaCaption.text) — main mode
        // ============================================================

        private void AnnounceCaption(GameOverController c)
        {
            try
            {
                if (c.sagaCaption == null) return;
                string txt = StringHelpers.StripTags(c.sagaCaption.text);
                if (string.IsNullOrEmpty(txt)) return;

                // Append a usage hint so the user knows the SAGA review is
                // where the Restore mechanic lives. The caption itself only
                // describes whether restoration is possible right now.
                string hint = Game.canBeRestored
                    ? Loc.Get(" Press REVIEW SAGA to read your chronicle and pick a year to restore.")
                    : Loc.Get(" Press REVIEW SAGA to read your chronicle.");
                ScreenReader.Say(txt + "." + hint, interrupt: false);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("GameOverNav.AnnounceCaption", ex);
            }
        }

        // ============================================================
        // Saga overlay
        // ============================================================

        private static bool IsSagaOverlayOpen(GameOverController c)
        {
            try
            {
                if (c.sagaContainer == null) return false;
                return c.sagaContainer.gameObject.activeSelf;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("GameOverNav.IsSagaOverlayOpen", ex); return false;
            }
        }

        private static SagaView GetSaga(GameOverController c)
        {
            EnsureSagaFieldResolved();
            // Mono 2.0 lacks FieldInfo.op_Equality — the (object) cast routes
            // through the reference-equality op instead of the missing one,
            // which would otherwise throw MissingMethodException on every tick.
            if ((object)_sagaField == null || (object)c == null) return null;
            try { return _sagaField.GetValue(c) as SagaView; }
            catch (Exception ex) { DebugLogger.Error("GameOverNav.GetSaga", ex); return null; }
        }

        private static void EnsureSagaFieldResolved()
        {
            if (_sagaFieldResolved) return;
            _sagaFieldResolved = true;
            try
            {
                _sagaField = typeof(GameOverController).GetField(
                    "saga", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            catch (Exception ex) { DebugLogger.Error("GameOverNav.ResolveSagaField", ex); }
        }

        private void AnnounceSagaOpened(GameOverController c)
        {
            try
            {
                SagaView saga = GetSaga(c);
                int years = (saga != null && saga.years != null) ? saga.years.count : 0;
                string clan = (saga != null && saga.clanName != null) ? StringHelpers.StripTags(saga.clanName.text) : null;
                var sb = new StringBuilder();
                sb.Append("Saga opened.");
                if (!string.IsNullOrEmpty(clan)) sb.Append(" ").Append(clan).Append(".");
                sb.Append(" ").Append(years).Append(" entries. Up and Down navigate years, D for full text, ");
                sb.Append(Game.canBeRestored ? "Enter restores selected year, " : "");
                sb.Append("Escape closes.");
                ScreenReader.Say(sb.ToString(), interrupt: false);

                // Also speak the current-year text so the player hears the run's
                // closing chapter immediately, the way a sighted player would
                // see it pre-loaded by SagaView.OnShow's LoadFromYear(Game.year).
                if (saga != null && saga.sagaText != null && !string.IsNullOrEmpty(saga.sagaText.text))
                {
                    string txt = StringHelpers.StripTags(saga.sagaText.text);
                    if (txt.Length > 250) txt = txt.Substring(0, 250) + "... Press D for full text.";
                    ScreenReader.Say(txt, interrupt: false);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("GameOverNav.AnnounceSagaOpened", ex);
            }
        }

        private bool HandleSagaOverlayInput(GameOverController c)
        {
            SagaView saga = GetSaga(c);
            if (saga == null || saga.years == null) return false;
            EnsureYearRowsBuilt(saga);

            // Enter — Restore. Model Y: a blank Enter is the universal
            // screen-completion key. TryRestore guards every precondition and
            // routes through the game's own "Really Restore?" confirmation.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TryRestore(saga);
                return true;
            }

            // Escape — close the overlay via the GameOverController's HideSaga
            // path (SagaView.Close fires onClose, which is hooked to HideSaga).
            if (Input.GetKeyDown(KeyCode.Escape) && !AnyModifier())
            {
                TryCloseOverlay(saga);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) && !AnyModifier())   { MoveYear(saga, -1); return true; }
            if (Input.GetKeyDown(KeyCode.DownArrow) && !AnyModifier()) { MoveYear(saga, +1); return true; }

            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceFullText(saga);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                AnnounceFullStatus(c);
                return true;
            }

            return false;
        }

        private void EnsureYearRowsBuilt(SagaView saga)
        {
            if (saga.years.count == _lastBuiltYearCount && _yearRowIndexes.Count > 0) return;
            _yearRowIndexes.Clear();
            for (int i = 0; i < saga.years.count; i++)
            {
                UIListItem row = saga.years[i];
                if (row is UIListItemWithIcons) _yearRowIndexes.Add(i);
            }
            _lastBuiltYearCount = saga.years.count;

            // Pre-position on whatever year SagaView.OnShow auto-selected
            // (LoadFromYear(Game.year) sets isSelected on the most recent row).
            if (_yearNavIndex < 0)
            {
                for (int k = 0; k < _yearRowIndexes.Count; k++)
                {
                    UIListItem row = saga.years[_yearRowIndexes[k]];
                    if (row != null && row.isSelected) { _yearNavIndex = k; break; }
                }
                if (_yearNavIndex < 0 && _yearRowIndexes.Count > 0)
                    _yearNavIndex = _yearRowIndexes.Count - 1; // newest year fallback
            }
        }

        private void MoveYear(SagaView saga, int dir)
        {
            if (_yearRowIndexes.Count == 0) { ScreenReader.Say("No years available."); return; }
            int newIdx = _yearNavIndex + dir;
            if (newIdx < 0) newIdx = _yearRowIndexes.Count - 1;
            if (newIdx >= _yearRowIndexes.Count) newIdx = 0;
            _yearNavIndex = newIdx;

            UIListItem row = saga.years[_yearRowIndexes[_yearNavIndex]];
            if (row != null)
            {
                try { saga.years.OnItemClicked(row); }
                catch (Exception ex) { DebugLogger.Error("GameOverNav.MoveYear", ex); }
            }
            AnnounceCurrentYear(saga, includeText: true);
        }

        private void AnnounceCurrentYear(SagaView saga, bool includeText)
        {
            if (_yearNavIndex < 0 || _yearNavIndex >= _yearRowIndexes.Count)
            {
                ScreenReader.Say("No year selected.");
                return;
            }
            UIListItem row = saga.years[_yearRowIndexes[_yearNavIndex]];
            if (row == null) return;

            var sb = new StringBuilder();
            int yearKey = row.key;
            if (yearKey == 0)
            {
                sb.Append("Ancient lore. ");
            }
            else
            {
                string yearLabel = SafeNameOfYear(yearKey);
                sb.Append("Year ").Append(yearLabel).Append(". ");
                string ruler = SafeNameOfRulerInYear(yearKey);
                if (!string.IsNullOrEmpty(ruler)) sb.Append("Ruler ").Append(ruler).Append(". ");
            }

            sb.Append(GetRestoreStatusFor(saga, yearKey)).Append(" ");

            if (includeText && saga.sagaText != null && !string.IsNullOrEmpty(saga.sagaText.text))
            {
                string txt = StringHelpers.StripTags(saga.sagaText.text);
                if (txt.Length > 200) txt = txt.Substring(0, 200) + "... Press D for full text.";
                sb.Append(txt);
            }
            ScreenReader.Say(sb.ToString());
        }

        private void AnnounceFullText(SagaView saga)
        {
            if (saga.sagaText == null || string.IsNullOrEmpty(saga.sagaText.text))
            {
                ScreenReader.Say("No saga text available for this year.");
                return;
            }
            ScreenReader.Say(StringHelpers.StripTags(saga.sagaText.text));
        }

        private void TryRestore(SagaView saga)
        {
            if (_yearNavIndex < 0 || _yearNavIndex >= _yearRowIndexes.Count)
            {
                ScreenReader.Say("Select a year first using the arrow keys.");
                return;
            }
            UIListItem row = saga.years[_yearRowIndexes[_yearNavIndex]];
            if (row == null) return;

            int yearKey = row.key;
            if (yearKey == Game.year)
            {
                ScreenReader.Say("Cannot restore to the current year. Pick an earlier year first.");
                return;
            }
            if (!saga.showRestoreButton)
            {
                ScreenReader.Say("Restore is not available on this view.");
                return;
            }
            if (!Game.canBeRestored)
            {
                int max = SafeRestoreMax();
                int used = Game.restores;
                if (max > 0 && used >= max)
                    ScreenReader.Say("No restores remaining. Used " + used + " of " + max + ".");
                else
                    ScreenReader.Say("Restore is not available right now.");
                return;
            }

            // Drive the click path so SagaView.year matches our focus, then
            // open the game's "Really Restore?" confirmation dialog.
            try { saga.years.OnItemClicked(row); }
            catch (Exception ex) { DebugLogger.Error("GameOverNav.TryRestore.OnItemClicked", ex); }
            try { saga.RestoreGame(); }
            catch (Exception ex) { DebugLogger.Error("GameOverNav.TryRestore.RestoreGame", ex); }
        }

        private void TryCloseOverlay(SagaView saga)
        {
            // SagaView.Close fires its onClose event, which GameOverController
            // hooks via HideSaga to deactivate sagaContainer + hide backdrop.
            try { saga.Close(); }
            catch (Exception ex)
            {
                DebugLogger.Error("GameOverNav.CloseOverlay", ex);
                ScreenReader.Say("Could not close the saga overlay.");
            }
        }

        // ============================================================
        // Full status (F5)
        // ============================================================

        public void AnnounceFullStatus(GameOverController c)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append(Loc.Get("Game over. "));
                string caption = c.sagaCaption != null ? StringHelpers.StripTags(c.sagaCaption.text) : null;
                if (!string.IsNullOrEmpty(caption)) sb.Append(caption).Append(". ");

                int max = SafeRestoreMax();
                int used = Game.restores;
                if (max > 0) sb.Append(Loc.Get("Restores used ")).Append(used).Append(Loc.Get(" of ")).Append(max).Append(". ");

                if (Game.canBeRestored)
                    sb.Append(Loc.Get("Restore available. Open REVIEW SAGA, pick an earlier year, then Enter."));
                else if (max > 0 && used >= max)
                    sb.Append(Loc.Get("No restores remaining."));
                else
                    sb.Append(Loc.Get("Restore is not available right now."));

                ScreenReader.Say(sb.ToString());
            }
            catch (Exception ex)
            {
                DebugLogger.Error("GameOverNav.AnnounceFullStatus", ex);
            }
        }

        // ============================================================
        // Helpers
        // ============================================================

        private string GetRestoreStatusFor(SagaView saga, int yearKey)
        {
            if (yearKey == Game.year) return "Current year — cannot restore.";
            if (!saga.showRestoreButton) return "Restore not available here.";
            if (Game.canBeRestored) return "Enter to restore.";
            int max = SafeRestoreMax();
            int used = Game.restores;
            if (max > 0 && used >= max) return "No restores remaining (" + used + " of " + max + " used).";
            return "Restore disabled.";
        }

        private static int SafeRestoreMax()
        {
            try { return Game.IntegerVariable("restoreMax"); }
            catch (Exception ex) { DebugLogger.Error("GameOverNav.RestoreMax", ex); return 0; }
        }

        private static string SafeNameOfYear(int year)
        {
            try { return Game.NameOfYear(year); }
            catch (Exception ex) { DebugLogger.Error("GameOverNav.NameOfYear", ex); return year.ToString(); }
        }

        private static string SafeNameOfRulerInYear(int year)
        {
            try { return Game.NameOfRulerInYear(year); }
            catch (Exception ex) { DebugLogger.Error("GameOverNav.NameOfRulerInYear", ex); return null; }
        }

        private static bool AnyModifier()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftShift)   || Input.GetKey(KeyCode.RightShift)
                || Input.GetKey(KeyCode.LeftAlt)     || Input.GetKey(KeyCode.RightAlt);
        }
    }
}
