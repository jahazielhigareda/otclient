// HtmlParser.cs — T32: HTML/CSS parser for rich-text news panels
// Ported from src/framework/html/ (htmlnode.h/cpp, htmlparser.cpp, queryselector.cpp)

using System.Text;

namespace OTClient.Framework.UI;

// ─── NodeType ────────────────────────────────────────────────────────────────

public enum NodeType { Element, Text, Comment, Doctype }

// ─── HtmlNode ────────────────────────────────────────────────────────────────

/// <summary>
/// A node in the parsed HTML tree.  Mirrors C++ <c>HtmlNode</c>.
/// </summary>
public sealed class HtmlNode
{
    // ── identity ─────────────────────────────────────────────────────────────
    public NodeType Type { get; internal set; } = NodeType.Element;
    public string Tag { get; internal set; } = string.Empty;

    // raw text for Text / Comment / Doctype nodes; also raw body of <script> / <style>
    internal string RawText { get; set; } = string.Empty;

    // ── tree links ───────────────────────────────────────────────────────────
    public HtmlNode? Parent { get; internal set; }
    public HtmlNode? Prev { get; internal set; }
    public HtmlNode? Next { get; internal set; }

    internal List<HtmlNode> ChildList { get; } = [];
    public IReadOnlyList<HtmlNode> Children => ChildList;

    // ── attributes / classes ─────────────────────────────────────────────────
    internal Dictionary<string, string> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, string> AttributesMap => Attributes;

    internal List<string> ClassListInternal { get; } = [];
    public IReadOnlyList<string> ClassList => ClassListInternal;

    // ── expression flag (template {{...}} nodes) ─────────────────────────────
    public bool IsExpression { get; internal set; }

    // ── document-level indexes (only populated on the root node) ─────────────
    internal Dictionary<string, HtmlNode> IdIndex { get; } = [];
    internal Dictionary<string, List<WeakReference<HtmlNode>>> ClassIndex { get; } = [];
    internal Dictionary<string, List<WeakReference<HtmlNode>>> TagIndex { get; } = [];

    // ── attribute helpers ────────────────────────────────────────────────────

    public string GetAttr(string name) =>
        Attributes.TryGetValue(name, out var v) ? v : string.Empty;

    public bool HasAttr(string name) => Attributes.ContainsKey(name);

    public void SetAttr(string name, string value)
    {
        Attributes[name.ToLowerInvariant()] = value;
        if (name.Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            var root = DocumentRoot();
            if (!string.IsNullOrEmpty(value))
                root.IdIndex[value] = this;
        }
    }

    public bool RemoveAttr(string name)
    {
        var key = name.ToLowerInvariant();
        return Attributes.Remove(key);
    }

    // ── text / content ───────────────────────────────────────────────────────

    /// <summary>Raw text stored in this node (e.g. the text of a Text node).</summary>
    public string GetText() => RawText;

    /// <summary>Recursive text content, mirroring C++ textContent().</summary>
    public string TextContent()
    {
        if (Type == NodeType.Text) return RawText;
        if (Type == NodeType.Element)
        {
            var sb = new StringBuilder(RawText);
            foreach (var c in ChildList) sb.Append(c.TextContent());
            return sb.ToString();
        }
        return string.Empty;
    }

    // ── innerHTML / outerHTML ────────────────────────────────────────────────

    public string InnerHTML()
    {
        if (Type != NodeType.Element) return ToString(recursive: true);
        var sb = new StringBuilder();
        foreach (var c in ChildList) sb.Append(c.ToString(recursive: true));
        if (!string.IsNullOrEmpty(RawText)) sb.Append(RawText);
        return sb.ToString();
    }

    public string OuterHTML() => ToString(recursive: true);

    // ── structural queries ───────────────────────────────────────────────────

    public HtmlNode DocumentRoot()
    {
        var cur = this;
        while (cur.Parent != null) cur = cur.Parent;
        return cur;
    }

    public HtmlNode? GetById(string id)
    {
        var root = DocumentRoot();
        return root.IdIndex.TryGetValue(id, out var n) ? n : null;
    }

    public List<HtmlNode> GetByClass(string cls)
    {
        var root = DocumentRoot();
        var out_ = new List<HtmlNode>();
        if (!root.ClassIndex.TryGetValue(cls, out var refs)) return out_;
        foreach (var w in refs)
            if (w.TryGetTarget(out var n)) out_.Add(n);
        return out_;
    }

    public List<HtmlNode> GetByTag(string tag)
    {
        var root = DocumentRoot();
        var out_ = new List<HtmlNode>();
        var key = tag.ToLowerInvariant();
        if (!root.TagIndex.TryGetValue(key, out var refs)) return out_;
        foreach (var w in refs)
            if (w.TryGetTarget(out var n)) out_.Add(n);
        return out_;
    }

    // ── index helpers (structural position) ─────────────────────────────────

    public int IndexInParent()
    {
        if (Parent == null) return -1;
        for (int i = 0; i < Parent.ChildList.Count; i++)
            if (ReferenceEquals(Parent.ChildList[i], this)) return i;
        return -1;
    }

    public bool IsOnlyChild()
    {
        if (Parent == null) return false;
        int count = 0;
        foreach (var c in Parent.ChildList) if (c.Type == NodeType.Element) count++;
        return count == 1;
    }

    public bool IsLastChild()
    {
        if (Parent == null) return false;
        for (int i = Parent.ChildList.Count - 1; i >= 0; i--)
            if (Parent.ChildList[i].Type == NodeType.Element)
                return ReferenceEquals(Parent.ChildList[i], this);
        return false;
    }

