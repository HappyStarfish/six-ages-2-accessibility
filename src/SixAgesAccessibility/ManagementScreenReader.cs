using System;
using System.Text;
using UnityEngine.UI;

namespace SixAgesAccessibility
{
    /// <summary>Reads management screen content for screen reader output.</summary>
    public static class ManagementScreenReader
    {
        /// <summary>
        /// Try to read a short summary of the current management screen. Returns true if handled.
        /// When <paramref name="interrupt"/> is false the summary is queued after any current
        /// speech (used to chain after a freshly-announced tutorial hint instead of cutting it off).
        /// </summary>
        public static bool TryReadSummary(ScreenController screen, bool interrupt = true)
        {
            try
            {
                if (screen is ClanScreenController clan)
                    return ReadClanSummary(clan, interrupt);
                if (screen is WarScreenController war)
                    return ReadWarSummary(war, interrupt);
                if (screen is WealthScreenController wealth)
                    return ReadWealthSummary(wealth, interrupt);
                if (screen is RelationsScreenController relations)
                    return ReadRelationsSummary(relations, interrupt);
                if (screen is MagicScreenController magic)
                    return ReadMagicSummary(magic, interrupt);
                if (screen is LoreScreenController lore)
                    return ReadLoreSummary(lore, interrupt);
                if (screen is SagaScreenController saga)
                    return ReadSagaSummary(saga, interrupt);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ManagementScreenReader.Summary", ex);
            }
            return false;
        }

        /// <summary>Try to read the full content of the current management screen. Returns true if handled.</summary>
        public static bool TryReadFull(ScreenController screen)
        {
            try
            {
                if (screen is ClanScreenController clan)
                    return ReadClanFull(clan);
                if (screen is WarScreenController war)
                    return ReadWarFull(war);
                if (screen is WealthScreenController wealth)
                    return ReadWealthFull(wealth);
                if (screen is RelationsScreenController relations)
                    return ReadRelationsFull(relations);
                if (screen is MagicScreenController magic)
                    return ReadMagicFull(magic);
                if (screen is LoreScreenController lore)
                    return ReadLoreFull(lore);
                if (screen is SagaScreenController saga)
                    return ReadSagaFull(saga);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ManagementScreenReader.Full", ex);
            }
            return false;
        }

        // ============================================================
        // Clan Screen
        // ============================================================

        private static bool ReadClanSummary(ClanScreenController c, bool interrupt)
        {
            // No "Clan." prefix — ScreenChangePatches.OnScreenChangedListener already
            // announced "Management phase. Clan screen." right before this. F5 also goes
            // through the listener, so the same prefix-strip applies in every code path.
            var sb = new StringBuilder();
            sb.Append(SafeText(c.clanName));
            sb.Append(Loc.Get(". Population: "));
            sb.Append(SafeText(c.totalHealthy));
            sb.Append(Loc.Get(" healthy"));

            // SafeText returns "" when the label or its text is null. The empty-string
            // case isn't "0" and used to slip through, producing "24 sick,  wounded"
            // — guard with IsNullOrEmpty so a missing field is silently skipped.
            string sick = SafeText(c.totalSick);
            string wounded = SafeText(c.totalWounded);
            if (!string.IsNullOrEmpty(sick) && sick != "0")
                sb.Append(", ").Append(sick).Append(Loc.Get(" sick"));
            if (!string.IsNullOrEmpty(wounded) && wounded != "0")
                sb.Append(", ").Append(wounded).Append(Loc.Get(" wounded"));

            sb.Append(Loc.Get(". Mood: ")).Append(SafeMood(c.moodDisplay));

            ScreenReader.Say(sb.ToString(), interrupt);
            return true;
        }

