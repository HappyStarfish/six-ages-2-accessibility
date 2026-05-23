using System;
using UnityEngine;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Up/Down review of the full-screen <see cref="TutorialController"/> text.
    ///
    /// The trainer often shows several tutorial topics in immediate succession.
    /// <see cref="Patches.MenuPatches"/> announces such a run as one combined block
    /// and hands the whole paragraph list here via <see cref="SetBlock"/>, so Up/Down
    /// always cover exactly what was read aloud — every topic of the block, in order.
    ///
    /// The class only consumes the arrow keys; Enter on Continue and Tab fall through
    /// to the generic keyboard navigation. Arrow review interrupts speech, exactly
    /// like every other review key in the mod — that behaviour is deliberate.
    /// </summary>
    public class TutorialScreenNavigator
    {
        private static TutorialScreenNavigator _instance;
        public static TutorialScreenNavigator Instance
        {
            get
            {
                if (_instance == null) _instance = new TutorialScreenNavigator();
                return _instance;
            }
        }

        // Every paragraph of the current tutorial block, in the order it was read
        // aloud: title + text of each topic, oldest topic first.
        private string[] _paragraphs;
        private int _index = -1;

        /// <summary>
        /// Replace the reviewable block. Called by the tutorial announce path with the
        /// full paragraph list of the block currently on screen (one topic, or several
        /// the trainer chained together), so Up/Down match what was just read aloud.
        /// </summary>
        public void SetBlock(string[] paragraphs)
        {
            _paragraphs = paragraphs;
            _index = -1;
        }

        /// <summary>
        /// Handle one input tick for the tutorial screen. Returns <c>true</c> only
        /// when an arrow key was consumed; any other key returns <c>false</c> so the
        /// caller falls through to the generic navigation (Continue button).
        /// </summary>
        public bool HandleInput(TutorialController tc)
        {
            if (tc == null) return false;
            if (AnyModifier()) return false;

            bool up = Input.GetKeyDown(KeyCode.UpArrow);
            bool down = Input.GetKeyDown(KeyCode.DownArrow);
            if (!up && !down) return false;

            try
            {
                if (_paragraphs == null || _paragraphs.Length == 0)
                {
                    ScreenReader.Say(Loc.Get("No tutorial text."));
                    return true;
                }

                // First move from -1 lands on paragraph 0 regardless of direction.
                int next = _index + (down ? 1 : -1);
                if (next < 0)
                {
                    if (_index < 0) next = 0;
                    else { ScreenReader.Say(Loc.Get("Beginning of tutorial.")); return true; }
                }
                if (next >= _paragraphs.Length)
                {
                    ScreenReader.Say(Loc.Get("End of tutorial. Press Enter to continue."));
                    return true;
                }

                _index = next;
                ScreenReader.Say(_paragraphs[_index]);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("TutorialScreenNavigator.HandleInput", ex);
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
