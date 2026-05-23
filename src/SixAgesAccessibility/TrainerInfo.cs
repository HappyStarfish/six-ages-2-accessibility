using System;
using System.Text;
using UnityEngine;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Helpers for surfacing the in-game Trainer's expectations to a screen
    /// reader. The Trainer (guided tutorial) locks every screen down to a single
    /// interactable element (<c>Tutorial.topicAvailableUI</c>) and sometimes
    /// further demands a specific value (<c>Tutorial.topicRequired</c>) before
    /// it advances to the next topic. Without spoken cues for these locks a
    /// blind user can pick the wrong clan / deity / option, find buttons
    /// mysteriously grey, and have no clue that the Trainer is the cause.
    ///
    /// All gates here key on <c>topicAvailableUI</c> being non-empty rather
    /// than <c>Tutorial.isTrainer</c>. Diagnostic logs from earlier play
    /// sessions showed our previous <c>isTrainer</c> gate never firing during
    /// the management-screen summary even when Trainer-side button locks
    /// were clearly active — likely a timing artifact where the flag flips
    /// async around topic transitions. <c>topicAvailableUI</c> reflects the
    /// Trainer's actual current expectation more reliably.
    /// </summary>
    public static class TrainerInfo
    {
        // Pre-advance capture of Tutorial.topicRequired. Set by the Harmony
        // prefix on ManagementController.CheckForAdvanceFromElement (see
        // Patches.TrainerPatches). Used by MarkIfRequired to dodge the race
        // where keyboard navigation triggers OnItemClicked →
        // CheckForAdvanceFromElement (advances the trainer) BEFORE the
        // accessibility announcement reads topicRequired — a live read here
        // would always see the post-advance state and lose the annotation on
        // the very item the user just landed on. The frame stamp is the
        // freshness gate: outside a 1-frame window we fall back to live state
        // (which is what we want for non-navigation announcements like
        // screen-open auto-reads).
        private static volatile string _capturedRequired;
        private static int _capturedFrame = -1;

        /// <summary>Snapshot the current Tutorial.topicRequired into a static so
        /// the next MarkIfRequired call in the same frame can use the pre-advance
        /// value. Called from a Harmony prefix on CheckForAdvanceFromElement.</summary>
        internal static void CaptureRequired()
        {
            int currentFrame = -1;
            try { currentFrame = Time.frameCount; } catch { }

            // Only capture once per frame. The OnItemClicked →
            // CheckForAdvanceFromElement → UpdateForSelectedRow →
            // CheckForAdvanceFromElement chain fires this method twice per
            // arrow press (the second call comes from MagicScreenController
            // line 536, inside UpdateForSelectedRow), and that second call
            // sees the POST-advance state (topicRequired == null after the
            // trainer just advanced past "select Uralda"). We want to keep
            // the FIRST capture in a frame, which is the pre-advance value
            // that the announcement should compare against.
            if (currentFrame >= 0 && currentFrame == _capturedFrame) return;

            _capturedFrame = currentFrame;
            try { _capturedRequired = Tutorial.topicRequired; }
            catch (Exception ex)
            {
                DebugLogger.Error("TrainerInfo.CaptureRequired", ex);
                _capturedRequired = null;
            }
        }

        /// <summary>True when the Trainer currently has a topic with an expected UI element.</summary>
        public static bool IsTopicActive
        {
            get
            {
                try { return !string.IsNullOrEmpty(Tutorial.topicAvailableUI); }
                catch (Exception ex)
                {
                    DebugLogger.Error("TrainerInfo.IsTopicActive", ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// Append "Trainer expects: List, specifically Boskoving." (or similar) when
        /// a topic is active. The <paramref name="prefix"/> is prepended only if
        /// the hint actually fires (e.g. ". " to join into a previous sentence).
        /// </summary>
        public static void AppendHint(StringBuilder sb, string prefix)
        {
            try
            {
                string expected = Tutorial.topicAvailableUI;
                bool isTrainer = false;
                string required = null;
                try { isTrainer = Tutorial.isTrainer; } catch { }
                try { required = Tutorial.topicRequired; } catch { }

                if (DebugLogger.IsActive)
                {
                    DebugLogger.Log("TrainerInfo", "AppendHint check: isTrainer="
                        + isTrainer + ", availableUI='" + (expected ?? "")
                        + "', required='" + (required ?? "") + "'");
                }

                if (string.IsNullOrEmpty(expected)) return;

                sb.Append(prefix).Append("Trainer expects action: ").Append(expected);
                if (!string.IsNullOrEmpty(required))
                    sb.Append(", specifically ").Append(required);
                sb.Append('.');
            }
            catch (Exception ex)
            {
                DebugLogger.Error("TrainerInfo.AppendHint", ex);
            }
        }

        /// <summary>
        /// If <paramref name="value"/> exactly matches the Trainer's
        /// <c>topicRequired</c> string, return " (trainer expects this)" so a
        /// caller can suffix the list-item announcement; otherwise return empty.
        /// Use this when speaking each list item so cycling reveals which
        /// option the Trainer is waiting for — the per-screen hint alone gets
        /// missed if the user starts scrolling immediately.
        /// </summary>
        public static string MarkIfRequired(string value)
        {
            try
            {
                if (string.IsNullOrEmpty(value)) return "";

                // Within the navigation race window prefer the pre-advance
                // capture; outside it use live state. The 1-frame window is
                // tight enough to keep stale captures from leaking across
                // unrelated screens, but wide enough to cover the
                // OnItemClicked → AnnounceListItem path which fires both
                // within a single Update tick.
                string req;
                int frame = -1;
                try { frame = Time.frameCount; } catch { }
                if (_capturedRequired != null && frame >= 0
                    && frame - _capturedFrame <= 1)
                {
                    req = _capturedRequired;
                }
                else
                {
                    req = Tutorial.topicRequired;
                }

                if (string.IsNullOrEmpty(req)) return "";
                return string.Equals(value, req, StringComparison.Ordinal)
                    ? " (trainer expects this)"
                    : "";
            }
            catch (Exception ex)
            {
                DebugLogger.Error("TrainerInfo.MarkIfRequired", ex);
                return "";
            }
        }

        /// <summary>
        /// When a button is locked because the Trainer wants a different
        /// action, return a human-readable reason. Returns <c>null</c> if no
        /// Trainer topic is active or if the named button is itself the
        /// expected action (in which case the lock must come from a game-mechanic
        /// condition, not the Trainer).
        ///
        /// <paramref name="buttonName"/> must match the key the game uses in
        /// <c>elementsSubjectToAvailability</c> (e.g. "Send", "ChooseLeader",
        /// "Build", "Sacrifice"). Mismatched names will appear "locked" by
        /// this helper even when the Trainer doesn't actually gate them.
        /// </summary>
        public static string LockReasonForButton(string buttonName)
        {
            try
            {
                string expected = Tutorial.topicAvailableUI;
                if (string.IsNullOrEmpty(expected)) return null;
                if (string.Equals(buttonName, expected, StringComparison.Ordinal))
                    return null;

                string required = Tutorial.topicRequired;
                string msg = "Locked by trainer. Trainer expects action: " + expected;
                if (!string.IsNullOrEmpty(required))
                    msg += ", specifically " + required;
                return msg + ".";
            }
            catch (Exception ex)
            {
                DebugLogger.Error("TrainerInfo.LockReasonForButton", ex);
                return null;
            }
        }
    }
}