        private static bool ReadClanFull(ClanScreenController c)
        {
            // Clan screen header is announced separately by ScreenChangePatches.
            var sb = new StringBuilder();
            sb.Append(SafeText(c.clanName)).Append(". ");

            // Population breakdown
            sb.Append(Loc.Get("Commoners: ")).Append(SafeText(c.commoners)).Append(Loc.Get(" healthy"));
            AppendIfNonZero(sb, c.commonersSick, "sick");
            AppendIfNonZero(sb, c.commonersWounded, "wounded");
            sb.Append(". ");

            sb.Append(Loc.Get("Children: ")).Append(SafeText(c.children)).Append(". ");

            sb.Append(Loc.Get("Warriors: ")).Append(SafeText(c.warriors)).Append(Loc.Get(" healthy"));
            AppendIfNonZero(sb, c.warriorsSick, "sick");
            AppendIfNonZero(sb, c.warriorsWounded, "wounded");
            sb.Append(". ");

            sb.Append(Loc.Get("Nobles: ")).Append(SafeText(c.nobles)).Append(Loc.Get(" healthy"));
            AppendIfNonZero(sb, c.noblesSick, "sick");
            AppendIfNonZero(sb, c.noblesWounded, "wounded");
            sb.Append(". ");

            sb.Append(Loc.Get("Total: ")).Append(SafeText(c.totalHealthy)).Append(Loc.Get(" healthy"));
            AppendIfNonZero(sb, c.totalSick, "sick");
            AppendIfNonZero(sb, c.totalWounded, "wounded");
            sb.Append(". ");

            sb.Append(Loc.Get("Mood: ")).Append(SafeMood(c.moodDisplay)).Append(". ");

            string family = SafeText(c.familyInfo);
            if (!string.IsNullOrEmpty(family))
                sb.Append(family).Append(". ");

            string venture = SafeText(c.ventureInfo);
            if (!string.IsNullOrEmpty(venture))
                sb.Append(Loc.Get("Venture: ")).Append(venture);

            ScreenReader.Say(sb.ToString());
            return true;
        }

        // ============================================================
        // War Screen
        // ============================================================

        private static bool ReadWarSummary(WarScreenController c, bool interrupt)
        {
            // "War screen." prefix is announced by ScreenChangePatches; start with content.
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Elite warriors: ")).Append(SafeText(c.eliteWarriors));
            sb.Append(Loc.Get(". Regular warriors: ")).Append(SafeText(c.regularWarriors));

            int fortCount = c.fortifications != null ? c.fortifications.count : 0;
            if (fortCount > 0) sb.Append(". ").Append(fortCount).Append(Loc.Get(" fortifications"));

            // Append the action-button hotkey list with per-button availability so
            // the user knows up front what's reachable from this screen â€” the flat
            // Tab cycle is empty by design here (NavSlotCollector skips these five
            // buttons because their on-screen labels are inconsistent).
            string actions = WarScreenHandler.DescribeActions(c);
            if (!string.IsNullOrEmpty(actions))
                sb.Append(". ").Append(actions);

            ScreenReader.Say(sb.ToString(), interrupt);
            return true;
        }

        private static bool ReadWarFull(WarScreenController c)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Elite warriors: ")).Append(SafeText(c.eliteWarriors)).Append(Loc.Get(" healthy"));
            AppendIfNonZero(sb, c.eliteWarriorsAbsent, "absent");
            AppendIfNonZero(sb, c.eliteWarriorsSick, "sick");
            AppendIfNonZero(sb, c.eliteWarriorsWounded, "wounded");
            sb.Append(". ");

            sb.Append(Loc.Get("Regular warriors: ")).Append(SafeText(c.regularWarriors)).Append(Loc.Get(" healthy"));
            AppendIfNonZero(sb, c.regularWarriorsAbsent, "absent");
            AppendIfNonZero(sb, c.regularWarriorsSick, "sick");
            AppendIfNonZero(sb, c.regularWarriorsWounded, "wounded");
            sb.Append(". ");

            AppendListSummary(sb, c.fortifications, Loc.Get("Fortifications"));
            AppendListSummary(sb, c.raidedByClans, Loc.Get("Raided by"));
            AppendListSummary(sb, c.raidedClans, Loc.Get("We raided"));

