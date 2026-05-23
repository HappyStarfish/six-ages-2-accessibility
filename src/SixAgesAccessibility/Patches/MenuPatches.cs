using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SixAgesAccessibility.Patches
{
    /// <summary>Harmony patches for main menu, game selection, tutorials and dialogs.</summary>
    [HarmonyPatch]
    public static class MenuPatches
    {
        // --- Main Menu ---

        /// <summary>Announce main menu when it becomes active.</summary>
        [HarmonyPatch(typeof(MainMenu), "OnActivate")]
        [HarmonyPostfix]
        public static void MainMenu_Activated()
        {
            try
            {
                // Queued (interrupt:false): when the trainer tutorial ends, the very last
                // topic ("You're ready to make your own story!...") is queued just before
                // the screen switches to MainMenu. Saying "Main Menu." with interrupt:true
                // here cut that closing message off entirely — the user pressed Continue
                // and only heard "Main Menu." Now we wait for the queue to drain first.
                ScreenReader.Say(Loc.Get("Main Menu."), interrupt: false);

                // Mini-tutorial stage 0: the one-time welcome. Delivered here — the first
                // time the main menu ever appears — so a new player is not stuck not
                // knowing the controls before any tutorial card teaches them. Queued
                // after "Main Menu." and returns null on every later visit.
                string welcome = OnboardingHintHandler.Instance.ConsumeWelcome();
                if (!string.IsNullOrEmpty(welcome))
                    ScreenReader.Say(welcome, interrupt: false);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MenuPatches.MainMenu", ex);
            }
        }

        // --- Choose Game (save/load screen) ---

        /// <summary>
        /// Announce a COMPACT overview of the Choose Game screen — chapter names and
        /// per-chapter entry counts, plus a "not available" marker for chapters whose
        /// rows are all placeholders. The previous version listed every save's full
        /// details (clan name, date, season, action label) and produced a ~1300-char
        /// wall of text that took 20+ seconds to read; the user couldn't realistically
        /// listen to it before reaching for an arrow key, which interrupted the
        /// announcement after the first chapter. Per-item details now live in the
        /// arrow-key focus path (one save per Tab/arrow press) where the user controls
        /// the pace. ChooseGame is reached via SPIELEN in the main menu, so a quick
        /// overview is what's needed; full inspection is interactive.
        /// </summary>
        [HarmonyPatch(typeof(ChooseGameController), "OnShowEnd")]
        [HarmonyPostfix]
        public static void ChooseGame_Shown(ChooseGameController __instance)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.Append(Loc.Get("Choose Game. "));

                UIList gameList = __instance.gameList;
                if (gameList != null && gameList.count > 0)
                {
                    string currentChapter = null;
                    int interactableCount = 0;
                    int unavailableCount = 0;
                    bool first = true;

                    for (int i = 0; i < gameList.count; i++)
                    {
                        UIListItem row = gameList[i];
                        if (row == null) continue;

                        SaveListItem save = row as SaveListItem;
                        if (save == null)
                        {
                            // Chapter header — flush the previous chapter's counts.
                            FlushChapterCounts(sb, currentChapter, interactableCount,
                                unavailableCount, ref first);
                            currentChapter = string.IsNullOrEmpty(row.text) ? null : row.text;
                            interactableCount = 0;
                            unavailableCount = 0;
                            continue;
                        }

                        if (save.button == null) continue;

                        bool interactable =
                            save.button.isActiveAndEnabled && save.button.IsInteractable();
                        if (interactable) interactableCount++;
                        else unavailableCount++;
                    }

                    FlushChapterCounts(sb, currentChapter, interactableCount,
                        unavailableCount, ref first);
                }

                ScreenReader.Say(sb.ToString());
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MenuPatches.ChooseGame", ex);
            }
        }

        /// <summary>
        /// Append a compact "Chapter X: N entries" line for one chapter. Chapters
        /// with no interactable entries are skipped entirely — that catches both
        /// genuinely-empty slots AND the permanently-unavailable PREQUEL/sequel
        /// teaser cards ("Ride Like the Wind", "The World Reborn") which would
        /// otherwise show up as "nicht verfügbar" noise the player can never act on.
        /// RLTW saves loaded from a configured StormAge folder live under the
        /// separate "Six Ages" chapter and are interactable, so they still come
        /// through.
        /// </summary>
        private static void FlushChapterCounts(
            System.Text.StringBuilder sb,
            string chapter,
            int interactableCount,
            int unavailableCount,
            ref bool first)
        {
            if (interactableCount == 0) return;
            if (string.IsNullOrEmpty(chapter)) return;

            if (!first) sb.Append(' ');
            first = false;

            sb.Append(Loc.Get("Chapter ")).Append(chapter).Append(": ");
            sb.Append(interactableCount).Append(' ');
            sb.Append(Loc.Get(interactableCount == 1 ? "entry" : "entries"));
            sb.Append('.');
        }


        // --- Tutorial full-screen dialog ---

        // TutorialController.OnShow internally calls UpdateToCurrentTopic, so patching
        // both fired the announcement twice and ate the tail of the previous speech
        // (typically the closing tutorial card "You're ready to make your own story...").
        // Patching only UpdateToCurrentTopic covers both first-show and subsequent topic
        // changes inside the same dialog (Continue → GC_AdvanceTrainer → fresh topic).
        // The signature cache guards against redundant fires for the same topic, which
        // can happen on layout rebuilds.
        private static string _lastSpokenTopicSignature;

        // A pending block of tutorial topics. The trainer shows chained topics a short
        // time apart; each topic is BUFFERED here, not spoken. FlushPendingTutorialAnnounce
        // emits the whole block as one utterance once it has settled — so nothing is
        // ever sent to the screen reader alone and then replaced (no audible fragment),
        // and a chain is never separate queued items an arrow press could chop apart.
        private static readonly System.Collections.Generic.List<string> _blockTopicTexts =
            new System.Collections.Generic.List<string>();
        private static readonly System.Collections.Generic.List<string> _blockParagraphs =
            new System.Collections.Generic.List<string>();
        private static bool _blockPending;
        private static bool _blockCoversScene;
        private static float _blockLastTopicTime;

        // Quiet time (no new topic) after which a buffered block is spoken. Longer than
        // the trainer's gap between chained topics, short enough to still feel prompt.
        // Seconds (not frames) so it is framerate-independent.
        private const float TutorialFlushDelaySeconds = 0.35f;

        /// <summary>Announce tutorial content when topic is set or changes.</summary>
        [HarmonyPatch(typeof(TutorialController), "UpdateToCurrentTopic")]
        [HarmonyPostfix]
        public static void Tutorial_TopicUpdated()
        {
            try
            {
                AnnounceTutorialContent();
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MenuPatches.TutorialUpdate", ex);
            }
        }

        private static void AnnounceTutorialContent()
        {
            string title = Tutorial.topicTitle;
            string[] texts = Tutorial.topicTextArray;

            string body = "";
            if (texts != null)
            {
                foreach (string t in texts)
                {
                    if (!string.IsNullOrEmpty(t) && !t.StartsWith("$"))
                        body += t + " ";
                }
            }

            string signature = (title ?? "") + "|" + body;
            if (signature == _lastSpokenTopicSignature)
            {
                DebugLogger.Log("MenuPatches", "Tutorial topic unchanged, skipping re-announce");
                return;
            }
            _lastSpokenTopicSignature = signature;

            if (!_blockPending)
            {
                // First topic of a new block. Stop whatever is being spoken right now
                // (e.g. a scene's text) so it does not blip on during the short settle
                // window — the block is then announced into clean silence.
                _blockTopicTexts.Clear();
                _blockParagraphs.Clear();
                _blockPending = true;
                _blockCoversScene = (Time.frameCount - ScenePatches.LastSceneAnnounceFrame <= 1);
                ScreenReader.Silence();
            }
            else
            {
                DebugLogger.Log("MenuPatches", "Chained tutorial topic, gap="
                    + (Time.realtimeSinceStartup - _blockLastTopicTime).ToString("0.000") + "s");
            }
            _blockLastTopicTime = Time.realtimeSinceStartup;

            // Buffer this topic: its spoken form (title + body) and its individual
            // review paragraphs (title, then each text element).
            string topicText = string.IsNullOrEmpty(title) ? body : (title + ". " + body);
            _blockTopicTexts.Add(topicText.Trim());
            if (!string.IsNullOrEmpty(title))
                _blockParagraphs.Add(title.Trim());
            if (texts != null)
            {
                foreach (string t in texts)
                {
                    if (string.IsNullOrEmpty(t) || t.StartsWith("$")) continue;
                    string clean = t.Trim();
                    if (clean.Length > 0) _blockParagraphs.Add(clean);
                }
            }

            DebugLogger.Log("MenuPatches", "Tutorial topic buffered: block now "
                + _blockTopicTexts.Count + " topic(s)");
        }

        /// <summary>
        /// Emit a buffered tutorial block once it has settled — no new topic for
        /// <see cref="TutorialFlushDelaySeconds"/>. Called once per frame from
        /// <see cref="KeyboardNavigationHandler"/>'s Update. Speaking the whole block as
        /// one utterance, only after the chain is complete, means nothing is ever spoken
        /// alone and then cut — so there is no audible fragment before the full text.
        /// </summary>
        public static void FlushPendingTutorialAnnounce()
        {
            try
            {
                if (!_blockPending) return;
                if (Time.realtimeSinceStartup - _blockLastTopicTime < TutorialFlushDelaySeconds)
                    return;

                _blockPending = false;

                var sb = new System.Text.StringBuilder();
                sb.Append("Tutorial. ");
                for (int i = 0; i < _blockTopicTexts.Count; i++)
                    sb.Append(_blockTopicTexts[i]).Append(' ');
                sb.Append(Loc.Get("Use Up and Down to re-read the text. Press Enter to continue."));

                DebugLogger.Log("MenuPatches", "Tutorial block announce: " + _blockTopicTexts.Count + " topic(s)");
                ScreenReader.Say(sb.ToString(), interrupt: true);

                // The arrow-review navigator reviews exactly this block, every topic of it.
                TutorialScreenNavigator.Instance.SetBlock(_blockParagraphs.ToArray());

                // A scene this tutorial block covered: its text + choices only make
                // sense once the tutorial is gone. ScenePatches delivers it when the
                // user dismisses the tutorial and lands back on the scene.
                if (_blockCoversScene)
                {
                    DebugLogger.Log("MenuPatches", "Deferring scene text until tutorial dismissed");
                    ScenePatches.DeferSceneUntilTutorialDismissed();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MenuPatches.FlushTutorial", ex);
            }
        }

        // --- Tutorial popup cards (small hint bubbles) ---

        // Joined text of the hint last announced. TutorialView.Init can fire several
        // times for the same hint (layout rebuilds), so we dedupe the announcement on
        // the full text — mirrors _lastSpokenTopicSignature for the full-screen path.
        private static string _lastAnnouncedHint;

        /// <summary>
        /// Announce a tutorial hint card as a single string when it appears, no matter
        /// how many pages the game splits it across. The page split is flattened by
        /// <see cref="TutorialHintHandler.BuildFullHintText"/>.
        ///
        /// Queued (interrupt:false) so the hint waits its turn behind whatever the
        /// screen reader is currently speaking — typically the screen-change
        /// announcement or a recently-pressed action's feedback. Cutting those off used
        /// to drop the ball on critical context (clan list summaries, response options)
        /// right before the hint. User-driven NextCard/PreviousCard still interrupt —
        /// a deliberate keypress expects an immediate response.
        /// </summary>
        [HarmonyPatch(typeof(TutorialView), "Init")]
        [HarmonyPostfix]
        public static void TutorialCard_Shown()
        {
            try
            {
                // Read every page and join them. Walks the native page index and
                // restores it; does not rebuild the view, so it is safe here even
                // though this is itself a postfix on TutorialView.Init.
                string full = TutorialHintHandler.Instance.BuildFullHintText();
                if (string.IsNullOrEmpty(full))
                    return;

                if (full == _lastAnnouncedHint)
                {
                    DebugLogger.Log("MenuPatches", "Tutorial hint unchanged, skipping re-announce");
                    return;
                }
                _lastAnnouncedHint = full;

                // Mini-tutorial: if this card is part of the on-boarding curriculum and
                // its concept has not been taught yet, append one short keyboard hint to
                // the end of the announcement — card text first, hint last. GetCardHint
                // returns null (and consumes nothing) for any card outside the curriculum
                // or a concept already delivered. We pass the full card text because the
                // native plugin exposes no reachable Lua-id accessor (TutorialView_Name
                // returns the optional trigger name field, null on most cards) — the
                // curriculum is fingerprinted by first-page text in EN+DE instead. See
                // OnboardingHintHandler class summary for the why.
                string toSay = Loc.Get("Hint: ") + full;
                string onboardingHint = OnboardingHintHandler.Instance.GetCardHint(full);
                if (!string.IsNullOrEmpty(onboardingHint))
                    toSay += " " + onboardingHint;

                ScreenReader.Say(toSay, interrupt: false);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MenuPatches.TutorialCard", ex);
            }
        }

        /// <summary>Announce when navigating tutorial card pages.</summary>
        [HarmonyPatch(typeof(TutorialView), "NextCard")]
        [HarmonyPostfix]
        public static void TutorialCard_Next()
        {
            try
            {
                string text = TutorialCard.currentText;
                if (!string.IsNullOrEmpty(text))
                {
                    int page = TutorialCard.textIndex + 1;
                    int total = TutorialCard.textCount;
                    ScreenReader.Say(text + " (page " + page + " of " + total + ")");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MenuPatches.TutorialNext", ex);
            }
        }

        [HarmonyPatch(typeof(TutorialView), "PreviousCard")]
        [HarmonyPostfix]
        public static void TutorialCard_Previous()
        {
            try
            {
                string text = TutorialCard.currentText;
                if (!string.IsNullOrEmpty(text))
                {
                    int page = TutorialCard.textIndex + 1;
                    int total = TutorialCard.textCount;
                    ScreenReader.Say(text + " (page " + page + " of " + total + ")");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MenuPatches.TutorialPrev", ex);
            }
        }

        // --- Tutorial reset (Controls overlay) ---

        /// <summary>
        /// When the player presses the game's "Reset Tutorial" button, also reset the
        /// mod's mini-tutorial — clear its progress file so the welcome and every
        /// curriculum hint appear again. One button, both resets. A short spoken
        /// confirmation tells the (blind) player the mod-side reset took effect, since
        /// the game itself only plays a click sound here.
        /// </summary>
        [HarmonyPatch(typeof(ControlsOverlay), "ResetTutorial")]
        [HarmonyPostfix]
        public static void ControlsOverlay_ResetTutorial()
        {
            try
            {
                OnboardingHintHandler.Instance.Reset();
                ScreenReader.Say(Loc.Get("Tutorial reset. The mod's hints will appear again."),
                    interrupt: false);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MenuPatches.ResetTutorial", ex);
            }
        }

        // --- Modal dialogs (confirmations, errors) ---

        /// <summary>Announce dialog title, message and buttons.</summary>
        [HarmonyPatch(typeof(SixAgesDialog), "OnShow")]
        [HarmonyPostfix]
        public static void Dialog_Shown(SixAgesDialog __instance)
        {
            try
            {
                string output = "Dialog: ";
                if (__instance.title != null && !string.IsNullOrEmpty(__instance.title.text))
                    output += __instance.title.text + ". ";
                if (__instance.message != null && !string.IsNullOrEmpty(__instance.message.text))
                    output += __instance.message.text + ". ";

                if (__instance.button0 != null && __instance.button0.gameObject.activeSelf)
                    output += "Button: " + __instance.button0.label.text + ". ";
                if (__instance.button1 != null && __instance.button1.gameObject.activeSelf)
                    output += "Button: " + __instance.button1.label.text + ". ";

                ScreenReader.Say(output);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MenuPatches.Dialog", ex);
            }
        }

        // --- Controls/Settings overlay ---
        //
        // The screen-change header (ScreenChangePatches) already announces "Menü-
        // phase. Einstellungen Bildschirm." when this overlay opens. The user
        // navigates element-by-element with the arrow keys, and each focus stop
        // announces its own label — a flat overview-on-open turned out to be
        // overkill for a menu (users want to walk through it, not hear a wall of
        // status). We therefore intentionally do NOT add an OnShow announcement
        // here. Per-toggle state changes are still announced by the keyboard
        // navigation layer (UIToggle.isOn flips when the user presses Space).

        // --- StormAge folder picker (RLTW continuation) ---
        //
        // ControlsOverlay.PickContinuationFolder originally opens SimpleFileBrowser,
        // a mouse-driven Unity component with no screen-reader support: panel switches,
        // a quick-links sidebar, a path input field, a results list, filename input,
        // and Submit/Cancel/Back/Forward/Up buttons. Making it accessible is a
        // multi-session project AND the underlying need is narrow (only users with a
        // Six Ages 1 "Ride Like the Wind" victory save have a StormAge folder to
        // import in the first place). The pragmatic bypass: skip the file browser
        // entirely and resolve the path from sources a screen-reader user can already
        // reach without leaving their normal workflow — the system clipboard (paste
        // a path from Explorer / NVDA file dialogs / Notepad), the previously stored
        // PlayerPref, and the hardcoded GOG default. Replicates the original's
        // PickSuccess validation (folder must be named "StormAge") and writes the
        // same PlayerPrefs key, so downstream "continue from RLTW" logic finds it
        // exactly where it expects.
        [HarmonyPatch(typeof(ControlsOverlay), "PickContinuationFolder")]
        [HarmonyPrefix]
        public static bool PickContinuationFolder_Prefix(ControlsOverlay __instance)
        {
            try
            {
                string picked = ResolveStormAgePath();
                if (picked == null)
                {
                    ScreenReader.Say(Loc.Get(
                        "To set the previous-game folder, copy the full path of your StormAge folder to the clipboard and activate this button again. The folder must be named StormAge."));
                    return false;
                }

                string filename = System.IO.Path.GetFileName(picked.TrimEnd(
                    System.IO.Path.DirectorySeparatorChar,
                    System.IO.Path.AltDirectorySeparatorChar));

                if (filename != "StormAge")
                {
                    ScreenReader.Say(Loc.Get("The folder must be named StormAge, but got: ") + filename);
                    return false;
                }

                if (!System.IO.Directory.Exists(picked))
                {
                    ScreenReader.Say(Loc.Get("Folder not found: ") + picked);
                    return false;
                }

                UnityEngine.PlayerPrefs.SetString("PreviousGame", picked);
                if (__instance.path != null)
                    __instance.path.text = picked;

                ScreenReader.Say(Loc.Get("Previous-game folder set to ") + picked);
                return false;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("MenuPatches.PickContinuationFolder", ex);
                return false;
            }
        }

        /// <summary>
        /// Walk the candidate sources in priority order and return the first one that
        /// looks like a StormAge folder, or null if none match. Validation (must be
        /// named "StormAge", must exist) happens in the caller so each rejection can
        /// be announced with a specific reason.
        /// </summary>
        private static string ResolveStormAgePath()
        {
            string clip = (UnityEngine.GUIUtility.systemCopyBuffer ?? "").Trim().Trim('"', '\'');
            if (!string.IsNullOrEmpty(clip))
                return clip;

            string defaultGog =
                "C:\\Program Files (x86)\\GOG Galaxy\\Games\\Six Ages 2 Lights Going Out\\Documents\\StormAge";
            if (System.IO.Directory.Exists(defaultGog))
                return defaultGog;

            return null;
        }
    }
}
