using Shared;

namespace Server.ECS.Components;

/// <summary>
/// Arch struct component – Tibia-style stats (level, exp, HP, MP, cap, soul, stamina).
/// </summary>
public struct StatsComponent
{
    public int     Level;
    public long    Experience;
    public Vocation Vocation;
    public int     CurrentHP;
    public int     MaxHP;
    public int     CurrentMP;
    public int     MaxMP;
    public int     Capacity;
    public int     MaxCapacity;
    public int     Soul;
    public int     Stamina;
    public float   RegenTimer;
    public bool    IsDirty;

    public StatsComponent() { IsDirty = true; }

    public void Initialize(Vocation vocation, int level = 1)
    {
        Vocation   = vocation;
        Level      = level;
        Soul       = Constants.MaxSoul;
        Stamina    = Constants.StartStamina;
        IsDirty    = true;
        RecalculateMaxValues();
        CurrentHP  = MaxHP;
        CurrentMP  = MaxMP;
        Capacity   = MaxCapacity;
        Experience = Constants.ExperienceForLevel(level);
    }

    public void RecalculateMaxValues()
    {
        MaxHP       = Constants.BaseHP  + (Level - 1) * VocationHelper.HPPerLevel(Vocation);
        MaxMP       = Constants.BaseMP  + (Level - 1) * VocationHelper.MPPerLevel(Vocation);
        MaxCapacity = Constants.BaseCap + (Level - 1) * VocationHelper.CapPerLevel(Vocation);
    }

    public bool AddExperience(long amount)
    {
        if (Level >= Constants.MaxLevel) return false;
        Experience += amount;
        bool leveledUp = false;
        while (Level < Constants.MaxLevel && Experience >= Constants.ExperienceForLevel(Level + 1))
        {
            LevelUp();
            leveledUp = true;
        }
        IsDirty = true;
        return leveledUp;
    }

    private void LevelUp()
    {
        Level++;
        int oldMaxHP = MaxHP;
        int oldMaxMP = MaxMP;
        RecalculateMaxValues();
        CurrentHP = Math.Min(CurrentHP + (MaxHP - oldMaxHP), MaxHP);
        CurrentMP = Math.Min(CurrentMP + (MaxMP - oldMaxMP), MaxMP);
    }

    public void Regenerate(float deltaTime)
    {
        RegenTimer += deltaTime;
        if (RegenTimer < Constants.RegenInterval) return;
        RegenTimer -= Constants.RegenInterval;

        bool changed = false;
        if (CurrentHP < MaxHP) { CurrentHP = Math.Min(MaxHP, CurrentHP + Constants.HPRegenBase); changed = true; }
        if (CurrentMP < MaxMP) { CurrentMP = Math.Min(MaxMP, CurrentMP + Constants.MPRegenBase); changed = true; }
        if (changed) IsDirty = true;
    }

    public bool TakeDamage(int damage)
    {
        CurrentHP -= damage;
        IsDirty    = true;
        if (CurrentHP <= 0) { CurrentHP = 0; return true; }
        return false;
    }

    public void HealHP(int amount)       { CurrentHP = Math.Min(MaxHP, CurrentHP + amount); IsDirty = true; }
    public bool ConsumeMP(int amount)    { if (CurrentMP < amount) return false; CurrentMP -= amount; IsDirty = true; return true; }
    public bool ConsumeSoul(int amount)  { if (Soul < amount) return false; Soul -= amount; IsDirty = true; return true; }

    public readonly long ExperienceToNextLevel()
    {
        if (Level >= Constants.MaxLevel) return 0;
        return Constants.ExperienceForLevel(Level + 1) - Experience;
    }

    public readonly int ExperiencePercent()
    {
        if (Level >= Constants.MaxLevel) return 100;
        long needed  = Constants.ExperienceToNextLevel(Level);
        if (needed <= 0) return 100;
        long current = Experience - Constants.ExperienceForLevel(Level);
        return (int)(current * 100 / needed);
    }
}
