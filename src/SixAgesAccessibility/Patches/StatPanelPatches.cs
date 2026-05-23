using System;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace SixAgesAccessibility.Patches
{
    /// <summary>
    /// Announces changes to the persistent StatPanel resources — herds, goods,
    /// warriors, magic, like, hate, fear, mock.
    ///
    /// <para>The game communicates a StatPanel change only visually:
    /// <c>UIStatText.SetValue</c> animates the number and tints it green/red,
    /// with no textual delta — not even for sighted players. Screen-reader users
    /// get nothing. This patch diffs the panel against its previous values and
    /// speaks the difference whenever the game runs an animated refresh.</para>
    ///
    /// <para>Scene/event resource changes are deliberately left to
    /// <see cref="ScenePatches"/>' ShowDeltas hook (the floating delta labels);
    /// announcing here as well while a scene is active would double-speak them, so
    /// the announcement is suppressed for InteractiveController screens — the
    /// baseline is still refreshed so the change is absorbed silently and the next
    /// management-screen diff stays correct.</para>
    /// </summary>
    [HarmonyPatch]
    public static class StatPanelPatches
    {
        // Resource names, index-aligned with ReadValues(). All eight are already
        // registered in LocSetup, so Loc.Get yields the German word.
        private static readonly string[] _names =
        {
            "Herds", "Goods", "Warriors", "Magic", "Like", "Hate", "Fear", "Mock",
        };

        // Last observed StatPanel values, one per _names entry. Null until the
        // first UpdateStats call establishes a baseline.
        private static int[] _lastValues;

        /// <summary>
        /// Diff the StatPanel after the game refreshes it and speak the changes.
        /// Runs as a postfix so the UIStatText.value fields already hold the new
        /// values. Announces only on an animated, non-prolog refresh that is not
        /// covering an interactive scene; the baseline is refreshed every call so
        /// non-animated repopulates (panel slide-in, save load) and scene changes
        /// stay consistent.
        /// </summary>
        [HarmonyPatch(typeof(StatPanel), "UpdateStats")]
        [HarmonyPostfix]
        public static void UpdateStats_Postfix(StatPanel __instance, bool animated, bool hidePrologStats)
        {
            try
            {
                if (__instance == null) return;

                int[] current = ReadValues(__instance);
                if (current == null) return;

                bool haveBaseline = _lastValues != null && _lastValues.Length == current.Length;

                if (haveBaseline && animated && !hidePrologStats && !InInteractiveScene())
                {
                    string line = BuildDeltaLine(current);
                    if (!string.IsNullOrEmpty(line))
                        ScreenReader.Say(line, interrupt: false);
                }

                // Refresh the baseline on every call — including non-animated
                // repopulates (panel slide-in, save load) and scene changes — so
                // the next diff is always against the latest displayed values.
                _lastValues = current;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("StatPanelPatches.UpdateStats", ex);
            }
        }

        /// <summary>Read the eight StatPanel values, or null if any field is missing.</summary>
        private static int[] ReadValues(StatPanel p)
        {
            if (p.herdsText == null || p.goodsText == null || p.swordsText == null
                || p.magicText == null || p.likeText == null || p.hateText == null
                || p.fearText == null || p.mockText == null)
                return null;

            return new int[]
            {
                p.herdsText.value, p.goodsText.value, p.swordsText.value, p.magicText.value,
                p.likeText.value, p.hateText.value, p.fearText.value, p.mockText.value,
            };
        }

        /// <summary>
        /// Build "Resource changes: Goods minus 3, Magic plus 5." from the diff
        /// against <see cref="_lastValues"/>, or null if nothing changed.
        /// </summary>
        private static string BuildDeltaLine(int[] current)
        {
            StringBuilder sb = new StringBuilder();
            int written = 0;
            for (int i = 0; i < current.Length; i++)
            {
                int delta = current[i] - _lastValues[i];
                if (delta == 0) continue;
                if (written > 0) sb.Append(", ");
                sb.Append(Loc.Get(_names[i]));
                sb.Append(' ');
                sb.Append(delta > 0 ? Loc.Get("plus ") : Loc.Get("minus "));
                sb.Append(Mathf.Abs(delta));
                written++;
            }
            if (written == 0) return null;
            return Loc.Get("Resource changes: ") + sb.ToString() + ".";
        }

        /// <summary>
        /// True while an interactive scene/event is the active screen. Its resource
        /// changes are announced by ScenePatches.ShowDeltas, so StatPanelPatches
        /// stays silent to avoid double-speaking them.
        /// </summary>
        private static bool InInteractiveScene()
        {
            try
            {
                ScreenManager sm = Singleton<ScreenManager>.instance;
                return sm != null && sm.activeScreen is InteractiveController;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("StatPanelPatches.InInteractiveScene", ex);
                return false;
            }
        }
    }
}
