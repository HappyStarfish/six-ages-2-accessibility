using System.Text;

namespace SixAgesAccessibility.Lore
{
    /// <summary>
    /// Feeds parsed lore content through the SixAgesDE translation plugin.
    ///
    /// Lore HTML is read straight from the game's browser_assets archive (and
    /// OSL lore scripts) — it never passes through the translator's string
    /// patches, so without this step the reader speaks English even when a
    /// German corpus entry exists. Block granularity lines up: the translation
    /// corpus stores lore paragraph-by-paragraph and LoreHtmlParser emits one
    /// node per paragraph / heading / list item, so a per-node lookup hits
    /// directly.
    ///
    /// Translation is lazy and cached per node: a myth page or the whole
    /// Manual can hold dozens of nodes and the translator spends a bounded
    /// budget on each cache miss, so translating only the node the user is
    /// about to hear keeps any pause down to a single keypress.
    /// </summary>
    public static class LoreTranslation
    {
        /// <summary>
        /// German plain text for <paramref name="node"/>. Translates and caches
        /// on first call; later calls return the cached result. Falls back to
        /// the original English text when no corpus entry exists or the
        /// translator plugin is absent.
        /// </summary>
        public static string NodeText(LoreNode node)
        {
            if (node == null) return string.Empty;
            if (!node.TranslationDone)
            {
                node.Translated = TranslationBridge.Translate(node.ToPlainText());
                node.TranslationDone = true;
            }
            return node.Translated ?? string.Empty;
        }

        /// <summary>
        /// German form of the document title. Cached after the first call.
        /// Returns a null/empty title unchanged so callers can fall back to a
        /// label of their own.
        /// </summary>
        public static string Title(LoreDocument doc)
        {
            if (doc == null) return null;
            if (!doc.TitleTranslationDone)
            {
                doc.TitleTranslated = string.IsNullOrEmpty(doc.Title)
                    ? doc.Title
                    : TranslationBridge.Translate(doc.Title);
                doc.TitleTranslationDone = true;
            }
            return doc.TitleTranslated;
        }

        /// <summary>
        /// German display text for a lore link. Cached on the link. A link's
        /// text is a sub-paragraph fragment, so proper nouns pass through
        /// unchanged and only phrases that exist as their own corpus entry get
        /// localized — the target topic is the game's lookup key and is never
        /// touched.
        /// </summary>
        public static string LinkText(LoreLink link)
        {
            if (link == null) return string.Empty;
            if (!link.DisplayTranslationDone)
            {
                link.DisplayTranslated = TranslationBridge.Translate(link.DisplayText);
                link.DisplayTranslationDone = true;
            }
            return link.DisplayTranslated ?? string.Empty;
        }

        /// <summary>
        /// Flatten the whole document to German plain text for the "read
        /// everything" hotkey. Translates every not-yet-seen node — the one
        /// place the per-node budget is paid in bulk, which is acceptable for
        /// an explicit whole-document request.
        /// </summary>
        public static string FullText(LoreDocument doc)
        {
            if (doc == null || doc.Nodes.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            for (int i = 0; i < doc.Nodes.Count; i++)
            {
                if (i > 0) sb.Append("\n\n");
                sb.Append(NodeText(doc.Nodes[i]));
            }
            return sb.ToString();
        }
    }
}
