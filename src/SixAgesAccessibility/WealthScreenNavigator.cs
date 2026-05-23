using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Two-zone keyboard navigation for the Wealth management screen.
    ///
    /// The screen displays seven resource counters (cattle, goats, horses, goods,
    /// exotic goods, food, market frequency) plus two UILists (treasures, trade
    /// partners) and a single CARAVAN action button. Without dedicated handling
    /// the lists are unreachable from the keyboard — UIList rows are not
    /// Selectables and flat Tab nav only ever lands on the one button. We expose
    /// both lists via Tab.
    ///
    /// Keys follow the unified Model Y scheme: arrows browse, Space activates the
    /// focused element (view a treasure, select a trade partner), D reads its
    /// description, and a blank Enter triggers the screen's one action — opening
    /// the Caravan dialog. C is a letter-key alias for that action, matching the
    /// War screen's F/R/O/C/W "letter opens sub-dialog" convention. Tab cycles
    /// zones; L jumps to the treasures list.
    /// </summary>
    public class WealthScreenNavigator
    {
        private enum Zone { Treasures, TradeClans }

        private Zone _zone = Zone.Treasures;
        private int _treasureIndex = -1;
        private int _tradeIndex = -1;

        public void ResetForNewScreen()
        {
            _zone = Zone.Treasures;
            _treasureIndex = -1;
            _tradeIndex = -1;
        }

        /// <summary>Top-level dispatch — called every Update while Wealth is active.</summary>
        public void HandleInput(WealthScreenController c)
        {
            if (c == null) return;

            // Enter — the screen's one action: open the Caravan dialog. Model Y
            // makes a blank Enter the universal main-action key (mirrors the
            // Relations screen, where Enter opens the emissary dialog).
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TryOpenCaravan(c);
                return;
            }

            // Tab / Shift+Tab — cycle zones (skips empty lists).
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                int dir = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? -1 : 1;
                CycleZone(c, dir);
                return;
            }

            // L — jump straight to the treasures list. Treasures are the more
            // commonly-inspected list (activating a treasure shows its summary in
            // the description box), so make it the default L target.
            if (Input.GetKeyDown(KeyCode.L) && !AnyModifier())
            {
                _zone = Zone.Treasures;
                AnnounceZone(c);
                return;
            }

            // C — open the Caravan dialog. A letter-key alias for the Enter
            // action: the Wealth screen exists mainly to launch a caravan, and
            // a dedicated hotkey matches the War screen's F/R/O/C/W convention.
            if (Input.GetKeyDown(KeyCode.C) && !AnyModifier())
            {
                TryOpenCaravan(c);
                return;
            }

            // D — read the description for the focused item.
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceDescription(c);
                return;
            }

            // Escape — management screens have no back; hint at screen switching.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ScreenReader.Say(Loc.Get("Use Ctrl+1 through 9 to switch screens, or Shift+F1 for shortcuts."));
                return;
            }

            // Up/Down — cycle items in the active list.
            if (Input.GetKeyDown(KeyCode.UpArrow) && !AnyModifier())
            {
                MoveItem(c, -1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow) && !AnyModifier())
            {
                MoveItem(c, +1);
                return;
            }

            // Space — activate the focused item via the list's own OnItemClicked
            // handler. For treasures this opens the description box (same path as
            // a mouse click); for trade clans it sets the selected clan and
            // exposes its info.
            if (Input.GetKeyDown(KeyCode.Space) && !AnyModifier())
            {
                ActivateFocused(c);
                return;
            }
        }

        // ---------- Zone selection ----------

        private void CycleZone(WealthScreenController c, int direction)
        {
            int zoneCount = 2;
            int current = (int)_zone;

            for (int i = 0; i < zoneCount; i++)
            {
                current += direction;
                if (current < 0) current = zoneCount - 1;
                if (current >= zoneCount) current = 0;

                Zone candidate = (Zone)current;
                if (IsZoneAvailable(c, candidate))
                {
                    _zone = candidate;
                    AnnounceZone(c);
                    return;
                }
            }

            // Both empty — fall back to a status announce so the user isn't met
            // with silence after Tab.
            AnnounceFullStatus(c);
        }

        private bool IsZoneAvailable(WealthScreenController c, Zone zone)
        {
            switch (zone)
            {
                case Zone.Treasures:  return c.treasures != null && c.treasures.gameObject.activeSelf && c.treasures.count > 0;
                case Zone.TradeClans: return c.tradeClans != null && c.tradeClans.gameObject.activeSelf && c.tradeClans.count > 0;
                default:              return false;
            }
        }

        private void AnnounceZone(WealthScreenController c)
        {
            switch (_zone)
            {
                case Zone.Treasures:
                {
                    int count = c.treasures != null ? c.treasures.count : 0;
                    ScreenReader.Say(Loc.Get("Treasures zone. ") + count
                        + (count == 1 ? Loc.Get(" treasure.") : Loc.Get(" treasures."))
                        + Loc.Get(" Up and Down to cycle, D for description, Space to view."));
                    break;
                }
                case Zone.TradeClans:
                {
                    int count = c.tradeClans != null ? c.tradeClans.count : 0;
                    ScreenReader.Say(Loc.Get("Trade partners zone. ") + count
                        + (count == 1 ? Loc.Get(" clan.") : Loc.Get(" clans."))
                        + Loc.Get(" Up and Down to cycle, D for clan info, Space selects."));
                    break;
                }
            }
        }

        // ---------- Item navigation ----------

        private void MoveItem(WealthScreenController c, int direction)
        {
            UIList list = GetActiveList(c);
            if (list == null || list.count == 0)
            {
                ScreenReader.Say(Loc.Get("List is empty."));
                return;
            }

            int idx = (_zone == Zone.Treasures) ? _treasureIndex : _tradeIndex;
            idx += direction;
            if (idx < 0) idx = list.count - 1;
            if (idx >= list.count) idx = 0;

            if (_zone == Zone.Treasures) _treasureIndex = idx;
            else _tradeIndex = idx;

            AnnounceCurrentItem(c, list, idx);
        }

        private void AnnounceCurrentItem(WealthScreenController c, UIList list, int idx)
        {
            UIListItem item = list[idx];
            if (item == null) { ScreenReader.Say(Loc.Get("(empty row)")); return; }

            if (_zone == Zone.TradeClans)
            {
                string formatted = TryFormatClanItem(item);
                if (!string.IsNullOrEmpty(formatted))
                {
                    ScreenReader.Say(formatted);
                    return;
                }
            }

            string text = !string.IsNullOrEmpty(item.text) ? item.text : item.gameObject.name;
            ScreenReader.Say(StringHelpers.StripTags(text));
        }

        private void ActivateFocused(WealthScreenController c)
        {
            UIList list = GetActiveList(c);
            if (list == null || list.count == 0) return;

            int idx = (_zone == Zone.Treasures) ? _treasureIndex : _tradeIndex;
            if (idx < 0 || idx >= list.count)
            {
                ScreenReader.Say(Loc.Get("Use Up and Down to pick an item first."));
                return;
            }

            UIListItem item = list[idx];
            if (item == null) return;

            try { list.OnItemClicked(item); }
            catch (Exception ex) { DebugLogger.Error("WealthNav.OnItemClicked", ex); }

            // Read back what just happened. The treasure description box is
            // populated synchronously in OnListItemSelected → ShowInfo, so a
            // direct read works without waiting on a callback.
            if (_zone == Zone.Treasures)
            {
                string desc = SafeTreasureSummary(c, idx);
                if (!string.IsNullOrEmpty(desc)) ScreenReader.Say(desc);
                else ScreenReader.Say(Loc.Get("Selected treasure."));
            }
            else
            {
                ScreenReader.Say(Loc.Get("Selected clan."));
            }
        }

        private void AnnounceDescription(WealthScreenController c)
        {
            UIList list = GetActiveList(c);
            if (list == null || list.count == 0) { ScreenReader.Say(Loc.Get("No items in list.")); return; }

            int idx = (_zone == Zone.Treasures) ? _treasureIndex : _tradeIndex;
            if (idx < 0 || idx >= list.count)
            {
                ScreenReader.Say(Loc.Get("Use Up and Down to pick an item first."));
                return;
            }

            if (_zone == Zone.Treasures)
            {
                string desc = SafeTreasureSummary(c, idx);
                if (!string.IsNullOrEmpty(desc))
                    ScreenReader.Say(desc);
                else
                    ScreenReader.Say(Loc.Get("No description available for this treasure."));
                return;
            }

            // Trade clans — read clan synopsis (mirrors the Klan-D-Taste path).
            string clanInfo = TryReadClanDescription(list[idx]);
            if (!string.IsNullOrEmpty(clanInfo))
                ScreenReader.Say(clanInfo);
            else
                ScreenReader.Say(Loc.Get("No description available for this clan."));
        }

        // ---------- Caravan action ----------

        private void TryOpenCaravan(WealthScreenController c)
        {
            // Prefer the on-screen button so any extra listeners (sound effects,
            // focus changes) fire as they would on a mouse click. If the button
            // is missing or hidden, fall back to ShowCaravanDialog directly —
            // the controller method is internally guarded by `if (base.isActive)`.
            UIButton btn = FindCaravanButton(c);
            if (btn != null && btn.gameObject.activeSelf && btn.IsInteractable())
            {
                ScreenReader.Say(Loc.Get("Opening Caravan dialog."));
                try
                {
                    var ev = new BaseEventData(EventSystem.current);
                    ((ISubmitHandler)btn).OnSubmit(ev);
                }
                catch (Exception ex)
                {
                    DebugLogger.Error("WealthNav.OpenCaravan.Submit", ex);
                }
                return;
            }

            ScreenReader.Say(Loc.Get("Opening Caravan dialog."));
            try { c.ShowCaravanDialog(); }
            catch (Exception ex) { DebugLogger.Error("WealthNav.ShowCaravanDialog", ex); }
        }

        // ---------- Full status (F5) ----------

        public void AnnounceFullStatus(WealthScreenController c)
        {
            var sb = new StringBuilder();

            int treasureCount = c.treasures != null ? c.treasures.count : 0;
            int clanCount = c.tradeClans != null ? c.tradeClans.count : 0;

            sb.Append(Loc.Get("Wealth status. "));
            sb.Append(treasureCount).Append(treasureCount == 1 ? Loc.Get(" treasure, ") : Loc.Get(" treasures, "));
            sb.Append(clanCount).Append(clanCount == 1 ? Loc.Get(" trade partner. ") : Loc.Get(" trade partners. "));

            if (_zone == Zone.Treasures)
                sb.Append(Loc.Get("Treasures zone active"));
            else
                sb.Append(Loc.Get("Trade partners zone active"));

            int idx = (_zone == Zone.Treasures) ? _treasureIndex : _tradeIndex;
            UIList list = GetActiveList(c);
            if (list != null && idx >= 0 && idx < list.count && list[idx] != null)
                sb.Append(Loc.Get(", focused: ")).Append(StringHelpers.StripTags(list[idx].text ?? ""));

            sb.Append(Loc.Get(". Tab cycles zones, Up and Down cycle items, D for description, "));
            sb.Append(Loc.Get("Space selects, Enter opens Caravan, F5 for status."));

            ScreenReader.Say(sb.ToString());
        }

        // ---------- Helpers ----------

        private UIList GetActiveList(WealthScreenController c)
        {
            return _zone == Zone.Treasures ? c.treasures : c.tradeClans;
        }

        private static string SafeTreasureSummary(WealthScreenController c, int idx)
        {
            try
            {
                if (c.treasureData == null) return null;
                if (idx < 0 || idx >= c.treasureData.count) return null;
                return c.treasureData[idx].summaryWithParens;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("WealthNav.TreasureSummary", ex);
                return null;
            }
        }

        private static string TryFormatClanItem(UIListItem item)
        {
            UIListItemWithIcons icons = item as UIListItemWithIcons;
            if (icons == null || icons.key <= 0) return null;

            Clan clan;
            try { clan = Clan.ClanWithIndex(icons.key); }
            catch (Exception ex)
            {
                DebugLogger.Error("WealthNav.ClanWithIndex", ex);
                return null;
            }
            if (clan.isNull || string.IsNullOrEmpty(clan.name)) return null;

            var sb = new StringBuilder();
            sb.Append(clan.name);
            // Attitude (hostile/friendly/allied/...) — what sighted players read
            // from clan-name colour on the map; "in our tribe" piggybacks on the
            // same call (the underline marker on the map).
            sb.Append(", ").Append(StringHelpers.AttitudeLabel(clan.attitudeColor, clan.inOurTribe));
            switch (clan.culture)
            {
                case Culture.culture_Chariot:   sb.Append(", ").Append(Loc.Get("Chariot")); break;
                case Culture.culture_Hyaloring: sb.Append(", ").Append(Loc.Get("Hyaloring")); break;
                case Culture.culture_Orlanthi:  sb.Append(", ").Append(Loc.Get("Orlanthi")); break;
            }
            if (clan.isClose) sb.Append(", ").Append(Loc.Get("near"));
            if (clan.haveFeud) sb.Append(Loc.Get(", feud"));
            if (clan.haveTrade) sb.Append(", ").Append(Loc.Get("trading"));
            if (clan.visitsByEmissary > 0) sb.Append(", ").Append(Loc.Get("visited by emissary"));
            if (clan.visitsByCaravan > 0) sb.Append(", ").Append(Loc.Get("visited by caravan"));
            return sb.ToString();
        }

        private static string TryReadClanDescription(UIListItem item)
        {
            UIListItemWithIcons icons = item as UIListItemWithIcons;
            if (icons == null || icons.key <= 0) return null;

            Clan clan;
            try { clan = Clan.ClanWithIndex(icons.key); }
            catch (Exception ex)
            {
                DebugLogger.Error("WealthNav.ClanWithIndex.D", ex);
                return null;
            }
            if (clan.isNull) return null;

            try { return StringHelpers.StripTags(clan.ExplanationWithDetail(2)); }
            catch (Exception ex)
            {
                DebugLogger.Error("WealthNav.ExplanationWithDetail", ex);
                return null;
            }
        }

        private static UIButton FindCaravanButton(WealthScreenController c)
        {
            try
            {
                UIButton[] all = c.GetComponentsInChildren<UIButton>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    UIButton b = all[i];
                    if (b == null) continue;
                    var click = b.onClick;
                    if (click == null) continue;
                    int n = click.GetPersistentEventCount();
                    for (int k = 0; k < n; k++)
                    {
                        if (!System.Object.ReferenceEquals(click.GetPersistentTarget(k), c)) continue;
                        if (click.GetPersistentMethodName(k) == "ShowCaravanDialog")
                            return b;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("WealthNav.FindCaravanButton", ex);
            }
            return null;
        }

        /// <summary>True when the on-screen Caravan button corresponds to <paramref name="btn"/>.</summary>
        public static bool IsCaravanButton(UIButton btn, WealthScreenController c)
        {
            if (btn == null || c == null) return false;
            try
            {
                var click = btn.onClick;
                if (click == null) return false;
                int n = click.GetPersistentEventCount();
                for (int i = 0; i < n; i++)
                {
                    if (!System.Object.ReferenceEquals(click.GetPersistentTarget(i), c)) continue;
                    if (click.GetPersistentMethodName(i) == "ShowCaravanDialog")
                        return true;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("WealthNav.IsCaravanButton", ex);
            }
            return false;
        }

        private static bool AnyModifier()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }
    }
}
