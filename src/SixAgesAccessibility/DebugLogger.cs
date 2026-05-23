using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace SixAgesAccessibility
{
    /// <summary>
    /// Centralized debug logging. Toggle with F12.
    /// When active: logs to BepInEx console + speaks critical entries via ScreenReader.
    /// When inactive: zero overhead (all calls short-circuit).
    /// </summary>
    public static class DebugLogger
    {
        private static ManualLogSource _log;
        private static bool _active;
        private static readonly List<string> _recentEntries = new List<string>();
        private const int MaxRecentEntries = 50;

        /// <summary>Whether debug mode is currently active.</summary>
        public static bool IsActive => _active;

        /// <summary>Initialize with the BepInEx logger.</summary>
        public static void Init(ManualLogSource log)
        {
            _log = log;
        }

        /// <summary>Toggle debug mode on/off. Returns new state.</summary>
        public static bool Toggle()
        {
            _active = !_active;
            string state = _active ? "ON" : "OFF";

            // Always log toggle to BepInEx
            _log?.LogInfo($"[DebugLogger] Debug mode: {state}");

            // Announce via screen reader
            ScreenReader.Say("Debug mode " + state);

            return _active;
        }

        /// <summary>Log a debug message. Only outputs when debug mode is active.</summary>
        public static void Log(string source, string message)
        {
            if (!_active) return;

            string entry = $"[{source}] {message}";
            _log?.LogInfo(entry);
            AddRecent(entry);
        }

        /// <summary>Log and speak a debug message. Only when debug mode is active.</summary>
        public static void LogAndSpeak(string source, string message)
        {
            if (!_active) return;

            string entry = $"[{source}] {message}";
            _log?.LogInfo(entry);
            AddRecent(entry);
            ScreenReader.Say(message, interrupt: false);
        }

        /// <summary>Log an error. ALWAYS logs to BepInEx regardless of debug mode.</summary>
        public static void Error(string source, string message)
        {
            string entry = $"[{source}] ERROR: {message}";
            _log?.LogError(entry);
            AddRecent(entry);

            // In debug mode, also speak errors
            if (_active)
                ScreenReader.Say("Error in " + source + ": " + message, interrupt: false);
        }

        /// <summary>Log an exception. ALWAYS logs to BepInEx regardless of debug mode.</summary>
        public static void Error(string source, Exception ex)
        {
            string entry = $"[{source}] EXCEPTION: {ex.Message}";
            _log?.LogError($"[{source}] {ex}");
            AddRecent(entry);

            if (_active)
                ScreenReader.Say("Exception in " + source + ": " + ex.Message, interrupt: false);
        }

        /// <summary>Log a warning. Always logs to BepInEx.</summary>
        public static void Warn(string source, string message)
        {
            string entry = $"[{source}] WARN: {message}";
            _log?.LogWarning(entry);
            AddRecent(entry);
        }

        /// <summary>Log Harmony patch registration. Always logs to BepInEx for diagnostics.</summary>
        public static void LogPatch(string patchTarget, bool success)
        {
            string status = success ? "OK" : "FAILED";
            string entry = $"[Harmony] Patch {patchTarget}: {status}";
            _log?.LogInfo(entry);
            AddRecent(entry);
        }

        /// <summary>Speak the last N debug entries via screen reader.</summary>
        public static void SpeakRecent(int count = 5)
        {
            if (_recentEntries.Count == 0)
            {
                ScreenReader.Say("No debug entries.");
                return;
            }

            int start = Math.Max(0, _recentEntries.Count - count);
            string output = "Last " + Math.Min(count, _recentEntries.Count) + " entries: ";
            for (int i = start; i < _recentEntries.Count; i++)
            {
                output += _recentEntries[i] + ". ";
            }
            ScreenReader.Say(output);
        }

        private static void AddRecent(string entry)
        {
            _recentEntries.Add(entry);
            if (_recentEntries.Count > MaxRecentEntries)
                _recentEntries.RemoveAt(0);
        }
    }
}
