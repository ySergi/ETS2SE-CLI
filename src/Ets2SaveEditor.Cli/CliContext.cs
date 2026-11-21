using Ets2SaveEditor.Core.Game;
using Ets2SaveEditor.Core.Profiles;
using Ets2SaveEditor.Core.Save;
using Ets2SaveEditor.Core.Tools;

namespace Ets2SaveEditor.Cli;

internal sealed class CliOptions
{
    public string? Profile { get; set; }
    public string SaveSlot { get; set; } = "autosave";
    public string? FilePath { get; set; }
    public string? ToolsDir { get; set; }
    public bool NoBackup { get; set; }
    public bool DryRun { get; set; }
    public bool Help { get; set; }
    public List<string> Positionals { get; } = [];
    public Dictionary<string, List<string>> Flags { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasFlag(string name) => Flags.ContainsKey(name);

    public string? GetFlag(string name) =>
        Flags.TryGetValue(name, out var list) && list.Count > 0 ? list[^1] : null;

    public IReadOnlyList<string> GetFlags(string name) =>
        Flags.TryGetValue(name, out var list) ? list : [];

    public static CliOptions Parse(string[] args)
    {
        var opt = new CliOptions();
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a is "-h" or "--help" or "-?")
            {
                opt.Help = true;
                continue;
            }

            if (a == "--")
            {
                for (int j = i + 1; j < args.Length; j++) opt.Positionals.Add(args[j]);
                break;
            }

            if (a is "-p" or "--profile") { opt.Profile = NextVal(args, ref i, a); continue; }
            if (a is "-s" or "--save") { opt.SaveSlot = NextVal(args, ref i, a); continue; }
            if (a is "-f" or "--file") { opt.FilePath = NextVal(args, ref i, a); continue; }
            if (a == "--tools-dir") { opt.ToolsDir = NextVal(args, ref i, a); continue; }
            if (a == "--no-backup") { opt.NoBackup = true; continue; }
            if (a == "--dry-run") { opt.DryRun = true; continue; }
            if (a == "-i") { AddFlag(opt, "interactive", ""); continue; }

            if (a.StartsWith("--", StringComparison.Ordinal))
            {
                var eq = a.IndexOf('=');
                if (eq > 2)
                {
                    AddFlag(opt, a[2..eq], a[(eq + 1)..]);
                }
                else
                {
                    var key = a[2..];
                    // Known boolean flags never consume a following positional argument.
                    if (!BooleanFlags.Contains(key) && i + 1 < args.Length && !LooksLikeFlag(args[i + 1]))
                        AddFlag(opt, key, args[++i]);
                    else
                        AddFlag(opt, key, "");
                }
                continue;
            }

            opt.Positionals.Add(a);
        }
        return opt;
    }

    private static readonly HashSet<string> BooleanFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "all", "interactive", "with-prerequisites", "overwrite", "force", "skip-intros",
        "no-log", "completed", "incomplete", "clear-log", "sync", "with-dlcs", "no-maps",
        "base-only", "all-dlcs", "no-secret", "keep-permanent", "owned", "available",
        "assign-truck", "only-with-truck", "spread", "max-skills", "add",
        "from-visited", "all-garages", "all-companies",
        "initial-only", "event-only", "ready", "pending",
        "no-academy", "no-roads", "include-bus", "with-feats",
    };

    private static bool LooksLikeFlag(string s) =>
        s.Length >= 2 && s[0] == '-' && !char.IsAsciiDigit(s[1]);

    private static void AddFlag(CliOptions opt, string key, string val)
    {
        if (!opt.Flags.TryGetValue(key, out var list))
        {
            list = [];
            opt.Flags[key] = list;
        }
        if (val.Length > 0) list.Add(val);
    }

    private static string NextVal(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length) throw new CliUsageException($"Option '{flag}' requires a value.");
        return args[++i];
    }
}

internal sealed class CliUsageException(string message) : Exception(message);

internal sealed class CliContext(CliOptions options)
{
    public CliOptions Options { get; } = options;
    public ToolLocator Tools { get; } = new(options.ToolsDir);

    public SiiDecryptTool? TryDecryptTool() =>
        Tools.FindSiiDecrypt() is { } p ? new SiiDecryptTool(p) : null;

    public SiiDecryptTool RequireDecryptTool() => new(Tools.RequireSiiDecrypt());
    public ScsExtractorTool RequireScsExtractor() => new(Tools.RequireScsExtractor());

    public string ResolveSaveDirectory()
    {
        if (!string.IsNullOrWhiteSpace(Options.FilePath))
        {
            var full = Path.GetFullPath(Options.FilePath);
            if (Directory.Exists(full)) return full;
            if (File.Exists(full)) return Path.GetDirectoryName(full)!;
            throw new FileNotFoundException($"Save path not found: '{full}'.");
        }

        var profile = ProfileLocator.Resolve(Options.Profile);
        var slot = profile.FindSave(Options.SaveSlot);
        if (slot is not null) return slot.Directory;

        var direct = Path.Combine(profile.SaveRoot, Options.SaveSlot);
        if (File.Exists(Path.Combine(direct, "game.sii"))) return direct;

        var avail = string.Join(", ", profile.GetSaves().Select(s => s.Name));
        throw new InvalidOperationException(
            $"Save slot '{Options.SaveSlot}' not found for profile '{profile.DisplayName}' ({profile.Id}). Available: {(avail.Length > 0 ? avail : "(none)")}.");
    }

    public Ets2Save LoadSave()
    {
        var decrypt = TryDecryptTool();
        if (!string.IsNullOrWhiteSpace(Options.FilePath) && File.Exists(Options.FilePath))
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(Options.FilePath))!;
            var info = Path.Combine(dir, "info.sii");
            return Ets2Save.LoadFromFile(Options.FilePath, File.Exists(info) ? info : null, decrypt);
        }
        return Ets2Save.LoadFromSaveDirectory(ResolveSaveDirectory(), decrypt);
    }

    public void Commit(Ets2Save save, string changeSummary)
    {
        if (Options.DryRun)
        {
            Console.WriteLine($"[dry-run] {changeSummary} (no files written)");
            return;
        }
        var backup = save.Save(createBackup: !Options.NoBackup);
        Console.WriteLine(changeSummary);
        if (backup is not null)
            Console.WriteLine($"Backup saved: {backup.Id} ({backup.Directory})");
    }

    public LevelTable GetLevelTable() => GameCatalog.LoadLevelTable();

    public static long ParseLong(string raw, string name) =>
        long.TryParse(raw, out var v) ? v : throw new CliUsageException($"Invalid integer for {name}: '{raw}'.");

    public static int ParseInt(string raw, string name) =>
        int.TryParse(raw, out var v) ? v : throw new CliUsageException($"Invalid integer for {name}: '{raw}'.");

    public static float ParseFloat(string raw, string name) =>
        float.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? v
            : throw new CliUsageException($"Invalid number for {name}: '{raw}'.");
}
