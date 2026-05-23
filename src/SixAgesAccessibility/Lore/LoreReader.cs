using System.Text;
using UnityEngine;

namespace SixAgesAccessibility.Lore
{
    /// <summary>
    /// Callback signature for LoreAction. We define our own delegate instead
    /// of using System.Action because Unity 2018 Mono throws TypeLoadException
    /// when it tries to load a class with a `System.Action` field — the type
    /// is defined in mscorlib 4.x but Unity's Mono BCL keeps `Action` in a
    /// different assembly, so the cross-assembly reference baked into our
    /// net46-compiled IL fails to resolve at JIT time. A local delegate keeps
    /// the reference inside our own DLL.
    /// </summary>
    public delegate void LoreActionInvoker();

    /// <summary>
    /// A dialog-level button surfaced to the reader as a virtual node past the
    /// last paragraph. Lets the user reach Tutorial/Manual/Close buttons on
    /// dialogs whose interactive controls would otherwise be unreachable
    /// because LoreDispatcher exclusively consumes Tab and arrow keys.
    /// </summary>
    public sealed class LoreAction
    {
        /// <summary>Localized label spoken when the user moves onto this action.</summary>
        public string Label;

        /// <summary>Invoked when the user presses Enter on the action.</summary>
        public LoreActionInvoker Invoke;
    }

    /// <summary>
    /// Host adapter for the in-game lore reader. Each dialog (Myth / Info /
    /// Manual) implements this so LoreReader stays free of Harmony / reflection
    /// glue. The reader calls into the host to follow links, toggle detail
    /// view, and close the dialog — actions that can only be carried out by
    /// the controller that owns the on-screen browser.
    /// </summary>
    public interface ILoreHost
    {
        /// <summary>True when the detail toggle is meaningful for the current topic.</summary>
        bool DetailAvailable { get; }

        /// <summary>True when the host is currently showing the detailed (non-_mini) variant.</summary>
        bool DetailActive { get; }

        /// <summary>Toggle between _mini and full variants. Caller must update the document.</summary>
        void ToggleDetail();

        /// <summary>Open a new topic by link. Implementations may use Reflection.</summary>
        void FollowLink(LoreLink link);

        /// <summary>Close the dialog (Esc fallback).</summary>
        void Close();

        /// <summary>Short display name for the dialog kind, used in the opening hint.</summary>
        string DialogName { get; }

        /// <summary>
        /// Dialog-level action buttons appended after the last paragraph. The
        /// reader navigates into them with Down after the last node. Empty or
        /// null means the dialog has no extra actions beyond Escape.
        /// </summary>
        LoreAction[] GetActions();
    }

    /// <summary>
    /// Stateful navigation engine over a LoreDocument. Lives across topic
    /// changes within a single dialog session — SetDocument() resets focus
    /// and announces the new content; HandleInput() reads the keyboard and
    /// routes into TTS / host actions.
    ///
    /// Two parallel cursors:
    ///   - node cursor (Up/Down, Ctrl+Up/Down for headings, Home/End)
    ///   - link cursor (Tab/Shift+Tab) — independent so the user can scan
    ///     paragraphs without losing their place in the link list and vice
    ///     versa. Enter follows the focused link if link mode is active,
    ///     otherwise it re-reads the focused paragraph.
    /// </summary>
    public sealed class LoreReader
    {
        private LoreDocument _doc = new LoreDocument();
        private int _nodeIndex = -1;
        private int _linkIndex = -1;
        private bool _linkFocused;     // true after Tab/Shift+Tab; cleared by Up/Down
        private string _sourceLabel;   // free-form, e.g. "Ernalda" or "Manual"
        private int _actionIndex = -1; // -1 = not in action zone; otherwise index into host actions

        /// <summary>The currently loaded document. Null-safe — never null after construction.</summary>
        public LoreDocument Document { get { return _doc; } }

