using Ets2SaveEditor.Core.Game;
using Ets2SaveEditor.Core.Sii;

namespace Ets2SaveEditor.Core.Save;

public sealed class HiredDriver(SiiUnit unit)
{
    public SiiUnit Unit { get; } = unit;
    public string Id => Unit.Id;
    public string Hometown => Unit.Get("hometown") ?? "";
    public string CurrentCity => Unit.Get("current_city") ?? "";
    public long State => Unit.GetLong("state") ?? 0;

    public long ExperiencePoints
    {
        get => Unit.GetLong("experience_points") ?? 0;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "XP cannot be negative.");
            Unit.SetLong("experience_points", value);
        }
    }

    public long GetSkill(SkillDef skill) => Unit.GetLong(skill.Key) ?? 0;

    public void SetSkill(SkillDef skill, long value)
    {
        if (value < 0 || value > skill.Max)
            throw new ArgumentOutOfRangeException(nameof(value), $"{skill.CliName} must be between 0 and {skill.Max}.");
        Unit.SetLong(skill.Key, value);
    }

    public void MaxSkills()
    {
        foreach (var s in Skills.All) SetSkill(s, s.Max);
    }

    public int GetLevel(LevelTable? table = null) => (table ?? LevelTable.Default).LevelFromXp(ExperiencePoints);
}
