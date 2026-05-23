using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Zone-based keyboard navigation for the Fortify dialog.
    ///
    /// FortifyDialogController has two UILists: <c>buildable</c> (the candidate
    /// fortifications, the only list with an action attached) and
    /// <c>fortifications</c> (existing fortifications, inspection only). The
    /// flat Tab nav can't reach either — UIList rows aren't Selectables — so
    /// without this navigator the user only ever sees the Close button and the
    /// (disabled) Fortify button. Enter then reports "Fortify is not available"
    /// instead of building anything.
    ///
    /// Keys follow the unified Model Y scheme: arrows browse, Space picks (and
    /// auto-selects to surface the cost), D reads details, a blank Enter completes
    /// the screen (Fortify), and Escape leaves. Tab toggles between Buildable and
    /// Existing zones; L jumps back to Buildable. The Enter path runs through the
    /// shared <see cref="ConfirmGate"/> for the two-step confirmation pattern.
    /// </summary>
    public class FortifyNavigator
    {
        private enum Zone { Buildable, Existing }

        private Zone _zone = Zone.Buildable;
        private int _buildableIndex;
        private int _existingIndex;
        private bool _buildableFirstAnnounceDone;
        private bool _existingFirstAnnounceDone;
        private readonly ConfirmGate _confirmGate = new ConfirmGate();

        /// <summary>Called when the dialog opens for the first time.</summary>
        public void ResetForNewScreen()
        {
            _zone = Zone.Buildable;
            _buildableIndex = 0;
            _existingIndex = 0;
            _buildableFirstAnnounceDone = false;
            _existingFirstAnnounceDone = false;
            _confirmGate.Reset();
        }

        /// <summary>Top-level dispatch — called every Update tick while the dialog is active.</summary>
        public void HandleInput(FortifyDialogController d)
        {
            if (d == null) return;

            // Enter — primary action (Fortify = build the selected fortification).
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TryFortify(d);
                return;
            }

            // Escape — leave without building.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TryClose(d);
                return;
            }

            // Tab / Shift+Tab — switch between Buildable and Existing zones.
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _confirmGate.Reset();
                _zone = (_zone == Zone.Buildable) ? Zone.Existing : Zone.Buildable;
                AnnounceZone(d);
                return;
            }

            // L — jump back to Buildable zone from anywhere.
            if (Input.GetKeyDown(KeyCode.L) && !AnyModifier())
            {
                _confirmGate.Reset();
                _zone = Zone.Buildable;
                AnnounceCurrentBuildable(d);
                return;
            }

            // F5 — full status (delegates to DialogContentReader-style output).
            if (Input.GetKeyDown(KeyCode.F5))
            {
                AnnounceFullStatus(d);
                return;
            }

            // Any zone-specific mutator key drops a pending Enter confirmation.
            // D and F5 are read-only and have already returned above.
            if (_confirmGate.IsPending
                && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)
                 || Input.GetKeyDown(KeyCode.Space)))
                _confirmGate.Reset();

            switch (_zone)
            {
                case Zone.Buildable: HandleBuildableInput(d); break;
                case Zone.Existing:  HandleExistingInput(d);  break;
            }
        }

        // ---------- Buildable zone ----------

        private void HandleBuildableInput(FortifyDialogController d)
        {
            if (d.buildable == null || d.buildable.count == 0)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                    ScreenReader.Say(Loc.Get("No buildable fortifications."));
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (_buildableFirstAnnounceDone)
                {
                    _buildableIndex--;
                    if (_buildableIndex < 0) _buildableIndex = d.buildable.count - 1;
                }
                SelectAndAnnounceBuildable(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (_buildableFirstAnnounceDone)
                {
                    _buildableIndex++;
                    if (_buildableIndex >= d.buildable.count) _buildableIndex = 0;
                }
                SelectAndAnnounceBuildable(d);
                return;
            }

            // Space — explicit "pick" (same effect as arrows because we auto-select
            // on cursor moves, but Space is the canonical select key elsewhere so
            // we accept it here too).
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SelectAndAnnounceBuildable(d);
                return;
            }

            // D — re-announce the focused item plus the dialog's cost label.
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceBuildableDescription(d);
                return;
            }
        }

        /// <summary>
        /// Move the dialog's own UIList selection to the cursor, fire OnItemClicked
        /// so <c>ValidateFortifyButton</c> runs (filling <c>costLabel</c> and
        /// flipping <c>actionButton.interactable</c>), then announce the result.
        /// Doing this on arrow keys — not only on Space — means the user hears the
        /// cost for each candidate as they browse, which is the whole point of the
        /// Fortify dialog: comparing what you can afford.
        /// </summary>
        private void SelectAndAnnounceBuildable(FortifyDialogController d)
        {
            if (_buildableIndex < 0) _buildableIndex = 0;
            if (_buildableIndex >= d.buildable.count) _buildableIndex = d.buildable.count - 1;

            UIListItem item = d.buildable[_buildableIndex];
            if (item == null) { _buildableFirstAnnounceDone = true; return; }

            // Mirrors a click: SetSelectedIndex + onItemClicked fires
            // FortifyDialogController.OnItemSelected → ValidateFortifyButton.
            d.buildable.selectedIndex = _buildableIndex;
            try { d.buildable.OnItemClicked(item); }
            catch (Exception ex) { DebugLogger.Error("FortifyNav.OnItemClicked", ex); }

            string name = StringHelpers.StripTags(item.text ?? "");
            string cost = (d.costLabel != null && !string.IsNullOrEmpty(d.costLabel.text))
                ? StringHelpers.StripTags(d.costLabel.text) : "";

            var sb = new StringBuilder();
            sb.Append(name);
            if (!string.IsNullOrEmpty(cost)) sb.Append(", ").Append(cost);
            if (d.actionButton == null || !d.actionButton.IsInteractable())
                sb.Append(Loc.Get(". Build is disabled (insufficient goods or invalid choice)."));
            else
                sb.Append(Loc.Get(". Press Enter to build."));

            ScreenReader.Say(sb.ToString());
            _buildableFirstAnnounceDone = true;
        }

        private void AnnounceCurrentBuildable(FortifyDialogController d)
        {
            if (d.buildable == null || d.buildable.count == 0)
            {
                ScreenReader.Say(Loc.Get("No buildable fortifications."));
                return;
            }
            SelectAndAnnounceBuildable(d);
        }

        private void AnnounceBuildableDescription(FortifyDialogController d)
        {
            if (d.buildable == null || _buildableIndex < 0 || _buildableIndex >= d.buildable.count) return;
            UIListItem item = d.buildable[_buildableIndex];
            if (item == null) return;
            // The UIList row carries the fortification's display name only — the
            // game doesn't surface a per-item description in any text field. Read
            // back the name plus the live cost label so D remains useful (re-read).
            string name = StringHelpers.StripTags(item.text ?? "");
            string cost = (d.costLabel != null && !string.IsNullOrEmpty(d.costLabel.text))
                ? StringHelpers.StripTags(d.costLabel.text) : Loc.Get("no cost shown");
            ScreenReader.Say(name + ", " + cost);
        }

        // ---------- Existing zone (inspection only) ----------

        private void HandleExistingInput(FortifyDialogController d)
        {
            if (d.fortifications == null || d.fortifications.count == 0)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                    ScreenReader.Say(Loc.Get("No existing fortifications."));
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (_existingFirstAnnounceDone)
                {
                    _existingIndex--;
                    if (_existingIndex < 0) _existingIndex = d.fortifications.count - 1;
                }
                AnnounceCurrentExisting(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (_existingFirstAnnounceDone)
                {
                    _existingIndex++;
                    if (_existingIndex >= d.fortifications.count) _existingIndex = 0;
                }
                AnnounceCurrentExisting(d);
                return;
            }

            // Space here would be misleading — the existing list is read-only;
            // tell the user to use the Buildable zone instead of silently nothing.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ScreenReader.Say(Loc.Get("Existing fortifications are read-only. Press Tab to switch to buildable."));
                return;
            }
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceCurrentExisting(d);
                return;
            }
        }

        private void AnnounceCurrentExisting(FortifyDialogController d)
        {
            if (d.fortifications == null || d.fortifications.count == 0)
            {
                ScreenReader.Say(Loc.Get("No existing fortifications."));
                return;
            }
            if (_existingIndex < 0) _existingIndex = 0;
            if (_existingIndex >= d.fortifications.count) _existingIndex = d.fortifications.count - 1;

            UIListItem item = d.fortifications[_existingIndex];
            string name = item != null && !string.IsNullOrEmpty(item.text)
                ? StringHelpers.StripTags(item.text) : Loc.Get("unknown fortification");
            ScreenReader.Say(name);
            _existingFirstAnnounceDone = true;
        }

        // ---------- Zone announcement ----------

        private void AnnounceZone(FortifyDialogController d)
        {
            switch (_zone)
            {
                case Zone.Buildable:
                    int bc = d.buildable != null ? d.buildable.count : 0;
                    ScreenReader.Say(Loc.Get("Buildable fortifications, ") + bc
                        + (bc == 1 ? Loc.Get(" entry.") : Loc.Get(" entries.")));
                    AnnounceCurrentBuildable(d);
                    break;
                case Zone.Existing:
                    int ec = d.fortifications != null ? d.fortifications.count : 0;
                    ScreenReader.Say(Loc.Get("Existing fortifications, ") + ec
                        + (ec == 1 ? Loc.Get(" entry.") : Loc.Get(" entries.")));
                    AnnounceCurrentExisting(d);
                    break;
            }
        }

        // ---------- Primary action (Enter) ----------

        private void TryFortify(FortifyDialogController d)
        {
            if (d.actionButton == null || !d.actionButton.IsInteractable())
            {
                _confirmGate.Reset();
                ScreenReader.Say(Loc.Get("Cannot fortify yet. Pick a buildable fortification you can afford."));
                return;
            }
            if (_confirmGate.RequestOrConfirm(BuildFortifySummary(d)))
                SubmitButton(d.actionButton);
        }

        /// <summary>
        /// Build the spoken summary for the two-step Enter confirmation: which
        /// fortification will be built and what it costs. Both come from the
        /// dialog's live UI state — the selected buildable row supplies the name,
        /// the cost label is already filled by ValidateFortifyButton.
        /// </summary>
        private static string BuildFortifySummary(FortifyDialogController d)
        {
            string name = Loc.Get("a fortification");
            if (d.buildable != null && d.buildable.selectedIndex >= 0
                && d.buildable.selectedIndex < d.buildable.count)
            {
                UIListItem item = d.buildable[d.buildable.selectedIndex];
                if (item != null && !string.IsNullOrEmpty(item.text))
                    name = StringHelpers.StripTags(item.text);
            }
            string cost = (d.costLabel != null && !string.IsNullOrEmpty(d.costLabel.text))
                ? StringHelpers.StripTags(d.costLabel.text) : "";
            return string.IsNullOrEmpty(cost)
                ? string.Format(Loc.Get("You build {0}."), name)
                : string.Format(Loc.Get("You build {0}. {1}."), name, cost);
        }

        // ---------- Close (Esc) ----------

        private static void TryClose(FortifyDialogController d)
        {
            // FortifyDialogController has no closeButton field — the X-icon is
            // wired in the prefab. UIRoleResolver finds it via onClick heuristic.
            UIButton closeBtn = UIRoleResolver.FindCloseButton(d);
            if (closeBtn != null && closeBtn.gameObject.activeSelf && closeBtn.IsInteractable())
                SubmitButton(closeBtn);
            else
                ScreenReader.Say(Loc.Get("Close button not found."));
        }

        // ---------- Full status (F5) ----------

        public void AnnounceFullStatus(FortifyDialogController d)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Fortify status. "));

            int bc = d.buildable != null ? d.buildable.count : 0;
            int ec = d.fortifications != null ? d.fortifications.count : 0;
            sb.Append(bc).Append(Loc.Get(" buildable, "));
            sb.Append(ec).Append(Loc.Get(" existing. "));

            string cost = (d.costLabel != null && !string.IsNullOrEmpty(d.costLabel.text))
                ? StringHelpers.StripTags(d.costLabel.text) : "";
            if (!string.IsNullOrEmpty(cost)) sb.Append(cost).Append(". ");

            if (d.actionButton != null)
                sb.Append(d.actionButton.IsInteractable()
                    ? Loc.Get("Build available. ")
                    : Loc.Get("Build disabled. "));

            sb.Append(Loc.Get("Up and Down browse, Tab switches zones, L returns to buildable, D re-reads, Enter builds, Escape closes."));
            ScreenReader.Say(sb.ToString());
        }

        // ---------- Helpers ----------

        private static void SubmitButton(UIButton button)
        {
            if (button == null) return;
            ISubmitHandler handler = button as ISubmitHandler;
            if (handler == null) return;
            handler.OnSubmit(new BaseEventData(EventSystem.current));
        }

        private static bool AnyModifier()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)
                || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
    }
}
