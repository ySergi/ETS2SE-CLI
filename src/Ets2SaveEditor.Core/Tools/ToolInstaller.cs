using System.IO.Compression;

namespace Ets2SaveEditor.Core.Tools;

/// <summary>Downloads SII_Decrypt and scs_extractor into the tools directory.</summary>
public static class ToolInstaller
{
    private const string SiiDecryptBase = "https://raw.githubusercontent.com/TheLazyTomcat/SII_Decrypt/bin/";
    private const string SiiDecryptExe = SiiDecryptBase + "Program_Console/Lazarus/Release/win_x64/SII_Decrypt.exe";
    private const string ScsExtractorZip = "https://download.eurotrucksimulator2.com/scs_extractor_1_55.zip";

    public static async Task InstallAsync(string toolsDir, bool force, Action<string> log, CancellationToken ct = default)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ets2se/1.0");

        var siiDir = Path.Combine(toolsDir, "SII_Decrypt");
        var siiExe = Path.Combine(siiDir, "SII_Decrypt.exe");
        if (force || !File.Exists(siiExe))
        {
            Directory.CreateDirectory(siiDir);
            log($"Downloading SII_Decrypt -> {siiExe}");
            await DownloadAsync(http, SiiDecryptExe, siiExe, ct);
            await DownloadAsync(http, SiiDecryptBase + "license.txt", Path.Combine(siiDir, "license.txt"), ct);
            await DownloadAsync(http, SiiDecryptBase + "program_readme.txt", Path.Combine(siiDir, "program_readme.txt"), ct);
        }
        else log($"SII_Decrypt already installed at {siiExe}");

        var scsDir = Path.Combine(toolsDir, "scs_extractor");
        var scsExe = Path.Combine(scsDir, "scs_extractor.exe");
        if (force || !File.Exists(scsExe))
        {
            Directory.CreateDirectory(scsDir);
            var zip = Path.Combine(scsDir, "scs_extractor.zip");
            log($"Downloading scs_extractor -> {scsExe}");
            await DownloadAsync(http, ScsExtractorZip, zip, ct);
            await ZipFile.ExtractToDirectoryAsync(zip, scsDir, overwriteFiles: true, ct);
            File.Delete(zip);
        }
        else log($"scs_extractor already installed at {scsExe}");
    }

    private static async Task DownloadAsync(HttpClient http, string url, string target, CancellationToken ct)
    {
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var tmp = target + ".part";
        await using (var fs = File.Create(tmp))
            await resp.Content.CopyToAsync(fs, ct);
        File.Move(tmp, target, overwrite: true);
    }
}
