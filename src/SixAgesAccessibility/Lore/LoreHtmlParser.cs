using System.IO;
using System.Text;

namespace SixAgesAccessibility.Lore
{
    /// <summary>
    /// Hand-rolled HTML-to-LoreDocument parser. Handles the narrow subset of
    /// markup the game's lore HTML actually uses: p / h1-h3 / ul / li / a / b /
    /// i / em / strong / br, plus head/style/script as skip-blocks. Anything
    /// unknown is treated as transparent (its tag is dropped, its text content
    /// flows into the surrounding node).
    ///
    /// Why not HtmlAgilityPack: Unity 2018's bundled Mono has gaps in the
    /// System.Xml stack that crash HAP at static-init time. A bespoke tokenizer
    /// also gives us cheap control over whitespace collapsing and entity
    /// decoding without dragging in a dependency.
    /// </summary>
    public static class LoreHtmlParser
    {
        /// <summary>Parse a raw HTML string (may be a body fragment or a full document).</summary>
        public static LoreDocument Parse(string html)
        {
            var doc = new LoreDocument();
            if (string.IsNullOrEmpty(html)) return doc;

            var st = new ParseState { Doc = doc };

            int i = 0;
            int n = html.Length;

            while (i < n)
            {
                char c = html[i];

                if (c == '<')
                {
                    // Comments must be detected BEFORE the IndexOf('>') below:
                    // the '>' inside '-->' would otherwise be mistaken for the
                    // tag terminator, leaving i past the only '-->' so the
                    // follow-up search finds none and eats the rest of the
                    // document. Info-DarkAge.html opens with a comment, so the
                    // entire intro page parsed to 0 nodes before this guard.
                    if (i + 3 < n && html[i + 1] == '!' && html[i + 2] == '-' && html[i + 3] == '-')
                    {
                        int closeIdx = html.IndexOf("-->", i + 4);
                        i = closeIdx < 0 ? n : closeIdx + 3;
                        continue;
                    }

                    int tagEnd = html.IndexOf('>', i + 1);
                    if (tagEnd < 0)
                    {
                        // Malformed — treat the rest as text rather than dropping.
                        st.AppendText(c);
                        i++;
                        continue;
                    }

                    string raw = html.Substring(i + 1, tagEnd - i - 1);
                    i = tagEnd + 1;

                    if (raw.Length > 0 && (raw[0] == '!' || raw[0] == '?')) continue;

                    bool isClose = raw.Length > 0 && raw[0] == '/';
                    string tagBody = isClose ? raw.Substring(1) : raw;
                    string tagName = ExtractTagName(tagBody);
                    if (tagName.Length == 0) continue;
                    string lowerName = tagName.ToLowerInvariant();

                    if (!isClose && (lowerName == "head" || lowerName == "style" || lowerName == "script"))
                    {
                        i = SkipUntilClose(html, i, lowerName);
                        continue;
                    }

                    HandleTag(st, lowerName, tagBody, isClose);
                    continue;
                }

                if (c == '&')
                {
                    int semi = html.IndexOf(';', i + 1);
                    if (semi > i && semi - i <= 10)
                    {
                        char decoded;
                        if (TryDecodeEntity(html.Substring(i + 1, semi - i - 1), out decoded))
                        {
                            st.AppendText(decoded);
                            i = semi + 1;
                            continue;
                        }
                    }
                    st.AppendText(c);
                    i++;
                    continue;
                }

                st.AppendText(c);
                i++;
            }

            st.FlushAndFinish();
            return doc;
        }

        private static void HandleTag(ParseState st, string lowerName, string tagBody, bool isClose)
        {
            if (isClose)
            {
                switch (lowerName)
                {
                    case "a":
                        st.CloseLink();
                        break;
                    case "p":
                    case "h1":
                    case "h2":
                    case "h3":
                    case "li":
                        st.CloseBlock();
                        break;
                    // ul/ol/b/i/em/strong/span/div/body/html close: ignored.
                }
                return;
            }

            switch (lowerName)
            {
                case "p":
                    st.OpenBlock(LoreNodeKind.Paragraph, 0);
                    break;
                case "h1":
                case "h2":
                case "h3":
                    st.OpenBlock(LoreNodeKind.Heading, lowerName[1] - '0');
                    break;
                case "li":
                    st.OpenBlock(LoreNodeKind.ListItem, 0);
                    break;
                case "br":
                    st.AppendBreak();
                    break;
                case "a":
                    st.OpenLink(ExtractAttribute(tagBody, "href"));
                    break;
                case "img":
                case "hr":
                case "meta":
                case "link":
                case "input":
                    // Void elements with no useful payload — skip silently. Alt
                    // text on <img> would be readable, but the game's lore HTML
                    // uses images only for decorative myth backdrops, so reading
                    // alt would mostly add noise.
                    break;
                // ul/ol/b/i/em/strong/span/div/body/html: transparent, no-op.
            }
        }

