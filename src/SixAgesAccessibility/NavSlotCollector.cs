using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Represents a single navigable focus slot. Holds either a Unity <see cref="Selectable"/>
    /// (button, toggle, input field) or a <see cref="ResponseButton"/> from a scene's TextPanel.
    ///
    /// ResponseButtons are not Selectables, so the Unity event system can't focus them
    /// directly — without a unified slot type, Tab would visit all Selectables first and
    /// only then the response buttons, mismatching the visual layout (Start can sit above
    /// the precondition recap on the intro screen).
    /// </summary>
    public struct NavSlot
    {
        public Selectable Selectable;
        public ResponseButton Response;
        public bool IsResponse { get { return Response != null; } }
    }

    /// <summary>
    /// Pure-function helpers that discover navigable elements on a screen and sort them
    /// by visual position. The keyboard navigation handler owns the resulting lists and
    /// the focus state — this class is intentionally stateless so it can be reused from
    /// any helper that needs a fresh button collection.
    /// </summary>
    public static class NavSlotCollector
    {
        /// <summary>True if a ResponseButton is interactive enough to receive focus.</summary>
        public static bool IsResponseButtonUsable(ResponseButton rb)
        {
            if (rb == null || !rb.gameObject.activeSelf) return false;
            // ResponseButton.interactable would NRE if the underlying Button component
            // is missing (which can happen on partially-initialized prefabs), so go
            // through GetComponent<Button>() directly.
            Button btn = rb.GetComponent<Button>();
            if (btn == null || !btn.interactable) return false;
            return true;
        }

        /// <summary>
        /// Fill <paramref name="target"/> with the screen's interactive buttons, toggles, and
        /// (for InteractiveController) the clan-name input field. The list is cleared first.
        ///
        /// Skips advisor-card buttons (PersonCard on the same GameObject as the UIButton):
        /// advisors have dedicated hotkeys (F3, Shift+F3), so leaving them out of Tab keeps
        /// the cycle focused on actual screen actions instead of seven advisor faces.
        /// </summary>
        public static void CollectButtonsInto(ScreenController screen, List<Selectable> target)
        {
            target.Clear();

            // ChooseGame: only the main button on each save item. Edit-button and per-row
            // delete-buttons are excluded — deletion is handled via the Delete hotkey with
            // two-press confirmation.
            ChooseGameController cg = screen as ChooseGameController;
            if (cg != null)
            {
                if (cg.gameList != null)
                {
                    for (int i = 0; i < cg.gameList.count; i++)
                    {
                        SaveListItem item = cg.gameList[i] as SaveListItem;
                        if (item == null) continue;
                        if (item.button == null) continue;
                        if (item.button.isActiveAndEnabled && item.button.IsInteractable())
                            target.Add(item.button);
                    }
                }
                return;
            }

            UIButton[] allButtons = screen.GetComponentsInChildren<UIButton>(false);
            foreach (var b in allButtons)
            {
                if (!b.isActiveAndEnabled || !b.IsInteractable()) continue;
                if (b.GetComponent<PersonCard>() != null) continue;
                target.Add(b);
            }

            UIToggle[] allToggles = screen.GetComponentsInChildren<UIToggle>(false);
            foreach (var t in allToggles)
            {
                if (t.isActiveAndEnabled && t.IsInteractable())
                    target.Add(t);
            }

            // InteractiveController: include the clan-name input field if visible.
            // TMP_InputField is a Selectable but not a UIButton/UIToggle, so it would
            // otherwise be missed. The intro's @nameentry tag uses this.
            InteractiveController ic = screen as InteractiveController;
            if (ic != null && ic.textPanel != null && ic.textPanel.nameField != null)
            {
                TMP_InputField nf = ic.textPanel.nameField;
                if (nf.gameObject.activeInHierarchy && nf.IsInteractable())
                    target.Add(nf);
            }
        }

        /// <summary>
        /// Fill <paramref name="target"/> with the active TextPanel's ResponseButtons (scene
        /// answer options, plus Start and Proceed buttons). Cleared first.
        /// </summary>
        public static void CollectResponseButtonsInto(ScreenController screen, List<ResponseButton> target)
        {
            target.Clear();

            InteractiveController ic = screen as InteractiveController;
            if (ic == null || ic.textPanel == null) return;

            TextPanel tp = ic.textPanel;

            if (tp.responseButtons != null)
            {
                foreach (var rb in tp.responseButtons)
                {
                    if (IsResponseButtonUsable(rb))
                        target.Add(rb);
                }
            }

            if (IsResponseButtonUsable(tp.startButton)) target.Add(tp.startButton);
            if (IsResponseButtonUsable(tp.proceedButton)) target.Add(tp.proceedButton);
        }

        /// <summary>
        /// Sort selectables by visual position: top-to-bottom (descending Y), left-to-right
        /// for elements on the same row. The 10-pixel Y tolerance treats a small vertical
        /// drift as the same row so flexbox-style layouts read naturally.
        /// </summary>
        public static void SortByPosition(List<Selectable> buttons)
        {
            buttons.Sort((a, b) =>
            {
                float ay = a.transform.position.y;
                float by = b.transform.position.y;
                if (Mathf.Abs(ay - by) > 10f)
                    return by.CompareTo(ay);
                return a.transform.position.x.CompareTo(b.transform.position.x);
            });
        }

        /// <summary>
        /// Compare NavSlots by visual position. Same Y-tolerance as <see cref="SortByPosition"/>
        /// so the merged ordering matches the appearance on screen.
        /// </summary>
        public static int CompareNavSlotsByPosition(NavSlot a, NavSlot b)
        {
            Transform ta = a.IsResponse ? a.Response.transform : a.Selectable.transform;
            Transform tb = b.IsResponse ? b.Response.transform : b.Selectable.transform;
            float ay = ta.position.y;
            float by = tb.position.y;
            if (Mathf.Abs(ay - by) > 10f)
                return by.CompareTo(ay);
            return ta.position.x.CompareTo(tb.position.x);
        }
    }
}
