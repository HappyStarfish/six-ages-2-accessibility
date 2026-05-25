namespace SixAgesAccessibility
{
    /// <summary>
    /// Central source of truth for spoken key-hint phrases.
    ///
    /// <para>Key bindings used to be described as string literals scattered across
    /// <c>KeyboardNavigationHandler.AnnounceScreenShortcuts</c> (the Shift+F1 key-hint
    /// help), the per-navigator orientation hints, <c>DialogContentReader</c> and
    /// <c>ManagementScreenReader</c>. Whenever a binding changed, every copy had to
    /// be found and edited by hand — and one was always missed, so the announcement
    /// then lied (see memory <c>feedback_keyhints_live_in_other_files</c>).</para>
    ///
    /// <para>This class fixes that: every recurring key-hint phrase is defined here
    /// exactly once. The Shift+F1 key-hint help (<c>AnnounceScreenShortcuts</c>) composes
    /// its per-screen text from these plus a short screen-specific literal, so a binding
    /// only ever has to be re-worded in one place. The full binding inventory this
    /// class is derived from lives in <c>docs/keybindings.md</c>.</para>
    ///
    /// <para>Scope: only keys that appear on <b>several</b> screens (or globally) are
    /// centralized here — those are the ones that drift. A key unique to one screen
    /// (War's W/R/C/O, Map's G/K/X) has no copy to drift against and stays a literal
    /// in that one screen's Shift+F1 branch.</para>
    ///
    /// <para>Excluded by decision (2026-05-22): F5, F6 and L are NOT advertised in any
    /// hint text. They remain functional keys in the code; they are simply no longer
    /// promoted, so this class has no property for them.</para>
    ///
    /// <para>Style: short, complete sentences ending in ". " (the wording level the
    /// user picked on 2026-05-22). Each phrase is looked up through <see cref="Loc.Get"/>,
    /// so composition is plain concatenation and German word order stays correct within
    /// each sentence. The English source string is the lookup key; the German values
    /// live in <c>LocSetup.cs</c>.</para>
    ///
    /// <para><see cref="Help"/> and <see cref="Season"/> are not used by the Shift+F1
    /// key-hint help itself; they are kept as the canonical phrasing for the mini-tutorial
    /// wording and the auto-announcement headers (next steps of the mini-tutorial).</para>
    /// </summary>
    public static class Hints
    {
        // ---- Focus movement ----

        /// <summary>Up/Down arrow keys move the focus between items.</summary>
        public static string Arrows => Loc.Get("Arrow keys move the focus. ");

        /// <summary>Left/Right arrow keys adjust the focused slider.</summary>
        public static string Slider => Loc.Get("Left and Right adjust a slider. ");

        /// <summary>Tab and Shift+Tab move between buttons (screens without zones).</summary>
        public static string TabButtons => Loc.Get("Tab cycles the buttons. ");

        /// <summary>Tab and Shift+Tab move between zones (zone-based navigators).</summary>
        public static string TabZones => Loc.Get("Tab cycles the zones. ");

        // ---- Reading aids ----

        /// <summary>D reads a fuller description of the focused item.</summary>
        public static string Describe => Loc.Get("D reads details. ");

        /// <summary>H repeats the current tutorial hint card.</summary>
        public static string Hint => Loc.Get("H repeats the tutorial hint. ");

        // ---- Global function keys ----

        /// <summary>Shift+F1 lists the mod's keys for the current screen. Not echoed
        /// inside that list itself; kept for the mini-tutorial and auto-announcements.</summary>
        public static string Help => Loc.Get("Shift F1 lists the keys for this screen. ");

        /// <summary>F1 opens the game's own help overlay.</summary>
        public static string GameHelp => Loc.Get("F1 opens the game's own help. ");

        /// <summary>F2 / F3 / F4 status, advisor and dashboard keys (management screens).</summary>
        public static string FunctionKeysManagement => Loc.Get("F2 status, F3 advisor, F4 concerns. ");

        /// <summary>F2 / F3 status and advisor keys (scenes — no dashboard there).</summary>
        public static string FunctionKeysScene => Loc.Get("F2 status, F3 advisor. ");

        /// <summary>F10 reads the current combat status.</summary>
        public static string Combat => Loc.Get("F10 reads the combat status. ");

        // ---- Screen-context keys ----

        /// <summary>S advances to the next season. Reserved for the mini-tutorial.</summary>
        public static string Season => Loc.Get("S advances to the next season. ");

        /// <summary>Ctrl+1..9 switch between management screens.</summary>
        public static string ManagementSwitch => Loc.Get("Ctrl 1 to 9 switches the management screen. ");

        /// <summary>F / Shift+F cycle a list filter (Relations, ChooseLeader, …).</summary>
        public static string Filter => Loc.Get("F changes the filter. ");

        /// <summary>I toggles the scene information panel (interactive scenes).</summary>
        public static string SceneInfo => Loc.Get("I toggles the scene information. ");

        /// <summary>P switches between the scene picture and its text (interactive scenes).</summary>
        public static string ScenePicture => Loc.Get("P switches between picture and text. ");
    }
}