        private sealed class ParseState
        {
            public LoreDocument Doc;
            private LoreNode _current;
            private readonly StringBuilder _runBuf = new StringBuilder();
            private string _activeHref;   // null = no link active, "" = <a> without href, else href
            private string _activeAnchor; // fragment portion or null

            // Whitespace collapser: when we encounter whitespace inside a node
            // that already has content, defer emitting the space until the next
            // non-whitespace character. This carries the space across run
            // boundaries (e.g. between "Hello" and a following <a>Humakt</a>),
            // which a buf-local "previous was space" check could not.
            private bool _pendingSpace;

            public void AppendText(char c)
            {
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    if (HasNodeContent()) _pendingSpace = true;
                    return;
                }
                EnsureBlock();
                if (_pendingSpace)
                {
                    _runBuf.Append(' ');
                    _pendingSpace = false;
                }
                _runBuf.Append(c);
            }

            public void AppendBreak()
            {
                // <br> renders as a hard line break in the browser. For TTS a
                // soft space is enough — paragraph breaks come from <p>.
                if (HasNodeContent()) _pendingSpace = true;
            }

            public void OpenBlock(LoreNodeKind kind, int level)
            {
                CloseBlock();
                _current = new LoreNode { Kind = kind, Level = level };
                _pendingSpace = false;
            }

            public void CloseBlock()
            {
                FlushRun();
                CloseLink();
                if (_current != null && _current.Runs.Count > 0)
                {
                    Doc.Nodes.Add(_current);
                    if (Doc.Title == null && _current.Kind == LoreNodeKind.Heading)
                    {
                        Doc.Title = _current.ToPlainText().Trim();
                    }
                }
                _current = null;
                _pendingSpace = false;
            }

            public void OpenLink(string href)
            {
                FlushRun();
                if (href == null)
                {
                    // <a> without href — treat as inline emphasis (no link record).
                    _activeHref = string.Empty;
                    _activeAnchor = null;
                    return;
                }
                int hashIdx = href.IndexOf('#');
                if (hashIdx < 0)
                {
                    _activeHref = href;
                    _activeAnchor = null;
                }
                else
                {
                    _activeHref = href.Substring(0, hashIdx);
                    _activeAnchor = hashIdx + 1 < href.Length ? href.Substring(hashIdx + 1) : null;
                }
            }

            public void CloseLink()
            {
                FlushRun();
                _activeHref = null;
                _activeAnchor = null;
            }

            public void FlushAndFinish()
            {
                FlushRun();
                if (_current != null && _current.Runs.Count > 0)
                {
                    Doc.Nodes.Add(_current);
                    if (Doc.Title == null && _current.Kind == LoreNodeKind.Heading)
                        Doc.Title = _current.ToPlainText().Trim();
                }
                _current = null;
            }

            private bool HasNodeContent()
            {
                return _runBuf.Length > 0 || (_current != null && _current.Runs.Count > 0);
            }

            private void EnsureBlock()
            {
                if (_current == null) _current = new LoreNode { Kind = LoreNodeKind.Paragraph };
            }

            private void FlushRun()
            {
                if (_runBuf.Length == 0) return;
                string text = _runBuf.ToString();
                _runBuf.Length = 0;

                EnsureBlock();
                var run = new LoreRun { Text = text };
                if (_activeHref != null && _activeHref.Length > 0)
                {
                    run.LinkHref = _activeHref;
                    run.LinkAnchor = _activeAnchor;
                }
                _current.Runs.Add(run);

                if (run.IsLink)
                {
                    Doc.Links.Add(new LoreLink
                    {
                        NodeIndex = Doc.Nodes.Count,        // index where _current lands at CloseBlock
                        RunIndex = _current.Runs.Count - 1,
                        DisplayText = text.Trim(),
                        Href = run.LinkHref,
                        Anchor = run.LinkAnchor,
                        TargetTopic = TopicFromHref(run.LinkHref)
                    });
                }
            }
        }

