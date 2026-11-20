using System.Globalization;

namespace Ets2SaveEditor.Core.Sii;

public sealed class SiiUnit
{
    public SiiUnit(string type, string id, string? headerLine = null)
    {
        Type = type;
        Id = id;
        HeaderLine = headerLine;
    }

    public string Type { get; }
    public string Id { get; }
    internal string? HeaderLine { get; set; }
    internal string? FooterLine { get; set; }
    public List<SiiAttribute> Attributes { get; } = new();

    public override string ToString() => $"{Type} : {Id}";

    public SiiUnit CloneWithId(string newId)
    {
        var copy = new SiiUnit(Type, newId, headerLine: null) { FooterLine = FooterLine };
        foreach (var attr in Attributes)
            copy.Attributes.Add(attr.Clone());
        return copy;
    }

    private int IndexOfKey(string key)
    {
        for (int i = 0; i < Attributes.Count; i++)
            if (Attributes[i].Key == key) return i;
        return -1;
    }

    public bool Has(string key) => IndexOfKey(key) >= 0;

    public string? Get(string key)
    {
        int i = IndexOfKey(key);
        return i < 0 ? null : Attributes[i].Value;
    }

    public string GetRequired(string key) =>
        Get(key) ?? throw new KeyNotFoundException($"Attribute '{key}' not found in unit '{Id}'.");

    /// <summary>Sets an existing attribute, or appends it when <paramref name="addIfMissing"/> is true.</summary>
    public void Set(string key, string value, bool addIfMissing = false)
    {
        int i = IndexOfKey(key);
        if (i >= 0)
        {
            Attributes[i].Value = value;
            return;
        }
        if (!addIfMissing)
            throw new KeyNotFoundException($"Attribute '{key}' not found in unit '{Id}'.");
        Attributes.Add(new SiiAttribute(key, value, IndentGuess()));
    }

    public bool TrySet(string key, string value)
    {
        int i = IndexOfKey(key);
        if (i < 0) return false;
        Attributes[i].Value = value;
        return true;
    }

    public long? GetLong(string key) => SiiValue.TryParseLong(Get(key), out var v) ? v : null;
    public float? GetFloat(string key) => SiiValue.TryParseFloat(Get(key), out var v) ? v : null;
    public bool? GetBool(string key) => Get(key) switch { "true" => true, "false" => false, _ => null };

    public void SetLong(string key, long value) => Set(key, value.ToString(CultureInfo.InvariantCulture));
    public void SetFloat(string key, float value) => Set(key, SiiValue.FormatFloat(value));

    public bool IsArray(string name)
    {
        var count = Get(name);
        if (count is not null) return int.TryParse(count, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        return Attributes.Any(a => a.Name == name && a.ArrayIndex == -1);
    }

    /// <summary>Returns the values of an array attribute ("name: N" followed by "name[i]: v" lines).</summary>
    public List<string> GetArray(string name)
    {
        var result = new List<string>();
        var count = Get(name);
        if (count is not null && int.TryParse(count, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
        {
            var byIndex = new Dictionary<int, string>(n);
            foreach (var a in Attributes)
                if (a.Name == name && a.ArrayIndex is int idx and >= 0)
                    byIndex[idx] = a.Value;
            for (int i = 0; i < n; i++)
                result.Add(byIndex.GetValueOrDefault(i, ""));
            return result;
        }

        foreach (var a in Attributes)
            if (a.Name == name && a.ArrayIndex == -1)
                result.Add(a.Value);
        return result;
    }

    /// <summary>Replaces an array attribute, keeping its position within the unit.</summary>
    public void SetArray(string name, IReadOnlyCollection<string> values, bool addIfMissing = false)
    {
        int countIdx = IndexOfKey(name);
        if (countIdx < 0 && !addIfMissing)
            throw new KeyNotFoundException($"Array attribute '{name}' not found in unit '{Id}'.");

        string indent = countIdx >= 0 ? Attributes[countIdx].Indent : IndentGuess();

        // Remove existing items (and count line), remember the insertion point.
        int insertAt = countIdx >= 0 ? countIdx : Attributes.Count;
        for (int i = Attributes.Count - 1; i >= 0; i--)
        {
            var a = Attributes[i];
            if (a.Name == name && (a.ArrayIndex is not null || a.Key == name))
            {
                Attributes.RemoveAt(i);
                if (i < insertAt) insertAt--;
            }
        }
        if (insertAt > Attributes.Count) insertAt = Attributes.Count;

        var newAttrs = new List<SiiAttribute>(values.Count + 1)
        {
            new(name, values.Count.ToString(CultureInfo.InvariantCulture), indent)
        };
        int k = 0;
        foreach (var v in values)
            newAttrs.Add(new SiiAttribute($"{name}[{k++}]", v, indent));
        Attributes.InsertRange(insertAt, newAttrs);
    }

    private string IndentGuess() => Attributes.Count > 0 ? Attributes[0].Indent : " ";
}
