using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SixAgesAccessibility.Patches;
using UnityEngine;

namespace SixAgesAccessibility
{
    /// <summary>
    /// BepInEx entry point. Initializes Tolk for screen-reader output, applies all Harmony
    /// patches in this assembly, and attaches the KeyboardNavigationHandler component to the
    /// plugin's GameObject so it ticks every frame for hotkey handling.
    /// </summary>
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(TranslationModGUID, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        /// <summary>Stable BepInEx plugin identifier; also used as the Harmony patcher ID.</summary>
        public const string PluginGUID = "com.accessibility.sixages2";

        /// <summary>
        /// BepInEx GUID of the separate German translation mod. Declared above as a soft
        /// dependency so BepInEx loads that mod first when it is installed — this guarantees
        /// it is already present in <c>Chainloader.PluginInfos</c> by the time <see cref="Awake"/>
        /// runs here, regardless of on-disk load order. Hardcoded rather than referenced
        /// because the two mods are independent assemblies with no build-time link.
        /// </summary>
        public const string TranslationModGUID = "com.translation.sixages2.de";

        /// <summary>Human-readable plugin name (shown in BepInEx logs and load order).</summary>
        public const string PluginName = "Six Ages 2 Accessibility";

        /// <summary>Plugin version. Bump on user-visible changes — affects BepInEx load resolution.</summary>
        public const string PluginVersion = "0.2.0";

        /// <summary>BepInEx logger shared with all helper classes (DebugLogger, ScreenReader).</summary>
        internal static ManualLogSource Log;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo(PluginName + " v" + PluginVersion + " loading...");

            DebugLogger.Init(Log);

            // Language policy: the accessibility mod speaks English by default. When the
            // German translation mod is installed the game UI is German, so screen-reader
            // output must match — the German string table is registered only in that case.
            // Detection is by BepInEx plugin presence; the soft dependency declared on this
            // class ensures the translation mod is already in PluginInfos when this runs.
            bool germanModActive = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(TranslationModGUID);
            if (germanModActive)
            {
                LocSetup.RegisterGerman();
                Log.LogInfo("German translation mod detected — screen reader output set to German.");
            }
            else
            {
                Log.LogInfo("German translation mod not present — screen reader output stays English.");
            }
            Loc.SetLanguageGerman(germanModActive);

            if (!ScreenReader.Init())
            {
                Log.LogWarning("Tolk not found — screen reader output disabled.");
            }

            try
            {
                _harmony = new Harmony(PluginGUID);
                _harmony.PatchAll();
                Log.LogInfo("Harmony patches applied.");
                VerifyPatches();
            }
            catch (Exception ex)
            {
                Log.LogError("Harmony patching failed: " + ex.ToString());
            }

            gameObject.AddComponent<KeyboardNavigationHandler>();
            Log.LogInfo("Keyboard navigation handler added.");

            // Subscribe to ScreenManager.onScreenChanged as soon as the singleton exists.
            // Lazy subscription from a Harmony postfix on ShowManagementScreen missed the
            // very first activation: the show coroutine fires onScreenChanged synchronously
            // when there are no transition tasks pending (instant load from a saved game),
            // so by the time the postfix runs the listener never received the first change.
            StartCoroutine(SubscribeWhenScreenManagerReady());

            ScreenReader.Say("Six Ages Accessibility loaded.");
            Log.LogInfo(PluginName + " loaded successfully.");
        }

        private IEnumerator SubscribeWhenScreenManagerReady()
        {
            const float timeoutSeconds = 30f;
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                // Probe with FindObjectOfType BEFORE touching Singleton.instance.
                // Singleton<T>.instance has a known NRE in its auto-create path:
                // when FindObjectOfType returns null, line 30 of Singleton.cs
                // calls `instance_.gameObject` on the null result before the
                // safety null-check below it. The previous version caught the
                // NRE but still spammed the log with two errors on every cold
                // start; this version avoids triggering the broken path until
                // ScreenManager actually exists in the scene.
                ScreenManager sm = null;
                try
                {
                    if (!Singleton<ScreenManager>.isShuttingDown)
                        sm = UnityEngine.Object.FindObjectOfType<ScreenManager>();
                }
                catch (Exception ex)
                {
                    DebugLogger.Error("Plugin.SubscribeWhenScreenManagerReady", ex);
                }

                if (sm != null && sm.onScreenChanged != null)
                {
                    ScreenChangePatches.SubscribeOnScreenChanged(sm);
                    yield break;
                }

                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            DebugLogger.Warn("Plugin", "ScreenManager singleton never became available (30s); screen-change announcements may be missed on first load.");
        }

        private void VerifyPatches()
        {
            try
            {
                int count = 0;
                foreach (var m in _harmony.GetPatchedMethods())
                {
                    string typeName = "?";
                    try
                    {
                        object dt = m.DeclaringType;
                        if (dt is Type t)
                            typeName = t.Name;
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Error("Plugin.VerifyPatches.DeclaringType", ex);
                    }
                    DebugLogger.LogPatch(typeName + "." + m.Name, true);
                    count++;
                }
                Log.LogInfo("Total patched methods: " + count);
            }
            catch (Exception ex)
            {
                Log.LogWarning("VerifyPatches failed (non-critical): " + ex.Message);
            }
        }

        private void OnDestroy()
        {
            if (_harmony != null)
                _harmony.UnpatchSelf();
            ScreenReader.Shutdown();
        }
    }
}
