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
    /// Zone-based keyboard navigation for the MapScreenController (Foray / Exploration).
    ///
    /// The screen is the project's hardest accessibility surface: the primary input
    /// is mouse-driven hex tapping on a visual map. Without a text-based replacement
    /// for the destination cursor, blind users can only run the default home-explore
    /// mission and never reach goals like Forage, Capture Horses, Search for Spirits,
    /// or destinations beyond their own lands. This navigator wraps every interactive
    /// region of the screen — the goal list, escort sliders, leader picker, action
    /// buttons — and adds a synthesised "Destination" zone backed by the known-clans
    /// data list, with each entry calling MapTapped on the corresponding clan center.
    ///
    /// Layout (Tab cycles in this order):
    ///   Goals       — UIList of 6 mission goals (Explore / Forage / CaptureHorses /
    ///                 SearchForRegalia / SearchForWeddingTreasure / SearchForSpirits)
    ///   Sliders     — eliteSlider (Swords) + regularSlider (Bows)
    ///   Leader      — chooseLeaderButton; D reads the current leader, Space opens
    ///                 the ChooseLeader sub-picker (handled by ChooseLeaderNavigator)
    ///   Destination — synthetic list: "Home" + every known clan + dunelands /
    ///                 chaos / gorped territory / wildlands tagged so spell-casting
    ///                 missions can target uninhabited zones too
    ///   HexCursor   — fine-grained hex selection for unnamed targets
    ///
    /// Keys follow the unified Model Y scheme: Space acts on the focused element
    /// (select a goal, open the leader picker, set a destination), arrows browse
    /// and adjust sliders, D reads details, a blank Enter sends the mission, Esc
    /// cancels back to the map. X opens the foray panel (Explore).
    /// </summary>
    public class MapScreenNavigator
    {
        private enum Zone { Goals, Sliders, Leader, Destination, HexCursor }
        private enum SliderId { Swords = 0, Bows = 1 }
        private enum FilterCategory { All, Clans, Tribe, Feuds, Landmarks }
        private enum SortMode { Default, ByDistance, Alphabetical }
        private const int ZoneCount = 5;
        private const int SliderCount = 2;

        // Hex-cursor POI filter — categories of "interesting things" the user can
        // cycle through with PageUp/PageDown (entry) and Ctrl+PageUp/PageDown
        // (category). Order is "most-frequently-useful first": own tribe is the
        // top hit at the start of every turn, then known neighbours, then
        // hostility-relevant clans, then geography and special hexes.
        private enum PoiCategory
        {
            AllPois = 0,
            OurTribe = 1,
            KnownClans = 2,
            Feuds = 3,
            TradePartners = 4,
            ChaosAndGorp = 5,
            WildAndDunes = 6,
            NamedZones = 7,
            ActiveMissions = 8,
            ExplorationFrontier = 9,
            DeadHexes = 10,
            MentionedHexes = 11,
        }
        private const int PoiCategoryCount = 12;

        private Zone _zone = Zone.Goals;
        private int _goalIndex = -1;
        private int _sliderIndex = -1;
        private int _destinationIndex = -1;
        private readonly ConfirmGate _confirmGate = new ConfirmGate();

        // Hex cursor state (separate zone). row/col index into the MapHex grid; -1
        // means "uninitialised" — first arrow press will sync from the live
        // explorationCursor.position so the user picks up where the game left off.
        private int _hexRow = -1;
        private int _hexCol = -1;

        // Filter + sort state for the destination list. Filter narrows the visible
        // entries (e.g. only tribe members); sort reorders them. Both default to a
        // permissive setting so a fresh map open shows everything.
        private FilterCategory _filter = FilterCategory.All;
        private SortMode _sort = SortMode.Default;

        // Two destination lists: _allDestinations is the raw collection (built once
        // per screen open from clan + map-zone APIs), _displayDestinations is the
        // filtered + sorted view that the user navigates. Splitting them means F/S
        // hotkeys re-derive the view in O(N) without re-querying the game APIs.
        private readonly List<DestinationEntry> _allDestinations = new List<DestinationEntry>();
        private readonly List<DestinationEntry> _displayDestinations = new List<DestinationEntry>();

        // POI catalogue for the hex-cursor zone. One list per category, built
        // lazily on first PgUp/PgDn use and held until ResetForNewScreen wipes
        // it. _poiCategory tracks which category is active; _poiIndex tracks the
        // current entry within that category. Indices reset to 0 on category
        // switch so the user always starts at the nearest entry.
        private readonly List<HexPoi>[] _pois = new List<HexPoi>[PoiCategoryCount];
        private bool _poisBuilt;
        private PoiCategory _poiCategory = PoiCategory.OurTribe;
        private int _poiIndex = -1;

        private struct HexPoi
        {
            public int Row;
            public int Col;
            public string Label;     // optional short label (mission type, "Burnpeak", etc.)
            public int CachedMiles;  // distance from player clan, -1 if unknown
        }

        private struct DestinationEntry
        {
            public string Name;
            public Vector2Int Center;
            public int ClanIndex; // -1 for non-clan zones (wildlands, rivers, etc.)
            public string Tag;    // "home", "tribe", "known", "wild", "zone", "river", ...
            // Set during ApplyFilterAndSort so the announcement can include miles
            // without recomputing per arrow press.
            public int CachedMiles;
        }

        public void ResetForNewScreen()
        {
            _zone = Zone.Goals;
            _goalIndex = -1;
            _sliderIndex = -1;
            _destinationIndex = -1;
            _hexRow = -1;
            _hexCol = -1;
            _filter = FilterCategory.All;
            _sort = SortMode.Default;
            _allDestinations.Clear();
            _displayDestinations.Clear();
            for (int i = 0; i < _pois.Length; i++) _pois[i] = null;
            _poisBuilt = false;
            _poiCategory = PoiCategory.OurTribe;
            _poiIndex = -1;
            _confirmGate.Reset();
        }

        /// <summary>Compact opening announcement — covers the keys the user needs to know
        /// about (Tab zones, G goal, K destination, F5 full status) without dumping the
        /// whole status. Queued behind any tutorial hint that's already speaking.
        ///
        /// Side effect: auto-opens the foray panel if it isn't already visible. The
        /// original game gates EVERY foray-mode operation behind a click on the
        /// EXPLORE button — without it, MapScreenController.MapTapped early-returns on
        /// `!forayGroup.isVisible`, so cursor moves never land, explorationMoved stays
        /// false, sliders aren't LinkTo-capped to horses-1, and ValidateSendButton runs
        /// on stale state. Our entire navigator (Goals / Sliders / Leader / Destination
        /// / HexCursor) is foray-mode only, so opening on entry matches user intent and
        /// removes a class of "looks like nothing happened" bugs.</summary>
        public void AnnounceOpening(MapScreenController s)
        {
            if (s == null) return;
            try
            {
                EnsureForayPanelOpen(s);

                var sb = new StringBuilder();
                sb.Append(Loc.Get("Map. Mission planning. "));
                int leader = GetLeaderIndex(s);
                if (leader > 0)
                {
                    string name = PluginImport.PC_PersonName(leader);
                    if (!string.IsNullOrEmpty(name)) sb.Append(Loc.Get("Leader: ")).Append(name).Append(". ");
                }
                if (s.goalList != null && s.goalList.selectedItem != null)
                    sb.Append(Loc.Get("Goal: ")).Append(SafeText(s.goalList.selectedItem)).Append(". ");

                AppendMapOverlayNotes(sb);

                sb.Append(Loc.Get("Tab cycles zones: goals, sliders, leader, destination list, hex cursor. "));
                sb.Append(Loc.Get("G jumps to goals, K to destination list. "));
                sb.Append(Loc.Get("In the list: F filters, O orders. "));
                sb.Append(Loc.Get("Space selects or sets the focused element, Enter sends, Escape cancels. F5 reads full status."));
                ScreenReader.Say(sb.ToString(), interrupt: false);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.AnnounceOpening", ex);
            }
        }

        /// <summary>
        /// Append the world-state overlays that sighted players see directly on
        /// the map but that have no text label: meteor crater, starfall impact,
        /// and the visible-but-unfound glacier. MapView.UpdateSymbolsAndLabels
        /// (decompiled MapView.cs:397-403) and UpdateGlacier (cs:381-391) read
        /// the same three Game booleans; we mirror them so the user hears about
        /// these landscape events on Map entry rather than only discovering
        /// them by stumbling across the symbols with the hex cursor.
        /// </summary>
        private static void AppendMapOverlayNotes(StringBuilder sb)
        {
            try
            {
                bool crater = false;
                bool starfall = false;
                bool foundGlacier = true; // safer default: don't announce if read fails
                try { crater = Game.BooleanVariable("showCrater"); } catch { }
                try { starfall = Game.BooleanVariable("starfall"); } catch { }
                try { foundGlacier = Game.BooleanVariable("foundGlacier"); } catch { }

                if (crater) sb.Append(Loc.Get("A meteor crater is visible on the map. "));
                if (starfall) sb.Append(Loc.Get("A starfall impact is visible on the map. "));
                // The glacier overlay is shown WHILE the glacier remains unfound
                // (see MapView.UpdateGlacier: SetActive when !foundGlacier). Once
                // the player explores it, the overlay disappears and the location
                // is just part of the normal map data.
                if (!foundGlacier) sb.Append(Loc.Get("A glacier is visible to the north, not yet explored. "));
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.AppendMapOverlayNotes", ex);
            }
        }

        /// <summary>Open the foray panel if it isn't already visible. Idempotent — the
        /// `isVisible` check skips re-opening on revisits (e.g., return from
        /// ChooseLeaderDialog) so we don't replay the ExplorationUp sound or reset the
        /// cursor the user already moved.</summary>
        private static void EnsureForayPanelOpen(MapScreenController s)
        {
            try
            {
                if (s.forayGroup == null) return;
                if (s.forayGroup.isVisible) return;
                s.ShowForayDialog();
                DebugLogger.Log("MapScreenNav", "EnsureForayPanelOpen: opened foray panel");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.EnsureForayPanelOpen", ex);
            }
        }

        public void HandleInput(MapScreenController s)
        {
            if (s == null) return;

            // Enter — Model Y: a blank Enter sends the mission, the one key that
            // completes the screen. Handled first so it works from every zone; a
            // held Ctrl is neither needed nor blocked.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TrySend(s);
                return;
            }

            // Escape — cancel and return to the map.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TryClose(s);
                return;
            }

            // Tab / Shift+Tab — cycle zones.
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _confirmGate.Reset();
                int dir = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? -1 : 1;
                CycleZone(dir);
                AnnounceZone(s);
                return;
            }

            // F5 — full status.
            if (Input.GetKeyDown(KeyCode.F5))
            {
                AnnounceFullStatus(s);
                return;
            }

            // Direct-jump hotkeys — let the user skip straight to a zone instead of
            // chaining Tabs. Mirrors RelationsScreen's F (filter) / E (emissary)
            // shortcut convention.
            if (!HasCtrlAlt())
            {
                if (Input.GetKeyDown(KeyCode.G) && !AnyShift())
                {
                    _confirmGate.Reset();
                    _zone = Zone.Goals;
                    AnnounceZone(s);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.K) && !AnyShift())
                {
                    _confirmGate.Reset();
                    _zone = Zone.Destination;
                    EnsureDestinationsBuilt(s);
                    AnnounceZone(s);
                    return;
                }
                // X — open the foray (Explore) panel. Replaces the old Action-zone
                // Explore button. Letter X chosen because E is taken by RelationsScreen
                // for Emissary; X is unused on every other screen.
                if (Input.GetKeyDown(KeyCode.X) && !AnyShift())
                {
                    _confirmGate.Reset();
                    TryExplore(s);
                    return;
                }
            }

            // Any zone-specific mutator key drops a pending Enter confirmation.
            // Tab/G/K/F5/Enter/Escape have already returned above; D/F/O per zone
            // are read-only or filter ops handled inside the zone — they don't
            // appear here, so they preserve the pending state if applicable.
            if (_confirmGate.IsPending
                && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)
                 || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)
                 || Input.GetKeyDown(KeyCode.Space)))
                _confirmGate.Reset();

            switch (_zone)
            {
                case Zone.Goals:       HandleGoalsInput(s);       break;
                case Zone.Sliders:     HandleSlidersInput(s);     break;
                case Zone.Leader:      HandleLeaderInput(s);      break;
                case Zone.Destination: HandleDestinationInput(s); break;
                case Zone.HexCursor:   HandleHexCursorInput(s);   break;
            }
        }

        // ---------- Primary action (Enter), Explore (X), Close (Esc) ----------

        private void TrySend(MapScreenController s)
        {
            if (s.actionButton == null || !s.actionButton.interactable)
            {
                _confirmGate.Reset();
                ScreenReader.Say(Loc.Get("Send is not available yet. ") + WhySendDisabled(s));
                return;
            }
            if (_confirmGate.RequestOrConfirm(BuildExpeditionSummary(s)))
                SubmitButton(s.actionButton);
        }

        /// <summary>
        /// Build the spoken summary for the two-step Enter confirmation. Names the
        /// goal, destination (whatever the user picked — clan target or hex location),
        /// leader, and warrior strength. Goal + destination are the two main
        /// decisions; warriors qualify the commitment.
        /// </summary>
        private string BuildExpeditionSummary(MapScreenController s)
        {
            string goal = (s.goalList != null && s.goalList.selectedItem != null)
                ? SafeText(s.goalList.selectedItem)
                : Loc.Get("no goal");

            string destination = DescribeCurrentDestination(s);

            int leaderIdx = GetLeaderIndex(s);
            string leader = leaderIdx > 0 ? PluginImport.PC_PersonName(leaderIdx) : Loc.Get("no leader");

            int swords = s.eliteSlider != null ? s.eliteSlider.intValue : 0;
            int bows = s.regularSlider != null ? s.regularSlider.intValue : 0;
            int totalWarriors = swords + bows;

            var sb = new StringBuilder();
            sb.Append(string.Format(Loc.Get("You send an expedition to {0}"), destination));
            sb.Append(string.Format(Loc.Get(", goal {0}"), goal));
            sb.Append(string.Format(Loc.Get(", led by {0}"), leader));
            sb.Append(string.Format(
                totalWarriors == 1 ? Loc.Get(", with {0} warrior") : Loc.Get(", with {0} warriors"),
                totalWarriors));
            sb.Append('.');

            // AtHome downgrade prediction — Foray() silently switches to the
            // goal_*AtHome variant when the cursor never moved, our own clan is
            // selected, or the cursor sits inside our territory. Without this
            // note the user can confirm a mission expecting Forage-abroad and
            // get Forage-at-home instead.
            string downgrade = GetAtHomeDowngradeReason(s);
            if (downgrade != null)
                sb.Append(Loc.Get(" Note: ")).Append(downgrade)
                  .Append(Loc.Get(" — mission will resolve as foray at home."));

            return sb.ToString();
        }

        /// <summary>
        /// Describe the current destination by reading live game state — the
        /// exploration cursor + mapAnnotations.selectedClan — rather than the
        /// navigator's _destinationIndex. The earlier _destinationIndex path
        /// went stale in several common situations: a Hex Cursor Space on an
        /// unnamed hex left an old list entry pointed at, a mouse click never
        /// updated the index at all, and a MapTapped rejection kept the index
        /// on the rejected entry. The earlier Game.ClanVariable("targetClan")
        /// lookup was dead — neither SA2 nor RLTW ever sets that variable
        /// (verified across both decompiles). Result of the bug: the Enter-
        /// confirmation summary named the wrong destination, or said "no
        /// destination" when one was in fact set.
        /// </summary>
        private string DescribeCurrentDestination(MapScreenController s)
        {
            try
            {
                if (!GetExplorationMoved(s))
                    return Loc.Get("no destination chosen yet");

                int sel = SafeSelectedClan(s);
                if (sel > 0 && sel != PlayerClan.index)
                {
                    Clan c = Clan.ClanWithIndex(sel);
                    if (!c.isNull) return c.name;
                }
                if (sel == PlayerClan.index)
                    return Loc.Get("home");

                Vector2 cursor = GetCursorPosition(s);
                MapZone zone = WorldMap.ZoneAtPoint(cursor);
                if (!zone.isNull)
                {
                    string label = zone.label;
                    if (!string.IsNullOrEmpty(label)) return label;
                    string name = zone.name;
                    if (!string.IsNullOrEmpty(name) && !IsInternalZoneCode(name)) return name;
                    string desc = DescribeWildZone(zone);
                    if (!string.IsNullOrEmpty(desc)) return desc;
                }

                try
                {
                    HexGrid hex = MapHex.HexAtPoint(cursor);
                    return string.Format(Loc.Get("hex {0}, {1}"), hex.row + 1, hex.column + 1);
                }
                catch { }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.DescribeCurrentDestination", ex);
            }
            return Loc.Get("no destination");
        }

        private static void TryExplore(MapScreenController s)
        {
            try { s.ShowForayDialog(); }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.ShowForayDialog", ex);
                ScreenReader.Say(Loc.Get("Could not open the explore panel."));
            }
        }

        private static void TryClose(MapScreenController s)
        {
            try { s.CancelDialog(); }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.CancelDialog", ex);
                ScreenReader.Say(Loc.Get("Could not close the dialog."));
            }
        }

        // ---------- Zone selection ----------

        private void CycleZone(int dir)
        {
            int z = (int)_zone + dir;
            if (z < 0) z = ZoneCount - 1;
            if (z >= ZoneCount) z = 0;
            _zone = (Zone)z;
        }

        private void AnnounceZone(MapScreenController s)
        {
            switch (_zone)
            {
                case Zone.Goals:
                    ScreenReader.Say(Loc.Get("Mission goal."));
                    AnnounceCurrentGoal(s);
                    break;
                case Zone.Sliders:
                    ScreenReader.Say(Loc.Get("Escort."));
                    AnnounceCurrentSlider(s);
                    break;
                case Zone.Leader:
                    ScreenReader.Say(Loc.Get("Leader."));
                    AnnounceLeader(s);
                    break;
                case Zone.Destination:
                    ScreenReader.Say(Loc.Get("Destination list. Filter ") + FilterName(_filter)
                                     + Loc.Get(", sort ") + SortName(_sort)
                                     + Loc.Get(". F filters, O orders."));
                    AnnounceCurrentDestination(s);
                    break;
                case Zone.HexCursor:
                    ScreenReader.Say(Loc.Get("Hex cursor. Arrows move one hex, Shift+arrow five hexes. Page Up and Down jump between points of interest, Ctrl with them switches category. Home jumps to your clan, D describes, Space sets destination."));
                    SyncHexFromCursor(s);
                    AnnounceCurrentHex(s);
                    break;
            }
        }

        private static string FilterName(FilterCategory f)
        {
            switch (f)
            {
                case FilterCategory.All:       return Loc.Get("all");
                case FilterCategory.Clans:     return Loc.Get("clans only");
                case FilterCategory.Tribe:     return Loc.Get("our tribe");
                case FilterCategory.Feuds:     return Loc.Get("feuds");
                case FilterCategory.Landmarks: return Loc.Get("landmarks");
                default: return f.ToString();
            }
        }

        private static string SortName(SortMode m)
        {
            switch (m)
            {
                case SortMode.Default:      return Loc.Get("default");
                case SortMode.ByDistance:   return Loc.Get("by distance");
                case SortMode.Alphabetical: return Loc.Get("alphabetical");
                default: return m.ToString();
            }
        }

        // ---------- Goals zone ----------

        private void HandleGoalsInput(MapScreenController s)
        {
            UIList list = s.goalList;
            if (list == null || list.count == 0) return;

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _goalIndex = (_goalIndex < 0) ? list.selectedIndex : _goalIndex;
                _goalIndex--;
                if (_goalIndex < 0) _goalIndex = list.count - 1;
                AnnounceCurrentGoal(s);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _goalIndex = (_goalIndex < 0) ? list.selectedIndex : _goalIndex;
                _goalIndex++;
                if (_goalIndex >= list.count) _goalIndex = 0;
                AnnounceCurrentGoal(s);
                return;
            }
            // Space — Model Y: select the focused goal. Enter is reserved
            // globally for Send.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ActivateCurrentGoal(s);
                return;
            }
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceGoalDescription(s, false);
                return;
            }
        }

        private void AnnounceCurrentGoal(MapScreenController s)
        {
            UIList list = s.goalList;
            if (list == null || list.count == 0)
            {
                ScreenReader.Say(Loc.Get("No goals available."));
                return;
            }
            int idx = (_goalIndex >= 0) ? _goalIndex : list.selectedIndex;
            if (idx < 0 || idx >= list.count) idx = 0;

            string goalName = SafeText(list[idx]);
            string marker = (idx == list.selectedIndex) ? Loc.Get(", currently selected") : "";
            ScreenReader.Say(goalName + marker + Loc.Get(". Press D for description."));
        }

        private void ActivateCurrentGoal(MapScreenController s)
        {
            UIList list = s.goalList;
            if (list == null || list.count == 0) return;
            int idx = (_goalIndex >= 0) ? _goalIndex : list.selectedIndex;
            if (idx < 0 || idx >= list.count) return;

            UIListItem item = list[idx];
            if (item == null) return;

            // OnItemClicked fires onItemClicked → MapScreenController.OnItemSelected,
            // which sets the goal, recomputes the leader for the new skill, validates
            // the SEND button, and shows the goal description tooltip.
            list.OnItemClicked(item);
            string goalName = SafeText(item);
            ScreenReader.Say(goalName + Loc.Get(" selected."));
            // The game routes the description through DescriptionBox.Show → our
            // TooltipPatches postfix speaks it; no need to repeat it here.
        }

        private void AnnounceGoalDescription(MapScreenController s, bool prefixGoalName)
        {
            UIList list = s.goalList;
            if (list == null || list.count == 0) return;
            int idx = (_goalIndex >= 0) ? _goalIndex : list.selectedIndex;
            if (idx < 0 || idx >= list.count) return;

            UIListItem item = list[idx];
            if (item == null) return;
            Goal g = (Goal)item.key;
            string desc = s.ExplanationForGoal(g);
            if (string.IsNullOrEmpty(desc))
            {
                ScreenReader.Say(Loc.Get("No description for this goal."));
                return;
            }
            if (prefixGoalName) desc = SafeText(item) + ": " + desc;
            ScreenReader.Say(desc);
        }

        // ---------- Sliders zone ----------

        private void HandleSlidersInput(MapScreenController s)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _sliderIndex = (_sliderIndex < 0) ? 0 : _sliderIndex - 1;
                if (_sliderIndex < 0) _sliderIndex = SliderCount - 1;
                AnnounceCurrentSlider(s);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _sliderIndex = (_sliderIndex < 0) ? 0 : _sliderIndex + 1;
                if (_sliderIndex >= SliderCount) _sliderIndex = 0;
                AnnounceCurrentSlider(s);
                return;
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (_sliderIndex < 0) _sliderIndex = 0;
                int dir = Input.GetKeyDown(KeyCode.RightArrow) ? 1 : -1;
                AdjustSlider(s, dir);
                return;
            }
        }

        private UISlider GetSlider(MapScreenController s, int idx)
        {
            switch ((SliderId)idx)
            {
                case SliderId.Swords: return s.eliteSlider;
                case SliderId.Bows:   return s.regularSlider;
                default: return null;
            }
        }

        private static string SliderLabel(SliderId id)
        {
            return (id == SliderId.Swords) ? Loc.Get("Swords") : Loc.Get("Bows");
        }

        private void AnnounceCurrentSlider(MapScreenController s)
        {
            int idx = (_sliderIndex < 0) ? 0 : _sliderIndex;
            UISlider slider = GetSlider(s, idx);
            if (slider == null)
            {
                ScreenReader.Say(Loc.Get("Slider not available."));
                return;
            }
            string label = SliderLabel((SliderId)idx);
            ScreenReader.Say(label + ". " + (int)slider.value + Loc.Get(" of ") + (int)slider.maxValue
                             + Loc.Get(". Left and Right to change."));
        }

        private void AdjustSlider(MapScreenController s, int dir)
        {
            UISlider slider = GetSlider(s, _sliderIndex);
            if (slider == null || !slider.IsInteractable()) return;

            float step = slider.wholeNumbers ? 1f : (slider.maxValue - slider.minValue) * 0.1f;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) step *= 5f;

            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                slider.value = (dir > 0) ? slider.maxValue : slider.minValue;
            else
                slider.value = Mathf.Clamp(slider.value + dir * step, slider.minValue, slider.maxValue);

            string label = SliderLabel((SliderId)_sliderIndex);
            ScreenReader.Say(label + " " + (int)slider.value + Loc.Get(" of ") + (int)slider.maxValue);
        }

        // ---------- Leader zone ----------

        private void HandleLeaderInput(MapScreenController s)
        {
            // Space — Model Y: open the leader picker (the focused element).
            // Enter is reserved globally for Send.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (s.chooseLeaderButton == null || !s.chooseLeaderButton.interactable)
                {
                    ScreenReader.Say(Loc.Get("Choose Leader is not available right now."));
                    return;
                }
                SubmitButton(s.chooseLeaderButton);
                // ChooseLeaderNavigator takes over once ChooseLeaderDialog opens.
                return;
            }
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceLeaderInfo(s);
                return;
            }
        }

        private int GetLeaderIndex(MapScreenController s)
        {
            // ManagementDialogController.leaderIndex is protected; reflection is the
            // only stable way to read the live value across screen transitions.
            try
            {
                FieldInfo f = typeof(ManagementDialogController).GetField(
                    "leaderIndex", BindingFlags.NonPublic | BindingFlags.Instance);
                if ((object)f == null) return -1;
                object v = f.GetValue(s);
                return (v is int i) ? i : -1;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.GetLeaderIndex", ex);
                return -1;
            }
        }

        private void AnnounceLeader(MapScreenController s)
        {
            int idx = GetLeaderIndex(s);
            if (idx <= 0)
            {
                ScreenReader.Say(Loc.Get("No leader chosen yet. Press Space to choose, D for details."));
                return;
            }
            string name = PluginImport.PC_PersonName(idx);
            if (string.IsNullOrEmpty(name)) name = Loc.Get("Person ") + idx;
            ScreenReader.Say(Loc.Get("Leader: ") + name + Loc.Get(". Space opens the picker, D reads full details."));
        }

        private void AnnounceLeaderInfo(MapScreenController s)
        {
            int idx = GetLeaderIndex(s);
            if (idx <= 0)
            {
                ScreenReader.Say(Loc.Get("No leader chosen yet."));
                return;
            }
            try
            {
                Person person = PlayerClan.PersonWithIndex(idx);
                // 95 = name + deity + skills + age + location + health. PersonBio is
                // a localized port of the game's English-only AttributedTextFor; the
                // health bit matters for foray leaders since a wounded leader
                // reduces mission outcomes.
                ScreenReader.Say(PersonBio.Localized(person, 95));
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.AnnounceLeaderInfo", ex);
                ScreenReader.Say(Loc.Get("Could not read leader details."));
            }
        }

        // ---------- Destination zone ----------

        private void HandleDestinationInput(MapScreenController s)
        {
            EnsureDestinationsBuilt(s);
            if (_displayDestinations.Count == 0)
            {
                // Even with an empty view we want F/S to work — the user might be
                // in "Tribe" filter with no tribe members and need to switch out.
                HandleDestinationFilterAndSortKeys(s);
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _destinationIndex--;
                if (_destinationIndex < 0) _destinationIndex = _displayDestinations.Count - 1;
                AnnounceCurrentDestination(s);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _destinationIndex++;
                if (_destinationIndex >= _displayDestinations.Count) _destinationIndex = 0;
                AnnounceCurrentDestination(s);
                return;
            }
            // Space — Model Y: set the focused entry as the destination. Enter
            // is reserved globally for Send.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ActivateCurrentDestination(s);
                return;
            }
            // D — describe the focused entry. For clan entries we read the full
            // synopsis from Clan.ExplanationWithDetail(2) (same source the relations
            // screen uses for D); map zones have no description in the game data so
            // we say so honestly instead of inventing one.
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceFocusedDestinationDescription();
                return;
            }
            HandleDestinationFilterAndSortKeys(s);
        }

        private void AnnounceFocusedDestinationDescription()
        {
            if (_displayDestinations.Count == 0) return;
            int idx = (_destinationIndex < 0) ? 0 : _destinationIndex;
            if (idx >= _displayDestinations.Count) return;
            DestinationEntry e = _displayDestinations[idx];
            if (e.ClanIndex > 0)
                AnnounceClanSynopsis(e.ClanIndex);
            else
                ScreenReader.Say(Loc.Get("No description available for this map zone."));
        }

        private static void AnnounceClanSynopsis(int clanIndex)
        {
            try
            {
                Clan c = Clan.ClanWithIndex(clanIndex);
                if (c.isNull)
                {
                    ScreenReader.Say(Loc.Get("No description available."));
                    return;
                }
                string text = c.ExplanationWithDetail(2);
                if (string.IsNullOrEmpty(text))
                {
                    ScreenReader.Say(Loc.Get("No description available for ") + c.name + ".");
                    return;
                }
                text = StringHelpers.StripTags(text).Replace("\n\n", ". ").Replace("\n", " ");
                ScreenReader.Say(text);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.AnnounceClanSynopsis", ex);
                ScreenReader.Say(Loc.Get("Could not read clan description."));
            }
        }

        private void HandleDestinationFilterAndSortKeys(MapScreenController s)
        {
            // F / Shift+F — cycle filter category. Shift reverses direction so the
            // user can step back without having to wrap through all five.
            if (Input.GetKeyDown(KeyCode.F) && !HasCtrlAlt())
            {
                int dir = AnyShift() ? -1 : 1;
                CycleFilter(dir);
                ApplyFilterAndSort(s);
                ScreenReader.Say(Loc.Get("Filter: ") + FilterName(_filter)
                                 + ". " + _displayDestinations.Count + Loc.Get(" entries."));
                return;
            }
            // O / Shift+O — cycle sort mode (O for "order"). S is taken globally by
            // ManagementMenuController.ShowSeason — MapScreenController inherits via
            // ManagementController, so the global S-handler eats the keypress before
            // we ever see it here. O is free across the project.
            if (Input.GetKeyDown(KeyCode.O) && !HasCtrlAlt())
            {
                int dir = AnyShift() ? -1 : 1;
                CycleSort(dir);
                ApplyFilterAndSort(s);
                ScreenReader.Say(Loc.Get("Sort: ") + SortName(_sort) + ".");
                return;
            }
        }

        private void CycleFilter(int dir)
        {
            int n = Enum.GetValues(typeof(FilterCategory)).Length;
            int v = (int)_filter + dir;
            if (v < 0) v = n - 1;
            if (v >= n) v = 0;
            _filter = (FilterCategory)v;
            // Reset focus — sorting may scramble positions.
            _destinationIndex = -1;
        }

        private void CycleSort(int dir)
        {
            int n = Enum.GetValues(typeof(SortMode)).Length;
            int v = (int)_sort + dir;
            if (v < 0) v = n - 1;
            if (v >= n) v = 0;
            _sort = (SortMode)v;
            _destinationIndex = -1;
        }

        private void EnsureDestinationsBuilt(MapScreenController s)
        {
            if (_allDestinations.Count > 0)
            {
                if (_displayDestinations.Count == 0) ApplyFilterAndSort(s);
                return;
            }
            BuildDestinations(s);
            ApplyFilterAndSort(s);
        }

        /// <summary>Apply the current filter + sort to _allDestinations and store the
        /// result in _displayDestinations. Distance is computed once here and cached
        /// on each entry so AnnounceCurrentDestination doesn't recompute per arrow.</summary>
        private void ApplyFilterAndSort(MapScreenController s)
        {
            _displayDestinations.Clear();

            for (int i = 0; i < _allDestinations.Count; i++)
            {
                DestinationEntry e = _allDestinations[i];
                if (!PassesFilter(e)) continue;

                int miles = -1;
                try { miles = PluginImport.Mission_MilesFromPlayerClan(e.Center.x, e.Center.y); }
                catch (Exception ex) { DebugLogger.Error("MapScreenNav.MilesFromPlayerClan", ex); }
                e.CachedMiles = miles;
                _displayDestinations.Add(e);
            }

            switch (_sort)
            {
                case SortMode.ByDistance:
                    _displayDestinations.Sort(CompareByDistance);
                    break;
                case SortMode.Alphabetical:
                    _displayDestinations.Sort(CompareByName);
                    break;
                case SortMode.Default:
                default:
                    // Build order is the canonical default — Home first, then known
                    // clans in ClanDataList order, then special zones, then named
                    // map zones. Mirrors the MapView label order.
                    break;
            }
        }

        private bool PassesFilter(DestinationEntry e)
        {
            switch (_filter)
            {
                case FilterCategory.All:
                    return true;
                case FilterCategory.Clans:
                    // Home + every known clan (ClanIndex > 0).
                    return e.ClanIndex > 0;
                case FilterCategory.Tribe:
                    return e.Tag == "home" || e.Tag == "tribe";
                case FilterCategory.Feuds:
                    return e.Tag == "feud";
                case FilterCategory.Landmarks:
                    return e.Tag == "zone" || e.Tag == "river" || e.Tag == "wild"
                           || e.Tag == "dunes" || e.Tag == "gorp" || e.Tag == "chaos";
                default:
                    return true;
            }
        }

        private static int CompareByDistance(DestinationEntry a, DestinationEntry b)
        {
            // Unknown distance (-1) sinks to the bottom so reachable picks come first.
            int am = a.CachedMiles < 0 ? int.MaxValue : a.CachedMiles;
            int bm = b.CachedMiles < 0 ? int.MaxValue : b.CachedMiles;
            return am.CompareTo(bm);
        }

        private static int CompareByName(DestinationEntry a, DestinationEntry b)
        {
            return string.Compare(a.Name ?? "", b.Name ?? "", StringComparison.OrdinalIgnoreCase);
        }

        private void BuildDestinations(MapScreenController s)
        {
            _allDestinations.Clear();
            try
            {
                // Home — always first. Selecting it returns the cursor to the player
                // clan center; combined with explorationMoved=true via MapTapped this
                // produces the goal_ForayAtHome / goal_ExploreAtHome variant.
                Clan home = Clan.ClanWithIndex(PlayerClan.index);
                if (!home.isNull)
                {
                    _allDestinations.Add(new DestinationEntry
                    {
                        Name = Loc.Get("Home (") + home.name + ")",
                        Center = home.center,
                        ClanIndex = PlayerClan.index,
                        Tag = "home"
                    });
                }

                // Known clans — neighbors / explored. ClanDataList drives MapView's
                // labels so this list stays in sync with what sighted users see.
                // Tag is set fine-grained ("tribe" / "feud" / "trade" / "known") so the
                // filter categories can pick the matching ones without re-querying the
                // clan flags later.
                ClanDataList known = new ClanDataList(ClanFilterBy.filter_KnownClans);
                for (int i = 0; i < known.count; i++)
                {
                    Clan c = known[i];
                    if (c.isNull) continue;
                    if (c.index == PlayerClan.index) continue; // already added as Home

                    string suffix = "";
                    string tag = "known";
                    if (c.inOurTribe) { suffix = Loc.Get(" (tribe)"); tag = "tribe"; }
                    else if (c.haveFeud) { suffix = Loc.Get(" (feud)"); tag = "feud"; }
                    else if (c.haveTrade) { suffix = Loc.Get(" (trade partner)"); tag = "trade"; }
                    _allDestinations.Add(new DestinationEntry
                    {
                        Name = c.name + suffix,
                        Center = c.center,
                        ClanIndex = c.index,
                        Tag = tag
                    });
                }

                // Special clan-shaped zones — these filters return clan indices that
                // stand in for territory rather than real clans. Often empty in the
                // mid-game; the real coverage comes from the MapZone block below.
                AppendSpecialZones(_allDestinations, ClanFilterBy.filter_Uninhabited, Loc.Get("Abandoned land"), "wild");
                AppendSpecialZones(_allDestinations, ClanFilterBy.filter_Duneland, Loc.Get("Dunes"), "dunes");
                AppendSpecialZones(_allDestinations, ClanFilterBy.filter_GorpedTerritory, Loc.Get("Gorp territory"), "gorp");
                AppendSpecialZones(_allDestinations, ClanFilterBy.filter_ChaosNest, Loc.Get("Chaos Nest"), "chaos");

                // Named map zones — rivers, hills, forests, glacier, crater, etc.
                // The MapZone API returns every authored region regardless of
                // exploration state; we filter out clan zones (already in the list
                // above), label-only zones (decorative anchors with no real area),
                // and crossings (river-passage helpers used internally by the game).
                AppendNamedMapZones(_allDestinations);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.BuildDestinations", ex);
            }
        }

        private static void AppendNamedMapZones(List<DestinationEntry> dest)
        {
            try
            {
                int count = PluginImport.MapZone_Count();
                for (int i = 0; i < count; i++)
                {
                    var zone = new MapZone { index = i };
                    // Clan-bearing zones are already represented by the clan list
                    // (ClanDataList(filter_KnownClans)). Adding them here would
                    // produce duplicate entries with raw zone names like "Verlaro1".
                    if (zone.isClanZone) continue;

                    // kLabelOnly zones are decorative label anchors that don't
                    // correspond to a tappable area — skipping prevents Space from
                    // setting an invalid destination.
                    if ((zone.flags & ZoneFlags.kLabelOnly) != 0) continue;

                    string label = zone.label;
                    string name = zone.name;
                    if (string.IsNullOrEmpty(label) && string.IsNullOrEmpty(name)) continue;

                    // Skip internal pathfinding helpers ("Across*" river crossings,
                    // "NBE5"/"SO3"/"Catchall*" sub-regions). Keep zones that have a
                    // human label even if their internal name is a code — those are
                    // legitimate destinations like "Burnpeak" or "Black Eel R."
                    if (string.IsNullOrEmpty(label) && IsInternalZoneCode(name)) continue;
                    string display = !string.IsNullOrEmpty(label) ? label : name;
                    if (name != null && name.StartsWith("Across", StringComparison.Ordinal)) continue;

                    Rect bounds = zone.bounds;
                    Vector2 center = new Vector2(bounds.x + bounds.width * 0.5f,
                                                  bounds.y + bounds.height * 0.5f);
                    dest.Add(new DestinationEntry
                    {
                        Name = display,
                        Center = new Vector2Int(Mathf.RoundToInt(center.x), Mathf.RoundToInt(center.y)),
                        ClanIndex = -1,
                        Tag = zone.isWild ? "river" : "zone"
                    });
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.AppendNamedMapZones", ex);
            }
        }

        private static void AppendSpecialZones(List<DestinationEntry> dest,
                                               ClanFilterBy filter, string label, string tag)
        {
            try
            {
                ClanDataList list = new ClanDataList(filter);
                for (int i = 0; i < list.count; i++)
                {
                    Clan c = list[i];
                    if (c.isNull) continue;
                    string name = list.count > 1 ? (label + " " + (i + 1)) : label;
                    dest.Add(new DestinationEntry
                    {
                        Name = name,
                        Center = c.center,
                        ClanIndex = -1,
                        Tag = tag
                    });
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.AppendSpecialZones", ex);
            }
        }

        private void AnnounceCurrentDestination(MapScreenController s)
        {
            if (_displayDestinations.Count == 0)
            {
                ScreenReader.Say(Loc.Get("No destinations match the current filter. Press F to widen."));
                return;
            }
            int idx = (_destinationIndex < 0) ? 0 : _destinationIndex;
            if (idx >= _displayDestinations.Count) idx = _displayDestinations.Count - 1;

            DestinationEntry e = _displayDestinations[idx];

            var sb = new StringBuilder();
            sb.Append(e.Name);

            // Selected marker — placed right after the name so fast arrow
            // browsing can't cut it off. The cursor's current target comes from
            // mapAnnotations.selectedClan, updated whenever the user picks a
            // clan-bearing hex; we use it as the source of truth.
            int selectedClan = SafeSelectedClan(s);
            if (e.ClanIndex > 0 && e.ClanIndex == selectedClan)
                sb.Append(Loc.Get(", currently targeted"));

            if (e.CachedMiles >= 0 && e.Tag != "home")
                sb.Append(", ").Append(e.CachedMiles).Append(Loc.Get(" miles"));
            sb.Append(Loc.Get(". Press Space to set destination."));
            ScreenReader.Say(sb.ToString());
        }

        private void ActivateCurrentDestination(MapScreenController s)
        {
            if (_displayDestinations.Count == 0) return;
            int idx = (_destinationIndex < 0) ? 0 : _destinationIndex;
            if (idx >= _displayDestinations.Count) return;

            DestinationEntry e = _displayDestinations[idx];
            Vector2 target = new Vector2(e.Center.x, e.Center.y);
            Goal goal = TryGetCurrentGoal(s);

            // Pre-check whether the tap will be accepted. If the destination fails
            // CanForayTo or the Forage 50-mile cap, MapTapped will silently revert the
            // cursor and selectedClan stays unchanged — the tooltip path may not always
            // route through ScreenReader, so we surface the reason directly.
            string rejection = CheckDestinationValidity(s, target, goal);

            // MapTapped is the canonical handler used by mouse clicks. It validates
            // CanForayTo, surfaces a tooltip on rejection, sets explorationMoved,
            // updates selectedClan + the description box, and re-validates SEND.
            // Using it here means we get the same gameplay rules and feedback as the
            // mouse path with no special-casing.
            try
            {
                s.MapTapped(target);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.MapTapped", ex);
                ScreenReader.Say(Loc.Get("Could not set destination."));
                return;
            }

            // Sync the hex cursor zone to the same hex so Tab → HexCursor lands on a
            // useful starting position and the user can fine-tune from there.
            try
            {
                HexGrid hex = MapHex.HexAtPoint(target);
                _hexRow = hex.row;
                _hexCol = hex.column;
            }
            catch (Exception ex) { DebugLogger.Error("MapScreenNav.HexFromCenter", ex); }

            if (rejection != null)
            {
                ScreenReader.Say(e.Name + Loc.Get(" not accepted: ") + rejection + ".");
                return;
            }

            // Tap accepted — read back state and announce. selectedClan changes only on
            // a valid target; "Send is still disabled" now folds in WhySendDisabled so
            // the user hears the actual remaining blocker (leader, warriors, horses).
            int newSelected = SafeSelectedClan(s);
            bool sendOk = s.actionButton != null && s.actionButton.interactable;
            string status = sendOk ? Loc.Get("Send is now enabled.") : Loc.Get("Send is still disabled. ") + WhySendDisabled(s);
            string hexInfo = (_hexRow >= 0) ? Loc.Get(" Hex cursor at ") + (_hexRow + 1) + ", " + (_hexCol + 1) + "." : "";

            // AtHome downgrade warning — picking your own clan or a hex inside your own
            // territory will silently turn the mission into a foray-at-home variant.
            string downgrade = GetAtHomeDowngradeReason(s);
            string atHome = (downgrade != null) ? Loc.Get(" Note: ") + downgrade + Loc.Get(" — mission will resolve as foray at home.") : "";

            if (newSelected > 0 && newSelected == e.ClanIndex)
                ScreenReader.Say(Loc.Get("Destination set to ") + e.Name + "." + hexInfo + " " + status + atHome);
            else if (e.Tag == "home")
                ScreenReader.Say(Loc.Get("Destination set to home.") + hexInfo + " " + status + atHome);
            else
                ScreenReader.Say(e.Name + Loc.Get(" selected on map.") + hexInfo + " " + status + atHome);
        }

        private static int SafeSelectedClan(MapScreenController s)
        {
            try
            {
                if (s.mapAnnotations == null) return -1;
                return s.mapAnnotations.selectedClan;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.SafeSelectedClan", ex);
                return -1;
            }
        }

        // ---------- Hex cursor zone ----------

        /// <summary>Sync (_hexRow, _hexCol) from the live explorationCursor.position so
        /// the user starts navigating from where the game last left the cursor —
        /// usually the player clan center on a fresh open. Called when the user enters
        /// the zone (Tab/H) or when arrow keys would move from an uninitialised state.</summary>
        private void SyncHexFromCursor(MapScreenController s)
        {
            if (_hexRow >= 0 && _hexCol >= 0) return;
            try
            {
                var view = s.mapAnnotations;
                Vector2 pos = (view != null && view.explorationCursor != null)
                    ? view.explorationCursor.position
                    : Vector2.zero;
                HexGrid hex = MapHex.HexAtPoint(pos);
                _hexRow = hex.row;
                _hexCol = hex.column;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.SyncHexFromCursor", ex);
                _hexRow = 0;
                _hexCol = 0;
            }
        }

        private void HandleHexCursorInput(MapScreenController s)
        {
            SyncHexFromCursor(s);
            int rows = MapHex.rows;
            int cols = MapHex.columns;
            if (rows <= 0 || cols <= 0) return;

            // PageUp / PageDown — POI filter system. Bare keys cycle the entry
            // index within the current category; Ctrl+key cycles category. Both
            // move _hexRow/_hexCol to the target so the user can then Space to
            // set it as the destination. Built lazily on first use.
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                EnsurePoisBuilt(s);
                if (ctrl) CyclePoiCategory(s, +1);
                else CyclePoiEntry(s, +1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                EnsurePoisBuilt(s);
                if (ctrl) CyclePoiCategory(s, -1);
                else CyclePoiEntry(s, -1);
                return;
            }

            // Big-step modifier — Shift moves five hexes at a time so the user can
            // sweep across the map quickly without hammering the arrow key 30 times.
            int step = AnyShift() ? 5 : 1;

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _hexRow = ClampHex(_hexRow - step, 0, rows - 1);
                AnnounceCurrentHex(s);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _hexRow = ClampHex(_hexRow + step, 0, rows - 1);
                AnnounceCurrentHex(s);
                return;
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                _hexCol = ClampHex(_hexCol - step, 0, cols - 1);
                AnnounceCurrentHex(s);
                return;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                _hexCol = ClampHex(_hexCol + step, 0, cols - 1);
                AnnounceCurrentHex(s);
                return;
            }
            // Space — Model Y: set the focused hex as the destination. Enter is
            // reserved globally for Send.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ActivateCurrentHex(s);
                return;
            }
            // Home — jump back to the player clan center. Useful after wandering far
            // off and wanting a quick "reset to my own lands" without holding arrow
            // keys for thirty hexes.
            if (Input.GetKeyDown(KeyCode.Home))
            {
                JumpToHomeHex(s);
                return;
            }
            // D — describe whatever the cursor sits on. Same source as the list
            // (Clan.ExplanationWithDetail(2)) when the hex belongs to a clan.
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceFocusedHexDescription();
                return;
            }
        }

        private void JumpToHomeHex(MapScreenController s)
        {
            try
            {
                Clan home = Clan.ClanWithIndex(PlayerClan.index);
                if (home.isNull)
                {
                    ScreenReader.Say(Loc.Get("Home location unknown."));
                    return;
                }
                HexGrid hex = MapHex.HexAtPoint(new Vector2(home.center.x, home.center.y));
                _hexRow = hex.row;
                _hexCol = hex.column;
                AnnounceCurrentHex(s);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.JumpToHomeHex", ex);
                ScreenReader.Say(Loc.Get("Could not jump to home."));
            }
        }

        private void AnnounceFocusedHexDescription()
        {
            if (_hexRow < 0 || _hexCol < 0) return;
            try
            {
                Vector2 center = HexCenter();
                int clanIdx = WorldMap.ClanAtPoint(center);
                if (clanIdx > 0)
                {
                    AnnounceClanSynopsis(clanIdx);
                    return;
                }
                ScreenReader.Say(Loc.Get("No description available for this hex."));
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.AnnounceFocusedHexDescription", ex);
            }
        }

        private static int ClampHex(int v, int lo, int hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }

        private Vector2 HexCenter()
        {
            // RectFor returns a 30x26 hex rect; the center is offset by half size.
            Rect r = MapHex.RectFor(_hexRow, _hexCol);
            return new Vector2(r.x + r.width * 0.5f, r.y + r.height * 0.5f);
        }

        private void AnnounceCurrentHex(MapScreenController s)
        {
            ScreenReader.Say(BuildHexAnnouncement(s));
        }

        /// <summary>Build the cursor-position announcement string. Split out from
        /// AnnounceCurrentHex so the POI-jump path can prepend a "N of M, label"
        /// context line and still speak the whole thing in a single ScreenReader.Say
        /// call (multiple Say calls would interrupt each other).</summary>
        private string BuildHexAnnouncement(MapScreenController s)
        {
            if (_hexRow < 0 || _hexCol < 0)
            {
                return Loc.Get("Hex cursor not initialised.");
            }
            Vector2 center = HexCenter();

            var sb = new StringBuilder();
            // Minimalist coords — "20, 14" instead of "Row 20 of 42, column 14 of
            // 37". Map size doesn't change while moving and the user can always
            // ask for it with F5; speaking it on every step is just noise.
            sb.Append(_hexRow + 1).Append(", ").Append(_hexCol + 1).Append(". ");

            // Identify what's at this hex.
            string here = DescribeHexContents(center);
            if (!string.IsNullOrEmpty(here)) sb.Append(here).Append(". ");

            // Terrain / accessibility — only mention if unusual. On a settled map most
            // hexes are explored + accessible (the game seeds exploration around the
            // player clan and through story events), so saying "Explored" every time
            // would be redundant noise. Stay quiet on the default case; speak up for
            // dead/inaccessible/unexplored where it actually changes the user's plan.
            if (MapHex.IsDead(_hexRow, _hexCol))
                sb.Append(Loc.Get("Dead — no longer exists. "));
            else if (!MapHex.IsAccessible(_hexRow, _hexCol))
                sb.Append(Loc.Get("Inaccessible. "));
            else if (!MapHex.IsExplored(_hexRow, _hexCol))
                sb.Append(Loc.Get("Unexplored. "));

            // Distance.
            try
            {
                int miles = PluginImport.Mission_MilesFromPlayerClan((int)center.x, (int)center.y);
                if (miles >= 0) sb.Append(miles).Append(Loc.Get(" miles from home. "));
            }
            catch (Exception ex) { DebugLogger.Error("MapScreenNav.HexMiles", ex); }

            sb.Append(Loc.Get("Space to set destination."));
            return sb.ToString();
        }

        /// <summary>Identify the clan or named map zone that contains the given point.
        /// Used by the hex cursor announcement to give context like "Verlaro clan" or
        /// "Black Eel River" instead of bare row/column numbers.</summary>
        private static string DescribeHexContents(Vector2 point)
        {
            try
            {
                int clanIdx = WorldMap.ClanAtPoint(point);
                if (clanIdx > 0)
                {
                    Clan c = Clan.ClanWithIndex(clanIdx);
                    if (!c.isNull)
                    {
                        if (clanIdx == PlayerClan.index) return Loc.Get("Home, ") + c.name;
                        // Lead with attitude — same info the coloured clan name
                        // on the map conveys. inOurTribe is folded in by the
                        // helper (the underline marker on the map label).
                        string attitude = StringHelpers.AttitudeLabel(c.attitudeColor, c.inOurTribe);
                        string extras = "";
                        if (c.haveFeud) extras += Loc.Get(", in feud");
                        if (c.haveTrade) extras += Loc.Get(", trade partner");
                        return c.name + ", " + attitude + extras;
                    }
                }
                MapZone zone = WorldMap.ZoneAtPoint(point);
                if (!zone.isNull)
                {
                    string label = zone.label;
                    string name = zone.name;
                    if (!string.IsNullOrEmpty(label)) return label;
                    if (!string.IsNullOrEmpty(name) && !IsInternalZoneCode(name)) return name;
                    // Internal pathfinding zone — derive a meaningful description
                    // from ZoneFlags + name prefix instead of a bare "wilderness".
                    return DescribeWildZone(zone);
                }
            }
            catch (Exception ex) { DebugLogger.Error("MapScreenNav.DescribeHexContents", ex); }
            return "";
        }

        /// <summary>Build a description for a zone that has no human label, by combining
        /// ZoneFlags (hilly / mountainous / river / extra-travel / catch-all / across)
        /// with the directional hint encoded in the internal name (NBE = "north of
        /// Black Eel", SO = "south of Oslira", etc.). For Across-zones we also check
        /// the matching wild-river flag — if a river is currently wild, sighted users
        /// see a Tutorial.ShowNote on click; we surface the same info up-front.</summary>
        private static string DescribeWildZone(MapZone zone)
        {
            ZoneFlags flags = zone.flags;

            // River crossings — most actionable info, mention first.
            string crossingRiver = null;
            bool crossingWild = false;
            if ((flags & ZoneFlags.kAcrossBlackEel) != 0)
            {
                crossingRiver = "Black Eel";
                try { crossingWild = Game.BooleanVariable("wildBlackEel"); } catch { }
            }
            else if ((flags & ZoneFlags.kAcrossOslira) != 0)
            {
                crossingRiver = "Oslira";
                try { crossingWild = Game.BooleanVariable("wildOslir"); } catch { }
            }
            else if ((flags & ZoneFlags.kAcrossForantin) != 0)
            {
                crossingRiver = "Forantin";
                try { crossingWild = Game.BooleanVariable("wildForantin"); } catch { }
            }
            if (!string.IsNullOrEmpty(crossingRiver))
            {
                return crossingWild
                    ? crossingRiver + Loc.Get(" river crossing — currently wild and dangerous")
                    : crossingRiver + Loc.Get(" river crossing");
            }

            var sb = new StringBuilder();
            // Terrain prefix — order matters: mountainous beats hilly when both are
            // set (game data shouldn't combine them, but be defensive).
            if ((flags & ZoneFlags.kMountainous) != 0) sb.Append(Loc.Get("mountainous "));
            else if ((flags & ZoneFlags.kHilly) != 0) sb.Append(Loc.Get("hilly "));

            // Direction hint from the name pattern — ten characters of memorable
            // context that orient the user without exposing the raw code.
            string direction = ParseDirectionFromCode(zone.name);

            // Noun — always "wilderness" when we have a direction hint, because the
            // kRiver flag on a code-named NBE/SO/NF zone means "riverbank-adjacent",
            // not "is the river itself". The real river zones (Black Eel R., Oslira
            // R., Forantin R.) carry a human label and short-circuit through the
            // label path before reaching this function — so saying "river south of
            // Oslira river" is always wrong. Reserve the river noun for the rare
            // unnamed-but-river zone (no direction hint at all).
            if (!string.IsNullOrEmpty(direction))
            {
                sb.Append(Loc.Get("wilderness ")).Append(direction);
            }
            else if ((flags & ZoneFlags.kRiver) != 0)
            {
                sb.Append(Loc.Get("river"));
            }
            else
            {
                sb.Append(Loc.Get("wilderness"));
            }

            // Difficulty marker — extra-travel zones cost more time to cross.
            if ((flags & ZoneFlags.kExtraTravel) != 0) sb.Append(Loc.Get(", difficult terrain"));

            return sb.ToString();
        }

        /// <summary>Parse the directional prefix out of an internal zone code. The
        /// game's pathfinding zones are named after their position relative to the
        /// three rivers — NBE3 = "North Black Eel sub-region 3", SO5 = "South
        /// Oslira sub-region 5". We strip the digits and map the letter prefix to
        /// a human-readable direction. Unknown prefixes return null so the caller
        /// can fall back gracefully.</summary>
        private static string ParseDirectionFromCode(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            int letterCount = 0;
            while (letterCount < name.Length
                   && name[letterCount] >= 'A' && name[letterCount] <= 'Z')
                letterCount++;
            if (letterCount == 0) return null;
            string code = name.Substring(0, letterCount);
            switch (code)
            {
                case "NBE": return Loc.Get("north of Black Eel river");
                case "SBE": return Loc.Get("south of Black Eel river");
                case "NO":  return Loc.Get("north of Oslira river");
                case "SO":  return Loc.Get("south of Oslira river");
                case "NF":  return Loc.Get("north of Forantin river");
                case "SF":  return Loc.Get("south of Forantin river");
                default: return null;
            }
        }

        /// <summary>True if a MapZone name looks like an internal pathfinding helper
        /// (e.g. "NBE5" = North Black Eel sub-region, "SO3" = South Oslira, "Catchall*",
        /// "Across*"). These zones don't appear as labels on the visible map and
        /// aren't meaningful gameplay targets — speaking the bare code ("NBE5") to
        /// the user is just confusing, so we substitute "wilderness".</summary>
        private static bool IsInternalZoneCode(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.StartsWith("Catchall", StringComparison.Ordinal)) return true;
            if (name.StartsWith("Across", StringComparison.Ordinal)) return true;
            // Pattern: 2-4 uppercase letters followed by 1-3 digits (no other chars).
            int i = 0;
            int letterCount = 0;
            while (i < name.Length && name[i] >= 'A' && name[i] <= 'Z') { i++; letterCount++; }
            if (letterCount < 2 || letterCount > 4) return false;
            if (i >= name.Length) return false;
            int digitStart = i;
            while (i < name.Length && name[i] >= '0' && name[i] <= '9') i++;
            if (i - digitStart < 1) return false;
            return i == name.Length;
        }

        private void ActivateCurrentHex(MapScreenController s)
        {
            if (_hexRow < 0 || _hexCol < 0) return;
            Vector2 center = HexCenter();
            Goal goal = TryGetCurrentGoal(s);

            // Pre-check rejection so we can announce a specific reason. MapTapped will
            // still be called either way to keep the game's tooltip + sound path firing
            // for sighted and screen-reader users alike.
            string rejection = CheckDestinationValidity(s, center, goal);

            try { s.MapTapped(center); }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.HexMapTapped", ex);
                ScreenReader.Say(Loc.Get("Could not set destination at this hex."));
                return;
            }

            if (rejection != null)
            {
                ScreenReader.Say(Loc.Get("Hex ") + (_hexRow + 1) + ", " + (_hexCol + 1)
                                 + Loc.Get(" not accepted: ") + rejection + ".");
                return;
            }

            bool sendOk = s.actionButton != null && s.actionButton.interactable;
            string status = sendOk ? Loc.Get("Send is now enabled.") : Loc.Get("Send is still disabled. ") + WhySendDisabled(s);

            // Phase-3 sync: search the destination list for an entry whose center
            // sits in the same hex. If we find one, jump _destinationIndex onto it
            // and tell the user where it is in the list — symmetrical to the
            // hex-positioning we do from the list-side Space.
            int matchIdx = FindDestinationIndexAtPoint(center);
            string listInfo;
            if (matchIdx >= 0)
            {
                _destinationIndex = matchIdx;
                listInfo = Loc.Get(" Found in list: ") + _displayDestinations[matchIdx].Name + ".";
            }
            else
            {
                // Unnamed hex — clear the list focus so a stale earlier list
                // pick can't be reported by anything that still reads the
                // index after Fix-1 removed the dependency from the Enter
                // summary. Defense-in-depth.
                _destinationIndex = -1;
                listInfo = "";
            }

            // AtHome downgrade — picking a hex inside our own territory turns the
            // mission into the foray-at-home variant on send. Surface up-front.
            string downgrade = GetAtHomeDowngradeReason(s);
            string atHome = (downgrade != null) ? Loc.Get(" Note: ") + downgrade + Loc.Get(" — mission will resolve as foray at home.") : "";

            ScreenReader.Say(Loc.Get("Hex ") + (_hexRow + 1) + ", " + (_hexCol + 1)
                             + Loc.Get(" set as destination.") + listInfo + " " + status + atHome);
        }

        /// <summary>Find the display-list entry whose center lies in the hex at the
        /// given world point. Used for the Hex → List sync on Space so the user can
        /// find what they just picked in the linear list view.</summary>
        private int FindDestinationIndexAtPoint(Vector2 point)
        {
            try
            {
                HexGrid target = MapHex.HexAtPoint(point);
                for (int i = 0; i < _displayDestinations.Count; i++)
                {
                    DestinationEntry e = _displayDestinations[i];
                    HexGrid eHex = MapHex.HexAtPoint(new Vector2(e.Center.x, e.Center.y));
                    if (eHex.row == target.row && eHex.column == target.column) return i;
                }
            }
            catch (Exception ex) { DebugLogger.Error("MapScreenNav.FindDestAtPoint", ex); }
            return -1;
        }

        // ---------- State / validity helpers ----------

        /// <summary>Read the live exploration cursor position. Returns Vector2.zero on any
        /// access failure; callers that need a "no cursor" sentinel should branch on the
        /// mapAnnotations null path instead.</summary>
        private static Vector2 GetCursorPosition(MapScreenController s)
        {
            try
            {
                if (s != null && s.mapAnnotations != null && s.mapAnnotations.explorationCursor != null)
                    return s.mapAnnotations.explorationCursor.position;
            }
            catch (Exception ex) { DebugLogger.Error("MapScreenNav.GetCursorPosition", ex); }
            return Vector2.zero;
        }

        /// <summary>Read MapScreenController.explorationMoved via reflection. The flag is
        /// flipped to true the first time MapTapped accepts a click, and gates whether
        /// Foray() will downgrade the goal to its goal_*AtHome variant. Private field, so
        /// reflection is the only way — same approach we use for leaderIndex.</summary>
        private static bool GetExplorationMoved(MapScreenController s)
        {
            try
            {
                FieldInfo f = typeof(MapScreenController).GetField(
                    "explorationMoved", BindingFlags.NonPublic | BindingFlags.Instance);
                // Cast to object: Mono 2.0 lacks MemberInfo.op_Equality.
                if ((object)f == null) return false;
                object v = f.GetValue(s);
                return (v is bool b) && b;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.GetExplorationMoved", ex);
                return false;
            }
        }

        /// <summary>Read the currently selected goal from the UI list. Returns goal_Favor as
        /// a sentinel "no goal yet" — Favor is never a valid mission goal so callers can
        /// safely treat it as "unknown".</summary>
        private static Goal TryGetCurrentGoal(MapScreenController s)
        {
            try
            {
                if (s != null && s.goalList != null && s.goalList.selectedItem != null)
                    return (Goal)s.goalList.selectedItem.key;
            }
            catch (Exception ex) { DebugLogger.Error("MapScreenNav.TryGetCurrentGoal", ex); }
            return Goal.goal_Favor;
        }

        /// <summary>Pre-check whether MapTapped will accept a destination for the current
        /// goal. Mirrors the game's CanForayTo plus the Forage-only 50-mile cap from
        /// ValidateSendButton. Returns null when the destination is acceptable; otherwise
        /// returns a human-readable reason. Used by destination/hex activation to give a
        /// specific rejection message instead of falling through to a generic "still
        /// disabled" status.</summary>
        private static string CheckDestinationValidity(MapScreenController s, Vector2 point, Goal goal)
        {
            try
            {
                if (PluginImport.MapHex_IsDeadAtPoint((int)point.x, (int)point.y))
                    return Loc.Get("this area no longer exists");

                if (!s.CanForayTo(point))
                {
                    switch (goal)
                    {
                        case Goal.goal_Explore:
                            return Loc.Get("exploration must be near known lands");
                        case Goal.goal_Forage:
                            return Loc.Get("foraging needs a hex inside known lands (already explored)");
                        case Goal.goal_CaptureHorses:
                            return Loc.Get("horse expedition must be inside known lands");
                        case Goal.goal_SearchForSpirits:
                            return Loc.Get("spirit expedition must be inside known lands");
                        case Goal.goal_SearchForRegalia:
                        case Goal.goal_SearchForWeddingTreasure:
                            return Loc.Get("treasure search must be near known lands and not at home");
                        default:
                            return Loc.Get("destination not allowed for this goal");
                    }
                }

                if (goal == Goal.goal_Forage)
                {
                    int miles = PluginImport.Mission_MilesFromPlayerClan((int)point.x, (int)point.y);
                    if (miles >= 50)
                        return Loc.Get("foraging cannot exceed 50 miles, this destination is ") + miles + Loc.Get(" miles away");
                }
            }
            catch (Exception ex) { DebugLogger.Error("MapScreenNav.CheckDestinationValidity", ex); }
            return null;
        }

        /// <summary>If the current state would cause Foray() to downgrade the goal to its
        /// goal_*AtHome variant, return a short reason string. Otherwise return null.
        /// Mirrors the three branches in MapScreenController.Foray (lines 167-179):
        /// !explorationMoved, selectedClan == PlayerClan.index, ClanAtPoint == PlayerClan.</summary>
        private static string GetAtHomeDowngradeReason(MapScreenController s)
        {
            try
            {
                if (!GetExplorationMoved(s)) return Loc.Get("cursor has not been moved yet");
                int sel = SafeSelectedClan(s);
                if (sel == PlayerClan.index) return Loc.Get("our own clan is selected as destination");
                Vector2 cursor = GetCursorPosition(s);
                if (WorldMap.ClanAtPoint(cursor) == PlayerClan.index)
                    return Loc.Get("cursor sits inside our own clan territory");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.GetAtHomeDowngradeReason", ex);
            }
            return null;
        }

        /// <summary>Reverse-engineered from MapScreenController.ValidateSendButton (lines
        /// 377-404). The SEND button is disabled when:
        ///   - leaderIndex <= 0
        ///   - escort total + 1 (for the leader) exceeds available horses (the slider
        ///     LinkTo usually prevents this, but a horse loss after open can break it)
        ///   - escort total < 2
        ///   - goal == Forage AND destination >= 50 miles from home
        ///   - !CanForayTo(cursor) — IsExplored for Forage/Spirits/Horses, IsAccessible
        ///     for Explore, plus regalia's "not at home" rule
        ///   - goal == CaptureHorses without the Horsebreaker blessing or full
        ///     Golden Daughters myth knowledge
        /// We re-check each branch in the same order so the message names the actual
        /// failing condition rather than a generic catch-all.</summary>
        private string WhySendDisabled(MapScreenController s)
        {
            int leader = GetLeaderIndex(s);
            if (leader <= 0) return Loc.Get("No leader chosen.");

            int swords = (s.eliteSlider != null) ? s.eliteSlider.intValue : 0;
            int bows = (s.regularSlider != null) ? s.regularSlider.intValue : 0;
            int total = swords + bows;

            // Horse availability — the validator forces escort to 0 when total + 1 (leader)
            // would exceed available horses, which then cascades into the "<2" branch with
            // a misleading message. Check it explicitly so the user hears the real cause.
            int horses = -1;
            try { horses = PlayerClan.horses; }
            catch (Exception ex) { DebugLogger.Error("MapScreenNav.WhySendDisabled.horses", ex); }
            if (horses >= 0 && total + 1 > horses)
                return Loc.Get("Not enough horses. We have ") + horses + Loc.Get(" but need ") + (total + 1)
                       + Loc.Get(" (escort plus leader). Lower swords or bows.");

            if (total < 2) return Loc.Get("Choose at least two warriors.");

            Goal goal = TryGetCurrentGoal(s);

            // Destination check — covers the Forage 50-mile cap and the per-goal
            // CanForayTo rules. Reads the live cursor position from mapAnnotations.
            Vector2 cursor = GetCursorPosition(s);
            string destReason = CheckDestinationValidity(s, cursor, goal);
            if (destReason != null)
                return Loc.Get("Destination is invalid: ") + destReason + ".";

            // Capture-horses: needs Horsebreaker blessing or full Golden Daughters myth.
            if (goal == Goal.goal_CaptureHorses)
            {
                try
                {
                    bool blessed = PlayerClan.BlessingActive("Horsebreaker");
                    if (!blessed && PlayerClan.MythKnowledge(Myth.myth_GoldenDaughters) <= Knowledge.kHeardOf)
                        return Loc.Get("Capture-horses requires the Horsebreaker blessing or full Golden Daughters knowledge.");
                }
                catch (Exception ex)
                {
                    DebugLogger.Error("MapScreenNav.WhySendDisabled.captureHorses", ex);
                }
            }

            // Final fallback — leaderIndex/escort/destination all looked OK to us but the
            // game still rejects. Likely an unmapped per-goal condition; fall back to a
            // generic message rather than claiming everything is fine.
            int sel = SafeSelectedClan(s);
            if (sel < 0 && (goal == Goal.goal_Explore || goal == Goal.goal_SearchForRegalia
                            || goal == Goal.goal_SearchForWeddingTreasure))
                return Loc.Get("Choose a destination first.");
            return Loc.Get("Destination not allowed for this goal.");
        }

        // ---------- Full status (F5) ----------

        public void AnnounceFullStatus(MapScreenController s)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Map. "));

            // Goal + description
            if (s.goalList != null && s.goalList.selectedItem != null)
            {
                string goalName = SafeText(s.goalList.selectedItem);
                sb.Append(Loc.Get("Goal: ")).Append(goalName).Append(". ");
                Goal g = (Goal)s.goalList.selectedItem.key;
                string desc = s.ExplanationForGoal(g);
                if (!string.IsNullOrEmpty(desc)) sb.Append(desc).Append(" ");
            }

            // Leader
            int leaderIdx = GetLeaderIndex(s);
            if (leaderIdx > 0)
            {
                string name = PluginImport.PC_PersonName(leaderIdx);
                if (!string.IsNullOrEmpty(name)) sb.Append(Loc.Get("Leader: ")).Append(name).Append(". ");
            }
            else
            {
                sb.Append(Loc.Get("No leader chosen. "));
            }

            // Sliders + horse cap. The game has no horse slider — eliteSlider is linked
            // to regularSlider with a shared cap of horses-1 (the leader takes the last
            // horse), so saying "X of horses-1" upfront tells the user why their max may
            // be lower than warriors. Pull horses defensively in case the API throws.
            int horses = -1;
            try { horses = PlayerClan.horses; }
            catch (Exception ex) { DebugLogger.Error("MapScreenNav.FullStatus.horses", ex); }
            if (s.eliteSlider != null && s.regularSlider != null)
                sb.Append(Loc.Get("Swords ")).Append(s.eliteSlider.intValue).Append(Loc.Get(" of ")).Append((int)s.eliteSlider.maxValue)
                  .Append(Loc.Get(", Bows ")).Append(s.regularSlider.intValue).Append(Loc.Get(" of ")).Append((int)s.regularSlider.maxValue)
                  .Append(". ");
            if (horses >= 0)
                sb.Append(Loc.Get("Horses available: ")).Append(horses)
                  .Append(Loc.Get(" (escort plus leader cannot exceed this). "));

            // Destination — selectedClan or "home"
            int sel = SafeSelectedClan(s);
            if (sel == PlayerClan.index)
                sb.Append(Loc.Get("Destination: home. "));
            else if (sel > 0)
            {
                Clan c = Clan.ClanWithIndex(sel);
                if (!c.isNull) sb.Append(Loc.Get("Destination: ")).Append(c.name).Append(". ");
            }
            else
            {
                sb.Append(Loc.Get("Destination not yet chosen. "));
            }

            // Forage-only distance readback — the 50-mile cap is unique to Forage and
            // the most common reason its SEND stays disabled. Speak the live distance
            // so the user can decide whether to move closer without trial-and-error.
            Goal currentGoal = TryGetCurrentGoal(s);
            if (currentGoal == Goal.goal_Forage)
            {
                try
                {
                    Vector2 cursor = GetCursorPosition(s);
                    int miles = PluginImport.Mission_MilesFromPlayerClan((int)cursor.x, (int)cursor.y);
                    if (miles >= 0)
                    {
                        sb.Append(Loc.Get("Distance to cursor: ")).Append(miles).Append(Loc.Get(" miles"));
                        if (miles >= 50) sb.Append(Loc.Get(" — over the 50 mile foraging limit"));
                        sb.Append(". ");
                    }
                }
                catch (Exception ex) { DebugLogger.Error("MapScreenNav.FullStatus.foragemiles", ex); }
            }

            // SEND
            bool canSend = s.actionButton != null && s.actionButton.interactable;
            sb.Append(canSend
                ? Loc.Get("Press Enter to send. X opens explore panel. Escape cancels.")
                : (Loc.Get("Send is disabled. ") + WhySendDisabled(s)));

            // AtHome downgrade prediction — even when SEND is enabled, the mission may
            // silently resolve as foray-at-home if the cursor never moved or sits in
            // our own territory. Let the user know before they press send.
            string downgrade = GetAtHomeDowngradeReason(s);
            if (downgrade != null)
                sb.Append(Loc.Get(" Note: ")).Append(downgrade)
                  .Append(Loc.Get(" — mission will resolve as foray at home if sent now."));

            ScreenReader.Say(sb.ToString());
        }

        // ---------- Helpers ----------

        private static string SafeText(UIListItem item)
        {
            if (item == null) return "";
            string t = item.text;
            if (string.IsNullOrEmpty(t)) t = item.gameObject != null ? item.gameObject.name : "";
            return t ?? "";
        }

        private static void SubmitButton(UIButton button)
        {
            if (button == null) return;
            ISubmitHandler handler = button as ISubmitHandler;
            if (handler == null) return;
            handler.OnSubmit(new BaseEventData(EventSystem.current));
        }

        // ---------- Hex POI catalogue (PgUp/PgDn filter system) ----------

        /// <summary>Build every POI category once and cache. Called lazily on first
        /// PgUp/PgDn use so the cost is paid only when the user opts into the filter
        /// system. The cache survives until ResetForNewScreen wipes it.</summary>
        private void EnsurePoisBuilt(MapScreenController s)
        {
            if (_poisBuilt) return;
            try
            {
                for (int i = 0; i < _pois.Length; i++) _pois[i] = new List<HexPoi>();

                BuildOurTribePois(_pois[(int)PoiCategory.OurTribe]);
                BuildKnownClansPois(_pois[(int)PoiCategory.KnownClans]);
                BuildFeudsPois(_pois[(int)PoiCategory.Feuds]);
                BuildTradePartnersPois(_pois[(int)PoiCategory.TradePartners]);
                BuildChaosAndGorpPois(_pois[(int)PoiCategory.ChaosAndGorp]);
                BuildWildAndDunesPois(_pois[(int)PoiCategory.WildAndDunes]);
                BuildNamedZonesPois(_pois[(int)PoiCategory.NamedZones]);
                BuildActiveMissionsPois(_pois[(int)PoiCategory.ActiveMissions]);
                BuildExplorationFrontierPois(_pois[(int)PoiCategory.ExplorationFrontier]);
                BuildDeadHexPois(_pois[(int)PoiCategory.DeadHexes]);
                BuildMentionedHexPois(_pois[(int)PoiCategory.MentionedHexes]);

                // Distance pre-compute + sort, ascending. PgDn moves away from home,
                // PgUp toward — matches the "nearest first" intuition.
                for (int i = 0; i < _pois.Length; i++)
                {
                    if ((PoiCategory)i == PoiCategory.AllPois) continue;
                    PrecomputeMilesAndSort(_pois[i]);
                }

                // AllPois — union of every per-clan / per-zone / mission / dead /
                // mentioned hex, deduped by (row, col). Skips ExplorationFrontier
                // (often 30+ entries and more useful as its own explicit category).
                BuildAllPoisUnion(_pois[(int)PoiCategory.AllPois]);
                PrecomputeMilesAndSort(_pois[(int)PoiCategory.AllPois]);

                _poisBuilt = true;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapScreenNav.EnsurePoisBuilt", ex);
            }
        }

        private static void AddPoiFromCenter(List<HexPoi> dest, Vector2Int center, string label)
        {
            try
            {
                HexGrid hex = MapHex.HexAtPoint(new Vector2(center.x, center.y));
                if (hex.row < 0 || hex.column < 0) return;
                dest.Add(new HexPoi { Row = hex.row, Col = hex.column, Label = label ?? "", CachedMiles = -1 });
            }
            catch (Exception ex) { DebugLogger.Error("MapScreenNav.AddPoiFromCenter", ex); }
        }

        private static void BuildOurTribePois(List<HexPoi> dest)
        {
            try
            {
                Clan home = Clan.ClanWithIndex(PlayerClan.index);
                if (!home.isNull) AddPoiFromCenter(dest, home.center, Loc.Get("Home (") + home.name + ")");

                ClanDataList list = new ClanDataList(ClanFilterBy.filter_KnownClans);
                for (int i = 0; i < list.count; i++)
                {
                    Clan c = list[i];
                    if (c.isNull || c.index == PlayerClan.index) continue;
                    if (!c.inOurTribe) continue;
                    AddPoiFromCenter(dest, c.center, c.name);
                }
            }
            catch (Exception ex) { DebugLogger.Error("MapScreenNav.BuildOurTribePois", ex); }
        }

        private static void BuildKnownClansPois(List<HexPoi> dest)
        {
            try
            {
                ClanDataList list = new ClanDataList(ClanFilterBy.filter_KnownClans);
                for (int i = 0; i < list.count; i++)
                {
                    Clan c = list[i];
                    if (c.isNull || c.index == PlayerClan.index) continue;
                    // Clans that fit a dedicated category are routed there instead, so
                    // the "known clans" bucket holds only the neutral remainder.
                    if (c.inOurTribe || c.haveFeud || c.haveTrade) continue;
                    AddPoiFromCenter(dest, c.center, c.name);
                }
            }
            catch (Exception ex) { DebugLogger.Error("MapScreenNav.BuildKnownClansPois", ex); }
        }

        private static void BuildFeudsPois(List<HexPoi> dest)
        {
            try
            {
                ClanDataList list = new ClanDataList(ClanFilterBy.filter_KnownClans);
                for (int i = 0; i < list.count; i++)
                {
                    Clan c = list[i];
                    if (c.isNull || c.index == PlayerClan.index) continue;
                    if (!c.haveFeud) continue;
                    AddPoiFromCenter(dest, c.center, c.name);
                }
            }
            catch (Exception ex) { DebugLogger.Error("MapScreenNav.BuildFeudsPois", ex); }
        }

        private static void BuildTradePartnersPois(List<HexPoi> dest)
        {
            try
            {
                ClanDataList list = new ClanDataList(ClanFilterBy.filter_KnownClans);
                for (int i = 0; i < list.count; i++)
                {
                    Clan c = list[i];
                    if (c.isNull || c.index == PlayerClan.index) continue;
                    if (!c.haveTrade) continue;
                    AddPoiFromCenter(dest, c.center, c.name);
                }
            }
            catch (Exception ex) { DebugLogger.Error("MapScreenNav.BuildTradePartnersPois", ex); }
        }

        private static void BuildChaosAndGorpPois(List<HexPoi> dest)
        {
            AppendClanFilterAsPois(dest, ClanFilterBy.filter_ChaosNest, Loc.Get("Chaos Nest"));
            AppendClanFilterAsPois(dest, ClanFilterBy.filter_GorpedTerritory, Loc.Get("Gorp territory"));
        }

        private static void BuildWildAndDunesPois(List<HexPoi> dest)
        {
            AppendClanFilterAsPois(dest, ClanFilterBy.filter_Uninhabited, Loc.Get("Abandoned land"));
            AppendClanFilterAsPois(dest, ClanFilterBy.filter_Duneland, Loc.Get("Dunes"));
        }

        private static void AppendClanFilterAsPois(List<HexPoi> dest, ClanFilterBy filter, string label)
        {
            try
            {
                ClanDataList list = new ClanDataList(filter);
                for (int i = 0; i < list.count; i++)
                {
                    Clan c = list[i];
                    if (c.isNull) continue;
                    string name = list.count > 1 ? (label + " " + (i + 1)) : label;
                    AddPoiFromCenter(dest, c.center, name);
                }
            }
            catch (Exception ex) { DebugLogger.Error("MapScreenNav.AppendClanFilterAsPois", ex); }
        }

        private static void BuildNamedZonesPois(List<HexPoi> dest)
        {
            try
            {
                int count = PluginImport.MapZone_Count();
                for (int i = 0; i < count; i++)
                {
                    var zone = new MapZone { index = i };
                    if (zone.isClanZone) continue;
                    if ((zone.flags & ZoneFlags.kLabelOnly) != 0) continue;
                    string label = zone.label;
                    string name = zone.name;
                    if (string.IsNullOrEmpty(label) && string.IsNullOrEmpty(name)) continue;
                    if (string.IsNullOrEmpty(label) && IsInternalZoneCode(name)) continue;
                    if (name != null && name.StartsWith("Across", StringComparison.Ordinal)) continue;

                    string display = !string.IsNullOrEmpty(label) ? label : name;
                    Rect bounds = zone.bounds;
                    Vector2 c = new Vector2(bounds.x + bounds.width * 0.5f, bounds.y + bounds.height * 0.5f);
                    try
                    {
                        HexGrid hex = MapHex.HexAtPoint(c);
                        if (hex.row < 0 || hex.column < 0) continue;
                        dest.Add(new HexPoi { Row = hex.row, Col = hex.column, Label = display, CachedMiles = -1 });
                    }
                    catch (Exception ex) { DebugLogger.Error("MapScreenNav.BuildNamedZonesPois.zone", ex); }
                }
            }
            catch (Exception ex) { DebugLogger.Error("MapScreenNav.BuildNamedZonesPois", ex); }
        }

        /// <summary>Iterate every action-mission queue and collect hex positions of
        /// outgoing missions. Game_InitMissionQueue is shared global state primed
        /// per-type; each call wipes the previous queue, so we collect immediately
        /// after each Init. The screen's UpdateUI() re-primes its own missionType
        /// on the next frame, so no state restoration is needed.</summary>
        private static void BuildActiveMissionsPois(List<HexPoi> dest)
        {
            QType[] types = new QType[7]
            {
                QType.type_Raid, QType.type_Emissary, QType.type_Trade, QType.type_War,
                QType.type_Exploration, QType.type_CattleRaid, QType.type_HonorRaid
            };
            for (int t = 0; t < types.Length; t++)
            {
                try
                {
                    int n = PluginImport.Game_InitMissionQueue((int)types[t]);
                    string typeLabel = MissionTypeLabel(types[t]);
                    for (int i = 0; i < n; i++)
                    {
                        float x = PluginImport.Game_MissionQueue_CenterX(i);
                        float y = PluginImport.Game_MissionQueue_CenterY(i);
                        try
                        {
                            HexGrid hex = MapHex.HexAtPoint(new Vector2(x, y));
                            if (hex.row < 0 || hex.column < 0) continue;
                            dest.Add(new HexPoi { Row = hex.row, Col = hex.column, Label = typeLabel, CachedMiles = -1 });
                        }
                        catch (Exception ex) { DebugLogger.Error("MapScreenNav.BuildActiveMissionsPois.hex", ex); }
                    }
                }
                catch (Exception ex) { DebugLogger.Error("MapScreenNav.BuildActiveMissionsPois.type", ex); }
            }
        }

        private static string MissionTypeLabel(QType t)
        {
            switch (t)
            {
                case QType.type_Raid:        return Loc.Get("active raid");
                case QType.type_Emissary:    return Loc.Get("active emissary");
                case QType.type_Trade:       return Loc.Get("active trade mission");
                case QType.type_War:         return Loc.Get("active war party");
                case QType.type_Exploration: return Loc.Get("active exploration");
                case QType.type_CattleRaid:  return Loc.Get("active cattle raid");
                case QType.type_HonorRaid:   return Loc.Get("active honor raid");
                default:                     return Loc.Get("active mission");
            }
        }

        /// <summary>Explored, accessible hexes that have at least one accessible
        /// but unexplored neighbour — the actual edge of the player's knowledge.
        /// Useful for picking foray destinations that push exploration outward.</summary>
        private static void BuildExplorationFrontierPois(List<HexPoi> dest)
        {
            try
            {
                int rows = MapHex.rows;
                int cols = MapHex.columns;
                if (rows <= 0 || cols <= 0) return;
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        if (!MapHex.IsExplored(r, c)) continue;
                        if (MapHex.IsDead(r, c)) continue;
                        if (!HasAccessibleUnexploredNeighbor(r, c, rows, cols)) continue;
                        dest.Add(new HexPoi { Row = r, Col = c, Label = Loc.Get("frontier"), CachedMiles = -1 });
                    }
                }
            }
            catch (Exception ex) { DebugLogger.Error("MapScreenNav.BuildExplorationFrontierPois", ex); }
        }

        /// <summary>True 6-neighbour hex adjacency. MapHex shifts odd rows left
        /// by half a hex (ColumnOffset=-15), so the upper/lower neighbour pair
        /// differs by row parity. Same-row neighbours are always (r, c±1).</summary>
        private static bool HasAccessibleUnexploredNeighbor(int r, int c, int rows, int cols)
        {
            int[] dr = new int[6] { 0, 0, -1, -1, 1, 1 };
            int[] dc;
            if ((r & 1) == 1) dc = new int[6] { -1, 1, -1, 0, -1, 0 };
            else              dc = new int[6] { -1, 1,  0, 1,  0, 1 };

            for (int k = 0; k < 6; k++)
            {
                int nr = r + dr[k];
                int nc = c + dc[k];
                if (nr < 0 || nr >= rows || nc < 0 || nc >= cols) continue;
                if (!MapHex.IsAccessible(nr, nc)) continue;
                if (MapHex.IsExplored(nr, nc)) continue;
                return true;
            }
            return false;
        }

        private static void BuildDeadHexPois(List<HexPoi> dest)
        {
            try
            {
                int rows = MapHex.rows;
                int cols = MapHex.columns;
                if (rows <= 0 || cols <= 0) return;
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                        if (MapHex.IsDead(r, c))
                            dest.Add(new HexPoi { Row = r, Col = c, Label = Loc.Get("dead hex"), CachedMiles = -1 });
            }
            catch (Exception ex) { DebugLogger.Error("MapScreenNav.BuildDeadHexPois", ex); }
        }

        private static void BuildMentionedHexPois(List<HexPoi> dest)
        {
            try
            {
                int rows = MapHex.rows;
                int cols = MapHex.columns;
                if (rows <= 0 || cols <= 0) return;
                // kMentionedHex = 3 (see MapHex constants). Hexes that lore or news
                // referenced but the player has not personally explored.
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                        if (MapHex.Explored(r, c) == 3)
                            dest.Add(new HexPoi { Row = r, Col = c, Label = Loc.Get("mentioned"), CachedMiles = -1 });
            }
            catch (Exception ex) { DebugLogger.Error("MapScreenNav.BuildMentionedHexPois", ex); }
        }

        private void BuildAllPoisUnion(List<HexPoi> dest)
        {
            // Dedupe by packed (row, col). 100_000 column-stride is safe — game maps
            // are well under that width even with margin.
            var seen = new HashSet<long>();
            for (int i = 0; i < _pois.Length; i++)
            {
                PoiCategory cat = (PoiCategory)i;
                if (cat == PoiCategory.AllPois || cat == PoiCategory.ExplorationFrontier) continue;
                var list = _pois[i];
                if (list == null) continue;
                for (int j = 0; j < list.Count; j++)
                {
                    HexPoi p = list[j];
                    long key = (long)p.Row * 100000L + p.Col;
                    if (seen.Contains(key)) continue;
                    seen.Add(key);
                    dest.Add(p);
                }
            }
        }

        private static void PrecomputeMilesAndSort(List<HexPoi> list)
        {
            if (list == null || list.Count == 0) return;
            for (int i = 0; i < list.Count; i++)
            {
                HexPoi p = list[i];
                int miles = -1;
                try
                {
                    Rect r = MapHex.RectFor(p.Row, p.Col);
                    int x = (int)(r.x + r.width * 0.5f);
                    int y = (int)(r.y + r.height * 0.5f);
                    miles = PluginImport.Mission_MilesFromPlayerClan(x, y);
                }
                catch (Exception ex) { DebugLogger.Error("MapScreenNav.PrecomputeMiles", ex); }
                p.CachedMiles = miles;
                list[i] = p;
            }
            list.Sort(ComparePoiByMiles);
        }

        private static int ComparePoiByMiles(HexPoi a, HexPoi b)
        {
            int am = a.CachedMiles < 0 ? int.MaxValue : a.CachedMiles;
            int bm = b.CachedMiles < 0 ? int.MaxValue : b.CachedMiles;
            return am.CompareTo(bm);
        }

        private void CyclePoiCategory(MapScreenController s, int dir)
        {
            int start = (int)_poiCategory;
            // Walk the ring of categories; the first one with entries wins. If none
            // has any (very fresh game or odd save), tell the user and bail.
            for (int step = 1; step <= PoiCategoryCount; step++)
            {
                int cand = ((start + dir * step) % PoiCategoryCount + PoiCategoryCount) % PoiCategoryCount;
                var list = _pois[cand];
                if (list != null && list.Count > 0)
                {
                    _poiCategory = (PoiCategory)cand;
                    _poiIndex = 0;
                    // Single Say call — a separate "Category X, N entries" Say would
                    // be immediately interrupted by the entry announcement, so the
                    // category name has to ride at the head of the same string.
                    JumpToCurrentPoi(s, includeCategoryHeader: true);
                    return;
                }
            }
            ScreenReader.Say(Loc.Get("No points of interest found on this map."));
        }

        private void CyclePoiEntry(MapScreenController s, int dir)
        {
            var list = _pois[(int)_poiCategory];
            if (list == null || list.Count == 0)
            {
                // Current category empty (the user may have switched into it before
                // anything was built, or all entries vanished). Roll forward to find
                // a non-empty one rather than swallowing the keypress silently.
                CyclePoiCategory(s, +1);
                return;
            }
            if (_poiIndex < 0) _poiIndex = 0;
            else
            {
                _poiIndex = (_poiIndex + dir) % list.Count;
                if (_poiIndex < 0) _poiIndex += list.Count;
            }
            JumpToCurrentPoi(s, includeCategoryHeader: false);
        }

        /// <summary>Move the hex cursor to the current POI and announce. When
        /// includeCategoryHeader is true the announcement leads with the category
        /// name and entry count — used when the user explicitly switched categories
        /// so they know what set of entries they're now scrolling through. Everything
        /// is built into a single string because a separate Say call for the header
        /// would be interrupted by the entry announcement that follows it.</summary>
        private void JumpToCurrentPoi(MapScreenController s, bool includeCategoryHeader)
        {
            var list = _pois[(int)_poiCategory];
            if (list == null || list.Count == 0) return;
            if (_poiIndex < 0 || _poiIndex >= list.Count) _poiIndex = 0;
            HexPoi p = list[_poiIndex];
            _hexRow = p.Row;
            _hexCol = p.Col;

            var sb = new StringBuilder();
            if (includeCategoryHeader)
            {
                sb.Append(string.Format(
                    Loc.Get("Category {0}, {1} entries. "),
                    PoiCategoryName(_poiCategory), list.Count));
            }
            sb.Append(string.Format(Loc.Get("{0} of {1}. "), _poiIndex + 1, list.Count));
            if (!string.IsNullOrEmpty(p.Label)) sb.Append(p.Label).Append(". ");
            sb.Append(BuildHexAnnouncement(s));
            ScreenReader.Say(sb.ToString());
        }

        private static string PoiCategoryName(PoiCategory c)
        {
            switch (c)
            {
                case PoiCategory.AllPois:             return Loc.Get("all points of interest");
                case PoiCategory.OurTribe:            return Loc.Get("our tribe");
                case PoiCategory.KnownClans:          return Loc.Get("known clans");
                case PoiCategory.Feuds:               return Loc.Get("feuds");
                case PoiCategory.TradePartners:       return Loc.Get("trade partners");
                case PoiCategory.ChaosAndGorp:        return Loc.Get("chaos and gorp");
                case PoiCategory.WildAndDunes:        return Loc.Get("wildlands and dunes");
                case PoiCategory.NamedZones:          return Loc.Get("named zones");
                case PoiCategory.ActiveMissions:      return Loc.Get("active missions");
                case PoiCategory.ExplorationFrontier: return Loc.Get("exploration frontier");
                case PoiCategory.DeadHexes:           return Loc.Get("dead hexes");
                case PoiCategory.MentionedHexes:      return Loc.Get("mentioned hexes");
                default: return c.ToString();
            }
        }

        private static bool HasCtrlAlt()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        private static bool AnyShift()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        private static bool AnyModifier()
        {
            return HasCtrlAlt() || AnyShift();
        }
    }
}
