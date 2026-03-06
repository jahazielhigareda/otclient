using System.Text.RegularExpressions;

namespace OTClient.Framework.UI;

/// <summary>
/// Parses OTUI/OTSS style files — the simple key–value format used by OTClient
/// to describe widget visual properties.
/// <para>
/// Format example:
/// <code>
/// UIButton#myBtn
///   text: Click me
///   color: #ff0000
///   width: 120
///   height: 24
///
/// UILabel
///   text: Hello world
///   margin: 4 8
/// </code>
/// Each block starts with a type header (<c>TypeName</c> or <c>TypeName#id</c>)
/// and contains indented <c>key: value</c> pairs.
/// </para>
/// Maps to <c>src/framework/ui/uimanager.*</c>.
/// Task 7.7.
/// </summary>
public sealed class OtuiParser
{
    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Represents one parsed OTUI block (type + optional id + properties).</summary>
    public sealed record StyleBlock(
        string TypeName,
        string? Id,
        IReadOnlyDictionary<string, string> Properties);

    /// <summary>
    /// Parses the given OTUI text and returns all style blocks found.
    /// </summary>
    public static IReadOnlyList<StyleBlock> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var results = new List<StyleBlock>();
        StyleBlock? current = null;
        Dictionary<string, string>? props = null;

        foreach (var rawLine in text.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');

            // Skip comments and blank lines
            if (line.TrimStart().StartsWith("//") || string.IsNullOrWhiteSpace(line))
                continue;

            bool indented = line.Length > 0 && (line[0] == ' ' || line[0] == '\t');

            if (!indented)
            {
                // Flush previous block
                if (current is not null && props is not null)
                    results.Add(current with { Properties = props });

                // Parse header: "TypeName" or "TypeName#id"
                var header = line.Trim();
                string typeName, id;
                int hashIdx = header.IndexOf('#');
                if (hashIdx >= 0)
                {
                    typeName = header[..hashIdx].Trim();
                    id       = header[(hashIdx + 1)..].Trim();
                }
                else
                {
                    typeName = header;
                    id       = string.Empty;
                }

                current = new StyleBlock(typeName, id.Length > 0 ? id : null, new Dictionary<string, string>());
                props   = [];
            }
            else if (current is not null && props is not null)
            {
                // Key-value pair
                var trimmed = line.Trim();
                int colon = trimmed.IndexOf(':');
                if (colon < 0) continue;

                string key   = trimmed[..colon].Trim();
                string value = trimmed[(colon + 1)..].Trim();
                props[key]   = value;
            }
        }

        // Flush last block
        if (current is not null && props is not null)
            results.Add(current with { Properties = props });

        return results;
    }

    // ─── Property coercions ───────────────────────────────────────────────────

    /// <summary>Tries to read an integer property. Returns <paramref name="defaultValue"/> when absent.</summary>
    public static int GetInt(IReadOnlyDictionary<string, string> props, string key, int defaultValue = 0)
        => props.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : defaultValue;

    /// <summary>Tries to read a float property. Returns <paramref name="defaultValue"/> when absent.</summary>
    public static float GetFloat(IReadOnlyDictionary<string, string> props, string key, float defaultValue = 0f)
        => props.TryGetValue(key, out var v) && float.TryParse(v, System.Globalization.NumberStyles.Float,
               System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : defaultValue;

    /// <summary>Tries to read a boolean property. Returns <paramref name="defaultValue"/> when absent.</summary>
    public static bool GetBool(IReadOnlyDictionary<string, string> props, string key, bool defaultValue = false)
        => props.TryGetValue(key, out var v) ? v.Equals("true", StringComparison.OrdinalIgnoreCase) : defaultValue;

    /// <summary>Returns a string property, or <paramref name="defaultValue"/>.</summary>
    public static string GetString(IReadOnlyDictionary<string, string> props, string key, string defaultValue = "")
        => props.TryGetValue(key, out var v) ? v : defaultValue;

    // ─── Colour parsing ───────────────────────────────────────────────────────

    private static readonly Regex s_hexColor = new(@"^#([0-9a-fA-F]{6,8})$", RegexOptions.Compiled);

    /// <summary>
    /// Parses a CSS-style hex colour string (<c>#RRGGBB</c> or <c>#RRGGBBAA</c>)
    /// into a Raylib <see cref="Raylib_cs.Color"/>.
    /// </summary>
    public static Raylib_cs.Color ParseColor(string hex, Raylib_cs.Color fallback = default)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        var m = s_hexColor.Match(hex);
        if (!m.Success) return fallback;

        var g = m.Groups[1].Value;
        byte r = Convert.ToByte(g[..2], 16);
        byte gr = Convert.ToByte(g[2..4], 16);
        byte b  = Convert.ToByte(g[4..6], 16);
        byte a  = g.Length == 8 ? Convert.ToByte(g[6..8], 16) : (byte)255;
        return new Raylib_cs.Color(r, gr, b, a);
    }
}
