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
    /// Zone-based keyboard navigation for the Reorganize dialog.
    ///
    /// The dialog has functional regions that the standard flat Tab nav cannot
    /// model: a 9-segment skill filter that re-sorts the candidate list, the list
    /// of PersonListItems with chief/leader toggles, and the 7-slot ring at the
    /// top. Without per-zone handling the user hears generic Unity GameObject
    /// names like "leaderToggle, on" or "ItemTemplate(Clone)" and never learns
    /// who is in the ring or which sort criterion is active.
    ///
    /// Keys follow the unified Model Y scheme: arrows browse, Space acts on the
    /// focused element (toggle a candidate's ring membership, or jump from a ring
    /// slot to its occupant in the list), D reads details, a blank Enter completes
    /// the screen (Reorganize), and Escape leaves. Tab cycles the List and Ring
    /// zones; F cycles the filter (sort key) in any zone, mirroring RelationsScreen
    /// so the user only ever has one "change sort" key to remember; L jumps back to
    /// the list. D in the list cycles detail facets — the first press combines the
    /// candidate's reorganize advice with their full bio, further presses cycle
    /// between the two facets.
    /// </summary>
    public class ReorganizeNavigator
    {
        private enum Zone { List, Ring }

        // Filter segments — must match the SetSegments call in
        // ReorganizeDialogController.OnShow. First seven map directly onto Skill enum
        // values 1..7 (Bargaining..Magic); index 7 ("A to Z") and 8 ("Deity") are
        // sort-only categories with no skill rating. These English strings double as
        // Loc keys — see FilterLabel.
        private static readonly string[] FilterNames = new[]
        {
            "Bargaining", "Combat", "Diplomacy", "Food",
            "Leadership", "Lore", "Magic", "A to Z", "Deity"
        };
        private const int FilterFirstSkill = 0;
        private const int FilterLastSkill = 6;

        // ringButtons is a private field on ReorganizeDialogController and the only
        // way to read the live working-ring slot occupants without re-implementing
        // the dialog's whole state machine.
        private static FieldInfo _ringButtonsField;

        private Zone _zone = Zone.List;
        private int _listIndex;
        private int _ringIndex;

        // D-cycle detail facets for the focused candidate: 0 = combined (advice
        // then bio), 1 = advice only, 2 = bio only. _detailFacetKey records which
        // person the counter belongs to, so moving the focus to another candidate
        // restarts at the combined facet without the arrow handlers needing to
        // reset anything explicitly.
        private int _detailFacet;
        private int _detailFacetKey = -1;

        // First-interaction marker per zone. While false, the first arrow press
        // announces the cursor's current item without moving — so the user hears
        // Item 0 instead of skipping it. Any item-level action (arrow, Space, C,
        // D) sets the flag to true. Reset on every new dialog open.
        private bool _listFirstAnnounceDone;
        private bool _ringFirstAnnounceDone;

        private readonly ConfirmGate _confirmGate = new ConfirmGate();

        /// <summary>Called when the dialog opens or re-appears. Resets indices so the
        /// next render starts in list mode on the first candidate.</summary>
        public void ResetForNewScreen()
        {
            _zone = Zone.List;
            // Start at 0 so direct actions like Space/C land on the first candidate
            // without needing a prior arrow press. The _listFirstAnnounceDone flag
            // makes the first arrow announce Item 0 instead of jumping past it.
            _listIndex = 0;
            _ringIndex = 0;
            _detailFacet = 0;
            _detailFacetKey = -1;
            _listFirstAnnounceDone = false;
            _ringFirstAnnounceDone = false;
            _confirmGate.Reset();
        }

        /// <summary>Top-level dispatch — called every Update tick while the dialog is active.</summary>
        public void HandleInput(ReorganizeDialogController d)
        {
            if (d == null) return;

            // Enter — primary action (Reorganize), the one key that completes the
            // screen. Model Y: a blank Enter is the universal screen-completion key,
            // handled globally so it works from every zone. No modifier is required.
            // The action button stays disabled until the working ring is valid and
            // actually changed (ValidateReorganizeButton), so a stray Enter is safe.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TryReorganize(d);
                return;
            }

            // Escape — discard changes and close.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TryClose(d);
                return;
            }

            // Tab / Shift+Tab — switch zones. Drops any pending Enter confirmation
            // (zone change is a context shift; the user should re-hear the summary).
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _confirmGate.Reset();
                int dir = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? -1 : 1;
                CycleZone(dir);
                AnnounceZone(d);
                return;
            }

            // F / Shift+F — cycle filter (sort criterion). Always-active across zones.
            if (Input.GetKeyDown(KeyCode.F) && !HasCtrlAlt())
            {
                _confirmGate.Reset();
                int dir = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? -1 : 1;
                CycleFilter(d, dir);
                return;
            }

            // L — jump back to List zone from anywhere.
            if (Input.GetKeyDown(KeyCode.L) && !AnyModifier())
            {
                _confirmGate.Reset();
                _zone = Zone.List;
                AnnounceCurrentListItem(d);
                return;
            }

            switch (_zone)
            {
                case Zone.List:   HandleListInput(d);   break;
                case Zone.Ring:   HandleRingInput(d);   break;
            }
        }

        // ---------- Zone selection ----------

        private void CycleZone(int direction)
        {
            int z = (int)_zone + direction;
            if (z < 0) z = 1;
            if (z > 1) z = 0;
            _zone = (Zone)z;
        }

        private void AnnounceZone(ReorganizeDialogController d)
        {
            switch (_zone)
            {
                case Zone.List:
                    ScreenReader.Say(Loc.Get("Candidate list."));
                    AnnounceCurrentListItem(d);
                    break;
                case Zone.Ring:
                    ScreenReader.Say(Loc.Get("Ring positions."));
                    AnnounceCurrentRingPosition(d);
                    break;
            }
        }

        // ---------- Filter cycle (F) ----------

        private void CycleFilter(ReorganizeDialogController d, int direction)
        {
            if (d.filter == null) return;
            int v = d.filter.value + direction;
            if (v < 0) v = FilterNames.Length - 1;
            if (v >= FilterNames.Length) v = 0;
            d.filter.SetValue(v);

            int candidates = d.list != null ? d.list.count : 0;
            ScreenReader.Say(Loc.Get("Sort by ") + FilterLabel(v) + ". "
                + candidates + Loc.Get(" candidates."));
            // Re-sort scrambles the ordering — drop focus so the next Down arrow
            // lands on the (now-new) first candidate instead of skipping it, and
            // drop the detail-cycle anchor so the next D starts at the combined facet.
            _listIndex = -1;
            _detailFacetKey = -1;
        }

        // ---------- List zone ----------

        private void HandleListInput(ReorganizeDialogController d)
        {
            if (d.list == null || d.list.count == 0) return;

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _confirmGate.Reset();
                if (_listFirstAnnounceDone)
                {
                    _listIndex--;
                    if (_listIndex < 0) _listIndex = d.list.count - 1;
                }
                AnnounceCurrentListItem(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _confirmGate.Reset();
                if (_listFirstAnnounceDone)
                {
                    _listIndex++;
                    if (_listIndex >= d.list.count) _listIndex = 0;
                }
                AnnounceCurrentListItem(d);
                return;
            }

            // Space — toggle ring membership (leaderToggle), the focused row's
            // main toggle.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _listFirstAnnounceDone = true;
                ToggleLeaderForCurrent(d);
                return;
            }

            // C — toggle chief (chiefToggle), the row's secondary toggle.
            if (Input.GetKeyDown(KeyCode.C) && !AnyModifier())
            {
                _listFirstAnnounceDone = true;
                ToggleChiefForCurrent(d);
                return;
            }

            // D — detail cycle: first press combines this candidate's reorganize
            // advice with their full bio, further presses cycle between the two.
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                _listFirstAnnounceDone = true;
                CycleDetail(d);
                return;
            }
        }

        private void AnnounceCurrentListItem(ReorganizeDialogController d)
        {
            if (d.list == null || d.list.count == 0)
            {
                ScreenReader.Say(Loc.Get("No candidates available."));
                return;
            }
            if (_listIndex >= d.list.count) _listIndex = d.list.count - 1;
            if (_listIndex < 0) _listIndex = 0;

            var item = d.list[_listIndex] as PersonListItem;
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("Empty slot."));
                return;
            }
            ScreenReader.Say(BuildPersonSummary(item, d));
            _listFirstAnnounceDone = true;
        }

        private string BuildPersonSummary(PersonListItem item, ReorganizeDialogController d)
        {
            if (item == null) return Loc.Get("Empty slot.");

            var sb = new StringBuilder();

            Person person = PlayerClan.PersonWithIndex(item.key);
            sb.Append(person.name);

            // Working-ring status right after the name — placed early so it is
            // heard even when fast arrow browsing interrupts the announcement.
            // Toggles reflect the uncommitted (working) state.
            bool isChief = item.chiefToggle != null && item.chiefToggle.isOn;
            bool isInRing = item.leaderToggle != null && item.leaderToggle.isOn;
            if (isChief)
                sb.Append(Loc.Get(", chief"));
            else if (isInRing)
                sb.Append(Loc.Get(", in ring"));

            string family = person.familyName;
            if (!string.IsNullOrEmpty(family))
                sb.Append(", ").Append(family).Append(Loc.Get(" family"));

            Deity deity = person.deity;
            if (deity != Deity.deity_None && deity != Deity.deity_Shaman)
                sb.Append(Loc.Get(", devotee of ")).Append(Game.NameOfDeity(deity));

            // Sort skill — only when a real skill (not A-Z / Deity) is the active filter.
            int filterValue = d.filter != null ? d.filter.value : -1;
            if (filterValue >= FilterFirstSkill && filterValue <= FilterLastSkill)
            {
                Skill sortSkill = (Skill)(filterValue + 1);
                double rating = person.PersonSkill(sortSkill);
                if (rating > 1.4)
                {
                    int adj = (int)rating - 1;
                    if (adj < 0) adj = 0;
                    if (adj > 5) adj = 5;
                    // Separator between skill name and rating: ": " reads more
                    // naturally than a bare space — "Anführerschaft: Berühmt"
                    // vs. the ambiguous "Anführerschaft Berühmt" which sounds
                    // like an unfinished noun phrase in German. Equivalent
                    // semantic reading in English ("Leadership: Famed").
                    sb.Append(", ").Append(FilterLabel(filterValue)).Append(": ")
                      .Append(Loc.Get(SkillAdjective(adj)));
                }
            }

            // Eligibility — the game hides the toggle when ineligible, so we need to
            // tell the user explicitly that Space/C will refuse.
            if (person.isIneligible)
                sb.Append(Loc.Get(", ineligible"));
            else if (person.ineligibleChief && !isChief)
                sb.Append(Loc.Get(", cannot be chief"));

            return sb.ToString();
        }

        private void ToggleLeaderForCurrent(ReorganizeDialogController d)
        {
            var item = SafeListItem(d, _listIndex);
            if (item == null || item.leaderToggle == null) return;

            if (!item.leaderToggle.gameObject.activeSelf)
            {
                ScreenReader.Say(Loc.Get("This person is ineligible for the ring."));
                return;
            }
            if (!item.leaderToggle.interactable)
            {
                ScreenReader.Say(Loc.Get("Ring is full. Remove someone else first."));
                return;
            }

            string nameOnly = PlayerClan.PersonWithIndex(item.key).name;
            bool wasOn = item.leaderToggle.isOn;
            item.leaderToggle.Set(!wasOn, sendCallback: true);

            // Set fires onValueChanged → OnLeaderToggled → RingCheckedFor → UpdateRing.
            // UpdateRing may refuse the change in edge cases; read the resulting state.
            if (item.leaderToggle.isOn != wasOn) _confirmGate.Reset();
            if (item.leaderToggle.isOn)
                ScreenReader.Say(nameOnly + Loc.Get(" added to ring."));
            else
                ScreenReader.Say(nameOnly + Loc.Get(" removed from ring."));
        }

        private void ToggleChiefForCurrent(ReorganizeDialogController d)
        {
            ToggleChiefOnItem(SafeListItem(d, _listIndex));
        }

        /// <summary>
        /// Flip the chief flag on <paramref name="item"/>. Shared by the List zone
        /// (C on the focused candidate) and the Ring zone (C on the focused ring
        /// slot — the slot's occupant is looked up in the candidate list first
        /// so the same UI toggle and game-side callback chain run as in the list).
        /// </summary>
        private void ToggleChiefOnItem(PersonListItem item)
        {
            if (item == null || item.chiefToggle == null) return;

            if (!item.chiefToggle.gameObject.activeSelf)
            {
                ScreenReader.Say(Loc.Get("This person cannot be chief."));
                return;
            }
            if (!item.chiefToggle.interactable)
            {
                ScreenReader.Say(Loc.Get("Another person is already chief. Remove the current chief first."));
                return;
            }

            string nameOnly = PlayerClan.PersonWithIndex(item.key).name;
            bool wasOn = item.chiefToggle.isOn;
            item.chiefToggle.Set(!wasOn, sendCallback: true);

            if (item.chiefToggle.isOn != wasOn) _confirmGate.Reset();
            if (item.chiefToggle.isOn)
                ScreenReader.Say(nameOnly + Loc.Get(" is now chief."));
            else
                ScreenReader.Say(nameOnly + Loc.Get(" is no longer chief."));
        }

        /// <summary>
        /// Find the PersonListItem for the person occupying ring slot
        /// <paramref name="ringIdx"/>. Returns null when the slot is empty or
        /// the occupant isn't in the current candidate list (rare — only some
        /// filter modes hide ring members from the list).
        /// </summary>
        private static PersonListItem FindListItemForRingPosition(ReorganizeDialogController d, int ringIdx)
        {
            FaceButton[] ring = GetRingButtons(d);
            if (ring == null || ringIdx < 0 || ringIdx >= ring.Length || ring[ringIdx] == null) return null;
            int personIdx = ring[ringIdx].personIndex;
            if (personIdx <= 0 || d.list == null) return null;
            for (int i = 0; i < d.list.count; i++)
            {
                if (d.list[i] != null && d.list[i].key == personIdx)
                    return d.list[i] as PersonListItem;
            }
            return null;
        }

        // ---------- Detail cycle (D) ----------

        /// <summary>
        /// D in the candidate list. First press on a person reads the combined
        /// facet (reorganize advice, then the full bio); each further press on the
        /// same person cycles between advice-only and bio-only. Moving the focus to
        /// another candidate restarts at the combined facet.
        /// </summary>
        private void CycleDetail(ReorganizeDialogController d)
        {
            var item = SafeListItem(d, _listIndex);
            if (item == null) return;
            int key = item.key;
            Person person = PlayerClan.PersonWithIndex(key);

            if (key != _detailFacetKey)
            {
                _detailFacetKey = key;
                _detailFacet = 0;
            }
            else
            {
                _detailFacet++;
                if (_detailFacet > 2) _detailFacet = 1;
            }

            bool wantAdvice = _detailFacet != 2;
            bool wantBio = _detailFacet != 1;

            var sb = new StringBuilder();
            if (wantAdvice)
            {
                string advice = BuildAdvice(person, key);
                sb.Append(advice);
                // Join the two facets without doubling a sentence-ending period.
                if (wantBio && advice.Length > 0)
                {
                    char last = advice[advice.Length - 1];
                    sb.Append(last == '.' || last == '!' || last == '?' ? " " : ". ");
                }
            }
            if (wantBio) sb.Append(BuildBio(person));
            ScreenReader.Say(sb.ToString());
        }

        /// <summary>
        /// The reorganize advice this candidate would give. The game shows it in a
        /// popup via ReorganizeDialogController.LeaderChosen, gated on the person
        /// being at home; we read the advisor statement directly (same source the
        /// popup uses) so the whole detail facet stays one announcement.
        /// </summary>
        private static string BuildAdvice(Person person, int key)
        {
            if (person.location != PersonLocation.kHome)
                return person.name + Loc.Get(" is away — no advice available.");

            string advice = null;
            try { advice = PlayerClan.ReorganizeStatementForAdvisor(key); }
            catch (Exception ex) { DebugLogger.Error("ReorganizeNav.BuildAdvice", ex); }

            if (string.IsNullOrEmpty(advice))
                return person.name + Loc.Get(" has no advice to give.");
            advice = StringHelpers.StripTags(advice).Replace("\n", ". ");
            if (string.IsNullOrEmpty(advice))
                return person.name + Loc.Get(" has no advice to give.");
            return advice;
        }

        /// <summary>Full localized dossier for the person (skills + age + deity + health).</summary>
        private static string BuildBio(Person person)
        {
            // 95 = name + deity + skills + age + location + health. PersonBio is a
            // localized port of the game's English-only Person.AttributedTextFor —
            // see PersonBio for why AttributedTextFor cannot be used directly. The
            // health bit surfaces "Sick" / "Wounded" / "Dead" when picking ring
            // members and council seats.
            return PersonBio.Localized(person, 95);
        }

        // ---------- Ring zone ----------

        private void HandleRingInput(ReorganizeDialogController d)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                _confirmGate.Reset();
                if (_ringFirstAnnounceDone)
                {
                    _ringIndex--;
                    if (_ringIndex < 0) _ringIndex = 6;
                }
                AnnounceCurrentRingPosition(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                _confirmGate.Reset();
                if (_ringFirstAnnounceDone)
                {
                    _ringIndex++;
                    if (_ringIndex > 6) _ringIndex = 0;
                }
                AnnounceCurrentRingPosition(d);
                return;
            }

            // Space — act on the focused ring slot: jump to its occupant in the
            // candidate list, where the user can toggle them out.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _ringFirstAnnounceDone = true;
                JumpToRingPositionInList(d);
                return;
            }

            // C — toggle chief on the slot's occupant without leaving the ring
            // view. Uses the same toggle path as the List zone (the chiefToggle
            // lives on the PersonListItem, so we find it via the slot's person
            // index). The game's own validation (radio-style, requires removing
            // an existing chief first) still applies.
            if (Input.GetKeyDown(KeyCode.C) && !AnyModifier())
            {
                _ringFirstAnnounceDone = true;
                var item = FindListItemForRingPosition(d, _ringIndex);
                if (item == null)
                    ScreenReader.Say(Loc.Get("Slot is empty."));
                else
                    ToggleChiefOnItem(item);
                return;
            }

            // D — re-read the focused ring position.
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                _ringFirstAnnounceDone = true;
                AnnounceCurrentRingPosition(d);
                return;
            }
        }

        private void AnnounceCurrentRingPosition(ReorganizeDialogController d)
        {
            if (_ringIndex < 0) _ringIndex = 0;
            if (_ringIndex > 6) _ringIndex = 6;

            FaceButton[] ring = GetRingButtons(d);
            int personIdx = (ring != null && _ringIndex < ring.Length && ring[_ringIndex] != null)
                ? ring[_ringIndex].personIndex
                : -1;

            string label = (_ringIndex == 0)
                ? Loc.Get("Chief slot")
                : Loc.Get("Ring slot ") + _ringIndex;
            if (personIdx > 0)
                ScreenReader.Say(label + ": " + PluginImport.PC_PersonName(personIdx)
                    + Loc.Get(". Press Space to find them in the list."));
            else
                ScreenReader.Say(label + Loc.Get(": empty."));
            _ringFirstAnnounceDone = true;
        }

        private void JumpToRingPositionInList(ReorganizeDialogController d)
        {
            FaceButton[] ring = GetRingButtons(d);
            if (ring == null || _ringIndex >= ring.Length || ring[_ringIndex] == null) return;
            int personIdx = ring[_ringIndex].personIndex;
            if (personIdx <= 0)
            {
                ScreenReader.Say(Loc.Get("Slot is empty."));
                return;
            }
            d.list.SelectItemWithKey(personIdx);
            for (int i = 0; i < d.list.count; i++)
            {
                if (d.list[i] != null && d.list[i].key == personIdx)
                {
                    _listIndex = i;
                    break;
                }
            }
            _zone = Zone.List;
            AnnounceCurrentListItem(d);
        }

        // ---------- Primary action (Enter) and Close (Esc) ----------

        private void TryReorganize(ReorganizeDialogController d)
        {
            if (d.actionButton == null || !d.actionButton.interactable)
            {
                _confirmGate.Reset();
                ScreenReader.Say(Loc.Get("Reorganize is not available yet. Ring is incomplete or unchanged."));
                return;
            }
            if (_confirmGate.RequestOrConfirm(BuildReorganizeSummary(d)))
                SubmitButton(d.actionButton);
        }

        /// <summary>
        /// Build the spoken summary for the two-step Enter confirmation. Names come
        /// from the working ring (uncommitted state) — slot 0 is the chief; slots
        /// 1–6 are the other ring members. The wording adapts so the announcement
        /// reads naturally for the common case of "chief plus others" and the edge
        /// cases of chief-only or no-chief-but-ring (the action button is disabled
        /// when the working ring is empty or unchanged, so a fully-empty path
        /// shouldn't normally reach this method).
        /// </summary>
        private static string BuildReorganizeSummary(ReorganizeDialogController d)
        {
            FaceButton[] ring = GetRingButtons(d);
            string chiefName = null;
            var others = new List<string>();

            if (ring != null)
            {
                for (int i = 0; i < ring.Length && i < 7; i++)
                {
                    if (ring[i] == null) continue;
                    int pIdx = ring[i].personIndex;
                    if (pIdx <= 0) continue;
                    string name = PluginImport.PC_PersonName(pIdx);
                    if (string.IsNullOrEmpty(name)) continue;
                    if (i == 0) chiefName = name;
                    else others.Add(name);
                }
            }

            string joinedOthers = StringHelpers.JoinList(others, Loc.Get("and"));

            if (!string.IsNullOrEmpty(chiefName) && others.Count > 0)
                return string.Format(Loc.Get("You choose {0} as chief, with {1} in the ring."),
                    chiefName, joinedOthers);
            if (!string.IsNullOrEmpty(chiefName))
                return string.Format(Loc.Get("You choose {0} as chief."), chiefName);
            if (others.Count > 0)
                return string.Format(Loc.Get("You put {0} in the ring."), joinedOthers);
            return Loc.Get("You change nothing.");
        }

        private static void TryClose(ReorganizeDialogController d)
        {
            var closeBtn = FindButtonByName(d, "CloseButton") ?? FindButtonByName(d, "CloseButton2");
            if (closeBtn != null) SubmitButton(closeBtn);
            else ScreenReader.Say(Loc.Get("Close button not found."));
        }

        private static void SubmitButton(UIButton button)
        {
            if (button == null) return;
            ISubmitHandler handler = button as ISubmitHandler;
            if (handler == null) return;
            handler.OnSubmit(new BaseEventData(EventSystem.current));
        }

        // ---------- Full status (F5) ----------

        public void AnnounceFullStatus(ReorganizeDialogController d)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Reorganize. "));

            FaceButton[] ring = GetRingButtons(d);
            sb.Append(Loc.Get("Working ring: "));
            for (int i = 0; i < 7; i++)
            {
                int personIdx = (ring != null && i < ring.Length && ring[i] != null)
                    ? ring[i].personIndex
                    : -1;
                if (personIdx > 0)
                    sb.Append(PluginImport.PC_PersonName(personIdx));
                else
                    sb.Append(Loc.Get("empty"));
                if (i == 0) sb.Append(Loc.Get(" (chief)"));
                if (i < 6) sb.Append(", ");
            }
            sb.Append(". ");

            if (d.ringInfo != null)
            {
                string info = StringHelpers.StripTags(d.ringInfo.text);
                if (!string.IsNullOrEmpty(info))
                    sb.Append(info).Append(". ");
            }

            int filterValue = d.filter != null ? d.filter.value : -1;
            if (filterValue >= 0 && filterValue < FilterNames.Length)
                sb.Append(Loc.Get("Sort by ")).Append(FilterLabel(filterValue)).Append(". ");

            sb.Append(IsActionEnabled(d)
                ? Loc.Get("Press Enter to reorganize. Escape to discard.")
                : Loc.Get("Reorganize disabled. Ring is incomplete or unchanged."));

            ScreenReader.Say(sb.ToString());
        }

        // ---------- Helpers ----------

        private static PersonListItem SafeListItem(ReorganizeDialogController d, int idx)
        {
            if (d.list == null || idx < 0 || idx >= d.list.count) return null;
            return d.list[idx] as PersonListItem;
        }

        private static FaceButton[] GetRingButtons(ReorganizeDialogController d)
        {
            try
            {
                // Unity 2018 Mono lacks FieldInfo.op_Equality / op_Inequality, so direct
                // `_ringButtonsField == null` throws MissingMethodException at JIT time.
                // Cast to object to use reference equality instead.
                if ((object)_ringButtonsField == null)
                {
                    _ringButtonsField = typeof(ReorganizeDialogController).GetField(
                        "ringButtons", BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if ((object)_ringButtonsField == null) return null;
                return _ringButtonsField.GetValue(d) as FaceButton[];
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ReorganizeNav.GetRingButtons", ex);
                return null;
            }
        }

        private static UIButton FindButtonByName(ReorganizeDialogController d, string name)
        {
            var buttons = d.gameObject.GetComponentsInChildren<UIButton>(includeInactive: false);
            foreach (var b in buttons)
            {
                if (b == null) continue;
                if (b.gameObject.name == name) return b;
            }
            return null;
        }

        private static bool IsActionEnabled(ReorganizeDialogController d)
        {
            return d.actionButton != null && d.actionButton.interactable;
        }

        /// <summary>Localized label for a filter segment (also used as the Loc key).</summary>
        private static string FilterLabel(int v)
        {
            if (v < 0 || v >= FilterNames.Length) return "";
            return Loc.Get(FilterNames[v]);
        }

        /// <summary>English skill-rating adjective; also the Loc key for the German value.</summary>
        private static string SkillAdjective(int adj)
        {
            switch (adj)
            {
                case 0: return "Fair";
                case 1: return "Good";
                case 2: return "Very Good";
                case 3: return "Excellent";
                case 4: return "Renowned";
                default: return "Heroic";
            }
        }

        private static bool HasCtrlAlt()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        private static bool AnyModifier()
        {
            return HasCtrlAlt()
                || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
    }
}
