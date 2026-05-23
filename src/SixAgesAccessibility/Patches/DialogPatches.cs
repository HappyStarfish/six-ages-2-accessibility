using System;
using HarmonyLib;

namespace SixAgesAccessibility.Patches
{
    /// <summary>Harmony patches for dialog screen content announcements.</summary>
    [HarmonyPatch]
    public static class DialogPatches
    {
        // ============================================================
        // Choose Leader Dialog
        // ============================================================

        /// <summary>Announce choose leader dialog content. Patched on ShowFor (not OnShow)
        /// because OnShow runs before the candidate list is populated — at OnShow time
        /// d.list.count is 0 and the announcement says "No candidates" even when twelve
        /// candidates are about to be added by ShowFor.</summary>
        [HarmonyPatch(typeof(ChooseLeaderDialog), "ShowFor", new Type[] { typeof(ILeadable), typeof(int) })]
        [HarmonyPostfix]
        public static void ChooseLeader_Shown(ChooseLeaderDialog __instance)
        {
            AnnounceDialog(__instance, "ChooseLeader");
        }

        // ============================================================
        // Management Dialogs
        // ============================================================

        /// <summary>Announce reorganize dialog content.</summary>
        [HarmonyPatch(typeof(ReorganizeDialogController), "OnShow")]
        [HarmonyPostfix]
        public static void Reorganize_Shown(ReorganizeDialogController __instance)
        {
            AnnounceDialog(__instance, "Reorganize");
        }

        /// <summary>Announce venture dialog content.</summary>
        [HarmonyPatch(typeof(VentureDialogController), "OnShow")]
        [HarmonyPostfix]
        public static void Venture_Shown(VentureDialogController __instance)
        {
            AnnounceDialog(__instance, "Venture");
        }

        /// <summary>Announce emissary dialog content.</summary>
        [HarmonyPatch(typeof(EmissaryDialogController), "OnShow")]
        [HarmonyPostfix]
        public static void Emissary_Shown(EmissaryDialogController __instance)
        {
            AnnounceDialog(__instance, "Emissary");
        }

        /// <summary>Announce ritual dialog content.</summary>
        [HarmonyPatch(typeof(RitualDialogController), "OnShow")]
        [HarmonyPostfix]
        public static void Ritual_Shown(RitualDialogController __instance)
        {
            AnnounceDialog(__instance, "Ritual");
        }

        /// <summary>Announce sacrifice dialog content.</summary>
        [HarmonyPatch(typeof(SacrificeDialogController), "OnShow")]
        [HarmonyPostfix]
        public static void Sacrifice_Shown(SacrificeDialogController __instance)
        {
            AnnounceDialog(__instance, "Sacrifice");
        }

        /// <summary>Announce spirit dialog content.</summary>
        [HarmonyPatch(typeof(SpiritDialogController), "OnShow")]
        [HarmonyPostfix]
        public static void Spirit_Shown(SpiritDialogController __instance)
        {
            AnnounceDialog(__instance, "Spirit");
        }

        /// <summary>Announce build/temple dialog content.</summary>
        [HarmonyPatch(typeof(BuildDialogController), "OnShow")]
        [HarmonyPostfix]
        public static void Build_Shown(BuildDialogController __instance)
        {
            AnnounceDialog(__instance, "Build");
        }

        /// <summary>Announce raid dialog content.</summary>
        [HarmonyPatch(typeof(RaidDialogController), "OnShow")]
        [HarmonyPostfix]
        public static void Raid_Shown(RaidDialogController __instance)
        {
            AnnounceDialog(__instance, "Raid");
        }

        /// <summary>Announce warriors dialog content.</summary>
        [HarmonyPatch(typeof(WarriorsDialogController), "OnShow")]
        [HarmonyPostfix]
        public static void Warriors_Shown(WarriorsDialogController __instance)
        {
            AnnounceDialog(__instance, "Warriors");
        }

        /// <summary>Announce caravan dialog content.</summary>
        [HarmonyPatch(typeof(CaravanDialogController), "OnShow")]
        [HarmonyPostfix]
        public static void Caravan_Shown(CaravanDialogController __instance)
        {
            AnnounceDialog(__instance, "Caravan");
        }

        /// <summary>Announce fortify dialog content.</summary>
        [HarmonyPatch(typeof(FortifyDialogController), "OnShow")]
        [HarmonyPostfix]
        public static void Fortify_Shown(FortifyDialogController __instance)
        {
            AnnounceDialog(__instance, "Fortify");
        }

