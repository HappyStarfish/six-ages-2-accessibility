using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SixAgesAccessibility.Patches
{
    /// <summary>
    /// Harmony patches for map-marker interaction. RelationsScreen, MapScreen and other
    /// MapController-derived screens place clickable "mission marker" UIButtons (gameobject
    /// name "Dot2(Clone)") for each clan that has an available mission of the screen's
    /// missionType. Sighted players read the explanation via a popup; this patch announces
    /// it through the screen reader whenever such a marker is tapped — regardless of whether
    /// the click came from a mouse or from the keyboard navigation handler activating a
    /// focused button.
    /// </summary>
    [HarmonyPatch]
    public static class MapPatches
    {
        private static readonly FieldInfo MissionTypeField =
            typeof(MapController).GetField("missionType", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>Announce the mission explanation text after the game shows it visually.</summary>
        [HarmonyPatch(typeof(MapController), "MissionMarkerTapped")]
        [HarmonyPostfix]
        public static void MissionMarkerTapped_Postfix(MapController __instance, UIButton b)
        {
            try
            {
                if (__instance == null || b == null) return;

                string text = GetMissionExplanation(__instance, b);
                if (string.IsNullOrEmpty(text)) return;

                ScreenReader.Say(text);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapPatches.MissionMarker", ex);
            }
        }

        /// <summary>
        /// Resolve the mission explanation text for a marker button on the given MapController.
        /// Returns null if the button is not a mission marker or the screen has no missionType.
        /// </summary>
        public static string GetMissionExplanation(MapController mc, UIButton b)
        {
            if (mc == null || b == null) return null;
            // Cast to object: Mono 2.0 lacks FieldInfo.op_Equality.
            if ((object)MissionTypeField == null) return null;

            try
            {
                object raw = MissionTypeField.GetValue(mc);
                if (raw == null) return null;
                int missionType = (int)(QType)raw;
                return PluginImport.Game_MissionExplanationForTag(b.intTag, missionType);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MapPatches.GetMissionExplanation", ex);
                return null;
            }
        }

        /// <summary>
        /// True if the given button is a clickable mission marker (instantiated by
        /// MapView.AddMissionMarkers — its GameObject carries a MapElement component).
        /// </summary>
        public static bool IsMissionMarker(UIButton b)
        {
            if (b == null) return false;
            return b.GetComponent<MapElement>() != null;
        }
    }
}
