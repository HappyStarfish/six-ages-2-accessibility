using System.Text;

namespace SixAgesAccessibility
{
    /// <summary>Centralized GameScreen → human-readable name mapping.</summary>
    public static class GameScreens
    {
        /// <summary>
        /// Returns the spoken name for a GameScreen. Explicit mappings for every screen
        /// the user can land on; an unrecognized enum value falls through to a string
        /// derived from the enum name (strip the "screen_" prefix and split CamelCase),
        /// so future additions still produce something readable.
        /// </summary>
        public static string NameOf(GameScreen screen)
        {
            switch (screen)
            {
                // Management (side-menu indices 1..9)
                case GameScreen.screen_Clan: return "Clan";
                case GameScreen.screen_Magic: return "Magic";
                case GameScreen.screen_Map: return "Map";
                case GameScreen.screen_Relations: return "Relations";
                case GameScreen.screen_War: return "War";
                case GameScreen.screen_Wealth: return "Wealth";
                case GameScreen.screen_Lore: return "Lore";
                case GameScreen.screen_Saga: return "Saga";
                case GameScreen.screen_Controls: return "Controls";
                case GameScreen.screen_SacredTime: return "Sacred time";

                // Management dialogs
                case GameScreen.screen_Reorganize: return "Reorganize";
                case GameScreen.screen_Venture: return "Venture";
                case GameScreen.screen_Emissary: return "Emissary";
                case GameScreen.screen_Foray: return "Foray";
                case GameScreen.screen_Ritual: return "Ritual";
                case GameScreen.screen_Sacrifice: return "Sacrifice";
                case GameScreen.screen_Spirit: return "Spirit";
                case GameScreen.screen_Temple: return "Temple";
                case GameScreen.screen_CattleRaid: return "Cattle raid";
                case GameScreen.screen_HonorRaid: return "Honor raid";
                case GameScreen.screen_Fortify: return "Fortify";
                case GameScreen.screen_Raid: return "Raid";
                case GameScreen.screen_Warriors: return "Warriors";
                case GameScreen.screen_Caravan: return "Caravan";

                // Menu / out-of-game
                case GameScreen.screen_MainMenu: return "Main menu";
                case GameScreen.screen_ChooseGame: return "Choose game";
                case GameScreen.screen_Marketing: return "Marketing";
                case GameScreen.screen_Thanks: return "Thanks";
                case GameScreen.screen_Menu: return "Menu";

                // Story / interactive
                case GameScreen.screen_Intro: return "Intro";
                case GameScreen.screen_IntroRecap: return "Intro recap";
                case GameScreen.screen_Scene: return "Scene";
                case GameScreen.screen_SceneNoProlog: return "Scene";
                case GameScreen.screen_Heroquest: return "Hero quest";
                case GameScreen.screen_Myth: return "Myth";
                case GameScreen.screen_Prolog: return "Prolog";
                case GameScreen.screen_PrologIntro: return "Prolog intro";
                case GameScreen.screen_EndScene: return "Scene end";
                case GameScreen.screen_EndQuest: return "Quest end";
                case GameScreen.screen_FinalScene: return "Final scene";
                case GameScreen.screen_FinalSceneProlog: return "Final scene prolog";

                // Combat
                case GameScreen.screen_BattleResults: return "Battle results";
                case GameScreen.screen_HeroicCombat: return "Heroic combat";

                // Picker / detail screens
                case GameScreen.screen_ChooseLeader: return "Choose leader";
                case GameScreen.screen_SacrificeDetails: return "Sacrifice details";

                // End-of-game
                case GameScreen.screen_Victory: return "Victory";
                case GameScreen.screen_Lost: return "Game over";
                case GameScreen.screen_Won: return "Game won";
                case GameScreen.screen_Manual: return "Manual";

                // Year-boundary marker (ShowManagementScreen rewrites this to screen_Clan
                // before activation, so it should normally never reach the announcer; map
                // anyway in case it ever escapes).
                case GameScreen.screen_FirstOfYear: return "First of year";

                case GameScreen.screen_All: return "All screens";

                default: return PrettifyEnumName(screen.ToString());
            }
        }

        /// <summary>
        /// Returns the spoken name for a side-menu index 1..9. This is identical to
        /// NameOf((GameScreen)index) for the management screens because the GameScreen enum
        /// values screen_Clan..screen_Controls are defined as 1..9 in Assembly-CSharp-firstpass.
        /// </summary>
        public static string NameOfMenuIndex(int menuIndex)
        {
            return NameOf((GameScreen)menuIndex);
        }

        /// <summary>
        /// Fallback formatter for unmapped enum names. Strips the "screen_" prefix and
        /// inserts spaces before each interior capital letter so something like
        /// "screen_BattleResults" reads as "Battle results" instead of "screen_BattleResults".
        /// </summary>
        private static string PrettifyEnumName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string body = raw;
            if (body.StartsWith("screen_", System.StringComparison.Ordinal))
                body = body.Substring("screen_".Length);
            if (body.Length == 0) return raw;

            var sb = new StringBuilder(body.Length + 4);
            for (int i = 0; i < body.Length; i++)
            {
                char c = body[i];
                if (i == 0)
                {
                    sb.Append(char.ToUpper(c));
                }
                else if (char.IsUpper(c))
                {
                    sb.Append(' ').Append(char.ToLower(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
