using System;
using System.Collections.Generic;
using System.Text;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Dashboard concerns + magic for the six management screens (Clan, Magic,
    /// Map, Relations, War, Wealth). Backed by <c>PluginImport.PC_Concerns*</c>
    /// and <c>BlessingDataList</c> — same sources the in-game dashboard tile
    /// (<c>DashboardMenuItem</c>) uses.
    ///
    /// Two access modes:
    /// <list type="bullet">
    /// <item><b>Cycle</b> (F4 / Shift+F4): step through every item across all six
    ///       screens, grouped into seven categories — Stresses, Advantages,
    ///       Warnings, Omens, Active magic, Known magic, Unlearned magic. The
    ///       first item of each category is preceded by a "Stresses, 3 items."
    ///       header; empty categories are skipped silently. Forward+backward
    ///       wrap through an overview line and an "End of dashboard." sentinel.</item>
    /// <item><b>Current-screen report</b> (Ctrl+F4 and auto-announce on screen
    ///       entry): one-shot summary of concerns + only currently active magic
    ///       for the screen the user just opened. The known/unlearned roster
    ///       is intentionally NOT included here — it's not "what's affecting
    ///       you now", just an availability list, and mixing it next to
    ///       warnings made the auto-announce sound like the known blessings
    ///       were answering the warnings.</item>
    /// </list>
    /// </summary>
    public static class ConcernReader
    {
        public enum ConcernCategory
        {
            Stress,
            Advantage,
            Warning,
            Omen,
            ActiveMagic,
            KnownMagic,
            UnlearnedMagic,
        }

        private struct ConcernItem
        {
            public ConcernCategory Category;
            public GameScreen Screen;
            public string Text;
            public bool FirstInCategory;
            public int IndexInCategory; // 1-based
        }

        // Cycle order. Stress first because the user almost always wants
        // "what's wrong right now" before browsing magic availability.
        private static readonly ConcernCategory[] CategoryOrder = new ConcernCategory[]
        {
            ConcernCategory.Stress,
            ConcernCategory.Advantage,
            ConcernCategory.Warning,
            ConcernCategory.Omen,
            ConcernCategory.ActiveMagic,
            ConcernCategory.KnownMagic,
            ConcernCategory.UnlearnedMagic,
        };

        // Screen order within each category mirrors the in-game dashboard
        // tile order (ManagementMenuController.OnEnable uses (GameScreen)(i+1)
        // for i=0..5, which is Clan/Magic/Map/Relations/War/Wealth).
        private static readonly GameScreen[] DashboardScreens = new GameScreen[]
        {
            GameScreen.screen_Clan,
            GameScreen.screen_Magic,
            GameScreen.screen_Map,
            GameScreen.screen_Relations,
            GameScreen.screen_War,
            GameScreen.screen_Wealth,
        };

        /// <summary>True if the given screen has a dashboard tile.</summary>
        public static bool HasDashboardEntry(GameScreen screen)
        {
            for (int i = 0; i < DashboardScreens.Length; i++)
                if (DashboardScreens[i] == screen) return true;
            return false;
        }

        // ============================================================
        // Cycle state (F4 / Shift+F4)
        // ============================================================

        private static List<ConcernItem> _cycleItems;
        // -1 = idle (no announcement yet this cycle), 0 = overview,
        // 1..N = item at index step-1, N+1 = "End of dashboard" sentinel.
        private static int _cycleStep = -1;

        /// <summary>True while the user is mid-cycle (used by the cycle-reset hook).</summary>
        public static bool HasCycleState { get { return _cycleStep >= 0; } }

        /// <summary>
        /// Reset the cycle so the next F4 starts fresh with the overview.
        /// Called from <see cref="KeyboardNavigationHandler"/> whenever the user
        /// presses any non-F4 key — same pattern as <see cref="AdvisorReader.ResetCycle"/>.
        /// </summary>
        public static void ResetCycle()
        {
            _cycleItems = null;
            _cycleStep = -1;
        }

        /// <summary>
        /// Advance the cycle by <paramref name="direction"/> (+1 forward, -1 backward)
        /// and speak the resulting step. The first press in a fresh cycle builds the
        /// item snapshot and announces the overview; subsequent presses walk through
        /// items in category-then-screen order; the last step before wrap is the
        /// "End of dashboard" sentinel.
        /// </summary>
        public static void Cycle(int direction)
        {
            try
            {
                if (_cycleItems == null) _cycleItems = BuildItems();
                int total = _cycleItems.Count + 2; // overview + items + end-of-dashboard

                int newStep;
                if (_cycleStep < 0)
                {
                    // First press in this cycle. Forward starts at overview;
                    // backward starts at the End sentinel so Shift+F4 first
                    // press doesn't dump the user into the middle.
                    newStep = (direction >= 0) ? 0 : total - 1;
                }
                else
                {
                    newStep = ((_cycleStep + direction) % total + total) % total;
                }
                _cycleStep = newStep;
                ScreenReader.Say(BuildSpeechForStep(newStep));
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ConcernReader.Cycle", ex);
                ScreenReader.Say(Loc.Get("Could not cycle dashboard."));
            }
        }

        private static string BuildSpeechForStep(int step)
        {
            if (step == 0) return BuildOverview();
            if (step == _cycleItems.Count + 1) return Loc.Get("End of dashboard.");

            ConcernItem item = _cycleItems[step - 1];
            var sb = new StringBuilder();

            if (item.FirstInCategory)
            {
                int count = CountInCategory(item.Category);
                sb.Append(Loc.Get(CategoryPluralLabel(item.Category))).Append(", ");
                sb.Append(count).Append(Loc.Get(count == 1 ? " item. " : " items. "));
            }

            sb.Append(Loc.Get(CategorySingularLabel(item.Category))).Append(' ');
            sb.Append(item.IndexInCategory).Append(": ").Append(item.Text);
            sb.Append(Loc.Get(", on ")).Append(Loc.Get(GameScreens.NameOf(item.Screen))).Append('.');
            return sb.ToString();
        }

        private static int CountInCategory(ConcernCategory cat)
        {
            int n = 0;
            for (int i = 0; i < _cycleItems.Count; i++)
                if (_cycleItems[i].Category == cat) n++;
            return n;
        }

        private static string BuildOverview()
        {
            if (_cycleItems == null || _cycleItems.Count == 0)
                return Loc.Get("Dashboard. Nothing to report.");

            var sb = new StringBuilder(Loc.Get("Dashboard. "));
            int parts = 0;
            for (int c = 0; c < CategoryOrder.Length; c++)
            {
                int count = CountInCategory(CategoryOrder[c]);
                if (count == 0) continue;
                if (parts > 0) sb.Append(", ");
                sb.Append(count).Append(' ').Append(Loc.Get(OverviewLabel(CategoryOrder[c], count)));
                parts++;
            }
            sb.Append('.');
            return sb.ToString();
        }

        // ============================================================
        // Current-screen one-shot (Ctrl+F4 and auto-announce)
        // ============================================================

        /// <summary>
        /// Compact one-shot summary for a single management screen: every
        /// concern (stress / advantage / warning / omen) plus currently active
        /// magic only. Known and unlearned magic are excluded — they're "what
        /// you could do", not "what's happening now", and they were confusing
        /// to hear right after the warnings in the auto-announce.
        ///
        /// <paramref name="verbose"/> appends the full <c>blessing.explanation</c>
        /// text to each active blessing — what the in-game hover tooltip on the
        /// magic icon shows. Auto-announce calls with false (keeps the screen
        /// entry quick); Ctrl+F4 calls with true so the user can pull the full
        /// effect description on demand.
        /// </summary>
        public static string BuildCurrentScreenReport(GameScreen screen, bool verbose = false)
        {
            if (!HasDashboardEntry(screen)) return null;

            var sb = new StringBuilder();
            sb.Append(Loc.Get(GameScreens.NameOf(screen))).Append(". ");
            int startLen = sb.Length;

            AppendConcernsTo(sb, screen);
            AppendActiveMagicTo(sb, screen, verbose);

            if (sb.Length == startLen) sb.Append(Loc.Get("No concerns and no active magic."));
            return sb.ToString();
        }

        // ============================================================
        // Item-list builder
        // ============================================================

        private static List<ConcernItem> BuildItems()
        {
            var byCategory = new Dictionary<ConcernCategory, List<ConcernItem>>();
            for (int c = 0; c < CategoryOrder.Length; c++)
                byCategory[CategoryOrder[c]] = new List<ConcernItem>();

            for (int s = 0; s < DashboardScreens.Length; s++)
            {
                GameScreen screen = DashboardScreens[s];
                CollectConcerns(screen, byCategory);
                CollectMagic(screen, byCategory);
            }

            var flat = new List<ConcernItem>();
            for (int c = 0; c < CategoryOrder.Length; c++)
            {
                var list = byCategory[CategoryOrder[c]];
                for (int i = 0; i < list.Count; i++)
                {
                    ConcernItem it = list[i];
                    it.IndexInCategory = i + 1;
                    it.FirstInCategory = (i == 0);
                    flat.Add(it);
                }
            }
            return flat;
        }

        private static void CollectConcerns(GameScreen screen, Dictionary<ConcernCategory, List<ConcernItem>> bucket)
        {
            try
            {
                int n = PluginImport.PC_ConcernsPertainingTo((int)screen);
                // Important: PC_ConcernID/Type/IsNegative/IsPositive/DashboardText
                // all index into the buffer set up by the latest call to
                // PC_ConcernsPertainingTo. Do NOT call any other PC_ConcernsPertainingTo
                // before reading all items for this screen, or the indices re-point.
                for (int i = 0; i < n; i++)
                {
                    ConcernCategory cat = ClassifyConcern(i);
                    string text = SafeConcernText(i);
                    bucket[cat].Add(new ConcernItem
                    {
                        Category = cat,
                        Screen = screen,
                        Text = text,
                    });
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ConcernReader.CollectConcerns", ex);
            }
        }

        private static void CollectMagic(GameScreen screen, Dictionary<ConcernCategory, List<ConcernItem>> bucket)
        {
            try
            {
                // Mirrors DashboardMenuItem.AddMagic: include direct deity
                // blessings with level >= kUnlearnedBlessing, skip rows that
                // are tagged as spirit-only (those come from the spirit loop
                // below to avoid double-listing).
                var deityList = new BlessingDataList();
                deityList.InitMagicPertainingToScreen(screen);
                for (int i = 0; i < deityList.count; i++)
                {
                    BlessingDataList.Item b = deityList[i];
                    if (b.GetString("spirit") != null) continue;
                    BlessingLevel level = PlayerClan.LevelOfMagic(b.blessingID);
                    if ((int)level < (int)BlessingLevel.kUnlearnedBlessing) continue;
                    ConcernCategory cat = MagicCategoryForLevel(level);
                    string text = FormatMagicItem(b, level, cat);
                    bucket[cat].Add(new ConcernItem
                    {
                        Category = cat,
                        Screen = screen,
                        Text = text,
                    });
                }

                // Spirit-bargain blessings active for this screen. Same gating
                // as DashboardMenuItem: skip "instant" entries (one-time spells
                // that don't show on the tile) and require a recent bargain
                // with the spirit.
                var spiritList = new SpiritDataList();
                spiritList.InitSpiritsKnown();
                var perSpirit = new BlessingDataList();
                for (int s = 0; s < spiritList.count; s++)
                {
                    string spirit = spiritList[s];
                    if (!PlayerClan.HaveRecentlyBargainedWith(spirit)) continue;
                    perSpirit.InitMagicForSpirit(spirit);
                    for (int k = 0; k < perSpirit.count; k++)
                    {
                        BlessingDataList.Item b = perSpirit[k];
                        if (b.blessingScreen != screen) continue;
                        if (b.BoolFromProto("instant")) continue;
                        BlessingLevel level = PlayerClan.LevelOfMagic(b.blessingID);
                        if ((int)level < (int)BlessingLevel.kTransientBlessing) continue;

                        ConcernCategory cat = ConcernCategory.ActiveMagic;
                        string text = FormatMagicItem(b, level, cat);
                        bucket[cat].Add(new ConcernItem
                        {
                            Category = cat,
                            Screen = screen,
                            Text = text,
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ConcernReader.CollectMagic", ex);
            }
        }

        // ============================================================
        // Classification + formatting
        // ============================================================

        private static ConcernCategory ClassifyConcern(int index)
        {
            try
            {
                if (PluginImport.PC_ConcernIsPositive(index)) return ConcernCategory.Advantage;
                if (PluginImport.PC_ConcernIsNegative(index)) return ConcernCategory.Stress;
                ConcernType t = (ConcernType)PluginImport.PC_ConcernType(index);
                if (t == ConcernType.kWarningConcern) return ConcernCategory.Warning;
                if (t == ConcernType.kOmenConcern) return ConcernCategory.Omen;
                // Ongoing / transient / permanent without sign — bucket under
                // Warning (the closest "needs attention" category).
                return ConcernCategory.Warning;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ConcernReader.ClassifyConcern", ex);
                return ConcernCategory.Warning;
            }
        }

        private static ConcernCategory MagicCategoryForLevel(BlessingLevel level)
        {
            if ((int)level >= (int)BlessingLevel.kTransientBlessing) return ConcernCategory.ActiveMagic;
            if (level == BlessingLevel.kBlessingKnown) return ConcernCategory.KnownMagic;
            return ConcernCategory.UnlearnedMagic;
        }

        private static string SafeConcernText(int index)
        {
            try
            {
                string raw = PluginImport.PC_ConcernDashboardText(index);
                if (string.IsNullOrEmpty(raw)) return Loc.Get("(unspecified)");
                return StringHelpers.StripTags(Game.ReplacePlaceholdersIn(raw));
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ConcernReader.SafeConcernText", ex);
                return Loc.Get("(unreadable)");
            }
        }

        private static string FormatMagicItem(BlessingDataList.Item b, BlessingLevel level, ConcernCategory cat)
        {
            string name = b.GetString("name");
            if (string.IsNullOrEmpty(name)) name = b.blessingID ?? "(blessing)";

            Deity deity = b.blessingDeity;
            string source = (deity != Deity.deity_None)
                ? Game.NameOfDeity(deity)
                : (b.GetString("spirit") + Loc.Get(" Spirit"));
            bool dead = deity != Deity.deity_None && PlayerClan.IsDeityDead(deity);

            var sb = new StringBuilder();
            sb.Append(name).Append(", ").Append(source);

            if (dead)
            {
                sb.Append(Loc.Get(", deity dead"));
                return sb.ToString();
            }

            // Only Active magic carries a meaningful state suffix (permanent
            // vs transient). For Known/Unlearned the category label already
            // captures the state — adding "known" or "unlearned" here would
            // just echo the category prefix the cycle has already announced.
            if (cat == ConcernCategory.ActiveMagic)
            {
                if ((int)level >= (int)BlessingLevel.kPermanentBlessing)
                    sb.Append(Loc.Get(", permanent"));
                else
                    sb.Append(Loc.Get(", transient"));
            }
            return sb.ToString();
        }

        // ============================================================
        // Category labels
        // ============================================================

        private static string CategoryPluralLabel(ConcernCategory cat)
        {
            switch (cat)
            {
                case ConcernCategory.Stress: return "Stresses";
                case ConcernCategory.Advantage: return "Advantages";
                case ConcernCategory.Warning: return "Warnings";
                case ConcernCategory.Omen: return "Omens";
                case ConcernCategory.ActiveMagic: return "Active magic";
                case ConcernCategory.KnownMagic: return "Known magic";
                case ConcernCategory.UnlearnedMagic: return "Unlearned magic";
                default: return "Items";
            }
        }

        private static string CategorySingularLabel(ConcernCategory cat)
        {
            switch (cat)
            {
                case ConcernCategory.Stress: return "Stress";
                case ConcernCategory.Advantage: return "Advantage";
                case ConcernCategory.Warning: return "Warning";
                case ConcernCategory.Omen: return "Omen";
                case ConcernCategory.ActiveMagic: return "Active";
                case ConcernCategory.KnownMagic: return "Known";
                case ConcernCategory.UnlearnedMagic: return "Unlearned";
                default: return "Item";
            }
        }

        private static string OverviewLabel(ConcernCategory cat, int count)
        {
            switch (cat)
            {
                case ConcernCategory.Stress: return count == 1 ? "stress" : "stresses";
                case ConcernCategory.Advantage: return count == 1 ? "advantage" : "advantages";
                case ConcernCategory.Warning: return count == 1 ? "warning" : "warnings";
                case ConcernCategory.Omen: return count == 1 ? "omen" : "omens";
                case ConcernCategory.ActiveMagic: return count == 1 ? "active blessing" : "active blessings";
                case ConcernCategory.KnownMagic: return count == 1 ? "known blessing" : "known blessings";
                case ConcernCategory.UnlearnedMagic: return count == 1 ? "unlearned blessing" : "unlearned blessings";
                default: return "items";
            }
        }

        // ============================================================
        // Current-screen report helpers
        // ============================================================

        private static void AppendConcernsTo(StringBuilder sb, GameScreen screen)
        {
            try
            {
                int n = PluginImport.PC_ConcernsPertainingTo((int)screen);
                for (int i = 0; i < n; i++)
                {
                    ConcernCategory cat = ClassifyConcern(i);
                    string text = SafeConcernText(i);
                    sb.Append(Loc.Get(CategorySingularLabel(cat))).Append(": ").Append(text);
                    if (text.Length == 0 || text[text.Length - 1] != '.') sb.Append('.');
                    sb.Append(' ');
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ConcernReader.AppendConcernsTo", ex);
            }
        }

        private static void AppendActiveMagicTo(StringBuilder sb, GameScreen screen, bool verbose)
        {
            try
            {
                bool started = false;

                var deityList = new BlessingDataList();
                deityList.InitMagicPertainingToScreen(screen);
                for (int i = 0; i < deityList.count; i++)
                {
                    BlessingDataList.Item b = deityList[i];
                    if (b.GetString("spirit") != null) continue;
                    BlessingLevel level = PlayerClan.LevelOfMagic(b.blessingID);
                    if ((int)level < (int)BlessingLevel.kTransientBlessing) continue;
                    Deity deity = b.blessingDeity;
                    if (deity != Deity.deity_None && PlayerClan.IsDeityDead(deity)) continue;

                    AppendActiveEntry(sb, ref started, FormatMagicItem(b, level, ConcernCategory.ActiveMagic));
                    AppendBlessingExplanation(sb, b, verbose);
                }

                var spiritList = new SpiritDataList();
                spiritList.InitSpiritsKnown();
                var perSpirit = new BlessingDataList();
                for (int s = 0; s < spiritList.count; s++)
                {
                    string spirit = spiritList[s];
                    if (!PlayerClan.HaveRecentlyBargainedWith(spirit)) continue;
                    perSpirit.InitMagicForSpirit(spirit);
                    for (int k = 0; k < perSpirit.count; k++)
                    {
                        BlessingDataList.Item b = perSpirit[k];
                        if (b.blessingScreen != screen) continue;
                        if (b.BoolFromProto("instant")) continue;
                        BlessingLevel level = PlayerClan.LevelOfMagic(b.blessingID);
                        if ((int)level < (int)BlessingLevel.kTransientBlessing) continue;

                        AppendActiveEntry(sb, ref started, FormatMagicItem(b, level, ConcernCategory.ActiveMagic));
                        AppendBlessingExplanation(sb, b, verbose);
                    }
                }

                if (started) sb.Append('.');
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ConcernReader.AppendActiveMagicTo", ex);
            }
        }

        private static void AppendBlessingExplanation(StringBuilder sb, BlessingDataList.Item b, bool verbose)
        {
            if (!verbose) return;
            try
            {
                string explanation = b.GetString("explanation");
                if (string.IsNullOrEmpty(explanation)) return;
                sb.Append(" (").Append(StringHelpers.StripTags(explanation)).Append(')');
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ConcernReader.AppendBlessingExplanation", ex);
            }
        }

        private static void AppendActiveEntry(StringBuilder sb, ref bool started, string entry)
        {
            if (string.IsNullOrEmpty(entry)) return;
            if (!started)
            {
                sb.Append(Loc.Get("Active magic: "));
                started = true;
            }
            else
            {
                sb.Append("; ");
            }
            sb.Append(entry);
        }
    }
}
