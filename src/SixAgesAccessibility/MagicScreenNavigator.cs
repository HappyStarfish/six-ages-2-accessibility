using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Zone-based keyboard navigation for the Magic management screen.
    ///
    /// The Magic screen has four functional regions: filter dropdown, deity/spirit list,
    /// blessing toggles, and action buttons (Sacrifice / Build / Bargain / Ritual). Tab
    /// cycles between the available zones and arrow keys navigate within the active zone,
    /// so a screen-reader user can reach every interactive element without depending on
    /// the visual layout the standard nav cannot detect.
    ///
    /// Owned by <see cref="KeyboardNavigationHandler"/> and dispatched from its Update tick
    /// whenever the active screen is a <see cref="MagicScreenController"/>.
    /// </summary>
    public class MagicScreenNavigator
    {
        private enum MagicZone { Filter, List, Blessings, Buttons }

        private MagicZone _zone = MagicZone.List;
        private int _listIndex;
        private int _blessingIndex;
        private int _buttonIndex;

        // Used to call back into the host for shared actions (button activation, tutorial
        // hint toggle). The host's other state (focus index, response buttons, etc.) is
        // never touched from here — Magic-screen navigation is fully self-contained.
        private readonly KeyboardNavigationHandler _host;

        public MagicScreenNavigator(KeyboardNavigationHandler host) { _host = host; }

        /// <summary>Reset zone + indices when entering a Magic screen for the first time.</summary>
        public void ResetForNewScreen()
        {
            _zone = MagicZone.List;
            _listIndex = 0;
            _blessingIndex = 0;
            _buttonIndex = 0;
        }

        /// <summary>Top-level dispatch — called every Update while the Magic screen is active.</summary>
        public void HandleInput(MagicScreenController mc)
        {
            // Tab / Shift+Tab — switch zones. Ctrl+Tab is handled globally by
            // KeyboardNavigationHandler.TryCycleManagementScreen and never
            // reaches this dispatch.
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                int dir = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? -1 : 1;
                CycleZone(mc, dir);
                return;
            }

            // L — jump to list zone
            if (Input.GetKeyDown(KeyCode.L))
            {
                _zone = MagicZone.List;
                AnnounceZone(mc);
                return;
            }

            // H — read current tutorial hint page; subsequent presses cycle pages.
            if (Input.GetKeyDown(KeyCode.H))
            {
                TutorialHintHandler.Instance.HandleHKey();
                return;
            }

            // Ctrl+R — read full details
            if (Input.GetKeyDown(KeyCode.R) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                AnnounceSelectedDetails(mc);
                return;
            }

            // D — read description/tooltip for current focus
            if (Input.GetKeyDown(KeyCode.D))
            {
                AnnounceDescription(mc);
                return;
            }

            // Escape — management screens have no back, hint at screen switching
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ScreenReader.Say(Loc.Get("Use Ctrl+1 through 9 to switch screens, or Shift+F1 for shortcuts."));
                return;
            }

            switch (_zone)
            {
                case MagicZone.Filter:    HandleFilterInput(mc); break;
                case MagicZone.List:      HandleListInput(mc); break;
                case MagicZone.Blessings: HandleBlessingInput(mc); break;
                case MagicZone.Buttons:   HandleButtonInput(mc); break;
            }
        }

        // ---------- Zone selection ----------

        private void CycleZone(MagicScreenController mc, int direction)
        {
            int zoneCount = 4;
            int current = (int)_zone;

            for (int i = 0; i < zoneCount; i++)
            {
                current += direction;
                if (current < 0) current = zoneCount - 1;
                if (current >= zoneCount) current = 0;

                MagicZone candidate = (MagicZone)current;
                if (IsZoneAvailable(mc, candidate))
                {
                    _zone = candidate;
                    AnnounceZone(mc);
                    return;
                }
            }

            AnnounceZone(mc);
        }

        private bool IsZoneAvailable(MagicScreenController mc, MagicZone zone)
        {
            switch (zone)
            {
                case MagicZone.Filter:
                    return mc.filter != null && mc.filter.options.Count > 1;
                case MagicZone.List:
                    UIList list = GetActiveList(mc);
                    return list != null && list.gameObject.activeSelf && list.count > 0;
                case MagicZone.Blessings:
                    return GetVisibleBlessingIndices(mc).Count > 0;
                case MagicZone.Buttons:
                    return GetVisibleButtons(mc).Count > 0;
                default:
                    return false;
            }
        }

        private UIList GetActiveList(MagicScreenController mc)
        {
            if (mc.currentMagicFilter == FilterOtherworldBy.filter_Other)
                return mc.otherList;
            return mc.list;
        }

        private List<int> GetVisibleBlessingIndices(MagicScreenController mc)
        {
            var visible = new List<int>();
            if (mc.effects == null) return visible;
            for (int i = 0; i < mc.effects.Count; i++)
            {
                UIDeityEffectItem effect = mc.effects[i];
                if (effect != null && effect.description != null && effect.description.gameObject.activeSelf)
                    visible.Add(i);
            }
            return visible;
        }

        private List<UIButton> GetVisibleButtons(MagicScreenController mc)
        {
            var buttons = new List<UIButton>();
            if (mc.sacrificeButton != null && mc.sacrificeButton.gameObject.activeSelf)
                buttons.Add(mc.sacrificeButton);
            if (mc.buildButton != null && mc.buildButton.gameObject.activeSelf)
                buttons.Add(mc.buildButton);
            if (mc.bargainButton != null && mc.bargainButton.gameObject.activeSelf)
                buttons.Add(mc.bargainButton);
            if (mc.ritualButton != null && mc.ritualButton.gameObject.activeSelf)
                buttons.Add(mc.ritualButton);
            return buttons;
        }

        // ---------- Per-zone input ----------

        private void HandleFilterInput(MagicScreenController mc)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow)
                || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                bool forward = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow);
                CycleFilter(mc, forward ? 1 : -1);
            }
        }

        /// <summary>
        /// Step the filter dropdown by <paramref name="dir"/> and re-seed the
        /// per-zone indexes so the new filter starts from the top of its
        /// list / blessings / buttons. Used by the Filter-zone Left/Right
        /// handler so the announcement and state reset stay in lock-step.
        /// </summary>
        private void CycleFilter(MagicScreenController mc, int dir)
        {
            if (mc == null || mc.filter == null) return;
            int count = mc.filter.options.Count;
            if (count <= 1) return;
            int newVal = mc.filter.value + dir;
            if (newVal < 0) newVal = count - 1;
            if (newVal >= count) newVal = 0;
            mc.filter.value = newVal;
            _listIndex = 0;
            _blessingIndex = 0;
            _buttonIndex = 0;
            AnnounceFilter(mc);
        }

        private void HandleListInput(MagicScreenController mc)
        {
            UIList activeList = GetActiveList(mc);
            if (activeList == null || activeList.count == 0)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                    ScreenReader.Say(Loc.Get("Empty list."));
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                int dir = Input.GetKeyDown(KeyCode.DownArrow) ? 1 : -1;
                if (_listIndex < 0) _listIndex = 0;
                _listIndex += dir;
                if (_listIndex < 0) _listIndex = activeList.count - 1;
                if (_listIndex >= activeList.count) _listIndex = 0;

                activeList.selectedIndex = _listIndex;
                UIListItem item = activeList[_listIndex];
                if (item != null && mc.currentMagicFilter != FilterOtherworldBy.filter_Other)
                    activeList.OnItemClicked(item);

                AnnounceListItem(mc, _listIndex);
                return;
            }

            // Model Y: the list browses on arrows (selection follows focus);
            // details are read with D, full details with Ctrl+R. Enter has no
            // separate role here.
        }

        private void HandleBlessingInput(MagicScreenController mc)
        {
            var visible = GetVisibleBlessingIndices(mc);
            if (visible.Count == 0) return;

            if (_blessingIndex < 0 || _blessingIndex >= visible.Count)
                _blessingIndex = 0;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                int dir = Input.GetKeyDown(KeyCode.DownArrow) ? 1 : -1;
                _blessingIndex += dir;
                if (_blessingIndex < 0) _blessingIndex = visible.Count - 1;
                if (_blessingIndex >= visible.Count) _blessingIndex = 0;

                AnnounceBlessing(mc, visible[_blessingIndex]);
                return;
            }

            // Model Y: Space acts on the focused element — here, flip the
            // blessing toggle. Enter is no longer a synonym.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                int effectIndex = visible[_blessingIndex];
                UIDeityEffectItem effect = mc.effects[effectIndex];

                if (effect.toggle != null && effect.toggle.gameObject.activeSelf)
                {
                    string name = effect.label != null && effect.label.gameObject.activeSelf
                        ? effect.label.text : Loc.Get("Blessing");

                    if (!effect.toggle.interactable)
                    {
                        ScreenReader.Say(name + Loc.Get(", cannot be changed."));
                        return;
                    }

                    if (effect.toggle.isOn)
                    {
                        effect.toggle.isOn = false;
                        ScreenReader.Say(name + Loc.Get(" — no longer in effect"));
                    }
                    else
                    {
                        // Spirits use Bargain action, not direct toggle — short-circuit so the
                        // user gets a useful prompt instead of silently bouncing off a refusal.
                        if (mc.currentMagicFilter == FilterOtherworldBy.filter_Spirits)
                        {
                            ScreenReader.Say(name + Loc.Get(", cannot toggle. Use the Bargain action instead."));
                            return;
                        }

                        // Pre-check: count permanent blessings vs temple capacity. The game's
                        // own toggle handler refuses silently when capacity is exceeded; we
                        // surface a precise reason instead.
                        int permanent = 0;
                        for (int i = 0; i < mc.effects.Count; i++)
                        {
                            if (mc.effects[i] != null && mc.effects[i].toggle != null
                                && mc.effects[i].toggle.gameObject.activeSelf && mc.effects[i].toggle.isOn)
                                permanent++;
                        }
                        int templeCapacity = PluginImport.PC_TempleSize(Game.currentDeity);
                        if (permanent >= templeCapacity)
                        {
                            if (templeCapacity == 0)
                                ScreenReader.Say(Loc.Get("No temple built. Build a shrine first to activate blessings."));
                            else
                                ScreenReader.Say(string.Format(
                                    Loc.Get("All {0} blessing slots are in use. Build a larger temple for more."),
                                    templeCapacity));
                            return;
                        }

                        effect.toggle.isOn = true;
                        ScreenReader.Say(name + Loc.Get(" — now in effect permanently"));
                    }
                }
                else
                {
                    ScreenReader.Say(Loc.Get("This is a description, not a toggle."));
                }
            }
        }

        private void HandleButtonInput(MagicScreenController mc)
        {
            var buttons = GetVisibleButtons(mc);
            if (buttons.Count == 0) return;

            if (_buttonIndex < 0 || _buttonIndex >= buttons.Count)
                _buttonIndex = 0;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                int dir = Input.GetKeyDown(KeyCode.DownArrow) ? 1 : -1;
                _buttonIndex += dir;
                if (_buttonIndex < 0) _buttonIndex = buttons.Count - 1;
                if (_buttonIndex >= buttons.Count) _buttonIndex = 0;

                AnnounceButton(buttons[_buttonIndex], _buttonIndex, buttons.Count);
                return;
            }

            // Model Y: Space acts on the focused element — here, fire the
            // focused action button (opens Sacrifice / Build / Bargain /
            // Ritual). Enter stays as a synonym: firing a focused button is
            // the closest thing this hub screen has to a completing action.
            if (Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                UIButton btn = buttons[_buttonIndex];
                if (btn.IsInteractable())
                {
                    _host.ActivateButtonExternal(btn);
                }
                else
                {
                    string label = btn.label != null ? btn.label.text : btn.gameObject.name;
                    ScreenReader.Say(label + Loc.Get(" is not available."));
                }
            }
        }

        // ---------- Announcements ----------

        private void AnnounceZone(MagicScreenController mc)
        {
            switch (_zone)
            {
                case MagicZone.Filter:
                    AnnounceFilter(mc);
                    break;
                case MagicZone.List:
                {
                    UIList activeList = GetActiveList(mc);
                    if (activeList != null && activeList.count > 0)
                    {
                        int idx = activeList.selectedIndex;
                        if (idx < 0 || idx >= activeList.count) idx = 0;
                        _listIndex = idx;
                        string text = StringHelpers.StripTags(activeList[idx] != null ? activeList[idx].text : "");
                        ScreenReader.Say(Loc.Get("List, ") + activeList.count + Loc.Get(" items. ") + text);
                    }
                    else
                    {
                        ScreenReader.Say(Loc.Get("List is empty."));
                    }
                    break;
                }
                case MagicZone.Blessings:
                {
                    var visible = GetVisibleBlessingIndices(mc);
                    if (visible.Count > 0)
                    {
                        if (_blessingIndex >= visible.Count) _blessingIndex = 0;
                        AnnounceBlessing(mc, visible[_blessingIndex], BlessingsZonePrefix(mc, visible.Count));
                    }
                    break;
                }
                case MagicZone.Buttons:
                {
                    var buttons = GetVisibleButtons(mc);
                    if (buttons.Count > 0)
                    {
                        if (_buttonIndex >= buttons.Count) _buttonIndex = 0;
                        AnnounceButton(buttons[_buttonIndex], _buttonIndex, buttons.Count, Loc.Get("Actions. "));
                    }
                    break;
                }
            }
        }

        private void AnnounceFilter(MagicScreenController mc)
        {
            string filterText = mc.filter.captionText != null ? mc.filter.captionText.text : "";
            ScreenReader.Say(Loc.Get("Filter: ") + Loc.Get(filterText) + ".");
        }

        /// <summary>
        /// Append "X of Y" for a Sacred Time row. The row's UIListItem.key
        /// holds the ClanMagic enum value the game stored when populating the
        /// list (see <c>MagicScreenController.FilterChanged</c>'s SacredTime
        /// case). PlayerClan.SacredTimeMagic returns the current allocation
        /// for that magic; Game.MaxMagic returns the cap. The visual
        /// SacretTimeListItem renders this as filled/empty checkboxes that
        /// flat keyboard nav can't read.
        /// </summary>
        private static void AppendSacredTimeMagicValue(StringBuilder sb, UIListItem item)
        {
            if (item == null) return;
            try
            {
                ClanMagic magic = (ClanMagic)item.key;
                int current = PlayerClan.SacredTimeMagic(magic);
                int max = Game.MaxMagic(magic);
                sb.Append(", ").Append(current).Append(Loc.Get(" of ")).Append(max);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MagicNav.AppendSacredTimeMagicValue", ex);
            }
        }

        private void AnnounceListItem(MagicScreenController mc, int index)
        {
            UIList activeList = GetActiveList(mc);
            if (activeList == null || index < 0 || index >= activeList.count) return;

            UIListItem item = activeList[index];
            if (item == null) return;

            string rawText = item.text ?? "";
            string text = StringHelpers.StripTags(rawText);

            var sb = new StringBuilder();
            sb.Append(text);

            // Trainer-required marker — when this deity/spirit is the one the
            // Trainer is waiting for (Tutorial.topicRequired matches the name),
            // append a clear cue so cycling through the list lands on the
            // expected item with audible feedback rather than silent equality.
            sb.Append(TrainerInfo.MarkIfRequired(text));

            // Dead-deity marker — MagicScreenController.FilterChanged wraps the
            // name in <s>...</s> when PlayerClan.IsDeityDead(deity) (line 243
            // in decompiled). StripTags would erase the visual cue, so we
            // detect the raw tag before stripping.
            if (rawText.IndexOf("<s>", StringComparison.OrdinalIgnoreCase) >= 0)
                sb.Append(Loc.Get(" (deity dead)"));

            // Recent-action and temple-damage markers come from the secondary
            // and tertiary icon sprites the same FilterChanged path sets:
            //   tertiary = Rune_Sacrifice_  → recent sacrifice (Gods view)
            //   tertiary = Rune_Bargained   → recent bargain   (Spirits view)
            //   secondary alpha 0.5         → temple damaged
            //   secondary alpha 0.25        → temple seriously damaged
            AppendListItemStateMarkers(sb, item as UIListItemWithIcons);

            // Add right-panel details for Gods/Spirits (updated by OnItemClicked)
            if (mc.currentMagicFilter == FilterOtherworldBy.filter_Gods
                || mc.currentMagicFilter == FilterOtherworldBy.filter_Spirits)
            {
                string temple = mc.templeSizeInfo != null ? mc.templeSizeInfo.text : "";
                if (!string.IsNullOrEmpty(temple))
                    sb.Append(". ").Append(temple);
            }
            else if (mc.currentMagicFilter == FilterOtherworldBy.filter_SacredTime)
            {
                // Sacred Time: each row maps to a ClanMagic enum value stored
                // in UIListItem.key (FilterChanged adds the row with
                // (int)PluginImport.Game_AllSacredTimeMagic_Magic(k)). Speak
                // the current allocation vs cap so the user hears "Fields, 3
                // of 5" — the visual checkboxes carry that info but are
                // invisible to flat nav.
                AppendSacredTimeMagicValue(sb, item);

                // effects[0] shows the explanation for the selected row
                // (UpdateForSelectedRow → Game_AllSacredTimeMagic_Explanation).
                if (mc.effects != null && mc.effects.Count > 0 && mc.effects[0] != null
                    && mc.effects[0].description != null && !string.IsNullOrEmpty(mc.effects[0].description.text))
                {
                    sb.Append(". ").Append(mc.effects[0].description.text);
                }
            }
            // filter_Other: rewards have no per-item description in the game
            // (PluginImport.PC_AllRewards_Item returns just the name), so
            // there's nothing extra to append — the row text is the whole
            // story.

            ScreenReader.Say(sb.ToString());
        }

        private static void AppendListItemStateMarkers(StringBuilder sb, UIListItemWithIcons icons)
        {
            if (icons == null) return;
            try
            {
                if (icons.tertiaryIcon != null
                    && icons.tertiaryIcon.gameObject.activeSelf
                    && icons.tertiaryIcon.sprite != null)
                {
                    string spriteName = icons.tertiaryIcon.sprite.name ?? "";
                    if (spriteName.IndexOf("Sacrifice", StringComparison.OrdinalIgnoreCase) >= 0)
                        sb.Append(Loc.Get(" (recently sacrificed)"));
                    else if (spriteName.IndexOf("Bargained", StringComparison.OrdinalIgnoreCase) >= 0)
                        sb.Append(Loc.Get(" (recently bargained)"));
                }
                if (icons.secondaryIcon != null
                    && icons.secondaryIcon.gameObject.activeSelf
                    && icons.secondaryIcon.sprite != null)
                {
                    // MagicScreenController encodes temple damage as alpha:
                    //   0.25 = seriously damaged, 0.5 = damaged, 1.0 = healthy.
                    float alpha = icons.secondaryIcon.color.a;
                    if (alpha <= 0.3f) sb.Append(Loc.Get(" (temple seriously damaged)"));
                    else if (alpha <= 0.6f) sb.Append(Loc.Get(" (temple damaged)"));
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MagicNav.AppendListItemStateMarkers", ex);
            }
        }

        private void AnnounceSelectedDetails(MagicScreenController mc)
        {
            var sb = new StringBuilder();

            string name = mc.deityName != null ? StringHelpers.StripTags(mc.deityName.text) : "";
            if (!string.IsNullOrEmpty(name))
                sb.Append(name).Append(". ");

            string desc = mc.deityDescription != null ? mc.deityDescription.text : "";
            if (!string.IsNullOrEmpty(desc))
                sb.Append(desc).Append(". ");

            string temple = mc.templeSizeInfo != null ? mc.templeSizeInfo.text : "";
            if (!string.IsNullOrEmpty(temple))
                sb.Append(temple).Append(". ");

            // Temple HP — MagicScreenController uses TempleHitPoints / MaxTempleHP
            // to fade the secondary list icon (alpha 0.25/0.5/1.0) so sighted
            // players see damage at a glance. The localized BuildInfo text in
            // templeSizeInfo doesn't carry the damage state explicitly, so we
            // call the same Plugin APIs directly for the currently selected deity.
            if (mc.currentMagicFilter == FilterOtherworldBy.filter_Gods)
            {
                AppendTempleDamageNote(sb);
            }

            if (mc.effects != null)
            {
                bool deadDeity = IsCurrentDeityDead(mc);
                bool first = true;
                for (int i = 0; i < mc.effects.Count; i++)
                {
                    UIDeityEffectItem effect = mc.effects[i];
                    if (effect == null || effect.description == null || !effect.description.gameObject.activeSelf)
                        continue;

                    if (first) { sb.Append(Loc.Get("Blessings: ")); first = false; }
                    else sb.Append(". ");

                    string label = effect.label != null && effect.label.gameObject.activeSelf
                        ? effect.label.text : "";
                    string edesc = effect.description.text ?? "";

                    if (!string.IsNullOrEmpty(label))
                    {
                        sb.Append(label);
                        if (effect.toggle != null && effect.toggle.gameObject.activeSelf)
                            sb.Append(DescribeBlessingState(effect, deadDeity));
                        if (!string.IsNullOrEmpty(edesc))
                            sb.Append(": ").Append(edesc);
                    }
                    else if (!string.IsNullOrEmpty(edesc))
                    {
                        sb.Append(edesc);
                    }
                }
                if (!first) sb.Append(". ");
            }

            ScreenReader.Say(sb.Length > 0 ? sb.ToString() : Loc.Get("No details available."));
        }

        /// <summary>
        /// Describe a blessing's state for the screen reader. Reconstructs the game's
        /// BlessingLevel (see docs/game-api.md) from the public UIDeityEffectItem fields
        /// the game itself sets in MagicScreenController.UpdateCheckbox:
        ///   toggle not interactable          → kUnlearnedBlessing  (known of, not learned)
        ///   toggle on                        → kPermanentBlessing  (occupies a temple slot)
        ///   recentSacrifice / recentBargain  → kTransientBlessing  (sacrifice / bargain)
        ///   otherwise                        → kBlessingKnown      (learned, not in effect)
        ///
        /// <para>The checkbox alone cannot carry this: it is one on/off bit projected from a
        /// five-value enum, so "off" conflates three distinct real states. Speaking the
        /// reconstructed level keeps "is the blessing in effect" separate from "does it
        /// occupy a permanent temple slot" — those diverge for sacrifice/bargain blessings,
        /// which are in effect while their checkbox is off.</para>
        ///
        /// <para><paramref name="deityDead"/> overrides everything: for a dead deity
        /// UpdateCheckbox forces every toggle off and non-interactable, which would
        /// otherwise be misread as "not yet learned" for blessings that are merely dormant.</para>
        /// </summary>
        private static string DescribeBlessingState(UIDeityEffectItem effect, bool deityDead)
        {
            if (effect == null || effect.toggle == null) return "";
            if (deityDead) return Loc.Get(" (deity is dead, blessings have no effect)");
            if (!effect.toggle.interactable) return Loc.Get(" (not yet learned)");
            if (effect.toggle.isOn) return Loc.Get(" (in effect permanently, from the temple)");
            if (effect.recentSacrifice) return Loc.Get(" (in effect temporarily, from a sacrifice)");
            if (effect.recentBargain) return Loc.Get(" (in effect temporarily, from a spirit bargain)");
            return Loc.Get(" (learned, not in effect)");
        }

        /// <summary>
        /// True when the screen is showing a deity (not a spirit) and that deity is dead.
        /// A dead deity's blessings cannot take effect; the game also forces all of its
        /// toggles off + non-interactable, so <see cref="DescribeBlessingState"/> needs this
        /// flag to avoid reporting dormant blessings as "not yet learned".
        /// </summary>
        private static bool IsCurrentDeityDead(MagicScreenController mc)
        {
            if (mc == null || mc.currentMagicFilter != FilterOtherworldBy.filter_Gods)
                return false;
            try
            {
                return PlayerClan.IsDeityDead(Game.currentDeity);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MagicNav.IsCurrentDeityDead", ex);
                return false;
            }
        }

        /// <summary>
        /// Speak the temple's hit-point bucket for the currently selected deity.
        /// Matches the bucketing MagicScreenController.FilterChanged uses for
        /// the secondary list icon alpha (HP &lt; max/2 → seriously damaged,
        /// HP &lt; max → damaged, HP == max → silent). The plugin call returns
        /// 0 for no temple, in which case we say nothing.
        /// </summary>
        private static void AppendTempleDamageNote(StringBuilder sb)
        {
            try
            {
                Deity deity = Game.currentDeity;
                if (deity == Deity.deity_None) return;
                int hp = PlayerClan.TempleHitPoints(deity);
                int max = PlayerClan.MaxTempleHP(deity);
                if (max <= 0 || hp >= max) return;
                if (hp < max / 2) sb.Append(Loc.Get("Temple seriously damaged. "));
                else sb.Append(Loc.Get("Temple damaged. "));
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MagicNav.AppendTempleDamageNote", ex);
            }
        }

        /// <summary>
        /// Build the spoken prefix for entering the Blessings zone: the zone name, a
        /// temple-slot summary ("2 of 3 permanent temple slots in use" — Gods view only,
        /// since spirits have no temple), and the entry count. The slot summary answers
        /// "how many blessings are chosen" up front; per-blessing state then comes from
        /// <see cref="DescribeBlessingState"/> as the user arrows through.
        /// </summary>
        private static string BlessingsZonePrefix(MagicScreenController mc, int visibleCount)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Blessings"));

            if (mc.currentMagicFilter == FilterOtherworldBy.filter_Gods && !IsCurrentDeityDead(mc))
            {
                try
                {
                    int capacity = PluginImport.PC_TempleSize(Game.currentDeity);
                    if (capacity > 0)
                    {
                        int used = 0;
                        for (int i = 0; i < mc.effects.Count; i++)
                        {
                            UIDeityEffectItem e = mc.effects[i];
                            if (e != null && e.toggle != null && e.toggle.gameObject.activeSelf && e.toggle.isOn)
                                used++;
                        }
                        sb.Append(". ").Append(string.Format(
                            Loc.Get("{0} of {1} permanent temple slots in use"), used, capacity));
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Error("MagicNav.BlessingsZonePrefix", ex);
                }
            }

            sb.Append(". ").Append(visibleCount).Append(Loc.Get(" entries. "));
            return sb.ToString();
        }

        private void AnnounceBlessing(MagicScreenController mc, int effectIndex, string prefix = "")
        {
            UIDeityEffectItem effect = mc.effects[effectIndex];
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(prefix))
                sb.Append(prefix);

            string label = effect.label != null && effect.label.gameObject.activeSelf
                ? effect.label.text : "";
            string desc = effect.description != null ? effect.description.text : "";

            if (!string.IsNullOrEmpty(label))
            {
                sb.Append(label);
                if (effect.toggle != null && effect.toggle.gameObject.activeSelf)
                    sb.Append(DescribeBlessingState(effect, IsCurrentDeityDead(mc)));
            }

            if (!string.IsNullOrEmpty(desc))
            {
                if (!string.IsNullOrEmpty(label)) sb.Append(". ");
                sb.Append(desc);
            }

            ScreenReader.Say(sb.ToString());
        }

        private void AnnounceButton(UIButton btn, int index, int total, string prefix = "")
        {
            string label = btn.label != null ? btn.label.text : btn.gameObject.name;
            string availability = btn.IsInteractable() ? "" : Loc.Get(", not available");
            ScreenReader.Say(prefix + label + availability);
        }

        private void AnnounceDescription(MagicScreenController mc)
        {
            switch (_zone)
            {
                case MagicZone.Filter:
                {
                    switch (mc.currentMagicFilter)
                    {
                        case FilterOtherworldBy.filter_Gods:
                            ScreenReader.Say(Loc.Get("Gods: shows deities your clan knows. Select one to see blessings and temple status. You can sacrifice, build temples, or perform rituals."));
                            break;
                        case FilterOtherworldBy.filter_Spirits:
                            ScreenReader.Say(Loc.Get("Spirits: shows spirits your clan knows. You can bargain with well-known spirits for temporary blessings."));
                            break;
                        case FilterOtherworldBy.filter_SacredTime:
                            ScreenReader.Say(Loc.Get("Sacred Time: shows magic categories for the next Sacred Time allocation."));
                            break;
                        default:
                            ScreenReader.Say(Loc.Get("Other: shows other magical effects currently active."));
                            break;
                    }
                    break;
                }
                case MagicZone.List:
                {
                    var sb = new StringBuilder();

                    string name = mc.deityName != null ? StringHelpers.StripTags(mc.deityName.text) : "";
                    if (!string.IsNullOrEmpty(name))
                        sb.Append(name).Append(". ");

                    string desc = mc.deityDescription != null ? mc.deityDescription.text : "";
                    if (!string.IsNullOrEmpty(desc))
                        sb.Append(desc).Append(". ");

                    string temple = mc.templeSizeInfo != null ? mc.templeSizeInfo.text : "";
                    if (!string.IsNullOrEmpty(temple))
                        sb.Append(temple).Append(". ");

                    if (mc.effects != null)
                    {
                        bool deadDeity = IsCurrentDeityDead(mc);
                        bool first = true;
                        for (int i = 0; i < mc.effects.Count; i++)
                        {
                            UIDeityEffectItem effect = mc.effects[i];
                            if (effect == null || effect.description == null
                                || !effect.description.gameObject.activeSelf)
                                continue;

                            string label = effect.label != null && effect.label.gameObject.activeSelf
                                ? effect.label.text : "";
                            string edesc = effect.description.text ?? "";

                            if (string.IsNullOrEmpty(label) && string.IsNullOrEmpty(edesc))
                                continue;

                            if (first) { sb.Append(Loc.Get("Blessings: ")); first = false; }
                            else sb.Append(". ");

                            if (!string.IsNullOrEmpty(label))
                            {
                                sb.Append(label);
                                if (effect.toggle != null && effect.toggle.gameObject.activeSelf)
                                    sb.Append(DescribeBlessingState(effect, deadDeity));
                                if (!string.IsNullOrEmpty(edesc))
                                    sb.Append(": ").Append(edesc);
                            }
                            else
                            {
                                sb.Append(edesc);
                            }
                        }
                        if (!first) sb.Append(". ");
                    }

                    ScreenReader.Say(sb.Length > 0 ? sb.ToString() : Loc.Get("No description available."));
                    break;
                }
                case MagicZone.Blessings:
                {
                    var visible = GetVisibleBlessingIndices(mc);
                    if (visible.Count == 0 || _blessingIndex < 0
                        || _blessingIndex >= visible.Count)
                    {
                        ScreenReader.Say(Loc.Get("No blessing selected."));
                        break;
                    }

                    UIDeityEffectItem effect = mc.effects[visible[_blessingIndex]];
                    var sb = new StringBuilder();

                    string label = effect.label != null && effect.label.gameObject.activeSelf
                        ? effect.label.text : "";
                    if (!string.IsNullOrEmpty(label))
                    {
                        sb.Append(label);
                        if (effect.toggle != null && effect.toggle.gameObject.activeSelf)
                            sb.Append(DescribeBlessingState(effect, IsCurrentDeityDead(mc)));
                        sb.Append(". ");
                    }

                    string desc = effect.description != null ? effect.description.text : "";
                    if (!string.IsNullOrEmpty(desc))
                        sb.Append(desc);

                    ScreenReader.Say(sb.Length > 0 ? sb.ToString() : Loc.Get("No description available."));
                    break;
                }
                case MagicZone.Buttons:
                {
                    var sb = new StringBuilder();

                    string temple = mc.templeSizeInfo != null ? mc.templeSizeInfo.text : "";
                    if (!string.IsNullOrEmpty(temple))
                        sb.Append(temple).Append(". ");

                    var buttons = GetVisibleButtons(mc);
                    for (int i = 0; i < buttons.Count; i++)
                    {
                        UIButton btn = buttons[i];
                        string label = btn.label != null ? btn.label.text : btn.gameObject.name;
                        if (i > 0) sb.Append(". ");
                        sb.Append(label);
                        if (!btn.IsInteractable())
                            sb.Append(Loc.Get(", not available"));
                    }

                    ScreenReader.Say(sb.Length > 0 ? sb.ToString() : Loc.Get("No actions available."));
                    break;
                }
            }
        }
    }
}
