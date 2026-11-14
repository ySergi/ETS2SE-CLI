using Ets2SaveEditor.Core.Sii;

namespace Ets2SaveEditor.Core.Tools;

public enum SiiFileFormat { PlainText, Encrypted, Binary, ThreeNK, Unknown }

/// <summary>Wrapper around TheLazyTomcat's SII_Decrypt console program.</summary>
public sealed class SiiDecryptTool(string exePath)
{
    public string ExePath { get; } = exePath;

    public static SiiFileFormat DetectFormat(string path)
    {
        Span<byte> head = stackalloc byte[11];
        using var fs = File.OpenRead(path);
        int n = fs.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        var span = head[..n];
        if (SiiDocument.LooksLikePlainText(span)) return SiiFileFormat.PlainText;
        if (span.StartsWith("ScsC"u8)) return SiiFileFormat.Encrypted;
        if (span.StartsWith("BSII"u8)) return SiiFileFormat.Binary;
        if (span.StartsWith("\u00033nK"u8) || (n >= 3 && span[..3].SequenceEqual("3nK"u8))) return SiiFileFormat.ThreeNK;
        return SiiFileFormat.Unknown;
    }

    /// <summary>
    /// Decrypts and/or decodes <paramref name="input"/> to plain-text SII at <paramref name="output"/>.
    /// Plain-text input is simply copied.
    /// </summary>
    public void DecryptTo(string input, string output)
    {
        if (DetectFormat(input) == SiiFileFormat.PlainText)
        {
            File.Copy(input, output, overwrite: true);
            return;
        }

        var dir = Path.GetDirectoryName(Path.GetFullPath(output));
        if (dir is not null) Directory.CreateDirectory(dir);

        var r = ProcessRunner.Run(ExePath, [input, output], timeout: TimeSpan.FromMinutes(5));
        switch (r.ExitCode)
        {
            case 0:
                break;
            case 1: // SIIDEC_RESULT_FORMAT_PLAINTEXT
                File.Copy(input, output, overwrite: true);
                break;
            default:
                throw new SiiDecryptException(r.ExitCode, Describe(r.ExitCode), r.StdOut);
        }

        if (!File.Exists(output) || DetectFormat(output) != SiiFileFormat.PlainText)
            throw new SiiDecryptException(r.ExitCode, "SII_Decrypt did not produce a plain-text SII file.", r.StdOut);
    }

    public static string Describe(int code) => code switch
    {
        -1 => "generic error",
        0 => "success",
        1 => "file is already plain text",
        2 => "file is encrypted",
        3 => "file is in binary format",
        4 => "file is 3nK-encoded",
        10 => "unknown file format",
        11 => "file is too small / truncated",
        12 => "buffer too small",
        _ => $"unexpected result code {code}",
    };
}

public sealed class SiiDecryptException(int code, string reason, string output)
    : Exception($"SII_Decrypt failed ({code}): {reason}.{(string.IsNullOrWhiteSpace(output) ? "" : Environment.NewLine + output.Trim())}")
{
    public int Code { get; } = code;
}
