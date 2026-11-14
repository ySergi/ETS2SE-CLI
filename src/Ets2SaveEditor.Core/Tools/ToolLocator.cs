namespace Ets2SaveEditor.Core.Tools;

/// <summary>Locates the external helper tools (SII_Decrypt and scs_extractor).</summary>
public sealed class ToolLocator
{
    public const string DefaultToolsDir = @"C:\Tools\ETS2";
    public const string ToolsDirEnvVar = "ETS2SE_TOOLS_DIR";

    public ToolLocator(string? toolsDir = null)
    {
        ToolsDir = toolsDir
                   ?? Environment.GetEnvironmentVariable(ToolsDirEnvVar)
                   ?? DefaultToolsDir;
    }

    public string ToolsDir { get; }

    public string? FindSiiDecrypt() => Find("SII_Decrypt.exe", "SII_Decrypt");
    public string? FindScsExtractor() => Find("scs_extractor.exe", "scs_extractor");

    public string RequireSiiDecrypt() => FindSiiDecrypt() ?? throw Missing("SII_Decrypt.exe", "SII_Decrypt");
    public string RequireScsExtractor() => FindScsExtractor() ?? throw Missing("scs_extractor.exe", "scs_extractor");

    private string? Find(string exe, string subDir)
    {
        string[] candidates =
        [
            Path.Combine(ToolsDir, subDir, exe),
            Path.Combine(ToolsDir, exe),
        ];
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        if (Directory.Exists(ToolsDir))
        {
            var hit = Directory.EnumerateFiles(ToolsDir, exe, SearchOption.AllDirectories).FirstOrDefault();
            if (hit is not null) return hit;
        }
        return null;
    }

    private ToolNotFoundException Missing(string exe, string subDir) =>
        new($"{exe} was not found. Expected it at '{Path.Combine(ToolsDir, subDir, exe)}'. " +
            $"Run 'ets2se tools install' or set {ToolsDirEnvVar} / --tools-dir.");
}

public sealed class ToolNotFoundException(string message) : Exception(message);