    public bool IsEmpty()
    {
        foreach (var c in ChildList)
        {
            if (c.Type == NodeType.Element) return false;
            if (c.Type == NodeType.Text && !string.IsNullOrEmpty(c.RawText)) return false;
        }
        return true;
    }

    /// <summary>1-based index among element siblings.</summary>
    public int IndexAmongElements()
    {
        int idx = 0;
        if (Parent == null) return 0;
        foreach (var c in Parent.ChildList)
        {
            if (c.Type == NodeType.Element) idx++;
            if (ReferenceEquals(c, this)) return idx;
        }
        return 0;
    }

    /// <summary>1-based index among same-tag siblings.</summary>
    public int IndexAmongType()
    {
        int idx = 0;
        if (Parent == null) return 0;
        foreach (var c in Parent.ChildList)
        {
            if (c.Type == NodeType.Element && c.Tag == Tag) idx++;
            if (ReferenceEquals(c, this)) return idx;
        }
        return 0;
    }

    // ── mutation ─────────────────────────────────────────────────────────────

    public void Append(HtmlNode child)  => AttachChild(child, ChildList.Count);
    public void Prepend(HtmlNode child) => AttachChild(child, 0);
    public void Insert(HtmlNode child, int pos)
    {
        if (pos < 0) pos = 0;
        if (pos > ChildList.Count) pos = ChildList.Count;
        AttachChild(child, pos);
    }

    public void Remove(HtmlNode child)
    {
        int idx = ChildList.IndexOf(child);
        if (idx < 0) return;
        UnregisterSubtreeFromIndexes(child);
        var left  = idx > 0 ? ChildList[idx - 1] : null;
        var right = idx + 1 < ChildList.Count ? ChildList[idx + 1] : null;
        if (left != null)  left.Next = right;
        if (right != null) right.Prev = left;
        ChildList.RemoveAt(idx);
        child.Parent = null;
        child.Prev   = null;
        child.Next   = null;
    }

    public void Clear()
    {
        foreach (var child in ChildList)
        {
            UnregisterSubtreeFromIndexes(child);
            child.Parent = null;
            child.Prev   = null;
            child.Next   = null;
        }
        ChildList.Clear();
    }

    private void AttachChild(HtmlNode child, int pos)
    {
        // detach from old parent
        if (child.Parent != null)
        {
            if (!ReferenceEquals(child.Parent, this))
                child.Parent.Remove(child);
            else
            {
                int oldIdx = ChildList.IndexOf(child);
                if (oldIdx >= 0)
                {
                    var l = oldIdx > 0 ? ChildList[oldIdx - 1] : null;
                    var r = oldIdx + 1 < ChildList.Count ? ChildList[oldIdx + 1] : null;
                    if (l != null) l.Next = r;
                    if (r != null) r.Prev = l;
                    ChildList.RemoveAt(oldIdx);
                    if (pos > ChildList.Count) pos = ChildList.Count;
                }
            }
        }

        // wire sibling links
        HtmlNode? before = pos > 0 ? ChildList[pos - 1] : null;
        HtmlNode? after  = pos < ChildList.Count ? ChildList[pos] : null;
        child.Prev = before;
        child.Next = after;
        if (before != null) before.Next = child;
        if (after  != null) after.Prev  = child;

        child.Parent = this;
        ChildList.Insert(pos, child);

        RegisterSubtreeInIndexes(child);
    }

    // ── index registration ───────────────────────────────────────────────────

