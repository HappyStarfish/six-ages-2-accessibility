using System;
using System.Runtime.InteropServices;

namespace SixAgesAccessibility
{
    /// <summary>Screen reader output via Tolk (supports NVDA, JAWS, etc.).</summary>
    public static class ScreenReader
    {
        private const string TolkDll = "Tolk";
        private static bool _initialized;

        [DllImport(TolkDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Load();

        [DllImport(TolkDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Unload();

        [DllImport(TolkDll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Tolk_HasSpeech();

        [DllImport(TolkDll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Tolk_Output([MarshalAs(UnmanagedType.LPWStr)] string str, [MarshalAs(UnmanagedType.Bool)] bool interrupt);

        [DllImport(TolkDll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Tolk_Speak([MarshalAs(UnmanagedType.LPWStr)] string str, [MarshalAs(UnmanagedType.Bool)] bool interrupt);

        [DllImport(TolkDll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Tolk_Silence();

        [DllImport(TolkDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr Tolk_DetectScreenReader();

        [DllImport(TolkDll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Tolk_IsLoaded();

        /// <summary>Initialize Tolk. Returns true if a screen reader was detected.</summary>
        public static bool Init()
        {
            if (_initialized) return true;

            try
            {
                Tolk_Load();
                _initialized = true;

                bool hasSpeech = Tolk_HasSpeech();
                IntPtr srPtr = Tolk_DetectScreenReader();
                string srName = srPtr != IntPtr.Zero ? Marshal.PtrToStringUni(srPtr) : "none";

                Plugin.Log.LogInfo($"Tolk loaded. Screen reader: {srName}, speech: {hasSpeech}");
                return hasSpeech;
            }
            catch (DllNotFoundException)
            {
                Plugin.Log.LogWarning("Tolk.dll not found in game directory.");
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Tolk init failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Speak text. If interrupt is true, stops any current speech first.</summary>
        public static void Say(string text, bool interrupt = true)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (_initialized)
            {
                try
                {
                    Tolk_Output(text, interrupt);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Tolk_Output failed: {ex.Message}");
                }
            }

            // Use LogInfo so [SR] lines reach LogOutput.log without lowering the global
            // BepInEx log level. We need to see what was actually spoken when diagnosing
            // "I press Tab and hear nothing" reports.
            Plugin.Log.LogInfo($"[SR] {(interrupt ? "!" : "+")} {text}");
        }

        /// <summary>Stop current speech.</summary>
        public static void Silence()
        {
            if (!_initialized) return;

            try
            {
                Tolk_Silence();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Tolk_Silence failed: {ex.Message}");
            }
        }

        /// <summary>Shutdown Tolk.</summary>
        public static void Shutdown()
        {
            if (!_initialized) return;

            try
            {
                Tolk_Unload();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Tolk_Unload failed: {ex.Message}");
            }

            _initialized = false;
        }
    }
}
