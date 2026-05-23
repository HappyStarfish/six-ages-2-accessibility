using System;
using UnityEngine;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Reads the on-screen stat panel for screen-reader users.
    ///
    /// F2 cycles through four short views: season explanation → time →
    /// resources → reputation. The cycle position only resets when the user
    /// presses a non-F2 key (handled by KeyboardNavigationHandler via
    /// <see cref="ResetCycle"/>) — same pattern as the F3 advisor cycle and
    /// the F4 dashboard cycle. Holding the position lets the user step back
    /// to where they were even after a pause.
    /// </summary>
    public static class StatPanelReader
    {
        // Stat-panel cycling state: 0=season-explanation, 1=time, 2=resources,
        // 3=reputation. -1 = idle (no announcement yet this cycle).
        private static int _cycleIndex = -1;
        private const int CycleCount = 4;

        /// <summary>True while the user is mid-cycle (used by the cycle-reset hook).</summary>
        public static bool HasCycleState { get { return _cycleIndex >= 0; } }

        /// <summary>Reset so the next F2 starts fresh at "season explanation".</summary>
        public static void ResetCycle() { _cycleIndex = -1; }

        /// <summary>Speak the current season and year. Called from the F2 cycle.</summary>
        public static void ReadSeasonAndYear()
        {
            try
            {
                string season = PluginImport.Game_SeasonName();
                string year = PluginImport.Game_YearName();

                if (string.IsNullOrEmpty(season) && string.IsNullOrEmpty(year))
                {
                    ScreenReader.Say("Time not available.");
                    return;
                }

                ScreenReader.Say(season + ", " + year);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("StatPanelReader.ReadSeasonAndYear", ex);
                ScreenReader.Say("Could not read time.");
            }
        }

        /// <summary>
        /// Speak season + year + the full season explanation — same text the hover
        /// tooltip on the season icon shows. First entry in the F2 cycle.
        /// Replicates the logic in
        /// <c>AdvisorHelper.ShowSeasonInfo</c>: prepend "Early" / "Late" based
        /// on turn-in-year parity, except for Sacred Time (turn 11) which has
        /// no early/late distinction. Falls back to season + year if the
        /// localization table doesn't return text.
        /// </summary>
        public static void ReadSeasonExplanation()
        {
            try
            {
                string shortName = Game.shortSeasonName;
                if (string.IsNullOrEmpty(shortName))
                {
                    ReadSeasonAndYear();
                    return;
                }

                string explanation = Localized.StringFromTable(shortName + "Explanation", "Text");
                int turnInYear = Game.turnInYear;
                if (turnInYear != 11)
                {
                    string prefix = (turnInYear % 2 == 1) ? "Early " : "Late ";
                    explanation = prefix + explanation;
                }

                if (string.IsNullOrEmpty(explanation))
                {
                    ReadSeasonAndYear();
                    return;
                }

                string season = PluginImport.Game_SeasonName();
                string year = PluginImport.Game_YearName();
                string header = (!string.IsNullOrEmpty(season) && !string.IsNullOrEmpty(year))
                    ? (season + ", " + year + ". ")
                    : "";
                ScreenReader.Say(header + StringHelpers.StripTags(explanation));
            }
            catch (Exception ex)
            {
                DebugLogger.Error("StatPanelReader.ReadSeasonExplanation", ex);
                ScreenReader.Say("Could not read season explanation.");
            }
        }

        /// <summary>Cycle through season-explanation → time → resources → reputation (F2).</summary>
        public static void Cycle()
        {
            _cycleIndex = (_cycleIndex + 1) % CycleCount;

            switch (_cycleIndex)
            {
                case 0: ReadSeasonExplanation(); break;
                case 1: ReadSeasonAndYear(); break;
                case 2: ReadResources(); break;
                case 3: ReadReputation(); break;
            }
        }

        private static void ReadResources()
        {
            try
            {
                if (IsPrologStatsHidden())
                {
                    ScreenReader.Say("Resources not yet shown.");
                    return;
                }
                int herds = PluginImport.PC_Herds();
                int goods = PluginImport.PC_Goods();
                int warriors = PluginImport.PC_Warriors();
                int magic = PluginImport.PC_Magic();
                ScreenReader.Say("Herds " + herds + ", Goods " + goods + ", Warriors " + warriors + ", Magic " + magic);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("StatPanelReader.ReadResources", ex);
                ScreenReader.Say("Could not read resources.");
            }
        }

        private static void ReadReputation()
        {
            try
            {
                if (IsPrologStatsHidden())
                {
                    ScreenReader.Say("Reputation not yet shown.");
                    return;
                }
                int like = PluginImport.PC_LikeCount();
                int hate = PluginImport.PC_HateCount();
                int fear = PluginImport.PC_FearCount();
                int mock = PluginImport.PC_MockCount();
                ScreenReader.Say("Like " + like + ", Hate " + hate + ", Fear " + fear + ", Mock " + mock);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("StatPanelReader.ReadReputation", ex);
                ScreenReader.Say("Could not read reputation.");
            }
        }

        // The prolog/tutorial sometimes hides parts of the HUD via GuidanceController so
        // narrative beats land before the player has any stats to look at. Mirror that
        // hiding for the audio readout — speaking "Herds 0, Goods 0..." would falsely
        // imply the values exist and are zero, when in fact they're meant to be invisible.
        private static bool IsPrologStatsHidden()
        {
            try
            {
                GuidanceController guidance = UnityEngine.Object.FindObjectOfType<GuidanceController>();
                return guidance != null && guidance.hideSomeStatistics;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("StatPanelReader.IsPrologStatsHidden", ex);
                return false;
            }
        }
    }
}
