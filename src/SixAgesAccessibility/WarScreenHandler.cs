using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Keyboard hotkeys and clean Tab-cycle labels for the War management screen.
    ///
    /// The screen has five dialog-launcher buttons (Fortify / Raid / HonorRaid /
    /// CattleRaid / Warriors) whose on-screen labels are inconsistent — some are
    /// missing entirely (Fortify falls back to GameObject name "FortifyButton"),
    /// some are ALLCAPS ("HERD RAID", "RAID"), some Pascal-case ("Warriors").
    /// We do two things to make this usable:
    ///
    ///  1. Identify each button via its UnityEvent persistent listener method
    ///     name (ShowFortifyDialog etc.) so the keyboard handler can speak a
    ///     normalised label ("Fortify", "Cattle raid", ...) when Tab focuses it.
    ///
    ///  2. Provide letter shortcuts F/R/O/C/W as a quicker alternative for users
    ///     who already know the screen — the controller methods are called
    ///     directly so HonorRaid still works even when its on-screen button is
    ///     hidden (no honor raids currently queued).
    /// </summary>
    public static class WarScreenHandler
    {
        // Method names on WarScreenController bound to the on-screen buttons via
        // UnityEvent persistent listeners. The Inspector-set targets/methods are
        // stable across game patches, while GameObject names and UILabel.text
        // values are not.
        private const string MethodFortify   = "ShowFortifyDialog";
        private const string MethodHerdRaid  = "ShowHerdRaidDialog";
        private const string MethodHonorRaid = "ShowHonorRaidDialog";
        private const string MethodRaid      = "ShowRaidDialog";
        private const string MethodWarriors  = "ShowWarriorsDialog";

        /// <summary>
        /// If <paramref name="btn"/> is one of WarScreen's five dialog-launcher
        /// buttons, return a normalised label for the screen reader ("Fortify",
        /// "Cattle raid", ...). Returns null otherwise; the caller falls back to
        /// the regular UILabel / GameObject-name path.
        /// </summary>
        public static string GetActionLabel(UIButton btn, WarScreenController war)
        {
            string method = GetWarActionMethod(btn, war);
            if (method == null) return null;
            return MethodToLabel(method);
        }

        /// <summary>
        /// Dispatch a keypress on the War screen. Returns true when consumed.
        /// </summary>
        public static bool HandleKeys(WarScreenController war)
        {
            if (war == null) return false;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)
                || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                return false;

            if (Input.GetKeyDown(KeyCode.F)) return InvokeAction(war, MethodFortify,   "Fortify");
            if (Input.GetKeyDown(KeyCode.R)) return InvokeAction(war, MethodRaid,      "Raid");
            if (Input.GetKeyDown(KeyCode.O)) return InvokeAction(war, MethodHonorRaid, "Honor raid");
            if (Input.GetKeyDown(KeyCode.C)) return InvokeAction(war, MethodHerdRaid,  "Cattle raid");
            if (Input.GetKeyDown(KeyCode.W)) return InvokeAction(war, MethodWarriors,  "Warriors");

            return false;
        }

        /// <summary>
        /// Build a short hotkey hint for the War-screen auto-summary. Plain
        /// listing without per-button availability — the controller's ShowXDialog
        /// methods are unconditional once the screen is active, and the dialog's
        /// own UI tells the user when an action has nothing to do.
        /// </summary>
        public static string DescribeActions(WarScreenController war)
        {
            return Loc.Get("Hotkeys: W warriors, R raid, C cattle raid, O honor raid, F fortify. Tab also cycles these buttons.");
        }

        // ----- internal helpers ---------------------------------------------

        private static bool InvokeAction(WarScreenController war, string methodName, string actionLabel)
        {
            if (war == null) return false;

            DebugLogger.Log("WarScreenHandler", "Invoking " + methodName);

            // Prefer the on-screen button's submit path so any extra listeners
            // wired up in the prefab still fire (sound effects, focus changes).
            // Falls back to the controller method directly when the button is
            // hidden or absent — Honor Raid is sometimes not instantiated until
            // honor raids exist, and we still want the user to be able to peek
            // at the dialog from the keyboard.
            UIButton btn = FindButton(war, methodName);
            if (btn != null && btn.gameObject.activeSelf && btn.IsInteractable())
            {
                ScreenReader.Say(Loc.Get("Opening ") + Loc.Get(actionLabel) + Loc.Get(" dialog."));
                try
                {
                    var ev = new BaseEventData(EventSystem.current);
                    ((ISubmitHandler)btn).OnSubmit(ev);
                }
                catch (Exception ex)
                {
                    DebugLogger.Error("WarScreenHandler.InvokeAction.Submit", ex);
                }
                return true;
            }

            // Fallback: call the controller method directly. Each ShowXDialog
            // method is internally guarded by `if (base.isActive)` and just
            // navigates to the corresponding management screen.
            ScreenReader.Say(Loc.Get("Opening ") + Loc.Get(actionLabel) + Loc.Get(" dialog."));
            try
            {
                war.SendMessage(methodName, SendMessageOptions.DontRequireReceiver);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("WarScreenHandler.InvokeAction.SendMessage", ex);
            }
            return true;
        }

        /// <summary>
        /// Locate the UIButton in the WarScreen hierarchy whose onClick invokes
        /// the named controller method. Returns null when nothing matches.
        /// </summary>
        private static UIButton FindButton(WarScreenController war, string methodName)
        {
            if (war == null || string.IsNullOrEmpty(methodName)) return null;
            try
            {
                UIButton[] all = war.GetComponentsInChildren<UIButton>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    UIButton b = all[i];
                    if (b == null) continue;
                    if (string.Equals(GetWarActionMethod(b, war), methodName))
                        return b;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("WarScreenHandler.FindButton", ex);
            }
            return null;
        }

        private static string GetWarActionMethod(UIButton btn, WarScreenController war)
        {
            if (btn == null || war == null) return null;
            try
            {
                var click = btn.onClick;
                if (click == null) return null;
                int n = click.GetPersistentEventCount();
                for (int i = 0; i < n; i++)
                {
                    if (!System.Object.ReferenceEquals(click.GetPersistentTarget(i), war))
                        continue;
                    string method = click.GetPersistentMethodName(i);
                    if (method == MethodFortify || method == MethodHerdRaid
                        || method == MethodHonorRaid || method == MethodRaid
                        || method == MethodWarriors)
                        return method;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("WarScreenHandler.GetWarActionMethod", ex);
            }
            return null;
        }

        private static string MethodToLabel(string method)
        {
            switch (method)
            {
                case MethodFortify:   return "Fortify";
                case MethodHerdRaid:  return "Cattle raid";
                case MethodHonorRaid: return "Honor raid";
                case MethodRaid:      return "Raid";
                case MethodWarriors:  return "Warriors";
                default:              return null;
            }
        }
    }
}