    internal void RegisterSubtreeInIndexes(HtmlNode node)
    {
        var root = DocumentRoot();
        var stack = new Stack<HtmlNode>();
        stack.Push(node);
        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            if (cur.Type == NodeType.Element)
            {
                if (!string.IsNullOrEmpty(cur.Tag) && cur.Tag != "root")
                {
                    if (!root.TagIndex.TryGetValue(cur.Tag, out var tl))
                        root.TagIndex[cur.Tag] = tl = [];
                    tl.Add(new WeakReference<HtmlNode>(cur));
                }
                var id = cur.GetAttr("id");
                if (!string.IsNullOrEmpty(id)) root.IdIndex[id] = cur;
                foreach (var cls in cur.ClassListInternal)
                {
                    if (!root.ClassIndex.TryGetValue(cls, out var cl))
                        root.ClassIndex[cls] = cl = [];
                    cl.Add(new WeakReference<HtmlNode>(cur));
                }
            }
            foreach (var c in cur.ChildList) stack.Push(c);
        }
    }

    internal void UnregisterSubtreeFromIndexes(HtmlNode node)
    {
        var root = DocumentRoot();
        var stack = new Stack<HtmlNode>();
        stack.Push(node);
        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            if (cur.Type == NodeType.Element)
            {
                if (!string.IsNullOrEmpty(cur.Tag) && cur.Tag != "root" &&
                    root.TagIndex.TryGetValue(cur.Tag, out var tl))
                    tl.RemoveAll(w => !w.TryGetTarget(out var t) || ReferenceEquals(t, cur));

                var id = cur.GetAttr("id");
                if (!string.IsNullOrEmpty(id) && root.IdIndex.TryGetValue(id, out var existing)
                    && ReferenceEquals(existing, cur))
                    root.IdIndex.Remove(id);

                foreach (var cls in cur.ClassListInternal)
                    if (root.ClassIndex.TryGetValue(cls, out var cl))
                        cl.RemoveAll(w => !w.TryGetTarget(out var t) || ReferenceEquals(t, cur));
            }
            foreach (var c in cur.ChildList) stack.Push(c);
        }
    }

    // ── CSS selector queries ─────────────────────────────────────────────────

    public HtmlNode? QuerySelector(string selector) =>
        HtmlQuerySelector.QuerySelector(this, selector);

    public List<HtmlNode> QuerySelectorAll(string selector) =>
        HtmlQuerySelector.QuerySelectorAll(this, selector);

    // ── clone ────────────────────────────────────────────────────────────────

    public HtmlNode Clone(bool deep = true)
    {
        var n = new HtmlNode
        {
            Type         = Type,
            Tag          = Tag,
            RawText      = RawText,
            IsExpression = IsExpression
        };
        foreach (var kv in Attributes) n.Attributes[kv.Key] = kv.Value;
        n.ClassListInternal.AddRange(ClassListInternal);

        if (deep)
        {
            HtmlNode? last = null;
            foreach (var ch in ChildList)
            {
                var c = ch.Clone(true);
                c.Parent = n;
                if (last != null) { last.Next = c; c.Prev = last; }
                n.ChildList.Add(c);
                last = c;
            }
        }
        RebuildIndexes(n);
        return n;
    }

    private static void RebuildIndexes(HtmlNode root)
    {
        root.IdIndex.Clear();
        root.ClassIndex.Clear();
        root.TagIndex.Clear();
        var stack = new Stack<HtmlNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            if (cur.Type == NodeType.Element)
            {
                if (!string.IsNullOrEmpty(cur.Tag) && cur.Tag != "root")
                {
                    if (!root.TagIndex.TryGetValue(cur.Tag, out var tl))
                        root.TagIndex[cur.Tag] = tl = [];
                    tl.Add(new WeakReference<HtmlNode>(cur));
                }
                var id = cur.GetAttr("id");
                if (!string.IsNullOrEmpty(id)) root.IdIndex[id] = cur;
                foreach (var cls in cur.ClassListInternal)
                {
                    if (!root.ClassIndex.TryGetValue(cls, out var cl))
                        root.ClassIndex[cls] = cl = [];
                    cl.Add(new WeakReference<HtmlNode>(cur));
                }
            }
            foreach (var c in cur.ChildList) stack.Push(c);
        }
    }

    // ── serialization ────────────────────────────────────────────────────────

    public string ToString(bool recursive)
    {
        switch (Type)
        {
            case NodeType.Text:
                return IsExpression ? "{{" + RawText + "}}" : RawText;
            case NodeType.Comment:
                return "<!--" + RawText + "-->";
            case NodeType.Doctype:
                return "<!" + RawText + ">";
            case NodeType.Element:
            {
                if (Tag == "root")
                {
                    var sb2 = new StringBuilder();
                    foreach (var c in ChildList) sb2.Append(c.ToString(recursive));
                    return sb2.ToString();
                }
                var sb = new StringBuilder("<").Append(Tag);
                foreach (var kv in Attributes)
                {
                    sb.Append(' ').Append(kv.Key);
                    if (!string.IsNullOrEmpty(kv.Value))
                        sb.Append("=\"").Append(kv.Value).Append('"');
                }
                if (ChildList.Count == 0 && string.IsNullOrEmpty(RawText))
                    return sb.Append(" />").ToString();
                sb.Append('>');
                if (recursive) foreach (var c in ChildList) sb.Append(c.ToString(recursive));
                sb.Append(RawText);
                sb.Append("</").Append(Tag).Append('>');
                return sb.ToString();
            }
            default: return string.Empty;
        }
    }

    public override string ToString() => ToString(recursive: false);
}

// ─── HtmlParser ──────────────────────────────────────────────────────────────