            string actions = WarScreenHandler.DescribeActions(c);
            if (!string.IsNullOrEmpty(actions))
                sb.Append(actions);

            ScreenReader.Say(sb.ToString());
            return true;
        }

        // ============================================================
        // Wealth Screen
        // ============================================================

        private static bool ReadWealthSummary(WealthScreenController c, bool interrupt)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Cattle: ")).Append(SafeText(c.cattle));
            AppendIfNonEmpty(sb, c.goats, Loc.Get(", Goats: "));
            AppendIfNonEmpty(sb, c.horses, Loc.Get(", Horses: "));
            sb.Append(Loc.Get(", Goods: ")).Append(SafeText(c.goods));
            AppendIfNonEmpty(sb, c.exoticGoods, Loc.Get(", Exotic: "));
            sb.Append(Loc.Get(", Food: ")).Append(SafeText(c.foodSupply));
            AppendIfNonEmpty(sb, c.marketFrequency, Loc.Get(", Market: "));

            int treasureCount = c.treasures != null ? c.treasures.count : 0;
            int clanCount = c.tradeClans != null ? c.tradeClans.count : 0;
            sb.Append(". ").Append(treasureCount).Append(treasureCount == 1 ? Loc.Get(" treasure, ") : Loc.Get(" treasures, "));
            sb.Append(clanCount).Append(clanCount == 1 ? Loc.Get(" trade partner. ") : Loc.Get(" trade partners. "));

            sb.Append(Loc.Get("Tab cycles treasures and trade partners zones, "));
            sb.Append(Loc.Get("Up and Down cycle items, Space selects, D for description. "));
            sb.Append(Loc.Get("Enter or C opens the Caravan dialog."));

