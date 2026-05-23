using System;
using HarmonyLib;
using UnityEngine;

namespace SixAgesAccessibility.Patches
{
    /// <summary>
    /// Harmony patches around AdvisorHelper. The game shows advisor commentary as
    /// floating description boxes (e.g. after pressing SEND in EmissaryDialog the
    /// advisor may say "Gifts worth ten cows or more are more likely to have an
    /// impact"). Sighted players read these visually; without this patch they are
    /// silent for screen-reader users, who then think the action did nothing.
    /// </summary>
    [HarmonyPatch]
    public static class AdvisorPatches
    {
        // Set while the game's own Tab handler runs ShowNextAdvice. The game binds Tab to
        // cycle through advisors at InteractiveController.Update line 1324 and
        // ManagementController.Update line 335, which calls SetAdvisor → ShowAdviceText →
        // ShowAdvice. We cannot stop that — Tab is a game shortcut — but we can stay silent
        // for it: the user already has F3 / Shift+F3 and uses Tab purely for navigation.
        private static bool _suppressAdvisorSpeech;

        // The (string, int, PersonCard) overload internally calls the (string, int, Vector2)
        // overload, so patching the Vector2 variant catches both code paths.
        [HarmonyPatch(typeof(AdvisorHelper), "ShowAdvice", new Type[] { typeof(string), typeof(int), typeof(Vector2) })]
        [HarmonyPostfix]
        public static void ShowAdvice_Postfix(string text, int person)
        {
            try
            {
                if (_suppressAdvisorSpeech) return;

                // The original method early-returns when person < 1; mirror that — those
                // calls are no-op clean-ups, not actual advice.
                if (person < 1) return;
                if (string.IsNullOrEmpty(text)) return;

                string clean = StringHelpers.StripTags(text);
                if (clean.Length == 0) return;

                // Resolve the advisor's name via the game's own person-index lookup —
                // same index the game uses to position the advice bubble over the right
                // portrait. PC_PersonName goes through PluginImport (the native plugin
                // that owns the person table), so we never guess. If the lookup returns
                // empty for some unknown reason we fall back to the generic prefix.
                string speaker = null;
                try { speaker = PluginImport.PC_PersonName(person); }
                catch (Exception ex) { DebugLogger.Error("AdvisorPatches.PC_PersonName", ex); }

                // "Advisor <name> says: ..." rather than just "<name> says:" — the
                // user wanted the role made explicit so a stray name can't be confused
                // with a scene character speaking. Falls back to bare "Advisor: ..."
                // when the person lookup returns empty.
                string prefix = !string.IsNullOrEmpty(speaker)
                    ? Loc.Get("Advisor ") + speaker + Loc.Get(" says: ")
                    : Loc.Get("Advisor: ");

                // Queue (interrupt:false) so an advice popup never cuts off whatever
                // speech triggered it — typically a screen open or a button activation
                // that already has an announcement in flight. The user explicitly asked
                // for this: cutting off the dialog header / list nav for the advisor
                // commentary was disruptive.
                ScreenReader.Say(prefix + clean, interrupt: false);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("AdvisorPatches.ShowAdvice", ex);
            }
        }

        // Tab in scenes / management calls ShowNextAdvice on the active controller. Each
        // override (Interactive base + Scene/Quest specializations + private Management
        // override) reaches ShowAdvice synchronously through SetAdvisor → ShowAdviceText,
        // so a per-call flag pair suppresses the announcement only for that path.

        [HarmonyPatch(typeof(InteractiveController), "ShowNextAdvice")]
        [HarmonyPrefix]
        public static void InteractiveShowNextAdvice_Prefix() { _suppressAdvisorSpeech = true; }

        [HarmonyPatch(typeof(InteractiveController), "ShowNextAdvice")]
        [HarmonyPostfix]
        public static void InteractiveShowNextAdvice_Postfix() { _suppressAdvisorSpeech = false; }

        [HarmonyPatch(typeof(SceneController), "ShowNextAdvice")]
        [HarmonyPrefix]
        public static void SceneShowNextAdvice_Prefix() { _suppressAdvisorSpeech = true; }

        [HarmonyPatch(typeof(SceneController), "ShowNextAdvice")]
        [HarmonyPostfix]
        public static void SceneShowNextAdvice_Postfix() { _suppressAdvisorSpeech = false; }

        // ManagementController.ShowNextAdvice is private — patch by name still works.
        [HarmonyPatch(typeof(ManagementController), "ShowNextAdvice")]
        [HarmonyPrefix]
        public static void ManagementShowNextAdvice_Prefix() { _suppressAdvisorSpeech = true; }

        [HarmonyPatch(typeof(ManagementController), "ShowNextAdvice")]
        [HarmonyPostfix]
        public static void ManagementShowNextAdvice_Postfix() { _suppressAdvisorSpeech = false; }

        // The game also auto-shows advice on every fresh scene via ShowAdviceForNewScene.
        // The user gets advisor input via F3 on demand, so this auto-burst is just noise on
        // top of the question text — suppress it the same way.
        [HarmonyPatch(typeof(SceneController), "ShowAdviceForNewScene")]
        [HarmonyPrefix]
        public static void SceneShowAdviceForNewScene_Prefix() { _suppressAdvisorSpeech = true; }

        [HarmonyPatch(typeof(SceneController), "ShowAdviceForNewScene")]
        [HarmonyPostfix]
        public static void SceneShowAdviceForNewScene_Postfix() { _suppressAdvisorSpeech = false; }

        [HarmonyPatch(typeof(QuestController), "ShowAdviceForNewScene")]
        [HarmonyPrefix]
        public static void QuestShowAdviceForNewScene_Prefix() { _suppressAdvisorSpeech = true; }

        [HarmonyPatch(typeof(QuestController), "ShowAdviceForNewScene")]
        [HarmonyPostfix]
        public static void QuestShowAdviceForNewScene_Postfix() { _suppressAdvisorSpeech = false; }

    }
}
