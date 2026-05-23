using System;
using System.Text;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Owns reading of the floating <see cref="TutorialView"/> hint card. The game
    /// splits long hints across several pages with prev/next buttons; this handler
    /// flattens that — every hint is read as a single string regardless of length,
    /// both for the automatic announcement (see the TutorialView.Init postfix in
    /// <see cref="Patches.MenuPatches"/>) and for the manual H key.
    ///
    /// The page split lives entirely in native code; the managed side only queries
    /// <see cref="TutorialCard.currentText"/> for whatever <see cref="TutorialCard.textIndex"/>
    /// is set. <see cref="BuildFullHintText"/> therefore walks the index across every
    /// page, collects the text, and restores the index. It deliberately never calls
    /// <see cref="TutorialView.Init"/> — that keeps it safe to invoke from inside the
    /// TutorialView.Init Harmony postfix without re-entering it, and avoids triggering
    /// a layout rebuild the blind user does not need.
    /// </summary>
    public class TutorialHintHandler
    {
        private static TutorialHintHandler _instance;
        public static TutorialHintHandler Instance
        {
            get
            {
                if (_instance == null) _instance = new TutorialHintHandler();
                return _instance;
            }
        }

        /// <summary>True if a tutorial hint card is currently visible.</summary>
        public bool HasActiveHint()
        {
            try
            {
                TutorialView tv = TutorialView.instance;
                return tv != null && tv.gameObject.activeSelf;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("TutorialHintHandler.HasActiveHint", ex);
                return false;
            }
        }

        /// <summary>
        /// Reads every page of the currently-visible tutorial hint card and joins them
        /// into one string, so a multi-page hint is announced as a single block instead
        /// of forcing the user to page through it.
        ///
        /// The native page pointer (<see cref="TutorialCard.textIndex"/>) is temporarily
        /// walked across all pages and then restored in a finally block. The visual
        /// <see cref="TutorialView"/> is deliberately NOT rebuilt (no Init call), so this
        /// is safe to call from inside the TutorialView.Init Harmony postfix.
        /// </summary>
        /// <returns>The full hint text, or an empty string if no hint is visible.</returns>
        public string BuildFullHintText()
        {
            try
            {
                TutorialView tv = TutorialView.instance;
                if (tv == null || !tv.gameObject.activeSelf)
                    return "";

                int total = SafeTextCount();
                if (total <= 0)
                    return "";

                int savedIndex = TutorialCard.textIndex;
                StringBuilder sb = new StringBuilder();
                try
                {
                    for (int i = 0; i < total; i++)
                    {
                        TutorialCard.textIndex = i;
                        string page = TutorialCard.currentText;
                        if (!string.IsNullOrEmpty(page))
                        {
                            if (sb.Length > 0) sb.Append(' ');
                            sb.Append(page.Trim());
                        }
                    }
                }
                finally
                {
                    // Always put the native page pointer back where the game left it,
                    // even if a page read threw.
                    TutorialCard.textIndex = savedIndex;
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                DebugLogger.Error("TutorialHintHandler.BuildFullHintText", ex);
                return "";
            }
        }

        /// <summary>
        /// Entry point for the H key. Reads the whole hint (all pages) as one string.
        /// Pressing H again simply re-reads it — there is no page cycle any more.
        ///
        /// If the card is part of the mini-tutorial curriculum, the matching keyboard
        /// hint is appended at the end — same content as the auto-announcement on first
        /// display, just via the non-consuming
        /// <see cref="OnboardingHintHandler.PeekCardHint"/> lookup so the user can hear
        /// it again every time they press H.
        /// </summary>
        public void HandleHKey()
        {
            try
            {
                string full = BuildFullHintText();
                if (string.IsNullOrEmpty(full))
                {
                    ScreenReader.Say(Loc.Get("No hint on this screen."));
                    return;
                }
                string toSay = Loc.Get("Hint: ") + full;
                string onboardingHint = OnboardingHintHandler.Instance.PeekCardHint(full);
                if (!string.IsNullOrEmpty(onboardingHint))
                    toSay += " " + onboardingHint;
                ScreenReader.Say(toSay);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("TutorialHintHandler.HandleHKey", ex);
            }
        }

        private int SafeTextCount()
        {
            try { return TutorialCard.textCount; }
            catch { return 0; }
        }
    }
}