        // ============================================================
        // Combat (Battle phase)
        // ============================================================

        /// <summary>Announce combat status whenever CombatStatusView refreshes. Called on
        /// initial engagement (ProceedToCombatBehindFlash), after every round resolution
        /// (ExecuteCombatOption), and after parley/chance scripts (DoResponseNumber when
        /// isRunningOptionScript). The reader produces a compact state-aware summary so
        /// the announcement is informative on every fire without manual gating.</summary>
        [HarmonyPatch(typeof(CombatStatusView), "UpdateToCombat")]
        [HarmonyPostfix]
        public static void CombatStatus_Updated(CombatStatusView __instance)
        {
            CombatScreenReader.AnnounceCombatUpdate(__instance);
        }

        /// <summary>Announce that the setup phase ends and combat begins. Drops the keyboard
        /// nav's cached ResponseButton list so the combat-option buttons added a few lines
        /// later by AddResponsesFromCombatOptions become reachable on the next Update tick.</summary>
        [HarmonyPatch(typeof(BattleController), "ProceedToCombatBehindFlash")]
        [HarmonyPostfix]
        public static void Battle_ProceedToCombat()
        {
            try
            {
                KeyboardNavigationHandler.RequestRefresh();
                CombatScreenReader.AnnounceCombatStart();
            }
            catch (Exception ex)
            {
                DebugLogger.Error("DialogPatches.ProceedToCombat", ex);
            }
        }

        /// <summary>Refresh the keyboard nav whenever a fresh batch of combat-option
        /// ResponseButtons is built — fired on initial entry, after every round, and when
        /// the player rejects a parley. Also surfaces context the standard ResponseButton
        /// nav can't model: heroic-combat mode + hero identity (Game.ClanPersonVariable
        /// "ourHero") and chance-offered risk markers on individual options.</summary>
        [HarmonyPatch(typeof(BattleController), "AddResponsesFromCombatOptions")]
        [HarmonyPostfix]
        public static void Battle_AddCombatOptions(BattleController __instance)
        {
            try
            {
                KeyboardNavigationHandler.RequestRefresh();
                CombatScreenReader.AnnounceCombatOptionContext(__instance);
            }
            catch (Exception ex) { DebugLogger.Error("DialogPatches.AddCombatOptions", ex); }
        }

        /// <summary>Announce battle-setup structural context after AddInitialPreparation has
        /// populated the textPanel with Magic slider, optional treasure list, objective radios
        /// and Proceed. The intro scene text is announced separately by ScenePatches via the
        /// InitializeFromScript postfix; this hook fills in the section headers
        /// ("Preparation", "Objective") and the attacker/defender + fortifications context
        /// that the toggle labels alone don't convey.</summary>
        [HarmonyPatch(typeof(BattleController), "AddInitialPreparation")]
        [HarmonyPostfix]
        public static void Battle_SetupReady(BattleController __instance)
        {
            try { CombatScreenReader.AnnounceBattleSetup(__instance); }
            catch (Exception ex) { DebugLogger.Error("DialogPatches.SetupReady", ex); }
        }

        /// <summary>Read out the round outcome text after the player chooses a combat option.
        /// Combat.resultText is set by Combat.DoPlayerOption inside ExecuteCombatOption and
        /// added to the textPanel — without this hook the screen-reader user would only
        /// hear the (stat) status update, not the narrative ("Their warriors fall back...").</summary>
        [HarmonyPatch(typeof(BattleController), "ExecuteCombatOption")]
        [HarmonyPostfix]
        public static void Battle_ExecuteOption()
        {
            try { CombatScreenReader.AnnounceRoundResult(); }
            catch (Exception ex) { DebugLogger.Error("DialogPatches.ExecuteOption", ex); }
        }

        // ============================================================
        // Season Advance
        // ============================================================

        private static bool _seasonPanelOpen;
        private static bool _advanceInProgress;

        /// <summary>True while the season-advance slide-in panel is visible.
        /// Read by KeyboardNavigationHandler to route Enter/Escape to Advance/HideSeason
        /// instead of the focused button on the underlying management screen — the
        /// advance/confirm button lives on the SideMenu and isn't part of the per-screen
        /// nav slot collection.</summary>
        public static bool IsSeasonPanelOpen { get { return _seasonPanelOpen; } }

