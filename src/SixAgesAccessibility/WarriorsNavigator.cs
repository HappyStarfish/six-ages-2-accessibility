using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Zone navigation for WarriorsDialog. The dialog has one warrior-count slider
    /// and four conditional toggles (offerGifts, recruitFromClan, recruitOutside,
    /// severancePay). Two reasons it needs its own navigator rather than the flat
    /// Tab nav:
    ///
    /// <list type="number">
    /// <item><description>Mode-flip: dragging the slider above PlayerClan.warriors
    /// flips actionButton.label between RECRUIT and DISMISS, and selectively enables
    /// different toggle subsets. Sighted players see the icon change; we have to
    /// say it. Without this announcement Enter is a coin toss.</description></item>
    /// <item><description>Toggle gating: each slider tick fires WarriorsChanged
    /// which disables three toggles in dismiss mode (and one in recruit mode).
    /// Tab-cycling through them flatly would pick up disabled stragglers and
    /// confuse the user about which option actually applies.</description></item>
    /// </list>
    ///
    /// <para>Two zones: <see cref="Zone.Slider"/> (left/right adjusts, mode is
    /// announced after each adjust), <see cref="Zone.Toggles"/> (up/down cycles
    /// the four toggles, only the currently-active subset is mentioned out loud).
    /// A blank Enter triggers actionButton, Escape closes via the prefab X-icon
    /// (resolved through UIRoleResolver since WarriorsDialogController has no
    /// closeButton field).</para>
    /// </summary>
    public class WarriorsNavigator
    {
        private enum Zone { Slider, Toggles }

        // Toggle order matches the visual prefab layout (recruit-related first,
        // then the dismiss-only severancePay). Same order is used for cycling
        // and full-status reporting so the mental model stays stable.
        private enum ToggleId { OfferGifts = 0, RecruitFromClan = 1, RecruitOutside = 2, SeverancePay = 3 }
        private const int ToggleCount = 4;

        private Zone _zone = Zone.Slider;
        private int _toggleIndex = -1;
        private readonly ConfirmGate _confirmGate = new ConfirmGate();

        public void ResetForNewScreen()
        {
            _zone = Zone.Slider;
            _toggleIndex = -1;
            _confirmGate.Reset();
        }

        public void HandleInput(WarriorsDialogController d)
        {
            if (d == null) return;

            // Enter — recruit/dismiss, the one key that completes the screen.
            // Model Y: a blank Enter is the universal screen-completion key,
            // handled globally so it works from either zone.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TryRecruit(d);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TryClose(d);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _confirmGate.Reset();
                int dir = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? -1 : 1;
                CycleZone(dir);
                AnnounceZone(d);
                return;
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                AnnounceFullStatus(d);
                return;
            }

            switch (_zone)
            {
                case Zone.Slider:  HandleSliderInput(d);  break;
                case Zone.Toggles: HandleTogglesInput(d); break;
            }
        }

        // ---------- Slider zone ----------

        private void HandleSliderInput(WarriorsDialogController d)
        {
            if (d.slider == null) return;

            if (Input.GetKeyDown(KeyCode.LeftArrow))  { _confirmGate.Reset(); AdjustSlider(d, -1); return; }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { _confirmGate.Reset(); AdjustSlider(d, 1);  return; }
        }

        private void AdjustSlider(WarriorsDialogController d, int direction)
        {
            UISlider s = d.slider;
            if (s == null || !s.IsInteractable()) return;

            float step = s.wholeNumbers ? 1f : (s.maxValue - s.minValue) * 0.1f;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) step *= 5f;

            // Ctrl+arrow jumps to min/max — useful for quick max-recruit.
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                s.value = direction > 0 ? s.maxValue : s.minValue;
            else
                s.value = Mathf.Clamp(s.value + direction * step, s.minValue, s.maxValue);

            // s.onValueChanged.Invoke(...) has already fired through the slider's
            // setter, which runs WarriorsChanged → updates actionButton label and
            // toggle interactability. Now report the new state to the user.
            AnnounceSliderState(d);
        }

        private void AnnounceSliderState(WarriorsDialogController d)
        {
            if (d.slider == null) return;
            int target = d.slider.intValue;
            int current = PlayerClan.warriors;
            int diff = target - current;
            int max = (int)d.slider.maxValue;

            var sb = new StringBuilder();
            sb.Append(Loc.Get("Warriors: ")).Append(target).Append(Loc.Get(" of ")).Append(max);
            if (diff > 0)
                sb.Append(Loc.Get(". Recruiting ")).Append(diff).Append(Loc.Get(diff == 1 ? " warrior." : " warriors."));
            else if (diff < 0)
                sb.Append(Loc.Get(". Dismissing ")).Append(-diff).Append(Loc.Get(-diff == 1 ? " warrior." : " warriors."));
            else
                sb.Append(Loc.Get(". No change."));
            ScreenReader.Say(sb.ToString());
        }

        // ---------- Toggles zone ----------

        private void HandleTogglesInput(WarriorsDialogController d)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))   { _confirmGate.Reset(); _toggleIndex = WrapDecrement(_toggleIndex, ToggleCount); AnnounceCurrentToggle(d); return; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) { _confirmGate.Reset(); _toggleIndex = WrapIncrement(_toggleIndex, ToggleCount); AnnounceCurrentToggle(d); return; }
            // Space flips the focused toggle. Enter is reserved for the
            // recruit/dismiss action (handled in HandleInput).
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _confirmGate.Reset();
                ToggleCurrent(d);
                return;
            }
        }

        private void AnnounceCurrentToggle(WarriorsDialogController d)
        {
            if (_toggleIndex < 0) _toggleIndex = 0;
            UIToggle t = GetToggle(d, _toggleIndex);
            string name = ToggleName(_toggleIndex);
            if (t == null)
            {
                ScreenReader.Say(name + Loc.Get(", missing."));
                return;
            }

            string state = Loc.Get(t.isOn ? "on" : "off");
            string availability = t.IsInteractable() ? "" : Loc.Get(", not available in current mode");
            ScreenReader.Say(name + ", " + state + availability);
        }

        private void ToggleCurrent(WarriorsDialogController d)
        {
            if (_toggleIndex < 0) _toggleIndex = 0;
            UIToggle t = GetToggle(d, _toggleIndex);
            if (t == null) return;

            string name = ToggleName(_toggleIndex);
            if (!t.IsInteractable())
            {
                ScreenReader.Say(name + Loc.Get(" is not available in the current mode. "
                    + "Use the slider to switch between recruiting and dismissing."));
                return;
            }

            // ISubmitHandler.OnSubmit mirrors a click — fires onValueChanged so the
            // dialog's RecruitingChanged updates actionButton interactability and
            // recalculates the slider's max value if offerGifts changed.
            ISubmitHandler handler = t as ISubmitHandler;
            if (handler != null)
                handler.OnSubmit(new BaseEventData(EventSystem.current));

            ScreenReader.Say(name + " " + Loc.Get(t.isOn ? "on" : "off"));
        }

        private static UIToggle GetToggle(WarriorsDialogController d, int index)
        {
            switch ((ToggleId)index)
            {
                case ToggleId.OfferGifts:      return d.offerGifts;
                case ToggleId.RecruitFromClan: return d.recruitFromClan;
                case ToggleId.RecruitOutside:  return d.recruitOutside;
                case ToggleId.SeverancePay:    return d.severancePay;
                default: return null;
            }
        }

        private static string ToggleName(int index)
        {
            switch ((ToggleId)index)
            {
                case ToggleId.OfferGifts:      return Loc.Get("Offer gifts (raises recruit cost)");
                case ToggleId.RecruitFromClan: return Loc.Get("Recruit from clan");
                case ToggleId.RecruitOutside:  return Loc.Get("Recruit outsiders");
                case ToggleId.SeverancePay:    return Loc.Get("Severance pay (softens dismissal)");
                default: return Loc.Get("Toggle");
            }
        }

        // ---------- Zone management ----------

        private void CycleZone(int dir)
        {
            int z = (int)_zone + dir;
            if (z < 0) z = 1;
            if (z > 1) z = 0;
            _zone = (Zone)z;
        }

        private void AnnounceZone(WarriorsDialogController d)
        {
            switch (_zone)
            {
                case Zone.Slider:
                    ScreenReader.Say(Loc.Get("Slider zone."));
                    AnnounceSliderState(d);
                    return;
                case Zone.Toggles:
                    ScreenReader.Say(Loc.Get("Toggle zone."));
                    AnnounceCurrentToggle(d);
                    return;
            }
        }

        // ---------- Primary action / close ----------

        private void TryRecruit(WarriorsDialogController d)
        {
            UIButton ab = d.actionButton;
            if (ab == null || !ab.gameObject.activeSelf || !ab.IsInteractable())
            {
                _confirmGate.Reset();
                if (d.slider != null && d.slider.intValue == PlayerClan.warriors)
                    ScreenReader.Say(Loc.Get("No change to warriors. Move the slider first to recruit or dismiss."));
                else
                    ScreenReader.Say(Loc.Get("Action is not available right now."));
                return;
            }

            if (!_confirmGate.RequestOrConfirm(BuildWarriorsSummary(d)))
                return;

            try
            {
                ((ISubmitHandler)ab).OnSubmit(new BaseEventData(EventSystem.current));
            }
            catch (Exception ex)
            {
                DebugLogger.Error("WarriorsNav.TryRecruit", ex);
            }
        }

        /// <summary>
        /// Build the spoken summary for the two-step Enter confirmation. The
        /// slider determines whether the screen recruits or dismisses (diff
        /// against PlayerClan.warriors); the summary uses the sign to pick
        /// verb. Toggles aren't named here — they modify cost/eligibility but
        /// not the core "how many" message; F5 reads them in full.
        /// </summary>
        private static string BuildWarriorsSummary(WarriorsDialogController d)
        {
            if (d.slider == null) return Loc.Get("You change nothing.");
            int target = d.slider.intValue;
            int current = PlayerClan.warriors;
            int diff = target - current;
            if (diff > 0)
                return string.Format(
                    Loc.Get(diff == 1 ? "You recruit {0} warrior." : "You recruit {0} warriors."),
                    diff);
            if (diff < 0)
                return string.Format(
                    Loc.Get(-diff == 1 ? "You dismiss {0} warrior." : "You dismiss {0} warriors."),
                    -diff);
            return Loc.Get("You change nothing.");
        }

        private void TryClose(WarriorsDialogController d)
        {
            // WarriorsDialogController has no closeButton field — the X-icon is
            // wired purely in the prefab. UIRoleResolver finds it via onClick
            // method-name heuristic (same path the focused-button announcement uses
            // to label the icon "Close").
            UIButton closeBtn = UIRoleResolver.FindCloseButton(d);
            if (closeBtn != null && closeBtn.gameObject.activeSelf && closeBtn.IsInteractable())
            {
                try
                {
                    ((ISubmitHandler)closeBtn).OnSubmit(new BaseEventData(EventSystem.current));
                }
                catch (Exception ex)
                {
                    DebugLogger.Error("WarriorsNav.TryClose", ex);
                }
            }
        }

        // ---------- F5 full status ----------

        public void AnnounceFullStatus(WarriorsDialogController d)
        {
            if (d == null) return;
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Warriors dialog status. "));

            if (d.slider != null)
            {
                int target = d.slider.intValue;
                int current = PlayerClan.warriors;
                int max = (int)d.slider.maxValue;
                int diff = target - current;
                sb.Append(Loc.Get("Slider: ")).Append(target).Append(Loc.Get(" of ")).Append(max);
                if (diff > 0)      sb.Append(Loc.Get(", recruiting ")).Append(diff).Append(". ");
                else if (diff < 0) sb.Append(Loc.Get(", dismissing ")).Append(-diff).Append(". ");
                else               sb.Append(Loc.Get(", no change. "));
            }

            for (int i = 0; i < ToggleCount; i++)
            {
                UIToggle t = GetToggle(d, i);
                if (t == null) continue;
                sb.Append(ToggleName(i)).Append(": ").Append(Loc.Get(t.isOn ? "on" : "off"));
                if (!t.IsInteractable()) sb.Append(Loc.Get(" (locked)"));
                sb.Append(". ");
            }

            UIButton ab = d.actionButton;
            if (ab != null)
            {
                string verb = (ab.label != null && !string.IsNullOrEmpty(ab.label.text))
                    ? Loc.Get(StringHelpers.StripTags(ab.label.text)) : Loc.Get("Action");
                sb.Append(verb).Append(ab.IsInteractable() ? Loc.Get(" available. ") : Loc.Get(" disabled. "));
            }

            sb.Append(Loc.Get("Tab switches zones. Left and Right adjust the slider, Up and Down cycle toggles, Space flips the focused toggle, Enter confirms, Escape closes."));
            ScreenReader.Say(sb.ToString());
        }

        // ---------- Helpers ----------

        private static int WrapIncrement(int idx, int count)
        {
            if (count <= 0) return -1;
            idx = (idx < 0) ? 0 : idx + 1;
            if (idx >= count) idx = 0;
            return idx;
        }

        private static int WrapDecrement(int idx, int count)
        {
            if (count <= 0) return -1;
            idx = (idx < 0) ? count - 1 : idx - 1;
            if (idx < 0) idx = count - 1;
            return idx;
        }
    }
}
