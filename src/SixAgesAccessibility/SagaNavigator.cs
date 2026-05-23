using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Single-zone keyboard navigation for the SagaScreenController.
    ///
    /// The saga screen is the only place in the game where past decisions can
    /// be undone via the Restore mechanic: pick an earlier year in the saga
    /// list, the per-year text loads, then Restore rewinds the entire game
    /// state to the end of that year. Restore is gated by Game.canBeRestored
    /// and bounded by Game.IntegerVariable("restoreMax") vs Game.restores.
    ///
    /// Without per-screen handling the year list is unreachable from the
    /// keyboard (UIList rows are not Selectables) and the Restore button is
    /// invisible to flat Tab nav, locking blind players out of the only
    /// in-game undo. We expose the years via Up/Down and route the Restore
    /// action through a blank Enter — no separate Action zone, per the global
    /// Model Y action-key pattern.
    /// </summary>
    public class SagaNavigator
    {
        // Indices into saga.years that point at selectable year rows.
        // Section headers in the same UIList are plain UIListItem (instantiated
        // from saga.yearHeaderTemplate) and would call LoadFromYear with key=0
        // if clicked, which collapses to the Lore entry — confusing, so we
        // skip them.
        private readonly List<int> _yearRowIndexes = new List<int>();
        private int _navIndex = -1;
        private int _lastBuiltCount = -1;

        public void ResetForNewScreen()
        {
            _yearRowIndexes.Clear();
            _navIndex = -1;
            _lastBuiltCount = -1;
        }

        public void HandleInput(SagaScreenController c)
        {
            if (c == null) return;
            SagaView saga = TryGetSaga();
            if (saga == null || saga.years == null) return;

            EnsureYearRowsBuilt(saga);

            // Enter — trigger the Restore action on the currently-loaded year.
            // The Restore button itself is unreachable via flat Tab; this is the
            // only way for the keyboard user to invoke the rewind. Model Y: a
            // blank Enter is the universal screen-completion key. TryRestore
            // guards every precondition and routes through the game's own
            // "Really Restore?" confirmation, so a stray Enter is safe.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TryRestore(saga);
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) && !AnyModifier())
            {
                MoveYear(saga, -1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow) && !AnyModifier())
            {
                MoveYear(saga, +1);
                return;
            }

            // D — read the full saga text for the currently loaded year.
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceFullText(saga);
                return;
            }
        }

        // ---------- Year-row collection ----------

        private SagaView TryGetSaga()
        {
            try { return Singleton<GameManager>.instance.saga; }
            catch (Exception ex) { DebugLogger.Error("SagaNav.TryGetSaga", ex); return null; }
        }

        private void EnsureYearRowsBuilt(SagaView saga)
        {
            if (saga.years.count == _lastBuiltCount && _yearRowIndexes.Count > 0)
                return;
            _yearRowIndexes.Clear();
            for (int i = 0; i < saga.years.count; i++)
            {
                UIListItem row = saga.years[i];
                if (row is UIListItemWithIcons)
                    _yearRowIndexes.Add(i);
            }
            _lastBuiltCount = saga.years.count;

            // Pre-position on whatever year SagaView.OnShow auto-selected
            // (it ends with LoadFromYear(Game.year), which sets isSelected on
            // the current year row). The first arrow press then moves relative
            // to "now" instead of jumping to the very top.
            if (_navIndex < 0)
            {
                for (int k = 0; k < _yearRowIndexes.Count; k++)
                {
                    UIListItem row = saga.years[_yearRowIndexes[k]];
                    if (row != null && row.isSelected) { _navIndex = k; break; }
                }
            }
        }

        // ---------- Year navigation ----------

        private void MoveYear(SagaView saga, int dir)
        {
            if (_yearRowIndexes.Count == 0) { ScreenReader.Say(Loc.Get("No years available.")); return; }
            int newIdx = _navIndex + dir;
            if (newIdx < 0) newIdx = _yearRowIndexes.Count - 1;
            if (newIdx >= _yearRowIndexes.Count) newIdx = 0;
            _navIndex = newIdx;

            // Drive the same path a mouse click would: OnItemClicked →
            // OnYearSelected → LoadFromYear(item.key). This updates the saga
            // text panel AND the restore button visibility/interactable state
            // in one go, so a subsequent Enter sees the right year.
            UIListItem row = saga.years[_yearRowIndexes[_navIndex]];
            if (row != null)
            {
                try { saga.years.OnItemClicked(row); }
                catch (Exception ex) { DebugLogger.Error("SagaNav.MoveYear.OnItemClicked", ex); }
            }
            AnnounceCurrent(saga, includeText: true);
        }

        private void AnnounceCurrent(SagaView saga, bool includeText)
        {
            if (_navIndex < 0 || _navIndex >= _yearRowIndexes.Count)
            {
                ScreenReader.Say(Loc.Get("No year selected."));
                return;
            }
            UIListItem row = saga.years[_yearRowIndexes[_navIndex]];
            if (row == null) return;

            var sb = new StringBuilder();
            int yearKey = row.key;

            if (yearKey == 0)
            {
                sb.Append(Loc.Get("Ancient lore. "));
            }
            else
            {
                string yearLabel = SafeNameOfYear(yearKey);
                sb.Append(Loc.Get("Year ")).Append(yearLabel).Append(". ");
                string ruler = SafeNameOfRulerInYear(yearKey);
                if (!string.IsNullOrEmpty(ruler))
                    sb.Append(Loc.Get("Ruler ")).Append(ruler).Append(". ");
            }

            sb.Append(GetRestoreStatusFor(saga, yearKey)).Append(" ");

            if (includeText && saga.sagaText != null && !string.IsNullOrEmpty(saga.sagaText.text))
            {
                string txt = StringHelpers.StripTags(saga.sagaText.text);
                if (txt.Length > 200) txt = txt.Substring(0, 200) + Loc.Get("... Press D for full text.");
                sb.Append(txt);
            }

            ScreenReader.Say(sb.ToString());
        }

        // ---------- Restore action ----------

        private void TryRestore(SagaView saga)
        {
            if (_navIndex < 0 || _navIndex >= _yearRowIndexes.Count)
            {
                ScreenReader.Say(Loc.Get("Select a year first using the arrow keys."));
                return;
            }
            UIListItem row = saga.years[_yearRowIndexes[_navIndex]];
            if (row == null) return;

            int yearKey = row.key;
            if (yearKey == Game.year)
            {
                ScreenReader.Say(Loc.Get("Cannot restore to the current year. Pick an earlier year first."));
                return;
            }
            if (!saga.showRestoreButton)
            {
                ScreenReader.Say(Loc.Get("Restore is not available on this view."));
                return;
            }
            if (!Game.canBeRestored)
            {
                int max = SafeRestoreMax();
                int used = Game.restores;
                if (max > 0 && used >= max)
                    ScreenReader.Say(Loc.Get("No restores remaining. Used ") + used + Loc.Get(" of ") + max + ".");
                else
                    ScreenReader.Say(Loc.Get("Restore is not available right now."));
                return;
            }

            // Make sure SagaView's private year field matches our focus —
            // RestoreGame() reads it directly. Re-running OnItemClicked is the
            // safe way to set it without bypassing the standard click path.
            try { saga.years.OnItemClicked(row); }
            catch (Exception ex) { DebugLogger.Error("SagaNav.TryRestore.OnItemClicked", ex); }

            // Trigger the game's own confirmation dialog. SixAgesDialog.OnShow
            // is patched in MenuPatches and will read the "Really Restore?"
            // title + body + CANCEL/RESTORE buttons aloud, giving the user the
            // standard escape hatch.
            try { saga.RestoreGame(); }
            catch (Exception ex) { DebugLogger.Error("SagaNav.TryRestore.RestoreGame", ex); }
        }

        // ---------- Status / full text (D and F5) ----------

        private void AnnounceFullText(SagaView saga)
        {
            if (saga.sagaText == null || string.IsNullOrEmpty(saga.sagaText.text))
            {
                ScreenReader.Say(Loc.Get("No saga text available for this year."));
                return;
            }
            ScreenReader.Say(StringHelpers.StripTags(saga.sagaText.text));
        }

        public void AnnounceFullStatus(SagaScreenController c)
        {
            SagaView saga = TryGetSaga();
            if (saga == null) { ScreenReader.Say(Loc.Get("Saga screen. No data available.")); return; }
            EnsureYearRowsBuilt(saga);

            var sb = new StringBuilder();
            sb.Append(Loc.Get("Saga. "));
            sb.Append(_yearRowIndexes.Count).Append(Loc.Get(" years. "));

            if (_navIndex >= 0 && _navIndex < _yearRowIndexes.Count)
            {
                UIListItem row = saga.years[_yearRowIndexes[_navIndex]];
                int yearKey = row != null ? row.key : -1;
                if (yearKey == 0) sb.Append(Loc.Get("Viewing ancient lore. "));
                else if (yearKey == Game.year) sb.Append(Loc.Get("Viewing current year. "));
                else sb.Append(Loc.Get("Viewing year ")).Append(SafeNameOfYear(yearKey)).Append(". ");
            }

            int max = SafeRestoreMax();
            int used = Game.restores;
            if (max > 0)
                sb.Append(Loc.Get("Restores used ")).Append(used).Append(Loc.Get(" of ")).Append(max).Append(". ");
            else
                sb.Append(Loc.Get("Restores: ")).Append(used).Append(Loc.Get(" used. "));

            if (Game.canBeRestored)
                sb.Append(Loc.Get("Restore available. Use arrows to pick a year, then Enter."));
            else if (max > 0 && used >= max)
                sb.Append(Loc.Get("No restores remaining."));
            else
                sb.Append(Loc.Get("Restore is not available right now."));

            ScreenReader.Say(sb.ToString());
        }

        private string GetRestoreStatusFor(SagaView saga, int yearKey)
        {
            if (yearKey == Game.year) return Loc.Get("Current year — cannot restore.");
            if (!saga.showRestoreButton) return Loc.Get("Restore not available here.");
            if (Game.canBeRestored) return Loc.Get("Enter to restore.");
            int max = SafeRestoreMax();
            int used = Game.restores;
            if (max > 0 && used >= max)
                return Loc.Get("No restores remaining (") + used + Loc.Get(" of ") + max + Loc.Get(" used).");
            return Loc.Get("Restore disabled.");
        }

        // ---------- Safe wrappers around game APIs ----------

        private static int SafeRestoreMax()
        {
            try { return Game.IntegerVariable("restoreMax"); }
            catch (Exception ex) { DebugLogger.Error("SagaNav.RestoreMax", ex); return 0; }
        }

        private static string SafeNameOfYear(int year)
        {
            try { return Game.NameOfYear(year); }
            catch (Exception ex) { DebugLogger.Error("SagaNav.NameOfYear", ex); return year.ToString(); }
        }

        private static string SafeNameOfRulerInYear(int year)
        {
            try { return Game.NameOfRulerInYear(year); }
            catch (Exception ex) { DebugLogger.Error("SagaNav.NameOfRulerInYear", ex); return null; }
        }

        private static bool AnyModifier()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }
    }
}
