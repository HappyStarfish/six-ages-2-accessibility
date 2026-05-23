using System;
using HarmonyLib;
using UnityEngine;

namespace SixAgesAccessibility.Patches
{
    /// <summary>
    /// Catches every tooltip popup the game shows via <see cref="DescriptionBox"/>. All
    /// hover-triggered explanations route through DescriptionBox.Show — including
    /// AdvisorHelper.ShowInfoFor (advisor / person hover), AdvisorHelper.ShowInfo
    /// (treasure / blessing / goal / clan list synopsis), AdvisorHelper.ShowSeasonInfo
    /// (Sea / Storm / Earth / Fire season hint at the season indicator), and any UI
    /// element configured with a <see cref="Rollover"/> component (Rollover.ShowRollover
    /// instantiates a clone of infoBox and calls Show on it). Without this patch, all of
    /// that content is mouse-only and silent for keyboard / screen-reader users.
    ///
    /// AdvisorHelper.ShowAdvice → adviceBox.Show is already covered by
    /// <see cref="AdvisorPatches.ShowAdvice_Postfix"/> with an "Advisor:" prefix, so we
    /// skip the adviceBox instance to avoid double-speech.
    /// </summary>
    [HarmonyPatch]
    public static class TooltipPatches
    {
        // The game often re-fires Show with the same string (LayoutRebuilder rebuilds,
        // hover-leave-hover cycles, the 0.1s GameManager.Callback path that wraps every
        // ShowInfo call). Collapse same-text repeats inside this window into a single
        // announcement.
        private static string _lastSpoken;
        private static float _lastSpokenTime;
        private const float SameTextCooldown = 1.5f;

        [HarmonyPatch(typeof(DescriptionBox), "Show",
            new Type[] { typeof(string), typeof(Sprite), typeof(Vector2), typeof(PointerDirection) })]
        [HarmonyPostfix]
        public static void Show_Postfix(DescriptionBox __instance, string description)
        {
            try
            {
                if (string.IsNullOrEmpty(description)) return;

                // adviceBox path is owned by AdvisorPatches; skip it to avoid duplication.
                if (AdvisorHelper.instance != null
                    && AdvisorHelper.instance.adviceBox != null
                    && __instance == AdvisorHelper.instance.adviceBox)
                    return;

                string clean = StringHelpers.StripTags(description);
                if (clean.Length == 0) return;

                float now = Time.unscaledTime;
                if (clean == _lastSpoken && now - _lastSpokenTime < SameTextCooldown)
                    return;

                _lastSpoken = clean;
                _lastSpokenTime = now;

                // Queue (interrupt:false) so this never cuts off the announcement that
                // likely TRIGGERED the tooltip — selecting a list item, focusing a
                // button. The user's own follow-up keypress will interrupt this naturally
                // since standard announcements use interrupt:true.
                ScreenReader.Say(clean, interrupt: false);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("TooltipPatches.Show", ex);
            }
        }
    }
}