        private static string ExtractTagName(string tagBody)
        {
            int end = 0;
            while (end < tagBody.Length)
            {
                char c = tagBody[end];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '/' || c == '>') break;
                end++;
            }
            return end == 0 ? string.Empty : tagBody.Substring(0, end);
        }

        private static int SkipUntilClose(string html, int from, string lowerName)
        {
            string close = "</" + lowerName;
            int candidate = html.IndexOf(close, from, System.StringComparison.OrdinalIgnoreCase);
            if (candidate < 0) return html.Length;
            int gt = html.IndexOf('>', candidate);
            return gt < 0 ? html.Length : gt + 1;
        }

        private static string ExtractAttribute(string tagBody, string name)
        {
            int idx = 0;
            while (idx < tagBody.Length)
            {
                while (idx < tagBody.Length && (tagBody[idx] == ' ' || tagBody[idx] == '\t' || tagBody[idx] == '\r' || tagBody[idx] == '\n')) idx++;
                int nameStart = idx;
                while (idx < tagBody.Length && tagBody[idx] != '=' && tagBody[idx] != ' ' && tagBody[idx] != '\t' && tagBody[idx] != '/' && tagBody[idx] != '>') idx++;
                string attrName = tagBody.Substring(nameStart, idx - nameStart);
                bool isMatch = string.Equals(attrName, name, System.StringComparison.OrdinalIgnoreCase);
                if (idx < tagBody.Length && tagBody[idx] == '=')
                {
                    idx++;
                    char quote = '\0';
                    if (idx < tagBody.Length && (tagBody[idx] == '"' || tagBody[idx] == '\''))
                    {
                        quote = tagBody[idx];
                        idx++;
                    }
                    int valStart = idx;
                    while (idx < tagBody.Length)
                    {
                        char c = tagBody[idx];
                        if (quote != '\0')
                        {
                            if (c == quote) break;
                        }
                        else if (c == ' ' || c == '\t' || c == '/' || c == '>') break;
                        idx++;
                    }
                    if (isMatch) return tagBody.Substring(valStart, idx - valStart);
                    if (quote != '\0' && idx < tagBody.Length) idx++;
                }
                else if (isMatch) return string.Empty;
            }
            return null;
        }

        private static string TopicFromHref(string href)
        {
            if (string.IsNullOrEmpty(href)) return string.Empty;
            int q = href.IndexOf('?');
            string path = q >= 0 ? href.Substring(0, q) : href;
            try { return Path.GetFileNameWithoutExtension(path); }
            catch
            {
                int slash = path.LastIndexOfAny(new[] { '/', '\\' });
                string seg = slash >= 0 ? path.Substring(slash + 1) : path;
                int dot = seg.LastIndexOf('.');
                return dot > 0 ? seg.Substring(0, dot) : seg;
            }
        }

        private static bool TryDecodeEntity(string entity, out char result)
        {
            result = '\0';
            if (string.IsNullOrEmpty(entity)) return false;
            if (entity[0] == '#')
            {
                int code;
                if (entity.Length > 1 && (entity[1] == 'x' || entity[1] == 'X'))
                {
                    if (int.TryParse(entity.Substring(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out code)
                        && code >= 0 && code <= 0xFFFF)
                    {
                        result = (char)code;
                        return true;
                    }
                    return false;
                }
                if (int.TryParse(entity.Substring(1), out code) && code >= 0 && code <= 0xFFFF)
                {
                    result = (char)code;
                    return true;
                }
                return false;
            }
            switch (entity)
            {
                case "amp": result = '&'; return true;
                case "lt": result = '<'; return true;
                case "gt": result = '>'; return true;
                case "quot": result = '"'; return true;
                case "apos": result = '\''; return true;
                case "nbsp": result = ' '; return true;
                case "mdash": result = '—'; return true;
                case "ndash": result = '–'; return true;
                case "hellip": result = '…'; return true;
                case "lsquo": result = '‘'; return true;
                case "rsquo": result = '’'; return true;
                case "ldquo": result = '“'; return true;
                case "rdquo": result = '”'; return true;
            }
            return false;
        }
    }
}
