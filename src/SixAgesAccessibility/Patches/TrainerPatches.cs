using HarmonyLib;

namespace SixAgesAccessibility.Patches
{
    /// <summary>
    /// Captures Tutorial.topicRequired BEFORE
    /// ManagementController.CheckForAdvanceFromElement runs, so the
    /// accessibility announcement that follows in the same frame can read
    /// the pre-advance value. Without this, keyboard navigation that
    /// triggers a trainer advance loses the "(trainer expects this)"
    /// annotation on the item the user just landed on — the advance has
    /// already happened by the time AnnounceListItem reads topicRequired.
    ///
    /// CheckForAdvanceFromElement is declared virtual on ManagementController
    /// and (as of this build) not overridden anywhere, so patching the base
    /// catches every call site (Magic/Wealth/Lore/Reorganize/etc).
    /// </summary>
    [HarmonyPatch]
    public static class TrainerPatches
    {
        [HarmonyPatch(typeof(ManagementController), nameof(ManagementController.CheckForAdvanceFromElement))]
        [HarmonyPrefix]
        static void CheckForAdvanceFromElement_Prefix()
        {
            TrainerInfo.CaptureRequired();
        }
    }
}
