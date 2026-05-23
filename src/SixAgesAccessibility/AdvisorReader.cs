using System;
using System.Collections.Generic;
using System.Text;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Reads advisor advice (F3) and advisor info (Shift+F3) for ring members in scenes
    /// and management screens, plus the expedition leader during expedition mode.
    ///
    /// F3 follows a two-mode interaction:
    ///   - First press: announce all advisors that have advice. Sets cycle index to 0.
    ///   - Subsequent presses: announce one advisor at a time, advancing the cycle index
    ///     and wrapping around at the end.
    /// The cycle index resets when the user presses any non-F3 key (handled by the
    /// keyboard navigation handler via <see cref="ResetCycle"/>).
    ///
    /// Shift+F3 reads the structured Person info (relations, traits, mood etc.) for the
    /// most recently cycled advisor, or all advisors if no cycle is active.
    /// </summary>
    public static class AdvisorReader
    {
        // -1 = no cycle, 0 = just announced all (next F3 reads advisor at index 0),
        // 1..N = next F3 reads advisor at this index.
        private static int _cycleIndex = -1;

        /// <summary>True while the user is in the middle of cycling individual advisors.</summary>
        public static bool IsCycling { get { return _cycleIndex > 0; } }

        /// <summary>True if any cycle state is active (including the "just announced all" sentinel).</summary>
        public static bool HasCycleState { get { return _cycleIndex >= 0; } }

        /// <summary>Reset the cycle so the next F3 starts with an "all advisors" announcement.</summary>
        public static void ResetCycle() { _cycleIndex = -1; }

        /// <summary>
        /// F3 entry point: first press announces all advisors with advice; subsequent presses
        /// cycle through them one at a time with wraparound.
        /// </summary>
        public static void ReadAdviceOrCycle(ScreenController screen)
        {
            if (_cycleIndex < 0)
            {
                ReadAllAdvice(screen);
                _cycleIndex = 0;
            }
            else
            {
                ReadSingleAdvice(screen, _cycleIndex);
            }
        }

        /// <summary>
        /// Shift+F3 entry point: read structured Person info for the currently cycled advisor,
        /// or for all advisors if no cycle is in progress.
        /// </summary>
        public static void ReadInfo(ScreenController screen)
        {
            if (_cycleIndex > 0)
                ReadCurrentInfo(screen);
            else
                ReadAllInfo(screen);
        }

        // --- Advice reading ---

        private static void ReadAllAdvice(ScreenController screen)
        {
            try
            {
                bool isScene = screen is InteractiveController;
                bool isManagement = screen is ManagementController;

                if (!isScene && !isManagement)
                {
                    ScreenReader.Say(Loc.Get("Advisor advice is available during scenes and management."));
                    return;
                }

                var sb = new StringBuilder();
                int advisorCount = 0;

                bool expeditionAdvice = false;
                try { expeditionAdvice = Game.BooleanVariable("expeditionAdvice"); }
                catch (Exception ex) { DebugLogger.Error("AdvisorReader.expeditionAdvice", ex); }

                if (expeditionAdvice && isScene)
                {
                    string advice = PluginImport.Script_AdviceForAdvisor(1);
                    if (!string.IsNullOrEmpty(advice))
                    {
                        int expLeader = 0;
                        try { expLeader = Game.IntegerVariable("expeditionLeader"); }
                        catch (Exception ex) { DebugLogger.Error("AdvisorReader.expeditionLeader", ex); }
                        string name = expLeader > 0 ? PluginImport.PC_PersonName(expLeader) : Loc.Get("Expedition leader");
                        bool highlighted;
                        string spoken = ExtractAdviceHighlight(advice, out highlighted);
                        sb.Append(name).Append(highlighted ? Loc.Get(", highlighted: ") : ": ").Append(spoken);
                        AppendSuggestedResponses(sb, 1);
                        advisorCount = 1;
                    }
                }
                else
                {
                    HashSet<int> absentSet, sulkingSet;
                    CollectAdvisorBlockers(out absentSet, out sulkingSet);

                    for (int pos = 1; pos <= 7; pos++)
                    {
                        int personIndex = PluginImport.PC_RingPerson(pos);
                        if (personIndex <= 0) continue;

                        string name = PluginImport.PC_PersonName(personIndex);
                        if (string.IsNullOrEmpty(name)) continue;

                        string advice = isScene
                            ? PluginImport.Script_AdviceForAdvisor(pos)
                            : PluginImport.PC_AdviceForAdvisor(pos);

                        if (string.IsNullOrEmpty(advice))
                        {
                            // Surface the reason for an empty-advice ring member so F3
                            // doesn't silently skip a chair the user expects to hear from.
                            // "absentAdvisor" (away from the clan) takes priority over
                            // "wontAdvise" (sulking) — same precedence Person.AttributedTextFor
                            // uses in the location/0x10 branch.
                            string blocker = null;
                            if (absentSet.Contains(personIndex)) blocker = Loc.Get("absent this season");
                            else if (sulkingSet.Contains(personIndex)) blocker = Loc.Get("sulking, not advising");
                            if (blocker == null) continue;

                            if (advisorCount > 0) sb.Append(" ... ");
                            sb.Append(name).Append(": ").Append(blocker).Append('.');
                            advisorCount++;
                            continue;
                        }

                        if (advisorCount > 0) sb.Append(" ... ");
                        bool highlighted;
                        string spoken = ExtractAdviceHighlight(advice, out highlighted);
                        sb.Append(name).Append(highlighted ? Loc.Get(", highlighted: ") : ": ").Append(spoken);

                        if (isScene)
                            AppendSuggestedResponses(sb, pos);

                        advisorCount++;
                    }
                }

                ScreenReader.Say(advisorCount == 0 ? Loc.Get("No advisor advice available.") : sb.ToString());
                DebugLogger.Log("AdvisorReader", advisorCount + " advisors read");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("AdvisorReader.ReadAllAdvice", ex);
                ScreenReader.Say(Loc.Get("Could not read advisor advice."));
            }
        }

        private static void ReadSingleAdvice(ScreenController screen, int startIndex)
        {
            try
            {
                bool isScene = screen is InteractiveController;
                bool isManagement = screen is ManagementController;

                if (!isScene && !isManagement)
                {
                    _cycleIndex = -1;
                    ScreenReader.Say(Loc.Get("Advisor advice is available during scenes and management."));
                    return;
                }

                bool expeditionAdvice = false;
                try { expeditionAdvice = Game.BooleanVariable("expeditionAdvice"); }
                catch (Exception ex) { DebugLogger.Error("AdvisorReader.expeditionAdvice", ex); }

                if (expeditionAdvice && isScene)
                {
                    // Expedition has only one advisor — same announcement, no advance needed.
                    string advice = PluginImport.Script_AdviceForAdvisor(1);
                    if (!string.IsNullOrEmpty(advice))
                    {
                        int expLeader = 0;
                        try { expLeader = Game.IntegerVariable("expeditionLeader"); }
                        catch (Exception ex) { DebugLogger.Error("AdvisorReader.expeditionLeader", ex); }
                        string name = expLeader > 0 ? PluginImport.PC_PersonName(expLeader) : Loc.Get("Expedition leader");
                        bool highlighted;
                        string spoken = ExtractAdviceHighlight(advice, out highlighted);
                        var sb = new StringBuilder();
                        sb.Append(name).Append(highlighted ? Loc.Get(", highlighted: ") : ": ").Append(spoken);
                        AppendSuggestedResponses(sb, 1);
                        ScreenReader.Say(sb.ToString());
                    }
                    else
                    {
                        ScreenReader.Say(Loc.Get("No advice available."));
                    }
                    return;
                }

                var positions = new List<int>();
                var names = new List<string>();
                var adviceTexts = new List<string>();

                HashSet<int> absentSet, sulkingSet;
                CollectAdvisorBlockers(out absentSet, out sulkingSet);

                for (int pos = 1; pos <= 7; pos++)
                {
                    int personIndex = PluginImport.PC_RingPerson(pos);
                    if (personIndex <= 0) continue;

                    string pName = PluginImport.PC_PersonName(personIndex);
                    if (string.IsNullOrEmpty(pName)) continue;

                    string advice = isScene
                        ? PluginImport.Script_AdviceForAdvisor(pos)
                        : PluginImport.PC_AdviceForAdvisor(pos);

                    if (string.IsNullOrEmpty(advice))
                    {
                        // Include absent / sulking ring members as cycle stops with a
                        // status note instead of skipping them — so the cycle count
                        // matches "every chair I can think of" rather than "chairs
                        // with non-empty advice this frame".
                        string blocker = null;
                        if (absentSet.Contains(personIndex)) blocker = Loc.Get("absent this season");
                        else if (sulkingSet.Contains(personIndex)) blocker = Loc.Get("sulking, not advising");
                        if (blocker == null) continue;

                        positions.Add(pos);
                        names.Add(pName);
                        adviceTexts.Add(blocker);
                        continue;
                    }

                    positions.Add(pos);
                    names.Add(pName);
                    adviceTexts.Add(advice);
                }

                if (positions.Count == 0)
                {
                    _cycleIndex = -1;
                    ScreenReader.Say(Loc.Get("No advisor advice available."));
                    return;
                }

                if (startIndex >= positions.Count) startIndex = 0;

                bool singleHighlighted;
                string singleSpoken = ExtractAdviceHighlight(adviceTexts[startIndex], out singleHighlighted);

                var output = new StringBuilder();
                output.Append(startIndex + 1).Append(Loc.Get(" of ")).Append(positions.Count).Append(": ");
                output.Append(names[startIndex])
                    .Append(singleHighlighted ? Loc.Get(", highlighted: ") : ": ")
                    .Append(singleSpoken);

                if (isScene)
                    AppendSuggestedResponses(output, positions[startIndex]);

                ScreenReader.Say(output.ToString());

                _cycleIndex = startIndex + 1;
                if (_cycleIndex >= positions.Count) _cycleIndex = 0;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("AdvisorReader.ReadSingleAdvice", ex);
                ScreenReader.Say(Loc.Get("Could not read advisor advice."));
                _cycleIndex = -1;
            }
        }

        // Snapshot the two "no advice this turn" person lists so we can explain
        // each empty-advice ring chair instead of silently skipping it. The
        // PluginImport person buffer is shared and gets reset on every
        // PersonList_SetFrom_Variable call (see PersonDataList.RefreshIfNeeded
        // in the decompiled source), so we MUST collect each list's person
        // indices into our own HashSet before issuing the next FromVariable —
        // accessing entry.personIndex after the second call would read from
        // the wrong buffer.
        private static void CollectAdvisorBlockers(out HashSet<int> absent, out HashSet<int> sulking)
        {
            absent = new HashSet<int>();
            sulking = new HashSet<int>();
            try
            {
                PersonDataList absentList = PersonDataList.FromVariable("absentAdvisor");
                for (int i = 0; i < absentList.count; i++)
                    absent.Add(absentList[i].personIndex);

                PersonDataList sulkingList = PersonDataList.FromVariable("wontAdvise");
                for (int i = 0; i < sulkingList.count; i++)
                    sulking.Add(sulkingList[i].personIndex);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("AdvisorReader.CollectAdvisorBlockers", ex);
            }
        }

        // Six Ages encodes advisor highlights with paired asterisks (*important*) in the
        // raw advice string. Sighted players see those segments rendered bold by
        // StringExtension.BoldTagString → <b>...</b>. We strip the markers for speech and
        // report whether any segment was highlighted so callers can prefix with "highlighted:".
        private static string ExtractAdviceHighlight(string raw, out bool highlighted)
        {
            highlighted = false;
            if (string.IsNullOrEmpty(raw)) return raw;
            if (raw.IndexOf('*') < 0) return raw;

            // Pair-count: an odd count means a stray * we'd rather keep verbatim than
            // misinterpret. (BoldTagString tolerates that, so do we.)
            int asteriskCount = 0;
            for (int i = 0; i < raw.Length; i++) if (raw[i] == '*') asteriskCount++;
            if (asteriskCount < 2 || (asteriskCount % 2) != 0) return raw;

            highlighted = true;
            return raw.Replace("*", string.Empty);
        }

        private static void AppendSuggestedResponses(StringBuilder sb, int position)
        {
            try
            {
                int bitmask = PluginImport.Script_SuggestionsForAdvisor(position);
                if (bitmask == 0) return;

                int responseCount = PluginImport.Script_ResponseCount();
                if (responseCount <= 0) return;

                sb.Append(Loc.Get(" Suggests: "));
                bool first = true;
                for (int i = 0; i < responseCount; i++)
                {
                    if ((bitmask & (1 << i)) != 0)
                    {
                        string respText = PluginImport.Script_ResponseText(i + 1);
                        if (!string.IsNullOrEmpty(respText))
                        {
                            if (!first) sb.Append(", ");
                            sb.Append(i + 1).Append(") ").Append(respText);
                            first = false;
                        }
                    }
                }
                sb.Append(".");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("AdvisorReader.Suggestions", ex);
            }
        }

        // --- Info reading (Shift+F3) ---

        private static void ReadAllInfo(ScreenController screen)
        {
            try
            {
                bool isScene = screen is InteractiveController;
                bool isManagement = screen is ManagementController;

                if (!isScene && !isManagement)
                {
                    ScreenReader.Say(Loc.Get("Advisor info is available during scenes and management."));
                    return;
                }

                bool expeditionAdvice = false;
                try { expeditionAdvice = Game.BooleanVariable("expeditionAdvice"); }
                catch (Exception ex) { DebugLogger.Error("AdvisorReader.expeditionAdvice", ex); }

                var sb = new StringBuilder();
                int count = 0;

                if (expeditionAdvice)
                {
                    int expLeader = 0;
                    try { expLeader = Game.IntegerVariable("expeditionLeader"); }
                    catch (Exception ex) { DebugLogger.Error("AdvisorReader.expeditionLeader", ex); }
                    if (expLeader > 0)
                    {
                        Person person = PlayerClan.PersonWithIndex(expLeader);
                        if (person != null)
                        {
                            // 111 = 110 (expedition bitmask) | 1 (name)
                            string info = FormatPersonInfo(person, 111);
                            if (!string.IsNullOrEmpty(info))
                            {
                                sb.Append(info);
                                count = 1;
                            }
                        }
                    }
                }
                else
                {
                    for (int pos = 1; pos <= 7; pos++)
                    {
                        int personIndex = PluginImport.PC_RingPerson(pos);
                        if (personIndex <= 0) continue;

                        Person person = PlayerClan.PersonWithIndex(personIndex);
                        if (person == null) continue;

                        // 127 = 126 (normal bitmask) | 1 (name)
                        string info = FormatPersonInfo(person, 127);
                        if (string.IsNullOrEmpty(info)) continue;

                        if (count > 0) sb.Append(" ... ");
                        sb.Append(info);
                        count++;
                    }
                }

                ScreenReader.Say(count == 0 ? Loc.Get("No advisor info available.") : sb.ToString());
                DebugLogger.Log("AdvisorReader", count + " advisor infos read");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("AdvisorReader.ReadAllInfo", ex);
                ScreenReader.Say(Loc.Get("Could not read advisor info."));
            }
        }

        private static void ReadCurrentInfo(ScreenController screen)
        {
            try
            {
                bool isScene = screen is InteractiveController;
                bool isManagement = screen is ManagementController;

                if (!isScene && !isManagement)
                {
                    ScreenReader.Say(Loc.Get("Advisor info is available during scenes and management."));
                    return;
                }

                bool expeditionAdvice = false;
                try { expeditionAdvice = Game.BooleanVariable("expeditionAdvice"); }
                catch (Exception ex) { DebugLogger.Error("AdvisorReader.expeditionAdvice", ex); }

                if (expeditionAdvice && isScene)
                {
                    int expLeader = 0;
                    try { expLeader = Game.IntegerVariable("expeditionLeader"); }
                    catch (Exception ex) { DebugLogger.Error("AdvisorReader.expeditionLeader", ex); }
                    if (expLeader > 0)
                    {
                        Person person = PlayerClan.PersonWithIndex(expLeader);
                        if (person != null)
                        {
                            ScreenReader.Say(FormatPersonInfo(person, 111));
                            DebugLogger.Log("AdvisorReader", "Read info for " + person.name);
                            return;
                        }
                    }
                    ScreenReader.Say(Loc.Get("No advisor info available."));
                    return;
                }

                // Rebuild positions list matching ReadSingleAdvice (advisors with advice)
                var personIndices = new List<int>();

                for (int pos = 1; pos <= 7; pos++)
                {
                    int personIndex = PluginImport.PC_RingPerson(pos);
                    if (personIndex <= 0) continue;

                    string pName = PluginImport.PC_PersonName(personIndex);
                    if (string.IsNullOrEmpty(pName)) continue;

                    string advice = isScene
                        ? PluginImport.Script_AdviceForAdvisor(pos)
                        : PluginImport.PC_AdviceForAdvisor(pos);

                    if (string.IsNullOrEmpty(advice)) continue;

                    personIndices.Add(personIndex);
                }

                // _cycleIndex points to NEXT advisor, so last read is at index - 1
                int lastIndex = _cycleIndex - 1;
                if (lastIndex < 0 || lastIndex >= personIndices.Count)
                {
                    ScreenReader.Say(Loc.Get("No advisor info available."));
                    return;
                }

                Person p = PlayerClan.PersonWithIndex(personIndices[lastIndex]);
                if (p != null)
                {
                    ScreenReader.Say(FormatPersonInfo(p, 127));
                    DebugLogger.Log("AdvisorReader", "Read info for " + p.name);
                }
                else
                {
                    ScreenReader.Say(Loc.Get("No advisor info available."));
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("AdvisorReader.ReadCurrentInfo", ex);
                ScreenReader.Say(Loc.Get("Could not read advisor info."));
            }
        }

        private static string FormatPersonInfo(Person person, int bitmask)
        {
            // PersonBio is a localized port of the game's English-only
            // Person.AttributedTextFor — see PersonBio. It already returns a clean,
            // tag-free, sentence-joined string in the active output language.
            return PersonBio.Localized(person, bitmask);
        }
    }
}
