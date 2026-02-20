using Shared;

namespace Server.ECS.Components;

/// <summary>
/// Arch struct component – Tibia-style skills (8 skills × level + tries).
/// NOTE: Contains a managed array – Arch handles this correctly via boxing.
/// For fully unmanaged layout you could use fixed buffers, but the array is
/// simpler and matches the original design.
/// </summary>
public struct SkillsComponent
{
    public struct SkillData
    {
        public int  Level;
        public long Tries;
        public SkillData(int level) { Level = level; Tries = 0; }
    }

    // 8 skills: Fist, Club, Sword, Axe, Distance, Shielding, Fishing, MagicLevel
    private SkillData[] _skills;
    public bool IsDirty;

    public SkillsComponent()
    {
        _skills = new SkillData[8];
        for (int i = 0; i < 8; i++)
            _skills[i] = new SkillData(Constants.StartSkillLevel);
        IsDirty = true;
    }

    // Ensure lazy init (required for default struct ctor path)
    private SkillData[] EnsureSkills() => _skills ??= InitSkills();
    private static SkillData[] InitSkills()
    {
        var arr = new SkillData[8];
        for (int i = 0; i < 8; i++) arr[i] = new SkillData(Constants.StartSkillLevel);
        return arr;
    }

    public int GetLevel(SkillType type) => EnsureSkills()[(int)type].Level;

    public bool AddTries(SkillType type, long tries, Vocation vocation)
    {
        int idx = (int)type;
        EnsureSkills()[idx].Tries += tries;

        float mult = type switch
        {
            SkillType.Club       => VocationHelper.MeleeSkillMultiplier(vocation),
            SkillType.Sword      => VocationHelper.MeleeSkillMultiplier(vocation),
            SkillType.Axe        => VocationHelper.MeleeSkillMultiplier(vocation),
            SkillType.Distance   => VocationHelper.DistanceSkillMultiplier(vocation),
            SkillType.Shielding  => VocationHelper.ShieldingSkillMultiplier(vocation),
            SkillType.MagicLevel => VocationHelper.MagicLevelMultiplier(vocation),
            _                    => 1.0f
        };

        bool leveledUp = false;
        long needed = Constants.SkillTriesForLevel(EnsureSkills()[idx].Level, mult);
        while (EnsureSkills()[idx].Level < Constants.MaxSkillLevel && EnsureSkills()[idx].Tries >= needed)
        {
            EnsureSkills()[idx].Tries -= needed;
            EnsureSkills()[idx].Level++;
            leveledUp = true;
            needed = Constants.SkillTriesForLevel(EnsureSkills()[idx].Level, mult);
        }
        if (leveledUp || tries > 0) IsDirty = true;
        return leveledUp;
    }

    public int GetPercent(SkillType type, Vocation vocation)
    {
        int idx = (int)type;
        if (EnsureSkills()[idx].Level >= Constants.MaxSkillLevel) return 100;
        float mult = type switch
        {
            SkillType.MagicLevel => VocationHelper.MagicLevelMultiplier(vocation),
            SkillType.Distance   => VocationHelper.DistanceSkillMultiplier(vocation),
            SkillType.Shielding  => VocationHelper.ShieldingSkillMultiplier(vocation),
            SkillType.Club or SkillType.Sword or SkillType.Axe
                                 => VocationHelper.MeleeSkillMultiplier(vocation),
            _                    => 1.0f
        };
        long needed = Constants.SkillTriesForLevel(EnsureSkills()[idx].Level, mult);
        if (needed <= 0) return 100;
        return (int)(EnsureSkills()[idx].Tries * 100 / needed);
    }
}
