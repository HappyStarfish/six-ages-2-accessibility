using System;
using HarmonyLib;

namespace SixAgesAccessibility.Patches
{
    /// <summary>
    /// Patches around HelpController. The game's help overlay is normally triggered
    /// by clicking a visual help button (helpButtonContainer, see GameManager.Update
    /// line 691) and shows screen-specific guidance: a title, an overall description,
    /// and left/right callouts pointing at UI elements with key/icon legends. Without
    /// these patches the overlay is silent for screen-reader users — visible-only.
    /// </summary>
    [HarmonyPatch]
    public static class HelpPatches
    {
        // OnShow runs UpdateForManagementScreen, which loads the help dict via
        // PluginImport.Game_LoadHelpDataForScreen and writes the resulting strings
        // into the screen's TMP fields. We read from those populated fields in our
        // postfix, after the original method has finished.
        [HarmonyPatch(typeof(HelpController), "OnShow")]
        [HarmonyPostfix]
        public static void OnShow_Postfix(HelpController __instance)
        {
            try
            {
                HelpScreenReader.Announce(__instance);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("HelpPatches.OnShow", ex);
            }
        }

        // Drop the cached announcement so a repeat (F5) on a different screen
        // can't surface stale text from a previous help overlay.
        [HarmonyPatch(typeof(HelpController), "OnHide")]
        [HarmonyPostfix]
        public static void OnHide_Postfix()
        {
            try
            {
                HelpScreenReader.Reset();
            }
            catch (Exception ex)
            {
                DebugLogger.Error("HelpPatches.OnHide", ex);
            }
        }
    }
}
