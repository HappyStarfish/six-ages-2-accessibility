using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Zone-based keyboard navigation for the Sacrifice dialog.
    ///
    /// SacrificeDialog has three regions: a list of blessing/effect radio buttons
    /// (UIDeityEffectItem.toggle with name + explanation labels), two amount sliders
    /// (goods + herds), and the action button. The flat Tab nav cannot read the
    /// blessing names from the toggles directly (the label is on a sibling
    /// TextMeshProUGUI inside UIDeityEffectItem) and gives no context for the
    /// sliders. This navigator wraps each zone with semantic announcements and
    /// gates Sacrifice activation on slider sums + interactability.
    ///
    /// Keys follow the unified Model Y scheme: arrows browse and adjust sliders,
    /// Space selects the focused blessing, D reads its description, a blank Enter
    /// completes the screen (Sacrifice), and Escape leaves.
    /// </summary>
    public class SacrificeNavigator
    {
        private enum Zone { Effects, Sliders }
        private enum SliderId { Goods = 0, Herds = 1 }
        private const int SliderCount = 2;

        // Default start zone is Sliders: SacrificeDialogController.SetupUI always
        // turns on a sensible default radio button (lastRadioButton — picks Healing
        // when wounded > sick, Curing when sick, etc.), so the user typically only
        // needs to set the offering amounts. Anyone who wants a different blessing
        // can Tab back to the Effects zone.
        private Zone _zone = Zone.Sliders;
        private int _effectIndex = -1;
        private int _sliderIndex = 0;
        private readonly ConfirmGate _confirmGate = new ConfirmGate();

        public void ResetForNewScreen()
        {
            _zone = Zone.Sliders;
            _effectIndex = -1;
            _sliderIndex = 0;
            _confirmGate.Reset();
        }

        public void HandleInput(SacrificeDialogController d)
        {
            if (d == null) return;

            // Enter — primary action (Sacrifice), the one key that completes the
            // screen. Model Y: a blank Enter is the universal screen-completion key,
            // handled globally so it works from every zone. No modifier is required.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TrySacrifice(d);
                return;
            }

            // Escape — leave without sacrificing.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TryClose(d);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                // Zone change drops the pending confirmation — the user's mental
                // context has shifted, so the next Enter should re-read the summary.
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
                case Zone.Effects: HandleEffectsInput(d); break;
                case Zone.Sliders: HandleSlidersInput(d); break;
            }
        }

        private void CycleZone(int dir)
        {
            int z = (int)_zone + dir;
            if (z < 0) z = 1;
            if (z > 1) z = 0;
            _zone = (Zone)z;
        }

        private void AnnounceZone(SacrificeDialogController d)
        {
            switch (_zone)
            {
                case Zone.Effects:  ScreenReader.Say(Loc.Get("Blessings.")); AnnounceCurrentEffect(d); break;
                case Zone.Sliders:  ScreenReader.Say(Loc.Get("Offerings.")); AnnounceCurrentSlider(d); break;
            }
        }

        // ---------- Effects zone ----------

        private void HandleEffectsInput(SacrificeDialogController d)
        {
            int count = SafeEffectCount(d);
            if (count == 0) return;

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _confirmGate.Reset();
                _effectIndex = NextVisibleEffectIndex(d, _effectIndex, -1);
                AnnounceCurrentEffect(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _confirmGate.Reset();
                _effectIndex = NextVisibleEffectIndex(d, _effectIndex, +1);
                AnnounceCurrentEffect(d);
                return;
            }
            // Space — select the focused blessing. Enter is reserved globally for
            // Sacrifice — see HandleInput.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SelectCurrentEffect(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceCurrentEffectExplanation(d);
                return;
            }
        }

        private void AnnounceCurrentEffect(SacrificeDialogController d)
        {
            int count = SafeEffectCount(d);
            if (count == 0) { ScreenReader.Say(Loc.Get("No blessings available.")); return; }
            if (_effectIndex < 0) _effectIndex = 0;
            if (_effectIndex >= count) _effectIndex = count - 1;

            var item = d.effects[_effectIndex];
            string state;
            if (item.toggle == null)
                state = "unavailable";
            else if (!item.toggle.gameObject.activeSelf)
                state = "hidden";
            else if (!item.toggle.interactable)
                state = "locked, not yet learned";
            else if (item.toggle.isOn)
                state = "selected";
            else
                state = "available";

            // State right after the name — heard even when fast arrow browsing
            // interrupts the announcement.
            ScreenReader.Say(SafeLabel(item) + ", " + Loc.Get(state));
        }

        private void AnnounceCurrentEffectExplanation(SacrificeDialogController d)
        {
            var item = SafeEffect(d, _effectIndex);
            if (item == null) return;
            string explanation = item.description != null ? StringHelpers.StripTags(item.description.text) : null;
            if (string.IsNullOrEmpty(explanation))
            {
                ScreenReader.Say(SafeLabel(item) + Loc.Get(", no description available."));
                return;
            }
            ScreenReader.Say(explanation);
        }

        private void SelectCurrentEffect(SacrificeDialogController d)
        {
            var item = SafeEffect(d, _effectIndex);
            if (item == null || item.toggle == null) return;
            if (!item.toggle.gameObject.activeSelf)
            {
                ScreenReader.Say(Loc.Get("Blessing not available."));
                return;
            }
            if (!item.toggle.interactable)
            {
                ScreenReader.Say(Loc.Get("Blessing not yet learned."));
                return;
            }
            if (item.toggle.isOn)
            {
                ScreenReader.Say(SafeLabel(item) + Loc.Get(" already selected."));
                return;
            }
            // UIDeityEffectItem.toggle is a UnityEngine.UI.Toggle (not UIToggle), so
            // assignment to isOn fires onValueChanged. The dialog's radio behavior
            // (turning others off) comes from the Toggle.group set in the prefab.
            item.toggle.isOn = true;
            _confirmGate.Reset();

            // Some blessings only work when the corresponding clan-state condition
            // is met (e.g. Chalana Arroy's Healing requires wounded clan members).
            // If the player picks one that won't apply, the game silently leaves
            // the Sacrifice button disabled. Surface that here so the user knows
            // immediately why they can't commit.
            string warn = ApplicabilityWarning(d, _effectIndex);
            if (!string.IsNullOrEmpty(warn))
                ScreenReader.Say(SafeLabel(item) + Loc.Get(" selected. ") + warn);
            else
                ScreenReader.Say(SafeLabel(item) + Loc.Get(" selected."));
        }

        // ---------- Sliders zone ----------

        private void HandleSlidersInput(SacrificeDialogController d)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _confirmGate.Reset();
                _sliderIndex--;
                if (_sliderIndex < 0) _sliderIndex = SliderCount - 1;
                AnnounceCurrentSlider(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _confirmGate.Reset();
                _sliderIndex++;
                if (_sliderIndex >= SliderCount) _sliderIndex = 0;
                AnnounceCurrentSlider(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                AdjustCurrentSlider(d, -1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                AdjustCurrentSlider(d, 1);
                return;
            }
            // D — universal detail key: re-read the focused slider.
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceCurrentSlider(d);
                return;
            }
        }

        private void AnnounceCurrentSlider(SacrificeDialogController d)
        {
            if (_sliderIndex < 0) _sliderIndex = 0;
            UISlider s = SliderAt(d, _sliderIndex);
            string label = SliderLabel(_sliderIndex);
            if (s == null)
            {
                ScreenReader.Say(label + Loc.Get(" slider not available."));
                return;
            }
            ScreenReader.Say(label + ": " + s.intValue + Loc.Get(" of ") + (int)s.maxValue
                + Loc.Get(". Left and Right to adjust, Shift for larger steps."));
        }

        private void AdjustCurrentSlider(SacrificeDialogController d, int dir)
        {
            UISlider s = SliderAt(d, _sliderIndex);
            if (s == null || !s.IsInteractable()) return;
            // Any slider-adjust keystroke drops the pending confirmation — even
            // when the value clamps and doesn't move, the user has signaled they
            // want to revise their input.
            _confirmGate.Reset();
            float step = s.wholeNumbers ? 1f : (s.maxValue - s.minValue) * 0.1f;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) step *= 5f;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                s.value = dir > 0 ? s.maxValue : s.minValue;
            else
                s.value = Mathf.Clamp(s.value + dir * step, s.minValue, s.maxValue);
            ScreenReader.Say(SliderLabel(_sliderIndex) + " " + s.intValue + Loc.Get(" of ") + (int)s.maxValue);
        }

        // ---------- Primary action (Enter) and Close (Esc) ----------

        private void TrySacrifice(SacrificeDialogController d)
        {
            if (d.actionButton == null || !d.actionButton.interactable)
            {
                // Don't leave the gate armed when the commit can't happen — the
                // user would otherwise hear the summary, the disable reason, and
                // then a second Enter would do nothing visible.
                _confirmGate.Reset();
                ScreenReader.Say(Loc.Get("Cannot sacrifice: ") + DescribeDisabledReason(d) + ".");
                return;
            }
            if (_confirmGate.RequestOrConfirm(BuildSacrificeSummary(d)))
                SubmitButton(d.actionButton);
        }

        /// <summary>
        /// Build the spoken summary for the two-step Enter confirmation: the recipient
        /// deity, the selected blessing, plus the non-zero offering sliders. The deity
        /// prefix is what turns a bare "you sacrifice Curing" into a meaningful "you
        /// sacrifice to Chalana Arroy: Curing" — the blessing name alone (kept in
        /// English so the game's internal lookups don't break) is otherwise opaque.
        /// Empty offerings are dropped from the sentence — the action button is gated
        /// on at least one non-zero slider, so reading "Goods 0 and Herds 5" would be noise.
        /// </summary>
        private static string BuildSacrificeSummary(SacrificeDialogController d)
        {
            int idx = SelectedIndex(d);
            string blessing = idx >= 0 ? SafeLabel(d.effects[idx]) : Loc.Get("no blessing");

            int goods = d.goodsSlider != null ? d.goodsSlider.intValue : 0;
            int herds = d.herdsSlider != null ? d.herdsSlider.intValue : 0;

            string deity = "";
            try
            {
                if (d.selectedDeity != Deity.deity_None)
                    deity = Game.NameOfDeity(d.selectedDeity);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("SacrificeNav.BuildSacrificeSummary.deity", ex);
            }

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(deity))
                sb.Append(string.Format(Loc.Get("You sacrifice to {0}: {1}"), deity, blessing));
            else
                sb.Append(string.Format(Loc.Get("You sacrifice {0}"), blessing));

            if (goods > 0 && herds > 0)
                sb.Append(string.Format(Loc.Get(" for {0} goods and {1} herds."), goods, herds));
            else if (goods > 0)
                sb.Append(string.Format(Loc.Get(" for {0} goods."), goods));
            else if (herds > 0)
                sb.Append(string.Format(Loc.Get(" for {0} herds."), herds));
            else
                sb.Append('.');

            return sb.ToString();
        }

        private static void TryClose(SacrificeDialogController d)
        {
            var cb = FindButtonByName(d, "CloseButton") ?? FindButtonByName(d, "CloseButton2");
            if (cb != null) SubmitButton(cb);
            else ScreenReader.Say(Loc.Get("Close button not found."));
        }

        // ---------- Full status (F5) ----------

        public void AnnounceFullStatus(SacrificeDialogController d)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Sacrifice dialog. "));

            // Selected blessing
            int selIdx = -1;
            int count = SafeEffectCount(d);
            for (int i = 0; i < count; i++)
            {
                if (d.effects[i].toggle != null && d.effects[i].toggle.isOn) { selIdx = i; break; }
            }
            if (selIdx >= 0)
                sb.Append(Loc.Get("Selected blessing: ")).Append(SafeLabel(d.effects[selIdx])).Append(". ");
            else
                sb.Append(Loc.Get("No blessing selected. "));

            // Sliders
            if (d.goodsSlider != null)
                sb.Append(Loc.Get("Goods: ")).Append(d.goodsSlider.intValue)
                  .Append(Loc.Get(" of ")).Append((int)d.goodsSlider.maxValue).Append(". ");
            if (d.herdsSlider != null)
                sb.Append(Loc.Get("Herds: ")).Append(d.herdsSlider.intValue)
                  .Append(Loc.Get(" of ")).Append((int)d.herdsSlider.maxValue).Append(". ");

            sb.Append(IsActionEnabled(d)
                ? Loc.Get("Press Enter to sacrifice. Escape to leave.")
                : Loc.Get("Sacrifice disabled: ") + DescribeDisabledReason(d) + ".");
            ScreenReader.Say(sb.ToString());
        }

        // ---------- Disabled-reason / applicability ----------

        /// <summary>
        /// Mirror SacrificeDialogController.ValidateSacrifice's disable conditions
        /// so the user hears WHY the Sacrifice button is disabled instead of a
        /// generic prompt. The game's logic gates on three things: a blessing must
        /// be selected, at least one offering slider must be > 0, and the selected
        /// blessing must be applicable to the current clan state (Healing needs
        /// wounded, Curing needs sick, when the Healer deity is targeted).
        /// </summary>
        private static string DescribeDisabledReason(SacrificeDialogController d)
        {
            int idx = SelectedIndex(d);
            bool hasOffering = (d.goodsSlider != null && d.goodsSlider.intValue > 0)
                            || (d.herdsSlider != null && d.herdsSlider.intValue > 0);

            if (idx < 0) return Loc.Get("no blessing selected");
            if (!hasOffering) return Loc.Get("no goods or herds pledged yet");

            // Blessing-specific applicability check (mirrors the game's logic).
            string applicabilityNote = ApplicabilityWarning(d, idx);
            if (!string.IsNullOrEmpty(applicabilityNote))
                return applicabilityNote;

            return Loc.Get("unknown reason");
        }

        /// <summary>
        /// Returns a short user-facing warning if the blessing at <paramref name="idx"/>
        /// would have no effect right now (Healer-only check). Empty string otherwise.
        /// Mirrors the conditional in SacrificeDialogController.ValidateSacrifice.
        /// </summary>
        private static string ApplicabilityWarning(SacrificeDialogController d, int idx)
        {
            try
            {
                if (d.selectedDeity != Deity.deity_Healer) return "";
                if (d.blessings == null || idx < 0 || idx >= d.blessings.count) return "";

                string key = d.blessings[idx].GetString("key");
                if (string.IsNullOrEmpty(key)) key = d.blessings[idx].GetString("name");

                if (key == "Healing" && PlayerClan.wounded == 0)
                    return Loc.Get("no one is wounded — this blessing only helps wounded clan members");
                if (key == "Curing" && PlayerClan.sick == 0)
                    return Loc.Get("no one is sick — this blessing only helps sick clan members");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("SacrificeNav.ApplicabilityWarning", ex);
            }
            return "";
        }

        private static int SelectedIndex(SacrificeDialogController d)
        {
            int count = SafeEffectCount(d);
            for (int i = 0; i < count; i++)
            {
                if (d.effects[i] != null && d.effects[i].toggle != null && d.effects[i].toggle.isOn)
                    return i;
            }
            return -1;
        }

        // ---------- Helpers ----------

        private static int SafeEffectCount(SacrificeDialogController d)
        {
            return d.effects != null ? d.effects.Count : 0;
        }

        private static UIDeityEffectItem SafeEffect(SacrificeDialogController d, int idx)
        {
            int n = SafeEffectCount(d);
            if (idx < 0 || idx >= n) return null;
            return d.effects[idx];
        }

        /// <summary>True if a deity-effect item is currently shown to sighted players.</summary>
        private static bool IsEffectVisible(UIDeityEffectItem item)
        {
            if (item == null) return false;
            if (item.toggle == null) return false;
            return item.toggle.gameObject.activeSelf;
        }

        /// <summary>
        /// Walk the effects list in <paramref name="direction"/> starting from
        /// <paramref name="from"/> until a visible item is found, wrapping around once.
        /// Hidden effects are pre-instantiated in the prefab with the placeholder
        /// label "Label" — sighted players only see the active ones, so the
        /// keyboard cycle must skip the rest.
        /// </summary>
        private static int NextVisibleEffectIndex(SacrificeDialogController d, int from, int direction)
        {
            int count = SafeEffectCount(d);
            if (count == 0) return 0;
            int start = (from < 0) ? (direction > 0 ? -1 : 0) : from;
            for (int step = 1; step <= count; step++)
            {
                int idx = ((start + direction * step) % count + count) % count;
                if (IsEffectVisible(d.effects[idx])) return idx;
            }
            return from < 0 ? 0 : from;
        }

        private static string SafeLabel(UIDeityEffectItem item)
        {
            if (item == null) return Loc.Get("Unknown");
            string s = item.label != null ? item.label.text : null;
            if (string.IsNullOrEmpty(s)) return Loc.Get("Blessing");
            return StringHelpers.StripTags(s);
        }

        private static UISlider SliderAt(SacrificeDialogController d, int idx)
        {
            switch ((SliderId)idx)
            {
                case SliderId.Goods: return d.goodsSlider;
                case SliderId.Herds: return d.herdsSlider;
                default: return null;
            }
        }

        private static string SliderLabel(int idx)
        {
            switch ((SliderId)idx)
            {
                case SliderId.Goods: return Loc.Get("Goods");
                case SliderId.Herds: return Loc.Get("Herds");
                default: return Loc.Get("Slider");
            }
        }

        private static bool IsActionEnabled(SacrificeDialogController d)
        {
            return d.actionButton != null && d.actionButton.interactable;
        }

        private static void SubmitButton(UIButton button)
        {
            if (button == null) return;
            ISubmitHandler h = button as ISubmitHandler;
            if (h == null) return;
            h.OnSubmit(new BaseEventData(EventSystem.current));
        }

        private static UIButton FindButtonByName(SacrificeDialogController d, string name)
        {
            var buttons = d.gameObject.GetComponentsInChildren<UIButton>(includeInactive: false);
            foreach (var b in buttons)
            {
                if (b == null) continue;
                if (b.gameObject.name == name) return b;
            }
            return null;
        }

        private static bool AnyModifier()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)
                || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
    }
}
