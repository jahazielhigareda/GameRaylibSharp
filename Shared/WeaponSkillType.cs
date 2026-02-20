namespace Shared;

/// <summary>
/// The weapon skill category used for damage rolls and skill training.
/// Matches the physical weapon types that map to SkillType entries.
/// </summary>
public enum WeaponSkillType : byte
{
    Fist     = 0,
    Club     = 1,
    Sword    = 2,
    Axe      = 3,
    Distance = 4,
}