/// <summary>
/// Lenient HTML5-style parser.  Mirrors C++ <c>parseHtml()</c>.
/// </summary>
public static class HtmlParser
{
    private static readonly HashSet<string> VoidTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "area","base","br","col","embed","hr","img","input","link","meta","source","track","wbr"
    };

    private static readonly HashSet<string> PCloseOn = new(StringComparer.OrdinalIgnoreCase)
    {
        "address","article","aside","blockquote","div","dl","fieldset","footer","form",
        "h1","h2","h3","h4","h5","h6","header","hgroup","hr","main","nav","ol","p",
        "pre","section","table","ul","figure","figcaption","menu"
    };

    private static readonly HashSet<string> TextOnlyTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "script","style","title","textarea","option"
    };

    // ── public entry point ───────────────────────────────────────────────────

    public static HtmlNode Parse(string html)
    {
        var root = new HtmlNode { Type = NodeType.Element, Tag = "root" };
        var st   = new Stack<HtmlNode>();
        st.Push(root);

        var hoistedRaw = new List<HtmlNode>();
        int i = 0, N = html.Length;

        // ── helpers ──────────────────────────────────────────────────────────

        void PushNode(HtmlNode node)
        {
            var parent = st.Peek();

            // merge consecutive non-expression text nodes
            if (node.Type == NodeType.Text && !node.IsExpression && parent.ChildList.Count > 0)
            {
                var last = parent.ChildList[^1];
                if (last.Type == NodeType.Text && !last.IsExpression)
                {
                    last.RawText += node.RawText;
                    return;
                }
            }

            // wire prev/next
            if (parent.ChildList.Count > 0)
            {
                var last = parent.ChildList[^1];
                last.Next  = node;
                node.Prev  = last;
            }
            node.Parent = parent;
            parent.ChildList.Add(node);

            if (node.Type == NodeType.Element)
            {
                if (!string.IsNullOrEmpty(node.Tag) && node.Tag != "root")
                {
                    if (!root.TagIndex.TryGetValue(node.Tag, out var tl))
                        root.TagIndex[node.Tag] = tl = [];
                    tl.Add(new WeakReference<HtmlNode>(node));
                }
                var id = node.GetAttr("id");
                if (!string.IsNullOrEmpty(id)) root.IdIndex[id] = node;
                foreach (var cls in node.ClassListInternal)
                {
                    if (!root.ClassIndex.TryGetValue(cls, out var cl))
                        root.ClassIndex[cls] = cl = [];
                    cl.Add(new WeakReference<HtmlNode>(node));
                }
            }
        }

        void AttachFront(HtmlNode parent, HtmlNode node)
        {
            node.Parent = null; node.Prev = null; node.Next = null;
            if (parent.ChildList.Count > 0)
            {
                var first = parent.ChildList[0];
                node.Next  = first;
                first.Prev = node;
                parent.ChildList.Insert(0, node);
            }
            else
            {
                parent.ChildList.Add(node);
            }
            node.Parent = parent;

            if (node.Type == NodeType.Element)
            {
                if (!string.IsNullOrEmpty(node.Tag) && node.Tag != "root")
                {
                    if (!root.TagIndex.TryGetValue(node.Tag, out var tl))
                        root.TagIndex[node.Tag] = tl = [];
                    tl.Add(new WeakReference<HtmlNode>(node));
                }
                var id = node.GetAttr("id");
                if (!string.IsNullOrEmpty(id)) root.IdIndex[id] = node;
                foreach (var cls in node.ClassListInternal)
                {
                    if (!root.ClassIndex.TryGetValue(cls, out var cl))
                        root.ClassIndex[cls] = cl = [];
                    cl.Add(new WeakReference<HtmlNode>(node));
                }
            }
        }

        void AppendOrDropWhitespace(string literal)
        {
            if (string.IsNullOrEmpty(literal)) return;
            bool onlyWs = true;
            foreach (char c in literal) { if (!IsSpace(c)) { onlyWs = false; break; } }
            var parent = st.Peek();
            if (onlyWs)
            {
                if (parent.ChildList.Count > 0)
                {
                    var last = parent.ChildList[^1];
                    if (last.Type == NodeType.Text && !last.IsExpression)
                    {
                        last.RawText += literal;
                        return;
                    }
                }
                return;
            }
            var t = new HtmlNode { Type = NodeType.Text, RawText = literal };
            PushNode(t);
        }

        // ── main parse loop ───────────────────────────────────────────────────

        while (i < N)
        {
            if (html[i] != '<')
            {
                int lt = html.IndexOf('<', i);
                if (lt < 0) lt = N;
                string txt = html.Substring(i, lt - i);
                i = lt;

                if (!string.IsNullOrEmpty(txt))
                {
                    string decoded = HtmlEntityDecode(txt);
                    var parent = st.Peek();

                    if (parent.Type == NodeType.Element && TextOnlyTags.Contains(parent.Tag))
                    {
                        parent.RawText += decoded;
                    }
                    else
                    {
                        // split by {{...}} expressions
                        int p = 0;
                        while (p < decoded.Length)
                        {
                            int open = decoded.IndexOf("{{", p, StringComparison.Ordinal);
                            if (open < 0)
                            {
                                AppendOrDropWhitespace(decoded.Substring(p));
                                break;
                            }
                            if (open > p)
                                AppendOrDropWhitespace(decoded.Substring(p, open - p));

                            int close = decoded.IndexOf("}}", open + 2, StringComparison.Ordinal);
                            if (close < 0)
                            {
                                AppendOrDropWhitespace(decoded.Substring(open));
                                break;
                            }
                            string expr = decoded.Substring(open + 2, close - (open + 2)).Trim();
                            if (!string.IsNullOrEmpty(expr))
                            {
                                var t = new HtmlNode { Type = NodeType.Text, RawText = expr, IsExpression = true };
                                PushNode(t);
                            }
                            p = close + 2;
                        }
                    }
                }
                continue;
            }

            // we are at '<'
            if (i + 3 < N && html.AsSpan(i, 4).SequenceEqual("<!--"))
            {
                i += 4;
                int end = html.IndexOf("-->", i, StringComparison.Ordinal);
                var node = new HtmlNode { Type = NodeType.Comment };
                node.RawText = end < 0 ? html.Substring(i) : html.Substring(i, end - i);
                PushNode(node);
                i = end < 0 ? N : end + 3;
                continue;
            }

            if (i + 1 < N && html[i + 1] == '!')
            {
                int gt = html.IndexOf('>', i + 2);
                var node = new HtmlNode { Type = NodeType.Doctype };
                node.RawText = gt < 0 ? html.Substring(i + 2) : html.Substring(i + 2, gt - (i + 2));
                PushNode(node);
                i = gt < 0 ? N : gt + 1;
                continue;
            }

            if (i + 1 < N && html[i + 1] == '/')
            {
                i += 2;
                int nameStart = i;
                while (i < N && IsNameChar(html[i])) i++;
                string closeTag = html.Substring(nameStart, i - nameStart).ToLowerInvariant();
                while (i < N && html[i] != '>') i++;
                if (i < N) i++;
                while (st.Count > 1)
                {
                    var top = st.Pop();
                    if (top.Type == NodeType.Element && top.Tag == closeTag) break;
                }
                continue;
            }

            // open tag
            i++;
            int tns = i;
            while (i < N && IsNameChar(html[i])) i++;
            string tag = html.Substring(tns, i - tns).ToLowerInvariant();

            ImpliedEndOnStart(tag, st);

            var elem = new HtmlNode { Type = NodeType.Element, Tag = tag };
            SkipWs(html, ref i, N);
            ParseAttributes(html, ref i, N, elem.Attributes, elem.ClassListInternal);

            bool isRaw = tag is "script" or "style";
            bool selfClosing = false;
            if (i < N && html[i] == '/') { selfClosing = true; i++; }
            if (i < N && html[i] == '>') i++;

            bool isVoid = VoidTags.Contains(tag);
            if (!selfClosing && !isVoid)
            {
                if (isRaw)
                {
                    string endTag = "</" + tag + ">";
                    int closePos = html.IndexOf(endTag, i, StringComparison.OrdinalIgnoreCase);
                    if (closePos < 0)
                    {
                        elem.RawText = html.Substring(i);
                        i = N;
                    }
                    else
                    {
                        elem.RawText = html.Substring(i, closePos - i);
                        i = closePos + endTag.Length;
                    }

                    if (!ReferenceEquals(st.Peek(), root))
                        hoistedRaw.Add(elem);
                    else
                        PushNode(elem);
                }
                else
                {
                    PushNode(elem);
                    st.Push(elem);
                }
            }
            else
            {
                PushNode(elem);
            }
        }

        while (st.Count > 1) st.Pop();

        // hoist script/style nodes to the front of root (mirrors C++)
        for (int r = hoistedRaw.Count - 1; r >= 0; r--)
            AttachFront(root, hoistedRaw[r]);

        return root;
    }

    // ── private helpers ───────────────────────────────────────────────────────

    private static bool IsSpace(char c)
        => c == ' ' || c == '\n' || c == '\t' || c == '\r' || c == '\f';

    private static bool IsNameChar(char c)
        => char.IsLetterOrDigit(c) || c == '-' || c == ':' || c == '_';

    private static bool IsAttrNameChar(char c)
        => char.IsLetterOrDigit(c) || c == '-' || c == ':' || c == '_'
        || c == '*' || c == '@' || c == '.' || c == '[' || c == ']'
        || c == '(' || c == ')' || c == '#';

    private static void SkipWs(string s, ref int i, int N)
    {
        while (i < N && IsSpace(s[i])) i++;
    }

    private static void ParseAttributes(string s, ref int i, int N,
        Dictionary<string, string> attrs, List<string> classList)
    {
        while (i < N)
        {
            SkipWs(s, ref i, N);
            if (i >= N || s[i] == '>' || (s[i] == '/' && i + 1 < N && s[i + 1] == '>')) break;

            int ks = i;
            while (i < N && IsAttrNameChar(s[i])) i++;
            if (i == ks) { i++; continue; }
            string key = s.Substring(ks, i - ks).ToLowerInvariant();

            SkipWs(s, ref i, N);
            string value = string.Empty;
            if (i < N && s[i] == '=')
            {
                i++;
                SkipWs(s, ref i, N);
                if (i < N && (s[i] == '"' || s[i] == '\''))
                {
                    char q = s[i++];
                    int vs = i;
                    while (i < N && s[i] != q) i++;
                    value = s.Substring(vs, i - vs);
                    if (i < N) i++;
                }
                else
                {
                    int vs = i;
                    while (i < N && !IsSpace(s[i]) && s[i] != '>' && s[i] != '/') i++;
                    value = s.Substring(vs, i - vs);
                }
            }
            value = HtmlEntityDecode(value);
            attrs[key] = value;

            if (key == "class" && !string.IsNullOrEmpty(value))
            {
                int j = 0;
                while (j < value.Length)
                {
                    while (j < value.Length && IsSpace(value[j])) j++;
                    int start2 = j;
                    while (j < value.Length && !IsSpace(value[j])) j++;
                    if (start2 < j) classList.Add(value.Substring(start2, j - start2));
                }
            }
        }
    }

    private static void ImpliedEndOnStart(string newTag, Stack<HtmlNode> st)
    {
        int guard = 0;
        bool popped = true;
        while (popped && st.Count > 1 && guard++ < 32)
        {
            popped = false;
            var top = st.Peek();
            if (top.Type != NodeType.Element) break;
            string open = top.Tag;

            if (open == "p" && PCloseOn.Contains(newTag))         { st.Pop(); popped = true; continue; }
            if (open == "li" && newTag == "li")                    { st.Pop(); popped = true; continue; }
            if ((open == "dt" && (newTag == "dt" || newTag == "dd"))
             || (open == "dd" && (newTag == "dt" || newTag == "dd"))) { st.Pop(); popped = true; continue; }
            if (open == "tr" && newTag is "tr" or "tbody" or "thead" or "tfoot") { st.Pop(); popped = true; continue; }
            if ((open == "th" && (newTag == "th" || newTag == "td"))
             || (open == "td" && (newTag == "td" || newTag == "th"))) { st.Pop(); popped = true; continue; }
            if ((open == "option" && (newTag == "option" || newTag == "optgroup"))
             || (open == "optgroup" && newTag == "optgroup"))         { st.Pop(); popped = true; continue; }
        }
    }

    /// <summary>Decodes HTML entities: &amp;amp; &amp;lt; &amp;gt; &amp;quot; &amp;apos; numeric &amp;#NNN; and hex &amp;#xHH; forms.</summary>
    public static string HtmlEntityDecode(string s)
    {
        if (s.IndexOf('&') < 0) return s;
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length;)
        {
            if (s[i] != '&') { sb.Append(s[i++]); continue; }
            int semi = s.IndexOf(';', i + 1);
            if (semi < 0) { sb.Append(s[i++]); continue; }
            string ent = s.Substring(i + 1, semi - (i + 1));
            string? rep = ent switch
            {
                "amp"  => "&",
                "lt"   => "<",
                "gt"   => ">",
                "quot" => "\"",
                "apos" => "'",
                _      => null
            };
            if (rep == null && ent.Length > 1 && ent[0] == '#')
            {
                long code = 0;
                bool ok;
                if (ent.Length > 2 && (ent[1] == 'x' || ent[1] == 'X'))
                    ok = long.TryParse(ent.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out code);
                else
                    ok = long.TryParse(ent.AsSpan(1), out code);
                if (ok && code > 0 && code <= 0x10FFFF)
                    rep = char.ConvertFromUtf32((int)code);
            }
            if (rep == null) { sb.Append(s[i++]); continue; }
            sb.Append(rep);
            i = semi + 1;
        }
        return sb.ToString();
    }
}

