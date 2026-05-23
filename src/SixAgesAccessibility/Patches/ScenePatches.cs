using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SixAgesAccessibility.Patches
{
    /// <summary>Harmony patches for scene/event text and choice announcements.</summary>
    [HarmonyPatch]
    public static class ScenePatches
    {
        // --- Tutorial awareness ---

        /// <summary>Check if a tutorial screen or hint is currently blocking.</summary>
        private static bool IsTutorialActive()
        {
            try
            {
                // Full-screen tutorial dialog
                if (Tutorial.isTrainer) return true;

                // Floating tutorial hint card
                TutorialView tv = TutorialView.instance;
                if (tv != null && tv.gameObject.activeSelf)
                    return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.IsTutorialActive", ex);
            }
            return false;
        }

        // --- Scene / News / Intro / Quest initialization ---
        // All controllers override InitializeFromScript — must patch each one.

        // Set while inside InitializeFromScript / SceneContinues. Both call AddResponses()
        // internally, which fires AddResponses_Postfix and would queue options BEFORE the
        // outer postfix gets to announce the scene text. In tutorial mode the scene text
        // is queued (interrupt:false), so the options would speak first. We suppress the
        // inner announcement here and let the outer postfix order it correctly.
        private static bool _suppressAddResponsesAnnounce;

        // Last scene announcement (header + caption + text) and its response list,
        // plus the frame they were built on. When a full-screen tutorial covers a
        // scene the same frame it initialised, the scene text is suppressed and
        // delivered later — see DeferSceneUntilTutorialDismissed.
        private static string _lastSceneAnnounce;
        private static string _lastResponsesAnnounce;
        private static int _lastSceneAnnounceFrame = -100;

        // True while a scene's text is waiting to be announced because a full-screen
        // tutorial covered it. Delivered by TryDeliverPendingSceneAnnounce once the
        // tutorial is dismissed and the user is back on the scene.
        private static bool _sceneAnnouncePending;

        // Did the previous Update tick already see a scene as the active screen? The
        // delivery waits for two consecutive ticks so a one-frame transition flicker
        // (active screen briefly the scene mid-frame) cannot deliver early.
        private static bool _sceneActiveLastTick;

        /// <summary>Frame on which the most recent scene text was announced.</summary>
        public static int LastSceneAnnounceFrame { get { return _lastSceneAnnounceFrame; } }

        /// <summary>
        /// Mark the most recent scene's text + options to be announced later — once the
        /// covering tutorial is dismissed. Called by the tutorial announce path instead
        /// of replaying the scene right away: the scene text (and its choices) only
        /// makes sense when the tutorial is gone, and replaying it immediately would
        /// just read it twice.
        /// </summary>
        public static void DeferSceneUntilTutorialDismissed()
        {
            _sceneAnnouncePending = true;
            // A stale frame number must never re-arm this for an unrelated tutorial.
            _lastSceneAnnounceFrame = -100;
        }

        /// <summary>
        /// Deliver a deferred scene announcement once the user is back on the scene —
        /// i.e. the covering tutorial has been dismissed and the active screen is an
        /// InteractiveController again. Called once per frame from
        /// KeyboardNavigationHandler.Update. Requires the scene to be active for two
        /// consecutive ticks so a mid-frame screen-transition flicker cannot trigger it.
        /// </summary>
        public static void TryDeliverPendingSceneAnnounce()
        {
            try
            {
                if (!_sceneAnnouncePending)
                {
                    _sceneActiveLastTick = false;
                    return;
                }

                var sm = Singleton<ScreenManager>.instance;
                bool sceneActive = sm != null && sm.activeScreen is InteractiveController;

                if (sceneActive && _sceneActiveLastTick)
                {
                    _sceneAnnouncePending = false;
                    _sceneActiveLastTick = false;
                    if (!string.IsNullOrEmpty(_lastSceneAnnounce))
                        ScreenReader.Say(_lastSceneAnnounce, interrupt: false);
                    if (!string.IsNullOrEmpty(_lastResponsesAnnounce))
                        ScreenReader.Say(_lastResponsesAnnounce, interrupt: false);
                    DebugLogger.Log("ScenePatches", "Delivered deferred scene text after tutorial dismissed");
                }
                else
                {
                    _sceneActiveLastTick = sceneActive;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.TryDeliverPending", ex);
            }
        }

        /// <summary>Shared logic: read scene text + responses after any InitializeFromScript.</summary>
        private static void AnnounceSceneInit(string source, ScreenController instance)
        {
            try
            {
                DebugLogger.Log("ScenePatches", source + " InitializeFromScript fired");
                _inPostChoiceState = false;
                _isSceneFinalized = false;
                // A fresh scene announces itself — any earlier deferred scene is moot.
                _sceneAnnouncePending = false;
                // New scene — drop any cached delta line so F5 doesn't echo
                // resource changes from the previous scene.
                _lastDeltaAnnouncement = null;

                // Tell the keyboard nav to drop its cached ResponseButton list — the
                // old buttons are being destroyed and replaced with a fresh set.
                KeyboardNavigationHandler.RequestRefresh();

                bool tutActive = IsTutorialActive();
                DebugLogger.Log("ScenePatches", "Tutorial active: " + tutActive);

                // GameManager.ShowScene runs InitializeFromScript() BEFORE Show() activates
                // the new controller — so by the time onScreenChanged fires the scene text
                // would already be queued and the listener-emitted phase header would land
                // last instead of first. Pull the header now and prepend it to the scene
                // output so the user hears "Story phase. Scene screen. <caption>. <text>"
                // in the right order. ScreenChangePatches.GetPhaseHeaderIfNew also updates
                // its last-announced state, so the listener will then skip its own emit.
                string header = ScreenChangePatches.GetPhaseHeaderIfNew(instance);
                if (header != null)
                    DebugLogger.Log("ScenePatches", "Prepending phase header: " + header);

                string caption = PluginImport.Script_Caption();
                string text = PluginImport.Script_Text();

                // News and battle-result events show a speaker portrait with the
                // person's name (NewsController.SetupSpeaker -> Script_Speaker).
                // Regular story scenes (SceneController) have no speaker element at
                // all, so this stays null for them — announcing it here would claim
                // a name the game itself never shows.
                string speakerName = (instance is NewsController)
                    ? GetSceneSpeakerName() : null;

                string output = "";
                if (header != null)
                    output += header + " ";
                if (!string.IsNullOrEmpty(caption))
                    output += caption + ". ";
                if (!string.IsNullOrEmpty(speakerName))
                    output += Loc.Get("Speaker: ") + speakerName + ". ";
                if (!string.IsNullOrEmpty(text))
                    output += text;

                DebugLogger.Log("ScenePatches", source + " text length=" + output.Length);

                // Stash for the tutorial re-queue path (see ReQueueLastSceneAfterTutorial):
                // if a full-screen tutorial covers this scene the same frame, it will
                // interrupt and then re-queue this text so the order is tutorial-first.
                _lastSceneAnnounce = output;
                _lastSceneAnnounceFrame = Time.frameCount;

                if (!string.IsNullOrEmpty(output))
                {
                    if (tutActive)
                        ScreenReader.Say(output, interrupt: false);
                    else
                        ScreenReader.Say(output);
                }

                AnnounceCurrentResponses(interrupt: false);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches." + source, ex);
            }
        }

        [HarmonyPatch(typeof(SceneController), "InitializeFromScript")]
        [HarmonyPrefix]
        public static void SceneInitialized_Prefix() { _suppressAddResponsesAnnounce = true; }

        [HarmonyPatch(typeof(SceneController), "InitializeFromScript")]
        [HarmonyPostfix]
        public static void SceneInitialized_Postfix(SceneController __instance)
        {
            _suppressAddResponsesAnnounce = false;
            AnnounceSceneInit("Scene", __instance);
        }

        [HarmonyPatch(typeof(NewsController), "InitializeFromScript")]
        [HarmonyPrefix]
        public static void NewsInitialized_Prefix() { _suppressAddResponsesAnnounce = true; }

        [HarmonyPatch(typeof(NewsController), "InitializeFromScript")]
        [HarmonyPostfix]
        public static void NewsInitialized_Postfix(NewsController __instance)
        {
            _suppressAddResponsesAnnounce = false;
            AnnounceSceneInit("News", __instance);
        }

        [HarmonyPatch(typeof(IntroController), "InitializeFromScript")]
        [HarmonyPrefix]
        public static void IntroInitialized_Prefix() { _suppressAddResponsesAnnounce = true; }

        [HarmonyPatch(typeof(IntroController), "InitializeFromScript")]
        [HarmonyPostfix]
        public static void IntroInitialized_Postfix(IntroController __instance)
        {
            _suppressAddResponsesAnnounce = false;
            AnnounceSceneInit("Intro", __instance);
        }

        [HarmonyPatch(typeof(QuestController), "InitializeFromScript")]
        [HarmonyPrefix]
        public static void QuestInitialized_Prefix() { _suppressAddResponsesAnnounce = true; }

        [HarmonyPatch(typeof(QuestController), "InitializeFromScript")]
        [HarmonyPostfix]
        public static void QuestInitialized_Postfix(QuestController __instance)
        {
            _suppressAddResponsesAnnounce = false;
            AnnounceSceneInit("Quest", __instance);
        }

        [HarmonyPatch(typeof(BattleController), "InitializeFromScript")]
        [HarmonyPrefix]
        public static void BattleInitialized_Prefix() { _suppressAddResponsesAnnounce = true; }

        [HarmonyPatch(typeof(BattleController), "InitializeFromScript")]
        [HarmonyPostfix]
        public static void BattleInitialized_Postfix(BattleController __instance)
        {
            _suppressAddResponsesAnnounce = false;
            AnnounceSceneInit("Battle", __instance);
        }

        [HarmonyPatch(typeof(BattleResultsController), "InitializeFromScript")]
        [HarmonyPrefix]
        public static void BattleResultsInitialized_Prefix() { _suppressAddResponsesAnnounce = true; }

        [HarmonyPatch(typeof(BattleResultsController), "InitializeFromScript")]
        [HarmonyPostfix]
        public static void BattleResultsInitialized_Postfix(BattleResultsController __instance)
        {
            _suppressAddResponsesAnnounce = false;
            AnnounceSceneInit("BattleResults", __instance);
            // Append the casualty table after the narrative text. The controller wires its
            // labels to integer game variables (eliteWeKilled, regularKilled, etc.) which
            // hold the final per-side counts at this point — read them directly. Cattle
            // raids hide the table on screen and the reader skips it accordingly.
            try { CombatScreenReader.AnnounceBattleResults(__instance); }
            catch (Exception ex) { DebugLogger.Error("ScenePatches.BattleResults", ex); }
        }

        // --- Response selection feedback ---

        // Captured in prefix before Script_DoResponse advances the script
        private static string _lastChosenResponseText;
        private static int _lastChosenResponseNumber;
        private static bool _inPostChoiceState;

        // True only while we are inside a DoResponseNumber call. Used by
        // ShowDeltas_Postfix to know whether to announce the resource changes
        // immediately or stash them, so that SceneFinal/SceneContinues (which
        // fire inside the same call and use interrupt:true) don't cut the
        // queued delta line off. Cleared in the postfix.
        private static bool _inDoResponseNumberCall;
        private static string _pendingDeltaAnnouncement;

        // True between SceneFinal_Postfix and the next InitializeFromScript / SceneContinues.
        // In this state the scene is finished — only a Proceed/Done button remains and the
        // textPanel body has been augmented with the result text (so Script_Text() returns
        // question+result concatenated). The keyboard nav's question-header slot reads this
        // flag to switch from "echo the original question" to "read only the result", which
        // is the only content the user hasn't already heard at that point.
        private static bool _isSceneFinalized;
        public static bool IsSceneFinalized { get { return _isSceneFinalized; } }

        /// <summary>Capture response text BEFORE the method clears it and advances the script.</summary>
        [HarmonyPatch(typeof(InteractiveController), "DoResponseNumber")]
        [HarmonyPrefix]
        public static void DoResponseNumber_Prefix(int response, InteractiveController __instance)
        {
            try
            {
                ResponseButton btn = __instance.textPanel.GetResponseButton(response);
                _lastChosenResponseText = btn != null ? btn.text : null;
                _lastChosenResponseNumber = response + 1;
                _inPostChoiceState = true;
                _inDoResponseNumberCall = true;
                _pendingDeltaAnnouncement = null;
                DebugLogger.Log("ScenePatches", "DoResponseNumber prefix: " + _lastChosenResponseNumber + " = " + _lastChosenResponseText);
            }
            catch (Exception ex)
            {
                _lastChosenResponseText = null;
                DebugLogger.Error("ScenePatches.DoResponseNumber_Pre", ex);
            }
        }

        /// <summary>
        /// Reset the per-call state. The choice itself isn't re-announced here —
        /// the user just pressed Enter on it, so re-speaking "X chosen." is noise.
        /// The result text is announced by whichever transition fired inside
        /// this call (SceneFinal_Postfix or SceneContinues_Postfix).
        /// </summary>
        [HarmonyPatch(typeof(InteractiveController), "DoResponseNumber")]
        [HarmonyPostfix]
        public static void DoResponseNumber_Postfix()
        {
            try
            {
                // Safety net: if ShowDeltas fired but neither SceneFinal nor
                // SceneContinues flushed the pending delta line (unusual but
                // possible for scripts that don't transition), flush it now so
                // the user still hears the resource changes.
                FlushPendingDeltaAnnouncement();
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.DoResponseNumber", ex);
            }
            finally
            {
                _inDoResponseNumberCall = false;
            }
        }

        /// <summary>
        /// Speak whatever ShowDeltas_Postfix stashed during this DoResponseNumber call.
        /// Called by SceneFinal_Postfix and SceneContinues_Postfix after their own
        /// result text, and as a safety net by DoResponseNumber_Postfix.
        /// </summary>
        private static void FlushPendingDeltaAnnouncement()
        {
            if (string.IsNullOrEmpty(_pendingDeltaAnnouncement)) return;
            ScreenReader.Say(_pendingDeltaAnnouncement, interrupt: false);
            DebugLogger.Log("ScenePatches", "ShowDeltas flushed pending delta line");
            _pendingDeltaAnnouncement = null;
        }

        // --- Scene continues (new text + new responses after a choice) ---

        [HarmonyPatch(typeof(InteractiveController), "SceneContinues")]
        [HarmonyPrefix]
        public static void SceneContinues_Prefix() { _suppressAddResponsesAnnounce = true; }

        /// <summary>Announce continuation text and new responses.</summary>
        [HarmonyPatch(typeof(InteractiveController), "SceneContinues")]
        [HarmonyPostfix]
        public static void SceneContinues_Postfix()
        {
            _suppressAddResponsesAnnounce = false;
            try
            {
                DebugLogger.Log("ScenePatches", "SceneContinues fired");
                _isSceneFinalized = false;

                // SceneContinues replaces the responseButtons in-place on the same
                // controller; signal the keyboard nav to recollect.
                KeyboardNavigationHandler.RequestRefresh();

                string resultText = PluginImport.Script_ResultText();
                if (!string.IsNullOrEmpty(resultText))
                    ScreenReader.Say(resultText, interrupt: false);

                // Flush resource changes after the result text so they're spoken
                // in narrative order (story → resource summary).
                FlushPendingDeltaAnnouncement();

                AnnounceCurrentResponses(interrupt: false);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.SceneContinues", ex);
            }
        }

        // --- NewChoice: announces different choice types ---

        /// <summary>Announce when a new choice type appears (list, slider, yes/no, etc.).</summary>
        [HarmonyPatch(typeof(InteractiveController), "NewChoice")]
        [HarmonyPostfix]
        public static void NewChoice_Postfix(InteractiveController __instance)
        {
            try
            {
                string resultText = PluginImport.Script_ResultText();
                string caption = PluginImport.Script_Caption();
                ScriptChoice choice = (ScriptChoice)PluginImport.Script_WhatChoice();

                DebugLogger.Log("ScenePatches", $"NewChoice type={choice}, caption={caption}");

                switch (choice)
                {
                    case ScriptChoice.kChooseYesNo:
                        string yesNoText = !string.IsNullOrEmpty(caption) ? caption : resultText;
                        if (!string.IsNullOrEmpty(yesNoText))
                            ScreenReader.Say(yesNoText + Loc.Get(" Yes or No. Press Y or N."));
                        // NewChoice ran AddYesNo(), which destroyed the previous choice's
                        // ResponseButtons and instantiated a fresh "Yes"/"No" pair. The
                        // keyboard nav still caches the old (now destroyed) buttons; force a
                        // re-collect so Up/Down can reach the new ones. Without this the user
                        // is stuck on the question header — and the periodic soft refresh
                        // can't save it when the button count is unchanged (yes/no → yes/no
                        // is 2 → 0 → 2). Mirrors the slider cases below.
                        KeyboardNavigationHandler.RequestRefresh();
                        break;

                    case ScriptChoice.kChooseOptions:
                        break;

                    case ScriptChoice.kChooseClan:
                    case ScriptChoice.kChooseClans:
                        AnnounceListChoice(caption, "clan");
                        KeyboardNavigationHandler.RequestEnterSceneList();
                        break;

                    case ScriptChoice.kChooseDeity:
                    case ScriptChoice.kChooseDeityList:
                    case ScriptChoice.kChooseDeityIncludingChaos:
                        AnnounceListChoice(caption, "deity");
                        KeyboardNavigationHandler.RequestEnterSceneList();
                        break;

                    case ScriptChoice.kChooseSpirit:
                        AnnounceListChoice(caption, "spirit");
                        KeyboardNavigationHandler.RequestEnterSceneList();
                        break;

                    case ScriptChoice.kChooseTreasure:
                    case ScriptChoice.kChooseTreasures:
                        AnnounceListChoice(caption, "treasure");
                        KeyboardNavigationHandler.RequestEnterSceneList();
                        break;

                    case ScriptChoice.kChooseLeader:
                        // ChooseLeaderDialog opens as a separate screen; its ShowFor postfix
                        // (DialogPatches.ChooseLeader_Shown → DialogContentReader.ReadChooseLeader)
                        // already speaks the caption plus candidate count plus shortcut hints.
                        // Echoing the caption here would duplicate that announcement and — worse —
                        // interrupts the dialog's own line because NewChoice fires after ShowFor.
                        // Stay silent and let the dialog own the announcement.
                        break;

                    case ScriptChoice.kChooseGoods:
                    case ScriptChoice.kChooseHerds:
                    case ScriptChoice.kChooseFood:
                    case ScriptChoice.kChooseHorses:
                    case ScriptChoice.kChooseWarriors:
                    case ScriptChoice.kChooseNumber:
                    case ScriptChoice.kChooseSacrifice:
                    case ScriptChoice.kChooseMilitary:
                    case ScriptChoice.kChooseShortcall:
                    case ScriptChoice.kChooseEscort:
                    case ScriptChoice.kChooseWealth:
                    case ScriptChoice.kChooseWealthAndHorses:
                    case ScriptChoice.kChooseTribute:
                        AnnounceSliderChoice(__instance, caption);
                        // NewChoice runs after TextPanel.AddSlider has populated
                        // textPanel.sliders, but the keyboard handler's _sliders cache
                        // was filled at screenChanged time when no sliders existed yet.
                        // Force a refresh so the next Update tick rebuilds _sliders and
                        // Up/Down can cycle into them — without this, the user is stuck
                        // on the question header.
                        KeyboardNavigationHandler.RequestRefresh();
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.NewChoice", ex);
            }
        }

        // --- Yes/No feedback ---

        /// <summary>
        /// Yes/No answer hook — diagnostic logging only. The choice is deliberately
        /// NOT re-announced, mirroring DoResponseNumber_Postfix above: the user just
        /// pressed Y/N (or Enter on the focused Yes/No button, having already heard
        /// it), so "Yes/No chosen." is noise. It also cannot be spoken cleanly from
        /// here — this postfix runs only after DoYesNo's nested ContinueScene →
        /// Script_Restart has already announced the next question/options/result, so
        /// the line landed out of order and interrupted that follow-up content. The
        /// follow-up (next question, options, or result text) is itself the
        /// confirmation that the answer registered.
        /// </summary>
        [HarmonyPatch(typeof(InteractiveController), "DoYesNo")]
        [HarmonyPostfix]
        public static void DoYesNo_Postfix(int r)
        {
            DebugLogger.Log("ScenePatches", $"DoYesNo({r}) fired");
        }

        // --- Scene final (done button appears) ---

        /// <summary>Announce result text when scene reaches its final state.</summary>
        [HarmonyPatch(typeof(InteractiveController), "SceneFinal")]
        [HarmonyPostfix]
        public static void SceneFinal_Postfix(string buttonLabel)
        {
            try
            {
                DebugLogger.Log("ScenePatches", "SceneFinal(" + buttonLabel + ") fired");
                _isSceneFinalized = true;

                string resultText = PluginImport.Script_ResultText();
                if (!string.IsNullOrEmpty(resultText))
                    ScreenReader.Say(resultText, interrupt: false);

                // Flush resource changes after the result text so they're spoken
                // in narrative order (story → resource summary).
                FlushPendingDeltaAnnouncement();

                if (!string.IsNullOrEmpty(buttonLabel))
                    ScreenReader.Say(Loc.Get(buttonLabel) + Loc.Get(" button, Enter."), interrupt: false);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.SceneFinal", ex);
            }
        }

        // --- Leader chosen feedback ---

        /// <summary>Announce which leader was chosen.</summary>
        [HarmonyPatch(typeof(InteractiveController), "LeaderChosen")]
        [HarmonyPostfix]
        public static void LeaderChosen_Postfix(int index)
        {
            try
            {
                DebugLogger.Log("ScenePatches", $"LeaderChosen({index}) fired");
                string name = PlayerClan.PersonWithIndex(index).name;
                ScreenReader.Say(name + Loc.Get(" chosen as leader."));
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.LeaderChosen", ex);
            }
        }

        // --- AddResponses: announce response count ---

        /// <summary>Announce when new responses appear.</summary>
        [HarmonyPatch(typeof(InteractiveController), "AddResponses")]
        [HarmonyPostfix]
        public static void AddResponses_Postfix()
        {
            try
            {
                DebugLogger.Log("ScenePatches", "AddResponses fired (suppressed=" + _suppressAddResponsesAnnounce + ")");
                // InitializeFromScript / SceneContinues call AddResponses internally and
                // run their own ordered announcement (text first, then responses) in the
                // postfix. Skip here to avoid speaking options before the question.
                if (_suppressAddResponsesAnnounce) return;

                // The game replaced the responseButtons collection in-place on the same
                // controller. KeyboardNav still holds the destroyed old buttons - signal a
                // refresh so the next Update tick re-collects, otherwise Up/Down get stuck
                // bouncing between the deltas header and an empty nav order.
                // (Observed: user picked "Slaughter livestock", two new "Enough for ..."
                // options appeared, but KeyboardNav cycled endlessly to "deltas header".)
                KeyboardNavigationHandler.RequestRefresh();

                AnnounceCurrentResponses(interrupt: false);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.AddResponses", ex);
            }
        }

        // --- DeltaText: announce resource changes ---

        /// <summary>Announce delta/change text (resource gains/losses).</summary>
        [HarmonyPatch(typeof(InteractiveController), "DeltaText", new Type[] { typeof(string) })]
        [HarmonyPostfix]
        public static void DeltaText_Postfix(string text)
        {
            try
            {
                DebugLogger.Log("ScenePatches", $"DeltaText fired: {text}");
                if (!string.IsNullOrEmpty(text))
                    ScreenReader.Say(text, interrupt: false);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.DeltaText", ex);
            }
        }

        // Buffer that collects resource-delta label texts during a single
        // ShowDeltas pass. Filled by DeltaLabelFor_Postfix (one entry per label
        // the game creates), drained by ShowDeltas_Postfix. The prefix clears
        // it so a previous unflushed pass can't leak into the next scene.
        //
        // Why a buffer at all: the game's ShowDeltas ends with SaveOriginalValues,
        // which sets `deltas = null` and `otherDeltas = null` before our postfix
        // can read them. Capturing at DeltaLabelFor (called once per resource,
        // returns the UILabel with the formatted text already set) sidesteps
        // that timing entirely — we read the texts as they are produced.
        private static readonly System.Collections.Generic.List<string> _deltaTexts =
            new System.Collections.Generic.List<string>();

        // The most recent finished "Resource changes: ..." announcement, kept
        // around so the F5 RepeatSceneText hotkey can re-read it alongside
        // caption/text/result AND so the KeyboardNav can expose it as a
        // dedicated navigable header slot above the response options.
        // Cleared when a new scene initializes so we don't echo a previous
        // scene's deltas.
        private static string _lastDeltaAnnouncement;
        public static string LastDeltaAnnouncement { get { return _lastDeltaAnnouncement; } }
        public static void ClearLastDeltaAnnouncement() { _lastDeltaAnnouncement = null; }

        /// <summary>Clear the per-pass delta buffer before the game populates it.</summary>
        [HarmonyPatch(typeof(InteractiveController), "ShowDeltas", new Type[] { typeof(ShowDeltaOptions) })]
        [HarmonyPrefix]
        public static void ShowDeltas_Prefix()
        {
            _deltaTexts.Clear();
        }

        /// <summary>
        /// Capture the formatted text of each delta label as the game builds it.
        /// DeltaLabelFor is private and called from two sites inside ShowDeltas:
        /// once per numeric delta (AddDelta → DeltaLabelFor) and once per
        /// otherDelta string (ShowDeltas → DeltaLabelFor with magic:true).
        /// Both paths flow through here, so a single hook covers everything
        /// a sighted player sees animate over the scene.
        /// </summary>
        [HarmonyPatch(typeof(InteractiveController), "DeltaLabelFor")]
        [HarmonyPostfix]
        public static void DeltaLabelFor_Postfix(UILabel __result)
        {
            try
            {
                if (__result == null) return;
                string text = __result.text;
                if (string.IsNullOrEmpty(text)) return;
                _deltaTexts.Add(text);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.DeltaLabelFor", ex);
            }
        }

        /// <summary>
        /// Announce the resource changes that ShowDeltas just rendered.
        /// Sighted players see them animate over the scene; without this
        /// hook blind players miss the per-resource gain/loss values that
        /// are NOT spelled out in the news text (e.g. caravan profits, raid
        /// casualties, sacrifice costs, magic-aura horse changes).
        /// </summary>
        [HarmonyPatch(typeof(InteractiveController), "ShowDeltas", new Type[] { typeof(ShowDeltaOptions) })]
        [HarmonyPostfix]
        public static void ShowDeltas_Postfix()
        {
            try
            {
                if (_deltaTexts.Count == 0)
                {
                    // No labels created this pass — normal for scenes that
                    // encode amounts in the story text rather than as floating
                    // numbers. Log so the user can tell "patch ran but had
                    // nothing to say" from "patch never ran".
                    DebugLogger.Log("ScenePatches", "ShowDeltas postfix — no deltas captured");
                    return;
                }

                var sb = new System.Text.StringBuilder();
                sb.Append(Loc.Get("Resource changes: "));
                int written = 0;
                for (int i = 0; i < _deltaTexts.Count; i++)
                {
                    string raw = _deltaTexts[i];
                    if (string.IsNullOrEmpty(raw)) continue;
                    string spoken = FormatDeltaText(raw);
                    if (string.IsNullOrEmpty(spoken)) continue;
                    if (written > 0) sb.Append(", ");
                    sb.Append(spoken);
                    written++;
                }
                _deltaTexts.Clear();
                if (written == 0) return;
                sb.Append(".");
                string line = sb.ToString();

                // Cache for F5 repeat regardless of whether we speak it now
                // or defer it — the user should be able to re-read either way.
                _lastDeltaAnnouncement = line;

                if (_inDoResponseNumberCall)
                {
                    // SceneFinal / SceneContinues will fire later in this call
                    // with interrupt:true and would cut this off if we spoke
                    // now. Stash and let those postfixes flush it after their
                    // own result text.
                    _pendingDeltaAnnouncement = line;
                    DebugLogger.Log("ScenePatches", "ShowDeltas deferred " + written + " deltas");
                }
                else
                {
                    ScreenReader.Say(line, interrupt: false);
                    DebugLogger.Log("ScenePatches", "ShowDeltas announced " + written + " deltas");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.ShowDeltas", ex);
                _deltaTexts.Clear();
            }
        }

        /// <summary>
        /// Turn a raw delta-label text into a screen-reader-friendly phrase.
        /// Three shapes from the game (see InteractiveController.DeltaLabelFor):
        /// "Goods +27" / "Cattle –12" → "Goods plus 27" / "Cattle minus 12";
        /// "Mood +" / "Mood –" (vague, no number) → "Mood up" / "Mood down";
        /// "Horses 3" (magic / otherDeltas) → unchanged, just stripped.
        /// </summary>
        private static string FormatDeltaText(string raw)
        {
            // Trim spaces only — parameterless Trim() is forbidden on Mono 2.0
            // (pulls in Array.Empty<char>); see CLAUDE.md Mono compat rules.
            string clean = StringHelpers.StripTags(raw).Trim(' ');
            if (clean.Length == 0) return null;

            // Vague positive: trailing " +" with nothing after.
            if (clean.EndsWith(" +"))
            {
                string resource = clean.Substring(0, clean.Length - 2).Trim(' ');
                return Loc.Get(resource) + Loc.Get(" up") + VagueAbsoluteSuffix(resource);
            }
            // Vague negative: trailing " –" (en-dash) or " -" (hyphen).
            if (clean.EndsWith(" –") || clean.EndsWith(" -"))
            {
                string resource = clean.Substring(0, clean.Length - 2).Trim(' ');
                return Loc.Get(resource) + Loc.Get(" down") + VagueAbsoluteSuffix(resource);
            }

            // Numeric "<resource> <sign><number>": localize the resource word and
            // the sign glyph, keep the number. Split on the last space so a
            // two-word resource ("Wyter strength") stays intact.
            int sp = clean.LastIndexOf(' ');
            if (sp > 0 && sp < clean.Length - 1)
            {
                string amt = clean.Substring(sp + 1)
                    .Replace("+", Loc.Get("plus "))
                    .Replace("–", Loc.Get("minus "))
                    .Replace("-", Loc.Get("minus "));
                return Loc.Get(clean.Substring(0, sp).Trim()) + " " + amt;
            }
            // No space: substitute the sign glyphs with words only.
            return clean
                .Replace("+", Loc.Get("plus "))
                .Replace("–", Loc.Get("minus "))
                .Replace("-", Loc.Get("minus "));
        }

        // Last absolute Mood/Food state announced as a vague-delta suffix
        // ("now worried"). The suffix is voiced only when this state differs
        // from the one already announced — a "Mood up" that stays inside the
        // same descriptor band must not repeat an unchanged status. Null until
        // the first vague delta of that resource establishes a baseline; kept
        // across scenes because the clan's Mood/Food state itself persists.
        private static string _lastMoodSuffixState;
        private static string _lastFoodSuffixState;

        /// <summary>
        /// For the two "vague" delta resources — Food and Mood — the game shows only
        /// a direction ("Food +", no number; InteractiveController.deltaInfo flags
        /// them "vague"). The plugin still exposes the resulting absolute state as
        /// text (PC_FoodText / PC_MoodText — the same strings the Clan and Wealth
        /// screens display), so append it: the user hears "Mood up, now content"
        /// instead of a bare direction.
        /// <para>The state is only appended when it actually changed since the last
        /// time it was announced for this resource — otherwise a vague "Mood up"
        /// that stays inside the same descriptor band would repeat "now worried"
        /// on every event, which carries no information. The stored baseline still
        /// advances so a later genuine change is still caught.</para>
        /// Returns "" for every other resource, when the state is unchanged, and on
        /// any failure, leaving the plain "up"/"down" phrasing intact.
        /// </summary>
        private static string VagueAbsoluteSuffix(string resource)
        {
            try
            {
                string state;
                if (resource == "Mood")
                    state = PluginImport.PC_MoodText();
                else if (resource == "Food")
                    state = PluginImport.PC_FoodText();
                else
                    return "";
                if (string.IsNullOrEmpty(state)) return "";
                state = state.Trim(' ');

                // Suppress the suffix when the resulting state matches the one
                // the user was last told for this resource; advance the baseline
                // otherwise so the next genuine change is announced.
                if (resource == "Mood")
                {
                    if (state == _lastMoodSuffixState) return "";
                    _lastMoodSuffixState = state;
                }
                else
                {
                    if (state == _lastFoodSuffixState) return "";
                    _lastFoodSuffixState = state;
                }

                return ", " + Loc.Get("now ") + state;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.VagueAbsoluteSuffix", ex);
                return "";
            }
        }

        // --- Public helpers for hotkey repeat ---

        /// <summary>Re-read the current scene/question text. Called by F5 hotkey.</summary>
        public static void RepeatSceneText()
        {
            try
            {
                string caption = PluginImport.Script_Caption();
                string text = PluginImport.Script_Text();
                string resultText = PluginImport.Script_ResultText();

                DebugLogger.Log("ScenePatches", "RepeatSceneText: caption=" + (caption != null ? caption.Length.ToString() : "0") + ", text=" + (text != null ? text.Length.ToString() : "0"));

                string output = "";
                if (!string.IsNullOrEmpty(caption))
                    output += caption + ". ";
                // Mirror the initial announcement: include the News/BattleResults
                // speaker so F5 re-reads the same content.
                string speakerName = GetActiveSceneSpeakerName();
                if (!string.IsNullOrEmpty(speakerName))
                    output += Loc.Get("Speaker: ") + speakerName + ". ";
                if (!string.IsNullOrEmpty(text))
                    output += text + " ";
                if (!string.IsNullOrEmpty(resultText))
                    output += resultText + " ";
                if (!string.IsNullOrEmpty(_lastDeltaAnnouncement))
                    output += _lastDeltaAnnouncement;

                if (!string.IsNullOrEmpty(output.Trim(' ')))
                    ScreenReader.Say(output.Trim(' '));
                else
                    ScreenReader.Say("No scene text.");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.RepeatSceneText", ex);
            }
        }

        /// <summary>Re-read the current response options or last choice. Called by F6 hotkey.</summary>
        public static void RepeatResponses()
        {
            RepeatResponses(interrupt: true);
        }

        /// <summary>Re-read the current response options or last choice.</summary>
        public static void RepeatResponses(bool interrupt)
        {
            try
            {
                // After choosing a response, show last chosen (not stale old options)
                if (_inPostChoiceState && !string.IsNullOrEmpty(_lastChosenResponseText))
                {
                    ScreenReader.Say("Last chosen: " + _lastChosenResponseNumber + ", " + _lastChosenResponseText + ".", interrupt);
                    return;
                }

                int count = PluginImport.Script_ResponseCount();
                string proceedLabel = GetActiveProceedButtonLabel();

                // If there are current options or a proceed button, announce them
                if (count > 0 || !string.IsNullOrEmpty(proceedLabel))
                {
                    AnnounceCurrentResponses(interrupt);
                    return;
                }

                // No current options and no last choice
                if (!string.IsNullOrEmpty(_lastChosenResponseText))
                {
                    ScreenReader.Say("Last chosen: " + _lastChosenResponseNumber + ", " + _lastChosenResponseText + ".", interrupt);
                    return;
                }

                ScreenReader.Say("No options available.", interrupt);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.RepeatResponses", ex);
            }
        }

        // --- Internal helpers ---

        private static void AnnounceCurrentResponses(bool interrupt)
        {
            int count = PluginImport.Script_ResponseCount();
            DebugLogger.Log("ScenePatches", $"ResponseCount={count}");

            string output = "";

            if (count > 0)
            {
                // Tutorials disable some responses via TextPanel.SetResponses
                // (Tutorial.topicConstrainResponses → responseButton.interactable = false).
                // Walk the live ResponseButtons so locked options can be flagged with
                // "locked:" while keeping the full count audible.
                TextPanel tp = GetActiveTextPanel();
                output = count + Loc.Get(" options: ");
                for (int i = 1; i <= count; i++)
                {
                    string respText = PluginImport.Script_ResponseText(i);
                    bool locked = tp != null && IsResponseLocked(tp, i - 1);
                    if (locked)
                        output += i + ", locked: " + respText + ". ";
                    else
                        output += i + ", " + respText + ". ";
                }
            }

            // Also check for Proceed/Done button on the active InteractiveController
            string proceedLabel = GetActiveProceedButtonLabel();
            if (!string.IsNullOrEmpty(proceedLabel))
            {
                output += Loc.Get(proceedLabel) + Loc.Get(" button, Enter. ");
            }

            // Intro @nameentry/@restore screen has extra controls beyond the recap responses:
            // a clan name input, restore-mode toggle group, and a Start button. Without
            // this hint the user only hears the precondition recap and may not realise the
            // game won't advance until they press Start (or that the name field even exists).
            string introExtras = GetIntroExtraControlsHint();
            if (!string.IsNullOrEmpty(introExtras))
            {
                output += introExtras;
            }

            // Stash the response list so the tutorial re-queue path can replay it
            // together with the scene text (see ReQueueLastSceneAfterTutorial).
            _lastResponsesAnnounce = output;

            if (!string.IsNullOrEmpty(output))
                ScreenReader.Say(output.TrimEnd(' '), interrupt);
        }

        /// <summary>
        /// Hint for the @nameentry intro setup screen — names the controls that aren't
        /// part of the response list (clan name field, restore toggles, Start button)
        /// so screen-reader users discover them without trial-and-error tabbing. Gated
        /// on <c>nameEntryContainer.activeSelf</c>: only this specific screen surfaces
        /// these controls, so non-intro scenes return null and are unaffected.
        ///
        /// Detection of Start cannot use <c>GetComponent&lt;Button&gt;()</c> — the
        /// startButton's interactable component is a <c>UIButton</c> (Selectable
        /// subclass, not Button), so that lookup returns null. We rely on the
        /// container gate instead and assume Start is present.
        /// </summary>
        private static string GetIntroExtraControlsHint()
        {
            try
            {
                TextPanel tp = GetActiveTextPanel();
                if (tp == null || tp.nameEntryContainer == null
                    || !tp.nameEntryContainer.activeSelf)
                    return null;

                var parts = new List<string>();
                parts.Add(Loc.Get("a clan name field"));
                if (tp.restoresContainer != null && tp.restoresContainer.activeSelf)
                    parts.Add(Loc.Get("restore mode toggles None, One, Unlimited"));
                if (tp.startButton != null && tp.startButton.gameObject.activeSelf)
                    parts.Add(Loc.Get("a Start button"));

                string list = string.Join(", ", parts.ToArray());
                return Loc.Get("Also on this screen: ") + list
                    + Loc.Get(". Use Tab to reach them; press Enter on Start to begin. ");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.IntroExtras", ex);
                return null;
            }
        }

        /// <summary>Get the active InteractiveController's TextPanel, or null.</summary>
        private static TextPanel GetActiveTextPanel()
        {
            try
            {
                var sm = Singleton<ScreenManager>.instance;
                if (sm == null) return null;
                InteractiveController ic = sm.activeScreen as InteractiveController;
                if (ic == null) return null;
                return ic.textPanel;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.GetActiveTextPanel", ex);
                return null;
            }
        }

        /// <summary>
        /// Name of the person shown in the current News/BattleResults speaker
        /// portrait, or null. The game stores the speaker as a person index via
        /// Script_Speaker(); a negative value means "no speaker" (PersonCard treats
        /// -1 as a cleared card). For any real index the display name is resolved
        /// through PC_PersonName — the same lookup the game's own PersonCard uses.
        /// </summary>
        private static string GetSceneSpeakerName()
        {
            try
            {
                int speaker = PluginImport.Script_Speaker();
                if (speaker < 0) return null;
                string name = PluginImport.PC_PersonName(speaker);
                return string.IsNullOrEmpty(name) ? null : name;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.GetSceneSpeakerName", ex);
                return null;
            }
        }

        /// <summary>
        /// Speaker name for the F5 repeat path: only a News/BattleResults screen
        /// shows a speaker, so gate on the active screen type before reading
        /// Script_Speaker() (a regular SceneController scene must not get a name).
        /// </summary>
        private static string GetActiveSceneSpeakerName()
        {
            try
            {
                var sm = Singleton<ScreenManager>.instance;
                if (sm == null || !(sm.activeScreen is NewsController)) return null;
                return GetSceneSpeakerName();
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.GetActiveSceneSpeakerName", ex);
                return null;
            }
        }

        /// <summary>True if the ResponseButton with the given 0-based index exists and has been disabled by the tutorial.</summary>
        private static bool IsResponseLocked(TextPanel tp, int responseIndex)
        {
            try
            {
                if (tp == null || tp.responseButtons == null) return false;
                foreach (var rb in tp.responseButtons)
                {
                    if (rb == null) continue;
                    if (rb.index != responseIndex) continue;
                    // ResponseButton.interactable wraps Button.interactable. We must
                    // also confirm a Button component exists — the property accessor
                    // throws NRE otherwise (seen earlier on dialog buttons).
                    var btn = rb.GetComponent<UnityEngine.UI.Button>();
                    if (btn == null) return false;
                    return !btn.interactable;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.IsResponseLocked", ex);
            }
            return false;
        }

        /// <summary>Get the label of the active Proceed/Done button if visible.</summary>
        private static string GetActiveProceedButtonLabel()
        {
            try
            {
                var sm = Singleton<ScreenManager>.instance;
                if (sm == null) return null;

                ScreenController screen = sm.activeScreen;
                InteractiveController ic = screen as InteractiveController;
                if (ic == null) return null;

                if (ic.textPanel != null
                    && ic.textPanel.proceedButton != null
                    && ic.textPanel.proceedButton.gameObject.activeSelf
                    && ic.textPanel.proceedButton.interactable)
                {
                    string label = ic.textPanel.proceedButton.text;
                    if (!string.IsNullOrEmpty(label))
                        return label;
                    // Fallback to label text
                    if (ic.textPanel.proceedButton.label != null)
                        return ic.textPanel.proceedButton.label.text;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.GetProceedButton", ex);
            }
            return null;
        }

        private static void AnnounceListChoice(string caption, string listType)
        {
            string text = !string.IsNullOrEmpty(caption) ? caption : Loc.Get("Choose a ") + listType + ".";
            ScreenReader.Say(text + Loc.Get(" Use arrow keys to navigate the list, Enter to select, then Tab and Enter for Proceed."));
        }

        private static void AnnounceSliderChoice(InteractiveController instance, string caption)
        {
            string text = !string.IsNullOrEmpty(caption) ? caption : Loc.Get("Choose an amount.");

            try
            {
                var sliders = instance.textPanel.sliders;
                if (sliders != null && sliders.Count > 0)
                {
                    string sliderInfo = sliders.Count + Loc.Get(" sliders: ");
                    foreach (var slider in sliders)
                    {
                        string label = slider.label != null ? Loc.Get(slider.label.text) : Loc.Get("Amount");
                        sliderInfo += label + " " + (int)slider.minValue + Loc.Get(" to ") + (int)slider.maxValue + ", ";
                    }
                    text += " " + sliderInfo.TrimEnd(',', ' ') + ".";
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("ScenePatches.AnnounceSliderChoice", ex);
            }

            text += Loc.Get(" Use Up/Down to switch sliders, Left/Right to change value, Enter for Proceed.");
            ScreenReader.Say(text);
        }
    }
}
