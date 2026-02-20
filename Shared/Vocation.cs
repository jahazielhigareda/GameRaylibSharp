namespace Shared;

/// <summary>
/// Vocaciones estilo Tibia.
/// Cada vocación tiene diferentes multiplicadores de HP, MP y skills.
/// </summary>
public enum Vocation : byte
{
    None     = 0,
    Knight   = 1,
    Paladin  = 2,
    Sorcerer = 3,
    Druid    = 4
}

public static class VocationHelper
{
    public static int HPPerLevel(Vocation voc) => voc switch
    {
        Vocation.Knight   => 15,
        Vocation.Paladin  => 10,
        Vocation.Sorcerer => 5,
        Vocation.Druid    => 5,
        _                 => 5
    };

    public static int MPPerLevel(Vocation voc) => voc switch
    {
        Vocation.Knight   => 5,
        Vocation.Paladin  => 15,
        Vocation.Sorcerer => 30,
        Vocation.Druid    => 30,
        _                 => 5
    };

    public static int CapPerLevel(Vocation voc) => voc switch
    {
        Vocation.Knight   => 25,
        Vocation.Paladin  => 20,
        Vocation.Sorcerer => 10,
        Vocation.Druid    => 10,
        _                 => 10
    };

    public static float MeleeSkillMultiplier(Vocation voc) => voc switch
    {
        Vocation.Knight   => 1.1f,
        Vocation.Paladin  => 1.2f,
        Vocation.Sorcerer => 1.5f,
        Vocation.Druid    => 1.5f,
        _                 => 1.0f
    };

    public static float DistanceSkillMultiplier(Vocation voc) => voc switch
    {
        Vocation.Knight   => 1.4f,
        Vocation.Paladin  => 1.1f,
        Vocation.Sorcerer => 1.4f,
        Vocation.Druid    => 1.4f,
        _                 => 1.0f
    };

    public static float MagicLevelMultiplier(Vocation voc) => voc switch
    {
        Vocation.Knight   => 3.0f,
        Vocation.Paladin  => 1.4f,
        Vocation.Sorcerer => 1.1f,
        Vocation.Druid    => 1.1f,
        _                 => 1.0f
    };

    public static float ShieldingSkillMultiplier(Vocation voc) => voc switch
    {
        Vocation.Knight   => 1.1f,
        Vocation.Paladin  => 1.1f,
        Vocation.Sorcerer => 1.5f,
        Vocation.Druid    => 1.5f,
        _                 => 1.0f
    };

    public static string Name(Vocation voc) => voc switch
    {
        Vocation.Knight   => "Knight",
        Vocation.Paladin  => "Paladin",
        Vocation.Sorcerer => "Sorcerer",
        Vocation.Druid    => "Druid",
        _                 => "None"
    };
}