// ─── HtmlQuerySelector ───────────────────────────────────────────────────────

/// <summary>
/// CSS selector matching: querySelector / querySelectorAll.
/// Supports: tag, #id, .class, [attr], [attr=val], [attr~=val], [attr^=val], [attr$=val],
/// [attr*=val], [attr|=val], :first-child, :last-child, :only-child, :nth-child(n),
/// :first-of-type, :last-of-type, :nth-of-type(n), :empty, :not(...),
/// descendant (space), child (>), adjacent (+), sibling (~) combinators.
/// </summary>
public static class HtmlQuerySelector
{
    // ── Simple selector ───────────────────────────────────────────────────────

    private enum AttrOp { Present, Equals, Includes, Prefix, Suffix, Substr, DashMatch }

    private sealed class AttrTest(string key, string val, AttrOp op)
    {
        public string Key { get; } = key;
        public string Val { get; } = val;
        public AttrOp Op  { get; } = op;
    }

    private sealed class SimpleSelector
    {
        public string Tag    { get; set; } = string.Empty;  // "" = any
        public string Id     { get; set; } = string.Empty;
        public List<string>   Classes { get; } = [];
        public List<AttrTest> Attrs   { get; } = [];
        public List<string>   Pseudos { get; } = [];
    }

    private enum Combinator { Descendant, Child, Adjacent, Sibling }

