using System;
using System.Collections.Generic;
using System.Text;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Centralised reader for combat-phase content: status panel, round results,
    /// available options, and the BattleResults casualty table.
    ///
    /// BattleController has two phases. Setup exposes the standard textPanel widgets
    /// (Magic slider, optional treasure list, objective radio group, Proceed button)
    /// which the generic NavSlot collector already handles, so the reader stays out
    /// of that path. Once the battle proper begins, CombatStatusView becomes visible
    /// and the textPanel is filled with combat-option ResponseButtons through
    /// BattleController.AddResponsesFromCombatOptions. From that point on the reader
    /// is the authoritative source for what the screen-reader user hears, because the
    /// status counts, state transitions, and round resultText would otherwise reach
    /// only sighted players.
    ///
    /// All native values come from Combat.us / Combat.them (PluginImport-backed)
    /// rather than the UILabel.text mirrors on CombatStatusView — the labels are not
    /// guaranteed to be set on every code path (the ProceedToCombatBehindFlash flow
    /// updates them right before the first speech we emit) and the underlying ints
    /// are always live. Each native call is isolated in its own try/catch since a
    /// single P/Invoke failure must not silently zero out the rest of the read.
    /// </summary>
    public static class CombatScreenReader
    {
        // ============================================================
        // Per-option risk state
        // ============================================================

        /// <summary>
        /// Combat-option buttons of the current round that carry a "chance"
        /// (PluginImport.CO_ChanceFor on the option key). The game exposes the
        /// risk only through this per-option native flag and a single global
        /// Combat.chanceOffered — there is no per-button marker even for
        /// sighted players — so the reader derives the risk itself and surfaces
        /// it both in the option-list summary and on each button while the user
        /// navigates. The ResponseButton instances are destroyed and recreated
        /// every round, so the set is cleared and refilled by
        /// <see cref="CaptureRiskyOptions"/> rather than accumulated.
        /// </summary>
        private static readonly HashSet<ResponseButton> _riskyOptions = new HashSet<ResponseButton>();

        /// <summary>Cleaned labels of the risky options in screen order — used for the
        /// spoken summary right after the option list is built.</summary>
        private static readonly List<string> _riskyOptionLabels = new List<string>();

        // ============================================================
        // Public entry points
        // ============================================================

        /// <summary>Compact status announcement triggered by every CombatStatusView.UpdateToCombat
        /// call. Fires often (initial engagement, each round, parley/chance scripts) but each
        /// call is informative — counts shift after a resolution, state flips when a parley is
        /// offered, etc. Queued (interrupt:false) so it never cuts a round-result text or a
        /// tutorial hint that may have just been spoken.</summary>
        public static void AnnounceCombatUpdate(CombatStatusView csv)
        {
            try
            {
                if (csv == null || csv.hidden) return;

                string text = BuildCompactStatus();
                if (string.IsNullOrEmpty(text)) return;

                ScreenReader.Say(text, interrupt: false);
                DebugLogger.Log("CombatScreenReader", "Combat status: " + text);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("CombatScreenReader.Update", ex);
            }
        }

        /// <summary>Long-form status for F10. Adds VP needed and helper details on top of the
        /// compact form, plus the outcome line when the battle is final. Returns true if any
        /// status was spoken — false lets the caller fall back to a generic message.</summary>
        public static bool AnnounceFullStatus(BattleController bc)
        {
            try
            {
                if (bc == null) return false;
                CombatStatusView csv = bc.status;
                if (csv == null || csv.hidden)
                {
                    // Setup phase is still active — status is hidden until ProceedToCombat.
                    ScreenReader.Say(Loc.Get("Combat not yet engaged. Adjust the Magic slider, choose a battle treasure if any, choose an objective, then activate Proceed."));
                    return true;
                }

                string text = BuildFullStatus();
                if (string.IsNullOrEmpty(text))
                    return false;

                ScreenReader.Say(text);
                DebugLogger.Log("CombatScreenReader", "F10 full status: " + text);
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("CombatScreenReader.Full", ex);
                return false;
            }
        }

        /// <summary>Announce the round outcome text (Combat.resultText) after the player picks
        /// a combat option. Combat.DoPlayerOption inside ExecuteCombatOption advances the engine
        /// and produces a one- or two-sentence narrative ("Their warriors fall back, exhausted.")
        /// which the textPanel adds visually but no other screen-reader path covers.</summary>
        public static void AnnounceRoundResult()
        {
            try
            {
                // BattleController.ExecuteCombatOption writes Combat.resultText
                // into the round body only when !Combat.isHeroicCombat
                // (decompiled line 249). In heroic combat the per-round
                // narrative comes from Combat.resultScript and is set as the
                // textPanel intro text instead — already reachable via the
                // question header (arrow up). Announcing Combat.resultText here
                // would echo a stale/irrelevant value, so mirror the game's
                // own condition verbatim rather than guessing the heroic path.
                if (SafeIsHeroicCombat()) return;

                string result = "";
                try { result = Combat.resultText; }
                catch (Exception ex) { DebugLogger.Error("CombatScreenReader.RT", ex); }

                if (string.IsNullOrEmpty(result)) return;

                string clean = StringHelpers.StripTags(result);
                if (string.IsNullOrEmpty(clean)) return;

                ScreenReader.Say(Loc.Get("Result: ") + clean, interrupt: false);
                DebugLogger.Log("CombatScreenReader", "Round result said");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("CombatScreenReader.RoundResult", ex);
            }
        }

        /// <summary>Announce the casualty table on BattleResults. The controller wires its
        /// labels to integer game variables that hold the per-side counts; for a cattle raid
        /// the table is hidden (results.gameObject.SetActive(false)) so we skip it.</summary>
        public static void AnnounceBattleResults(BattleResultsController brc)
        {
            try
            {
                if (brc == null) return;

                bool isCattle = false;
                try { isCattle = brc.type == QType.type_CattleRaid; }
                catch (Exception ex) { DebugLogger.Error("CombatScreenReader.IsCattle", ex); }
                if (isCattle) return;

                int eliteWeKilled = SafeInt("eliteWeKilled");
                int eliteWeWounded = SafeInt("eliteWeWounded");
                int regularWeKilled = SafeInt("regularWeKilled");
                int regularWeWounded = SafeInt("regularWeWounded");
                int eliteKilled = SafeInt("eliteKilled");
                int eliteWounded = SafeInt("eliteWounded");
                int regularKilled = SafeInt("regularKilled");
                int regularWounded = SafeInt("regularWounded");

                int themTotal = eliteWeKilled + eliteWeWounded + regularWeKilled + regularWeWounded;
                int usTotal = eliteKilled + eliteWounded + regularKilled + regularWounded;

                string usName = SafeName(true);
                string themName = SafeName(false);

                var sb = new StringBuilder();
                sb.Append(Loc.Get("Casualties. "));

                if (!string.IsNullOrEmpty(themName)) sb.Append(Loc.Get("Enemy ")).Append(themName).Append(": ");
                else sb.Append(Loc.Get("Enemy: "));
                sb.Append(eliteWeKilled).Append(Loc.Get(" elite killed, "))
                  .Append(eliteWeWounded).Append(Loc.Get(" wounded; "))
                  .Append(regularWeKilled).Append(Loc.Get(" regular killed, "))
                  .Append(regularWeWounded).Append(Loc.Get(" wounded."));

                sb.Append(" ");

                if (!string.IsNullOrEmpty(usName)) sb.Append(usName).Append(": ");
                else sb.Append(Loc.Get("Us: "));
                sb.Append(eliteKilled).Append(Loc.Get(" elite killed, "))
                  .Append(eliteWounded).Append(Loc.Get(" wounded; "))
                  .Append(regularKilled).Append(Loc.Get(" regular killed, "))
                  .Append(regularWounded).Append(Loc.Get(" wounded."));

                if (themTotal == 0 && usTotal == 0) sb.Append(Loc.Get(" No casualties on either side."));

                AppendOutcome(sb);

                ScreenReader.Say(sb.ToString(), interrupt: false);
                DebugLogger.Log("CombatScreenReader", "Battle results announced");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("CombatScreenReader.BattleResults", ex);
            }
        }

        /// <summary>Spoken when the setup phase ends and combat begins (ProceedToCombatBehindFlash
        /// postfix). Announces that combat has begun plus the battle context; the combat options
        /// themselves are read by the ResponseButton navigation. The numerical status follows from
        /// the UpdateToCombat call inside the same method. The control hint ("number keys 1 to 9…")
        /// was dropped — it repeated on every battle and the user found it redundant.</summary>
        public static void AnnounceCombatStart()
        {
            try
            {
                string themName = SafeName(false);
                var sb = new StringBuilder();
                sb.Append(Loc.Get("Combat begins"));
                if (!string.IsNullOrEmpty(themName)) sb.Append(Loc.Get(" against ")).Append(themName);
                sb.Append(". ");
                sb.Append(BuildBattleContext());
                if (SafeIsHeroicCombat())
                    sb.Append(Loc.Get("Heroic combat: send a champion to fight alone. "));

                ScreenReader.Say(sb.ToString(), interrupt: false);
                DebugLogger.Log("CombatScreenReader", "Combat start announced");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("CombatScreenReader.Start", ex);
            }
        }

        /// <summary>
        /// Battle setup announcement — fired right after BattleController.AddInitialPreparation
        /// has populated the textPanel with Magic slider, optional treasures, objective toggles
        /// and Proceed. The intro scene text is announced separately by ScenePatches; this hook
        /// fills in the structural context the section headers ("Preparation", "Objective")
        /// would carry visually.
        /// </summary>
        public static void AnnounceBattleSetup(BattleController bc)
        {
            try
            {
                if (bc == null) return;
                int treasures = SafeBattleTreasureCount();

                var sb = new StringBuilder();
                sb.Append(Loc.Get("Battle setup. "));
                sb.Append(BuildBattleContext());
                sb.Append(Loc.Get("Choose magic to spend with the slider. "));
                if (treasures > 0)
                    sb.Append(treasures).Append(Loc.Get(treasures == 1 ? " battle treasure" : " battle treasures"))
                      .Append(Loc.Get(" available — press L to enter the list, then arrow keys, Enter to select. "));
                sb.Append(Loc.Get("Then choose the objective: "));
                sb.Append(DescribeObjectives(SafeBattleType(bc)));
                sb.Append(Loc.Get("Activate Proceed to begin combat."));

                ScreenReader.Say(sb.ToString(), interrupt: false);
                DebugLogger.Log("CombatScreenReader", "Battle setup announced");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("CombatScreenReader.SetupAnnounce", ex);
            }
        }

        /// <summary>
        /// Announce the freshly added combat options after AddResponsesFromCombatOptions —
        /// covers the heroic-combat / hero-card transitions the standard ResponseButton nav
        /// would miss, and names the options that carry a "chance" risk. Captures the
        /// per-option risk first (<see cref="CaptureRiskyOptions"/>) while the native option
        /// list is still current, so the keyboard nav can flag each risky button afterwards.
        /// </summary>
        public static void AnnounceCombatOptionContext(BattleController bc)
        {
            try
            {
                CaptureRiskyOptions(bc);

                var sb = new StringBuilder();
                bool heroicNow = SafeIsHeroicCombat();
                if (heroicNow)
                {
                    string heroName = SafeOurHeroName();
                    if (!string.IsNullOrEmpty(heroName))
                        sb.Append(Loc.Get("Heroic combat. Our champion: ")).Append(heroName).Append(". ");
                    else
                        sb.Append(Loc.Get("Heroic combat. "));
                }

                // Name the risky options outright. The old wording only said "one or more
                // options carry a risk — listen for chance", but no per-option chance hint
                // was ever emitted, so the user had nothing to listen for. The same risk is
                // now also spoken as a prefix on each affected button during navigation.
                if (_riskyOptionLabels.Count > 0)
                {
                    sb.Append(Loc.Get("Risk warning. Caution on: "));
                    sb.Append(string.Join(", ", _riskyOptionLabels.ToArray()));
                    sb.Append(". ");
                }

                if (sb.Length > 0)
                    ScreenReader.Say(sb.ToString(), interrupt: false);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("CombatScreenReader.OptionContext", ex);
            }
        }

        /// <summary>
        /// Scan the freshly built combat-option ResponseButtons and record which ones carry
        /// a "chance" (PluginImport.CO_ChanceFor on the option key). Runs from the
        /// AddResponsesFromCombatOptions postfix while the native option list is still
        /// current — responseButton.index is the option index that CO_Key maps back to the
        /// native key. Each native call is isolated so one failed boundary call cannot drop
        /// the rest of the scan.
        /// </summary>
        private static void CaptureRiskyOptions(BattleController bc)
        {
            _riskyOptions.Clear();
            _riskyOptionLabels.Clear();
            try
            {
                if (bc == null || bc.textPanel == null || bc.textPanel.responseButtons == null)
                    return;

                foreach (ResponseButton rb in bc.textPanel.responseButtons)
                {
                    if (!NavSlotCollector.IsResponseButtonUsable(rb)) continue;

                    bool risky = false;
                    try
                    {
                        string key = PluginImport.CO_Key(rb.index);
                        if (!string.IsNullOrEmpty(key))
                            risky = PluginImport.CO_ChanceFor(key);
                    }
                    catch (Exception ex) { DebugLogger.Error("CombatScreenReader.ChanceFor", ex); }

                    if (!risky) continue;

                    _riskyOptions.Add(rb);
                    string label = (rb.label != null) ? StringHelpers.StripTags(rb.label.text) : null;
                    if (!string.IsNullOrEmpty(label))
                        _riskyOptionLabels.Add(label);
                }
                DebugLogger.Log("CombatScreenReader", "Risky combat options: " + _riskyOptionLabels.Count);
            }
            catch (Exception ex) { DebugLogger.Error("CombatScreenReader.CaptureRisky", ex); }
        }

        /// <summary>True if the given combat-option button carries a chance/risk. Consulted by
        /// the keyboard nav so each risky option is flagged while the user arrows the list.</summary>
        public static bool IsRiskyOption(ResponseButton rb)
        {
            return rb != null && _riskyOptions.Contains(rb);
        }

        // ============================================================
        // Builders
        // ============================================================

        private static string BuildCompactStatus()
        {
            var sb = new StringBuilder();

            int s = SafeStateInt();
            string state = DescribeState();
            if (!string.IsNullOrEmpty(state)) sb.Append(state).Append(". ");

            string usName = SafeName(true);
            string themName = SafeName(false);

            int usE = SafeWarriors(true, true);
            int usR = SafeWarriors(true, false);
            int themE = SafeWarriors(false, true);
            int themR = SafeWarriors(false, false);
            int helpersE = SafeHelpers(true);
            int helpersR = SafeHelpers(false);
            bool usReserves = SafeReserves(true);
            bool themReserves = SafeReserves(false);

            if (!string.IsNullOrEmpty(usName)) sb.Append(usName).Append(": ");
            else sb.Append(Loc.Get("Us: "));
            sb.Append(usE).Append(Loc.Get(" elite"));
            if (helpersE > 0) sb.Append(Loc.Get(" plus ")).Append(helpersE).Append(Loc.Get(" helper elite"));
            sb.Append(", ").Append(usR).Append(Loc.Get(" regular"));
            if (helpersR > 0) sb.Append(Loc.Get(" plus ")).Append(helpersR).Append(Loc.Get(" helper regular"));
            if (usReserves) sb.Append(Loc.Get(", reserves available"));
            sb.Append(". ");

            if (!string.IsNullOrEmpty(themName)) sb.Append(Loc.Get("Enemy ")).Append(themName).Append(": ");
            else sb.Append(Loc.Get("Enemy: "));
            sb.Append(themE).Append(Loc.Get(" elite, ")).Append(themR).Append(Loc.Get(" regular"));
            if (themReserves) sb.Append(Loc.Get(", reserves available"));
            sb.Append(".");

            // Victory points: announced once we're in actual combat. Disengaged
            // = pre-engagement (VP all zero), Final = the outcome line below
            // carries the result instead.
            int vpNeeded = SafeVpNeeded();
            if (vpNeeded > 0
                && s != (int)CombatState.state_Final
                && s != (int)CombatState.state_Disengaged)
            {
                int usVp = SafeVp(true);
                int themVp = SafeVp(false);
                sb.Append(Loc.Get(" Victory points: us ")).Append(usVp)
                  .Append(Loc.Get(", enemy ")).Append(themVp)
                  .Append(Loc.Get(", need ")).Append(vpNeeded).Append(".");
            }

            // At Final the sighted player immediately sees the win/lose picture
            // (CombatStatusView.UpdateToCombat activates winPicture/losePicture).
            // Mirror that in the automatic announcement so the result is spoken
            // the moment combat ends, instead of only later on BattleResults.
            if (s == (int)CombatState.state_Final)
                AppendOutcome(sb);

            return sb.ToString();
        }

        /// <summary>
        /// Battle setup context: who's attacking, fortifications on the
        /// defender's side, cattle-raid flag. Spoken once on combat start
        /// (ProceedToCombatBehindFlash) — once combat is engaged this info
        /// doesn't change, so the per-round AnnounceCombatUpdate stays compact.
        /// </summary>
        private static string BuildBattleContext()
        {
            var sb = new StringBuilder();
            try
            {
                bool usAttacking = Combat.us.isAttacker;
                sb.Append(Loc.Get(usAttacking ? "We are attacking. " : "We are defending. "));

                string enemyKind = DescribeEnemyKind();
                if (!string.IsNullOrEmpty(enemyKind))
                    sb.Append(Loc.Get("Enemy: ")).Append(Loc.Get(enemyKind)).Append(". ");

                bool isCattleRaid = false;
                try { isCattleRaid = Combat.type == QType.type_CattleRaid; }
                catch (Exception ex) { DebugLogger.Error("CombatScreenReader.IsCattle", ex); }

                if (isCattleRaid)
                {
                    sb.Append(Loc.Get("Cattle raid: herds at stake. "));
                }
                else
                {
                    bool fortified = SafeDefenderFortified(usAttacking);
                    sb.Append(Loc.Get(fortified ? "Defender is fortified. "
                                        : "Defender has no fortifications. "));
                }
            }
            catch (Exception ex) { DebugLogger.Error("CombatScreenReader.BattleContext", ex); }
            return sb.ToString();
        }

        /// <summary>
        /// Describe the enemy faction the way the in-game battle icon does.
        /// <c>CombatStatusGrid.UpdateToCombat</c> picks one of seven sprites
        /// based on <c>Combat.them.clan.culture</c> and four stereotype tags
        /// — humans get their cultural label (Vingkotling/Hyaloring/Charioteer),
        /// non-humans get their stereotype (Elves/Alkothi/Dwarves/Trolls).
        /// We mirror that exact selection in words. Empty string when we
        /// can't read the clan (covers null-Combat / between-battles).
        /// </summary>
        private static string DescribeEnemyKind()
        {
            try
            {
                Clan clan = Combat.them.clan;
                if (clan.isNull) return null;

                if (clan.culture == Culture.culture_Orlanthi)  return "Vingkotling";
                if (clan.culture == Culture.culture_Hyaloring) return "Hyaloring";
                if (clan.culture == Culture.culture_Chariot)   return "Charioteers";
                if (clan.IsStereotype("aldryami")) return "Elves";
                if (clan.IsStereotype("alkothi")) return "Alkothi";
                if (clan.IsStereotype("mostali")) return "Dwarves";
                if (clan.IsStereotype("uz")) return "Trolls";
            }
            catch (Exception ex) { DebugLogger.Error("CombatScreenReader.EnemyKind", ex); }
            return null;
        }

        /// <summary>
        /// Mirror the exact wall-icon condition from
        /// <c>CombatStatusGrid.UpdateToCombat</c>. When we attack, the enemy
        /// (defender) shows the BattleIcon_Wall_ sprite iff their clan
        /// fortifications carry bit 0x20. When we defend, our clan shows it
        /// iff bit 0x20 OR bit 0x02 is set. The old "fortifications != 0"
        /// test over-reported: the value can carry non-wall bits, and 0x02
        /// must not count for the attacker case. Returns false on a null
        /// clan / unreadable boundary so we never claim a wall that isn't
        /// shown on screen.
        /// </summary>
        private static bool SafeDefenderFortified(bool usAttacking)
        {
            try
            {
                if (usAttacking)
                {
                    Clan them = Combat.them.clan;
                    if (them.isNull) return false;
                    return (them.fortifications & 0x20) != 0;
                }
                else
                {
                    Clan us = Combat.us.clan;
                    if (us.isNull) return false;
                    return (us.fortifications & 0x20) != 0
                        || (us.fortifications & 0x02) != 0;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("CombatScreenReader.Forts", ex);
                return false;
            }
        }

        private static string BuildFullStatus()
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Combat status. "));
            // Battle setup context (attacker/defender + fortifications + cattle
            // raid flag) — included in F10 so the user can re-orient mid-combat.
            sb.Append(BuildBattleContext());
            sb.Append(BuildCompactStatus());

            // BuildCompactStatus already appends VP for in-progress combat and
            // the outcome line at Final. F10 is also useful at Disengaged or
            // Final, so re-emit VP there to be exhaustive (compact path skips
            // those states to avoid redundancy with the outcome line /
            // pre-combat zero VP). The outcome itself is not re-emitted here —
            // BuildCompactStatus already covers Final.
            int s = SafeStateInt();
            int vpNeeded = SafeVpNeeded();
            if (vpNeeded > 0
                && (s == (int)CombatState.state_Disengaged || s == (int)CombatState.state_Final))
            {
                int usVp = SafeVp(true);
                int themVp = SafeVp(false);
                sb.Append(Loc.Get(" Victory points: us ")).Append(usVp)
                  .Append(Loc.Get(", enemy ")).Append(themVp)
                  .Append(Loc.Get(", needed ")).Append(vpNeeded).Append(".");
            }

            return sb.ToString();
        }

        private static void AppendOutcome(StringBuilder sb)
        {
            int winnerInt;
            try { winnerInt = (int)Combat.winner; }
            catch (Exception ex) { winnerInt = 0; DebugLogger.Error("CombatScreenReader.Winner", ex); }

            if (winnerInt == (int)CombatSide.kUs) sb.Append(Loc.Get(" Outcome: victory."));
            else if (winnerInt == (int)CombatSide.kThem) sb.Append(Loc.Get(" Outcome: defeat."));
            else if (winnerInt == (int)CombatSide.kDraw) sb.Append(Loc.Get(" Outcome: draw."));
            else if (winnerInt == (int)CombatSide.kWePaidTribute) sb.Append(Loc.Get(" Outcome: we paid tribute."));
            else if (winnerInt == (int)CombatSide.kTheyPaidTribute) sb.Append(Loc.Get(" Outcome: enemy paid tribute."));

            // Degree of success: raw integer from
            // PluginImport.Raid_DegreeOfSuccess(). The native scale is
            // undocumented in the decompiled source, so expose the value
            // verbatim instead of inventing qualitative labels.
            int dos = SafeDegreeOfSuccess();
            string dosLabel = DescribeDegreeOfSuccess(dos);
            if (!string.IsNullOrEmpty(dosLabel)) sb.Append(' ').Append(dosLabel);
        }

        private static string DescribeDegreeOfSuccess(int dos)
        {
            return Loc.Get("Degree of success: ") + dos + ".";
        }

        /// <summary>
        /// Verbatim mirror of the objective radio group in
        /// <c>BattleController.AddInitialPreparation</c>. The choices are keyed
        /// off <c>ScriptResult</c>, not <c>weAreDefending</c> — the old
        /// two-way split silently misreported the honor-raid case:
        /// <list type="bullet">
        /// <item>kDoDefensiveBattle / kDoDefensiveCattleRaid → drive off / kill / survival</item>
        /// <item>kDoOffensiveHonorRaid → honor / survival</item>
        /// <item>everything else (incl. kDoDefensiveHonorRaid, matching the
        /// game's own fall-through) → plunder / kill / survival</item>
        /// </list>
        /// </summary>
        private static string DescribeObjectives(ScriptResult type)
        {
            if (type == ScriptResult.kDoDefensiveBattle
                || type == ScriptResult.kDoDefensiveCattleRaid)
                return Loc.Get("drive them off, kill as many as possible, or survival. ");
            if (type == ScriptResult.kDoOffensiveHonorRaid)
                return Loc.Get("honor, or survival. ");
            return Loc.Get("plunder, kill as many as possible, or survival. ");
        }

        private static string DescribeState()
        {
            int s = SafeStateInt();
            switch (s)
            {
                case (int)CombatState.state_Disengaged:     return Loc.Get("Disengaged");
                case (int)CombatState.state_InContact:      return Loc.Get("In contact");
                case (int)CombatState.state_Melee:          return Loc.Get("Melee");
                case (int)CombatState.state_Final:          return Loc.Get("Battle final");
                case (int)CombatState.state_ParleyProposed: return Loc.Get("Parley proposed");
                case (int)CombatState.state_Parleying:      return Loc.Get("Parleying");
                default: return "";
            }
        }

        // ============================================================
        // Safe accessors — every PluginImport call is isolated so a single failed
        // boundary call doesn't zero out the rest of the announcement.
        // ============================================================

        private static int SafeStateInt()
        {
            try { return (int)Combat.state; }
            catch (Exception ex) { DebugLogger.Error("CombatScreenReader.State", ex); return 0; }
        }

        private static string SafeName(bool isUs)
        {
            try { return isUs ? Combat.us.name : Combat.them.name; }
            catch (Exception ex) { DebugLogger.Error("CombatScreenReader.Name", ex); return ""; }
        }

        private static int SafeWarriors(bool isUs, bool elite)
        {
            try
            {
                Combatant c = isUs ? Combat.us : Combat.them;
                return elite ? c.eliteWarriors : c.regularWarriors;
            }
            catch (Exception ex) { DebugLogger.Error("CombatScreenReader.Warriors", ex); return 0; }
        }

        private static int SafeHelpers(bool elite)
        {
            try { return elite ? Combat.us.helperEliteWarriors : Combat.us.helperRegularWarriors; }
            catch (Exception ex) { DebugLogger.Error("CombatScreenReader.Helpers", ex); return 0; }
        }

        private static bool SafeReserves(bool isUs)
        {
            try { return isUs ? Combat.us.haveReserves : Combat.them.haveReserves; }
            catch (Exception ex) { DebugLogger.Error("CombatScreenReader.Reserves", ex); return false; }
        }

        private static int SafeVp(bool isUs)
        {
            try { return isUs ? Combat.us.victoryPoints : Combat.them.victoryPoints; }
            catch (Exception ex) { DebugLogger.Error("CombatScreenReader.Vp", ex); return 0; }
        }

        private static int SafeVpNeeded()
        {
            try { return Combat.vpNeeded; }
            catch (Exception ex) { DebugLogger.Error("CombatScreenReader.VpNeeded", ex); return 0; }
        }

        private static int SafeInt(string varName)
        {
            try { return Game.IntegerVariable(varName); }
            catch (Exception ex) { DebugLogger.Error("CombatScreenReader.Var:" + varName, ex); return 0; }
        }

        private static int SafeDegreeOfSuccess()
        {
            try { return Combat.degreeOfSuccess; }
            catch (Exception ex) { DebugLogger.Error("CombatScreenReader.DoS", ex); return 0; }
        }

        private static bool SafeIsHeroicCombat()
        {
            try { return Combat.isHeroicCombat; }
            catch (Exception ex) { DebugLogger.Error("CombatScreenReader.Heroic", ex); return false; }
        }

        private static int SafeBattleTreasureCount()
        {
            try { return PluginImport.PC_InitList_BattleTreasures(); }
            catch (Exception ex) { DebugLogger.Error("CombatScreenReader.Treasures", ex); return 0; }
        }

        private static ScriptResult SafeBattleType(BattleController bc)
        {
            // Neutral fallback on an unreadable boundary -> generic
            // plunder/kill/survival hint (the most common offensive case).
            try { return bc.type; }
            catch (Exception ex)
            {
                DebugLogger.Error("CombatScreenReader.BattleType", ex);
                return ScriptResult.kDoOffensiveBattle;
            }
        }

        private static string SafeOurHeroName()
        {
            try
            {
                int idx = Game.ClanPersonVariable("ourHero");
                if (idx <= 0) return null;
                Person p = PlayerClan.PersonWithIndex(idx);
                return p != null ? p.name : null;
            }
            catch (Exception ex) { DebugLogger.Error("CombatScreenReader.HeroName", ex); return null; }
        }
    }
}
