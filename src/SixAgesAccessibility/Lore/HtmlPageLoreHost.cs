using UnityEngine;

namespace SixAgesAccessibility.Lore
{
    /// <summary>
    /// Generic ILoreHost for static-page lore dialogs (InfoDialogController,
    /// ManualDialogController). Both load a single HTML resource and have no
    /// per-topic graph, so detail-toggle and link-follow are no-ops with
    /// informative announcements.
    ///
    /// Close goes through ScreenManager.HideDialog rather than a closeButton
    /// field — neither dialog exposes one as a public field, but both register
    /// on the dialog stack and HideDialog is the canonical close path.
    /// </summary>
    public sealed class HtmlPageLoreHost : ILoreHost
    {
        private static readonly LoreAction[] _noActions = new LoreAction[0];

        private readonly ScreenController _controller;
        private readonly string _name;
        private readonly LoreAction[] _actions;

        /// <summary>Construct with the controller and a short display name ("Intro" / "Manual").</summary>
        public HtmlPageLoreHost(ScreenController controller, string name)
            : this(controller, name, null)
        {
        }

        /// <summary>
        /// Construct with controller, display name, and dialog-level action
        /// buttons. The actions are surfaced as virtual nodes past the last
        /// paragraph and let the user reach buttons like "Start tutorial" or
        /// "Open manual" that the regular Tab cycle cannot reach while the
        /// lore dispatcher owns the keyboard.
        /// </summary>
        public HtmlPageLoreHost(ScreenController controller, string name, LoreAction[] actions)
        {
            _controller = controller;
            _name = name;
            _actions = actions ?? _noActions;
        }

        /// <inheritdoc />
        public string DialogName { get { return _name; } }

        /// <inheritdoc />
        public bool DetailAvailable { get { return false; } }

        /// <inheritdoc />
        public bool DetailActive { get { return false; } }

        /// <inheritdoc />
        public void ToggleDetail() { /* not applicable */ }

        /// <inheritdoc />
        public LoreAction[] GetActions() { return _actions; }

        /// <inheritdoc />
        public void FollowLink(LoreLink link)
        {
            if (link == null)
            {
                ScreenReader.Say("Cannot follow link.");
                return;
            }
            if (string.IsNullOrEmpty(link.TargetTopic))
            {
                // Anchor-only — Phase 1 doesn't model intra-document anchors.
                ScreenReader.Say("Anchor link — use Up and Down to read further.");
                return;
            }
            // External or cross-document links from Info/Manual aren't safe to
            // follow here. The browser would handle them via Application.OpenURL
            // (mailto, http) or by re-routing into ManualDialog. Both bypass our
            // reader. Announce and let the user close manually.
            ScreenReader.Say("Link points to " + link.DisplayText + ", which opens outside this reader.");
        }

        /// <inheritdoc />
        public void Close()
        {
            if (_controller == null) return;
            try
            {
                ScreenManager sm = _controller.parent;
                if (sm == null) sm = Singleton<ScreenManager>.instance;
                if (sm != null) sm.HideDialog(_controller, instant: false);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("HtmlPageLoreHost.Close", ex);
            }
        }
    }
}
