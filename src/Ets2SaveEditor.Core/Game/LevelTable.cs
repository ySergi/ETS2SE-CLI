namespace Ets2SaveEditor.Core.Game;

/// <summary>Player level calculation (def/economy_data_xp.sii, "level_xp" = XP needed for the next level).</summary>
public sealed class LevelTable
{
    /// <summary>Values from ETS2 1.5x def/economy_data_xp.sii.</summary>
    public static readonly int[] DefaultLevelXp =
    [
        200, 500, 700, 900, 1000, 1100, 1300, 1600, 1700, 2100,
        2300, 2600, 2700, 2900, 3000, 3100, 3400, 3700, 4000, 4300,
        4600, 4700, 4900, 5200, 5700, 5900, 6000, 6200, 6600, 6800,
    ];

    public static LevelTable Default { get; } = new(DefaultLevelXp);

    private readonly int[] _levelXp;

    public LevelTable(IReadOnlyList<int> levelXp)
    {
        if (levelXp.Count == 0) throw new ArgumentException("Level table cannot be empty.", nameof(levelXp));
        _levelXp = levelXp.ToArray();
    }

    public IReadOnlyList<int> LevelXp => _levelXp;

    /// <summary>XP needed to advance from <paramref name="level"/> to level+1. Levels past the table reuse the last entry.</summary>
    public long XpForNextLevel(int level) => _levelXp[Math.Min(level, _levelXp.Length - 1)];

    /// <summary>Total XP required to reach <paramref name="level"/> (level 0 = 0 XP).</summary>
    public long TotalXpForLevel(int level)
    {
        if (level <= 0) return 0;
        long total = 0;
        for (int l = 0; l < level; l++) total += XpForNextLevel(l);
        return total;
    }

    public int LevelFromXp(long xp)
    {
        int level = 0;
        long acc = 0;
        while (level < 100_000)
        {
            long need = XpForNextLevel(level);
            if (acc + need > xp) break;
            acc += need;
            level++;
        }
        return level;
    }
}
