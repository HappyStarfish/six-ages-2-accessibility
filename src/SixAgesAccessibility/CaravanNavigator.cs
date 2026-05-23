using System;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Zone-based keyboard navigation for the Caravan dialog — the most complex
    /// dialog in the game. Six functional regions:
    ///
    /// <list type="bullet">
    /// <item><description>List — recipient clan</description></item>
    /// <item><description>Mode — Sell/Buy radio plus the alternative "establish trade route" toggle</description></item>
    /// <item><description>Goods — five buy/sell pairs (Food, Goods, Herds, Horses, Treasure) gated by clan capabilities</description></item>
    /// <item><description>Treasures — picks a specific item when "sell treasure" is on</description></item>
    /// <item><description>Escort — elite + regular warrior sliders, and the caravan-size radio</description></item>
    /// <item><description>Leader — opens the leader-picker sub-dialog</description></item>
    /// </list>
    ///
    /// <para>Keys follow the unified Model Y scheme: a blank Enter completes the
    /// screen (Send), handled globally so it works from every zone; Space acts on
    /// the focused element (select a clan or treasure, flip a toggle, open the
    /// leader chooser); arrows browse and adjust sliders; D reads details; Escape
    /// closes. Tab cycles zones, L jumps to the clan list, R reads the active
    /// trade routes. The flat Tab nav would read these regions as bare GameObject
    /// names ("buyFood, off" / "sellGoods, off") with no semantic context and no
    /// list reachability.</para>
    /// </summary>
    public class CaravanNavigator
    {
        private enum Zone { List, Mode, Goods, Treasures, Escort, Leader }
        private const int ZoneCount = 6;

        // 5 commodities × 2 directions; layout: pair index 0..4 = Food, Goods, Herds,
        // Horses, Treasure. Within each pair, 0=buy and 1=sell.
        private enum Commodity { Food = 0, Goods = 1, Herds = 2, Horses = 3, Treasure = 4 }
        private const int CommodityCount = 5;
        private const int GoodsItemCount = 10; // 5 commodities × 2 (buy/sell)

        private enum ModeItem { SellBuy = 0, EstablishRoute = 1 }
        private const int ModeItemCount = 2;

        private enum EscortItem { Elite = 0, Regular = 1, CaravanSize = 2 }
        private const int EscortItemCount = 3;

        private static FieldInfo _leaderIndexField;

        private Zone _zone = Zone.List;
        private int _listIndex = -1;
        private int _modeIndex = -1;
        private int _goodsIndex = -1;
        private int _treasureIndex = -1;
        private int _escortIndex = -1;
        private readonly ConfirmGate _confirmGate = new ConfirmGate();

        public void ResetForNewScreen()
        {
            _zone = Zone.List;
            _listIndex = -1;
            _modeIndex = -1;
            _goodsIndex = -1;
            _treasureIndex = -1;
            _escortIndex = -1;
            _confirmGate.Reset();
        }

        public void HandleInput(CaravanDialogController d)
        {
            if (d == null) return;

            // Enter — Model Y: a blank Enter is the universal screen-completion
            // key (Send), handled globally so it works from every zone. No
            // modifier is required; a held Ctrl is neither needed nor blocked.
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
                CycleZone(d, dir);
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

            // R — list current trade-route partners. The Caravan dialog
            // visually surfaces these as map lines (mapAnnotations.showTradeRoutes
            // → MapView.UpdateTradeRoutes pulls ClanFilterBy.filter_TradeWithUs).
            // We expose the same data as a spoken list so blind users see what
            // sighted players see on the trade-route overlay.
            if (Input.GetKeyDown(KeyCode.R) && !AnyModifier())
            {
                AnnounceTradeRoutes();
                return;
            }

            // Any zone-specific mutator key drops a pending Enter confirmation.
            // Tab/L/F5/R/Enter/Escape have already returned above; D in each zone
            // is read-only and isn't listed here, so it preserves the pending state.
            if (_confirmGate.IsPending
                && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)
                 || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)
                 || Input.GetKeyDown(KeyCode.Space)))
                _confirmGate.Reset();

            switch (_zone)
            {
                case Zone.List:      HandleListInput(d);      break;
                case Zone.Mode:      HandleModeInput(d);      break;
                case Zone.Goods:     HandleGoodsInput(d);     break;
                case Zone.Treasures: HandleTreasuresInput(d); break;
                case Zone.Escort:    HandleEscortInput(d);    break;
                case Zone.Leader:    HandleLeaderInput(d);    break;
            }
        }

        // ---------- Zone selection ----------

        private void CycleZone(CaravanDialogController d, int dir)
        {
            // Treasures zone is only reachable when treasure trade is enabled — skip
            // it otherwise so Tab doesn't park the user on a hidden region.
            for (int i = 0; i < ZoneCount; i++)
            {
                int z = (int)_zone + dir;
                if (z < 0) z = ZoneCount - 1;
                if (z >= ZoneCount) z = 0;
                _zone = (Zone)z;
                if (_zone == Zone.Treasures && !IsTreasureTradeActive(d))
                    continue;
                return;
            }
        }

        private static bool IsTreasureTradeActive(CaravanDialogController d)
        {
            return (d.sellTreasure != null && d.sellTreasure.isOn)
                || (d.buyTreasure != null && d.buyTreasure.isOn);
        }

        private void AnnounceZone(CaravanDialogController d)
        {
            switch (_zone)
            {
                case Zone.List:      ScreenReader.Say(Loc.Get("Recipient clan."));        AnnounceCurrentClan(d); break;
                case Zone.Mode:      ScreenReader.Say(Loc.Get("Trade mode."));            AnnounceCurrentMode(d); break;
                case Zone.Goods:     ScreenReader.Say(Loc.Get("Goods to trade."));        AnnounceCurrentGoods(d); break;
                case Zone.Treasures: ScreenReader.Say(Loc.Get("Treasure list."));         AnnounceCurrentTreasure(d); break;
                case Zone.Escort:    ScreenReader.Say(Loc.Get("Warriors and caravan size.")); AnnounceCurrentEscort(d); break;
                case Zone.Leader:    AnnounceLeaderHeader(d); break;
            }
        }

        // ---------- List zone ----------

        private void HandleListInput(CaravanDialogController d)
        {
            if (d.clanList == null || d.clanList.count == 0) return;

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _listIndex--;
                if (_listIndex < 0) _listIndex = d.clanList.count - 1;
                AnnounceCurrentClan(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _listIndex++;
                if (_listIndex >= d.clanList.count) _listIndex = 0;
                AnnounceCurrentClan(d);
                return;
            }
            // Space — select the focused clan as the caravan recipient.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SelectCurrentClan(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceClanSynopsis(d);
                return;
            }
        }

        private void AnnounceCurrentClan(CaravanDialogController d)
        {
            if (d.clanList == null || d.clanList.count == 0)
            {
                ScreenReader.Say(Loc.Get("No clans available."));
                return;
            }
            if (_listIndex < 0) _listIndex = d.clanList.selectedIndex >= 0 ? d.clanList.selectedIndex : 0;
            if (_listIndex >= d.clanList.count) _listIndex = d.clanList.count - 1;

            var item = d.clanList[_listIndex];
            string marker = (item != null && item.key == Game.ClanVariable("otherClan"))
                ? Loc.Get(", selected") : "";
            ScreenReader.Say(ClanItemSummary(item, marker));
        }

        private void SelectCurrentClan(CaravanDialogController d)
        {
            if (d.clanList == null || _listIndex < 0 || _listIndex >= d.clanList.count) return;
            var item = d.clanList[_listIndex];
            if (item == null) return;

            // Mouse-equivalent path: OnItemClicked → SelectedClan → UpdateForSelectedClan
            // → UpdateGoals + UpdateCheckboxesExceptTreasure. So once this returns,
            // establishRoute.interactable / sellBuy.isOn / goods-toggle interactability
            // already reflect the new clan.
            d.clanList.OnItemClicked(item);

            var sb = new StringBuilder();
            sb.Append(Clan.ClanWithIndex(item.key).name).Append(Loc.Get(" selected. "));
            sb.Append(DescribeClanRelationship(d, item.key));

            ScreenReader.Say(sb.ToString());
        }

        /// <summary>
        /// Spoken trailer after picking a clan: explains the deal options that
        /// just opened or closed. Mirrors <c>UpdateGoals</c> + the horses gate
        /// in <c>UpdateCheckboxesExceptTreasure</c> so the user knows why a
        /// later toggle is locked without having to discover it by trial.
        /// </summary>
        private static string DescribeClanRelationship(CaravanDialogController d, int clanKey)
        {
            bool hasTrade = false;
            bool clanHasHorses = true;
            try { hasTrade = PluginImport.Clan_HaveTrade(clanKey); }
            catch (Exception ex) { DebugLogger.Error("CaravanNav.HaveTrade", ex); }
            try { clanHasHorses = PluginImport.Clan_HasHorses(clanKey); }
            catch (Exception ex) { DebugLogger.Error("CaravanNav.HasHorses", ex); }

            var sb = new StringBuilder();
            if (hasTrade)
                sb.Append(Loc.Get("Already trading with this clan — Establish Route is locked, goods and treasures still work."));
            else
                sb.Append(Loc.Get("No existing trade — Establish Route is available."));

            if (!clanHasHorses)
                sb.Append(Loc.Get(" This clan has no horses — Buy Horses and Sell Horses are unavailable."));

            return sb.ToString();
        }

        private void AnnounceClanSynopsis(CaravanDialogController d)
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
                DebugLogger.Error("CaravanNav.AnnounceClanSynopsis", ex);
            }
        }

        /// <summary>
        /// Clan-list row summary: name, the "selected" marker (right after the
        /// name so fast arrow browsing can't cut it off), then trade/feud status.
        /// </summary>
        private static string ClanItemSummary(UIListItem item, string marker)
        {
            if (item == null) return Loc.Get("Unknown clan");
            try
            {
                Clan clan = Clan.ClanWithIndex(item.key);
                var sb = new StringBuilder();
                sb.Append(clan.name);
                if (!string.IsNullOrEmpty(marker)) sb.Append(marker);
                if (clan.haveTrade) sb.Append(Loc.Get(", trade partner"));
                if (clan.haveFeud) sb.Append(Loc.Get(", feud"));
                return sb.ToString();
            }
            catch
            {
                return item.text ?? Loc.Get("Clan");
            }
        }

        // ---------- Mode zone ----------

        private void HandleModeInput(CaravanDialogController d)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _modeIndex--;
                if (_modeIndex < 0) _modeIndex = ModeItemCount - 1;
                AnnounceCurrentMode(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _modeIndex++;
                if (_modeIndex >= ModeItemCount) _modeIndex = 0;
                AnnounceCurrentMode(d);
                return;
            }
            // Space — flip the focused mode toggle.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ToggleCurrentMode(d);
                return;
            }
        }

        private void AnnounceCurrentMode(CaravanDialogController d)
        {
            if (_modeIndex < 0) _modeIndex = 0;
            switch ((ModeItem)_modeIndex)
            {
                case ModeItem.SellBuy:
                {
                    UIToggle t = d.sellBuy;
                    if (t == null) { ScreenReader.Say(Loc.Get("(toggle not available)")); break; }
                    string label = GetToggleLabel(t);
                    string locked = t.interactable ? "" : Loc.Get(", locked");
                    ScreenReader.Say(label + ", " + Loc.Get(t.isOn ? "on" : "off") + locked);
                    break;
                }
                case ModeItem.EstablishRoute:
                {
                    UIToggle t = d.establishRoute;
                    if (t == null || !t.gameObject.activeSelf)
                    {
                        ScreenReader.Say(GetToggleLabel(t) + Loc.Get(", not available."));
                        break;
                    }
                    string label = GetToggleLabel(t);
                    string locked = t.interactable ? "" : Loc.Get(", locked");
                    ScreenReader.Say(label + ", " + Loc.Get(t.isOn ? "on" : "off") + locked);
                    break;
                }
            }
        }

        private void ToggleCurrentMode(CaravanDialogController d)
        {
            switch ((ModeItem)_modeIndex)
            {
                case ModeItem.SellBuy:
                {
                    UIToggle t = d.sellBuy;
                    if (t == null) { ScreenReader.Say(Loc.Get("(toggle not available)")); return; }
                    if (!t.interactable) { ScreenReader.Say(GetToggleLabel(t) + Loc.Get(", locked")); return; }
                    t.Set(!t.isOn, sendCallback: true);
                    ScreenReader.Say(GetToggleLabel(t) + " " + Loc.Get(t.isOn ? "on" : "off"));
                    break;
                }
                case ModeItem.EstablishRoute:
                {
                    UIToggle t = d.establishRoute;
                    if (t == null || !t.interactable)
                    {
                        ScreenReader.Say(GetToggleLabel(t) + Loc.Get(", locked"));
                        return;
                    }
                    t.Set(!t.isOn, sendCallback: true);
                    ScreenReader.Say(GetToggleLabel(t) + " " + Loc.Get(t.isOn ? "on" : "off"));
                    break;
                }
            }
        }

        // ---------- Goods zone (10 toggles) ----------

        private void HandleGoodsInput(CaravanDialogController d)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _goodsIndex--;
                if (_goodsIndex < 0) _goodsIndex = GoodsItemCount - 1;
                AnnounceCurrentGoods(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _goodsIndex++;
                if (_goodsIndex >= GoodsItemCount) _goodsIndex = 0;
                AnnounceCurrentGoods(d);
                return;
            }
            // Space — flip the focused buy/sell toggle.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ToggleCurrentGoods(d);
                return;
            }
        }

        private void AnnounceCurrentGoods(CaravanDialogController d)
        {
            if (_goodsIndex < 0) _goodsIndex = 0;
            UIToggle t = GoodsToggleAt(d, _goodsIndex);
            string label = GoodsLabelWithDirection(d, _goodsIndex);
            if (t == null || !t.gameObject.activeSelf)
            {
                ScreenReader.Say(label + Loc.Get(", unavailable"));
                return;
            }
            string locked = t.interactable ? "" : Loc.Get(", locked");
            ScreenReader.Say(label + ", " + Loc.Get(t.isOn ? "on" : "off") + locked);
        }

        private void ToggleCurrentGoods(CaravanDialogController d)
        {
            UIToggle t = GoodsToggleAt(d, _goodsIndex);
            string label = GoodsLabelWithDirection(d, _goodsIndex);
            if (t == null || !t.gameObject.activeSelf)
            {
                ScreenReader.Say(label + Loc.Get(", not available"));
                return;
            }
            if (!t.interactable)
            {
                string reason = WhyGoodsLocked(d, _goodsIndex);
                ScreenReader.Say(string.IsNullOrEmpty(reason)
                    ? label + Loc.Get(", locked")
                    : label + Loc.Get(", locked. ") + reason);
                return;
            }
            t.Set(!t.isOn, sendCallback: true);
            ScreenReader.Say(label + " " + Loc.Get(t.isOn ? "on" : "off"));

            // Sell-treasure auto-selects index 0 if no treasure is chosen yet
            // (CaravanDialogController.CheckboxTapped). Surface that side-effect
            // so the user knows which treasure the deal is now bound to.
            // The list rows hold the treasure name in their text field
            // (filled by SetupUI from treasureDataSource[i].name), so we read
            // straight from the UIList rather than via the private data source.
            if (t == d.sellTreasure && t.isOn && d.treasures != null && d.treasures.selectedIndex >= 0)
            {
                int sel = d.treasures.selectedIndex;
                if (sel < d.treasures.count)
                {
                    UIListItem row = d.treasures[sel];
                    string treasureName = row != null ? row.text : null;
                    if (!string.IsNullOrEmpty(treasureName))
                        ScreenReader.Say(Loc.Get("Auto-selected treasure: ")
                            + StringHelpers.StripTags(treasureName) + ".");
                }
            }
        }

        /// <summary>
        /// Mirrors the per-toggle conditions in <c>UpdateCheckboxesExceptTreasure</c>.
        /// Returns a short reason or null when the lock can't be attributed.
        /// </summary>
        private static string WhyGoodsLocked(CaravanDialogController d, int idx)
        {
            // Mode-lock: any of the three exclusive paths suppress all goods.
            bool inTreasureMode = (d.sellTreasure != null && d.sellTreasure.isOn)
                               || (d.buyTreasure  != null && d.buyTreasure.isOn);
            bool inRouteMode    = d.establishRoute != null && d.establishRoute.isOn;
            if (inTreasureMode || inRouteMode)
                return Loc.Get("This is a treasure deal or trade route — cannot mix with goods. Turn off treasure or trade route first.");

            int otherClan = -1;
            try { otherClan = Game.ClanVariable("otherClan"); }
            catch (Exception ex) { DebugLogger.Error("CaravanNav.WhyLocked.otherClan", ex); }
            bool hasHorses = otherClan == -1;
            try { if (otherClan != -1) hasHorses = PluginImport.Clan_HasHorses(otherClan); }
            catch (Exception ex) { DebugLogger.Error("CaravanNav.WhyLocked.HasHorses", ex); }

            int commodity = idx / 2;
            bool buy = (idx % 2) == 0;
            switch ((Commodity)commodity)
            {
                case Commodity.Food:
                    return buy ? Loc.Get("We do not need to buy food right now.")
                               : Loc.Get("We cannot sell food right now.");
                case Commodity.Goods:
                    return buy ? null
                               : Loc.Get("We have fewer than 20 goods to sell.");
                case Commodity.Herds:
                    return buy ? null
                               : Loc.Get("We have 100 herds or fewer — too few to sell.");
                case Commodity.Horses:
                    if (!hasHorses) return Loc.Get("The recipient clan has no horses.");
                    if (!buy)
                    {
                        bool scant = false;
                        try { scant = PlayerClan.horsesAreScant; }
                        catch (Exception ex) { DebugLogger.Error("CaravanNav.WhyLocked.horsesAreScant", ex); }
                        if (scant) return Loc.Get("Our own horses are scant — cannot sell.");
                    }
                    return null;
                case Commodity.Treasure:
                    if (!buy) return Loc.Get("We have no treasures to sell.");
                    return null;
            }
            return null;
        }

        private static UIToggle GoodsToggleAt(CaravanDialogController d, int idx)
        {
            // Layout: even indices are Buy, odd indices are Sell. 0..1 Food,
            // 2..3 Goods, 4..5 Herds, 6..7 Horses, 8..9 Treasure.
            int commodity = idx / 2;
            bool buy = (idx % 2) == 0;
            switch ((Commodity)commodity)
            {
                case Commodity.Food:     return buy ? d.buyFood     : d.sellFood;
                case Commodity.Goods:    return buy ? d.buyGoods    : d.sellGoods;
                case Commodity.Herds:    return buy ? d.buyHerds    : d.sellHerds;
                case Commodity.Horses:   return buy ? d.buyHorses   : d.sellHorses;
                case Commodity.Treasure: return buy ? d.buyTreasure : d.sellTreasure;
                default: return null;
            }
        }

        /// <summary>
        /// Goods toggles in the prefab share the commodity name as their visible label
        /// ("Food", "Goods", "Herds", "Horses", "Treasure") — the buy/sell distinction
        /// is conveyed visually by being in the buy column or the sell column. Sighted
        /// players see two separate "Food" toggles side by side; without a Buy/Sell
        /// prefix a screen-reader user cannot tell which is which. The prefix follows
        /// the field-name distinction (<c>buyFood</c> vs <c>sellFood</c>) in
        /// <see cref="CaravanDialogController"/>, so it is not invented text.
        /// </summary>
        private static string GoodsLabelWithDirection(CaravanDialogController d, int idx)
        {
            UIToggle t = GoodsToggleAt(d, idx);
            string verb = ((idx % 2) == 0) ? Loc.Get("Buy") : Loc.Get("Sell");
            return verb + " " + GetToggleLabel(t);
        }

        /// <summary>
        /// Read the visible label of a UIToggle from its TextMeshPro label component
        /// (set in the Unity prefab). Falls back to the GameObject name when the label
        /// is missing or empty — never invents human-readable text.
        /// </summary>
        private static string GetToggleLabel(UIToggle t)
        {
            if (t == null) return "(missing)";
            try
            {
                if (t.label != null)
                {
                    string text = t.label.text;
                    if (!string.IsNullOrEmpty(text))
                        return StringHelpers.StripTags(text);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("CaravanNav.GetToggleLabel", ex);
            }
            return t.gameObject != null ? t.gameObject.name : "(unnamed)";
        }

        // ---------- Treasures zone ----------

        private void HandleTreasuresInput(CaravanDialogController d)
        {
            if (d.treasures == null || d.treasures.count == 0) return;

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _treasureIndex--;
                if (_treasureIndex < 0) _treasureIndex = d.treasures.count - 1;
                AnnounceCurrentTreasure(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _treasureIndex++;
                if (_treasureIndex >= d.treasures.count) _treasureIndex = 0;
                AnnounceCurrentTreasure(d);
                return;
            }
            // Space — select the focused treasure for the deal.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SelectCurrentTreasure(d);
                return;
            }
        }

        private void AnnounceCurrentTreasure(CaravanDialogController d)
        {
            if (d.treasures == null || d.treasures.count == 0)
            {
                ScreenReader.Say(Loc.Get("No treasures."));
                return;
            }
            if (_treasureIndex < 0) _treasureIndex = d.treasures.selectedIndex >= 0 ? d.treasures.selectedIndex : 0;
            if (_treasureIndex >= d.treasures.count) _treasureIndex = d.treasures.count - 1;

            var item = d.treasures[_treasureIndex];
            string label = item != null ? (item.text ?? item.gameObject.name) : Loc.Get("Unknown");
            string selected = (d.treasures.selectedIndex == _treasureIndex) ? Loc.Get(", selected") : "";
            ScreenReader.Say(StringHelpers.StripTags(label) + selected);
        }

        private void SelectCurrentTreasure(CaravanDialogController d)
        {
            if (d.treasures == null || _treasureIndex < 0 || _treasureIndex >= d.treasures.count) return;
            var item = d.treasures[_treasureIndex];
            if (item == null) return;
            d.treasures.OnItemClicked(item);
            ScreenReader.Say(Loc.Get("Treasure selected."));
        }

        // ---------- Escort zone ----------

        private void HandleEscortInput(CaravanDialogController d)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _escortIndex--;
                if (_escortIndex < 0) _escortIndex = EscortItemCount - 1;
                AnnounceCurrentEscort(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _escortIndex++;
                if (_escortIndex >= EscortItemCount) _escortIndex = 0;
                AnnounceCurrentEscort(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                AdjustEscort(d, -1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                AdjustEscort(d, 1);
                return;
            }
            // D — universal detail key: re-read the focused escort element.
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceCurrentEscort(d);
                return;
            }
        }

        private void AnnounceCurrentEscort(CaravanDialogController d)
        {
            if (_escortIndex < 0) _escortIndex = 0;
            switch ((EscortItem)_escortIndex)
            {
                case EscortItem.Elite:   AnnounceSlider(d.eliteSlider); break;
                case EscortItem.Regular: AnnounceSlider(d.regularSlider); break;
                case EscortItem.CaravanSize:
                    AnnounceCaravanSize(d);
                    break;
            }
        }

        private static void AnnounceSlider(UISlider s)
        {
            string label = GetSliderLabel(s);
            if (s == null || !s.gameObject.activeSelf)
            {
                ScreenReader.Say(label + Loc.Get(", not available"));
                return;
            }
            string locked = s.IsInteractable() ? "" : Loc.Get(", locked");
            ScreenReader.Say(label + ": " + s.intValue + Loc.Get(" of ") + (int)s.maxValue + locked
                + Loc.Get(". Left and Right to adjust, Shift for larger steps."));
        }

        private void AdjustEscort(CaravanDialogController d, int dir)
        {
            switch ((EscortItem)_escortIndex)
            {
                case EscortItem.Elite:   AdjustSlider(d.eliteSlider, dir); break;
                case EscortItem.Regular: AdjustSlider(d.regularSlider, dir); break;
                case EscortItem.CaravanSize:
                    if (d.caravanSize == null) return;
                    int v = d.caravanSize.value + dir;
                    if (v < 1) v = 3;
                    if (v > 3) v = 1;
                    d.caravanSize.value = v;
                    AnnounceCaravanSize(d);
                    break;
            }
        }

        private static void AdjustSlider(UISlider s, int dir)
        {
            if (s == null || !s.IsInteractable()) return;
            float step = s.wholeNumbers ? 1f : (s.maxValue - s.minValue) * 0.1f;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) step *= 5f;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                s.value = dir > 0 ? s.maxValue : s.minValue;
            else
                s.value = Mathf.Clamp(s.value + dir * step, s.minValue, s.maxValue);
            ScreenReader.Say(GetSliderLabel(s) + " " + s.intValue + Loc.Get(" of ") + (int)s.maxValue);
        }

        /// <summary>
        /// Speak the active radio toggle inside a UIToggleGroup (for caravanSize:
        /// the visual label of the currently-on toggle, set in the prefab — we never
        /// invent "small / medium / large").
        /// </summary>
        private static void AnnounceCaravanSize(CaravanDialogController d)
        {
            UIToggleGroup g = d.caravanSize;
            if (g == null) { ScreenReader.Say(Loc.Get("(toggle group not available)")); return; }

            UIToggle active = FindActiveToggleInGroup(g);
            if (active == null)
            {
                ScreenReader.Say(Loc.Get("None selected. Left and Right to change."));
                return;
            }
            ScreenReader.Say(GetToggleLabel(active) + Loc.Get(". Left and Right to change."));
        }

        private static UIToggle FindActiveToggleInGroup(UIToggleGroup g)
        {
            if (g == null) return null;
            try
            {
                var list = g.toggles;
                if (list == null) return null;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] != null && list[i].isOn) return list[i];
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("CaravanNav.FindActiveToggleInGroup", ex);
            }
            return null;
        }

        /// <summary>
        /// Read the visible label of a UISlider from its TextMeshPro label component
        /// (set in the Unity prefab). Falls back to the GameObject name when missing.
        /// </summary>
        private static string GetSliderLabel(UISlider s)
        {
            if (s == null) return "(missing)";
            try
            {
                if (s.label != null)
                {
                    string text = s.label.text;
                    if (!string.IsNullOrEmpty(text))
                        return StringHelpers.StripTags(text);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("CaravanNav.GetSliderLabel", ex);
            }
            return s.gameObject != null ? s.gameObject.name : "(unnamed)";
        }

        // ---------- Leader zone ----------

        private void HandleLeaderInput(CaravanDialogController d)
        {
            // Space opens the leader chooser (the focused element). Enter is
            // reserved globally for Send — see HandleInput.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (d.chooseLeaderButton != null && d.chooseLeaderButton.interactable)
                    SubmitButton(d.chooseLeaderButton);
                else
                    ScreenReader.Say(Loc.Get("Choose Leader is not available."));
                return;
            }
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnouncePersonInfo(d);
                return;
            }
        }

        /// <summary>
        /// Spoken when Tab lands on the leader zone: the currently-picked leader's
        /// name plus the next-action hint. Uses our own localized caption rather
        /// than the dialog's English-only <c>leaderCaption</c> property.
        /// </summary>
        private void AnnounceLeaderHeader(CaravanDialogController d)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Caravan leader. "));

            int li = GetLeaderIndex(d);
            if (li > 0)
                sb.Append(PluginImport.PC_PersonName(li))
                  .Append(Loc.Get(". Press Space to change. D for personal info."));
            else
                sb.Append(Loc.Get("No leader chosen. Press Space to pick one."));

            ScreenReader.Say(sb.ToString());
        }

        /// <summary>
        /// D-key handler in the leader zone. Reads the full attributed person info
        /// (name + family + deity + skills + age — same bitmask the ChooseLeader and
        /// Reorganize navigators use). Mirrors the D behavior on every other person
        /// list in the mod.
        /// </summary>
        private void AnnouncePersonInfo(CaravanDialogController d)
        {
            int li = GetLeaderIndex(d);
            if (li <= 0)
            {
                ScreenReader.Say(Loc.Get("No leader chosen. Press Space to pick one."));
                return;
            }
            try
            {
                Person person = new Person { index = li };
                // 95 = name + deity + skills + age + location + health. PersonBio is
                // a localized port of the game's English-only AttributedTextFor; the
                // health bit flags a wounded caravan leader as the worse pick.
                ScreenReader.Say(PersonBio.Localized(person, 95));
            }
            catch (Exception ex)
            {
                DebugLogger.Error("CaravanNav.AnnouncePersonInfo", ex);
                ScreenReader.Say(PluginImport.PC_PersonName(li));
            }
        }

        /// <summary>
        /// Read out the current trade-route partners. Pulls the same data the game's
        /// trade-route overlay uses: <c>ClanDataList(ClanFilterBy.filter_TradeWithUs)</c>
        /// — see <c>MapView.UpdateTradeRoutes</c>. The map view itself is purely
        /// visual; this hotkey gives blind users equivalent access to that overlay.
        /// </summary>
        private static void AnnounceTradeRoutes()
        {
            try
            {
                var list = new ClanDataList(ClanFilterBy.filter_TradeWithUs);
                int n = list.count;
                if (n == 0)
                {
                    ScreenReader.Say(Loc.Get("No active trade routes."));
                    return;
                }

                var sb = new StringBuilder();
                sb.Append(n).Append(n == 1 ? Loc.Get(" trade route: ") : Loc.Get(" trade routes: "));
                for (int i = 0; i < n; i++)
                {
                    Clan c = list[i];
                    if (c.isNull || string.IsNullOrEmpty(c.name)) continue;
                    if (i > 0) sb.Append(", ");
                    sb.Append(c.name);
                }
                sb.Append(".");
                ScreenReader.Say(sb.ToString());
            }
            catch (Exception ex)
            {
                DebugLogger.Error("CaravanNav.AnnounceTradeRoutes", ex);
                ScreenReader.Say(Loc.Get("Could not read trade routes."));
            }
        }

        // ---------- Primary action (Enter) and Close (Esc) ----------

        private void TrySend(CaravanDialogController d)
        {
            if (d.actionButton == null || !d.actionButton.interactable)
            {
                _confirmGate.Reset();
                ScreenReader.Say(WhySendDisabled(d));
                return;
            }
            if (_confirmGate.RequestOrConfirm(BuildCaravanSummary(d)))
                SubmitButton(d.actionButton);
        }

        /// <summary>
        /// Build the spoken summary for the two-step Enter confirmation. Names
        /// the recipient clan, the mode (trade vs. establish route), the leader,
        /// the warrior escort, and — in trade mode — what is being sold/bought.
        /// Treasure-sell includes the selected treasure's name so the user knows
        /// which specific item is leaving. Establish-route mode skips the
        /// sell/buy lists since no goods are exchanged on that path.
        /// </summary>
        private static string BuildCaravanSummary(CaravanDialogController d)
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
            int totalWarriors = elite + regular;

            bool routeMode = d.establishRoute != null && d.establishRoute.isOn;

            string template = routeMode
                ? Loc.Get("You send a caravan to {0} to establish a trade route, led by {1}")
                : Loc.Get("You send a caravan to {0} to trade, led by {1}");

            var sb = new StringBuilder();
            sb.Append(string.Format(template, clan, leader));
            sb.Append(string.Format(
                totalWarriors == 1 ? Loc.Get(", escorted by {0} warrior") : Loc.Get(", escorted by {0} warriors"),
                totalWarriors));

            if (!routeMode)
            {
                string sells = CollectActiveCommodities(d, buy: false);
                string buys  = CollectActiveCommodities(d, buy: true);
                if (sells.Length > 0)
                    sb.Append(string.Format(Loc.Get(", selling {0}"), sells));
                if (buys.Length > 0)
                    sb.Append(string.Format(Loc.Get(", buying {0}"), buys));
            }

            sb.Append('.');
            return sb.ToString();
        }

        /// <summary>
        /// Join the active commodity toggles for one direction into a localised
        /// "Food, Goods and Herds" phrase. For Treasure-sell the row text from
        /// the treasures list is substituted in place of the bare word "Treasure"
        /// so the summary names the specific item being parted with.
        /// </summary>
        private static string CollectActiveCommodities(CaravanDialogController d, bool buy)
        {
            var list = new System.Collections.Generic.List<string>(5);
            // Food / Goods / Herds / Horses use the toggle's own label text
            // (already localised by the game), so the summary stays in the
            // active language without us hard-coding commodity names.
            if (IsToggleOn(buy ? d.buyFood   : d.sellFood))   list.Add(GetToggleLabel(buy ? d.buyFood   : d.sellFood));
            if (IsToggleOn(buy ? d.buyGoods  : d.sellGoods))  list.Add(GetToggleLabel(buy ? d.buyGoods  : d.sellGoods));
            if (IsToggleOn(buy ? d.buyHerds  : d.sellHerds))  list.Add(GetToggleLabel(buy ? d.buyHerds  : d.sellHerds));
            if (IsToggleOn(buy ? d.buyHorses : d.sellHorses)) list.Add(GetToggleLabel(buy ? d.buyHorses : d.sellHorses));
            // Treasure-sell names the chosen treasure (the buy-side has no list,
            // the partner picks the item, so it stays as just "Treasure").
            UIToggle treasureToggle = buy ? d.buyTreasure : d.sellTreasure;
            if (IsToggleOn(treasureToggle))
            {
                string treasureName = null;
                if (!buy && d.treasures != null && d.treasures.selectedIndex >= 0
                    && d.treasures.selectedIndex < d.treasures.count)
                {
                    UIListItem row = d.treasures[d.treasures.selectedIndex];
                    if (row != null && !string.IsNullOrEmpty(row.text))
                        treasureName = StringHelpers.StripTags(row.text);
                }
                list.Add(string.IsNullOrEmpty(treasureName)
                    ? GetToggleLabel(treasureToggle)
                    : treasureName);
            }
            if (list.Count == 0) return string.Empty;
            return StringHelpers.JoinList(list, Loc.Get("and"));
        }

        private static bool IsToggleOn(UIToggle t)
        {
            return t != null && t.isOn && t.gameObject.activeSelf;
        }

        /// <summary>
        /// Mirrors <c>CaravanDialogController.ValidateSendButton</c>: the send button
        /// goes interactive when leader+warriors+clan are picked AND ONE of three
        /// goal paths is satisfied (direct trade with both sell+buy, treasure trade,
        /// or establish route). Returns the first missing piece — exactly the prereq
        /// that's blocking, never an apology message that doesn't apply.
        /// </summary>
        private static string WhySendDisabled(CaravanDialogController d)
        {
            int otherClan = -1;
            try { otherClan = Game.ClanVariable("otherClan"); }
            catch (Exception ex) { DebugLogger.Error("CaravanNav.WhySend.otherClan", ex); }

            if (otherClan == -1) return Loc.Get("Send is disabled. Pick a recipient clan first.");

            if (GetLeaderIndex(d) <= 0) return Loc.Get("Send is disabled. Pick a leader first.");

            int warriors = 0;
            if (d.eliteSlider != null) warriors += d.eliteSlider.intValue;
            if (d.regularSlider != null) warriors += d.regularSlider.intValue;
            if (warriors <= 1)
                return Loc.Get("Send is disabled. Set warrior escort first — at least two warriors needed in total.");

            // Goal-path check matches the original boolean precisely.
            bool hasHorses = false;
            try { hasHorses = otherClan == -1 || PluginImport.Clan_HasHorses(otherClan); }
            catch (Exception ex) { DebugLogger.Error("CaravanNav.WhySend.HasHorses", ex); }

            bool sellSomething = (d.sellHerds  != null && d.sellHerds.isOn)
                              || (d.sellFood   != null && d.sellFood.isOn)
                              || (d.sellGoods  != null && d.sellGoods.isOn)
                              || (hasHorses && d.sellHorses != null && d.sellHorses.isOn);
            bool buySomething  = (d.buyHerds   != null && d.buyHerds.isOn)
                              || (d.buyFood    != null && d.buyFood.isOn)
                              || (d.buyGoods   != null && d.buyGoods.isOn)
                              || (hasHorses && d.buyHorses != null && d.buyHorses.isOn);
            bool treasurePath  = (d.sellTreasure != null && d.sellTreasure.isOn
                                  && d.treasures != null && d.treasures.selectedIndex != -1)
                              || (d.buyTreasure  != null && d.buyTreasure.isOn);
            bool routePath     = d.establishRoute != null && d.establishRoute.isOn;

            if (routePath || treasurePath || (sellSomething && buySomething))
                return Loc.Get("Send is disabled."); // Should not be reached — actionButton would be interactable.

            // Direct trade started but incomplete — guide to the missing side.
            if (sellSomething && !buySomething)
                return Loc.Get("Send is disabled. Pick something to buy from the goods zone, or switch to a treasure deal or trade route.");
            if (buySomething && !sellSomething)
                return Loc.Get("Send is disabled. Pick something to sell from the goods zone, or switch to a treasure deal or trade route.");

            // Sell-treasure on but no treasure picked.
            if (d.sellTreasure != null && d.sellTreasure.isOn
                && d.treasures != null && d.treasures.selectedIndex == -1)
                return Loc.Get("Send is disabled. Pick a treasure from the treasure list, or switch to a different deal.");

            // Nothing chosen at all.
            bool routeAvailable = d.establishRoute != null && d.establishRoute.interactable;
            if (routeAvailable)
                return Loc.Get("Send is disabled. Pick a deal: a sell and a buy commodity, a treasure trade, or establish a trade route.");
            return Loc.Get("Send is disabled. Pick a deal: a sell and a buy commodity, or a treasure trade.");
        }

        private static void TryClose(CaravanDialogController d)
        {
            var cb = FindButtonByName(d, "CloseButton") ?? FindButtonByName(d, "CloseButton2");
            if (cb != null) SubmitButton(cb);
            else ScreenReader.Say(Loc.Get("Close button not found."));
        }

        // ---------- F5 status ----------

        public void AnnounceFullStatus(CaravanDialogController d)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Caravan dialog. "));

            int otherClan = Game.ClanVariable("otherClan");
            if (otherClan > 0)
                sb.Append(Loc.Get("Recipient: ")).Append(Clan.ClanWithIndex(otherClan).name).Append(". ");
            else sb.Append(Loc.Get("No recipient. "));

            if (d.establishRoute != null && d.establishRoute.isOn)
                sb.Append(GetToggleLabel(d.establishRoute)).Append(Loc.Get(" on. "));
            else
            {
                if (d.sellBuy != null)
                    sb.Append(GetToggleLabel(d.sellBuy)).Append(d.sellBuy.isOn ? Loc.Get(" on. ") : Loc.Get(" off. "));
                AppendActiveGoods(sb, d);
            }

            int li = GetLeaderIndex(d);
            if (li > 0) sb.Append(Loc.Get("Leader: ")).Append(PluginImport.PC_PersonName(li)).Append(". ");
            else sb.Append(Loc.Get("No leader. "));

            if (d.eliteSlider != null)
                sb.Append(GetSliderLabel(d.eliteSlider)).Append(": ").Append(d.eliteSlider.intValue).Append(", ");
            if (d.regularSlider != null)
                sb.Append(GetSliderLabel(d.regularSlider)).Append(": ").Append(d.regularSlider.intValue).Append(". ");
            if (d.caravanSize != null)
            {
                UIToggle activeSize = FindActiveToggleInGroup(d.caravanSize);
                if (activeSize != null)
                    sb.Append(GetToggleLabel(activeSize)).Append(". ");
            }

            sb.Append(IsActionEnabled(d)
                ? Loc.Get("Press Enter to send. Escape to leave.")
                : WhySendDisabled(d));
            ScreenReader.Say(sb.ToString());
        }

        private static void AppendActiveGoods(StringBuilder sb, CaravanDialogController d)
        {
            int active = 0;
            AppendIfOn(sb, d.buyFood,      Loc.Get("Buy"),  ref active);
            AppendIfOn(sb, d.sellFood,     Loc.Get("Sell"), ref active);
            AppendIfOn(sb, d.buyGoods,     Loc.Get("Buy"),  ref active);
            AppendIfOn(sb, d.sellGoods,    Loc.Get("Sell"), ref active);
            AppendIfOn(sb, d.buyHerds,     Loc.Get("Buy"),  ref active);
            AppendIfOn(sb, d.sellHerds,    Loc.Get("Sell"), ref active);
            AppendIfOn(sb, d.buyHorses,    Loc.Get("Buy"),  ref active);
            AppendIfOn(sb, d.sellHorses,   Loc.Get("Sell"), ref active);
            AppendIfOn(sb, d.buyTreasure,  Loc.Get("Buy"),  ref active);
            AppendIfOn(sb, d.sellTreasure, Loc.Get("Sell"), ref active);
            if (active == 0) sb.Append(Loc.Get("no goods chosen. "));
        }

        private static void AppendIfOn(StringBuilder sb, UIToggle t, string verb, ref int active)
        {
            if (t == null || !t.isOn) return;
            sb.Append(verb).Append(" ").Append(GetToggleLabel(t)).Append(", ");
            active++;
        }

        // ---------- Helpers ----------

        private static int GetLeaderIndex(CaravanDialogController d)
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
                DebugLogger.Error("CaravanNav.GetLeaderIndex", ex);
                return 0;
            }
        }

        private static bool IsActionEnabled(CaravanDialogController d)
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

        private static UIButton FindButtonByName(CaravanDialogController d, string name)
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