    private sealed class SelectorStep
    {
        public SimpleSelector Simple { get; set; } = new();
        public Combinator CombinatorToPrev { get; set; } = Combinator.Descendant;
    }

    private sealed class ParsedSelector
    {
        public List<SelectorStep> Steps { get; } = [];
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public static HtmlNode? QuerySelector(HtmlNode root, string selector)
    {
        var parts = SplitSelectorList(selector);
        foreach (var part in parts)
        {
            var parsed = ParseSelector(part.Trim());
            var result = FindFirst(root, parsed, root);
            if (result != null) return result;
        }
        return null;
    }

    public static List<HtmlNode> QuerySelectorAll(HtmlNode root, string selector)
    {
        var out_ = new List<HtmlNode>();
        var parts = SplitSelectorList(selector);
        var seen  = new HashSet<HtmlNode>(ReferenceEqualityComparer.Instance);
        foreach (var part in parts)
        {
            var parsed = ParseSelector(part.Trim());
            FindAll(root, parsed, root, out_, seen);
        }
        return out_;
    }

    // ── Traversal ─────────────────────────────────────────────────────────────

    private static HtmlNode? FindFirst(HtmlNode scope, ParsedSelector sel, HtmlNode root)
    {
        var stack = new Stack<HtmlNode>();
        PushChildren(scope, stack);
        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            if (MatchesAll(cur, sel, scope)) return cur;
            PushChildren(cur, stack);
        }
        return null;
    }

