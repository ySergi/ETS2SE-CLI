using System.Text.RegularExpressions;

namespace Ets2SaveEditor.Core.Sii;

/// <summary>A single "key: value" line inside a unit. Lines that do not parse are kept verbatim.</summary>
public sealed partial class SiiAttribute
{
    private string? _rawLine;
    private string _value;

    public string Indent { get; }
    /// <summary>Full key including an optional index suffix, e.g. "visited_cities[3]". Null for unparsed lines.</summary>
    public string? Key { get; }

    public string Value
    {
        get => _value;
        set
        {
            if (Key is null) throw new InvalidOperationException("Cannot set the value of an unparsed line.");
            if (_value == value) return;
            _value = value;
            _rawLine = null;
        }
    }

    /// <summary>Base name without index, e.g. "visited_cities".</summary>
    public string? Name { get; }
    /// <summary>Index if the key is of the form name[i]; -1 for "name[]"; null otherwise.</summary>
    public int? ArrayIndex { get; }

    [GeneratedRegex(@"^(?<indent>\s*)(?<key>[A-Za-z0-9_]+(?:\[\d*\])?)\s*:\s?(?<value>.*)$")]
    private static partial Regex LineRegex();

    private SiiAttribute(string indent, string? key, string value, string? rawLine)
    {
        Indent = indent;
        Key = key;
        _value = value;
        _rawLine = rawLine;
        if (key is null) return;

        int b = key.IndexOf('[');
        if (b < 0)
        {
            Name = key;
        }
        else
        {
            Name = key[..b];
            var inner = key[(b + 1)..^1];
            ArrayIndex = inner.Length == 0 ? -1 : int.Parse(inner);
        }
    }

    public SiiAttribute(string key, string value, string indent = " ") : this(indent, key, value, null) { }

    public static SiiAttribute ParseLine(string line)
    {
        var m = LineRegex().Match(line);
        if (!m.Success) return new SiiAttribute("", null, line, line);
        return new SiiAttribute(m.Groups["indent"].Value, m.Groups["key"].Value, m.Groups["value"].Value, line);
    }

    public bool IsModified => _rawLine is null;

    public SiiAttribute Clone() =>
        Key is null ? new SiiAttribute("", null, _value, _rawLine) : new SiiAttribute(Indent, Key, _value, _rawLine);

    public string ToLine() => _rawLine ?? $"{Indent}{Key}: {_value}";

    public override string ToString() => ToLine();
}