        /// <summary>Replace the document and reset cursors. Does not announce — caller decides.</summary>
        public void SetDocument(LoreDocument doc, string sourceLabel)
        {
            _doc = doc ?? new LoreDocument();
            _nodeIndex = _doc.Nodes.Count > 0 ? 0 : -1;
            _linkIndex = -1;
            _linkFocused = false;
            _actionIndex = -1;
            _sourceLabel = sourceLabel;
        }

        private static int ActionCount(ILoreHost host)
        {
            if (host == null) return 0;
            var actions = host.GetActions();
            return actions == null ? 0 : actions.Length;
        }

        /// <summary>
        /// Speak a one-shot opening summary describing the topic and available
        /// hotkeys. Intended for the initial open / topic-change moment.
        /// </summary>
        public void AnnounceOpening(ILoreHost host)
        {
            var sb = new StringBuilder();
            string title = LoreTranslation.Title(_doc);
            if (string.IsNullOrEmpty(title)) title = _sourceLabel;
            if (string.IsNullOrEmpty(title)) title = host != null ? host.DialogName : Loc.Get("Lore");
            sb.Append(title).Append('.');

            int paragraphs = _doc.Nodes.Count;
            int links = _doc.Links.Count;
            int actions = ActionCount(host);
            sb.Append(' ').Append(paragraphs).Append(Loc.Get(paragraphs == 1 ? " paragraph" : " paragraphs"));
            if (links > 0) sb.Append(", ").Append(links).Append(Loc.Get(links == 1 ? " link" : " links"));
            if (actions > 0) sb.Append(", ").Append(actions).Append(Loc.Get(actions == 1 ? " action after the text" : " actions after the text"));
            sb.Append('.');

            if (paragraphs == 0 && actions == 0)
            {
                sb.Append(Loc.Get(" No readable text. Press Escape to close."));
                ScreenReader.Say(sb.ToString());
                return;
            }

            sb.Append(' ').Append(BuildHotkeyHint(host));
            ScreenReader.Say(sb.ToString());

            // Read the first paragraph immediately so the user lands on content.
            // interrupt:false queues this after the hint. If the document has
            // no paragraphs but does have actions (edge case for empty-page
            // dialogs), drop straight into the first action instead.
            if (_nodeIndex >= 0 && _nodeIndex < _doc.Nodes.Count)
            {
                ScreenReader.Say(BuildNodeAnnouncement(_doc.Nodes[_nodeIndex], _nodeIndex), interrupt: false);
            }
            else if (actions > 0)
            {
                _actionIndex = 0;
                ScreenReader.Say(BuildActionAnnouncement(host.GetActions()[0]), interrupt: false);
            }
        }

        /// <summary>
        /// Return a status string for F5: title, current position, and detail-mode.
        /// </summary>
        public string BuildFullStatus(ILoreHost host)
        {
            var sb = new StringBuilder();
            string title = LoreTranslation.Title(_doc);
            if (string.IsNullOrEmpty(title)) title = _sourceLabel;
            if (string.IsNullOrEmpty(title)) title = host != null ? host.DialogName : Loc.Get("Lore");
            sb.Append(title).Append('.');

            if (_doc.IsEmpty)
            {
                sb.Append(Loc.Get(" No readable text."));
                return sb.ToString();
            }

            if (_actionIndex >= 0 && host != null)
            {
                var actions = host.GetActions();
                if (actions != null && _actionIndex < actions.Length && actions[_actionIndex] != null)
                {
                    sb.Append(Loc.Get(" Action focused: ")).Append(actions[_actionIndex].Label).Append('.');
                }
            }
            else if (_linkFocused && _linkIndex >= 0 && _linkIndex < _doc.Links.Count)
            {
                var link = _doc.Links[_linkIndex];
                sb.Append(Loc.Get(" Link focused: ")).Append(LoreTranslation.LinkText(link)).Append('.');
            }
            else if (_nodeIndex >= 0 && _nodeIndex < _doc.Nodes.Count)
            {
                var node = _doc.Nodes[_nodeIndex];
                sb.Append(' ').Append(NodeKindLabel(node)).Append(": ").Append(Truncate(LoreTranslation.NodeText(node), 80)).Append('.');
            }

            sb.Append(' ').Append(_doc.Nodes.Count).Append(Loc.Get(" paragraphs total"));
            if (_doc.Links.Count > 0) sb.Append(", ").Append(_doc.Links.Count).Append(Loc.Get(" links"));
            sb.Append('.');

            if (host != null && host.DetailAvailable)
            {
                sb.Append(Loc.Get(host.DetailActive ? " Detailed view." : " Compact view. D toggles detail."));
            }

            sb.Append(' ').Append(BuildHotkeyHint(host));
            return sb.ToString();
        }

