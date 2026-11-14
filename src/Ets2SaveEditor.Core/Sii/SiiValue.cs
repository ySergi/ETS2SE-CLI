using System.Globalization;
using System.Text;

namespace Ets2SaveEditor.Core.Sii;

/// <summary>Helpers for SII scalar values.</summary>
public static class SiiValue
{
    public static bool TryParseLong(string? s, out long value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return true;
        if (s.StartsWith('&') && TryParseFloat(s, out var f) && f == MathF.Floor(f) && Math.Abs(f) < 9e15)
        {
            value = (long)f;
            return true;
        }
        return false;
    }

    /// <summary>Parses either a decimal float ("0.5") or SCS hex float notation ("&amp;3f000000").</summary>
    public static bool TryParseFloat(string? s, out float value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        if (s[0] == '&')
        {
            if (uint.TryParse(s.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint bits))
            {
                value = BitConverter.UInt32BitsToSingle(bits);
                return true;
            }
            return false;
        }
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Formats a float the way the game does: integers as plain numbers, everything else as hex bits.</summary>
    public static string FormatFloat(float value)
    {
        if (float.IsFinite(value) && value == MathF.Floor(value) && Math.Abs(value) < 1e7f)
            return ((long)value).ToString(CultureInfo.InvariantCulture);
        return "&" + BitConverter.SingleToUInt32Bits(value).ToString("x8", CultureInfo.InvariantCulture);
    }

    public static string Quote(string s)
    {
        var sb = new StringBuilder(s.Length + 2).Append('"');
        foreach (var c in s)
        {
            if (c is '"' or '\\') sb.Append('\\');
            sb.Append(c);
        }
        return sb.Append('"').ToString();
    }

    public static string Unquote(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Length < 2 || s[0] != '"' || s[^1] != '"') return s;
        var inner = s[1..^1];
        if (!inner.Contains('\\')) return inner;
        var sb = new StringBuilder(inner.Length);
        for (int i = 0; i < inner.Length; i++)
        {
            if (inner[i] == '\\' && i + 1 < inner.Length) i++;
            sb.Append(inner[i]);
        }
        return sb.ToString();
    }

    public static bool IsNull(string? s) => string.IsNullOrEmpty(s) || s is "null" or "nil";

    /// <summary>True if a value is a valid unquoted SII token (e.g. city names).</summary>
    public static bool IsToken(string s) =>
        s.Length is > 0 and <= 12 * 4 && s.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '.');
}
