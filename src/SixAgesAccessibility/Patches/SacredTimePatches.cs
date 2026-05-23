using HarmonyLib;

namespace SixAgesAccessibility.Patches
{
    /// <summary>
    /// Suppresses stale "Sacred Time" announcements during the year-advance.
    ///
    /// When the player presses Proceed on the year-end Sacred Time screen, the
    /// game runs <see cref="SacredTime.Proceed"/> and advances the turn. The
    /// advance dispatches a sequence of News, Scene and dialog screens for the
    /// new year. Sacred Time stays <c>ScreenManager.currentScreen</c> until the
    /// first full-screen scene replaces it, so any dialog dispatched in that
    /// window opens on top of Sacred Time. Every time such a dialog closes,
    /// <c>ScreenManager</c> re-activates the underlying screen and fires
    /// <c>onScreenChanged</c> again — the exact same mechanism by which a Scene
    /// re-announces itself after a ChooseLeader dialog closes.
    ///
    /// Our <see cref="ScreenChangePatches"/> listener and the
    /// <see cref="SacredTimeNavigator"/> then faithfully re-announce a screen
    /// the player has already left for good, while a different scene is on
    /// screen — which is what the user hears as "Sacred Time announced 2-3 more
    /// times" plus the screen briefly flashing up.
    ///
    /// The fix: from the moment Proceed runs until the next genuine
    /// <see cref="SacredTime.OnShow"/> (next year's Sacred Time), treat every
    /// Sacred Time activation as transient and stay silent. <c>OnShow</c> is a
    /// reliable discriminator — a closing dialog only re-activates the screen
    /// (<c>OnActivate</c>); it never re-runs <c>OnShow</c>, and nothing calls
    /// <c>Show(SacredTime)</c> mid-advance.
    /// </summary>
    [HarmonyPatch]
    public static class SacredTimePatches
    {
        private static bool _suppressAnnouncements;

        /// <summary>
        /// True between <see cref="SacredTime.Proceed"/> and the next genuine
        /// <see cref="SacredTime.OnShow"/>. While true, the screen-change
        /// listener and the Sacred Time navigator must not announce Sacred
        /// Time: every activation in this window is a transient re-activation
        /// caused by a year-advance dialog closing on top of it.
        /// </summary>
        public static bool SuppressSacredTimeAnnouncements
        {
            get { return _suppressAnnouncements; }
        }

        /// <summary>
        /// Arm suppression the instant the player advances from Sacred Time.
        /// Runs as a prefix so the flag is set before the turn-advance begins
        /// dispatching the new year's screens.
        /// </summary>
        [HarmonyPatch(typeof(SacredTime), "Proceed")]
        [HarmonyPrefix]
        public static void Proceed_Prefix()
        {
            _suppressAnnouncements = true;
            DebugLogger.Log("SacredTime",
                "Proceed pressed - suppressing Sacred Time re-announcements until the next year");
        }

        /// <summary>
        /// Clear suppression when a genuine Sacred Time screen is shown. OnShow
        /// runs only when the game actually presents the screen (via
        /// ShowManagementScreen), never for the transient re-activations a
        /// closing dialog produces.
        /// </summary>
        [HarmonyPatch(typeof(SacredTime), "OnShow")]
        [HarmonyPostfix]
        public static void OnShow_Postfix()
        {
            if (_suppressAnnouncements)
                DebugLogger.Log("SacredTime",
                    "OnShow - genuine Sacred Time entry, re-enabling announcements");
            _suppressAnnouncements = false;
        }
    }
}
