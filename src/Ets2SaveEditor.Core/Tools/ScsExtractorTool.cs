namespace Ets2SaveEditor.Core.Tools;

/// <summary>Wrapper around SCS Software's official Game Archive Extractor (scs_extractor.exe).</summary>
public sealed class ScsExtractorTool(string exePath)
{
    public string ExePath { get; } = exePath;

    /// <summary>Extracts a whole .scs archive into <paramref name="outputDir"/>.</summary>
    public ProcessResult Extract(string archive, string outputDir)
    {
        if (!File.Exists(archive)) throw new FileNotFoundException("Archive not found.", archive);
        Directory.CreateDirectory(outputDir);

        // Run from a scratch working directory: SCS advises against running it inside the game folder.
        var r = ProcessRunner.Run(ExePath, [Path.GetFullPath(archive), Path.GetFullPath(outputDir)],
            workingDir: outputDir, timeout: TimeSpan.FromMinutes(15));

        bool anyFile = Directory.EnumerateFileSystemEntries(outputDir).Any();
        if (r.ExitCode != 0 || !anyFile)
        {
            var msg = (r.StdOut + r.StdErr).Trim();
            throw new InvalidOperationException(
                $"scs_extractor failed on '{Path.GetFileName(archive)}' (exit {r.ExitCode}). {msg}");
        }
        return r;
    }
}
