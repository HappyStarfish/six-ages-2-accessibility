using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Reads the contents of the in-game HelpController (the per-screen help overlay
    /// the game shows when F1 is pressed). The overlay's text is populated by
    /// HelpController.UpdateForManagementScreen via PluginImport's Dict_* API; we read
    /// it back from the populated TextMeshPro fields so we don't depend on the
    /// dictionary still being loaded at re-read time.
    ///
    /// The overlay is read aloud as one block on open, and its individual sections —
    /// screen name, description, left/right callouts, key list — are handed to
    /// <see cref="HelpScreenNavigator"/> so the user can step through them with Up/Down,
    /// the same way scene text and the tutorial are reviewed.
    /// </summary>
    public static class HelpScreenReader
    {
        // Cached announcement of the most recently shown help overlay so F5 can
        // repeat it without re-loading the dictionary (which would be needed if we
        // pulled from the Dict_* API instead of the TMP fields).
        private static string _lastAnnouncement;

        /// <summary>
        /// Build and speak an announcement for the help overlay, and hand its sections
        /// to <see cref="HelpScreenNavigator"/> for Up/Down review. Called from the
        /// HelpController.OnShow postfix after UpdateForManagementScreen has populated
        /// the screen-name, overall and left/right callout TMP fields.
        /// </summary>
        public static void Announce(HelpController help)
        {
            try
            {
                if (help == null) return;

                List<string> sections = BuildSections(help);
                HelpScreenNavigator.Instance.SetBlock(sections.ToArray());
                if (sections.Count == 0) return;

                var sb = new StringBuilder();
                sb.Append(Loc.Get("Help. "));
                for (int i = 0; i < sections.Count; i++)
                    sb.Append(sections[i]).Append(' ');
                sb.Append(Loc.Get("Use Up and Down to read it section by section. Escape closes."));

                _lastAnnouncement = sb.ToString();
                ScreenReader.Say(_lastAnnouncement);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("HelpScreenReader.Announce", ex);
            }
        }

        /// <summary>Re-speak the most recent help announcement (F5). Returns true if handled.</summary>
        public static bool TryRepeat()
        {
            if (string.IsNullOrEmpty(_lastAnnouncement)) return false;
            ScreenReader.Say(_lastAnnouncement);
            return true;
        }

        /// <summary>Drop the cached announcement and review block when the overlay closes.</summary>
        public static void Reset()
        {
            _lastAnnouncement = null;
            HelpScreenNavigator.Instance.SetBlock(null);
        }

        /// <summary>
        /// Split the help overlay into the sections the user reviews with Up/Down:
        /// screen name, overall description, left callout, right callout, global keys.
        /// Empty sections are skipped so the review never lands on a blank step.
        /// </summary>
        private static List<string> BuildSections(HelpController help)
        {
            var list = new List<string>();

            string name = SafeText(help.screenName);
            if (name.Length > 0)
                list.Add(name.Trim() + ".");

            string overall = StringHelpers.StripTags(SafeText(help.screenOverall));
            if (overall.Length > 0)
                list.Add(overall.Trim());

            string left = BuildCallout(Loc.Get("Left:"), help.leftCallout, help.leftCalloutGroup, help);
            if (left.Length > 0)
                list.Add(left);

            string right = BuildCallout(Loc.Get("Right:"), help.rightCallout, help.rightCalloutGroup, help);
            if (right.Length > 0)
                list.Add(right);

            // Global key list — parented to overallContent in HelpController.AddKey.
            string keys = BuildKeyList(Loc.Get("Keys:"), help.overallContent, help);
            if (keys.Length > 0)
                list.Add(keys);

            return list;
        }

        private static string BuildCallout(string prefix, TMPro.TextMeshProUGUI text,
            RectTransform group, HelpController help)
        {
            if (text == null || !text.gameObject.activeSelf) return string.Empty;
            string body = StringHelpers.StripTags(text.text);
            if (body.Length == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.Append(prefix).Append(' ').Append(body.Trim());

            string keys = BuildKeyList(null, group, help);
            if (keys.Length > 0)
                sb.Append(' ').Append(keys);

            return sb.ToString();
        }

        // Each non-hidden UILabelWithIcon in HelpController.keyElements is parented
        // to either overallContent (global keys), leftCalloutGroup or rightCalloutGroup.
        // We pick the ones parented to the requested container.
        private static string BuildKeyList(string label, RectTransform container, HelpController help)
        {
            if (help.keyElements == null || container == null) return string.Empty;

            var sb = new StringBuilder();
            bool any = false;
            for (int i = 0; i < help.keyElements.Count; i++)
            {
                UILabelWithIcon el = help.keyElements[i];
                if (el == null) continue;
                if (el.hidden) continue;
                if (el.transform.parent != container) continue;

                string keyText = StringHelpers.StripTags(el.text);
                if (keyText.Length == 0) continue;

                if (!any)
                {
                    if (!string.IsNullOrEmpty(label))
                        sb.Append(label).Append(' ');
                    any = true;
                }
                else
                {
                    sb.Append(", ");
                }
                sb.Append(keyText);
            }
            if (any) sb.Append('.');
            return sb.ToString();
        }

        private static string SafeText(TMPro.TextMeshProUGUI tmp)
        {
            if (tmp == null) return string.Empty;
            string s = tmp.text;
            return string.IsNullOrEmpty(s) ? string.Empty : s;
        }
    }
}
