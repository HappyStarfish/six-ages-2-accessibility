using System;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Zone navigation for RaidDialog. The same controller backs both general Raid
    /// (screen_Raid) and Cattle Raid (screen_CattleRaid), distinguished by
    /// <c>screenIndex</c>. Three structural reasons we need a navigator rather than
    /// the flat Tab nav:
    ///
    /// <list type="number">
    /// <item><description>Two UILists (raidableClans + helperClans) — UIList rows
    /// are not Selectables, so flat Tab can't reach them.</description></item>
    /// <item><description>Helper zone is conditional: <c>helperGroup.gameObject.SetActive</c>
    /// is true only on general Raid. CattleRaid has no helpers, no filter dropdown
    /// — the navigator skips that zone entirely on cattle raids.</description></item>
    /// <item><description>Filter dropdown (Alliances / Favors Due) cycles
    /// helperClans and isn't trivially Tab-discoverable inside the helperGroup
    /// container.</description></item>
    /// </list>
    ///
    /// <para>Zones: <see cref="Zone.Raidable"/> (auto-active on open),
    /// <see cref="Zone.Sliders"/> (elite + regular), <see cref="Zone.Helpers"/>
    /// (skipped on CattleRaid), <see cref="Zone.Leader"/>.</para>
    ///
    /// <para>Keys follow the unified Model Y scheme: arrows browse and adjust
    /// sliders, Space acts on the focused element (select a target, toggle a
    /// helper, open the leader chooser), D reads details, a blank Enter completes
    /// the screen (Raid), and Escape leaves. Tab cycles zones; F cycles the helper
    /// filter inside the Helpers zone; L jumps back to the raidable list.</para>
    /// </summary>
    public class RaidNavigator
    {
        private enum Zone { Raidable, Sliders, Helpers, Leader }
        private enum SliderId { Elite = 0, Regular = 1 }
        private const int SliderCount = 2;

        // ManagementDialogController.leaderIndex is protected; cached reflection.
        private static FieldInfo _leaderIndexField;

        private Zone _zone = Zone.Raidable;
        private int _raidableIndex = -1;
        private int _helperIndex = -1;
        private int _sliderIndex = -1;
        private readonly ConfirmGate _confirmGate = new ConfirmGate();

        public void ResetForNewScreen()
        {
            _zone = Zone.Raidable;
            _raidableIndex = -1;
            _helperIndex = -1;
            _sliderIndex = -1;
            _confirmGate.Reset();
        }

        public void HandleInput(RaidDialogController d)
        {
            if (d == null) return;

            // Enter — primary action (Raid), the one key that completes the screen.
            // Model Y: a blank Enter is the universal screen-completion key, handled
            // globally so it works from every zone. No modifier is required.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TryRaid(d);
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
                CycleZone(dir, d);
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
                _zone = Zone.Raidable;
                AnnounceCurrentRaidable(d);
                return;
            }

            switch (_zone)
            {
                case Zone.Raidable: HandleRaidableInput(d); break;
                case Zone.Sliders:  HandleSlidersInput(d);  break;
                case Zone.Helpers:  HandleHelpersInput(d);  break;
                case Zone.Leader:   HandleLeaderInput(d);   break;
            }
        }

        // ---------- Raidable list zone ----------

        private void HandleRaidableInput(RaidDialogController d)
        {
            if (d.raidableClans == null || d.raidableClans.count == 0) return;

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _confirmGate.Reset();
                _raidableIndex = WrapDecrement(_raidableIndex, d.raidableClans.count);
                AnnounceCurrentRaidable(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _confirmGate.Reset();
                _raidableIndex = WrapIncrement(_raidableIndex, d.raidableClans.count);
                AnnounceCurrentRaidable(d);
                return;
            }
            // Space — select the focused clan as the raid target.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _confirmGate.Reset();
                SelectCurrentRaidable(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceClanSynopsis(d, d.raidableClans, _raidableIndex);
                return;
            }
        }

        private void AnnounceCurrentRaidable(RaidDialogController d)
        {
            if (d.raidableClans == null || d.raidableClans.count == 0)
            {
                ScreenReader.Say(Loc.Get("No raidable clans available."));
                return;
            }
            if (_raidableIndex < 0)
                _raidableIndex = d.raidableClans.selectedIndex >= 0 ? d.raidableClans.selectedIndex : 0;
            if (_raidableIndex >= d.raidableClans.count) _raidableIndex = d.raidableClans.count - 1;

            var item = d.raidableClans[_raidableIndex];
            string marker = (item != null && item.key == Game.ClanVariable("otherClan"))
                ? Loc.Get(", selected") : "";
            ScreenReader.Say(ClanItemSummary(item, marker));
        }

        private void SelectCurrentRaidable(RaidDialogController d)
        {
            if (d.raidableClans == null || _raidableIndex < 0 || _raidableIndex >= d.raidableClans.count) return;
            var item = d.raidableClans[_raidableIndex];
            if (item == null) return;
            // Mirror a click — fires OnClanSelected which sets otherClan game var,
            // updates mapAnnotations, and runs ValidateRaidButton so actionButton
            // becomes interactable when the conditions are met.
            d.raidableClans.OnItemClicked(item);
            try
            {
                Clan clan = Clan.ClanWithIndex(item.key);
                ScreenReader.Say(clan.name + Loc.Get(" selected as target. Press Enter to raid."));
            }
            catch
            {
                ScreenReader.Say(Loc.Get("Target selected."));
            }
        }

        // ---------- Helpers list zone (general Raid only) ----------

        private void HandleHelpersInput(RaidDialogController d)
        {
            // F (or Shift+F) cycles the filter dropdown. The dropdown's onValueChanged
            // refills helperClans, so the user hears the new filter name and helper
            // count after each press.
            if (Input.GetKeyDown(KeyCode.F))
            {
                _confirmGate.Reset();
                CycleHelperFilter(d);
                return;
            }

            if (d.helperClans == null || d.helperClans.count == 0) return;

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _confirmGate.Reset();
                _helperIndex = WrapDecrement(_helperIndex, d.helperClans.count);
                AnnounceCurrentHelper(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _confirmGate.Reset();
                _helperIndex = WrapIncrement(_helperIndex, d.helperClans.count);
                AnnounceCurrentHelper(d);
                return;
            }
            // Space — toggle the focused clan as the raid helper.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _confirmGate.Reset();
                SelectCurrentHelper(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceClanSynopsis(d, d.helperClans, _helperIndex);
                return;
            }
        }

        private void CycleHelperFilter(RaidDialogController d)
        {
            if (d.filter == null || d.filter.options == null || d.filter.options.Count <= 1)
            {
                ScreenReader.Say(Loc.Get("No filter available."));
                return;
            }
            int dir = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? -1 : 1;
            int count = d.filter.options.Count;
            int newVal = d.filter.value + dir;
            if (newVal < 0) newVal = count - 1;
            if (newVal >= count) newVal = 0;
            d.filter.value = newVal; // triggers OnFilterChanged → helperClans.Fill
            string filterName = d.filter.options[newVal].text;
            int hc = d.helperClans != null ? d.helperClans.count : 0;
            ScreenReader.Say(Loc.Get("Filter: ") + filterName + ". "
                + hc + (hc == 1 ? Loc.Get(" helper.") : Loc.Get(" helpers.")));
            _helperIndex = -1; // reset so first arrow lands on item 0
        }

        private void AnnounceCurrentHelper(RaidDialogController d)
        {
            if (d.helperClans == null || d.helperClans.count == 0)
            {
                ScreenReader.Say(Loc.Get("No helper clans available."));
                return;
            }
            if (_helperIndex < 0) _helperIndex = 0;
            if (_helperIndex >= d.helperClans.count) _helperIndex = d.helperClans.count - 1;

            var item = d.helperClans[_helperIndex];
            int currentHelper = Game.ClanVariable("helperClan");
            string marker = (item != null && item.key == currentHelper && currentHelper > 0)
                ? Loc.Get(", selected as helper") : "";
            ScreenReader.Say(ClanItemSummary(item, marker));
        }

        private void SelectCurrentHelper(RaidDialogController d)
        {
            if (d.helperClans == null || _helperIndex < 0 || _helperIndex >= d.helperClans.count) return;
            var item = d.helperClans[_helperIndex];
            if (item == null) return;
            // OnHelperClanSelected toggles helperClan: tapping the already-selected
            // helper deselects it. Same behaviour for keyboard users.
            d.helperClans.OnItemClicked(item);
            try
            {
                int currentHelper = Game.ClanVariable("helperClan");
                if (currentHelper == item.key)
                {
                    Clan clan = Clan.ClanWithIndex(item.key);
                    ScreenReader.Say(clan.name + Loc.Get(" will help. Press Space again to remove."));
                }
                else
                {
                    ScreenReader.Say(Loc.Get("Helper removed."));
                }
            }
            catch
            {
                ScreenReader.Say(Loc.Get("Helper toggled."));
            }
        }

        // ---------- Slider zone ----------

        private void HandleSlidersInput(RaidDialogController d)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _confirmGate.Reset();
                _sliderIndex = WrapDecrement(_sliderIndex, SliderCount);
                AnnounceCurrentSlider(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _confirmGate.Reset();
                _sliderIndex = WrapIncrement(_sliderIndex, SliderCount);
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

        private void AnnounceCurrentSlider(RaidDialogController d)
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

        private void AdjustCurrentSlider(RaidDialogController d, int dir)
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

        private static UISlider SliderAt(RaidDialogController d, int index)
        {
            switch ((SliderId)index)
            {
                case SliderId.Elite:   return d.eliteSlider;
                case SliderId.Regular: return d.regularSlider;
                default: return null;
            }
        }

        private static string SliderLabel(int index)
        {
            switch ((SliderId)index)
            {
                case SliderId.Elite:   return Loc.Get("Swords");
                case SliderId.Regular: return Loc.Get("Bows");
                default: return Loc.Get("Slider");
            }
        }

        // ---------- Leader zone ----------

        private void HandleLeaderInput(RaidDialogController d)
        {
            // Space opens the leader chooser (the focused element). Enter is
            // reserved globally for Raid — see HandleInput.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _confirmGate.Reset();
                if (d.chooseLeaderButton != null && d.chooseLeaderButton.interactable)
                    SubmitButton(d.chooseLeaderButton);
                else
                    ScreenReader.Say(Loc.Get("Choose Leader is not available."));
                return;
            }
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceLeader(d);
                return;
            }
        }

        private void AnnounceLeader(RaidDialogController d)
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

        // ---------- Zone management ----------

        private void CycleZone(int dir, RaidDialogController d)
        {
            // Helpers zone is only visible on general Raid (helperGroup.SetActive on
            // OnShow against screenIndex == screen_Raid). CattleRaid skips it.
            bool helpersAvailable = HelpersZoneVisible(d);

            int z = (int)_zone;
            for (int safety = 0; safety < 10; safety++)
            {
                z += dir;
                if (z < 0) z = 3;
                if (z > 3) z = 0;
                if ((Zone)z == Zone.Helpers && !helpersAvailable) continue;
                break;
            }
            _zone = (Zone)z;
        }

        private static bool HelpersZoneVisible(RaidDialogController d)
        {
            return d.helperGroup != null && d.helperGroup.gameObject.activeSelf;
        }

        private void AnnounceZone(RaidDialogController d)
        {
            switch (_zone)
            {
                case Zone.Raidable:
                    int rc = d.raidableClans != null ? d.raidableClans.count : 0;
                    ScreenReader.Say(Loc.Get("Raidable clans, ") + rc
                        + (rc == 1 ? Loc.Get(" entry.") : Loc.Get(" entries.")));
                    AnnounceCurrentRaidable(d);
                    return;
                case Zone.Sliders:
                    ScreenReader.Say(Loc.Get("Warriors."));
                    AnnounceCurrentSlider(d);
                    return;
                case Zone.Helpers:
                    int hc = d.helperClans != null ? d.helperClans.count : 0;
                    string filterName = (d.filter != null && d.filter.options != null
                        && d.filter.value >= 0 && d.filter.value < d.filter.options.Count)
                        ? d.filter.options[d.filter.value].text : "";
                    ScreenReader.Say(Loc.Get("Helpers")
                        + (string.IsNullOrEmpty(filterName) ? "" : " (" + filterName + ")")
                        + ", " + hc + (hc == 1
                            ? Loc.Get(" entry. F cycles filter.")
                            : Loc.Get(" entries. F cycles filter.")));
                    AnnounceCurrentHelper(d);
                    return;
                case Zone.Leader:
                    ScreenReader.Say(Loc.Get("Leader."));
                    AnnounceLeader(d);
                    return;
            }
        }

        // ---------- Primary action / close ----------

        private void TryRaid(RaidDialogController d)
        {
            UIButton ab = d.actionButton;
            if (ab == null || !ab.gameObject.activeSelf || !ab.IsInteractable())
            {
                _confirmGate.Reset();
                ScreenReader.Say(WhyRaidDisabled(d));
                return;
            }
            if (_confirmGate.RequestOrConfirm(BuildRaidSummary(d)))
                SubmitButton(ab);
        }

        /// <summary>
        /// Build the spoken summary for the two-step Enter confirmation. Names
        /// the target clan, total warriors committed (swords + bows), the helper
        /// clan if any, and the chosen leader. Sliders are summed rather than
        /// listed separately — sighted players see two bars, but the meaningful
        /// commitment is the total raiding force.
        /// </summary>
        private static string BuildRaidSummary(RaidDialogController d)
        {
            string target = ClanName(Game.ClanVariable("otherClan"), Loc.Get("no target"));
            int swords = d.eliteSlider != null ? d.eliteSlider.intValue : 0;
            int bows = d.regularSlider != null ? d.regularSlider.intValue : 0;
            int totalWarriors = swords + bows;
            int helperKey = HelpersZoneVisible(d) ? Game.ClanVariable("helperClan") : 0;
            int leaderIdx = GetLeaderIndex(d);

            var sb = new StringBuilder();
            sb.Append(string.Format(Loc.Get("You raid {0}"), target));
            sb.Append(string.Format(
                totalWarriors == 1
                    ? Loc.Get(" with {0} warrior")
                    : Loc.Get(" with {0} warriors"),
                totalWarriors));
            if (helperKey > 0)
                sb.Append(string.Format(Loc.Get(", helped by {0}"), ClanName(helperKey, "")));
            if (leaderIdx > 0)
                sb.Append(string.Format(Loc.Get(", led by {0}"), PluginImport.PC_PersonName(leaderIdx)));
            sb.Append('.');
            return sb.ToString();
        }

        private static string ClanName(int idx, string fallback)
        {
            if (idx <= 0) return fallback;
            try { return Clan.ClanWithIndex(idx).name; }
            catch { return fallback; }
        }

        private static string WhyRaidDisabled(RaidDialogController d)
        {
            // Mirror ValidateRaidButton's checks so the user knows what's missing
            // instead of a flat "not available right now".
            int totalWarriors = (d.eliteSlider != null ? d.eliteSlider.intValue : 0)
                              + (d.regularSlider != null ? d.regularSlider.intValue : 0);
            if (totalWarriors == 0)
                return Loc.Get("Raid is disabled. Move the Swords or Bows slider above zero.");
            int targetIdx = d.raidableClans != null ? d.raidableClans.selectedIndex : -1;
            if (targetIdx < 0)
                return Loc.Get("Raid is disabled. Pick a target from the raidable list first.");
            int helperIdx = d.helperClans != null ? d.helperClans.selectedIndex : -1;
            if (helperIdx >= 0)
            {
                var t = d.raidableClans[targetIdx];
                var h = d.helperClans[helperIdx];
                if (t != null && h != null && t.key == h.key)
                    return Loc.Get("Raid is disabled. The helper and the target are the same clan — pick another helper or remove it.");
            }
            return Loc.Get("Raid is not available right now.");
        }

        private void TryClose(RaidDialogController d)
        {
            // RaidDialogController has no closeButton field — X-icon wired in the
            // prefab. UIRoleResolver finds it via onClick method-name heuristic.
            UIButton closeBtn = UIRoleResolver.FindCloseButton(d);
            if (closeBtn != null && closeBtn.gameObject.activeSelf && closeBtn.IsInteractable())
                SubmitButton(closeBtn);
        }

        // ---------- F5 full status ----------

        public void AnnounceFullStatus(RaidDialogController d)
        {
            if (d == null) return;
            var sb = new StringBuilder();
            string captionText = (d.caption != null && !string.IsNullOrEmpty(d.caption.text))
                ? StringHelpers.StripTags(d.caption.text) : Loc.Get("Raid");
            sb.Append(captionText).Append(Loc.Get(" status. "));

            int otherClan = Game.ClanVariable("otherClan");
            if (otherClan > 0)
            {
                try { sb.Append(Loc.Get("Target: ")).Append(Clan.ClanWithIndex(otherClan).name).Append(". "); }
                catch { sb.Append(Loc.Get("Target chosen. ")); }
            }
            else sb.Append(Loc.Get("No target chosen. "));

            int helperClan = Game.ClanVariable("helperClan");
            if (HelpersZoneVisible(d))
            {
                if (helperClan > 0)
                {
                    try { sb.Append(Loc.Get("Helper: ")).Append(Clan.ClanWithIndex(helperClan).name).Append(". "); }
                    catch { sb.Append(Loc.Get("Helper chosen. ")); }
                }
                else sb.Append(Loc.Get("No helper chosen. "));
            }

            for (int i = 0; i < SliderCount; i++)
            {
                UISlider s = SliderAt(d, i);
                if (s == null || !s.gameObject.activeSelf) continue;
                sb.Append(SliderLabel(i)).Append(": ").Append(s.intValue)
                  .Append(Loc.Get(" of ")).Append((int)s.maxValue).Append(". ");
            }

            int leaderIdx = GetLeaderIndex(d);
            if (leaderIdx > 0)
                sb.Append(Loc.Get("Leader: ")).Append(PluginImport.PC_PersonName(leaderIdx)).Append(". ");
            else sb.Append(Loc.Get("No leader chosen. "));

            UIButton ab = d.actionButton;
            if (ab != null)
            {
                string verb = (ab.label != null && !string.IsNullOrEmpty(ab.label.text))
                    ? StringHelpers.StripTags(ab.label.text) : Loc.Get("Raid");
                sb.Append(verb).Append(ab.IsInteractable() ? Loc.Get(" available. ") : Loc.Get(" disabled. "));
            }

            sb.Append(Loc.Get("Tab cycles zones. "));
            if (HelpersZoneVisible(d)) sb.Append(Loc.Get("F cycles the helper filter. "));
            sb.Append(Loc.Get("L returns to the raidable list. Up and Down navigate, Left and Right adjust sliders, D describes the focused clan. Enter raids, Escape closes."));

            ScreenReader.Say(sb.ToString());
        }

        // ---------- Helpers ----------

        private static int GetLeaderIndex(RaidDialogController d)
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
                DebugLogger.Error("RaidNav.GetLeaderIndex", ex);
                return 0;
            }
        }

        private static string ClanItemSummary(UIListItem item, string marker)
        {
            if (item == null) return Loc.Get("Unknown clan");
            try
            {
                Clan clan = Clan.ClanWithIndex(item.key);
                var sb = new StringBuilder();
                sb.Append(clan.name);
                // Selected marker right after the name — heard even when fast arrow
                // browsing interrupts the announcement.
                if (!string.IsNullOrEmpty(marker)) sb.Append(marker);
                if (clan.haveFeud)  sb.Append(Loc.Get(", feud"));
                if (clan.haveTrade) sb.Append(Loc.Get(", trade partner"));
                return sb.ToString();
            }
            catch
            {
                return item.text ?? Loc.Get("Clan");
            }
        }

        private void AnnounceClanSynopsis(RaidDialogController d, UIList list, int idx)
        {
            if (list == null || idx < 0 || idx >= list.count) return;
            var item = list[idx];
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
                DebugLogger.Error("RaidNav.AnnounceClanSynopsis", ex);
            }
        }

        private static void SubmitButton(UIButton button)
        {
            if (button == null) return;
            ISubmitHandler h = button as ISubmitHandler;
            if (h == null) return;
            try { h.OnSubmit(new BaseEventData(EventSystem.current)); }
            catch (Exception ex) { DebugLogger.Error("RaidNav.SubmitButton", ex); }
        }

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

        private static bool AnyModifier()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)
                || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
    }
}
