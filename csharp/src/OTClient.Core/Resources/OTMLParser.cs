namespace OTClient.Framework.Resources;

/// <summary>
/// Parses OTML (OTClient Markup Language) — the indentation-based configuration
/// format used for .otml, .otui, and .otmod files.
/// <para>
/// Format synopsis:
/// <code>
/// // line comment
/// # hash comment
///
/// key: value
/// node:
///   child: 42
///   flag: true
///   inner:
///     deep: hello
///
/// # Alias definition
/// &amp;myColor: #ff0000
///
/// widget:
///   color: *myColor     # alias reference
///   count: null
/// </code>
/// </para>
/// Maps to <c>src/framework/otml/otmlparser.*</c>.
/// Task 9.5.
/// </summary>
public static class OTMLParser
{
    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Parses OTML text and returns a document.</summary>
    public static OTMLDocument Parse(string text, string sourcePath = "")
    {
        ArgumentNullException.ThrowIfNull(text);
        var doc = new OTMLDocument { SourcePath = sourcePath };
        var ctx = new ParseContext(text, sourcePath, doc.Root);
        ctx.Parse();
        return doc;
    }

    // ─── ParseContext ─────────────────────────────────────────────────────────

    private sealed class ParseContext
    {
        private readonly string[]  _lines;
        private readonly string    _source;
        private readonly OTMLNode  _docRoot;

        // Alias table: name → value
        private readonly Dictionary<string, string> _aliases
            = new(StringComparer.Ordinal);

        // Parent stack: (node, indent-level)
        private readonly Stack<(OTMLNode Node, int Indent)> _stack = new();

        public ParseContext(string text, string source, OTMLNode docRoot)
        {
            _lines   = text.ReplaceLineEndings("\n").Split('\n');
            _source  = source;
            _docRoot = docRoot;
            _stack.Push((_docRoot, -1));
        }

        // ─── Parser main loop ─────────────────────────────────────────────────

        public void Parse()
        {
            for (int lineNo = 0; lineNo < _lines.Length; lineNo++)
            {
                string raw = _lines[lineNo];

                // Measure indent before trimming
                int indent = MeasureIndent(raw);
                string line = raw.TrimStart(' ', '\t').TrimEnd();

                // Skip blank lines and comments
                if (line.Length == 0 || line.StartsWith("//") || line.StartsWith('#'))
                    continue;

                // Split into tag : value (value may be absent or empty)
                (string tag, string? rawValue) = SplitTagValue(line);

                // Strip inline comment from value
                if (rawValue is not null)
                    rawValue = StripInlineComment(rawValue);

                // Alias definition: &name: value
                if (tag.StartsWith('&'))
                {
                    string aliasName = tag[1..].Trim();
                    if (!string.IsNullOrEmpty(aliasName) && rawValue is not null)
                        _aliases[aliasName] = rawValue.Trim();
                    continue;
                }

                // Build node
                bool isNull  = rawValue is not null && rawValue.Trim() == "null";
                string? value = rawValue?.Trim();

                // Resolve alias reference in value: *name
                if (value is not null && value.StartsWith('*'))
                {
                    string refName = value[1..].Trim();
                    value = _aliases.TryGetValue(refName, out var resolved) ? resolved : value;
                }

                // Strip surrounding quotes from value
                if (value is not null)
                    value = StripQuotes(value);

                var node = new OTMLNode
                {
                    Tag    = tag.Trim(),
                    Value  = isNull ? null : value,
                    IsNull = isNull,
                    Source = $"{_source}:{lineNo + 1}",
                };

                // Pop stack until parent has lower indent
                while (_stack.Count > 1 && _stack.Peek().Indent >= indent)
                    _stack.Pop();

                _stack.Peek().Node.AddChild(node);

                // If value is absent (pure container), push this node as potential parent
                if (rawValue is null || rawValue.Trim().Length == 0)
                    _stack.Push((node, indent));
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private static int MeasureIndent(string line)
        {
            int i = 0;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
                i++;
            return i;
        }

        /// <summary>
        /// Splits a OTML line into (tag, value?).
        /// Value is <c>null</c> when no colon exists (value-less container).
        /// </summary>
        private static (string Tag, string? Value) SplitTagValue(string line)
        {
            int colonIdx = line.IndexOf(':');
            if (colonIdx < 0)
                return (line, null);      // tag-only node

            string tag   = line[..colonIdx];
            string value = colonIdx + 1 < line.Length ? line[(colonIdx + 1)..] : string.Empty;
            return (tag, value);
        }

        private static string StripInlineComment(string value)
        {
            int idx = value.IndexOf(" //", StringComparison.Ordinal);
            return idx >= 0 ? value[..idx] : value;
        }

        private static string StripQuotes(string s)
        {
            s = s.Trim();
            if (s.Length >= 2)
            {
                char f = s[0], l = s[^1];
                if ((f == '"' && l == '"') || (f == '\'' && l == '\''))
                    return s[1..^1];
            }
            return s;
        }
    }
}