            ScreenReader.Say(sb.ToString(), interrupt);
            return true;
        }

        private static bool ReadWealthFull(WealthScreenController c)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Get("Cattle: ")).Append(SafeText(c.cattle));
            sb.Append(Loc.Get(". Goats: ")).Append(SafeText(c.goats));
            sb.Append(Loc.Get(". Horses: ")).Append(SafeText(c.horses));
            sb.Append(Loc.Get(". Goods: ")).Append(SafeText(c.goods));
            sb.Append(Loc.Get(". Exotic goods: ")).Append(SafeText(c.exoticGoods));
            sb.Append(Loc.Get(". Food: ")).Append(SafeText(c.foodSupply));
            sb.Append(Loc.Get(". Market: ")).Append(SafeText(c.marketFrequency));
            sb.Append(". ");

            AppendListSummary(sb, c.treasures, Loc.Get("Treasures"));
            AppendListSummary(sb, c.tradeClans, Loc.Get("Trade partners"));

            sb.Append(Loc.Get("Tab cycles zones, Up and Down cycle items, Space selects, D for description, "));
            sb.Append(Loc.Get("Enter or C opens the Caravan dialog, F5 for status."));

            ScreenReader.Say(sb.ToString());
            return true;
        }

        // ============================================================
        // Relations Screen
        // ============================================================

        private static bool ReadRelationsSummary(RelationsScreenController c, bool interrupt)
        {
            var sb = new StringBuilder();

            if (c.filter != null)
            {
                string filterText = c.filter.captionText != null ? c.filter.captionText.text : "";
                if (!string.IsNullOrEmpty(filterText))
                    sb.Append(Loc.Get("Filter: ")).Append(Loc.Get(filterText)).Append(". ");
            }

            int clanCount = c.list != null ? c.list.count : 0;
            sb.Append(clanCount).Append(Loc.Get(" clans listed. "));
            sb.Append(Loc.Get("Up and Down cycle clans, D reads description. "));
            sb.Append(Loc.Get("F cycles filter, Enter opens the emissary dialog"));

            int markerCount = CountActiveMissionMarkers(c);
            if (markerCount > 0)
                sb.Append(Loc.Get(". Tab leaves the list and cycles ")).Append(markerCount)
                  .Append(markerCount == 1 ? Loc.Get(" mission marker") : Loc.Get(" mission markers"))
                  .Append(Loc.Get(" for clans with available emissary missions"));
            // No "Tab cycles markers" promise when none exist â€” the tutorial-state
            // setup we hit most often has zero queued missions, and the previous
            // wording sent the user chasing a nonexistent feature.

            TrainerInfo.AppendHint(sb, prefix: ". ");

            ScreenReader.Say(sb.ToString(), interrupt);
            return true;
        }

        private static bool ReadRelationsFull(RelationsScreenController c)
        {
            var sb = new StringBuilder();

            if (c.filter != null)
            {
                string filterText = c.filter.captionText != null ? c.filter.captionText.text : "";
                if (!string.IsNullOrEmpty(filterText))
                    sb.Append(Loc.Get("Filter: ")).Append(Loc.Get(filterText)).Append(". ");
            }

            if (c.list != null && c.list.count > 0)
            {
                sb.Append(c.list.count).Append(Loc.Get(" clans: "));
                int max = c.list.count > 10 ? 10 : c.list.count;
                for (int i = 0; i < max; i++)
                {
                    UIListItem item = c.list[i];
                    if (item != null)
                    {
                        string text = item.text;
                        if (!string.IsNullOrEmpty(text))
                        {
                            if (i > 0) sb.Append(", ");
                            sb.Append(text);
                        }
                    }
                }
                if (c.list.count > 10) sb.Append(Loc.Get(" and ")).Append(c.list.count - 10).Append(Loc.Get(" more"));
                sb.Append(". ");
            }
            else
            {
                sb.Append(Loc.Get("No clans listed. "));
            }

            sb.Append(Loc.Get("Up and Down cycle clans (clan list is active by default). "));
            sb.Append(Loc.Get("D reads a clan's description; press D again to cycle paragraphs. "));
            sb.Append(Loc.Get("F cycles the filter. "));
            sb.Append(Loc.Get("Enter opens the emissary dialog. "));

            int markerCount = CountActiveMissionMarkers(c);
            if (markerCount > 0)
                sb.Append(Loc.Get("Tab leaves the list and cycles ")).Append(markerCount)
                  .Append(markerCount == 1 ? Loc.Get(" mission marker") : Loc.Get(" mission markers"))
                  .Append(Loc.Get(" on the map. "));
            else
                sb.Append(Loc.Get("No mission markers on the map right now. "));

            TrainerInfo.AppendHint(sb, prefix: "");

            ScreenReader.Say(sb.ToString());
            return true;
        }

        /// <summary>
        /// Count active mission-marker UIButtons (Dot2(Clone)) in the screen's
        /// MapView. Mission markers are UIButtons that carry a MapElement
        /// component â€” the same pair MapPatches.IsMissionMarker checks for. We
        /// query the live hierarchy so the summary reflects whatever the game
        /// just queued in OnShow â†’ UpdateUI â†’ AddMissionMarkers.
        /// </summary>
        private static int CountActiveMissionMarkers(RelationsScreenController c)
        {
            try
            {
                if (c == null || c.mapAnnotations == null) return 0;
                UIButton[] btns = c.mapAnnotations.GetComponentsInChildren<UIButton>(false);
                if (btns == null) return 0;
                int count = 0;
                for (int i = 0; i < btns.Length; i++)
                {
                    if (btns[i] == null) continue;
                    if (btns[i].GetComponent<MapElement>() == null) continue;
                    if (!btns[i].gameObject.activeSelf) continue;
                    count++;
                }
                return count;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ManagementScreenReader.CountMissionMarkers", ex);
                return 0;
            }
        }

        // ============================================================
        // Magic Screen
        // ============================================================

        private static bool ReadMagicSummary(MagicScreenController c, bool interrupt)
        {
            var sb = new StringBuilder();

            if (c.filter != null)
            {
                string filterText = c.filter.captionText != null ? c.filter.captionText.text : "";
                if (!string.IsNullOrEmpty(filterText))
                    sb.Append(Loc.Get(filterText)).Append(". ");
            }

            // Filter-aware list selection. MagicScreenController.FilterChanged
            // hides the main `list` and shows `otherList` when the user picks
            // Other; rewardDataList feeds otherList. Reading c.list there would
            // report whatever count the previous filter left behind.
            //
            // Sacred Time and Other have no deity/spirit selection, so we skip
            // the "Selected:" segment for them — saying just "Filter. N entries"
            // beats "Filter. . N entries" with the dangling empty selection.
            bool isOther = c.currentMagicFilter == FilterOtherworldBy.filter_Other;
            UIList activeList = isOther ? c.otherList : c.list;
            int listCount = activeList != null ? activeList.count : 0;

            string deity = SafeText(c.deityName);
            bool hasDeity = !string.IsNullOrEmpty(deity);
            if (hasDeity)
                sb.Append(Loc.Get("Selected: ")).Append(StringHelpers.StripTags(deity)).Append(". ");

            if (listCount > 0)
            {
                string itemWord = isOther ? Loc.Get("rewards") : Loc.Get("entries");
                sb.Append(listCount).Append(' ').Append(itemWord).Append(Loc.Get(" in list."));
            }
            else if (isOther)
            {
                sb.Append(Loc.Get("No rewards earned yet."));
            }

            ScreenReader.Say(sb.ToString(), interrupt);
            return true;
        }

        private static bool ReadMagicFull(MagicScreenController c)
        {
            var sb = new StringBuilder();

            if (c.filter != null)
            {
                string filterText = c.filter.captionText != null ? c.filter.captionText.text : "";
                if (!string.IsNullOrEmpty(filterText))
                    sb.Append(Loc.Get("Filter: ")).Append(Loc.Get(filterText)).Append(". ");
            }

            string deity = SafeText(c.deityName);
            if (!string.IsNullOrEmpty(deity))
            {
                sb.Append(StringHelpers.StripTags(deity));

                string desc = SafeText(c.deityDescription);
                if (!string.IsNullOrEmpty(desc))
                    sb.Append(": ").Append(desc);
                sb.Append(". ");
            }

            string temple = SafeText(c.templeSizeInfo);
            if (!string.IsNullOrEmpty(temple))
                sb.Append(temple).Append(". ");

            // Read blessings/effects
            if (c.effects != null)
            {
                bool hasEffects = false;
                for (int i = 0; i < c.effects.Count; i++)
                {
                    UIDeityEffectItem effect = c.effects[i];
                    if (effect != null && effect.gameObject.activeSelf)
                    {
                        string label = effect.label != null ? effect.label.text : "";
                        if (!string.IsNullOrEmpty(label))
                        {
                            if (!hasEffects)
                            {
                                sb.Append(Loc.Get("Blessings: "));
                                hasEffects = true;
                            }
                            else
                            {
                                sb.Append(", ");
                            }
                            bool isOn = effect.toggle != null && effect.toggle.isOn;
                            sb.Append(label);
                            if (isOn) sb.Append(Loc.Get(" (active)"));
                        }
                    }
                }
                if (hasEffects) sb.Append(". ");
            }

            // List items — same filter-aware list pick as the summary path,
            // since for filter_Other the items live in otherList, not list.
            bool isOther = c.currentMagicFilter == FilterOtherworldBy.filter_Other;
            UIList activeList = isOther ? c.otherList : c.list;
            int listCount = activeList != null ? activeList.count : 0;
            if (listCount > 0)
            {
                string itemWord = isOther ? Loc.Get("rewards") : Loc.Get("entries");
                sb.Append(listCount).Append(' ').Append(itemWord).Append(Loc.Get(". Press L to navigate list"));
            }
            else if (isOther)
            {
                sb.Append(Loc.Get("No rewards earned yet."));
            }

            ScreenReader.Say(sb.ToString());
            return true;
        }

        // ============================================================
        // Lore Screen
        // ============================================================

        private static bool ReadLoreSummary(LoreScreenController c, bool interrupt)
        {
            int historyCount = c.historyList != null ? c.historyList.count : 0;
            int mythCount    = c.mythList    != null ? c.mythList.count    : 0;

            var sb = new StringBuilder();
            sb.Append(historyCount).Append(Loc.Get(historyCount == 1 ? " history entry, " : " history entries, "));
            sb.Append(mythCount).Append(Loc.Get(mythCount == 1 ? " myth. " : " myths. "));

            // Preview the first few entries from each list so the auto-summary
            // gives the user a sense of what's actually in there before they Tab
            // into the lists.
            AppendListPreview(sb, c.historyList, Loc.Get("History"), 3);
            AppendListPreview(sb, c.mythList,    Loc.Get("Myths"),   3);

            sb.Append(Loc.Get("Tab cycles history and myths zones, Up and Down cycle entries, "));
            sb.Append(Loc.Get("Enter opens, M opens Manual."));

            ScreenReader.Say(sb.ToString(), interrupt);
            return true;
        }

        private static bool ReadLoreFull(LoreScreenController c)
        {
            var sb = new StringBuilder();

            if (c.historyList != null && c.historyList.count > 0)
            {
                sb.Append(Loc.Get("History: "));
                for (int i = 0; i < c.historyList.count; i++)
                {
                    UIListItem item = c.historyList[i];
                    if (item != null)
                    {
                        string text = item.text;
                        if (!string.IsNullOrEmpty(text))
                        {
                            if (i > 0) sb.Append(", ");
                            sb.Append(text);
                        }
                    }
                }
                sb.Append(". ");
            }

            if (c.mythList != null && c.mythList.count > 0)
            {
                sb.Append(Loc.Get("Myths: "));
                for (int i = 0; i < c.mythList.count; i++)
                {
                    UIListItem item = c.mythList[i];
                    if (item != null)
                    {
                        string text = item.text;
                        if (!string.IsNullOrEmpty(text))
                        {
                            if (i > 0) sb.Append(", ");
                            sb.Append(text);
                        }
                    }
                }
                sb.Append(". ");
            }

            sb.Append(Loc.Get("Tab cycles zones, Up and Down cycle entries, Enter opens, "));
            sb.Append(Loc.Get("M opens Manual, F5 for status."));

            ScreenReader.Say(sb.ToString());
            return true;
        }

        /// <summary>
        /// Append a short preview of the first <paramref name="max"/> entries from a list
        /// (e.g. "History: A, B, C and 7 more. "). Skips empty lists silently.
        /// </summary>
        private static void AppendListPreview(StringBuilder sb, UIList list, string label, int max)
        {
            if (list == null || list.count == 0) return;
            sb.Append(label).Append(": ");
            int n = list.count > max ? max : list.count;
            int written = 0;
            for (int i = 0; i < n; i++)
            {
                UIListItem item = list[i];
                if (item == null) continue;
                string text = item.text;
                if (string.IsNullOrEmpty(text)) continue;
                if (written > 0) sb.Append(", ");
                sb.Append(StringHelpers.StripTags(text));
                written++;
            }
            if (list.count > max)
                sb.Append(Loc.Get(" and ")).Append(list.count - max).Append(Loc.Get(" more"));
            sb.Append(". ");
        }

        // ============================================================
        // Saga Screen
        // ============================================================

        private static bool ReadSagaSummary(SagaScreenController c, bool interrupt)
        {
            try
            {
                SagaView saga = Singleton<GameManager>.instance.saga;
                if (saga != null)
                {
                    var sb = new StringBuilder();

                    // Restore-status orientation up front: the user's primary
                    // reason to be on this screen is either to read history or
                    // to undo a decision. Both want to know how many restores
                    // remain before they pick a year.
                    int max = 0;
                    try { max = Game.IntegerVariable("restoreMax"); }
                    catch (Exception ex) { DebugLogger.Error("ManagementScreenReader.SagaSummary.restoreMax", ex); }
                    int used = Game.restores;
                    if (max > 0)
                        sb.Append(Loc.Get("Restores used ")).Append(used).Append(Loc.Get(" of ")).Append(max).Append(". ");

                    if (saga.sagaText != null && !string.IsNullOrEmpty(saga.sagaText.text))
                    {
                        string clean = StringHelpers.StripTags(saga.sagaText.text);
                        if (clean.Length > 200)
                            clean = clean.Substring(0, 200) + "...";
                        sb.Append(clean).Append(" ");
                    }

                    sb.Append(Loc.Get("Up and Down to pick a year. D for full text. "));
                    if (max > 0 && Game.canBeRestored)
                        sb.Append(Loc.Get("Enter to restore. "));
                    sb.Append(Loc.Get("F5 for status."));

                    ScreenReader.Say(sb.ToString(), interrupt);
                    return true;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ManagementScreenReader.SagaSummary", ex);
            }
            // Header already announced by ScreenChangePatches; emit empty content.
            return true;
        }

        private static bool ReadSagaFull(SagaScreenController c)
        {
            try
            {
                SagaView saga = Singleton<GameManager>.instance.saga;
                if (saga != null && saga.sagaText != null)
                {
                    string text = saga.sagaText.text;
                    if (!string.IsNullOrEmpty(text))
                    {
                        ScreenReader.Say(StringHelpers.StripTags(text));
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ManagementScreenReader.SagaFull", ex);
            }
            ScreenReader.Say(Loc.Get("No saga text available."));
            return true;
        }

        // ============================================================
        // Helpers
        // ============================================================

        /// <summary>Safely get text from a UILabel, returns empty string if null.</summary>
        private static string SafeText(UILabel label)
        {
            if (label == null) return "";
            return label.text ?? "";
        }

        /// <summary>Get mood text from MoodDisplay via its label field.</summary>
        private static string SafeMood(MoodDisplay display)
        {
            if (display == null || display.label == null) return Loc.Get("unknown");
            string text = display.label.text;
            if (string.IsNullOrEmpty(text)) return Loc.Get("unknown");

            // The game sets moodDisplay.label.text with the hardcoded English template
            // "The clan mood is X." via Unity code (not PluginImport), so the prefix
            // never reaches our Translator while X itself is routed through PluginImport
            // and ends up German. Strip the English prefix and re-emit it via Loc.Get
            // so the screen reader hears a clean German "Die Klanstimmung ist X.".
            const string EnPrefix = "The clan mood is ";
            if (text.StartsWith(EnPrefix, System.StringComparison.Ordinal))
            {
                string moodWord = text.Substring(EnPrefix.Length).TrimEnd('.', ' ');
                return Loc.Get(EnPrefix) + moodWord + ".";
            }
            return text;
        }

        /// <summary>Append a label value with description if the value is non-zero.</summary>
        private static void AppendIfNonZero(StringBuilder sb, UILabel label, string description)
        {
            string val = SafeText(label);
            if (!string.IsNullOrEmpty(val) && val != "0")
                sb.Append(", ").Append(val).Append(" ").Append(Loc.Get(description));
        }

        /// <summary>Append <paramref name="prefix"/> + label.text only when the label has non-empty content.</summary>
        private static void AppendIfNonEmpty(StringBuilder sb, UILabel label, string prefix)
        {
            string val = SafeText(label);
            if (!string.IsNullOrEmpty(val))
                sb.Append(prefix).Append(val);
        }

        /// <summary>Append a list summary with item count and first few item texts.</summary>
        private static void AppendListSummary(StringBuilder sb, UIList list, string listName)
        {
            if (list == null || list.count == 0) return;

            sb.Append(listName).Append(": ");
            int max = list.count > 5 ? 5 : list.count;
            for (int i = 0; i < max; i++)
            {
                UIListItem item = list[i];
                if (item != null)
                {
                    string text = item.text;
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(text);
                    }
                }
            }
            if (list.count > 5) sb.Append(Loc.Get(" and ")).Append(list.count - 5).Append(Loc.Get(" more"));
            sb.Append(". ");
        }

    }
}
