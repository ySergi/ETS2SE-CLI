using Ets2SaveEditor.Core.Game;
using Ets2SaveEditor.Core.Save;
using Ets2SaveEditor.Core.Tools;

namespace Ets2SaveEditor.Cli.Commands;

internal static class SystemCommands
{
    public static int Raw(CliContext ctx, string[] args)
    {
        if (args.Length == 0) throw new CliUsageException("Usage: ets2se raw <get|set|find|decrypt> ...");
        var sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "decrypt":
            {
                CoreCommands.RequireArg(args, 1, "raw decrypt <input.sii> [output.sii]");
                var input = Path.GetFullPath(args[1]);
                var output = args.Length > 2 ? Path.GetFullPath(args[2]) : input;
                var tool = ctx.RequireDecryptTool();
                tool.DecryptTo(input, output);
                Console.WriteLine($"Decrypted '{input}' -> '{output}'.");
                return 0;
            }

            case "find":
            {
                CoreCommands.RequireArg(args, 1, "raw find <unitType> [--limit N]");
                int limit = ctx.Options.GetFlag("limit") is { } l ? CliContext.ParseInt(l, "limit") : 50;
                var save = ctx.LoadSave();
                var matches = save.Game.UnitsOfType(args[1]).ToList();
                foreach (var u in matches.Take(limit))
                    Console.WriteLine($"{u.Type} : {u.Id}");
                if (matches.Count > limit)
                    Console.WriteLine($"... ({matches.Count - limit} more; pass --limit {matches.Count} to see all)");
                Console.WriteLine($"Total: {matches.Count}");
                return 0;
            }

            case "get":
            {
                CoreCommands.RequireArg(args, 1, "raw get <unitId> [attrKey]");
                var save = ctx.LoadSave();
                var unit = save.Game.GetUnit(args[1]);
                if (args.Length >= 3)
                {
                    var key = args[2];
                    if (unit.IsArray(key))
                    {
                        var arr = unit.GetArray(key);
                        for (int i = 0; i < arr.Count; i++) Console.WriteLine($"{key}[{i}]: {arr[i]}");
                    }
                    else
                    {
                        Console.WriteLine($"{key}: {unit.GetRequired(key)}");
                    }
                    return 0;
                }
                Console.WriteLine($"{unit.Type} : {unit.Id} {{");
                foreach (var a in unit.Attributes) Console.WriteLine(a.ToLine());
                Console.WriteLine("}");
                return 0;
            }

            case "set":
            {
                CoreCommands.RequireArg(args, 3, "raw set <unitId> <attrKey> <value> [--add]");
                var save = ctx.LoadSave();
                var unit = save.Game.GetUnit(args[1]);
                bool add = ctx.Options.HasFlag("add");
                var prev = unit.Get(args[2]);
                unit.Set(args[2], args[3], addIfMissing: add);
                ctx.Commit(save, $"{unit.Id}.{args[2]}: {prev ?? "(new)"} -> {args[3]}");
                return 0;
            }

            default:
                throw new CliUsageException($"Unknown raw subcommand '{args[0]}'.");
        }
    }

    public static int Backup(CliContext ctx, string[] args)
    {
        if (args.Length == 0) throw new CliUsageException("Usage: ets2se backup <list|create|restore> ...");
        var saveDir = ctx.ResolveSaveDirectory();
        var sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "list":
            {
                var list = SaveBackupManager.ListBackups(saveDir);
                if (list.Count == 0) { Console.WriteLine($"No backups found for '{saveDir}'."); return 0; }
                foreach (var b in list)
                    Console.WriteLine($"{b.Id,-22} {b.CreatedAt:yyyy-MM-dd HH:mm:ss}  {b.TotalBytes,10:N0} bytes  {b.Directory}");
                return 0;
            }

            case "create":
            {
                var entry = SaveBackupManager.CreateBackup(saveDir);
                Console.WriteLine($"Created backup {entry.Id} at '{entry.Directory}' ({entry.TotalBytes:N0} bytes).");
                return 0;
            }

            case "restore":
            {
                var entry = SaveBackupManager.Restore(saveDir, args.Length > 1 ? args[1] : null);
                Console.WriteLine($"Restored backup {entry.Id} into '{saveDir}'.");
                return 0;
            }

            default:
                throw new CliUsageException($"Unknown backup subcommand '{args[0]}'.");
        }
    }

    public static async Task<int> ToolsAsync(CliContext ctx, string[] args)
    {
        if (args.Length == 0) throw new CliUsageException("Usage: ets2se tools <status|install> [--force]");
        var sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "status":
                Console.WriteLine($"Tools directory:  {ctx.Tools.ToolsDir}");
                Console.WriteLine($"SII_Decrypt.exe:  {ctx.Tools.FindSiiDecrypt() ?? "NOT INSTALLED"}");
                Console.WriteLine($"scs_extractor:    {ctx.Tools.FindScsExtractor() ?? "NOT INSTALLED"}");
                Console.WriteLine($"ETS2 install dir: {GameCatalog.FindGameDirectory() ?? "NOT FOUND"}");
                return 0;

            case "install":
                bool force = ctx.Options.HasFlag("force");
                await ToolInstaller.InstallAsync(ctx.Tools.ToolsDir, force, Console.WriteLine);
                return 0;

            default:
                throw new CliUsageException($"Unknown tools subcommand '{args[0]}'.");
        }
    }

    public static int Catalog(CliContext ctx, string[] args)
    {
        if (args.Length == 0) throw new CliUsageException("Usage: ets2se catalog <status|sync|cities> ...");
        var sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "status":
            {
                var cached = GameCatalog.TryLoadCached();
                if (cached is null)
                {
                    Console.WriteLine($"Catalog not synced yet. Cache path: {GameCatalog.CacheJsonPath()}");
                    Console.WriteLine("Run 'ets2se catalog sync' to extract def.scs with scs_extractor.exe.");
                    return 0;
                }
                Console.WriteLine($"Cache file:    {GameCatalog.CacheJsonPath()}");
                Console.WriteLine($"Game dir:      {cached.GameDirectory}");
                Console.WriteLine($"Synced (UTC):  {cached.ExtractedAtUtc:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"XP levels:     {cached.LevelXp.Length}");
                Console.WriteLine($"Cities:        {cached.Cities.Count}");
                Console.WriteLine($"Companies:     {cached.Companies.Count}");
                Console.WriteLine($"Truck models:  {cached.TruckModels.Count}");
                return 0;
            }

            case "sync":
            {
                var gameDir = GameCatalog.FindGameDirectory(ctx.Options.GetFlag("game-dir"))
                              ?? throw new InvalidOperationException("Could not locate ETS2 installation directory. Pass --game-dir <path>.");
                bool withDlcs = ctx.Options.HasFlag("with-dlcs");
                var extractor = ctx.RequireScsExtractor();
                var data = GameCatalog.SyncFromGame(extractor, gameDir, withDlcs, log: Console.WriteLine);
                Console.WriteLine($"Synced catalog: {data.Cities.Count} cities, {data.Companies.Count} companies, {data.TruckModels.Count} trucks, {data.LevelXp.Length} XP levels.");
                return 0;
            }

            case "cities":
            {
                var cached = GameCatalog.TryLoadCached()
                             ?? throw new InvalidOperationException("Catalog not synced yet. Run 'ets2se catalog sync' first.");
                var filter = ctx.Options.GetFlag("filter");
                var list = cached.Cities
                    .Where(c => string.IsNullOrEmpty(filter)
                                || c.Id.Contains(filter, StringComparison.OrdinalIgnoreCase)
                                || c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                                || c.Country.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (var c in list)
                    Console.WriteLine($"{c.Id,-22} {c.Name,-24} {c.Country,-16} ({c.SourceArchive})");
                Console.WriteLine($"Total: {list.Count}");
                return 0;
            }

            default:
                throw new CliUsageException($"Unknown catalog subcommand '{args[0]}'.");
        }
    }
}
