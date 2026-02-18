using Shared;

namespace Server.ECS.Components;

/// <summary>
/// Componente de skills estilo Tibia.
/// Cada skill tiene un nivel y tries acumulados.
/// </summary>
public class SkillsComponent
{
    public struct SkillData
    {
        public int  Level  { get; set; }
        public long Tries  { get; set; }

        public SkillData(int level)
        {
            Level = level;
            Tries = 0;
        }
    }

    private readonly SkillData[] _skills = new SkillData[8]; // 8 skills (incluyendo MagicLevel)

    public bool IsDirty { get; set; } = true;

    public SkillsComponent()
    {
        for (int i = 0; i < _skills.Length; i++)
            _skills[i] = new SkillData(Constants.StartSkillLevel);
    }

    /// <summary>
    /// Obtiene los datos de un skill.
    /// </summary>
    public SkillData GetSkill(SkillType type) => _skills[(int)type];

    /// <summary>
    /// Obtiene el nivel de un skill.
    /// </summary>
    public int GetLevel(SkillType type) => _skills[(int)type].Level;

    /// <summary>
    /// Añade tries a un skill. Sube de nivel si corresponde.
    /// Retorna true si subió de nivel.
    /// </summary>
    public bool AddTries(SkillType type, long tries, Vocation vocation)
    {
        int idx = (int)type;
        _skills[idx].Tries += tries;

        float multiplier = type switch
        {
            SkillType.Fist      => 1.0f,
            SkillType.Club      => VocationHelper.MeleeSkillMultiplier(vocation),
            SkillType.Sword     => VocationHelper.MeleeSkillMultiplier(vocation),
            SkillType.Axe       => VocationHelper.MeleeSkillMultiplier(vocation),
            SkillType.Distance  => VocationHelper.DistanceSkillMultiplier(vocation),
            SkillType.Shielding => VocationHelper.ShieldingSkillMultiplier(vocation),
            SkillType.Fishing   => 1.0f,
            SkillType.MagicLevel => VocationHelper.MagicLevelMultiplier(vocation),
            _ => 1.0f
        };

        bool leveledUp = false;
        long needed = Constants.SkillTriesForLevel(_skills[idx].Level, multiplier);

        while (_skills[idx].Level < Constants.MaxSkillLevel && _skills[idx].Tries >= needed)
        {
            _skills[idx].Tries -= needed;
            _skills[idx].Level++;
            leveledUp = true;
            needed = Constants.SkillTriesForLevel(_skills[idx].Level, multiplier);
        }

        if (leveledUp || tries > 0) IsDirty = true;
        return leveledUp;
    }

    /// <summary>
    /// Porcentaje de progreso hacia el siguiente nivel de un skill (0-100).
    /// </summary>
    public int GetPercent(SkillType type, Vocation vocation)
    {
        int idx = (int)type;
        if (_skills[idx].Level >= Constants.MaxSkillLevel) return 100;

        float multiplier = type switch
        {
            SkillType.MagicLevel => VocationHelper.MagicLevelMultiplier(vocation),
            SkillType.Distance   => VocationHelper.DistanceSkillMultiplier(vocation),
            SkillType.Shielding  => VocationHelper.ShieldingSkillMultiplier(vocation),
            SkillType.Club or SkillType.Sword or SkillType.Axe
                                 => VocationHelper.MeleeSkillMultiplier(vocation),
            _ => 1.0f
        };

        long needed = Constants.SkillTriesForLevel(_skills[idx].Level, multiplier);
        if (needed <= 0) return 100;
        return (int)(_skills[idx].Tries * 100 / needed);
    }
}