        /// <summary>Per-frame input handler — called from KeyboardNavigationHandler dispatch.</summary>
        public void HandleInput(ILoreHost host)
        {
            int actionCount = ActionCount(host);
            if (_doc == null || (_doc.IsEmpty && actionCount == 0))
            {
                // F5 still useful (says "no readable text"); other keys idle.
                if (Input.GetKeyDown(KeyCode.F5))
                {
                    ScreenReader.Say(BuildFullStatus(host));
                }
                return;
            }

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (Input.GetKeyDown(KeyCode.F5))
            {
                ScreenReader.Say(BuildFullStatus(host));
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Lore dispatch consumes all our hotkeys, so the standard
                // ManagementDialog-Escape handler never runs. Close locally.
                if (host != null) host.Close();
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (ctrl) MoveToHeading(-1);
                else MoveCursor(-1, host);
                return;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (ctrl) MoveToHeading(+1);
                else MoveCursor(+1, host);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Home))
            {
                if (_doc.Nodes.Count == 0 && actionCount == 0) return;
                _linkFocused = false;
                _actionIndex = -1;
                if (_doc.Nodes.Count > 0)
                {
                    _nodeIndex = 0;
                    AnnounceFocusedNode();
                }
                else
                {
                    _actionIndex = 0;
                    AnnounceFocusedAction(host);
                }
                return;
            }
            if (Input.GetKeyDown(KeyCode.End))
            {
                if (_doc.Nodes.Count == 0 && actionCount == 0) return;
                _linkFocused = false;
                if (actionCount > 0)
                {
                    _actionIndex = actionCount - 1;
                    AnnounceFocusedAction(host);
                }
                else
                {
                    _actionIndex = -1;
                    _nodeIndex = _doc.Nodes.Count - 1;
                    AnnounceFocusedNode();
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (shift) MoveLink(-1);
                else MoveLink(+1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (_actionIndex >= 0 && host != null)
                {
                    var actions = host.GetActions();
                    if (actions != null && _actionIndex < actions.Length)
                    {
                        var action = actions[_actionIndex];
                        if (action != null && action.Invoke != null)
                        {
                            try
                            {
                                action.Invoke();
                            }
                            catch (System.Exception ex)
                            {
                                DebugLogger.Error("LoreReader.InvokeAction", ex);
                            }
                            return;
                        }
                    }
                }
                if (_linkFocused && _linkIndex >= 0 && _linkIndex < _doc.Links.Count)
                {
                    var link = _doc.Links[_linkIndex];
                    if (host != null) host.FollowLink(link);
                    return;
                }
                AnnounceFocusedNode();
                return;
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                if (AnyTextEditFocused()) return;
                ScreenReader.Say(LoreTranslation.FullText(_doc));
                return;
            }

            if (Input.GetKeyDown(KeyCode.L))
            {
                if (AnyTextEditFocused()) return;
                AnnounceLinkList();
                return;
            }

            if (Input.GetKeyDown(KeyCode.D))
            {
                if (AnyTextEditFocused()) return;
                if (host != null && host.DetailAvailable)
                {
                    ScreenReader.Say(Loc.Get(host.DetailActive ? "Switching to compact view." : "Switching to detailed view."));
                    host.ToggleDetail();
                }
                else
                {
                    ScreenReader.Say(Loc.Get("No detail view for this topic."));
                }
                return;
            }
        }

        private void MoveCursor(int delta, ILoreHost host)
        {
            int actionCount = ActionCount(host);

            // Inside the action zone: walk through actions, then back into the
            // last node when the user steps up from the first action.
            if (_actionIndex >= 0)
            {
                int next = _actionIndex + delta;
                if (next < 0)
                {
                    _actionIndex = -1;
                    if (_doc.Nodes.Count > 0)
                    {
                        _nodeIndex = _doc.Nodes.Count - 1;
                        _linkFocused = false;
                        AnnounceFocusedNode();
                    }
                    else
                    {
                        ScreenReader.Say(Loc.Get("Start of document."));
                        _actionIndex = 0;
                        AnnounceFocusedAction(host);
                    }
                    return;
                }
                if (next >= actionCount)
                {
                    ScreenReader.Say(Loc.Get("End of document."));
                    _actionIndex = actionCount - 1;
                    AnnounceFocusedAction(host);
                    return;
                }
                _actionIndex = next;
                AnnounceFocusedAction(host);
                return;
            }

            // Inside the node zone: walk through paragraphs, then fall into
            // the first action when the user steps past the last node.
            if (_doc.Nodes.Count == 0)
            {
                if (actionCount > 0)
                {
                    _actionIndex = 0;
                    _linkFocused = false;
                    AnnounceFocusedAction(host);
                }
                return;
            }
            int target = (_nodeIndex < 0 ? 0 : _nodeIndex) + delta;
            if (target < 0)
            {
                ScreenReader.Say(Loc.Get("Start of document."));
                _nodeIndex = 0;
                AnnounceFocusedNode();
                return;
            }
            if (target >= _doc.Nodes.Count)
            {
                if (actionCount > 0)
                {
                    _actionIndex = 0;
                    _linkFocused = false;
                    AnnounceFocusedAction(host);
                    return;
                }
                ScreenReader.Say(Loc.Get("End of document."));
                _nodeIndex = _doc.Nodes.Count - 1;
                AnnounceFocusedNode();
                return;
            }
            _nodeIndex = target;
            _linkFocused = false;
            AnnounceFocusedNode();
        }

        private void MoveToHeading(int delta)
        {
            if (_doc.Nodes.Count == 0) return;
            // Ctrl+arrow always lands on a heading, even if the user was in
            // the action zone — leaving the actions is the natural step back
            // into the document.
            int from;
            if (_actionIndex >= 0) from = delta > 0 ? _doc.Nodes.Count : -1;
            else from = _nodeIndex < 0 ? (delta > 0 ? -1 : _doc.Nodes.Count) : _nodeIndex;
            int i = from + delta;
            while (i >= 0 && i < _doc.Nodes.Count)
            {
                if (_doc.Nodes[i].Kind == LoreNodeKind.Heading)
                {
                    _nodeIndex = i;
                    _linkFocused = false;
                    _actionIndex = -1;
                    AnnounceFocusedNode();
                    return;
                }
                i += delta;
            }
            ScreenReader.Say(Loc.Get(delta > 0 ? "No further heading." : "No previous heading."));
        }

        private void MoveLink(int delta)
        {
            if (_doc.Links.Count == 0)
            {
                ScreenReader.Say(Loc.Get("No links on this page."));
                return;
            }
            if (_linkIndex < 0)
            {
                _linkIndex = delta > 0 ? 0 : _doc.Links.Count - 1;
            }
            else
            {
                _linkIndex += delta;
                if (_linkIndex < 0) _linkIndex = _doc.Links.Count - 1;
                else if (_linkIndex >= _doc.Links.Count) _linkIndex = 0;
            }
            _linkFocused = true;
            var link = _doc.Links[_linkIndex];
            // Move the paragraph cursor onto the link's host node so a follow-up
            // Up/Down arrow continues from where the link sits rather than from
            // an unrelated earlier position. Leaves the action zone if the user
            // was reading dialog buttons when they pressed Tab.
            if (link.NodeIndex >= 0 && link.NodeIndex < _doc.Nodes.Count)
            {
                _nodeIndex = link.NodeIndex;
                _actionIndex = -1;
            }
            ScreenReader.Say(Loc.Get("Link ") + (_linkIndex + 1) + ": " + LoreTranslation.LinkText(link) + Loc.Get(". Enter to follow."));
        }

        private void AnnounceFocusedNode()
        {
            if (_nodeIndex < 0 || _nodeIndex >= _doc.Nodes.Count) return;
            var node = _doc.Nodes[_nodeIndex];
            ScreenReader.Say(BuildNodeAnnouncement(node, _nodeIndex));
        }

        private void AnnounceFocusedAction(ILoreHost host)
        {
            if (host == null || _actionIndex < 0) return;
            var actions = host.GetActions();
            if (actions == null || _actionIndex >= actions.Length) return;
            ScreenReader.Say(BuildActionAnnouncement(actions[_actionIndex]));
        }

        private static string BuildActionAnnouncement(LoreAction action)
        {
            string label = action != null && !string.IsNullOrEmpty(action.Label) ? action.Label : Loc.Get("Action");
            return Loc.Get("Action: ") + label + Loc.Get(". Press Enter to activate.");
        }

        private string BuildNodeAnnouncement(LoreNode node, int index)
        {
            string text = LoreTranslation.NodeText(node).Trim();
            switch (node.Kind)
            {
                case LoreNodeKind.Heading:
                    return Loc.Get("Heading: ") + text;
                case LoreNodeKind.ListItem:
                    return Loc.Get("Bullet: ") + text;
                default:
                    return text;
            }
        }

        private void AnnounceLinkList()
        {
            if (_doc.Links.Count == 0)
            {
                ScreenReader.Say(Loc.Get("No links on this page."));
                return;
            }
            var sb = new StringBuilder();
            sb.Append(_doc.Links.Count).Append(Loc.Get(_doc.Links.Count == 1 ? " link." : " links."));
            for (int i = 0; i < _doc.Links.Count; i++)
            {
                sb.Append(' ').Append(i + 1).Append(": ").Append(LoreTranslation.LinkText(_doc.Links[i])).Append('.');
            }
            ScreenReader.Say(sb.ToString());
        }

        private static string NodeKindLabel(LoreNode node)
        {
            switch (node.Kind)
            {
                case LoreNodeKind.Heading: return Loc.Get("Heading");
                case LoreNodeKind.ListItem: return Loc.Get("Bullet");
                default: return Loc.Get("Paragraph");
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max - 1) + "…";
        }

        private static string BuildHotkeyHint(ILoreHost host)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Up and Down to read paragraphs, Ctrl plus arrows for headings, Tab for links, Enter to follow, M reads everything, L lists links"));
            if (host != null && host.DetailAvailable) sb.Append(Loc.Get(", D toggles detail"));
            sb.Append(Loc.Get(", Escape closes."));
            return sb.ToString();
        }

        private static bool AnyTextEditFocused()
        {
            // Defensive: the lore dialogs don't carry input fields, but if a
            // future host gains one we don't want letter-key hotkeys to fire
            // mid-typing. Mirrors the guard pattern in KeyboardNavigationHandler.
            var ev = UnityEngine.EventSystems.EventSystem.current;
            if (ev == null) return false;
            var sel = ev.currentSelectedGameObject;
            if (sel == null) return false;
            return sel.GetComponent<UnityEngine.UI.InputField>() != null
                || sel.GetComponent<TMPro.TMP_InputField>() != null;
        }
    }
}
