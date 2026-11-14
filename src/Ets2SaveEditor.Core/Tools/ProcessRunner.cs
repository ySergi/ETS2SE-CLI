using System.Diagnostics;
using System.Text;

namespace Ets2SaveEditor.Core.Tools;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);

public static class ProcessRunner
{
    public static ProcessResult Run(string exe, IEnumerable<string> args, string? workingDir = null, TimeSpan? timeout = null)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDir ?? Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start '{exe}'.");
        p.StandardInput.Close(); // tools must never wait for keyboard input
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();

        var limit = timeout ?? TimeSpan.FromMinutes(10);
        if (!p.WaitForExit(limit))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw new TimeoutException($"'{Path.GetFileName(exe)}' did not finish within {limit.TotalSeconds:0}s.");
        }
        p.WaitForExit();
        return new ProcessResult(p.ExitCode, stdout.Result, stderr.Result);
    }
}
