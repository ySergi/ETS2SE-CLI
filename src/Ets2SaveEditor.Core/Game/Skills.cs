namespace Ets2SaveEditor.Core.Game;

/// <summary>A driver skill as stored in the save (economy unit for the player, driver_ai units for hired drivers).</summary>
public sealed record SkillDef(string Key, string CliName, string DisplayName, int Max, bool IsBitmask = false);

public static class Skills
{
    public const int AdrClasses = 6;
    public const int AdrAll = (1 << AdrClasses) - 1; // 63

    public static readonly SkillDef Adr = new("adr", "adr", "ADR (bitmask of 6 classes)", AdrAll, IsBitmask: true);
    public static readonly SkillDef LongDistance = new("long_dist", "long-distance", "Long distance", 6);
    public static readonly SkillDef HighValue = new("heavy", "high-value", "High value cargo", 6);
    public static readonly SkillDef Fragile = new("fragile", "fragile", "Fragile cargo", 6);
    public static readonly SkillDef JustInTime = new("urgent", "just-in-time", "Just-in-time delivery", 6);
    public static readonly SkillDef EcoDriving = new("mechanical", "eco-driving", "Eco driving", 6);

    public static IReadOnlyList<SkillDef> All { get; } = [Adr, LongDistance, HighValue, Fragile, JustInTime, EcoDriving];

    public static SkillDef? Find(string name) =>
        All.FirstOrDefault(s => s.CliName.Equals(name, StringComparison.OrdinalIgnoreCase)
                                || s.Key.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static int AdrClassCount(long mask) => System.Numerics.BitOperations.PopCount((ulong)(mask & AdrAll));

    /// <summary>Human readable list of unlocked ADR classes (1, 2, 3, 4, 6, 8 in game order).</summary>
    public static string DescribeAdr(long mask)
    {
        string[] classes = ["1 Explosives", "2 Gases", "3 Flammable liquids", "4 Flammable solids", "6 Toxic", "8 Corrosive"];
        var names = new List<string>();
        for (int i = 0; i < AdrClasses; i++)
            if ((mask & (1L << i)) != 0) names.Add(classes[i].Split(' ')[0]);
        return names.Count == 0 ? "none" : "class " + string.Join(",", names);
    }
}
