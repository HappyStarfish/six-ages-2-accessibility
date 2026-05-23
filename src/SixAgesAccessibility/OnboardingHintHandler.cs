using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Drives the subtle on-boarding mini-tutorial: it teaches a blind, keyboard-only
    /// player the mod's controls beside the game's own tutorial cards.
    ///
    /// <para><b>How it attaches.</b> The game shows floating tutorial hint cards
    /// (<see cref="TutorialView"/>) and full-screen tutorial views. The S44 plan was to
    /// key the curriculum on the Lua card <c>id</c> via <c>PluginImport.TutorialView_Name()</c>,
    /// but in-game verification (Session 45, 2026-05-23) showed that native returns the
    /// optional Lua <c>name</c> trigger field — set on only a handful of cards (e.g.
    /// <c>"TutorialBottom"</c>, <c>"Replayable"</c>) and <c>null</c> on most. The Lua
    /// <c>id</c> is therefore unreachable from native; we identify cards by the first-page
    /// text instead, fingerprinting against both the English original and the German
    /// corpus translation so the curriculum works regardless of which language the
    /// translator mod presents to the screen reader.</para>
    ///
    /// <para>The postfix in <c>MenuPatches.TutorialCard_Shown</c> passes the full
    /// tutorial-card text (already joined across pages by
    /// <see cref="TutorialHintHandler.BuildFullHintText"/>) to <see cref="GetCardHint"/>.
    /// We walk an ordered list of <i>(prefix, concept)</i> pairs and return the hint
    /// for the first concept whose prefix the text starts with — core content first,
    /// hint last (see memory <c>feedback_no_repeated_hints</c>).</para>
    ///
    /// <para><b>Fires once.</b> Persistence is per <i>concept</i>, not per card: several
    /// cards share a concept, so e.g. the season hint is spoken exactly once no matter
    /// which season-related card the player meets first. Seen concepts are stored one-
    /// per-line in <c>onboarding-progress.txt</c> next to the plugin DLL — a plain text
    /// file, deliberately not JSON (Mono 2.0 safe).</para>
    ///
    /// <para><b>Welcome.</b> A separate one-time welcome announcement (the <c>welcome</c>
    /// concept) is delivered from the main-menu postfix the first time the menu appears,
    /// so a player with no documentation is not stuck in a chicken-and-egg trap.</para>
    ///
    /// <para><b>Reset.</b> <see cref="Reset"/> deletes the progress file and clears the
    /// in-memory set, so the game's own "Reset Tutorial" button (postfixed in
    /// <c>MenuPatches</c>) resets both the game tutorial and this mini-tutorial at once.</para>
    ///
    /// <para><b>Wording note.</b> The hint sentences below describe key bindings. They
    /// are an additional place those bindings are written down — when a binding changes,
    /// this curriculum must be checked alongside the other key-hint sources (see memory
    /// <c>feedback_keyhints_live_in_other_files</c> and <see cref="Hints"/>). The wording
    /// was approved by the user on 2026-05-22 (docs/mini-tutorial-spec.md, section 8).</para>
    /// </summary>
    public class OnboardingHintHandler
    {
        // ---- Curriculum concept keys (also the lines written to the progress file) ----

        private const string WelcomeConcept = "welcome";
        private const string ConceptHintCards = "hintcards";
        private const string ConceptDecisions = "decisions";
        private const string ConceptLeader = "leader";
        private const string ConceptClanRing = "clanring";
        private const string ConceptScreenSwitch = "screenswitch";
        private const string ConceptHelp = "help";
        private const string ConceptManagement = "management";
        private const string ConceptSeason = "season";
        private const string ConceptAdvisor = "advisor";
        private const string ConceptMagic = "magic";
        private const string ConceptSaves = "saves";

        private const string ProgressFileName = "onboarding-progress.txt";

        private static OnboardingHintHandler _instance;

        /// <summary>Lazily-created process-wide instance.</summary>
        public static OnboardingHintHandler Instance
        {
            get
            {
                if (_instance == null) _instance = new OnboardingHintHandler();
                return _instance;
            }
        }

        /// <summary>
        /// Ordered list of (text prefix, concept) pairs. The first prefix the tutorial
        /// card text starts with wins. Each curriculum card contributes two prefixes —
        /// one in English (game default) and one in German (when the translator mod is
        /// active). Both come from the actual game data: English from
        /// <c>Content\TutorialTips-DarkAge.lua</c>, German from
        /// <c>tools\extract-corpus\corpus_de.json</c>. Built once by
        /// <see cref="BuildCurriculum"/>; see <c>docs\mini-tutorial-spec.md</c> §6.
        /// </summary>
        private readonly List<KeyValuePair<string, string>> _prefixToConcept;

        /// <summary>Curriculum concept key → English hint sentence (also the <see cref="Loc"/> key).</summary>
        private readonly Dictionary<string, string> _conceptToHint;

        /// <summary>Concepts already delivered — loaded from disk, written through on every add.</summary>
        private readonly HashSet<string> _seen = new HashSet<string>();

        /// <summary>Absolute path of the progress file, or null if persistence is unavailable.</summary>
        private string _progressFilePath;

        /// <summary>True once <see cref="EnsureLoaded"/> has run (success or failure).</summary>
        private bool _loaded;

        private OnboardingHintHandler()
        {
            _prefixToConcept = BuildCurriculum();
            _conceptToHint = BuildHintText();
        }

        /// <summary>
        /// Returns the localized one-time welcome announcement the very first time it is
        /// asked for, then null forever after (until <see cref="Reset"/>). Call this from
        /// the main-menu postfix.
        /// </summary>
        /// <returns>The welcome text to speak, or null if it has already been delivered.</returns>
        public string ConsumeWelcome()
        {
            try
            {
                EnsureLoaded();
                if (_seen.Contains(WelcomeConcept))
                    return null;
                MarkSeen(WelcomeConcept);
                DebugLogger.Log("Onboarding", "Welcome announcement delivered (first run).");
                return Loc.Get("Welcome to Six Ages 2. This mod makes the game playable "
                    + "with a keyboard and screen reader. Use the arrow keys to move through "
                    + "the display, and Enter to activate. Shift F1 always tells you the keys "
                    + "for the screen you are on. These hints appear only once.");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("OnboardingHintHandler.ConsumeWelcome", ex);
                return null;
            }
        }

        /// <summary>
        /// Given the full text of the tutorial card now on screen (all pages joined),
        /// returns the localized onboarding hint to append to the card announcement —
        /// or null when the card is not part of the curriculum, or its concept's hint
        /// has already been delivered.
        ///
        /// Identification is by text prefix because the native plugin exposes no
        /// reachable Lua-<c>id</c> accessor: <c>TutorialView_Name()</c> returns the
        /// optional Lua <c>name</c> trigger field, which is null on most cards. See
        /// the class summary and <c>docs/mini-tutorial-spec.md</c> §7.
        ///
        /// Has a side effect: the first time a concept's hint is returned, that concept
        /// is marked seen and persisted, so the hint is never spoken twice via the
        /// auto-announce on <c>TutorialView.Init</c>. Use <see cref="PeekCardHint"/> for
        /// a non-consuming lookup (H key repeat).
        /// </summary>
        /// <param name="cardText">The full card text (page 1 first).</param>
        public string GetCardHint(string cardText)
        {
            try
            {
                EnsureLoaded();
                string concept = LookupConcept(cardText);
                if (concept == null) return null;
                if (_seen.Contains(concept))
                {
                    DebugLogger.Log("Onboarding", "Tutorial concept '" + concept
                        + "' — already delivered.");
                    return null;
                }
                string text = LocalizedHintFor(concept);
                if (text == null) return null;
                MarkSeen(concept);
                DebugLogger.Log("Onboarding", "Tutorial concept '" + concept
                    + "' — hint delivered.");
                return text;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("OnboardingHintHandler.GetCardHint", ex);
                return null;
            }
        }

        /// <summary>
        /// Like <see cref="GetCardHint"/>, but never consults or updates the
        /// <c>seen</c> set — always returns the hint as long as the card text matches a
        /// curriculum prefix. Used by the H key (manual hint repeat) so the user always
        /// hears the keyboard hint along with the card text, even after the auto-
        /// announce on first display already consumed the concept.
        /// </summary>
        /// <param name="cardText">The full card text (page 1 first).</param>
        public string PeekCardHint(string cardText)
        {
            try
            {
                EnsureLoaded();
                string concept = LookupConcept(cardText);
                if (concept == null) return null;
                return LocalizedHintFor(concept);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("OnboardingHintHandler.PeekCardHint", ex);
                return null;
            }
        }

        /// <summary>
        /// Resolves the card text to a curriculum concept key, or null when the card is
        /// not in the curriculum. Logs the first 60 chars of any unmatched card so a
        /// playtest with F12 on doubles as a curriculum-coverage check.
        /// </summary>
        private string LookupConcept(string cardText)
        {
            if (string.IsNullOrEmpty(cardText))
            {
                DebugLogger.Log("Onboarding", "Tutorial card text empty — not in curriculum.");
                return null;
            }
            // Contains, not StartsWith: the English Lua source uses a curly U+2019
            // apostrophe in several cards' first sentence (e.g. "You’ll be guiding…").
            // The runtime returns that codepoint intact (the corpus's [Hit/Cache] hits
            // prove it) but writing it in source-level fingerprints is fragile across
            // editor encodings. Picking substrings that sit after any apostrophe and
            // matching via Contains keeps the curriculum readable and still uniquely
            // identifies each card — the tutorial card text set is small and curated.
            //
            // Trim() (parameterless) NOT TrimStart() — TrimStart()/TrimEnd() bind to
            // the params char[] overload, which on Mono 2.0 calls Array.Empty<char>()
            // and crashes at first use with MissingMethodException. Trim() is a
            // dedicated method, safe. See memory feedback_mono_compat.
            string norm = cardText.Trim();
            foreach (KeyValuePair<string, string> entry in _prefixToConcept)
            {
                if (norm.IndexOf(entry.Key, StringComparison.Ordinal) >= 0)
                    return entry.Value;
            }
            int cut = norm.Length < 60 ? norm.Length : 60;
            DebugLogger.Log("Onboarding", "Tutorial card text not in curriculum: '"
                + norm.Substring(0, cut) + (norm.Length > cut ? "..." : "") + "'");
            return null;
        }

        /// <summary>Returns the localized hint sentence for a concept, or null when
        /// no hint text was registered for it (curriculum-data defect — logged).</summary>
        private string LocalizedHintFor(string concept)
        {
            string english;
            if (!_conceptToHint.TryGetValue(concept, out english))
            {
                DebugLogger.Warn("Onboarding", "Concept '" + concept + "' has no hint text — skipped.");
                return null;
            }
            return Loc.Get(english);
        }

        /// <summary>
        /// Forgets every delivered concept and deletes the progress file, so the welcome
        /// and all curriculum hints will appear again. Called from the postfix on the
        /// game's own "Reset Tutorial" button.
        /// </summary>
        public void Reset()
        {
            try
            {
                EnsureLoaded(); // resolves _progressFilePath even on a fresh process
                _seen.Clear();
                if (!string.IsNullOrEmpty(_progressFilePath) && File.Exists(_progressFilePath))
                {
                    File.Delete(_progressFilePath);
                    DebugLogger.Log("Onboarding", "Progress file deleted — mini-tutorial reset.");
                }
                else
                {
                    DebugLogger.Log("Onboarding", "Mini-tutorial reset (no progress file present).");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("OnboardingHintHandler.Reset", ex);
            }
        }

        // ---- internals ----

        /// <summary>
        /// Reads the progress file into <see cref="_seen"/> on first use. Marks itself done
        /// even on failure, so a broken path does not retry file IO on every call; the
        /// handler then still works in-memory for the rest of the session.
        /// </summary>
        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                _progressFilePath = Path.Combine(dir, ProgressFileName);
                if (File.Exists(_progressFilePath))
                {
                    string[] lines = File.ReadAllLines(_progressFilePath);
                    foreach (string line in lines)
                    {
                        string concept = (line ?? "").Trim();
                        if (concept.Length > 0) _seen.Add(concept);
                    }
                    DebugLogger.Log("Onboarding", "Loaded " + _seen.Count
                        + " seen concept(s) from " + _progressFilePath);
                }
                else
                {
                    DebugLogger.Log("Onboarding", "No progress file yet — first run. Path: "
                        + _progressFilePath);
                }
            }
            catch (Exception ex)
            {
                // Persistence disabled for this session; in-memory _seen still works.
                _progressFilePath = null;
                DebugLogger.Error("OnboardingHintHandler.EnsureLoaded", ex);
            }
        }

        /// <summary>Adds a concept to the in-memory set and appends it to the progress file.</summary>
        private void MarkSeen(string concept)
        {
            _seen.Add(concept);
            if (string.IsNullOrEmpty(_progressFilePath)) return;
            try
            {
                File.AppendAllText(_progressFilePath, concept + Environment.NewLine);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("OnboardingHintHandler.MarkSeen", ex);
            }
        }

        /// <summary>
        /// Builds the (text-prefix → concept) curriculum. Each card from the original
        /// id-based map (<c>docs/mini-tutorial-spec.md</c> §6) is now represented by its
        /// English first-page text (from the Lua tutorial file) and its German first-page
        /// text (from the translation corpus). Both prefixes are checked at runtime so
        /// the mini-tutorial works whether the player runs the German translator mod or
        /// the untranslated game.
        ///
        /// Prefixes deliberately stop short of any curly apostrophe or other punctuation
        /// that has historically varied between Lua source and rendered text, so the
        /// match is robust against minor text re-encoding.
        /// </summary>
        private static List<KeyValuePair<string, string>> BuildCurriculum()
        {
            var list = new List<KeyValuePair<string, string>>();

            // Stage 1 — operating hint cards (b1, b5, i5, s2, g7)
            Add(list, ConceptHintCards, "Welcome to Six Ages! These tips");
            Add(list, ConceptHintCards, "Willkommen zu Six Ages! Diese Tipps");
            Add(list, ConceptHintCards, "You can drag guide boxes out of the way");
            Add(list, ConceptHintCards, "Hinweisfenster lassen sich beiseiteziehen");
            Add(list, ConceptHintCards, "Click the picture to hide the text.");
            Add(list, ConceptHintCards, "Klickt das Bild an, um den Text zu verbergen.");
            Add(list, ConceptHintCards, "Clicking the picture hides any text covering it");
            Add(list, ConceptHintCards, "Ein Klick auf das Bild blendet darüberliegenden");

            // Stage 2 — decisions / response options (q1, q2, i10, i10continued, i12)
            Add(list, ConceptDecisions, "You can change any answer");
            Add(list, ConceptDecisions, "Ihr könnt jede Antwort");
            Add(list, ConceptDecisions, "You can change this and subsequent answers");
            Add(list, ConceptDecisions, "Ihr könnt diese und folgende Antworten");
            // q1/q2 first sentence is identical: "You<curly-apostrophe>ll be guiding the
            // destiny of a small clan." Match the apostrophe-free tail so the source
            // file stays plain ASCII.
            Add(list, ConceptDecisions, "be guiding the destiny of a small clan");
            Add(list, ConceptDecisions, "Ihr lenkt das Schicksal eines kleinen Klans");

            // Stage 3a — choosing a leader (u1)
            Add(list, ConceptLeader, "Pick a leader, then click");
            Add(list, ConceptLeader, "Wählt einen Anführer aus und klickt dann");

            // Stage 3b — clan ring (rd1, rd2, rd3)
            Add(list, ConceptClanRing, "Choosing a clan ring is a balancing act");
            Add(list, ConceptClanRing, "Einen Klanring zu wählen ist ein Balanceakt");
            Add(list, ConceptClanRing, "Clicking the small picture selects a leader");
            Add(list, ConceptClanRing, "Ein Klick auf das kleine Bild wählt einen Anführer");
            Add(list, ConceptClanRing, "To fill a position, use one of the wide checkboxes");
            Add(list, ConceptClanRing, "Um einen Platz zu besetzen, nutzt eines der breiten");

            // Stage 4 — switching management screens (intro7, u4)
            Add(list, ConceptScreenSwitch, "Use the *Menu* button to switch between");
            Add(list, ConceptScreenSwitch, "Mit dem *Menu*-Knopf wechselt ihr zwischen");
            Add(list, ConceptScreenSwitch, "The Menu will warn you of problems");
            Add(list, ConceptScreenSwitch, "Das Menü warnt euch vor Problemen");

            // Stage 5 — help (u5)
            Add(list, ConceptHelp, "To get more information about any screen, click the *?*");
            Add(list, ConceptHelp, "Mehr Auskunft zu einem Bildschirm bekommt ihr per Klick auf *?*");

            // Stage 6 — management screens (one hint, on the first one visited) —
            // intro3, m4, ma1, re1, wa1, we1, lo1, sa1, st1
            Add(list, ConceptManagement, "This screen shows the size and mood of your clan");
            Add(list, ConceptManagement, "Dieser Bildschirm zeigt die Größe und Stimmung eures Klans");
            Add(list, ConceptManagement, "You have several kinds of magic at your disposal");
            Add(list, ConceptManagement, "Euch stehen mehrere Arten von Magie zur Verfügung");
            Add(list, ConceptManagement, "From here you can send out expeditions to explore");
            Add(list, ConceptManagement, "Von hier sendet ihr Expeditionen aus");
            Add(list, ConceptManagement, "Emissaries can help our friends remember us");
            Add(list, ConceptManagement, "Gesandte können unseren Freunden helfen");
            Add(list, ConceptManagement, "Here you will send your clan to war");
            Add(list, ConceptManagement, "Hier zieht ihr mit eurem Klan in den Krieg");
            Add(list, ConceptManagement, "This screen lets you review your clan");
            Add(list, ConceptManagement, "Auf diesem Bildschirm seht ihr die weltlichen Schätze");
            Add(list, ConceptManagement, "The Lore screen contains stories about the Gods War");
            Add(list, ConceptManagement, "Der Lore-Bildschirm enthält Geschichten über den Götterkrieg");
            Add(list, ConceptManagement, "The Saga tracks your clan");
            Add(list, ConceptManagement, "Die Saga verzeichnet die wichtigen Begebenheiten");
            Add(list, ConceptManagement, "The two-week Sacred Time precedes each new year");
            Add(list, ConceptManagement, "Die zweiwöchige Heilige Zeit geht jedem neuen Jahr voraus");

            // Stage 7a — season (intro5, u3, u7)
            // intro5+u3 share the first sentence: "The *Season* button advances time, …"
            Add(list, ConceptSeason, "The *Season* button advances time");
            Add(list, ConceptSeason, "Der *Season*-Knopf trägt die Zeit weiter");
            Add(list, ConceptSeason, "An action (such as sacrificing");
            Add(list, ConceptSeason, "Ein Spielzug (etwa opfern");

            // Stage 7b — advisors (s1)
            Add(list, ConceptAdvisor, "Click an advisor to see their take on the situation");
            Add(list, ConceptAdvisor, "Klickt einen Berater an, um seine Sicht der Lage");

            // Stage 7c — magic (intro6, u6)
            Add(list, ConceptMagic, "You can show the clan");
            Add(list, ConceptMagic, "Die göttliche Magie des Klans");

            // Stage 7d — saved games (b2)
            Add(list, ConceptSaves, "Manage saved games by clicking Edit");
            Add(list, ConceptSaves, "Verwaltet Spielstände durch Klick auf *Edit*");

            // Stage 7e (hq5 → quester) intentionally NOT wired — see spec §10 #7.

            return list;
        }

        /// <summary>
        /// Builds the concept → English hint sentence map. Each English string is also the
        /// <see cref="Loc"/> lookup key; the German values live in <c>LocSetup.cs</c>.
        /// </summary>
        private static Dictionary<string, string> BuildHintText()
        {
            var map = new Dictionary<string, string>();
            map[ConceptHintCards] = "The game sometimes shows hint cards. The mod reads "
                + "them aloud automatically. Press H to hear the current hint again.";
            map[ConceptDecisions] = "In a decision, move through the response options with "
                + "the arrow keys. Enter selects the current response.";
            map[ConceptLeader] = "To choose a person for a task, move through the candidates "
                + "with the arrow keys. Space selects the person, Enter confirms. D reads "
                + "their life story.";
            map[ConceptClanRing] = "In the clan ring, Tab switches between the list of "
                + "people and the ring. In the list, Space puts a person into the ring or "
                + "removes them, C makes them chieftain. Enter applies the new arrangement.";
            map[ConceptScreenSwitch] = "Switch between the management screens with Ctrl and "
                + "a number from 1 to 9. Ctrl with Tab moves through them in order.";
            map[ConceptHelp] = "Open a screen's help with F1 and move through it section by "
                + "section with the arrow keys. Shift F1 lists the mod's keys.";
            map[ConceptManagement] = "Management screens show figures, lists and buttons. "
                + "Tab switches between the areas. Shift F1 lists the keys this screen has.";
            map[ConceptSeason] = "Advance time with the S key — that is the game's Season "
                + "button. An action costs half a season.";
            map[ConceptAdvisor] = "Press F3 to hear your advisors' take on a scene. Each "
                + "advisor also reads out the responses they consider sensible — that "
                + "can be none, one, several or all of them, depending on the advisor "
                + "and the situation. Shift F3 gives you information about the advisor.";
            map[ConceptMagic] = "You hear your clan's magic on the Magic screen. F4 also "
                + "steps through the concerns and the active magic.";
            map[ConceptSaves] = "Delete a save in the saved-games list with the Delete key — "
                + "pressing it twice within three seconds confirms.";
            return map;
        }

        /// <summary>Appends one (prefix, concept) pair to the curriculum list.</summary>
        private static void Add(List<KeyValuePair<string, string>> list, string concept, string prefix)
        {
            list.Add(new KeyValuePair<string, string>(prefix, concept));
        }
    }
}
