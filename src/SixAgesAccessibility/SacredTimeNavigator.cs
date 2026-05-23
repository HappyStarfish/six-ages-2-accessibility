using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Two-zone keyboard navigation for the year-end Sacred Time screen.
    ///
    /// Sacred Time has two interactive regions: a forecast text (story of the
    /// past year + omens for the next) and a magic-allocation block of ten
    /// UIToggleLines (Fields..War) for distributing PlayerClan.magic into
    /// blessings. Two action buttons (Saga, Proceed) live on the screen but
    /// are NOT modeled as a navigation zone — per the project's Model Y action
    /// pattern, the primary action is always a blank Enter and secondary actions
    /// are reachable via direct hotkeys, no separate Action zone.
    ///
    /// Key bindings:
    ///   Tab / Shift+Tab        — switch between Forecast and Allocation zones
    ///   Enter                  — Proceed (advance to next year), always
    ///   G                      — open the Saga chronicle dialog, always
    ///   Esc                    — orientation hint (no back action exists)
    ///   F5                     — full status (reserve + allocations + forecast preview)
    ///
    /// Forecast zone:
    ///   Up / Down              — read previous / next paragraph of the forecast
    ///   D                      — re-read the year header (year + reserve)
    ///
    /// Allocation zone:
    ///   Up / Down              — switch between the 10 lines (Fields..War)
    ///   Right / Left           — increase / decrease the focused line's value
    ///   D                      — re-announce focused line + remaining reserve
    ///
    /// SagaDialog overlay: SacredTime.ShowSaga reparents the shared SagaView
    /// into a slide-in dialog. The active screen stays SacredTime, so the
    /// overlay is detected via SacredTime.sagaDialog.gameObject.activeSelf
    /// and we switch into year-list mode (mirrors SagaNavigator without
    /// Restore — the dialog disables it via showRestoreButton=false).
    /// </summary>
    public class SacredTimeNavigator
    {
        private enum Mode { Main, SagaDialog }
        private enum Zone { Forecast, Allocation }

        private readonly KeyboardNavigationHandler _host;

        private Mode _mode = Mode.Main;
        private Zone _zone = Zone.Forecast;
        private int _allocIndex;            // 0..9 — index into SacredTime.toggleLines
        private bool _sagaDialogOpenLastTick;
        private bool _openingAnnounced;
        private readonly ConfirmGate _confirmGate = new ConfirmGate();

        // Forecast paragraph navigation. PlayerClan.forecast is a multi-paragraph
        // narrative; reading the whole block at once is too much, scrolling by
        // pixels is meaningless to a screen reader. We split into paragraphs on
        // entry and walk them one at a time via Up/Down.
        private string[] _forecastParagraphs;
        private int _forecastParaIndex = -1;
        private string _forecastSourceCached;

        // Saga-dialog mode state (mirrors SagaNavigator without restore).
        private readonly List<int> _sagaYearRows = new List<int>();
        private int _sagaYearIndex = -1;
        private int _sagaLastBuiltCount = -1;

        public SacredTimeNavigator(KeyboardNavigationHandler host) { _host = host; }

        /// <summary>Reset zone + indices when entering Sacred Time for a new year.</summary>
        public void ResetForNewScreen()
        {
            _mode = Mode.Main;
            _zone = Zone.Forecast;
            _allocIndex = 0;
            _sagaDialogOpenLastTick = false;
            _openingAnnounced = false;
            _forecastParagraphs = null;
            _forecastParaIndex = -1;
            _forecastSourceCached = null;
            _sagaYearRows.Clear();
            _sagaYearIndex = -1;
            _sagaLastBuiltCount = -1;
            _confirmGate.Reset();
        }

        /// <summary>Top-level dispatch — called every Update while a SacredTime screen is active.</summary>
        public void HandleInput(SacredTime st)
        {
            if (st == null) return;

            // After Proceed, the year-advance briefly re-activates this screen
            // each time a dialog closes on top of it. Stay completely inert in
            // that window: no opening announcement, and no input handling — so a
            // stray Enter cannot fire a second Proceed. SacredTimePatches clears
            // this once next year's Sacred Time genuinely opens.
            if (Patches.SacredTimePatches.SuppressSacredTimeAnnouncements) return;

            // Detect SagaDialog open/close transitions. Re-parents the shared
            // SagaView into the slide-in dialog without changing the active
            // ScreenController, so we have to watch the dialog GO ourselves.
            bool dialogOpen = st.sagaDialog != null && st.sagaDialog.gameObject.activeSelf;
            if (dialogOpen && !_sagaDialogOpenLastTick)
            {
                _mode = Mode.SagaDialog;
                _sagaYearRows.Clear();
                _sagaYearIndex = -1;
                _sagaLastBuiltCount = -1;
                AnnounceSagaDialogOpened(st);
            }
            else if (!dialogOpen && _sagaDialogOpenLastTick)
            {
                _mode = Mode.Main;
                ScreenReader.Say("Saga closed. Back to Sacred Time.");
            }
            _sagaDialogOpenLastTick = dialogOpen;

            if (_mode == Mode.SagaDialog)
            {
                HandleSagaDialogInput(st);
                return;
            }

            // First-time arrival summary.
            if (!_openingAnnounced)
            {
                _openingAnnounced = true;
                AnnounceOpening(st);
            }

            HandleMainInput(st);
        }

        // ============================================================
        // Main mode
        // ============================================================

        private void HandleMainInput(SacredTime st)
        {
            // Enter — primary action: Proceed (advance to next year). Model Y:
            // a blank Enter is the universal screen-completion key, and it
            // always wins over the zone-local handlers.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TryProceed(st);
                return;
            }

            // G — open the Saga chronicle dialog. Secondary action, reachable
            // from any zone via this dedicated hotkey (no separate Action zone).
            if (Input.GetKeyDown(KeyCode.G) && !AnyModifier())
            {
                _confirmGate.Reset();
                TryShowSaga(st);
                return;
            }

            // Tab / Shift+Tab — cycle between Forecast and Allocation.
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _confirmGate.Reset();
                _zone = (_zone == Zone.Forecast) ? Zone.Allocation : Zone.Forecast;
                AnnounceZone(st);
                return;
            }

            // Escape — orientation help (Sacred Time has no back).
            if (Input.GetKeyDown(KeyCode.Escape) && !AnyModifier())
            {
                ScreenReader.Say(Loc.Get("Sacred Time has no back. Enter to continue, G for Saga, Tab to switch between Forecast and Allocation."));
                return;
            }

            // F5 — full status (reserve + all allocations + forecast preview).
            if (Input.GetKeyDown(KeyCode.F5))
            {
                AnnounceFullStatus(st);
                return;
            }

            // Any allocation-mutating key drops a pending Enter confirmation.
            // Forecast Up/Down only walks paragraphs (read-only navigation), but
            // they also serve as Allocation cursor moves — disarming on them in
            // both zones is correct because the user has moved away from the
            // moment they heard the summary.
            if (_confirmGate.IsPending
                && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)
                 || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)))
                _confirmGate.Reset();

            switch (_zone)
            {
                case Zone.Forecast:   HandleForecastInput(st);   break;
                case Zone.Allocation: HandleAllocationInput(st); break;
            }
        }

        private void AnnounceZone(SacredTime st)
        {
            switch (_zone)
            {
                case Zone.Forecast:
                    ScreenReader.Say(Loc.Get("Forecast zone. Up and Down read paragraphs."));
                    break;
                case Zone.Allocation:
                    ScreenReader.Say(Loc.Get("Magic allocation zone. ") + AllocationFocusLine(st)
                        + Loc.Get(" Right and Left to adjust, Up and Down to switch lines."));
                    break;
            }
        }

        // ---------- Forecast zone ----------

        private void HandleForecastInput(SacredTime st)
        {
            // D — re-read the year header so the user can re-orient.
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceYearHeader(st);
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) && !AnyModifier())
            {
                MoveForecastParagraph(-1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow) && !AnyModifier())
            {
                MoveForecastParagraph(+1);
                return;
            }
        }

        /// <summary>
        /// Advance to the previous/next paragraph and read it. The split is
        /// done lazily on first navigation (and rebuilt if PlayerClan.forecast
        /// changes mid-screen, which it doesn't in normal play but is cheap to
        /// detect). Empty paragraphs are skipped on the way in via the split.
        /// </summary>
        private void MoveForecastParagraph(int dir)
        {
            EnsureForecastParagraphsBuilt();
            if (_forecastParagraphs == null || _forecastParagraphs.Length == 0)
            {
                ScreenReader.Say("No forecast text available.");
                return;
            }

            // Initialize on first move: -1 + +1 = 0 (top); -1 + -1 wraps to last.
            int next = _forecastParaIndex + dir;
            if (next < 0)
            {
                if (_forecastParaIndex < 0) next = 0;
                else { ScreenReader.Say(Loc.Get("Beginning of forecast.")); return; }
            }
            if (next >= _forecastParagraphs.Length)
            {
                ScreenReader.Say(Loc.Get("End of forecast."));
                return;
            }
            _forecastParaIndex = next;
            ScreenReader.Say(_forecastParagraphs[_forecastParaIndex]);
        }

        private void EnsureForecastParagraphsBuilt()
        {
            string src = SafeForecast();
            if (src == _forecastSourceCached && _forecastParagraphs != null) return;
            _forecastSourceCached = src;

            if (string.IsNullOrEmpty(src))
            {
                _forecastParagraphs = new string[0];
                _forecastParaIndex = -1;
                return;
            }

            string clean = StringHelpers.StripTags(src);
            // Split on blank-line separators (\n\n or variants) AND single
            // newlines — the forecast in this game uses single newlines between
            // sentences for some paragraph breaks. Treat any line as its own
            // paragraph after trimming, drop empties.
            string[] raw = clean.Split(new[] { "\r\n\r\n", "\n\n", "\r\n", "\n" }, StringSplitOptions.None);
            var list = new List<string>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                string p = raw[i].Trim();
                if (p.Length > 0) list.Add(p);
            }
            _forecastParagraphs = list.ToArray();
            _forecastParaIndex = -1;
        }

        private void AnnounceYearHeader(SacredTime st)
        {
            string year = SafeYearName();
            int reserve = SafeMagicReserve(st);
            int used = SafeMagicUsed(st);
            int remaining = reserve - used;
            ScreenReader.Say(Loc.Get("Year of ") + (string.IsNullOrEmpty(year) ? Loc.Get("the unknown") : year)
                + Loc.Get(". Magic reserve ") + reserve + ", " + remaining + Loc.Get(" remaining."));
        }

        // ---------- Allocation zone ----------

        private static readonly string[] _allocNames = {
            "Fields", "Pastures", "Wilds", "Crafts", "Harmony",
            "Health", "Exploring", "Ritual", "Diplomacy", "War"
        };

        private void HandleAllocationInput(SacredTime st)
        {
            int count = SafeAllocCount(st);
            if (count == 0)
            {
                ScreenReader.Say(Loc.Get("No allocation lines available."));
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) && !AnyModifier())
            {
                _allocIndex = (_allocIndex - 1 + count) % count;
                AnnounceCurrentAllocation(st, includeReserve: false);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow) && !AnyModifier())
            {
                _allocIndex = (_allocIndex + 1) % count;
                AnnounceCurrentAllocation(st, includeReserve: false);
                return;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow) && !AnyModifier())
            {
                AdjustCurrentAllocation(st, +1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow) && !AnyModifier())
            {
                AdjustCurrentAllocation(st, -1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceCurrentAllocation(st, includeReserve: true);
                return;
            }
        }

        /// <summary>Announce the focused allocation line; toggle nothing.</summary>
        private void AnnounceCurrentAllocation(SacredTime st, bool includeReserve)
        {
            ScreenReader.Say(AllocationFocusLine(st)
                + (includeReserve ? " " + ReserveSuffix(st) : ""));
        }

        private string AllocationFocusLine(SacredTime st)
        {
            int count = SafeAllocCount(st);
            if (_allocIndex < 0 || _allocIndex >= count) return Loc.Get("No line.");
            UIToggleLine line = SafeAllocLine(st, _allocIndex);
            string name = Loc.Get(_allocNames[_allocIndex]);
            if (line == null) return name + Loc.Get(" unavailable.");
            int v = SafeIntValue(line);
            int max = line.availableToggleCount;
            return name + ": " + v + Loc.Get(" of ") + max + ".";
        }

        private string ReserveSuffix(SacredTime st)
        {
            int reserve = SafeMagicReserve(st);
            int used = SafeMagicUsed(st);
            return Loc.Get("Reserve ") + (reserve - used) + Loc.Get(" of ") + reserve + Loc.Get(" remaining.");
        }

        /// <summary>
        /// Increment or decrement the focused line's intValue by one. Toggles
        /// the next-on-or-next-off Toggle directly so the cascading on/off
        /// behavior in <see cref="UIToggleLine.OnToggle"/> kicks in for free,
        /// and <see cref="SacredTime.OnToggle"/> fires to recompute capacity
        /// across all lines (the global magicReserve constraint).
        /// </summary>
        private void AdjustCurrentAllocation(SacredTime st, int direction)
        {
            int count = SafeAllocCount(st);
            if (_allocIndex < 0 || _allocIndex >= count) return;

            UIToggleLine line = SafeAllocLine(st, _allocIndex);
            if (line == null) { ScreenReader.Say(_allocNames[_allocIndex] + " unavailable."); return; }

            int max = line.availableToggleCount;
            if (max == 0)
            {
                ScreenReader.Say(_allocNames[_allocIndex] + " has no slots this year.");
                return;
            }

            int currentValue = SafeIntValue(line);

            if (direction > 0)
            {
                // Already at local max?
                if (currentValue >= max)
                {
                    ScreenReader.Say(_allocNames[_allocIndex] + " already at maximum, " + max + ".");
                    return;
                }

                // Global capacity exhausted? Other lines have already eaten the
                // pool. UIToggleLine.SetCapacity sets toggles[i].interactable
                // false in that case, so we mirror that check up front.
                int reserve = SafeMagicReserve(st);
                int used = SafeMagicUsed(st);
                if (used >= reserve)
                {
                    ScreenReader.Say(Loc.Get("Magic reserve empty. Reduce another allocation first."));
                    return;
                }

                Toggle t = line.toggles[currentValue]; // turning the next-off slot ON
                if (t == null || !t.interactable)
                {
                    ScreenReader.Say(_allocNames[_allocIndex] + " is locked at " + currentValue + ".");
                    return;
                }
                t.isOn = true;
            }
            else
            {
                if (currentValue <= 0)
                {
                    ScreenReader.Say(_allocNames[_allocIndex] + " already at zero.");
                    return;
                }
                Toggle t = line.toggles[currentValue - 1]; // turning the highest-on slot OFF
                if (t == null) return;
                t.isOn = false;
            }

            // Re-read AFTER setting — UIToggleLine.OnToggle cascades, and
            // SacredTime.OnToggle re-bounds every line's capacity. Both run
            // synchronously, so the new intValue and reserve are correct.
            ScreenReader.Say(AllocationFocusLine(st) + " " + ReserveSuffix(st));
        }

        // ---------- Actions ----------

        private void TryShowSaga(SacredTime st)
        {
            try { st.ShowSaga(); }
            catch (Exception ex)
            {
                DebugLogger.Error("SacredTimeNav.ShowSaga", ex);
                ScreenReader.Say("Could not open the Saga.");
            }
        }

        private void TryProceed(SacredTime st)
        {
            if (!_confirmGate.RequestOrConfirm(BuildAllocationSummary(st)))
                return;
            try { st.Proceed(); }
            catch (Exception ex)
            {
                DebugLogger.Error("SacredTimeNav.Proceed", ex);
                ScreenReader.Say("Could not advance from Sacred Time.");
            }
        }

        /// <summary>
        /// Build the spoken summary for the two-step Enter confirmation. Lists
        /// only the allocation lines the player put magic into (skip zeros so
        /// the announcement doesn't read "Fields 0, Pastures 0, ..."). Names the
        /// total committed and what stays in reserve.
        /// </summary>
        private string BuildAllocationSummary(SacredTime st)
        {
            int count = SafeAllocCount(st);
            int reserve = SafeMagicReserve(st);
            int used = SafeMagicUsed(st);
            int remaining = reserve - used;

            var parts = new List<string>();
            for (int i = 0; i < count && i < _allocNames.Length; i++)
            {
                UIToggleLine line = SafeAllocLine(st, i);
                if (line == null) continue;
                int v = SafeIntValue(line);
                if (v <= 0) continue;
                parts.Add(Loc.Get(_allocNames[i]) + " " + v);
            }

            if (parts.Count == 0)
                return string.Format(Loc.Get("You allocate no magic. Reserve {0} unused."), reserve);

            string joined = StringHelpers.JoinList(parts, Loc.Get("and"));
            return string.Format(Loc.Get("You allocate magic: {0}. Reserve {1} of {2} unused."),
                joined, remaining, reserve);
        }

        // ---------- Opening + full status ----------

        private void AnnounceOpening(SacredTime st)
        {
            string year = SafeYearName();
            int reserve = SafeMagicReserve(st);
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Sacred Time, year of ")).Append(string.IsNullOrEmpty(year) ? Loc.Get("the unknown") : year).Append(". ");
            sb.Append(Loc.Get("Magic reserve ")).Append(reserve).Append(". ");
            sb.Append(Loc.Get("Up and Down read forecast paragraphs. Tab switches to Magic allocation. G for Saga, Enter to continue."));
            ScreenReader.Say(sb.ToString(), interrupt: false);
        }

        public void AnnounceFullStatus(SacredTime st)
        {
            if (st == null) { ScreenReader.Say(Loc.Get("Sacred Time. No data available.")); return; }
            int reserve = SafeMagicReserve(st);
            int used = SafeMagicUsed(st);

            var sb = new StringBuilder();
            sb.Append(Loc.Get("Sacred Time. Year of ")).Append(SafeYearName()).Append(". ");
            sb.Append(Loc.Get("Magic ")).Append(used).Append(Loc.Get(" used of ")).Append(reserve).Append(". ");

            int count = SafeAllocCount(st);
            for (int i = 0; i < count; i++)
            {
                UIToggleLine line = SafeAllocLine(st, i);
                if (line == null) continue;
                int v = SafeIntValue(line);
                int max = line.availableToggleCount;
                if (max == 0) continue;
                sb.Append(Loc.Get(_allocNames[i])).Append(' ').Append(v).Append('/').Append(max).Append(". ");
            }

            EnsureForecastParagraphsBuilt();
            if (_forecastParagraphs != null && _forecastParagraphs.Length > 0)
            {
                sb.Append(Loc.Get("Forecast: ")).Append(_forecastParagraphs.Length).Append(Loc.Get(" paragraphs. "));
                sb.Append(_forecastParagraphs[0]);
                if (_forecastParagraphs.Length > 1) sb.Append(Loc.Get(" Up and Down for more."));
            }

            ScreenReader.Say(sb.ToString());
        }

        // ============================================================
        // Saga-dialog mode (overlay opened via SacredTime.ShowSaga)
        // ============================================================

        private void HandleSagaDialogInput(SacredTime st)
        {
            SagaView saga = SafeSagaFromDialog(st);
            if (saga == null || saga.years == null) return;

            EnsureSagaYearRowsBuilt(saga);

            // Escape — close the dialog. SagaDialog.Close drives the slide-out
            // animation and re-enables main-mode input on the next tick.
            if (Input.GetKeyDown(KeyCode.Escape) && !AnyModifier())
            {
                TryCloseSagaDialog(st);
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) && !AnyModifier())   { MoveSagaYear(saga, -1); return; }
            if (Input.GetKeyDown(KeyCode.DownArrow) && !AnyModifier()) { MoveSagaYear(saga, +1); return; }

            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceSagaFullText(saga);
                return;
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                AnnounceSagaCurrentYear(saga, includeText: true);
                return;
            }
        }

        private SagaView SafeSagaFromDialog(SacredTime st)
        {
            try { return st.sagaDialog != null ? st.sagaDialog.saga : null; }
            catch (Exception ex) { DebugLogger.Error("SacredTimeNav.SafeSagaFromDialog", ex); return null; }
        }

        private void AnnounceSagaDialogOpened(SacredTime st)
        {
            SagaView saga = SafeSagaFromDialog(st);
            int years = saga != null && saga.years != null ? saga.years.count : 0;
            ScreenReader.Say("Saga opened. Chronicle of " + years + " entries. Up and Down to navigate years, D for full text, Escape to close.");
        }

        private void EnsureSagaYearRowsBuilt(SagaView saga)
        {
            if (saga.years.count == _sagaLastBuiltCount && _sagaYearRows.Count > 0) return;
            _sagaYearRows.Clear();
            for (int i = 0; i < saga.years.count; i++)
            {
                UIListItem row = saga.years[i];
                if (row is UIListItemWithIcons) _sagaYearRows.Add(i);
            }
            _sagaLastBuiltCount = saga.years.count;

            if (_sagaYearIndex < 0)
            {
                for (int k = 0; k < _sagaYearRows.Count; k++)
                {
                    UIListItem row = saga.years[_sagaYearRows[k]];
                    if (row != null && row.isSelected) { _sagaYearIndex = k; break; }
                }
                if (_sagaYearIndex < 0 && _sagaYearRows.Count > 0)
                    _sagaYearIndex = _sagaYearRows.Count - 1; // newest year by default
            }
        }

        private void MoveSagaYear(SagaView saga, int dir)
        {
            if (_sagaYearRows.Count == 0) { ScreenReader.Say("No years available."); return; }
            int newIdx = _sagaYearIndex + dir;
            if (newIdx < 0) newIdx = _sagaYearRows.Count - 1;
            if (newIdx >= _sagaYearRows.Count) newIdx = 0;
            _sagaYearIndex = newIdx;

            UIListItem row = saga.years[_sagaYearRows[_sagaYearIndex]];
            if (row != null)
            {
                try { saga.years.OnItemClicked(row); }
                catch (Exception ex) { DebugLogger.Error("SacredTimeNav.MoveSagaYear", ex); }
            }
            AnnounceSagaCurrentYear(saga, includeText: true);
        }

        private void AnnounceSagaCurrentYear(SagaView saga, bool includeText)
        {
            if (_sagaYearIndex < 0 || _sagaYearIndex >= _sagaYearRows.Count)
            {
                ScreenReader.Say("No year selected.");
                return;
            }
            UIListItem row = saga.years[_sagaYearRows[_sagaYearIndex]];
            if (row == null) return;

            var sb = new StringBuilder();
            int yearKey = row.key;
            if (yearKey == 0) sb.Append("Ancient lore. ");
            else
            {
                string yearLabel = SafeNameOfYear(yearKey);
                sb.Append("Year ").Append(yearLabel).Append(". ");
                string ruler = SafeNameOfRulerInYear(yearKey);
                if (!string.IsNullOrEmpty(ruler)) sb.Append("Ruler ").Append(ruler).Append(". ");
            }

            if (includeText && saga.sagaText != null && !string.IsNullOrEmpty(saga.sagaText.text))
            {
                string txt = StringHelpers.StripTags(saga.sagaText.text);
                if (txt.Length > 200) txt = txt.Substring(0, 200) + "... Press D for full text.";
                sb.Append(txt);
            }
            ScreenReader.Say(sb.ToString());
        }

        private void AnnounceSagaFullText(SagaView saga)
        {
            if (saga.sagaText == null || string.IsNullOrEmpty(saga.sagaText.text))
            {
                ScreenReader.Say("No saga text available for this year.");
                return;
            }
            ScreenReader.Say(StringHelpers.StripTags(saga.sagaText.text));
        }

        private void TryCloseSagaDialog(SacredTime st)
        {
            try { st.sagaDialog.Close(); }
            catch (Exception ex)
            {
                DebugLogger.Error("SacredTimeNav.CloseSaga", ex);
                ScreenReader.Say("Could not close the Saga.");
            }
        }

        // ============================================================
        // Safe wrappers around game APIs
        // ============================================================

        private static string SafeYearName()
        {
            try { return Game.yearName; }
            catch (Exception ex) { DebugLogger.Error("SacredTimeNav.YearName", ex); return null; }
        }

        private static string SafeForecast()
        {
            try { return PlayerClan.forecast; }
            catch (Exception ex) { DebugLogger.Error("SacredTimeNav.Forecast", ex); return null; }
        }

        private static int SafeMagicReserve(SacredTime st)
        {
            try
            {
                // PlayerClan.magic is the total pool for this Sacred Time; it stays
                // unchanged until Proceed() commits the allocations.
                // st.reserve / st.magic UI texts switch to displaying the *remaining*
                // amount after the first OnToggle, so they're not safe to read for
                // "total reserve" — the private SacredTime.magicReserve field is the
                // canonical total, but it's private; PlayerClan.magic is the same
                // value SacredTime.OnShow snapshots into it, and is public.
                return PlayerClan.magic;
            }
            catch (Exception ex) { DebugLogger.Error("SacredTimeNav.Reserve", ex); return 0; }
        }

        private static int SafeMagicUsed(SacredTime st)
        {
            try { return st.magicUsed; }
            catch (Exception ex) { DebugLogger.Error("SacredTimeNav.Used", ex); return 0; }
        }

        private static int SafeAllocCount(SacredTime st)
        {
            try { return st.toggleLines != null ? st.toggleLines.Length : 0; }
            catch (Exception ex) { DebugLogger.Error("SacredTimeNav.AllocCount", ex); return 0; }
        }

        private static UIToggleLine SafeAllocLine(SacredTime st, int idx)
        {
            try
            {
                if (st.toggleLines == null) return null;
                if (idx < 0 || idx >= st.toggleLines.Length) return null;
                return st.toggleLines[idx];
            }
            catch (Exception ex) { DebugLogger.Error("SacredTimeNav.AllocLine", ex); return null; }
        }

        private static int SafeIntValue(UIToggleLine line)
        {
            try { return line.intValue; }
            catch (Exception ex) { DebugLogger.Error("SacredTimeNav.IntValue", ex); return 0; }
        }

        private static string SafeNameOfYear(int year)
        {
            try { return Game.NameOfYear(year); }
            catch (Exception ex) { DebugLogger.Error("SacredTimeNav.NameOfYear", ex); return year.ToString(); }
        }

        private static string SafeNameOfRulerInYear(int year)
        {
            try { return Game.NameOfRulerInYear(year); }
            catch (Exception ex) { DebugLogger.Error("SacredTimeNav.NameOfRulerInYear", ex); return null; }
        }

        private static bool AnyModifier()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftShift)   || Input.GetKey(KeyCode.RightShift)
                || Input.GetKey(KeyCode.LeftAlt)     || Input.GetKey(KeyCode.RightAlt);
        }
    }
}
