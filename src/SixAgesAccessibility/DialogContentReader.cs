using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SixAgesAccessibility
{
    /// <summary>Reads dialog and special screen content for screen reader output.</summary>
    public static class DialogContentReader
    {
        // ============================================================
        // Entry points
        // ============================================================

        /// <summary>Try to read a summary of the current dialog screen. Returns true if handled.</summary>
        public static bool TryReadSummary(ScreenController screen)
        {
            try
            {
                if (screen is ChooseLeaderDialog cld) return ReadChooseLeader(cld, false);
                // Check subclasses before base classes (Sacrifice/Spirit before EffectsDialog)
                if (screen is SacrificeDialogController sac) return ReadSacrifice(sac, false);
                if (screen is SpiritDialogController spi) return ReadSpirit(spi, false);
                if (screen is BuildDialogController bld) return ReadBuild(bld, false);
                if (screen is ReorganizeDialogController reorg) return ReadReorganize(reorg, false);
                if (screen is VentureDialogController vent) return ReadVenture(vent, false);
                if (screen is EmissaryDialogController emis) return ReadEmissary(emis, false);
                if (screen is RitualDialogController rit) return ReadRitual(rit, false);
                if (screen is RaidDialogController raid) return ReadRaid(raid, false);
                if (screen is WarriorsDialogController war) return ReadWarriors(war, false);
                if (screen is CaravanDialogController car) return ReadCaravan(car, false);
                if (screen is FortifyDialogController fort) return ReadFortify(fort, false);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("DialogReader.Summary", ex);
            }
            return false;
        }

        /// <summary>Try to read the full content of the current dialog screen. Returns true if handled.</summary>
        public static bool TryReadFull(ScreenController screen)
        {
            try
            {
                if (screen is ChooseLeaderDialog cld) return ReadChooseLeader(cld, true);
                if (screen is SacrificeDialogController sac) return ReadSacrifice(sac, true);
                if (screen is SpiritDialogController spi) return ReadSpirit(spi, true);
                if (screen is BuildDialogController bld) return ReadBuild(bld, true);
                if (screen is ReorganizeDialogController reorg) return ReadReorganize(reorg, true);
                if (screen is VentureDialogController vent) return ReadVenture(vent, true);
                if (screen is EmissaryDialogController emis) return ReadEmissary(emis, true);
                if (screen is RitualDialogController rit) return ReadRitual(rit, true);
                if (screen is RaidDialogController raid) return ReadRaid(raid, true);
                if (screen is WarriorsDialogController war) return ReadWarriors(war, true);
                if (screen is CaravanDialogController car) return ReadCaravan(car, true);
                if (screen is FortifyDialogController fort) return ReadFortify(fort, true);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("DialogReader.Full", ex);
            }
            return false;
        }

        // ============================================================
        // Combat Status
        // ============================================================

        /// <summary>Forward to the dedicated combat reader. Kept as public API so callers
        /// outside this file (or future ones) get the richer status without having to know
        /// about CombatScreenReader directly. Returns true if a battle status was spoken.</summary>
        public static bool ReadCombatStatus(ScreenController screen)
        {
            BattleController bc = screen as BattleController;
            if (bc == null) return false;
            return CombatScreenReader.AnnounceFullStatus(bc);
        }

        // ============================================================
        // Scene Info View
        // ============================================================

        /// <summary>Read SceneInfoView content.</summary>
        public static void ReadSceneInfo(SceneInfoView view)
        {
            if (view == null) return;

            var sb = new StringBuilder();
            sb.Append(Loc.Get("Scene Info. "));

            string horses = SafeLabel(view.horses);
            if (!string.IsNullOrEmpty(horses))
                sb.Append(Loc.Get("Horses: ")).Append(horses).Append(". ");

            string food = SafeLabel(view.foodSupply);
            if (!string.IsNullOrEmpty(food))
                sb.Append(Loc.Get("Food: ")).Append(food).Append(". ");

            AppendListItems(sb, view.treasures, Loc.Get("Treasures"));
            AppendListItems(sb, view.clanList, Loc.Get("Clans"));

            if (sb.Length <= 12)
                sb.Append(Loc.Get("No data available."));

            ScreenReader.Say(sb.ToString());
        }

        // ============================================================
        // Dashboard
        // ============================================================

        /// <summary>
        /// Read the "current screen relevant" one-shot summary: every concern
        /// (stress / advantage / warning / omen) for this screen plus only the
        /// currently active magic. Used by Ctrl+F4 and by the auto-announcement
        /// when entering a management screen. The known/unlearned roster is not
        /// included here — it belongs to the cycle, not the "what's happening
        /// right now" view.
        /// </summary>
        public static void ReadCurrentScreenRelevant(GameScreen screen)
        {
            try
            {
                if (Singleton<GameManager>.isShuttingDown) return;
                if (!ConcernReader.HasDashboardEntry(screen))
                {
                    ScreenReader.Say(Loc.Get("Dashboard concerns are only available on management screens."));
                    return;
                }
                // Verbose: include the full blessing.explanation text — what
                // the in-game hover tooltip on each magic icon shows. Ctrl+F4
                // is the deliberate "tell me what these blessings do" hotkey;
                // the auto-announce on screen entry stays compact (verbose:false).
                string report = ConcernReader.BuildCurrentScreenReport(screen, verbose: true);
                if (!string.IsNullOrEmpty(report)) ScreenReader.Say(report);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("DialogReader.CurrentScreenRelevant", ex);
                ScreenReader.Say(Loc.Get("Could not read concerns."));
            }
        }

        // ============================================================
        // Choose Leader Dialog
        // ============================================================

        private static bool ReadChooseLeader(ChooseLeaderDialog d, bool full)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Choose Leader. "));

            // RitualDialogController.ChooseLeader calls SetViewOnly which hides the
            // chooseButton and shows the closeButton — the dialog is informational
            // only, the leader is auto-picked from PC_BestPerson by the game when
            // START is pressed. Without flagging this, the user wastes time selecting
            // candidates that have no effect.
            bool viewOnly = d.chooseButton != null && !d.chooseButton.gameObject.activeSelf
                            && d.closeButton != null && d.closeButton.gameObject.activeSelf;

            string caption = SafeLabel(d.caption);
            if (!string.IsNullOrEmpty(caption))
                sb.Append(caption).Append(". ");

            if (d.list != null && d.list.count > 0)
            {
                sb.Append(d.list.count).Append(Loc.Get(" candidates"));
                if (full)
                {
                    sb.Append(": ");
                    AppendPersonList(sb, d.list);
                }
                else
                {
                    sb.Append(". ");
                }
            }
            else
            {
                sb.Append(Loc.Get("No candidates. "));
            }

            if (viewOnly)
                sb.Append(Loc.Get("View only — the leader is chosen automatically. Use arrow keys to browse, F sorts by skill, Escape to return."));
            else
                sb.Append(Loc.Get("Use arrow keys to browse, F sorts by skill, Space selects a candidate, Enter chooses."));

            // interrupt:false so a tutorial hint queued just before the dialog opens
            // (e.g. "Pick a leader, then click Choose") plays through to completion. F5
            // repeats this announcement on demand.
            ScreenReader.Say(sb.ToString(), interrupt: false);
            return true;
        }

        // ============================================================
        // Reorganize Dialog
        // ============================================================

        private static bool ReadReorganize(ReorganizeDialogController d, bool full)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Reorganize. "));

            // Read current ring from game state
            sb.Append(Loc.Get("Current ring: "));
            for (int pos = 1; pos <= 7; pos++)
            {
                try
                {
                    int personIdx = PluginImport.PC_RingPerson(pos);
                    if (personIdx <= 0)
                        sb.Append(Loc.Get("empty"));
                    else
                        sb.Append(PluginImport.PC_PersonName(personIdx));
                }
                catch
                {
                    sb.Append(Loc.Get("unknown"));
                }
                if (pos < 7) sb.Append(", ");
            }
            sb.Append(". ");

            string ringInfo = SafeLabel(d.ringInfo);
            if (!string.IsNullOrEmpty(ringInfo))
                sb.Append(ringInfo).Append(". ");

            if (d.list != null && d.list.count > 0)
            {
                sb.Append(d.list.count).Append(Loc.Get(" candidates"));
                if (full)
                {
                    sb.Append(": ");
                    AppendPersonList(sb, d.list);
                }
                else
                {
                    sb.Append(". ");
                }
            }

            // ReorganizeNavigator zone-based, Model Y keys: Down/Up cycle candidates,
            // F changes sort, Space toggles ring membership, C toggles chief, D cycles
            // the candidate's advice and full bio. Tab toggles between candidate list
            // and ring slot view (where Space jumps to the slot's occupant in the
            // list). A blank Enter commits the new ring, Escape closes.
            sb.Append(Loc.Get("Up and Down cycle candidates. F changes sort, Space toggles ring membership, C toggles chief, D reads advice and full information, Tab switches to ring slots, F5 reads ring status. Enter reorganizes, Escape closes."));
            ScreenReader.Say(sb.ToString());
            return true;
        }

        // ============================================================
        // Venture Dialog
        // ============================================================

        private static bool ReadVenture(VentureDialogController d, bool full)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Venture. "));

            int count = (d.ventureList != null) ? d.ventureList.count : -1;
            if (count > 0)
            {
                sb.Append(count).Append(Loc.Get(" ventures"));
                if (full)
                {
                    sb.Append(": ");
                    AppendVentureItemsWithRunning(sb, d.ventureList);
                }
                else
                {
                    sb.Append(". ");
                }
            }
            else
            {
                sb.Append(Loc.Get("No ventures available. "));
            }

            string desc = d.ventureDescription != null ? d.ventureDescription.text : "";
            if (!string.IsNullOrEmpty(desc))
                sb.Append(desc).Append(". ");

            sb.Append(Loc.Get("Up and Down cycle ventures (list active by default), Space selects the focused venture, D for description, L returns to list. Enter starts the venture, Escape closes."));
            DebugLogger.Log("DialogReader", "Venture: count=" + count + ", descLen=" + (desc != null ? desc.Length : 0));
            ScreenReader.Say(sb.ToString());
            return true;
        }

        /// <summary>
        /// Like <see cref="AppendListItems"/>, but tag entries whose venture is
        /// already running. VentureDialogController.OnShow uses
        /// <c>PC_VentureListIsInEffect(i)</c> to attach a Running-Man icon (see
        /// decompiled line 109-112); sighted players see the icon, we surface
        /// it as "(running)" so the user knows pressing Start on those is a
        /// no-op rather than launching a fresh venture.
        /// </summary>
        private static void AppendVentureItemsWithRunning(StringBuilder sb, UIList list)
        {
            if (list == null) return;
            for (int i = 0; i < list.count; i++)
            {
                UIListItem item = list[i];
                if (item == null) continue;
                string text = item.text;
                if (string.IsNullOrEmpty(text)) continue;
                if (i > 0) sb.Append(", ");
                sb.Append(text);
                bool running = false;
                try { running = PluginImport.PC_VentureListIsInEffect(i); }
                catch (Exception ex) { DebugLogger.Error("DialogReader.VentureInEffect", ex); }
                if (running) sb.Append(Loc.Get(" (running)"));
            }
            sb.Append(". ");
        }

        // ============================================================
        // Emissary Dialog
        // ============================================================

        private static bool ReadEmissary(EmissaryDialogController d, bool full)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Emissary. "));

            if (d.clanList != null && d.clanList.count > 0)
            {
                sb.Append(d.clanList.count).Append(Loc.Get(" clans"));
                if (full)
                {
                    sb.Append(": ");
                    AppendListItems(sb, d.clanList, null);
                }
                else
                {
                    sb.Append(". ");
                }
            }

            if (full)
            {
                AppendSlider(sb, d.eliteSlider);
                AppendSlider(sb, d.regularSlider);
                AppendSlider(sb, d.goodsSlider);
                AppendSlider(sb, d.herdsSlider);
                AppendSlider(sb, d.horsesSlider);
            }

            AppendLeaderInfo(sb, d);

            sb.Append(Loc.Get("Tab cycles zones: clan list, gifts and escort, leader. "));
            sb.Append(Loc.Get("L jumps to the clan list. Up and Down navigate, Left and Right adjust sliders. "));
            sb.Append(Loc.Get("Space selects the focused clan, Enter sends, D reads details, Escape closes."));
            ScreenReader.Say(sb.ToString());
            return true;
        }

        // ============================================================
        // Ritual Dialog
        // ============================================================

        private static bool ReadRitual(RitualDialogController d, bool full)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Ritual. "));

            if (d.ritualList != null && d.ritualList.count > 0)
            {
                sb.Append(d.ritualList.count).Append(Loc.Get(" rituals"));
                if (full)
                {
                    sb.Append(": ");
                    AppendListItems(sb, d.ritualList, null);
                }
                else
                {
                    sb.Append(". ");
                }
            }
            else
            {
                sb.Append(Loc.Get("No rituals available. "));
            }

            string desc = d.ritualDescription != null ? d.ritualDescription.text : "";
            if (!string.IsNullOrEmpty(desc))
                sb.Append(desc).Append(". ");

            string interval = d.intervalText != null ? d.intervalText.text : "";
            if (!string.IsNullOrEmpty(interval))
                sb.Append(interval).Append(". ");

            AppendLeaderInfo(sb, d);

            sb.Append(Loc.Get("Up and Down navigate the ritual list, Space selects the ritual, L for list. Enter performs the ritual, Escape closes."));
            ScreenReader.Say(sb.ToString());
            return true;
        }

        // ============================================================
        // Sacrifice Dialog
        // ============================================================

        private static bool ReadSacrifice(SacrificeDialogController d, bool full)
        {
            var sb = new StringBuilder();

            string caption = d.dialogCaption != null ? d.dialogCaption.text : "";
            if (!string.IsNullOrEmpty(caption))
                sb.Append(caption).Append(". ");
            else
                sb.Append(Loc.Get("Sacrifice. "));

            AppendSlider(sb, d.goodsSlider);
            AppendSlider(sb, d.herdsSlider);

            if (full)
                AppendEffects(sb, d.effects);

            string extra = d.extraInfo != null ? d.extraInfo.text : "";
            if (!string.IsNullOrEmpty(extra))
                sb.Append(extra).Append(". ");

            sb.Append(Loc.Get("Tab cycles zones: blessings, sliders. Up and Down navigate the active zone, Space selects a blessing, Left and Right adjust sliders, D reads details. Enter sacrifices, Escape closes."));
            ScreenReader.Say(sb.ToString());
            return true;
        }

        // ============================================================
        // Spirit Dialog
        // ============================================================

        private static bool ReadSpirit(SpiritDialogController d, bool full)
        {
            // Always emit the full content here. The dialog is small (one
            // approach group + N blessings + caption), and SpiritNavigator
            // owns input in a way that requires the user to know the choices
            // up front — a short summary would re-introduce the silent
            // auto-Persuade behavior we just fixed. Both summary (on dialog
            // open) and full (F5) produce the same announcement.
            var sb = new StringBuilder();

            string caption = d.dialogCaption != null ? d.dialogCaption.text : "";
            sb.Append(string.IsNullOrEmpty(caption) ? Loc.Get("Spirit Bargain. ") : caption + ". ");

            // Bargain approaches — collected from UIDeityEffectItems on the
            // dialog that are NOT in the effects (blessings) list. Hidden
            // approaches (Raven hides both Releases; Healing hides Release
            // Longer) skip themselves via gameObject.activeSelf.
            UIDeityEffectItem[] allItems = d.GetComponentsInChildren<UIDeityEffectItem>(false);
            bool hasBargain = false;
            for (int i = 0; i < allItems.Length; i++)
            {
                UIDeityEffectItem item = allItems[i];
                if (item == null || !item.gameObject.activeSelf) continue;

                bool isEffect = false;
                for (int j = 0; j < d.effects.Count; j++)
                {
                    if (d.effects[j] == item) { isEffect = true; break; }
                }
                if (isEffect) continue;
                if (item.label == null || string.IsNullOrEmpty(item.label.text)) continue;

                if (!hasBargain) { sb.Append(Loc.Get("Bargain approaches: ")); hasBargain = true; }
                else sb.Append(", ");
                sb.Append(StringHelpers.StripTags(item.label.text));
                if (item.toggle != null)
                {
                    if (item.toggle.isOn) sb.Append(Loc.Get(" (selected)"));
                    if (!item.toggle.interactable) sb.Append(Loc.Get(" (not available)"));
                }
            }
            if (hasBargain) sb.Append(". ");

            // Blessings (the inherited effects list). Inline to use a
            // "Blessings:" prefix instead of AppendEffects's generic
            // "Options:", so the user can tell them apart from the bargain
            // approaches above.
            //
            // EffectsDialogController pre-instantiates spare slots and hides
            // them via UIDeityEffectItem.HideAll() (called from
            // SpiritDialogController.UpdateRadioButton when the level is
            // kUnknownBlessing). HideAll deactivates the toggle's GO, so we
            // gate visibility on toggle.gameObject.activeSelf — the OUTER GO
            // can still be active and would leak the placeholder label
            // ("Label") into the announcement.
            if (d.effects != null)
            {
                bool hasBlessings = false;
                for (int i = 0; i < d.effects.Count; i++)
                {
                    UIDeityEffectItem b = d.effects[i];
                    if (b == null) continue;
                    if (b.toggle == null || !b.toggle.gameObject.activeSelf) continue;
                    string label = b.label != null ? b.label.text : "";
                    if (string.IsNullOrEmpty(label)) continue;

                    if (!hasBlessings) { sb.Append(Loc.Get("Blessings: ")); hasBlessings = true; }
                    else sb.Append(", ");
                    sb.Append(StringHelpers.StripTags(label));
                    if (b.toggle.isOn) sb.Append(Loc.Get(" (selected)"));
                    if (!b.toggle.interactable) sb.Append(Loc.Get(" (not yet learned)"));
                }
                if (hasBlessings) sb.Append(". ");
            }

            string extra = d.extraInfo != null ? d.extraInfo.text : "";
            if (!string.IsNullOrEmpty(extra))
                sb.Append(extra).Append(". ");

            sb.Append(Loc.Get("Tab switches between approaches and blessings. Up and Down navigate, Space selects, D reads details. Enter bargains, Escape closes."));
            ScreenReader.Say(sb.ToString());
            return true;
        }

        // ============================================================
        // Build (Temple) Dialog
        // ============================================================

        private static bool ReadBuild(BuildDialogController d, bool full)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Build Temple. "));

            string deity = d.deityName != null ? d.deityName.text : "";
            if (!string.IsNullOrEmpty(deity))
                sb.Append(StringHelpers.StripTags(deity)).Append(". ");

            string explanation = d.sizeExplanation != null ? d.sizeExplanation.text : "";
            if (!string.IsNullOrEmpty(explanation))
                sb.Append(explanation).Append(". ");

            // Damage status — game's OnShowEnd shows a tutorial note for this, but
            // we want it spoken inline so the user sees it before deciding to act.
            AppendBuildDamageStatus(sb, d);

            // Per-button cost/benefit — the visual InfoList shows Shrine/Temple/Great
            // Temple side-by-side with cost, blessings, maintenance. Without this
            // context, "Build" or "Reduce" gives the user nothing actionable.
            AppendBuildAction(sb, d, isBuild: true);
            AppendBuildAction(sb, d, isBuild: false);

            // F5: full table of all available tiers for comparison.
            if (full && d.infoList != null && d.infoList.count > 0)
            {
                sb.Append(Loc.Get("All tiers: "));
                for (int i = 0; i < d.infoList.count; i++)
                {
                    var item = d.infoList[i] as TempleBuildListItem;
                    if (item == null || !item.gameObject.activeSelf) continue;
                    AppendTierLine(sb, item, TempleTierName(i));
                }
            }

            sb.Append(Loc.Get("Tab cycles the buttons, Space activates the focused one. Enter builds the temple, Escape closes."));
            ScreenReader.Say(sb.ToString());
            return true;
        }

        /// <summary>
        /// Speak the temple's current hit points if it is damaged. Mirrors the
        /// "TempleRebuild" tutorial note the game shows for the same condition,
        /// but inline so the announcement-on-open carries it.
        /// </summary>
        private static void AppendBuildDamageStatus(StringBuilder sb, BuildDialogController d)
        {
            try
            {
                var size = PlayerClan.TempleSize(d.selectedDeity);
                if (size <= TempleSize.kNoTemple) return;
                int hp = PlayerClan.TempleHitPoints(d.selectedDeity);
                int maxHp = PlayerClan.MaxTempleHP(d.selectedDeity);
                if (maxHp > 0 && hp < maxHp)
                    sb.Append(Loc.Get("Temple is damaged: ")).Append(hp).Append(Loc.Get(" of ")).Append(maxHp).Append(Loc.Get(" hit points. "));
            }
            catch (Exception ex)
            {
                DebugLogger.Error("DialogReader.AppendBuildDamage", ex);
            }
        }

        /// <summary>
        /// Speak the Build / Reduce action with the target tier's cost and benefits.
        /// Only emits anything if the corresponding button is visible.
        /// </summary>
        private static void AppendBuildAction(StringBuilder sb, BuildDialogController d, bool isBuild)
        {
            UIButton btn = isBuild ? d.buildButton : d.reduceButton;
            if (btn == null || !btn.gameObject.activeSelf) return;

            string label = (btn.label != null && !string.IsNullOrEmpty(btn.label.text)) ? btn.label.text
                : (isBuild ? Loc.Get("Build") : Loc.Get("Reduce"));
            sb.Append(label);
            if (!btn.IsInteractable()) sb.Append(Loc.Get(" (disabled)"));

            // Resolve target tier index in the infoList.
            int targetIdx = ResolveBuildTargetIndex(d, isBuild);
            if (d.infoList != null && targetIdx >= 0 && targetIdx < d.infoList.count)
            {
                var item = d.infoList[targetIdx] as TempleBuildListItem;
                if (item != null)
                {
                    sb.Append(Loc.Get(" to ")).Append(TempleTierName(targetIdx)).Append(": ");
                    // includeCost=true only on Build — for Reduce, costText is the target
                    // tier's BUILD cost (not what reducing pays), so it would mislead.
                    AppendTierLine(sb, item, null, includeCost: isBuild);
                }
                else sb.Append(". ");
            }
            else if (!isBuild && targetIdx == -1)
            {
                // Reduce from kShrine returns -1 — the temple goes away entirely.
                sb.Append(Loc.Get(" to nothing — removes the shrine. "));
            }
            else
            {
                sb.Append(". ");
            }
        }

        /// <summary>
        /// Append a single tier's stats. With <paramref name="includeCost"/>=true:
        /// "cost X herds, N blessings, maintenance Y goods Z herds per year."
        /// With includeCost=false (e.g. Reduce target): "N blessings, maintenance ..."
        /// — the costText field shows the BUILD cost of that tier, which is misleading
        /// when we're moving DOWN to it.
        /// </summary>
        private static void AppendTierLine(StringBuilder sb, TempleBuildListItem item, string tierLabel, bool includeCost = true)
        {
            string cost = item.costText != null ? item.costText.text.Trim() : "?";
            string blessings = item.blessingsText != null ? item.blessingsText.text.Trim() : "?";
            string maintenance = item.maintenanceText != null
                ? item.maintenanceText.text.Replace('\n', ' ').Replace("  ", " ").Trim()
                : "?";

            if (!string.IsNullOrEmpty(tierLabel))
                sb.Append(tierLabel).Append(": ");
            if (includeCost)
                sb.Append(Loc.Get("cost ")).Append(cost).Append(Loc.Get(" herds, "));
            sb.Append(blessings).Append(Loc.Get(" blessings, "))
              .Append(Loc.Get("maintenance ")).Append(maintenance).Append(Loc.Get(" per year. "));
        }

        /// <summary>
        /// Map current temple size + action direction to the infoList row that the
        /// action would land on. infoList layout (set in BuildDialogController.OnInit):
        /// 0 = Shrine, 1 = Temple, 2 = Great Temple.
        /// </summary>
        private static int ResolveBuildTargetIndex(BuildDialogController d, bool isBuild)
        {
            try
            {
                var size = PlayerClan.TempleSize(d.selectedDeity);
                if (isBuild)
                {
                    switch (size)
                    {
                        case TempleSize.kNoTemple:    return 0; // → shrine
                        case TempleSize.kShrine:      return 1; // → temple
                        case TempleSize.kTemple:      return 2; // → great temple
                        default:                      return -1;
                    }
                }
                else
                {
                    switch (size)
                    {
                        case TempleSize.kShrine:      return -1; // → no temple (no row)
                        case TempleSize.kTemple:      return 0;  // → shrine
                        case TempleSize.kGreatTemple: return 1;  // → temple
                        default:                      return -1;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("DialogReader.ResolveBuildTarget", ex);
                return -1;
            }
        }

        private static string TempleTierName(int idx)
        {
            switch (idx)
            {
                case 0: return Loc.Get("shrine");
                case 1: return Loc.Get("temple");
                case 2: return Loc.Get("great temple");
                default: return Loc.Get("tier ") + (idx + 1);
            }
        }

        // ============================================================
        // Raid Dialog
        // ============================================================

        private static bool ReadRaid(RaidDialogController d, bool full)
        {
            var sb = new StringBuilder();

            string caption = SafeLabel(d.caption);
            if (!string.IsNullOrEmpty(caption))
                sb.Append(caption).Append(". ");
            else
                sb.Append(Loc.Get("Raid. "));

            if (d.raidableClans != null && d.raidableClans.count > 0)
            {
                sb.Append(d.raidableClans.count).Append(Loc.Get(" raidable clans"));
                if (full)
                {
                    sb.Append(": ");
                    AppendListItems(sb, d.raidableClans, null);
                }
                else
                {
                    sb.Append(". ");
                }
            }

            if (d.helperClans != null && d.helperClans.count > 0)
                sb.Append(d.helperClans.count).Append(Loc.Get(" helper clans. "));

            AppendSlider(sb, d.eliteSlider);
            AppendSlider(sb, d.regularSlider);

            AppendLeaderInfo(sb, d);

            sb.Append(Loc.Get("Up and Down navigate the raidable clan list, L returns to list. Tab cycles to sliders and helpers. Space selects a target or toggles a helper, D describes a clan. Enter raids, Escape closes."));
            ScreenReader.Say(sb.ToString());
            return true;
        }

        // ============================================================
        // Warriors Dialog
        // ============================================================

        private static bool ReadWarriors(WarriorsDialogController d, bool full)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Warriors. "));

            AppendSlider(sb, d.slider);

            if (full)
            {
                AppendToggle(sb, d.recruitFromClan, "Recruit from clan");
                AppendToggle(sb, d.recruitOutside, "Recruit outsiders");
                AppendToggle(sb, d.offerGifts, "Offer gifts");
                AppendToggle(sb, d.severancePay, "Severance pay");
            }

            // Live mode-name: actionButton.label.text flips between "RECRUIT" and
            // "DISMISS" depending on slider position relative to current warrior count.
            // Mention it so the user knows which direction Enter will go.
            string warriorMode = (d.actionButton != null && d.actionButton.label != null)
                ? StringHelpers.StripTags(d.actionButton.label.text ?? "") : "";
            if (!string.IsNullOrEmpty(warriorMode))
                sb.Append(Loc.Get("Mode: ")).Append(Loc.Get(warriorMode).ToLowerInvariant()).Append(". ");

            sb.Append(Loc.Get("Left and Right adjust the slider, Tab cycles toggles, Space flips the focused toggle. Enter recruits or dismisses, Escape closes."));
            ScreenReader.Say(sb.ToString());
            return true;
        }

        // ============================================================
        // Caravan Dialog
        // ============================================================

        private static bool ReadCaravan(CaravanDialogController d, bool full)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Caravan. "));

            if (d.clanList != null && d.clanList.count > 0)
            {
                sb.Append(d.clanList.count).Append(Loc.Get(" clans"));
                if (full)
                {
                    sb.Append(": ");
                    AppendListItems(sb, d.clanList, null);
                }
                else
                {
                    sb.Append(". ");
                }
            }

            if (full)
            {
                AppendSlider(sb, d.eliteSlider);
                AppendSlider(sb, d.regularSlider);

                sb.Append(Loc.Get("Buy: "));
                AppendToggleCompact(sb, d.buyFood, "Food");
                AppendToggleCompact(sb, d.buyGoods, "Goods");
                AppendToggleCompact(sb, d.buyHerds, "Herds");
                AppendToggleCompact(sb, d.buyHorses, "Horses");
                AppendToggleCompact(sb, d.buyTreasure, "Treasure");
                sb.Append(Loc.Get(". Sell: "));
                AppendToggleCompact(sb, d.sellFood, "Food");
                AppendToggleCompact(sb, d.sellGoods, "Goods");
                AppendToggleCompact(sb, d.sellHerds, "Herds");
                AppendToggleCompact(sb, d.sellHorses, "Horses");
                AppendToggleCompact(sb, d.sellTreasure, "Treasure");
                sb.Append(". ");

                AppendToggle(sb, d.establishRoute, "Establish route");

                if (d.treasures != null && d.treasures.count > 0)
                    sb.Append(d.treasures.count).Append(Loc.Get(" treasures. "));
            }

            AppendLeaderInfo(sb, d);

            sb.Append(Loc.Get("Tab cycles zones: clan list, mode, goods, treasures, escort, leader. L returns to the clan list. Up and Down navigate, Left and Right adjust sliders. Space selects or toggles the focused element, D reads details, R reads active trade routes. Enter sends, Escape closes."));
            ScreenReader.Say(sb.ToString());
            return true;
        }

        // ============================================================
        // Fortify Dialog
        // ============================================================

        private static bool ReadFortify(FortifyDialogController d, bool full)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Fortify. "));

            if (d.buildable != null && d.buildable.count > 0)
            {
                sb.Append(d.buildable.count).Append(Loc.Get(" buildable"));
                if (full)
                {
                    sb.Append(": ");
                    AppendListItems(sb, d.buildable, null);
                }
                else
                {
                    sb.Append(". ");
                }
            }

            if (d.fortifications != null && d.fortifications.count > 0)
            {
                sb.Append(d.fortifications.count).Append(Loc.Get(" existing"));
                if (full)
                {
                    sb.Append(": ");
                    AppendListItems(sb, d.fortifications, null);
                }
                else
                {
                    sb.Append(". ");
                }
            }

            string cost = SafeLabel(d.costLabel);
            if (!string.IsNullOrEmpty(cost))
                sb.Append(Loc.Get("Cost: ")).Append(cost).Append(". ");

            // Build availability is gated by goods + valid pick; report so F5 reflects
            // whether Enter would actually fire right now.
            if (full && d.actionButton != null)
                sb.Append(d.actionButton.IsInteractable() ? Loc.Get("Build available. ") : Loc.Get("Build disabled. "));

            sb.Append(Loc.Get("Up and Down navigate buildable fortifications, Space picks one, L returns to list, Tab switches to existing fortifications. Cost is announced when you pick one. Enter builds, Escape closes."));
            ScreenReader.Say(sb.ToString());
            return true;
        }

        // ============================================================
        // Helpers
        // ============================================================

        /// <summary>Safely read text from a UILabel.</summary>
        private static string SafeLabel(UILabel label)
        {
            if (label == null) return "";
            return label.text ?? "";
        }

        /// <summary>Append list item texts to a StringBuilder.</summary>
        private static void AppendListItems(StringBuilder sb, UIList list, string label)
        {
            if (list == null || list.count == 0) return;

            if (!string.IsNullOrEmpty(label))
                sb.Append(label).Append(": ");

            int max = list.count > 8 ? 8 : list.count;
            for (int i = 0; i < max; i++)
            {
                UIListItem item = list[i];
                if (item == null) continue;

                if (i > 0) sb.Append(", ");

                string text = item.text;
                if (string.IsNullOrEmpty(text))
                    text = item.gameObject.name;
                sb.Append(text);
            }
            if (list.count > 8)
                sb.Append(Loc.Get(" and ")).Append(list.count - 8).Append(Loc.Get(" more"));
            sb.Append(". ");
        }

        /// <summary>Append person list items with info text.</summary>
        private static void AppendPersonList(StringBuilder sb, UIList list)
        {
            if (list == null || list.count == 0) return;

            int max = list.count > 6 ? 6 : list.count;
            for (int i = 0; i < max; i++)
            {
                UIListItem item = list[i];
                if (item == null) continue;

                if (i > 0) sb.Append(", ");

                string name = item.text ?? "";
                sb.Append(name);

                PersonListItem pli = item as PersonListItem;
                if (pli != null && pli.info != null && !string.IsNullOrEmpty(pli.info.text))
                    sb.Append(" (").Append(pli.info.text).Append(")");
            }
            if (list.count > 6)
                sb.Append(Loc.Get(" and ")).Append(list.count - 6).Append(Loc.Get(" more"));
            sb.Append(". ");
        }

        /// <summary>Append slider info.</summary>
        private static void AppendSlider(StringBuilder sb, UISlider slider)
        {
            if (slider == null || !slider.gameObject.activeSelf) return;

            string label = slider.label != null ? Loc.Get(slider.label.text) : Loc.Get("Amount");
            sb.Append(label).Append(": ").Append((int)slider.value)
              .Append(Loc.Get(" of ")).Append((int)slider.maxValue).Append(". ");
        }

        /// <summary>
        /// Append toggle state. Reads the toggle's own visible label
        /// (<c>UIToggle.label.text</c>, set in the Unity prefab) and falls back to
        /// the <paramref name="fallbackLabel"/> only when the prefab has no text.
        /// Caller-supplied labels are kept around as a safety net for navigators
        /// that haven't been audited yet.
        /// </summary>
        private static void AppendToggle(StringBuilder sb, UIToggle toggle, string fallbackLabel)
        {
            if (toggle == null || !toggle.gameObject.activeSelf) return;
            string label = ResolveToggleLabel(toggle, fallbackLabel);
            sb.Append(label).Append(": ").Append(toggle.isOn ? Loc.Get("on") : Loc.Get("off"));
            if (!toggle.interactable) sb.Append(Loc.Get(" (not available)"));
            sb.Append(". ");
        }

        /// <summary>Append toggle state in compact format for buy/sell lists.</summary>
        private static void AppendToggleCompact(StringBuilder sb, UIToggle toggle, string fallbackLabel)
        {
            if (toggle == null || !toggle.gameObject.activeSelf) return;
            if (toggle.isOn) sb.Append(ResolveToggleLabel(toggle, fallbackLabel)).Append(" ");
        }

        private static string ResolveToggleLabel(UIToggle toggle, string fallbackLabel)
        {
            if (toggle != null && toggle.label != null)
            {
                string text = toggle.label.text;
                if (!string.IsNullOrEmpty(text)) return StringHelpers.StripTags(text);
            }
            return Loc.Get(fallbackLabel);
        }

        /// <summary>Append deity/spirit effects info (radio buttons for blessings).</summary>
        private static void AppendEffects(StringBuilder sb, System.Collections.Generic.List<UIDeityEffectItem> effects)
        {
            if (effects == null) return;

            bool hasEffects = false;
            for (int i = 0; i < effects.Count; i++)
            {
                UIDeityEffectItem effect = effects[i];
                if (effect == null || !effect.gameObject.activeSelf) continue;

                string label = effect.label != null ? effect.label.text : "";
                if (string.IsNullOrEmpty(label)) continue;

                if (!hasEffects)
                {
                    sb.Append(Loc.Get("Options: "));
                    hasEffects = true;
                }
                else
                {
                    sb.Append(", ");
                }

                sb.Append(label);
                if (effect.toggle != null)
                {
                    if (effect.toggle.isOn)
                        sb.Append(Loc.Get(" (selected)"));
                    if (!effect.toggle.interactable)
                        sb.Append(Loc.Get(" (not available)"));
                }
            }
            if (hasEffects) sb.Append(". ");
        }

        /// <summary>Append leader info if a leader is selected on a ManagementDialogController.</summary>
        private static void AppendLeaderInfo(StringBuilder sb, ManagementDialogController d)
        {
            if (d == null || d.personCard == null) return;

            try
            {
                if (d.personCard.gameObject.activeSelf && d.personCard.nameText != null)
                {
                    string name = d.personCard.nameText.text;
                    if (!string.IsNullOrEmpty(name))
                        sb.Append(Loc.Get("Leader: ")).Append(name).Append(". ");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("DialogContentReader.AppendLeader", ex);
            }
        }

    }
}
