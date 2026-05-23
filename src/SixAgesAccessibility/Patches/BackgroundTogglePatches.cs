using HarmonyLib;
using UnityEngine;

namespace SixAgesAccessibility.Patches
{
    /// <summary>
    /// Drops the game's hardcoded Space → <see cref="InteractiveController.HandleBackgroundToggle"/>
    /// binding while leaving every other entry point untouched.
    ///
    /// The conflict: <c>InteractiveController.Update</c> contains
    /// <c>if (Input.GetKeyUp(KeyCode.Space) ...) HandleBackgroundToggle();</c>. We use
    /// Space for UIToggle activation in keyboard navigation, and the resulting
    /// <c>CanvasGroup.FadeOut(..., disableGameObject: true)</c> on the textPanel
    /// disables the GameObject after the fade — stripping the clan name field,
    /// restore toggles and Skip button from <c>isActiveAndEnabled</c> and therefore
    /// from our nav. The accessibility mod surfaces the same picture/text toggle on
    /// the dedicated P key, so the Space binding is redundant.
    ///
    /// We don't track state or use a time window. The trigger source is encoded in
    /// Unity's input state itself: <c>Input.GetKeyUp(Space)</c> is true only on the
    /// frame the game's Update fires the call. Our P-key handler and mouse clicks
    /// on the scene picture invoke <c>HandleBackgroundToggle</c> on frames where
    /// that flag is false, so they pass through the prefix unchanged.
    /// </summary>
    [HarmonyPatch(typeof(InteractiveController), "HandleBackgroundToggle")]
    public static class BackgroundTogglePatches
    {
        [HarmonyPrefix]
        public static bool HandleBackgroundToggle_Prefix()
        {
            if (Input.GetKeyUp(KeyCode.Space))
            {
                DebugLogger.Log("BackgroundToggle", "Suppressed Space-driven HandleBackgroundToggle");
                return false;
            }
            return true;
        }
    }
}
