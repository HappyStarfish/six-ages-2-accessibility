using System;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Zone-based keyboard navigation for the Emissary dialog.
    ///
    /// Four functional regions: a clan list (recipient selection), five amount
    /// sliders (elite warriors, regular warriors, goods, herds, horses), the
    /// leader chooser, and the Send action. The flat Tab nav technically reaches
    /// the buttons and sliders but cannot model "navigate the clan list" (UIList
    /// isn't a Selectable) or the dependency that Send is gated on a clan being
    /// picked. This navigator wraps each zone with semantic announcements and
    /// exposes the clan list as Up/Down to browse plus Space to select.
    ///
    /// Keys follow the unified Model Y scheme: arrows browse and adjust sliders,
    /// Space selects the focused element (clan, or the leader chooser), D reads
    /// details, a blank Enter completes the screen (Send), and Escape leaves.
    /// </summary>
    public class EmissaryNavigator
    {
        private enum Zone { List, Sliders, Leader }
        private enum SliderId { Elite = 0, Regular = 1, Goods = 2, Herds = 3, Horses = 4 }
        private const int SliderCount = 5;

        // ManagementDialogController.leaderIndex is protected; reflection cache.
        private static FieldInfo _leaderIndexField;

        private Zone _zone = Zone.List;
        private int _listIndex = -1;
        private int _sliderIndex = -1;
        private readonly ConfirmGate _confirmGate = new ConfirmGate();

        public void ResetForNewScreen()
        {
            _zone = Zone.List;
            _listIndex = -1;
            _sliderIndex = -1;
            _confirmGate.Reset();
        }

        public void HandleInput(EmissaryDialogController d)
        {
            if (d == null) return;

            // Enter — primary action (Send), the one key that completes the screen.
            // Model Y: a blank Enter is the universal screen-completion key, handled
            // globally so it works from every zone. No modifier is required; a held
            // Ctrl is neither needed nor blocked.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TrySend(d);
                return;
            }

            // Escape — leave without sending.
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

            if (Input.GetKeyDown(KeyCode.L) && !AnyModifier())
            {
                _confirmGate.Reset();
                _zone = Zone.List;
                AnnounceCurrentClan(d);
                return;
            }

            switch (_zone)
            {
                case Zone.List:    HandleListInput(d);    break;
                case Zone.Sliders: HandleSlidersInput(d); break;
                case Zone.Leader:  HandleLeaderInput(d);  break;
            }
        }

        private void CycleZone(int dir)
        {
            int z = (int)_zone + dir;
            if (z < 0) z = 2;
            if (z > 2) z = 0;
            _zone = (Zone)z;
        }

        private void AnnounceZone(EmissaryDialogController d)
        {
            switch (_zone)
            {
                case Zone.List:    ScreenReader.Say(Loc.Get("Clan list.")); AnnounceCurrentClan(d); break;
                case Zone.Sliders: ScreenReader.Say(Loc.Get("Gifts and escort.")); AnnounceCurrentSlider(d); break;
                case Zone.Leader:  ScreenReader.Say(Loc.Get("Leader.")); AnnounceLeader(d); break;
            }
        }

        // ---------- List zone ----------

        private void HandleListInput(EmissaryDialogController d)
        {
            if (d.clanList == null || d.clanList.count == 0) return;

            // First arrow press after the dialog opened: land the read cursor on
            // the clan already set as the recipient (carried over from the
            // Relations screen) instead of jumping past it to index 0 / the end.
            if (_listIndex < 0
                && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)))
            {
                _confirmGate.Reset();
                _listIndex = SelectedClanIndex(d);
                if (_listIndex < 0) _listIndex = 0;
                AnnounceCurrentClan(d);
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _confirmGate.Reset();
                _listIndex--;
                if (_listIndex < 0) _listIndex = d.clanList.count - 1;
                AnnounceCurrentClan(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _confirmGate.Reset();
                _listIndex++;
                if (_listIndex >= d.clanList.count) _listIndex = 0;
                AnnounceCurrentClan(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _confirmGate.Reset();
                SelectCurrentClan(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceClanSynopsis(d);
                return;
            }
        }

        private void AnnounceCurrentClan(EmissaryDialogController d)
        {
            if (d.clanList == null || d.clanList.count == 0)
            {
                ScreenReader.Say(Loc.Get("No clans available."));
                return;
            }
            if (_listIndex < 0)
            {
                _listIndex = SelectedClanIndex(d);
                if (_listIndex < 0) _listIndex = 0;
            }
            if (_listIndex >= d.clanList.count) _listIndex = d.clanList.count - 1;

            var item = d.clanList[_listIndex];
            bool isSelected = item != null && item.key == Game.ClanVariable("otherClan");
            ScreenReader.Say(ClanItemSummary(item, isSelected));
        }

        private void SelectCurrentClan(EmissaryDialogController d)
        {
            if (d.clanList == null || _listIndex < 0 || _listIndex >= d.clanList.count) return;
            var item = d.clanList[_listIndex];
            if (item == null) return;
            // Mirror a click — fires onItemClicked which the dialog has wired to
            // OnClanSelected → SelectedClan → UpdateForSelectedClan + Send validation.
            d.clanList.OnItemClicked(item);
            string name = Clan.ClanWithIndex(item.key).name;
            ScreenReader.Say(name + Loc.Get(" selected as recipient."));
        }

        private void AnnounceClanSynopsis(EmissaryDialogController d)
        {
            if (d.clanList == null || _listIndex < 0 || _listIndex >= d.clanList.count) return;
            var item = d.clanList[_listIndex];
            if (item == null) return;
            try
            {
                Clan clan = Clan.ClanWithIndex(item.key);
                string text = clan.ExplanationWithDetail(2);
                if (string.IsNullOrEmpty(text)) text = clan.name;
                ScreenReader.Say(StringHelpers.StripTags(text).Replace("\n\n", ". ").Replace("\n", " "));
            }
            catch (Exception ex)
            {
                DebugLogger.Error("EmissaryNav.AnnounceClanSynopsis", ex);
            }
        }

        private static string ClanItemSummary(UIListItem item, bool isSelected)
        {
            if (item == null) return Loc.Get("Unknown clan");
            try
            {
                Clan clan = Clan.ClanWithIndex(item.key);
                var sb = new StringBuilder();
                sb.Append(clan.name);
                // Selected marker right after the name — heard even when fast arrow
                // browsing interrupts the announcement.
                if (isSelected) sb.Append(Loc.Get(", selected"));
                // ClanCell shows culture + near + tribe + feud icons; mirror those briefly.
                // Properties may not all exist on every Clan instance — guard with try/catch
                // at the outer call site.
                if (clan.haveFeud) sb.Append(Loc.Get(", feud"));
                if (clan.haveTrade) sb.Append(Loc.Get(", trade partner"));
                return sb.ToString();
            }
            catch
            {
                return item.text ?? Loc.Get("Clan");
            }
        }

        // ---------- Sliders zone ----------

        private void HandleSlidersInput(EmissaryDialogController d)
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
            if (Input.GetKeyDown(KeyCode.LeftArrow))  { _confirmGate.Reset(); AdjustCurrentSlider(d, -1); return; }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { _confirmGate.Reset(); AdjustCurrentSlider(d, 1);  return; }
            // D — universal detail key: re-read the focused slider.
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceCurrentSlider(d);
                return;
            }
        }

        private void AnnounceCurrentSlider(EmissaryDialogController d)
        {
            if (_sliderIndex < 0) _sliderIndex = 0;
            UISlider s = SliderAt(d, _sliderIndex);
            string label = SliderLabel(_sliderIndex);
            if (s == null || !s.gameObject.activeSelf)
            {
                ScreenReader.Say(label + Loc.Get(" slider not available."));
                return;
            }
            string status = s.IsInteractable() ? "" : Loc.Get(" (locked)");
            ScreenReader.Say(label + ": " + s.intValue + Loc.Get(" of ") + (int)s.maxValue + status
                + Loc.Get(". Left and Right to adjust, Shift for larger steps."));
        }

        private void AdjustCurrentSlider(EmissaryDialogController d, int dir)
        {
            UISlider s = SliderAt(d, _sliderIndex);
            if (s == null || !s.IsInteractable()) return;
            float step = s.wholeNumbers ? 1f : (s.maxValue - s.minValue) * 0.1f;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) step *= 5f;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                s.value = dir > 0 ? s.maxValue : s.minValue;
            else
                s.value = Mathf.Clamp(s.value + dir * step, s.minValue, s.maxValue);
            ScreenReader.Say(SliderLabel(_sliderIndex) + " " + s.intValue + Loc.Get(" of ") + (int)s.maxValue);
        }

        // ---------- Leader zone ----------

        private void HandleLeaderInput(EmissaryDialogController d)
        {
            // Space opens the leader chooser (the focused element). Enter is
            // reserved globally for Send — see HandleInput.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _confirmGate.Reset();
                if (d.chooseLeaderButton != null && d.chooseLeaderButton.interactable)
                {
                    SubmitButton(d.chooseLeaderButton);
                }
                else
                {
                    string trainerReason = TrainerInfo.LockReasonForButton("ChooseLeader");
                    ScreenReader.Say(!string.IsNullOrEmpty(trainerReason)
                        ? trainerReason
                        : Loc.Get("Choose Leader is not available."));
                }
                return;
            }
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceLeader(d);
                return;
            }
        }

        private void AnnounceLeader(EmissaryDialogController d)
        {
            int leaderIdx = GetLeaderIndex(d);
            if (leaderIdx <= 0)
            {
                ScreenReader.Say(Loc.Get("No leader chosen yet. Press Space to pick one."));
                return;
            }
            string name = PluginImport.PC_PersonName(leaderIdx);
            ScreenReader.Say(Loc.Get("Leader: ") + name + Loc.Get(". Press Space to change."));
        }

        // ---------- Primary action (Enter) and Close (Esc) ----------

        private void TrySend(EmissaryDialogController d)
        {
            if (d.actionButton == null || !d.actionButton.interactable)
            {
                _confirmGate.Reset();
                ScreenReader.Say(WhySendDisabled(d));
                return;
            }
            if (_confirmGate.RequestOrConfirm(BuildEmissarySummary(d)))
                SubmitButton(d.actionButton);
        }

        /// <summary>
        /// Build the spoken summary for the two-step Enter confirmation. Names
        /// the recipient clan, the leader, and lists non-zero gifts/escort. The
        /// game requires at least two warriors so they always appear; goods,
        /// herds, horses only when the player set them above zero.
        /// </summary>
        private static string BuildEmissarySummary(EmissaryDialogController d)
        {
            string clan;
            int otherClan = Game.ClanVariable("otherClan");
            if (otherClan > 0)
            {
                try { clan = Clan.ClanWithIndex(otherClan).name; }
                catch { clan = Loc.Get("no clan"); }
            }
            else clan = Loc.Get("no clan");

            int leaderIdx = GetLeaderIndex(d);
            string leader = leaderIdx > 0 ? PluginImport.PC_PersonName(leaderIdx) : Loc.Get("no leader");

            int elite   = d.eliteSlider   != null ? d.eliteSlider.intValue   : 0;
            int regular = d.regularSlider != null ? d.regularSlider.intValue : 0;
            int goods   = d.goodsSlider   != null ? d.goodsSlider.intValue   : 0;
            int herds   = d.herdsSlider   != null ? d.herdsSlider.intValue   : 0;
            int horses  = d.horsesSlider  != null ? d.horsesSlider.intValue  : 0;
            int totalWarriors = elite + regular;

            // Optional gift list — "X goods, Y herds, Z horses" with the and-joiner.
            var gifts = new System.Collections.Generic.List<string>();
            if (goods  > 0) gifts.Add(string.Format(Loc.Get("{0} goods"),  goods));
            if (herds  > 0) gifts.Add(string.Format(Loc.Get("{0} herds"),  herds));
            if (horses > 0) gifts.Add(string.Format(Loc.Get("{0} horses"), horses));

            var sb = new StringBuilder();
            sb.Append(string.Format(Loc.Get("You send an emissary to {0}"), clan));
            sb.Append(string.Format(Loc.Get(", led by {0}"), leader));
            sb.Append(string.Format(
                totalWarriors == 1 ? Loc.Get(", escorted by {0} warrior") : Loc.Get(", escorted by {0} warriors"),
                totalWarriors));
            if (gifts.Count > 0)
                sb.Append(string.Format(Loc.Get(", with gifts of {0}"),
                    StringHelpers.JoinList(gifts, Loc.Get("and"))));
            sb.Append('.');
            return sb.ToString();
        }

        /// <summary>
        /// Build a context-appropriate reason for a disabled Send button.
        /// Trainer locks take priority — when the Trainer is gating the
        /// dialog to a different element, the game-mechanic message would be
        /// misleading (the user has met the mechanic conditions but Send
        /// stays grey because the Trainer hasn't advanced yet). Only fall
        /// back to "pick a clan, leader, warriors" when no Trainer lock is
        /// active. The mechanic check mirrors
        /// <see cref="EmissaryDialogController.ValidateSendButton"/>:
        /// leader present, at least two total warriors, and a clan picked.
        /// </summary>
        private static string WhySendDisabled(EmissaryDialogController d)
        {
            string trainerReason = TrainerInfo.LockReasonForButton("Send");
            if (!string.IsNullOrEmpty(trainerReason)) return trainerReason;

            var missing = new System.Collections.Generic.List<string>();
            int otherClan = -1;
            try { otherClan = Game.ClanVariable("otherClan"); }
            catch (Exception ex) { DebugLogger.Error("EmissaryNav.WhySendDisabled.otherClan", ex); }
            if (otherClan <= 0) missing.Add(Loc.Get("pick a clan"));

            int leaderIdx = GetLeaderIndex(d);
            if (leaderIdx <= 0) missing.Add(Loc.Get("pick a leader"));

            int warriors = 0;
            try { warriors = (d.eliteSlider != null ? d.eliteSlider.intValue : 0)
                           + (d.regularSlider != null ? d.regularSlider.intValue : 0); }
            catch (Exception ex) { DebugLogger.Error("EmissaryNav.WhySendDisabled.warriors", ex); }
            if (warriors < 2) missing.Add(Loc.Get("send at least two warriors"));

            if (missing.Count == 0)
                return Loc.Get("Send is disabled, but the game-mechanic conditions appear to be met. The game may be waiting for an internal state update.");
            return Loc.Get("Send needs: ") + string.Join(", ", missing.ToArray()) + ".";
        }

        private static void TryClose(EmissaryDialogController d)
        {
            if (d.closeButton != null) SubmitButton(d.closeButton);
            else ScreenReader.Say(Loc.Get("Close button not found."));
        }

        // ---------- F5 status ----------

        public void AnnounceFullStatus(EmissaryDialogController d)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Emissary dialog. "));

            int otherClan = Game.ClanVariable("otherClan");
            if (otherClan > 0)
            {
                Clan clan = Clan.ClanWithIndex(otherClan);
                sb.Append(Loc.Get("Recipient: ")).Append(clan.name).Append(". ");
            }
            else sb.Append(Loc.Get("No recipient chosen. "));

            int leaderIdx = GetLeaderIndex(d);
            if (leaderIdx > 0)
                sb.Append(Loc.Get("Leader: ")).Append(PluginImport.PC_PersonName(leaderIdx)).Append(". ");
            else sb.Append(Loc.Get("No leader chosen. "));

            for (int i = 0; i < SliderCount; i++)
            {
                UISlider s = SliderAt(d, i);
                if (s == null || !s.gameObject.activeSelf) continue;
                sb.Append(SliderLabel(i)).Append(": ").Append(s.intValue).Append(". ");
            }

            if (IsActionEnabled(d))
                sb.Append(Loc.Get("Press Enter to send. Escape to leave."));
            else
                sb.Append(WhySendDisabled(d));
            ScreenReader.Say(sb.ToString());
        }

        // ---------- Helpers ----------

        private static UISlider SliderAt(EmissaryDialogController d, int idx)
        {
            switch ((SliderId)idx)
            {
                case SliderId.Elite:   return d.eliteSlider;
                case SliderId.Regular: return d.regularSlider;
                case SliderId.Goods:   return d.goodsSlider;
                case SliderId.Herds:   return d.herdsSlider;
                case SliderId.Horses:  return d.horsesSlider;
                default: return null;
            }
        }

        private static string SliderLabel(int idx)
        {
            switch ((SliderId)idx)
            {
                case SliderId.Elite:   return Loc.Get("Elite warriors");
                case SliderId.Regular: return Loc.Get("Regular warriors");
                case SliderId.Goods:   return Loc.Get("Goods gift");
                case SliderId.Herds:   return Loc.Get("Herds gift");
                case SliderId.Horses:  return Loc.Get("Horses gift");
                default: return Loc.Get("Slider");
            }
        }

        /// <summary>
        /// Index of the clan currently set as the emissary recipient
        /// (<c>Game.ClanVariable("otherClan")</c>), or -1 if none is set or it is
        /// not in the list. Lets the read cursor open on the pre-chosen clan.
        /// </summary>
        private static int SelectedClanIndex(EmissaryDialogController d)
        {
            if (d.clanList == null) return -1;
            int otherClan;
            try { otherClan = Game.ClanVariable("otherClan"); }
            catch (Exception ex)
            {
                DebugLogger.Error("EmissaryNav.SelectedClanIndex", ex);
                return -1;
            }
            if (otherClan <= 0) return -1;
            for (int i = 0; i < d.clanList.count; i++)
            {
                UIListItem it = d.clanList[i];
                if (it != null && it.key == otherClan) return i;
            }
            return -1;
        }

        private static int GetLeaderIndex(EmissaryDialogController d)
        {
            try
            {
                if ((object)_leaderIndexField == null)
                {
                    _leaderIndexField = typeof(ManagementDialogController).GetField(
                        "leaderIndex", BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if ((object)_leaderIndexField == null) return 0;
                object v = _leaderIndexField.GetValue(d);
                return v is int i ? i : 0;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("EmissaryNav.GetLeaderIndex", ex);
                return 0;
            }
        }

        private static bool IsActionEnabled(EmissaryDialogController d)
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

        private static bool AnyModifier()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)
                || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
    }
}
