using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Two-zone keyboard navigation for the Lore management screen.
    ///
    /// The screen has two parallel UILists (history entries, known myths) and a
    /// single MANUAL action button. Both lists feed MythDialogController via
    /// OnItemClicked → ShowDialogWithTopic; the screen itself does not display
    /// the lore text inline. Without a navigator the lists are unreachable
    /// (UIList rows are not Selectables) and flat Tab nav lands repeatedly on
    /// the lone button. We expose both lists via Tab and route Manual through
    /// hotkey M, matching the action-key pattern used by other navigators.
    /// </summary>
    public class LoreScreenNavigator
    {
        private enum Zone { History, Myths }

        private Zone _zone = Zone.History;
        private int _historyIndex = -1;
        private int _mythIndex = -1;

        public void ResetForNewScreen()
        {
            _zone = Zone.History;
            _historyIndex = -1;
            _mythIndex = -1;
        }

        /// <summary>Top-level dispatch — called every Update while Lore is active.</summary>
        public void HandleInput(LoreScreenController c)
        {
            if (c == null) return;

            // Tab / Shift+Tab — cycle zones (skips empty lists).
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                int dir = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? -1 : 1;
                CycleZone(c, dir);
                return;
            }

            // L — jump to history list (the larger of the two on most clans).
            if (Input.GetKeyDown(KeyCode.L) && !AnyModifier())
            {
                _zone = Zone.History;
                AnnounceZone(c);
                return;
            }

            // M — open the in-game Manual screen. The MANUAL button is the only
            // Selectable on the screen but routing through a hotkey lets the
            // user trigger it without first Tabbing past the lists.
            if (Input.GetKeyDown(KeyCode.M) && !AnyModifier())
            {
                TryOpenManual(c);
                return;
            }

            // D — read a fuller form of the focused entry. Lore entries only
            // expose name + filename, so D essentially repeats the current row
            // verbatim; for myths we add the deity icon hint.
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

            // Enter / Space — open the focused entry. OnItemClicked routes
            // through MythDialogController.ShowDialogWithTopic, which loads the
            // history/myth filename in an HTML dialog. The dialog itself is
            // browser-based (currently inaccessible per the Tier-4 TODO), but
            // at least the navigation reaches it.
            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                 || Input.GetKeyDown(KeyCode.Space)) && !AnyModifier())
            {
                ActivateFocused(c);
                return;
            }
        }

        // ---------- Zone selection ----------

        private void CycleZone(LoreScreenController c, int direction)
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

            AnnounceFullStatus(c);
        }

        private bool IsZoneAvailable(LoreScreenController c, Zone zone)
        {
            switch (zone)
            {
                case Zone.History: return c.historyList != null && c.historyList.gameObject.activeSelf && c.historyList.count > 0;
                case Zone.Myths:   return c.mythList    != null && c.mythList.gameObject.activeSelf    && c.mythList.count    > 0;
                default:           return false;
            }
        }

        private void AnnounceZone(LoreScreenController c)
        {
            switch (_zone)
            {
                case Zone.History:
                {
                    int n = c.historyList != null ? c.historyList.count : 0;
                    ScreenReader.Say(Loc.Get("History zone. ") + n + Loc.Get(n == 1 ? " entry." : " entries.")
                        + Loc.Get(" Up and Down to cycle, Enter opens it."));
                    break;
                }
                case Zone.Myths:
                {
                    int n = c.mythList != null ? c.mythList.count : 0;
                    ScreenReader.Say(Loc.Get("Myths zone. ") + n + Loc.Get(n == 1 ? " myth." : " myths.")
                        + Loc.Get(" Up and Down to cycle, Enter opens it."));
                    break;
                }
            }
        }

        // ---------- Item navigation ----------

        private void MoveItem(LoreScreenController c, int direction)
        {
            UIList list = GetActiveList(c);
            if (list == null || list.count == 0)
            {
                ScreenReader.Say(Loc.Get("List is empty."));
                return;
            }

            int idx = (_zone == Zone.History) ? _historyIndex : _mythIndex;
            idx += direction;
            if (idx < 0) idx = list.count - 1;
            if (idx >= list.count) idx = 0;

            if (_zone == Zone.History) _historyIndex = idx;
            else _mythIndex = idx;

            AnnounceCurrentItem(list, idx);
        }

        private void AnnounceCurrentItem(UIList list, int idx)
        {
            UIListItem item = list[idx];
            if (item == null) { ScreenReader.Say(Loc.Get("(empty row)")); return; }
            string text = !string.IsNullOrEmpty(item.text) ? item.text : item.gameObject.name;
            ScreenReader.Say(StringHelpers.StripTags(text));
        }

        private void ActivateFocused(LoreScreenController c)
        {
            UIList list = GetActiveList(c);
            if (list == null || list.count == 0) return;

            int idx = (_zone == Zone.History) ? _historyIndex : _mythIndex;
            if (idx < 0 || idx >= list.count)
            {
                ScreenReader.Say(Loc.Get("Use Up and Down to pick an entry first."));
                return;
            }

            UIListItem item = list[idx];
            if (item == null) return;

            string label = !string.IsNullOrEmpty(item.text) ? StringHelpers.StripTags(item.text) : Loc.Get("entry");
            ScreenReader.Say(Loc.Get("Opening ") + label + ".");
            try { list.OnItemClicked(item); }
            catch (Exception ex) { DebugLogger.Error("LoreNav.OnItemClicked", ex); }
        }

        private void AnnounceDescription(LoreScreenController c)
        {
            UIList list = GetActiveList(c);
            if (list == null || list.count == 0) { ScreenReader.Say(Loc.Get("No entries in list.")); return; }

            int idx = (_zone == Zone.History) ? _historyIndex : _mythIndex;
            if (idx < 0 || idx >= list.count)
            {
                ScreenReader.Say(Loc.Get("Use Up and Down to pick an entry first."));
                return;
            }

            UIListItem item = list[idx];
            if (item == null) return;

            // The screen-level lists do not expose summaries — entry text is
            // the only metadata held in UIListItem. Re-read it via the screen
            // reader so D acts as a "read again" key consistent with other
            // navigators, and append "Press Enter to open" so the user knows
            // the next step.
            string text = !string.IsNullOrEmpty(item.text) ? StringHelpers.StripTags(item.text) : Loc.Get("(unnamed entry)");
            ScreenReader.Say(text + Loc.Get(". Press Enter to open."));
        }

        // ---------- Manual action ----------

        private void TryOpenManual(LoreScreenController c)
        {
            UIButton btn = FindManualButton(c);
            if (btn != null && btn.gameObject.activeSelf && btn.IsInteractable())
            {
                ScreenReader.Say(Loc.Get("Opening Manual."));
                try
                {
                    var ev = new BaseEventData(EventSystem.current);
                    ((ISubmitHandler)btn).OnSubmit(ev);
                }
                catch (Exception ex)
                {
                    DebugLogger.Error("LoreNav.OpenManual.Submit", ex);
                }
                return;
            }

            // Fallback — call ShowManual directly. The controller method takes
            // a UIButton parameter, but only uses it for sound effects, so
            // passing null still navigates to the Manual screen.
            ScreenReader.Say(Loc.Get("Opening Manual."));
            try { c.ShowManual(null); }
            catch (Exception ex) { DebugLogger.Error("LoreNav.ShowManual", ex); }
        }

        // ---------- Full status (F5) ----------

        public void AnnounceFullStatus(LoreScreenController c)
        {
            int historyCount = c.historyList != null ? c.historyList.count : 0;
            int mythCount    = c.mythList    != null ? c.mythList.count    : 0;

            var sb = new StringBuilder();
            sb.Append(Loc.Get("Lore status. "));
            sb.Append(historyCount).Append(Loc.Get(historyCount == 1 ? " history entry, " : " history entries, "));
            sb.Append(mythCount).Append(Loc.Get(mythCount == 1 ? " myth. " : " myths. "));

            if (_zone == Zone.History)
                sb.Append(Loc.Get("History zone active"));
            else
                sb.Append(Loc.Get("Myths zone active"));

            int idx = (_zone == Zone.History) ? _historyIndex : _mythIndex;
            UIList list = GetActiveList(c);
            if (list != null && idx >= 0 && idx < list.count && list[idx] != null)
                sb.Append(Loc.Get(", focused: ")).Append(StringHelpers.StripTags(list[idx].text ?? ""));

            sb.Append(Loc.Get(". Tab cycles zones, Up and Down cycle entries, Enter opens, "));
            sb.Append(Loc.Get("M opens Manual, F5 for status."));

            ScreenReader.Say(sb.ToString());
        }

        // ---------- Helpers ----------

        private UIList GetActiveList(LoreScreenController c)
        {
            return _zone == Zone.History ? c.historyList : c.mythList;
        }

        private static UIButton FindManualButton(LoreScreenController c)
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
                        if (click.GetPersistentMethodName(k) == "ShowManual")
                            return b;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("LoreNav.FindManualButton", ex);
            }
            return null;
        }

        /// <summary>True when the on-screen MANUAL button corresponds to <paramref name="btn"/>.</summary>
        public static bool IsManualButton(UIButton btn, LoreScreenController c)
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
                    if (click.GetPersistentMethodName(i) == "ShowManual")
                        return true;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("LoreNav.IsManualButton", ex);
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
