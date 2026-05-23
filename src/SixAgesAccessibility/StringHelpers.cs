using System.Collections.Generic;
using System.Text;

namespace SixAgesAccessibility
{
    /// <summary>Shared text-processing helpers used by readers and patches.</summary>
    public static class StringHelpers
    {
        /// <summary>
        /// Join a list as "A, B and C" using <paramref name="conjunction"/> ("and",
        /// "und", "or"…) before the last item. Items are inserted verbatim — pass
        /// already-localized strings. Empty list returns "", single item returns
        /// the item itself, two items return "A {conjunction} B".
        /// </summary>
        public static string JoinList(IList<string> items, string conjunction)
        {
            if (items == null || items.Count == 0) return string.Empty;
            if (items.Count == 1) return items[0] ?? string.Empty;
            if (items.Count == 2) return (items[0] ?? "") + " " + conjunction + " " + (items[1] ?? "");

            var sb = new StringBuilder();
            int last = items.Count - 1;
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0)
                {
                    if (i == last) sb.Append(' ').Append(conjunction).Append(' ');
                    else sb.Append(", ");
                }
                sb.Append(items[i] ?? "");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Strip TMP/HTML-style tags ("&lt;b&gt;...&lt;/b&gt;", "&lt;color=...&gt;...", etc.) and trim whitespace.
        /// Manual loop instead of Regex: Unity 2018's bundled Mono rejects RegexOptions in the
        /// Regex(string, RegexOptions) constructor with ArgumentOutOfRangeException, which would
        /// crash the static initializer and bubble up as a TypeInitializationException on every
        /// call — surfacing as game-side error dialogs.
        /// </summary>
        public static string StripTags(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            var sb = new StringBuilder(s.Length);
            bool inTag = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '<') { inTag = true; continue; }
                if (c == '>') { inTag = false; continue; }
                if (!inTag) sb.Append(c);
            }
            return sb.ToString().Trim(' ', '\t', '\r', '\n');
        }

        /// <summary>
        /// Collapse embedded newlines, tabs and runs of whitespace into single spaces
        /// so a multi-line UILabel reads as one flowing phrase. Handles BOTH real
        /// control characters AND the two-character literal "\n" / "\r" that some
        /// Unity prefab labels carry as plain text. Used by save-list and toggle
        /// labels where the visual layout uses line breaks the screen reader doesn't.
        /// </summary>
        public static string FlattenWhitespace(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Replace("\\n", " ").Replace("\\r", " ");
            s = s.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
            while (s.IndexOf("  ") >= 0) s = s.Replace("  ", " ");
            return s.Trim();
        }

        /// <summary>
        /// Map a clan's <see cref="AttitudeColor"/> + <c>inOurTribe</c> flag to the
        /// short spoken phrase that conveys the same thing as the colored
        /// underlined clan name on the map. Sighted players read the relationship
        /// at a glance from name colour (red = hostile, blue = allied, etc.) and
        /// an underline for tribe membership; this is the screen-reader equivalent.
        ///
        /// The enum is exhaustively mapped against
        /// <c>decompiled-firstpass/AttitudeColor.cs</c> (7 values) so a new
        /// value the game might add would fall through to a generic "relations
        /// unknown" rather than crash.
        /// </summary>
        public static string AttitudeLabel(AttitudeColor color, bool inOurTribe)
        {
            // inOurTribe trumps the colour mapping for the friendly side of the
            // spectrum — the map uses an underline on the name, but the colour
            // remains whatever the attitude is. Speak both so the user knows
            // it's a tribesman AND how warm specifically.
            string baseLabel;
            switch (color)
            {
                case AttitudeColor.kHostileColor:    baseLabel = Loc.Get("hostile"); break;
                case AttitudeColor.kUnfriendlyColor: baseLabel = Loc.Get("unfriendly"); break;
                case AttitudeColor.kNeutralColor:    baseLabel = Loc.Get("neutral"); break;
                case AttitudeColor.kFriendlyColor:   baseLabel = Loc.Get("friendly"); break;
                case AttitudeColor.kAlliedColor:     baseLabel = Loc.Get("allied"); break;
                case AttitudeColor.kTribeColor:      baseLabel = Loc.Get("tribe member"); break;
                case AttitudeColor.kOurColor:        baseLabel = Loc.Get("our clan"); break;
                default:                             baseLabel = Loc.Get("relations unknown"); break;
            }

            if (inOurTribe && color != AttitudeColor.kTribeColor && color != AttitudeColor.kOurColor)
                return baseLabel + Loc.Get(", in our tribe");
            return baseLabel;
        }
    }
}
