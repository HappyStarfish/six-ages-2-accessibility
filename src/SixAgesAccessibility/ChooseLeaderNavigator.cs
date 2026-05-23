using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Single-zone keyboard navigation for the ChooseLeaderDialog.
    ///
    /// The dialog opens whenever a scene returns kChooseLeader or a management
    /// dialog (Emissary / Caravan / Raid / Venture / Reorganize) routes through
    /// its leader picker. It contains a 7-segment skill filter and a UIList of
    /// PersonListItems. Without per-screen handling the user only hears generic
    /// GameObject names like "ItemTemplate(Clone)" with no person name attached,
    /// which makes the dialog unusable — and it is game-blocking whenever the
    /// script demands a leader selection.
    ///
    /// Per the global action-key pattern (memory: feedback_dialog_action_pattern):
    /// the Choose button is NOT a separate Tab zone. The user browses with
    /// arrow keys, selects a candidate with Space, and commits with a blank
    /// Enter. The Close button (only visible in RitualDialog view-only mode) is
    /// reached with Escape, mirroring its semantic role as "leave without acting."
    /// </summary>
    public class ChooseLeaderNavigator
    {
        /// <summary>True when the dialog is in view-only mode (RitualDialogController
        /// calls SetViewOnly after base.ChooseLeader). In that mode the Choose button
        /// is hidden and the leader is auto-picked by the game from PC_BestPerson —
        /// the dialog is only there to let the user inspect qualified candidates.
        /// Selecting an item still works and updates the list highlight, but does
        /// nothing useful gameplay-wise; the user should browse and press Escape.</summary>
        private static bool IsViewOnly(ChooseLeaderDialog d)
        {
            if (d == null) return false;
            // SetViewOnly hides chooseButton via SetActive(false). closeButton is
            // active only in view-only mode (OnShow defaults it to inactive).
            bool chooseHidden = d.chooseButton == null || !d.chooseButton.gameObject.activeSelf;
            bool closeShown = d.closeButton != null && d.closeButton.gameObject.activeSelf;
            return chooseHidden && closeShown;
        }

        // Filter segments — match the SetSegments call in ChooseLeaderDialog.ShowFor.
        // These map directly onto Skill enum values 1..7 (Bargaining..Magic).
        private static readonly string[] FilterNames = new[]
        {
            "Bargaining", "Combat", "Diplomacy", "Food",
            "Leadership", "Lore", "Magic"
        };

        private int _listIndex;
        private readonly ConfirmGate _confirmGate = new ConfirmGate();

        /// <summary>Called when the dialog opens. Resets index so the first arrow
        /// press lands on item 0 (or last item, going up).</summary>
        public void ResetForNewScreen()
        {
            // -1 means "no focus yet" — the first Down/Up press lands on item 0
            // / last item without skipping it (starting at 0 would silently
            // bypass index 0 because the first Down increments to 1).
            _listIndex = -1;
            _confirmGate.Reset();
        }

        /// <summary>Top-level dispatch — called every Update tick while the dialog is active.</summary>
        public void HandleInput(ChooseLeaderDialog d)
        {
            if (d == null) return;

            // Enter — primary action (Choose), the one key that completes the
            // screen. Model Y: a blank Enter is the universal screen-completion
            // key. The Choose button stays disabled until a candidate is
            // selected, so a stray Enter just reports that and is harmless.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TryChoose(d);
                return;
            }

            // Escape — leave the dialog. Only meaningful in view-only mode where
            // closeButton exists; in normal mode the dialog forces a selection.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TryClose(d);
                return;
            }

            // F / Shift+F — cycle filter (sort criterion).
            if (Input.GetKeyDown(KeyCode.F) && !HasCtrlAlt())
            {
                _confirmGate.Reset();
                int dir = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? -1 : 1;
                CycleFilter(d, dir);
                return;
            }

            if (d.list == null || d.list.count == 0) return;

            if (Input.GetKeyDown(KeyCode.UpArrow) && !AnyModifier())
            {
                _confirmGate.Reset();
                _listIndex--;
                if (_listIndex < 0) _listIndex = d.list.count - 1;
                AnnounceCurrentListItem(d);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow) && !AnyModifier())
            {
                _confirmGate.Reset();
                _listIndex++;
                if (_listIndex >= d.list.count) _listIndex = 0;
                AnnounceCurrentListItem(d);
                return;
            }

            // Space — select this person (enables the Choose button). Enter is
            // reserved for the Choose action itself (handled above).
            if (Input.GetKeyDown(KeyCode.Space) && !AnyModifier())
            {
                _confirmGate.Reset();
                SelectCurrent(d);
                return;
            }

            // D — full attributed bio (skills + age + deity + location).
            if (Input.GetKeyDown(KeyCode.D) && !AnyModifier())
            {
                AnnounceFullPersonInfo(d);
                return;
            }
        }

        // ---------- Filter cycle (F) ----------

        private void CycleFilter(ChooseLeaderDialog d, int direction)
        {
            if (d.filter == null) return;
            int v = d.filter.value + direction;
            if (v < 0) v = FilterNames.Length - 1;
            if (v >= FilterNames.Length) v = 0;
            d.filter.SetValue(v);

            int candidates = d.list != null ? d.list.count : 0;
            ScreenReader.Say(Loc.Get("Sort by ") + Loc.Get(FilterNames[v]) + ". " + candidates + Loc.Get(" candidates."));
            // Re-sort scrambles the ordering — drop focus so the next Down arrow
            // lands on the (now-new) first candidate instead of skipping it.
            _listIndex = -1;
        }

        // ---------- List browsing ----------

        private void AnnounceCurrentListItem(ChooseLeaderDialog d)
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
        }

        private string BuildPersonSummary(PersonListItem item, ChooseLeaderDialog d)
        {
            if (item == null) return Loc.Get("Empty slot.");

            var sb = new StringBuilder();

            Person person = PlayerClan.PersonWithIndex(item.key);
            sb.Append(person.name);

            // Selected marker right after the name — heard even when fast arrow
            // browsing interrupts the rest of the announcement.
            if (item.isSelected)
                sb.Append(Loc.Get(", selected"));

            string family = person.familyName;
            if (!string.IsNullOrEmpty(family))
                sb.Append(", ").Append(family).Append(Loc.Get(" family"));

            Deity deity = person.deity;
            if (deity != Deity.deity_None && deity != Deity.deity_Shaman)
                sb.Append(Loc.Get(", devotee of ")).Append(Game.NameOfDeity(deity));

            // Sort skill — 0..6 always maps to a real skill in this dialog.
            int filterValue = d.filter != null ? d.filter.value : -1;
            if (filterValue >= 0 && filterValue < FilterNames.Length)
            {
                Skill sortSkill = (Skill)(filterValue + 1);
                double rating = person.PersonSkill(sortSkill);
                if (rating > 1.4)
                {
                    int adj = (int)rating - 1;
                    if (adj < 0) adj = 0;
                    if (adj > 5) adj = 5;
                    sb.Append(", ").Append(Loc.Get(FilterNames[filterValue])).Append(" ").Append(SkillAdjective(adj));
                }
            }

            // Ring status — committed ring (PC_RingPerson 1..7); ring leader = pos 1.
            string ringStatus = GetRingStatus(item.key);
            if (!string.IsNullOrEmpty(ringStatus))
                sb.Append(", ").Append(ringStatus);

            return sb.ToString();
        }

        private static string GetRingStatus(int personIndex)
        {
            if (personIndex <= 0) return null;
            try
            {
                for (int pos = 1; pos <= 7; pos++)
                {
                    int idx = PluginImport.PC_RingPerson(pos);
                    if (idx == personIndex)
                        return Loc.Get((pos == 1) ? "ring leader" : "ring member");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ChooseLeaderNav.GetRingStatus", ex);
            }
            return null;
        }

        private void SelectCurrent(ChooseLeaderDialog d)
        {
            if (d.list == null || _listIndex < 0 || _listIndex >= d.list.count) return;
            var item = d.list[_listIndex] as PersonListItem;
            if (item == null) return;

            string nameOnly = PlayerClan.PersonWithIndex(item.key).name;
            bool viewOnly = IsViewOnly(d);

            // Enter / Space should make the item selected (enabling the Choose
            // button) without deselecting on a second press. UIList.OnItemClicked
            // toggles isSelected when disableDeselect is false, so guard.
            if (item.isSelected)
            {
                ScreenReader.Say(viewOnly
                    ? nameOnly + Loc.Get(" is highlighted. View only — leader is chosen automatically. Escape to return.")
                    : nameOnly + Loc.Get(" is already selected. Press Enter to choose."));
                return;
            }

            d.list.OnItemClicked(item);
            ScreenReader.Say(viewOnly
                ? nameOnly + Loc.Get(" highlighted. View only — leader is chosen automatically. Escape to return.")
                : nameOnly + Loc.Get(" selected. Press Enter to choose."));
        }

        private void AnnounceFullPersonInfo(ChooseLeaderDialog d)
        {
            if (d.list == null || _listIndex < 0 || _listIndex >= d.list.count) return;
            var item = d.list[_listIndex] as PersonListItem;
            if (item == null) return;
            Person person = PlayerClan.PersonWithIndex(item.key);
            // 95 = name + deity + skills + age + location + health. PersonBio is a
            // localized port of the game's English-only Person.AttributedTextFor; the
            // health bit surfaces "Sick" / "Wounded" / "Dead", important for leader
            // picks where a wounded candidate is a worse choice.
            ScreenReader.Say(PersonBio.Localized(person, 95));
        }

        // ---------- Primary action (Enter) and Close (Esc) ----------

        private void TryChoose(ChooseLeaderDialog d)
        {
            if (IsViewOnly(d))
            {
                _confirmGate.Reset();
                ScreenReader.Say(Loc.Get("View only — the leader is chosen automatically. Press Escape to return."));
                return;
            }
            if (d.chooseButton == null || !d.chooseButton.gameObject.activeSelf)
            {
                _confirmGate.Reset();
                ScreenReader.Say(Loc.Get("Choose is not available in this dialog."));
                return;
            }
            if (!d.chooseButton.interactable)
            {
                _confirmGate.Reset();
                ScreenReader.Say(Loc.Get("Choose is not available yet. Select a candidate first."));
                return;
            }
            if (_confirmGate.RequestOrConfirm(BuildChooseSummary(d)))
                SubmitButton(d.chooseButton);
        }

        /// <summary>
        /// Build the spoken summary for the two-step Enter confirmation. The dialog
        /// is single-pick (one selected PersonListItem in the list), so the summary
        /// only ever names that one person.
        /// </summary>
        private static string BuildChooseSummary(ChooseLeaderDialog d)
        {
            if (d.list == null) return Loc.Get("You choose no one.");
            for (int i = 0; i < d.list.count; i++)
            {
                var item = d.list[i] as PersonListItem;
                if (item == null) continue;
                if (item.isSelected)
                {
                    string name = PlayerClan.PersonWithIndex(item.key).name;
                    return string.Format(Loc.Get("You choose {0} as leader."), name);
                }
            }
            return Loc.Get("You choose no one.");
        }

        private void TryClose(ChooseLeaderDialog d)
        {
            if (d.closeButton == null || !d.closeButton.gameObject.activeSelf)
            {
                // Normal (non-view-only) mode forces a selection — there is no
                // close path. Tell the user instead of silently swallowing Esc.
                ScreenReader.Say(Loc.Get("This dialog requires a leader. Pick one and press Enter."));
                return;
            }
            SubmitButton(d.closeButton);
        }

        private static void SubmitButton(UIButton button)
        {
            if (button == null) return;
            ISubmitHandler handler = button as ISubmitHandler;
            if (handler == null) return;
            handler.OnSubmit(new BaseEventData(EventSystem.current));
        }

        // ---------- Full status (F5) ----------

        public void AnnounceFullStatus(ChooseLeaderDialog d)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Choose Leader. "));
            bool viewOnly = IsViewOnly(d);

            string caption = d.caption != null ? d.caption.text : null;
            if (!string.IsNullOrEmpty(caption))
                sb.Append(caption).Append(". ");

            int candidates = d.list != null ? d.list.count : 0;
            sb.Append(candidates).Append(Loc.Get(" candidates. "));

            if (viewOnly)
            {
                sb.Append(Loc.Get("View only — leader is chosen automatically. "));
            }
            else if (d.list != null && d.list.selectedItem != null)
            {
                Person person = PlayerClan.PersonWithIndex(d.list.selectedItem.key);
                sb.Append(Loc.Get("Selected: ")).Append(person.name).Append(". ");
            }
            else
            {
                sb.Append(Loc.Get("No leader chosen. "));
            }

            int filterValue = d.filter != null ? d.filter.value : -1;
            if (filterValue >= 0 && filterValue < FilterNames.Length)
                sb.Append(Loc.Get("Sort by ")).Append(Loc.Get(FilterNames[filterValue])).Append(". ");

            if (viewOnly)
            {
                sb.Append(Loc.Get("Press Escape to return."));
            }
            else
            {
                bool canChoose = d.chooseButton != null && d.chooseButton.gameObject.activeSelf
                                 && d.chooseButton.interactable;
                sb.Append(canChoose ? Loc.Get("Press Enter to choose.") : Loc.Get("Choose is disabled — select a candidate first."));
            }

            ScreenReader.Say(sb.ToString());
        }

        // ---------- Helpers ----------

        private static string SkillAdjective(int adj)
        {
            switch (adj)
            {
                case 0: return Loc.Get("Fair");
                case 1: return Loc.Get("Good");
                case 2: return Loc.Get("Very Good");
                case 3: return Loc.Get("Excellent");
                case 4: return Loc.Get("Renowned");
                default: return Loc.Get("Heroic");
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
