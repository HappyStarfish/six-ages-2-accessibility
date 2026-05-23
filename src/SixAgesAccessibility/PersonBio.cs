using System;
using System.Text;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Builds a screen-reader person dossier — name, age, skills, deity, location
    /// and health — in the active output language.
    ///
    /// <para>The game's own <c>Person.AttributedTextFor</c> assembles this text from
    /// English string literals hardcoded in Assembly-CSharp (skill names, "age",
    /// "Worships:", the location/health words). Those literals never pass through
    /// the German translation mod's PluginImport hooks, so calling
    /// <c>AttributedTextFor</c> directly leaks English even with the translation mod
    /// installed.</para>
    ///
    /// <para>This helper reproduces <c>AttributedTextFor</c>'s logic faithfully but
    /// routes every literal through <see cref="Loc"/>. The dossier therefore follows
    /// the accessibility mod's own language switch: German when the translation mod
    /// is present, otherwise the exact English wording <c>AttributedTextFor</c> would
    /// have produced (every <see cref="Loc"/> key IS that English literal, and
    /// <see cref="Loc.Get"/> returns the key unchanged when nothing is registered).</para>
    ///
    /// <para>Used by every navigator that shows a full person dossier — Reorganize,
    /// ChooseLeader, Caravan, Map foray leader — and by the advisor reader.</para>
    /// </summary>
    internal static class PersonBio
    {
        // aWhat bit flags, mirroring Person.AttributedTextFor.
        private const int BitName     = 0x001;
        private const int BitDeity    = 0x002;
        private const int BitSkills   = 0x004;
        private const int BitAge      = 0x008;
        private const int BitLocation = 0x010;
        private const int BitHealth   = 0x040;
        private const int BitNameRing = 0x200;  // name, plus a ring-position suffix
        private const int BitMojara   = 0x800;  // "Mojara's Mask" artifact holder
        private const int BitDeadOnly = 0x1000; // health branch: announce "Dead" plainly

        /// <summary>
        /// Localized person dossier, a faithful port of <c>Person.AttributedTextFor</c>.
        /// <paramref name="aWhat"/> is the same bit mask the game uses (95 =
        /// name + deity + skills + age + location + health). Returns one
        /// screen-reader-ready sentence; never null, and non-empty whenever the mask
        /// includes the name bit.
        /// </summary>
        public static string Localized(Person person, int aWhat)
        {
            try
            {
                return Build(person, aWhat);
            }
            catch (Exception ex)
            {
                // Person properties are native PluginImport calls — guard the whole
                // build and fall back to the bare name so the user still hears who
                // is focused.
                DebugLogger.Error("PersonBio.Localized", ex);
                try { return person.name; }
                catch { return string.Empty; }
            }
        }

        private static string Build(Person person, int aWhat)
        {
            var text = new StringBuilder();

            // --- Name (bit 1, also implied by the ring-position bit) ---
            if ((aWhat & BitName) != 0 || (aWhat & BitNameRing) != 0)
                text.Append(person.name);

            // --- Age (bit 8) — "Age: 25" when first, ", age 25" mid-sentence ---
            if ((aWhat & BitAge) != 0)
            {
                text.Append(text.Length == 0 ? Loc.Get("Age: ") : Loc.Get(", age "));
                text.Append(person.age);
            }

            // --- Skills (bit 4) — only skills the person is actually rated in ---
            if ((aWhat & BitSkills) != 0)
            {
                for (Skill skill = Skill.skill_Bargaining; skill <= Skill.skill_Magic; skill++)
                {
                    double rating = person.PersonSkill(skill);
                    if (rating <= 1.4) continue;
                    int adj = (int)rating - 1;
                    if (adj < 0) adj = 0;
                    if (adj > 5) adj = 5;
                    AppendNewline(text);
                    text.Append(Loc.Get(SkillNameKey(skill))).Append(": ")
                        .Append(Loc.Get(AdjectiveKey(adj)));
                }
            }

            // --- Deity (bit 2) ---
            if ((aWhat & BitDeity) != 0)
            {
                AppendNewline(text);
                if (person.deity != Deity.deity_Shaman)
                {
                    // "Worships" for the living / married-out / departed, past-tense
                    // "Worshiped" for the dead — same split as the game.
                    bool present = !person.isDead
                        || person.location == PersonLocation.kMarriedOut
                        || person.location == PersonLocation.kDeparted;
                    text.Append(present ? Loc.Get("Worships: ") : Loc.Get("Worshiped: "));
                }
                text.Append(Game.NameOfDeity(person.deity));
            }

            // --- Nickname (always, no bit gate) ---
            string nickname = person.nickname;
            if (!string.IsNullOrEmpty(nickname))
            {
                AppendNewline(text);
                // The game quotes most nicknames but leaves the Berenethtelli
                // honorific bare — mirror that.
                if (nickname.Contains("Berenethtelli"))
                    text.Append(nickname);
                else
                    text.Append('“').Append(nickname).Append('”');
            }

            // --- Mojara's Mask (bit 0x800) ---
            if ((aWhat & BitMojara) != 0 && Game.ClanPersonVariable("majora") == person.index)
            {
                AppendNewline(text);
                text.Append(Loc.Get("Mojara’s Mask"));
            }

            // --- Location (bit 16) ---
            if ((aWhat & BitLocation) != 0)
                AppendLocation(text, person);

            // --- Health (bit 64) ---
            if ((aWhat & BitHealth) != 0)
                AppendHealth(text, person, aWhat);

            // --- Ring-position suffix (bit 0x200) ---
            if ((aWhat & BitNameRing) != 0 && person.ringPosition > 0)
                text.Append(" ®").Append(person.ringPosition);

            // The game separates sections with newlines; speak them as sentences.
            string result = text.ToString().Replace("\n", ". ");
            return string.IsNullOrEmpty(result) ? person.name : result;
        }

        // ---------- Section helpers ----------

        private static void AppendLocation(StringBuilder text, Person person)
        {
            if (!person.isHome)
            {
                AppendNewline(text);
                text.Append(AwayStatus(person.location));
            }
            else if (person.location == PersonLocation.kOccupied)
            {
                AppendNewline(text);
                text.Append(Loc.Get("(Drumming)"));
            }
            else
            {
                // At home, but flagged absent or refusing to advise.
                if (PluginImport.PersonList_SetFrom_Variable("absentAdvisor") > 0
                    && PluginImport.PersonList_Contains(person.index))
                {
                    AppendNewline(text);
                    text.Append(Loc.Get("Absent"));
                }
                if (PluginImport.PersonList_SetFrom_Variable("wontAdvise") > 0
                    && PluginImport.PersonList_Contains(person.index))
                {
                    AppendNewline(text);
                    text.Append(Loc.Get("Sulking"));
                }
            }
        }

        private static void AppendHealth(StringBuilder text, Person person, int aWhat)
        {
            if ((aWhat & BitDeadOnly) != 0 && person.isDead)
            {
                text.Append('\n').Append(Loc.Get("Dead"));
                return;
            }
            PersonLocation loc = person.location;
            if (person.isHealthy
                || loc == PersonLocation.kUnderworld
                || loc == PersonLocation.kEnslaved
                || loc == PersonLocation.kDeparted)
                return;

            AppendNewline(text);
            if (person.isSick)
            {
                // After Humakt's death a badly afflicted person is "Unliving"
                // rather than merely "Sick" — same threshold as the game.
                bool unliving = person.ineligible > 50 && Game.BooleanVariable("humaktDead");
                text.Append(unliving ? Loc.Get("Unliving") : SickWord());
            }
            else if (person.isWounded)
            {
                text.Append(Loc.Get("Wounded"));
            }
        }

        private static string AwayStatus(PersonLocation loc)
        {
            switch (loc)
            {
                case PersonLocation.kUnderworld:      return Loc.Get("Dead");
                case PersonLocation.kMarriedOut:      return Loc.Get("(Married outside the clan)");
                case PersonLocation.kOtherworld:      return Loc.Get("(In the Otherworld)");
                case PersonLocation.kExploring:       return Loc.Get("(Exploring)");
                case PersonLocation.kCapturingHorses: return Loc.Get("(Searching for horses)");
                case PersonLocation.kForaging:        return Loc.Get("(Foraging)");
                case PersonLocation.kSearching:       return Loc.Get("(Searching for spirits)");
                case PersonLocation.kVanished:        return Loc.Get("(Vanished)");
                case PersonLocation.kDeparted:        return Loc.Get("(Departed)");
                default:                             return Loc.Get("(Away from the clan)");
            }
        }

        // ---------- Word/key helpers ----------

        /// <summary>
        /// German for the person-health status "Sick". This one word cannot go
        /// through <see cref="Loc.Get"/>: the key "Sick" is already registered as the
        /// (corpus-confirmed) German delta name "Kranke", a different meaning.
        /// Branching on the active language keeps the English build saying exactly
        /// "Sick" while the German build says "Krank".
        /// </summary>
        private static string SickWord()
        {
            return Loc.GermanActive ? "Krank" : "Sick";
        }

        /// <summary>English skill name; also the <see cref="Loc"/> key for the German value.</summary>
        private static string SkillNameKey(Skill skill)
        {
            switch (skill)
            {
                case Skill.skill_Bargaining: return "Bargaining";
                case Skill.skill_Combat:     return "Combat";
                case Skill.skill_Diplomacy:  return "Diplomacy";
                case Skill.skill_Food:       return "Food";
                case Skill.skill_Leadership: return "Leadership";
                case Skill.skill_Lore:       return "Lore";
                case Skill.skill_Magic:      return "Magic";
                default:                     return "";
            }
        }

        /// <summary>English skill-rating adjective; also the <see cref="Loc"/> key.</summary>
        private static string AdjectiveKey(int adj)
        {
            switch (adj)
            {
                case 0:  return "Fair";
                case 1:  return "Good";
                case 2:  return "Very Good";
                case 3:  return "Excellent";
                case 4:  return "Renowned";
                default: return "Heroic";
            }
        }

        private static void AppendNewline(StringBuilder sb)
        {
            if (sb.Length > 0) sb.Append('\n');
        }
    }
}
