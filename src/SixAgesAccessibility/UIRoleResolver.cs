using System;
using System.Reflection;
using UnityEngine.UI;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Resolves a clean spoken label for an icon-only UIButton by mapping it to the
    /// game's own semantic structure — the controller's named UIButton fields
    /// (closeButton, actionButton, chooseLeaderButton, ...) and, failing that, the
    /// button's own Rollover.message tooltip used for sighted hover.
    ///
    /// Used as a pre-fallback in <see cref="KeyboardNavigationHandler.AnnounceFocusedButton"/>
    /// so Tab on an X-icon close button announces "Close" instead of the prefab's
    /// internal GameObject name like "CloseButton2". The generic GameObject-name
    /// fallback only fires when both the role lookup and the tooltip lookup come
    /// up empty — which is then a documentable data gap, not a screen-reader hack.
    /// </summary>
    public static class UIRoleResolver
    {
        // Cache typeof(UIButton) so the FieldType comparison doesn't go through
        // Type.op_Equality each call (which exists in modern .NET but is missing
        // on Unity 2018 Mono — same family as the LINQ / Array.Empty pitfalls).
        private static readonly Type UIButtonType = typeof(UIButton);

        // Per-controller-type cache of UIButton fields so repeat focus changes on
        // the same screen don't re-walk the reflection tree every frame.
        private static readonly System.Collections.Generic.Dictionary<Type, FieldInfo[]> _fieldCache
            = new System.Collections.Generic.Dictionary<Type, FieldInfo[]>();

        /// <summary>
        /// Resolve a clean label for <paramref name="btn"/> when it has no UILabel
        /// text of its own. Returns null when neither the controller's role-fields
        /// nor a Rollover tooltip carry an answer; the caller then falls back to
        /// whatever it was doing before (GameObject.name, in our case).
        /// </summary>
        public static string ResolveButtonLabel(UIButton btn, UnityEngine.MonoBehaviour controller)
        {
            if (btn == null) return null;

            // Skip the resolver when the button already speaks for itself —
            // a non-empty UILabel is the prefab author's chosen text, always
            // preferred over our reverse-engineered guesses.
            if (btn.label != null && !string.IsNullOrEmpty(btn.label.text))
                return null;

            string byRole = ResolveByRole(btn, controller);
            if (!string.IsNullOrEmpty(byRole)) return byRole;

            string byTooltip = ResolveByTooltip(btn);
            if (!string.IsNullOrEmpty(byTooltip)) return byTooltip;

            string byOnClick = ResolveByOnClickListener(btn, controller);
            if (!string.IsNullOrEmpty(byOnClick)) return byOnClick;

            return null;
        }

        /// <summary>
        /// Walk the controller's UIButton fields up the inheritance chain and check
        /// whether any of them references this exact button instance. Returns a
        /// human-spoken label for known field names, or null when no field matches.
        /// </summary>
        private static string ResolveByRole(UIButton btn, UnityEngine.MonoBehaviour controller)
        {
            if (controller == null) return null;
            try
            {
                Type t = controller.GetType();
                // Mono 2.0 lacks Type.op_Inequality / op_Equality — direct `!=` against
                // null or another typeof(...) throws MissingMethodException at runtime.
                // Cast to object to route through object.op_Inequality (reference compare).
                Type monoBehaviourType = typeof(UnityEngine.MonoBehaviour);
                Type objectType = typeof(object);
                while ((object)t != null
                    && (object)t != (object)monoBehaviourType
                    && (object)t != (object)objectType)
                {
                    FieldInfo[] fields;
                    if (!_fieldCache.TryGetValue(t, out fields))
                    {
                        fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic
                            | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        _fieldCache[t] = fields;
                    }

                    for (int i = 0; i < fields.Length; i++)
                    {
                        FieldInfo f = fields[i];
                        // Type comparison via Equals — Mono 2.0 lacks Type.op_Equality.
                        if (!UIButtonType.Equals(f.FieldType)) continue;
                        object val = f.GetValue(controller);
                        if (!System.Object.ReferenceEquals(val, btn)) continue;
                        return MapFieldNameToLabel(f.Name, btn);
                    }
                    t = t.BaseType;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("UIRoleResolver.ResolveByRole", ex);
            }
            return null;
        }

        /// <summary>
        /// Map a controller field name (closeButton, actionButton, ...) to the
        /// label a screen-reader user should hear. Returns null for unknown field
        /// names so the caller can keep looking — better silent here than wrong.
        /// </summary>
        private static string MapFieldNameToLabel(string fieldName, UIButton btn)
        {
            switch (fieldName)
            {
                case "closeButton": return "Close";
                // actionButton is the Build/Recruit/Raid/Sacrifice button that flips
                // its own UILabel text live — RECRUIT vs DISMISS on Warriors, RAID vs
                // HERD RAID on RaidDialog. Prefer the live label when present so the
                // user hears the same word the sighted player reads.
                case "actionButton":
                    if (btn.label != null && !string.IsNullOrEmpty(btn.label.text))
                        return btn.label.text;
                    return "Confirm action";
                case "chooseLeaderButton": return "Choose leader";
                case "chooseButton":       return "Choose";
                case "buildButton":        return "Build";
                case "reduceButton":       return "Reduce";
                case "leaderButton":       return "Choose leader";
                // emissaryButton is intentionally unmapped here: KeyboardNavigationHandler
                // already has a richer special case ("Send emissary to <Clan>. Press Enter.")
                // so we don't want to shadow that with a plain "Emissary".
                default: return null;
            }
        }

        /// <summary>
        /// Read the Rollover (sighted-hover tooltip) message attached to the button
        /// or any of its children. Returns the trimmed message or null. Mirrors the
        /// data path TooltipPatches uses for the visual popup, just keyboard-driven.
        /// </summary>
        private static string ResolveByTooltip(UIButton btn)
        {
            try
            {
                Rollover[] rollovers = btn.GetComponentsInChildren<Rollover>(true);
                if (rollovers != null)
                {
                    for (int i = 0; i < rollovers.Length; i++)
                    {
                        Rollover r = rollovers[i];
                        if (r == null) continue;
                        if (string.IsNullOrEmpty(r.message)) continue;
                        return StringHelpers.StripTags(r.message).Trim();
                    }
                }

                TextRollover[] textRollovers = btn.GetComponentsInChildren<TextRollover>(true);
                if (textRollovers != null)
                {
                    for (int i = 0; i < textRollovers.Length; i++)
                    {
                        TextRollover tr = textRollovers[i];
                        if (tr == null) continue;
                        // TextRollover.info is the hover text — a plain string field.
                        if (string.IsNullOrEmpty(tr.info)) continue;
                        return StringHelpers.StripTags(tr.info).Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("UIRoleResolver.ResolveByTooltip", ex);
            }
            return null;
        }

        /// <summary>
        /// Inspect the button's onClick UnityEvent persistent listeners and infer the
        /// role from the bound method name. The Inspector-set method name is the
        /// developer's stated intent and stays stable across patches — unlike the
        /// GameObject name "CloseButton2" which is just prefab-author noise.
        ///
        /// <para>Catches the X-icon close buttons on FortifyDialog, WarriorsDialog,
        /// RaidDialog (no closeButton C# field, no Rollover tooltip) — those wire
        /// onClick to a method name that signals dismissal (Close / Hide / Cancel /
        /// Dismiss) or to <c>GameManager.ShowManagementScreen</c> with the previous
        /// management screen as argument. In a ManagementDialogController context,
        /// the latter unambiguously means "back to caller", i.e. close.</para>
        /// </summary>
        private static string ResolveByOnClickListener(UIButton btn, UnityEngine.MonoBehaviour controller)
        {
            try
            {
                var click = btn.onClick;
                if (click == null) return null;
                int n = click.GetPersistentEventCount();
                for (int i = 0; i < n; i++)
                {
                    string method = click.GetPersistentMethodName(i);
                    if (string.IsNullOrEmpty(method)) continue;
                    if (LooksLikeCloseMethod(method))
                        return "Close";
                    // ShowManagementScreen IS a close when invoked from inside a dialog
                    // (back to spawning screen). On a ManagementController itself the
                    // same method is used to OPEN dialogs — skip via controller-type guard.
                    if (method == "ShowManagementScreen"
                        && controller is ManagementDialogController)
                        return "Close";
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("UIRoleResolver.ResolveByOnClickListener", ex);
            }
            return null;
        }

        private static bool LooksLikeCloseMethod(string method)
        {
            if (string.IsNullOrEmpty(method)) return false;
            return method.IndexOf("Close",   StringComparison.OrdinalIgnoreCase) >= 0
                || method.IndexOf("Hide",    StringComparison.OrdinalIgnoreCase) >= 0
                || method.IndexOf("Cancel",  StringComparison.OrdinalIgnoreCase) >= 0
                || method.IndexOf("Dismiss", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Locate the dialog's Close button on screens where the controller does not
        /// expose it as a C# field (FortifyDialog, WarriorsDialog, RaidDialog wire
        /// theirs purely in the prefab as "CloseButton2"). Walks every active+
        /// interactable UIButton in the controller's hierarchy and returns the first
        /// one whose onClick listener method name signals dismissal — same heuristic
        /// as <see cref="ResolveByOnClickListener"/>. Returns null when none found.
        /// </summary>
        public static UIButton FindCloseButton(UnityEngine.MonoBehaviour controller)
        {
            if (controller == null) return null;
            try
            {
                UIButton[] buttons = controller.GetComponentsInChildren<UIButton>(false);
                if (buttons == null) return null;
                for (int i = 0; i < buttons.Length; i++)
                {
                    UIButton b = buttons[i];
                    if (b == null) continue;
                    if (!b.gameObject.activeSelf) continue;
                    if (!b.IsInteractable()) continue;

                    var click = b.onClick;
                    if (click == null) continue;
                    int n = click.GetPersistentEventCount();
                    for (int j = 0; j < n; j++)
                    {
                        string method = click.GetPersistentMethodName(j);
                        if (string.IsNullOrEmpty(method)) continue;
                        if (LooksLikeCloseMethod(method))
                            return b;
                        if (method == "ShowManagementScreen"
                            && controller is ManagementDialogController)
                            return b;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("UIRoleResolver.FindCloseButton", ex);
            }
            return null;
        }
    }
}
