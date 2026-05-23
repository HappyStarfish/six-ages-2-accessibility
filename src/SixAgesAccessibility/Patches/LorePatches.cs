using System;
using HarmonyLib;
using SixAgesAccessibility.Lore;

namespace SixAgesAccessibility.Patches
{
    /// <summary>
    /// Patches that feed the in-game lore reader. MythDialogController.ShowTopic
    /// is the single funnel for initial open, link-follow, and detail toggle —
    /// hooking its postfix covers all three with one patch. OnHide clears the
    /// dispatcher's ownership so non-Lore screens don't keep routing input
    /// into a stale reader.
    /// </summary>
    [HarmonyPatch]
    public static class LorePatches
    {
        // ShowTopic(string filename, bool detail, string anchor, bool animated)
        // is private — Harmony patches it fine, but we bind by argument names so
        // the patch keeps working if the signature gets reordered (it hasn't,
        // but the cost of being defensive is one attribute line).
        [HarmonyPatch(typeof(MythDialogController), "ShowTopic",
            new[] { typeof(string), typeof(bool), typeof(string), typeof(bool) })]
        [HarmonyPostfix]
        public static void Myth_ShowTopic_Postfix(
            MythDialogController __instance,
            string filename,
            bool detail)
        {
            try
            {
                LoreDialogDispatcher.OnMythTopic(__instance, filename, detail);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("LorePatches.Myth_ShowTopic", ex);
            }
        }

        [HarmonyPatch(typeof(MythDialogController), "OnHide")]
        [HarmonyPostfix]
        public static void Myth_OnHide_Postfix(MythDialogController __instance)
        {
            try
            {
                LoreDialogDispatcher.OnDialogHide(__instance);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("LorePatches.Myth_OnHide", ex);
            }
        }

        // InfoDialog: single-page intro. OnShow runs after webView is ready and
        // ShowIntro has set the URL — we ignore the in-game browser entirely
        // and pull the same HTML straight from the archive.
        [HarmonyPatch(typeof(InfoDialogController), "OnShow")]
        [HarmonyPostfix]
        public static void Info_OnShow_Postfix(InfoDialogController __instance)
        {
            try
            {
                LoreDialogDispatcher.OnInfoShown(__instance);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("LorePatches.Info_OnShow", ex);
            }
        }

        [HarmonyPatch(typeof(InfoDialogController), "OnHide")]
        [HarmonyPostfix]
        public static void Info_OnHide_Postfix(InfoDialogController __instance)
        {
            try
            {
                LoreDialogDispatcher.OnDialogHide(__instance);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("LorePatches.Info_OnHide", ex);
            }
        }

        // ManualDialog: full manual + TOC. OnShow may load the manual fresh or
        // re-scroll to a startingAnchor when the dialog is reopened. Either way
        // our flat reader treats it as one big document.
        [HarmonyPatch(typeof(ManualDialogController), "OnShow")]
        [HarmonyPostfix]
        public static void Manual_OnShow_Postfix(ManualDialogController __instance)
        {
            try
            {
                LoreDialogDispatcher.OnManualShown(__instance);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("LorePatches.Manual_OnShow", ex);
            }
        }

        [HarmonyPatch(typeof(ManualDialogController), "OnHide")]
        [HarmonyPostfix]
        public static void Manual_OnHide_Postfix(ManualDialogController __instance)
        {
            try
            {
                LoreDialogDispatcher.OnDialogHide(__instance);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("LorePatches.Manual_OnHide", ex);
            }
        }
    }
}
