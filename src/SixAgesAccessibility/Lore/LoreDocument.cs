using System.Collections.Generic;
using System.Text;

namespace SixAgesAccessibility.Lore
{
    /// <summary>Structural classification of a top-level node.</summary>
    public enum LoreNodeKind
    {
        /// <summary>An h1/h2/h3 heading. Level carries the heading depth.</summary>
        Heading,
        /// <summary>A regular paragraph (p, or default text content outside a list).</summary>
        Paragraph,
        /// <summary>A single bullet of an ul/ol list.</summary>
        ListItem
    }

    /// <summary>
    /// One inline span inside a node. A run is either plain text or a hyperlink.
    /// Boldness and italics are intentionally not modelled — the screen reader
    /// flattens to plain text and emphasis would only add noise on TTS.
    /// </summary>
    public sealed class LoreRun
    {
        /// <summary>Visible text of this run. Whitespace already normalized.</summary>
        public string Text;

        /// <summary>Link target as found in href, with the anchor stripped. Null for non-link runs.</summary>
        public string LinkHref;

        /// <summary>The fragment portion of href without the leading '#', or null.</summary>
        public string LinkAnchor;

        /// <summary>True if this run is a hyperlink (LinkHref non-null).</summary>
        public bool IsLink { get { return LinkHref != null; } }
    }

    /// <summary>One paragraph, heading, or list item. Indexed for Up/Down navigation.</summary>
    public sealed class LoreNode
    {
        /// <summary>Kind discriminator — Heading/Paragraph/ListItem.</summary>
        public LoreNodeKind Kind;

        /// <summary>For Heading: 1, 2, or 3. Otherwise 0.</summary>
        public int Level;

        /// <summary>Inline runs that make up this node, in document order.</summary>
        public readonly List<LoreRun> Runs = new List<LoreRun>();

        /// <summary>
        /// German plain text for this node, or null until LoreTranslation has
        /// run. Cached so repeated reads (Up/Down revisits, F5) cost one lookup.
        /// </summary>
        public string Translated;

        /// <summary>True once LoreTranslation has attempted this node — guards the cache.</summary>
        public bool TranslationDone;

        /// <summary>Concatenate all run text without link decorations.</summary>
        public string ToPlainText()
        {
            if (Runs.Count == 1) return Runs[0].Text ?? string.Empty;
            var sb = new StringBuilder();
            for (int i = 0; i < Runs.Count; i++)
            {
                var r = Runs[i];
                if (r != null && r.Text != null) sb.Append(r.Text);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Flat reference into a link run, used by Tab/Shift+Tab navigation.
    /// Stored separately so the link-cycle is independent of paragraph order
    /// and we can announce "link N of M" without iterating the node tree each press.
    /// </summary>
    public sealed class LoreLink
    {
        /// <summary>Index into LoreDocument.Nodes.</summary>
        public int NodeIndex;

        /// <summary>Index into LoreNode.Runs at that node.</summary>
        public int RunIndex;

        /// <summary>The visible link text, stripped of surrounding whitespace.</summary>
        public string DisplayText;

        /// <summary>Raw href without the fragment portion.</summary>
        public string Href;

        /// <summary>Fragment portion of href (without leading '#') or null.</summary>
        public string Anchor;

        /// <summary>
        /// File-basename of href without extension. This is the topic key the
        /// game uses for ShowDialogWithTopic / OpenLink. Empty for fragment-only links.
        /// </summary>
        public string TargetTopic;

        /// <summary>German form of <see cref="DisplayText"/>, or null until LoreTranslation has run.</summary>
        public string DisplayTranslated;

        /// <summary>True once LoreTranslation has attempted the link text — guards the cache.</summary>
        public bool DisplayTranslationDone;
    }

    /// <summary>
    /// Parsed lore document — a flat list of nodes plus an extracted link table.
    /// Produced by LoreHtmlParser, consumed by LoreReader for navigation.
    /// </summary>
    public sealed class LoreDocument
    {
        /// <summary>Top-level nodes in reading order.</summary>
        public readonly List<LoreNode> Nodes = new List<LoreNode>();

        /// <summary>All hyperlink runs in document order.</summary>
        public readonly List<LoreLink> Links = new List<LoreLink>();

        /// <summary>Title from the first encountered heading, or null.</summary>
        public string Title;

        /// <summary>German form of <see cref="Title"/>, or null until LoreTranslation has run.</summary>
        public string TitleTranslated;

        /// <summary>True once LoreTranslation has attempted the title — guards the cache.</summary>
        public bool TitleTranslationDone;

        /// <summary>True if the document carries no readable nodes.</summary>
        public bool IsEmpty { get { return Nodes.Count == 0; } }

        /// <summary>Flatten the entire document for "read whole document" hotkey.</summary>
        public string ToPlainText()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (i > 0) sb.Append("\n\n");
                sb.Append(Nodes[i].ToPlainText());
            }
            return sb.ToString();
        }
    }
}
