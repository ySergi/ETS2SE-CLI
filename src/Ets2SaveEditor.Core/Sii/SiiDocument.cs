using System.Text;
using System.Text.RegularExpressions;

namespace Ets2SaveEditor.Core.Sii;

/// <summary>
/// Lossless, line-based model of a plain-text SII ("SiiNunit") file.
/// Unmodified lines are written back byte-for-byte, so a parse/serialize
/// round trip without edits reproduces the original text exactly.
/// </summary>
public sealed partial class SiiDocument
{
    private readonly List<object> _nodes = new(); // string (raw line) or SiiUnit
    private Dictionary<string, SiiUnit>? _index;

    public string NewLine { get; private set; } = "\r\n";
    public bool TrailingNewLine { get; private set; }

    public IEnumerable<SiiUnit> Units => _nodes.OfType<SiiUnit>();

    [GeneratedRegex(@"^\s*(?<type>[^\s:{}]+)\s*:\s*(?<id>[^\s{}]+)\s*\{\s*$")]
    private static partial Regex UnitHeaderRegex();

    public static bool LooksLikePlainText(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith("\uFEFF"u8)) head = head[3..];
        return head.StartsWith("SiiNunit"u8);
    }

    public static SiiDocument Load(string path) => Parse(File.ReadAllText(path, Encoding.UTF8));

    public void Save(string path)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, Serialize(), new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }

    public static SiiDocument Parse(string text)
    {
        var doc = new SiiDocument();
        if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];

        doc.NewLine = text.Contains("\r\n") ? "\r\n" : "\n";
        doc.TrailingNewLine = text.EndsWith('\n');

        var lines = text.Split('\n');
        int count = lines.Length;
        if (doc.TrailingNewLine) count--; // last element is the empty string after the final '\n'

        SiiUnit? current = null;
        for (int i = 0; i < count; i++)
        {
            var line = lines[i];
            if (line.EndsWith('\r')) line = line[..^1];

            if (current is null)
            {
                var m = UnitHeaderRegex().Match(line);
                if (m.Success)
                {
                    current = new SiiUnit(m.Groups["type"].Value, m.Groups["id"].Value, line);
                    doc._nodes.Add(current);
                }
                else
                {
                    doc._nodes.Add(line);
                }
            }
            else if (line.Trim() == "}")
            {
                current.FooterLine = line;
                current = null;
            }
            else
            {
                current.Attributes.Add(SiiAttribute.ParseLine(line));
            }
        }

        if (current is not null)
            throw new FormatException($"Unterminated unit '{current.Type} : {current.Id}'.");

        return doc;
    }

    public string Serialize()
    {
        var sb = new StringBuilder(1 << 20);
        bool first = true;
        void Line(string s)
        {
            if (!first) sb.Append(NewLine);
            sb.Append(s);
            first = false;
        }

        foreach (var node in _nodes)
        {
            if (node is string raw)
            {
                Line(raw);
                continue;
            }

            var unit = (SiiUnit)node;
            Line(unit.HeaderLine ?? $"{unit.Type} : {unit.Id} {{");
            foreach (var attr in unit.Attributes) Line(attr.ToLine());
            Line(unit.FooterLine ?? "}");
        }

        if (TrailingNewLine) sb.Append(NewLine);
        return sb.ToString();
    }

    private Dictionary<string, SiiUnit> Index
    {
        get
        {
            if (_index is null)
            {
                _index = new Dictionary<string, SiiUnit>(StringComparer.Ordinal);
                foreach (var u in Units) _index.TryAdd(u.Id, u);
            }
            return _index;
        }
    }

    public SiiUnit? FindUnit(string? id)
    {
        if (string.IsNullOrEmpty(id) || id is "null" or "nil") return null;
        return Index.GetValueOrDefault(id);
    }

    public SiiUnit GetUnit(string id) =>
        FindUnit(id) ?? throw new KeyNotFoundException($"Unit '{id}' not found in save.");

    public IEnumerable<SiiUnit> UnitsOfType(string type) => Units.Where(u => u.Type == type);

    public SiiUnit? FirstOfType(string type) => Units.FirstOrDefault(u => u.Type == type);

    public bool RemoveUnit(SiiUnit unit)
    {
        int idx = _nodes.IndexOf(unit);
        if (idx < 0) return false;
        _nodes.RemoveAt(idx);
        // Units are separated by an empty line; drop the separator that followed it.
        if (idx < _nodes.Count && _nodes[idx] is string s && s.Length == 0)
            _nodes.RemoveAt(idx);
        _index = null;
        return true;
    }

    public void InsertUnitAfter(SiiUnit anchor, SiiUnit unit)
    {
        int idx = _nodes.IndexOf(anchor);
        if (idx < 0) throw new InvalidOperationException("Anchor unit not part of this document.");
        _nodes.Insert(idx + 1, "");
        _nodes.Insert(idx + 2, unit);
        _index = null;
    }
}
