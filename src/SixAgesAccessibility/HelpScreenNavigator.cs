using System;
using UnityEngine;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Up/Down review of the game's help overlay (<see cref="HelpController"/>),
    /// the panel F1 opens.
    ///
    /// When the overlay appears, <see cref="HelpScreenReader"/> reads it aloud as one
    /// block and also hands its individual sections — screen name, description,
    /// left/right callouts, key list — here via <see cref="SetBlock"/>. The user can
    /// then step through those sections one at a time with Up/Down, the same way scene
    /// text and the tutorial are reviewed.
    ///
    /// Only the arrow keys are consumed; Escape (close) and F5 (repeat) fall through
    /// to their own handlers. Arrow review interrupts speech, exactly like every other
    /// review key in the mod — that behaviour is deliberate.
    /// </summary>
    public class HelpScreenNavigator
    {
        private static HelpScreenNavigator _instance;
        public static HelpScreenNavigator Instance
        {
            get
            {
                if (_instance == null) _instance = new HelpScreenNavigator();
                return _instance;
            }
        }

        // Each section of the help overlay currently shown, in the order it was read
        // aloud. Null when no overlay is open.
        private string[] _paragraphs;
        private int _index = -1;

        /// <summary>
        /// Replace the reviewable block. Called by <see cref="HelpScreenReader.Announce"/>
        /// with the section list of the overlay just shown, and with <c>null</c> from
        /// <see cref="HelpScreenReader.Reset"/> when the overlay closes.
        /// </summary>
        public void SetBlock(string[] paragraphs)
        {
            _paragraphs = paragraphs;
            _index = -1;
        }

        /// <summary>
        /// Handle one input tick while the help overlay is open. Returns <c>true</c>
        /// only when an arrow key was consumed; any other key returns <c>false</c> so
        /// the caller falls through to the generic navigation (close button, Escape).
        /// </summary>
        public bool HandleInput(HelpController help)
        {
            if (help == null) return false;
            if (AnyModifier()) return false;

            bool up = Input.GetKeyDown(KeyCode.UpArrow);
            bool down = Input.GetKeyDown(KeyCode.DownArrow);
            if (!up && !down) return false;

            try
            {
                if (_paragraphs == null || _paragraphs.Length == 0)
                {
                    ScreenReader.Say(Loc.Get("No help text."));
                    return true;
                }

                // First move from -1 lands on section 0 regardless of direction.
                int next = _index + (down ? 1 : -1);
                if (next < 0)
                {
                    if (_index < 0) next = 0;
                    else { ScreenReader.Say(Loc.Get("Beginning of help.")); return true; }
                }
                if (next >= _paragraphs.Length)
                {
                    ScreenReader.Say(Loc.Get("End of help."));
                    return true;
                }

                _index = next;
                ScreenReader.Say(_paragraphs[_index]);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("HelpScreenNavigator.HandleInput", ex);
            }
            return true;
        }

        private static bool AnyModifier()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftShift)   || Input.GetKey(KeyCode.RightShift)
                || Input.GetKey(KeyCode.LeftAlt)     || Input.GetKey(KeyCode.RightAlt);
        }
    }
}
