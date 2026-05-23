using System.Reflection;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SixAgesAccessibility.Lore
{
    /// <summary>
    /// ILoreHost adapter for MythDialogController. Bridges LoreReader to the
    /// game's controller through public fields where possible and Reflection
    /// where the game keeps state private (isDetailed, OpenLink). Stateless —
    /// reads everything live from the controller so detail-toggle reflects the
    /// current display without our cache going stale.
    /// </summary>
    public sealed class MythDialogLoreHost : ILoreHost
    {
        private static readonly LoreAction[] _noActions = new LoreAction[0];

        private readonly MythDialogController _mdc;
        private static FieldInfo _isDetailedField;

        /// <summary>Wrap the live MythDialogController in an ILoreHost.</summary>
        public MythDialogLoreHost(MythDialogController mdc)
        {
            _mdc = mdc;
        }

        /// <inheritdoc />
        public string DialogName { get { return "Lore"; } }

        /// <inheritdoc />
        public LoreAction[] GetActions() { return _noActions; }

        /// <inheritdoc />
        public bool DetailAvailable
        {
            get
            {
                return _mdc != null
                    && _mdc.detailToggle != null
                    && _mdc.detailToggle.gameObject != null
                    && _mdc.detailToggle.gameObject.activeSelf;
            }
        }

        /// <inheritdoc />
        public bool DetailActive
        {
            get
            {
                if (_mdc == null) return false;
                try
                {
                    if ((object)_isDetailedField == null)
                    {
                        _isDetailedField = typeof(MythDialogController)
                            .GetField("isDetailed", BindingFlags.Instance | BindingFlags.NonPublic);
                    }
                    if ((object)_isDetailedField == null) return false;
                    object v = _isDetailedField.GetValue(_mdc);
                    return v is bool && (bool)v;
                }
                catch (System.Exception ex)
                {
                    DebugLogger.Error("MythDialogLoreHost.DetailActive", ex);
                    return false;
                }
            }
        }

        /// <inheritdoc />
        public void ToggleDetail()
        {
            if (_mdc == null) return;
            try
            {
                // ToggleDetail(UIButton b) — `b` is unused inside the method body,
                // it just forwards to ShowTopic(currentTopic, !isDetailed, ...).
                _mdc.ToggleDetail(null);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("MythDialogLoreHost.ToggleDetail", ex);
            }
        }

        /// <inheritdoc />
        public void FollowLink(LoreLink link)
        {
            if (_mdc == null || link == null)
            {
                ScreenReader.Say("Cannot follow link.");
                return;
            }
            if (string.IsNullOrEmpty(link.TargetTopic))
            {
                // Fragment-only link inside the current document — anchors aren't
                // useful to a screen-reader user (the document is read top-to-
                // bottom anyway). Announce and ignore rather than no-op silently.
                ScreenReader.Say("Internal anchor — use Up and Down to read further.");
                return;
            }

            // ShowDialogWithTopic is public + static and triggers ScreenManager,
            // which fires OnShow → ShowTopic on our existing patch hook → the
            // doc gets reloaded and announced. Cleaner than reflecting OpenLink
            // and we lose only the anchor (which we ignore on purpose).
            try
            {
                MythDialogController.ShowDialogWithTopic(link.TargetTopic);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("MythDialogLoreHost.FollowLink(" + link.TargetTopic + ")", ex);
                ScreenReader.Say("Could not open " + link.DisplayText + ".");
            }
        }

        /// <inheritdoc />
        public void Close()
        {
            if (_mdc == null) return;
            UIButton btn = _mdc.closeButton;
            if (btn == null || btn.gameObject == null || !btn.gameObject.activeSelf || !btn.IsInteractable())
            {
                ScreenReader.Say("Close not available.");
                return;
            }
            try
            {
                ISubmitHandler h = btn.GetComponent<ISubmitHandler>();
                if (h != null) h.OnSubmit(new BaseEventData(EventSystem.current));
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("MythDialogLoreHost.Close", ex);
            }
        }
    }
}
