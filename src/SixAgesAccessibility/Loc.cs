using System.Collections.Generic;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Localization lookup for screen-reader strings.
    ///
    /// <para>Language policy — the accessibility mod is English by default. Every announcement
    /// is wrapped in <see cref="Get"/>, whose key IS the English source string. When the
    /// separate German translation mod is installed, <see cref="Plugin"/> detects it and calls
    /// <c>LocSetup.RegisterGerman</c>, which fills this table with German values; lookups then
    /// return German. With no German registered, <see cref="Get"/> returns the key itself,
    /// i.e. English. So the same code path produces English or German purely from whether the
    /// German table was registered — see <see cref="GermanActive"/>.</para>
    ///
    /// <para>Consequence for callers: a screen-reader string only follows the language switch
    /// if it goes through <see cref="Get"/>. A raw literal passed straight to
    /// <c>ScreenReader.Say</c> stays English even with the translation mod active.</para>
    /// </summary>
    public static class Loc
    {
        private static readonly Dictionary<string, string> _strings = new Dictionary<string, string>();

        /// <summary>
        /// True when the German translation mod is active and the German string table has been
        /// registered. Announcement code normally does not need this — routing through
        /// <see cref="Get"/> is enough — but it is exposed for diagnostics and for the rare
        /// announcement that must branch on language (e.g. word order).
        /// </summary>
        public static bool GermanActive { get; private set; }

        /// <summary>Record the resolved output language. Called once from <c>Plugin.Awake</c>.</summary>
        public static void SetLanguageGerman(bool german)
        {
            GermanActive = german;
        }

        /// <summary>
        /// Look up a localized string by key. Falls back to the key itself if the key is not
        /// registered, so the screen reader still says something meaningful even when a string
        /// hasn't been added to the table yet.
        /// </summary>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            string value;
            return _strings.TryGetValue(key, out value) ? value : key;
        }

        /// <summary>
        /// Register a key/value pair. Intended for the future loader; called from setup code
        /// when a strings file is parsed. Overwrites any existing value for the key.
        /// </summary>
        public static void Register(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            _strings[key] = value ?? string.Empty;
        }
    }
}