        /// <summary>Announce when season advance panel is toggled. Hands off the
        /// announcement to KeyboardNavigationHandler so the focus state and the spoken
        /// text live in one place.</summary>
        [HarmonyPatch(typeof(ManagementMenuController), "ShowSeason")]
        [HarmonyPostfix]
        public static void Season_Shown()
        {
            try
            {
                _seasonPanelOpen = true;
                KeyboardNavigationHandler.EnterSeasonMode();
            }
            catch (Exception ex)
            {
                DebugLogger.Error("DialogPatches.ShowSeason", ex);
            }
        }

        /// <summary>Snapshot whether the panel was actually open before this call so the
        /// postfix can distinguish a genuine close from the no-op HideSeason that runs again
        /// from ManagementMenuController.OnDisable a few frames after Advance() triggers a
        /// screen change. Without this guard the second call would announce "cancelled"
        /// after the season has already been advanced.</summary>
        [HarmonyPatch(typeof(ManagementMenuController), "HideSeason", new Type[] { typeof(bool) })]
        [HarmonyPrefix]
        public static void Season_Hide_Prefix(out bool __state)
        {
            __state = _seasonPanelOpen;
        }

        /// <summary>Announce when season advance panel is hidden. ExitSeasonMode must
        /// run on EVERY genuine close — both Cancel and Advance — otherwise the modal
        /// keyboard handler stays active and hijacks input on the next screen. The
        /// _advanceInProgress flag only gates the spoken "cancelled" message.</summary>
        [HarmonyPatch(typeof(ManagementMenuController), "HideSeason", new Type[] { typeof(bool) })]
        [HarmonyPostfix]
        public static void Season_Hidden(bool __state)
        {
            try
            {
                _seasonPanelOpen = false;
                if (!__state) return;
                KeyboardNavigationHandler.ExitSeasonMode();
                if (_advanceInProgress) return;
                ScreenReader.Say("Season advance cancelled.", interrupt: false);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("DialogPatches.HideSeason", ex);
            }
        }

        /// <summary>Announce "Advancing season..." BEFORE the action runs, and set the
        /// in-progress flag so the HideSeason call inside Advance() doesn't announce
        /// "cancelled". Speaking from the prefix matters: GC_AdvanceTurn synchronously
        /// triggers the new scene's InitializeFromScript inside the same call stack, so
        /// scene text is already announced (interrupt:true) before a postfix runs. A
        /// postfix announcement would cut off the new scene mid-sentence; a prefix one
        /// gets briefly cut by the new scene instead, which is the right direction.</summary>
        [HarmonyPatch(typeof(ManagementMenuController), "Advance")]
        [HarmonyPrefix]
        public static void Season_Advancing_Prefix()
        {
            _advanceInProgress = true;
            try { ScreenReader.Say(Loc.Get("Advancing season...")); }
            catch (Exception ex) { DebugLogger.Error("DialogPatches.Advance.Prefix", ex); }
        }

        /// <summary>Clear the in-progress flag once Advance returns.</summary>
        [HarmonyPatch(typeof(ManagementMenuController), "Advance")]
        [HarmonyPostfix]
        public static void Season_Advanced()
        {
            _advanceInProgress = false;
        }

        // ============================================================
        // Results Overlay (sacrifice / blessing / spirit results)
        // ============================================================

        /// <summary>
        /// Announce the result text shown after a sacrifice or other ritual action.
        /// ResultsOverlay.ShowResults is static and stores the text in view.result.text.
        /// </summary>
        [HarmonyPatch(typeof(ResultsOverlay), "ShowResults")]
        [HarmonyPostfix]
        public static void ResultsOverlay_Shown(string aString, int aSpeaker)
        {
            try
            {
                if (string.IsNullOrEmpty(aString))
                {
                    ScreenReader.Say(Loc.Get("Result shown. Press Escape or Close to continue."));
                    return;
                }

                string speakerName = null;
                try { speakerName = PluginImport.PC_PersonName(aSpeaker); }
                catch (Exception ex) { DebugLogger.Error("DialogPatches.PC_PersonName", ex); }

                string text = !string.IsNullOrEmpty(speakerName)
                    ? speakerName + Loc.Get(" says: ") + aString
                    : aString;

                ScreenReader.Say(text);
                DebugLogger.Log("DialogPatches", "ResultsOverlay text announced (speaker=" + aSpeaker + ")");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("DialogPatches.ResultsOverlay", ex);
            }
        }

        // ============================================================
        // Helper
        // ============================================================

        private static void AnnounceDialog(ScreenController screen, string source)
        {
            try
            {
                DebugLogger.Log("DialogPatches", source + " OnShow fired");
                DialogContentReader.TryReadSummary(screen);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("DialogPatches." + source, ex);
            }
        }
    }
}