    private static void FindAll(HtmlNode scope, ParsedSelector sel, HtmlNode root,
        List<HtmlNode> out_, HashSet<HtmlNode> seen)
    {
        var stack = new Stack<HtmlNode>();
        PushChildren(scope, stack);
        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            if (MatchesAll(cur, sel, scope) && seen.Add(cur)) out_.Add(cur);
            PushChildren(cur, stack);
        }
    }

    private static void PushChildren(HtmlNode n, Stack<HtmlNode> stack)
    {
        // push in reverse so we visit in document order
        for (int k = n.ChildList.Count - 1; k >= 0; k--)
            stack.Push(n.ChildList[k]);
    }

    // ── Matching ──────────────────────────────────────────────────────────────

    private static bool MatchesAll(HtmlNode node, ParsedSelector sel, HtmlNode scope)
    {
        if (node.Type != NodeType.Element) return false;
        int stepIdx = sel.Steps.Count - 1;
        HtmlNode? cur = node;
        while (stepIdx >= 0)
        {
            var step = sel.Steps[stepIdx];
            if (cur == null || !MatchesSimple(cur, step.Simple)) return false;
            if (stepIdx == 0) return true;
            switch (step.CombinatorToPrev)
            {
                case Combinator.Descendant:
                    cur = cur.Parent;
                    while (cur != null && !MatchesSimple(cur, sel.Steps[stepIdx - 1].Simple))
                        cur = cur.Parent;
                    if (cur == null) return false;
                    break;
                case Combinator.Child:
                    cur = cur.Parent;
                    break;
                case Combinator.Adjacent:
                    cur = PrevElement(cur);
                    break;
                case Combinator.Sibling:
                    {
                        var sibling = PrevElement(cur);
                        while (sibling != null && !MatchesSimple(sibling, sel.Steps[stepIdx - 1].Simple))
                            sibling = PrevElement(sibling);
                        cur = sibling;
                    }
                    break;
            }
            stepIdx--;
        }
        return true;
    }

    private static HtmlNode? PrevElement(HtmlNode n)
    {
        var p = n.Prev;
        while (p != null && p.Type != NodeType.Element) p = p.Prev;
        return p;
    }

    private static bool MatchesSimple(HtmlNode node, SimpleSelector s)
    {
        if (node.Type != NodeType.Element) return false;
        if (!string.IsNullOrEmpty(s.Tag) && s.Tag != "*" && node.Tag != s.Tag) return false;
        if (!string.IsNullOrEmpty(s.Id)  && node.GetAttr("id") != s.Id) return false;
        foreach (var cls in s.Classes)
            if (!node.ClassListInternal.Contains(cls, StringComparer.Ordinal)) return false;
        foreach (var at in s.Attrs)
            if (!MatchAttr(node, at)) return false;
        foreach (var ps in s.Pseudos)
            if (!MatchPseudo(node, ps)) return false;
        return true;
    }

    private static bool MatchAttr(HtmlNode n, AttrTest at)
    {
        if (!n.Attributes.TryGetValue(at.Key, out var val))
            return false;  // attribute not present → no match regardless of op
        return at.Op switch
        {
            AttrOp.Present   => true,
            AttrOp.Equals    => val == at.Val,
            AttrOp.Includes  => Array.Exists(val.Split(' '), v => v == at.Val),
            AttrOp.Prefix    => val.StartsWith(at.Val, StringComparison.Ordinal),
            AttrOp.Suffix    => val.EndsWith(at.Val, StringComparison.Ordinal),
            AttrOp.Substr    => val.Contains(at.Val, StringComparison.Ordinal),
            AttrOp.DashMatch => val == at.Val || val.StartsWith(at.Val + "-", StringComparison.Ordinal),
            _                => false
        };
    }

    private static bool MatchPseudo(HtmlNode n, string pseudo)
    {
        if (pseudo.StartsWith("not(", StringComparison.OrdinalIgnoreCase))
        {
            var inner = pseudo.Substring(4, pseudo.Length - 5).Trim();
            var innerSel = ParseSelector(inner);
            return !MatchesAll(n, innerSel, n.DocumentRoot());
        }
        return pseudo switch
        {
            "first-child"    => n.IndexAmongElements() == 1,
            "last-child"     => n.IsLastChild(),
            "only-child"     => n.IsOnlyChild(),
            "empty"          => n.IsEmpty(),
            "first-of-type"  => n.IndexAmongType() == 1,
            "last-of-type"   => IsLastOfType(n),
            _                => NthChild(n, pseudo)
        };
    }

    private static bool IsLastOfType(HtmlNode n)
    {
        if (n.Parent == null) return true;
        for (int i = n.Parent.ChildList.Count - 1; i >= 0; i--)
        {
            var c = n.Parent.ChildList[i];
            if (c.Type == NodeType.Element && c.Tag == n.Tag)
                return ReferenceEquals(c, n);
        }
        return false;
    }

    private static bool NthChild(HtmlNode n, string pseudo)
    {
        if (pseudo.StartsWith("nth-child(", StringComparison.OrdinalIgnoreCase))
        {
            string arg = pseudo.Substring(10, pseudo.Length - 11).Trim();
            int idx = n.IndexAmongElements();
            return MatchNth(arg, idx);
        }
        if (pseudo.StartsWith("nth-of-type(", StringComparison.OrdinalIgnoreCase))
        {
            string arg = pseudo.Substring(12, pseudo.Length - 13).Trim();
            int idx = n.IndexAmongType();
            return MatchNth(arg, idx);
        }
        return false; // unknown pseudo — treat as non-matching
    }

    private static bool MatchNth(string arg, int idx)
    {
        if (arg == "odd")  return idx % 2 == 1;
        if (arg == "even") return idx % 2 == 0;
        int n = 0, b = 0;
        int nIdx = arg.IndexOf('n', StringComparison.OrdinalIgnoreCase);
        if (nIdx < 0) { int.TryParse(arg, out b); return idx == b; }
        string aPart = arg.Substring(0, nIdx).Trim();
        if (aPart == "" || aPart == "+") n = 1;
        else if (aPart == "-") n = -1;
        else int.TryParse(aPart, out n);
        string bPart = arg.Substring(nIdx + 1).Trim();
        if (bPart.Length > 0) int.TryParse(bPart, out b);
        if (n == 0) return idx == b;
        int rem = idx - b;
        return rem >= 0 && rem % n == 0;
    }

    // ── Parser ────────────────────────────────────────────────────────────────

    private static List<string> SplitSelectorList(string s)
    {
        var parts = new List<string>();
        var cur   = new StringBuilder();
        int paren = 0, bracket = 0;
        foreach (char ch in s)
        {
            if (ch == '(') paren++;
            else if (ch == ')') paren--;
            else if (ch == '[') bracket++;
            else if (ch == ']') bracket--;
            if (ch == ',' && paren == 0 && bracket == 0)
            {
                if (cur.Length > 0) { parts.Add(cur.ToString()); cur.Clear(); }
            }
            else cur.Append(ch);
        }
        if (cur.Length > 0) parts.Add(cur.ToString());
        return parts;
    }

    private static ParsedSelector ParseSelector(string selectorStr)
    {
        var sel    = new ParsedSelector();
        var tokens = Tokenize(selectorStr);
        var steps  = new List<(SimpleSelector ss, char comb)>();
        char lastComb = ' ';

        foreach (var tok in tokens)
        {
            if (tok is ">" or "+" or "~") { lastComb = tok[0]; continue; }
            var ss = ParseCompound(tok);
            steps.Add((ss, lastComb));
            lastComb = ' ';
        }

        for (int k = 0; k < steps.Count; k++)
        {
            var (ss, comb) = steps[k];
            var step = new SelectorStep { Simple = ss };
            if (k > 0)
                step.CombinatorToPrev = comb switch
                {
                    '>' => Combinator.Child,
                    '+' => Combinator.Adjacent,
                    '~' => Combinator.Sibling,
                    _   => Combinator.Descendant
                };
            sel.Steps.Add(step);
        }
        if (sel.Steps.Count == 0) sel.Steps.Add(new SelectorStep());
        return sel;
    }

    private static List<string> Tokenize(string s)
    {
        var tokens  = new List<string>();
        var cur     = new StringBuilder();
        int paren   = 0, bracket = 0;

        void Flush() { if (cur.Length > 0) { tokens.Add(cur.ToString()); cur.Clear(); } }

        foreach (char ch in s)
        {
            if (ch == '(') paren++;
            else if (ch == ')') paren--;
            else if (ch == '[') bracket++;
            else if (ch == ']') bracket--;

            if (paren == 0 && bracket == 0 && (ch == '>' || ch == '+' || ch == '~'))
            {
                Flush(); tokens.Add(ch.ToString());
            }
            else if (paren == 0 && bracket == 0 && char.IsWhiteSpace(ch))
            {
                Flush();
            }
            else cur.Append(ch);
        }
        Flush();
        return tokens;
    }

    private static SimpleSelector ParseCompound(string tok)
    {
        var s = new SimpleSelector();
        int i = 0;

        static bool IsIdentChar(char c)
            => char.IsLetterOrDigit(c) || c == '-' || c == '_';

        string ReadIdent()
        {
            int st = i;
            while (i < tok.Length && IsIdentChar(tok[i])) i++;
            return tok.Substring(st, i - st);
        }

        if (i < tok.Length)
        {
            if (tok[i] == '*') i++;
            else if (char.IsLetter(tok[i]))
            {
                s.Tag = ReadIdent().ToLowerInvariant();
            }
        }

        while (i < tok.Length)
        {
            char c = tok[i];
            if (c == '.')
            {
                i++;
                string cls = ReadIdent();
                if (!string.IsNullOrEmpty(cls)) s.Classes.Add(cls);
            }
            else if (c == '#')
            {
                i++;
                s.Id = ReadIdent();
            }
            else if (c == '[')
            {
                i++;
                while (i < tok.Length && char.IsWhiteSpace(tok[i])) i++;
                int ks = i;
                while (i < tok.Length && tok[i] != ']' && tok[i] != '=' &&
                       tok[i] != '~' && tok[i] != '^' && tok[i] != '$' &&
                       tok[i] != '*' && tok[i] != '|' && !char.IsWhiteSpace(tok[i])) i++;
                string key = tok.Substring(ks, i - ks).ToLowerInvariant();
                while (i < tok.Length && char.IsWhiteSpace(tok[i])) i++;

                AttrOp op = AttrOp.Present;
                string val = string.Empty;
                if (i < tok.Length && tok[i] != ']')
                {
                    string opStr;
                    if (i + 1 < tok.Length && tok[i + 1] == '=')
                    {
                        opStr = tok.Substring(i, 2); i += 2;
                    }
                    else if (tok[i] == '=') { opStr = "="; i++; }
                    else opStr = string.Empty;

                    op = opStr switch
                    {
                        "="  => AttrOp.Equals,
                        "~=" => AttrOp.Includes,
                        "^=" => AttrOp.Prefix,
                        "$=" => AttrOp.Suffix,
                        "*=" => AttrOp.Substr,
                        "|=" => AttrOp.DashMatch,
                        _    => AttrOp.Present
                    };
                    while (i < tok.Length && char.IsWhiteSpace(tok[i])) i++;
                    if (i < tok.Length && (tok[i] == '"' || tok[i] == '\''))
                    {
                        char q = tok[i++];
                        int vs = i;
                        while (i < tok.Length && tok[i] != q) i++;
                        val = tok.Substring(vs, i - vs);
                        if (i < tok.Length) i++;
                    }
                    else
                    {
                        int vs = i;
                        while (i < tok.Length && tok[i] != ']') i++;
                        val = tok.Substring(vs, i - vs).TrimEnd();
                    }
                }
                while (i < tok.Length && tok[i] != ']') i++;
                if (i < tok.Length) i++;
                s.Attrs.Add(new AttrTest(key, val, op));
            }
            else if (c == ':')
            {
                i++;
                if (i < tok.Length && tok[i] == ':') i++; // ::pseudo-element → ignore extra colon
                int ps = i;
                // read until another unescaped : or end, but keep parens balanced
                int depth = 0;
                while (i < tok.Length)
                {
                    if (tok[i] == '(') depth++;
                    else if (tok[i] == ')') { if (depth == 0) break; depth--; }
                    else if (tok[i] == ':' && depth == 0) break;
                    i++;
                }
                // include closing paren if depth was entered
                if (i < tok.Length && tok[i] == ')') i++;
                s.Pseudos.Add(tok.Substring(ps, i - ps).ToLowerInvariant());
            }
            else i++;
        }
        return s;
    }
}
