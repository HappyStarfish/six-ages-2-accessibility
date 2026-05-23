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
    /// Zone-based keyboard navigation for the Spirit Bargain dialog.
    ///
    /// SpiritDialogController has three regions: a list of blessing radio buttons
    /// (the inherited <c>effects</c> from EffectsDialogController), four approach
    /// radio buttons (Persuade / Offer Magic / Release for Larger Effect /
    /// Release for Longer Effect, instantiated into <c>bargainToggleGroup</c>) and
    /// the Bargain action button. The flat Tab nav only sees actionButton + Close,
    /// so blind users would silently auto-bargain with the default Persuade
    /// approach on the first blessing without knowing the choice existed.
    ///
    /// We mirror SacrificeNavigator's two-zone pattern: Effects (blessings) and
    /// Approaches. Approaches are gated case by case in the game:
    /// <list type="bullet">
    ///   <item>Offer Magic — only interactable when <c>PlayerClan.magic &gt; 0</c></item>
    ///   <item>Release for Larger Effect — disabled when the focused blessing is
    ///     already a stored permanent blessing (its "blessing" plugin string is
    ///     non-empty); also hidden for Raven (and reused for Healing's UI shape)</item>
    ///   <item>Release for Longer Effect — disabled when the focused blessing's
    ///     <c>instant</c> proto flag is set; hidden for Raven and Healing</item>
    /// </list>
    /// The radios react to blessing changes (<see cref="EffectsDialogController.RadioButtonTapped"/>
    /// is overridden in SpiritDialogController), so we re-read availability after
    /// every blessing switch.
    ///
    /// Keys follow the unified Model Y scheme: arrows browse, Space selects the
    /// focused approach or blessing, D reads its description, a blank Enter
    /// completes the screen (Bargain), and Escape leaves.
    /// </summary>
    public class SpiritNavigator
    {
        private enum Zone { Approaches, Blessings }

        private Zone _zone = Zone.Approaches;
        private int _approachIndex = -1;
        private int _blessingIndex = -1;
        private readonly ConfirmGate _confirmGate = new ConfirmGate();

        // Cached reflection handles for the four approach checkboxes. They are
        // private fields on SpiritDialogController, so we reach them once per run
        // and reuse the FieldInfo for every input tick. Resolved lazily in
        // EnsureApproachesResolved so a missing/renamed field doesn't crash the
        // ctor — we just lose that one approach.
        private static FieldInfo _persuadeField;
        private static FieldInfo _magicField;
        private static FieldInfo _doubleField;
        private static FieldInfo _lengthenField;
        private static bool _approachFieldsResolved;

        public void ResetForNewScreen()
        {
            _zone = Zone.Approaches;
            _approachIndex = -1;
            _blessingIndex = -1;
            _confirmGate.Reset();
        }

        public void HandleInput(SpiritDialogController d)
        {
            if (d == null) return;

            // Enter — primary action (Bargain), the one key that completes the
            // screen. Model Y: a blank Enter is the universal screen-completion key,
            // handled globally so it works from every zone. No modifier is required.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TryBargain(d);
                return;
            }

            // Escape — leave without bargaining.
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
                case Zone.Approaches: HandleApproachesInput(d); break;
                case Zone.Blessings:  HandleBlessingsInput(d);  break;
            }
        }

        private void CycleZone(int dir)
        {
            int z = (int)_zone + dir;
            if (z < 0) z = 1;
            if (z > 1) z = 0;
            _zone = (Zone)z;
        }

        private void AnnounceZone(SpiritDialogController d)
        {
            switch (_zone)
            {
                case Zone.Approaches: ScreenReader.Say(Loc.Get("Bargain approach.")); AnnounceCurrentApproach(d); break;
                case Zone.Blessings:  ScreenReader.Say(Loc.Get("Blessings."));         AnnounceCurrentBlessing(d); break;
            }
        }

        // ============================================================
        // Approaches zone (4 radios on bargainToggleGroup)
        // ============================================================

        private void HandleApproachesInput(SpiritDialogController d)
        {
            List<UIDeityEffectItem> approaches = CollectVisibleApproaches(d);
            if (approaches.Count == 0)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                    ScreenReader.Say(Loc.Get("No bargain approaches available."));
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _confirmGate.Reset();
                _approachIndex = StepIndex(_approachIndex, -1, approaches.Count);
                AnnounceCurrentApproach(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _confirmGate.Reset();
                _approachIndex = StepIndex(_approachIndex, +1, approaches.Count);
                AnnounceCurrentApproach(d);
                return;
            }
            // Space — select the focused approach. Enter is reserved globally for
            // Bargain — see HandleInput.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _confirmGate.Reset();
                SelectCurrentApproach(d, approaches);
                return;
            }
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceCurrentApproachDescription(d);
                return;
            }
        }

        private void AnnounceCurrentApproach(SpiritDialogController d)
        {
            List<UIDeityEffectItem> approaches = CollectVisibleApproaches(d);
            if (approaches.Count == 0) { ScreenReader.Say(Loc.Get("No bargain approaches available.")); return; }
            if (_approachIndex < 0) _approachIndex = 0;
            if (_approachIndex >= approaches.Count) _approachIndex = approaches.Count - 1;

            UIDeityEffectItem item = approaches[_approachIndex];
            // State right after the name — heard even when fast arrow browsing
            // interrupts the announcement.
            ScreenReader.Say(SafeLabel(item) + ", " + Loc.Get(DescribeApproachState(item)));
        }

        private void AnnounceCurrentApproachDescription(SpiritDialogController d)
        {
            List<UIDeityEffectItem> approaches = CollectVisibleApproaches(d);
            if (_approachIndex < 0 || _approachIndex >= approaches.Count) return;
            UIDeityEffectItem item = approaches[_approachIndex];
            string desc = item.description != null ? StringHelpers.StripTags(item.description.text) : null;
            if (string.IsNullOrEmpty(desc))
            {
                ScreenReader.Say(SafeLabel(item) + Loc.Get(", no description."));
                return;
            }
            ScreenReader.Say(desc);
        }

        private void SelectCurrentApproach(SpiritDialogController d, List<UIDeityEffectItem> approaches)
        {
            if (_approachIndex < 0 || _approachIndex >= approaches.Count) return;
            UIDeityEffectItem item = approaches[_approachIndex];
            if (item == null || item.toggle == null) return;
            if (!item.toggle.gameObject.activeSelf) { ScreenReader.Say(Loc.Get("Approach not available.")); return; }
            if (!item.toggle.interactable)         { ScreenReader.Say(SafeLabel(item) + Loc.Get(" is not available right now.")); return; }
            if (item.toggle.isOn) { ScreenReader.Say(SafeLabel(item) + Loc.Get(" already selected.")); return; }

            // Toggle.isOn assignment fires onValueChanged; the radio group on
            // bargainToggleGroup turns the others off automatically. Mirrors
            // SacrificeNavigator.SelectCurrentEffect.
            item.toggle.isOn = true;
            ScreenReader.Say(SafeLabel(item) + Loc.Get(" selected."));
        }

        // ============================================================
        // Blessings zone (the inherited effects list)
        // ============================================================

        private void HandleBlessingsInput(SpiritDialogController d)
        {
            int count = SafeEffectCount(d);
            if (count == 0)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                    ScreenReader.Say(Loc.Get("No blessings available."));
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _confirmGate.Reset();
                _blessingIndex = NextVisibleBlessingIndex(d, _blessingIndex, -1);
                AnnounceCurrentBlessing(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _confirmGate.Reset();
                _blessingIndex = NextVisibleBlessingIndex(d, _blessingIndex, +1);
                AnnounceCurrentBlessing(d);
                return;
            }
            // Space — select the focused blessing. Enter is reserved globally for
            // Bargain — see HandleInput.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _confirmGate.Reset();
                SelectCurrentBlessing(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceCurrentBlessingDescription(d);
                return;
            }
        }

        private void AnnounceCurrentBlessing(SpiritDialogController d)
        {
            int count = SafeEffectCount(d);
            if (count == 0) { ScreenReader.Say(Loc.Get("No blessings available.")); return; }
            if (_blessingIndex < 0) _blessingIndex = NextVisibleBlessingIndex(d, -1, +1);
            if (_blessingIndex < 0) { ScreenReader.Say(Loc.Get("No blessings available.")); return; }

            UIDeityEffectItem item = d.effects[_blessingIndex];
            string state;
            if (item.toggle == null)                          state = "unavailable";
            else if (!item.toggle.gameObject.activeSelf)      state = "hidden";
            else if (!item.toggle.interactable)               state = "not yet learned";
            else if (item.toggle.isOn)                        state = "selected";
            else                                              state = "available";
            ScreenReader.Say(SafeLabel(item) + ", " + Loc.Get(state));
        }

        private void AnnounceCurrentBlessingDescription(SpiritDialogController d)
        {
            UIDeityEffectItem item = SafeEffect(d, _blessingIndex);
            if (item == null) return;
            string desc = item.description != null ? StringHelpers.StripTags(item.description.text) : null;
            if (string.IsNullOrEmpty(desc)) { ScreenReader.Say(SafeLabel(item) + Loc.Get(", no description.")); return; }
            ScreenReader.Say(desc);
        }

        private void SelectCurrentBlessing(SpiritDialogController d)
        {
            UIDeityEffectItem item = SafeEffect(d, _blessingIndex);
            if (item == null || item.toggle == null) return;
            if (!item.toggle.gameObject.activeSelf) { ScreenReader.Say(Loc.Get("Blessing not available.")); return; }
            if (!item.toggle.interactable)          { ScreenReader.Say(Loc.Get("Blessing not yet learned.")); return; }
            if (item.toggle.isOn) { ScreenReader.Say(SafeLabel(item) + Loc.Get(" already selected.")); return; }

            item.toggle.isOn = true;

            // SpiritDialogController.RadioButtonTapped re-evaluates which release
            // checkboxes are interactable for the newly selected blessing. Read
            // the current approach again so the user hears the updated state
            // (e.g. "Release for Longer Effect, not available right now" if the
            // new blessing has the instant flag).
            string label = SafeLabel(item);
            ScreenReader.Say(label + Loc.Get(" selected."));
        }

        // ============================================================
        // Primary action (Enter) and Close (Esc)
        // ============================================================

        private void TryBargain(SpiritDialogController d)
        {
            if (d.actionButton == null || !d.actionButton.interactable)
            {
                _confirmGate.Reset();
                ScreenReader.Say(Loc.Get("Cannot bargain: ") + DescribeDisabledReason(d) + ".");
                return;
            }
            if (_confirmGate.RequestOrConfirm(BuildBargainSummary(d)))
                SubmitButton(d.actionButton);
        }

        /// <summary>
        /// Build the spoken summary for the two-step Enter confirmation: which
        /// blessing the player wants and which approach they intend to use.
        /// Both fields are radio-selected (game enforces exactly one each), so
        /// the format is fixed: blessing first (the goal), then approach.
        /// </summary>
        private static string BuildBargainSummary(SpiritDialogController d)
        {
            string blessing = Loc.Get("no blessing");
            int count = SafeEffectCount(d);
            for (int i = 0; i < count; i++)
            {
                UIDeityEffectItem b = d.effects[i];
                if (b != null && b.toggle != null && b.toggle.isOn && IsEffectVisible(b))
                { blessing = SafeLabel(b); break; }
            }

            EnsureApproachesResolved();
            string approach = Loc.Get("no approach");
            UIDeityEffectItem[] all = new[]
            {
                ReadApproach(d, _persuadeField), ReadApproach(d, _magicField),
                ReadApproach(d, _doubleField),   ReadApproach(d, _lengthenField),
            };
            for (int i = 0; i < all.Length; i++)
            {
                UIDeityEffectItem a = all[i];
                if (a != null && a.toggle != null && a.toggle.isOn) { approach = SafeLabel(a); break; }
            }

            return string.Format(Loc.Get("You bargain for {0} via {1}."), blessing, approach);
        }

        private static void TryClose(SpiritDialogController d)
        {
            UIButton cb = FindButtonByName(d, "CloseButton") ?? FindButtonByName(d, "CloseButton2");
            if (cb != null) SubmitButton(cb);
            else ScreenReader.Say(Loc.Get("Close button not found."));
        }

        // ============================================================
        // Full status (F5)
        // ============================================================

        public void AnnounceFullStatus(SpiritDialogController d)
        {
            var sb = new StringBuilder();
            string caption = d.dialogCaption != null ? StringHelpers.StripTags(d.dialogCaption.text) : null;
            sb.Append(string.IsNullOrEmpty(caption) ? Loc.Get("Spirit Bargain. ") : caption + ". ");

            // Approaches
            List<UIDeityEffectItem> approaches = CollectVisibleApproaches(d);
            if (approaches.Count > 0)
            {
                sb.Append(Loc.Get("Approaches: "));
                for (int i = 0; i < approaches.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    UIDeityEffectItem ai = approaches[i];
                    sb.Append(SafeLabel(ai));
                    string st = DescribeApproachState(ai);
                    if (st != "available") sb.Append(" (").Append(Loc.Get(st)).Append(")");
                }
                sb.Append(". ");
            }

            // Blessings
            int count = SafeEffectCount(d);
            int visible = 0;
            int selBlessing = -1;
            for (int i = 0; i < count; i++)
            {
                UIDeityEffectItem b = d.effects[i];
                if (!IsEffectVisible(b)) continue;
                visible++;
                if (b.toggle != null && b.toggle.isOn) selBlessing = i;
            }
            sb.Append(Loc.Get("Blessings: ")).Append(visible).Append(". ");
            if (selBlessing >= 0)
                sb.Append(Loc.Get("Selected: ")).Append(SafeLabel(d.effects[selBlessing])).Append(". ");

            // ExtraInfo (NeedleGrass pasture damage / Healing wounded+sick text)
            string extra = d.extraInfo != null ? StringHelpers.StripTags(d.extraInfo.text) : null;
            if (!string.IsNullOrEmpty(extra)) sb.Append(extra).Append(". ");

            sb.Append(d.actionButton != null && d.actionButton.interactable
                ? Loc.Get("Press Enter to bargain. Escape to leave.")
                : Loc.Get("Bargain disabled: ") + DescribeDisabledReason(d) + ".");
            ScreenReader.Say(sb.ToString());
        }

        // ============================================================
        // Approach collection / state
        // ============================================================

        /// <summary>
        /// Build the visible-approach list in declaration order
        /// (Persuade, Offer Magic, Release Larger, Release Longer). Hidden
        /// checkboxes (Raven hides both Releases; Healing hides Release Longer)
        /// are skipped so Up/Down arrow keys cycle only what the player can
        /// actually pick.
        /// </summary>
        private static List<UIDeityEffectItem> CollectVisibleApproaches(SpiritDialogController d)
        {
            var list = new List<UIDeityEffectItem>(4);
            EnsureApproachesResolved();
            AddIfVisible(list, ReadApproach(d, _persuadeField));
            AddIfVisible(list, ReadApproach(d, _magicField));
            AddIfVisible(list, ReadApproach(d, _doubleField));
            AddIfVisible(list, ReadApproach(d, _lengthenField));
            return list;
        }

        private static void AddIfVisible(List<UIDeityEffectItem> list, UIDeityEffectItem item)
        {
            if (item == null) return;
            // UIDeityEffectItem owns a "hidden" flag but the prefab also toggles
            // GameObject.activeSelf for non-Raven dialogs; check both.
            if (item.toggle != null && !item.toggle.gameObject.activeSelf) return;
            list.Add(item);
        }

        private static UIDeityEffectItem ReadApproach(SpiritDialogController d, FieldInfo field)
        {
            // Mono 2.0 has no FieldInfo.op_Equality — use (object) cast for the
            // null check, otherwise every input tick throws MissingMethodException
            // and the dialog becomes uncontrollable. Same dance for d, which is
            // a UnityEngine.Object subclass (op_Equality exists there but cast
            // is harmless and makes intent uniform with the FieldInfo branch).
            if ((object)field == null || (object)d == null) return null;
            try { return field.GetValue(d) as UIDeityEffectItem; }
            catch (Exception ex) { DebugLogger.Error("SpiritNav.ReadApproach", ex); return null; }
        }

        private static void EnsureApproachesResolved()
        {
            if (_approachFieldsResolved) return;
            _approachFieldsResolved = true;
            try
            {
                Type t = typeof(SpiritDialogController);
                BindingFlags bf = BindingFlags.Instance | BindingFlags.NonPublic;
                _persuadeField = t.GetField("persuadeCheckbox", bf);
                _magicField    = t.GetField("magicCheckbox", bf);
                _doubleField   = t.GetField("doubleCheckbox", bf);
                _lengthenField = t.GetField("lengthenCheckbox", bf);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("SpiritNav.ResolveApproaches", ex);
            }
        }

        /// <summary>English state token; also the <see cref="Loc"/> key for the German value.</summary>
        private static string DescribeApproachState(UIDeityEffectItem item)
        {
            if (item == null || item.toggle == null) return "unavailable";
            if (!item.toggle.gameObject.activeSelf) return "hidden";
            if (!item.toggle.interactable) return "not available";
            if (item.toggle.isOn) return "selected";
            return "available";
        }

        // ============================================================
        // Disabled-reason
        // ============================================================

        /// <summary>
        /// SpiritDialogController doesn't expose a single boolean ValidateBargain
        /// like Sacrifice does — actionButton is set interactable=true in
        /// SetupUI and only toggled off for the NeedleGrass-no-grazing-stress
        /// edge case. So the only common disable reason we surface is "no
        /// approach selected", plus an extra-info echo when the game has set it.
        /// </summary>
        private static string DescribeDisabledReason(SpiritDialogController d)
        {
            // Approach must be selected (game defaults Persuade to true on Show,
            // but a user could have toggled it off without picking another).
            EnsureApproachesResolved();
            UIDeityEffectItem persuade = ReadApproach(d, _persuadeField);
            UIDeityEffectItem magic    = ReadApproach(d, _magicField);
            UIDeityEffectItem dbl      = ReadApproach(d, _doubleField);
            UIDeityEffectItem lng      = ReadApproach(d, _lengthenField);
            bool any = (persuade != null && persuade.toggle != null && persuade.toggle.isOn)
                    || (magic    != null && magic.toggle    != null && magic.toggle.isOn)
                    || (dbl      != null && dbl.toggle      != null && dbl.toggle.isOn)
                    || (lng      != null && lng.toggle      != null && lng.toggle.isOn);
            if (!any) return Loc.Get("no bargain approach selected");

            // Echo the dialog's own extra-info if the game disabled the button
            // for a per-spirit reason ("Pastures show some damage." / "There is
            // minimal pasture damage." for Needle Grass).
            string extra = d.extraInfo != null ? StringHelpers.StripTags(d.extraInfo.text) : null;
            if (!string.IsNullOrEmpty(extra)) return extra;

            return Loc.Get("the bargain is not available right now");
        }

        // ============================================================
        // Helpers
        // ============================================================

        private static int SafeEffectCount(SpiritDialogController d)
        {
            return d.effects != null ? d.effects.Count : 0;
        }

        private static UIDeityEffectItem SafeEffect(SpiritDialogController d, int idx)
        {
            int n = SafeEffectCount(d);
            if (idx < 0 || idx >= n) return null;
            return d.effects[idx];
        }

        private static bool IsEffectVisible(UIDeityEffectItem item)
        {
            if (item == null) return false;
            if (item.toggle == null) return false;
            return item.toggle.gameObject.activeSelf;
        }

        /// <summary>
        /// Walk the effects list in <paramref name="direction"/> from
        /// <paramref name="from"/> until a visible item is found, wrapping once.
        /// EffectsDialogController pre-instantiates spare slots that stay hidden
        /// via UpdateRadioButton, so the keyboard cycle has to skip them.
        /// </summary>
        private static int NextVisibleBlessingIndex(SpiritDialogController d, int from, int direction)
        {
            int count = SafeEffectCount(d);
            if (count == 0) return -1;
            int start = (from < 0) ? (direction > 0 ? -1 : 0) : from;
            for (int step = 1; step <= count; step++)
            {
                int idx = ((start + direction * step) % count + count) % count;
                if (IsEffectVisible(d.effects[idx])) return idx;
            }
            return from < 0 ? -1 : from;
        }

        private static int StepIndex(int from, int direction, int count)
        {
            if (count <= 0) return -1;
            if (from < 0) return direction > 0 ? 0 : count - 1;
            int idx = (from + direction) % count;
            if (idx < 0) idx += count;
            return idx;
        }

        private static string SafeLabel(UIDeityEffectItem item)
        {
            if (item == null) return Loc.Get("Unknown");
            string s = item.label != null ? item.label.text : null;
            if (string.IsNullOrEmpty(s)) return Loc.Get("Option");
            return StringHelpers.StripTags(s);
        }

        private static void SubmitButton(UIButton button)
        {
            if (button == null) return;
            ISubmitHandler h = button as ISubmitHandler;
            if (h == null) return;
            h.OnSubmit(new BaseEventData(EventSystem.current));
        }

        private static UIButton FindButtonByName(SpiritDialogController d, string name)
        {
            UIButton[] buttons = d.gameObject.GetComponentsInChildren<UIButton>(false);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].gameObject.name == name) return buttons[i];
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
