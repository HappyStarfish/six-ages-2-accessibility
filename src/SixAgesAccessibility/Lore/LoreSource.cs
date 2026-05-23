using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace SixAgesAccessibility.Lore
{
    /// <summary>
    /// Loads lore HTML by topic name, in the same order MythDialogController does:
    /// OSL script first (dynamic, may reflect game state), static archive HTML second.
    ///
    /// Static HTML lives in the ZenFulcrum browser archive at
    /// "&lt;Application.dataPath&gt;/Resources/browser_assets" (zfbRes_v1 format,
    /// see docs/game-api.md). We read the index lazily on first hit and cache it
    /// for the rest of the session — entry layout is fixed at runtime, the
    /// archive itself is not modified once the game is installed.
    /// </summary>
    public static class LoreSource
    {
        private static readonly Dictionary<string, ArchiveEntry> _index =
            new Dictionary<string, ArchiveEntry>();
        private static bool _indexBuilt;
        private static bool _indexFailed;
        private static string _archivePath;

        private struct ArchiveEntry
        {
            public long Offset;
            public uint Size;
        }

        /// <summary>
        /// Convenience for the MythDialog flow. Replicates ShowTopic's source-selection:
        /// detail=false picks the "_mini" variant first, then falls back to the bare topic.
        /// </summary>
        public static LoreDocument LoadByTopic(string topic, bool detail, string chapter)
        {
            if (string.IsNullOrEmpty(topic)) return new LoreDocument();
            if (string.IsNullOrEmpty(chapter)) chapter = ResolveChapter();

            string variant = detail ? topic : topic + "_mini";

            // Mirror MythDialogController.ShowTopic exactly:
            //   if Game.ScriptExists("lore_" + variant) → use OSL, no fallback
            //   else → static "{chapter}-Lore/{variant}.html", fallback "{chapter}-Lore/{topic}.html"
            string oslHtml = TryLoadOsl(variant);
            if (!string.IsNullOrEmpty(oslHtml))
            {
                return LoreHtmlParser.Parse(oslHtml);
            }

            string staticHtml = TryLoadStatic(chapter, variant);
            if (string.IsNullOrEmpty(staticHtml) && !detail)
            {
                staticHtml = TryLoadStatic(chapter, topic);
            }
            if (!string.IsNullOrEmpty(staticHtml))
            {
                return LoreHtmlParser.Parse(staticHtml);
            }

            return new LoreDocument();
        }

        /// <summary>Load any HTML resource by archive path, e.g. "Manual/TOC.html". No OSL fallback.</summary>
        public static LoreDocument LoadByPath(string archivePath)
        {
            string html = TryLoadByPath(archivePath);
            return string.IsNullOrEmpty(html) ? new LoreDocument() : LoreHtmlParser.Parse(html);
        }

        /// <summary>
        /// Mirrors MythDialogController.ShowTopic's chapter logic exactly:
        ///   chapter = Utils.IsShowMode() ? "DarkAge" : Game.AppChapter()
        /// We can't call Utils.IsShowMode without binding to game internals, so
        /// fall back to "DarkAge" when AppChapter throws or is empty (the only
        /// chapter shipped at the time of writing).
        /// </summary>
        public static string ResolveChapter()
        {
            try
            {
                string ch = Game.AppChapter();
                if (!string.IsNullOrEmpty(ch)) return ch;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("LoreSource.ResolveChapter", ex);
            }
            return "DarkAge";
        }

        private static string TryLoadOsl(string name)
        {
            try
            {
                string script = "lore_" + name;
                if (!Game.ScriptExists(script)) return null;
                return Game.TextFromRunningScript(script);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("LoreSource.TryLoadOsl(" + name + ")", ex);
                return null;
            }
        }

        private static string TryLoadStatic(string chapter, string name)
        {
            string path = "/" + chapter + "-Lore/" + name + ".html";
            return TryLoadByPath(path);
        }

        private static string TryLoadByPath(string path)
        {
            if (!EnsureIndex()) return null;
            string key = path.StartsWith("/") ? path : "/" + path;
            ArchiveEntry entry;
            if (!_index.TryGetValue(key, out entry)) return null;

            try
            {
                using (var fs = new FileStream(_archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    fs.Seek(entry.Offset, SeekOrigin.Begin);
                    byte[] buf = new byte[entry.Size];
                    int read = 0;
                    while (read < buf.Length)
                    {
                        int n = fs.Read(buf, read, buf.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    if (read != buf.Length)
                    {
                        DebugLogger.Warn("LoreSource", "short read on " + key + " (" + read + "/" + buf.Length + ")");
                        return null;
                    }
                    return Encoding.UTF8.GetString(buf);
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("LoreSource.TryLoadByPath(" + key + ")", ex);
                return null;
            }
        }

        private static bool EnsureIndex()
        {
            if (_indexBuilt) return true;
            if (_indexFailed) return false;

            try
            {
                _archivePath = Path.Combine(Application.dataPath, "Resources/browser_assets");
                if (!File.Exists(_archivePath))
                {
                    DebugLogger.Warn("LoreSource", "archive not found at " + _archivePath);
                    _indexFailed = true;
                    return false;
                }

                // BinaryReader's 3-arg (stream, encoding, leaveOpen) ctor was
                // added in .NET 4.5 — Unity 2018's bundled Mono lacks it and
                // throws MissingMethodException at first archive read. Fall
                // back to the 1-arg ctor (UTF8 default); the surrounding
                // `using` on FileStream handles disposal either way.
                using (var fs = new FileStream(_archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var br = new BinaryReader(fs))
                {
                    int magicLen = br.ReadByte();
                    if (magicLen != 9)
                    {
                        DebugLogger.Warn("LoreSource", "unexpected magicLen " + magicLen);
                        _indexFailed = true;
                        return false;
                    }
                    string magic = Encoding.ASCII.GetString(br.ReadBytes(magicLen));
                    if (magic != "zfbRes_v1")
                    {
                        DebugLogger.Warn("LoreSource", "unexpected magic '" + magic + "'");
                        _indexFailed = true;
                        return false;
                    }
                    uint entryCount = br.ReadUInt32();
                    for (uint k = 0; k < entryCount; k++)
                    {
                        int pathLen = br.ReadByte();
                        string path = Encoding.UTF8.GetString(br.ReadBytes(pathLen));
                        long offset = br.ReadInt64();
                        uint size = br.ReadUInt32();
                        _index[path] = new ArchiveEntry { Offset = offset, Size = size };
                    }
                }
                Plugin.Log?.LogInfo("[LoreSource] indexed " + _index.Count + " browser_assets entries");
                _indexBuilt = true;
                return true;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("LoreSource.EnsureIndex", ex);
                _indexFailed = true;
                return false;
            }
        }
    }
}
