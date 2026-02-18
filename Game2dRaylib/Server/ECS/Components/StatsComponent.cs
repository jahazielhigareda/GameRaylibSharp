using Shared;

namespace Server.ECS.Components;

/// <summary>
/// Componente de stats estilo Tibia.
/// Maneja nivel, experiencia, HP, MP, capacidad, soul, stamina.
/// </summary>
public class StatsComponent
{
    // --- Nivel y Experiencia ---
    public int  Level      { get; set; } = Constants.StartLevel;
    public long Experience { get; set; } = 0;

    // --- Vocación ---
    public Vocation Vocation { get; set; } = Vocation.None;

    // --- HP ---
    public int CurrentHP { get; set; }
    public int MaxHP     { get; set; }

    // --- MP ---
    public int CurrentMP { get; set; }
    public int MaxMP     { get; set; }

    // --- Capacidad ---
    public int Capacity    { get; set; }
    public int MaxCapacity { get; set; }

    // --- Soul ---
    public int Soul { get; set; } = Constants.MaxSoul;

    // --- Stamina (en minutos) ---
    public int Stamina { get; set; } = Constants.StartStamina;

    // --- Regeneración ---
    public float RegenTimer { get; set; } = 0f;

    // --- Flag de cambio (para enviar updates solo cuando cambia) ---
    public bool IsDirty { get; set; } = true;

    public StatsComponent() { }

    /// <summary>
    /// Inicializa los stats para un nuevo jugador con la vocación dada.
    /// </summary>
    public void Initialize(Vocation vocation, int level = 1)
    {
        Vocation = vocation;
        Level    = level;

        RecalculateMaxValues();

        CurrentHP  = MaxHP;
        CurrentMP  = MaxMP;
        Capacity   = MaxCapacity;
        Experience = Constants.ExperienceForLevel(level);
        IsDirty    = true;
    }

    /// <summary>
    /// Recalcula HP máximo, MP máximo y capacidad basados en nivel y vocación.
    /// </summary>
    public void RecalculateMaxValues()
    {
        MaxHP       = Constants.BaseHP + (Level - 1) * VocationHelper.HPPerLevel(Vocation);
        MaxMP       = Constants.BaseMP + (Level - 1) * VocationHelper.MPPerLevel(Vocation);
        MaxCapacity = Constants.BaseCap + (Level - 1) * VocationHelper.CapPerLevel(Vocation);
    }

    /// <summary>
    /// Añade experiencia y sube de nivel si corresponde.
    /// Retorna true si subió de nivel.
    /// </summary>
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

        // Restaurar la diferencia ganada
        CurrentHP += (MaxHP - oldMaxHP);
        CurrentMP += (MaxMP - oldMaxMP);

        // Asegurar que no exceda el máximo
        CurrentHP = Math.Min(CurrentHP, MaxHP);
        CurrentMP = Math.Min(CurrentMP, MaxMP);
    }

    /// <summary>
    /// Regenera HP y MP según el intervalo de regeneración.
    /// </summary>
    public void Regenerate(float deltaTime)
    {
        RegenTimer += deltaTime;

        if (RegenTimer >= Constants.RegenInterval)
        {
            RegenTimer -= Constants.RegenInterval;

            bool changed = false;

            if (CurrentHP < MaxHP)
            {
                CurrentHP = Math.Min(MaxHP, CurrentHP + Constants.HPRegenBase);
                changed = true;
            }

            if (CurrentMP < MaxMP)
            {
                CurrentMP = Math.Min(MaxMP, CurrentMP + Constants.MPRegenBase);
                changed = true;
            }

            if (changed) IsDirty = true;
        }
    }

    /// <summary>
    /// Aplica daño al jugador. Retorna true si murió.
    /// </summary>
    public bool TakeDamage(int damage)
    {
        CurrentHP -= damage;
        IsDirty = true;

        if (CurrentHP <= 0)
        {
            CurrentHP = 0;
            return true; // Murió
        }
        return false;
    }

    /// <summary>
    /// Cura HP.
    /// </summary>
    public void HealHP(int amount)
    {
        CurrentHP = Math.Min(MaxHP, CurrentHP + amount);
        IsDirty = true;
    }

    /// <summary>
    /// Consume MP. Retorna true si tenía suficiente.
    /// </summary>
    public bool ConsumeMP(int amount)
    {
        if (CurrentMP < amount) return false;
        CurrentMP -= amount;
        IsDirty = true;
        return true;
    }

    /// <summary>
    /// Consume soul points. Retorna true si tenía suficiente.
    /// </summary>
    public bool ConsumeSoul(int amount)
    {
        if (Soul < amount) return false;
        Soul -= amount;
        IsDirty = true;
        return true;
    }

    /// <summary>
    /// Experiencia necesaria para el siguiente nivel.
    /// </summary>
    public long ExperienceToNextLevel()
    {
        if (Level >= Constants.MaxLevel) return 0;
        return Constants.ExperienceForLevel(Level + 1) - Experience;
    }

    /// <summary>
    /// Porcentaje de experiencia hacia el siguiente nivel (0-100).
    /// </summary>
    public int ExperiencePercent()
    {
        if (Level >= Constants.MaxLevel) return 100;
        long needed = Constants.ExperienceToNextLevel(Level);
        if (needed <= 0) return 100;
        long current = Experience - Constants.ExperienceForLevel(Level);
        return (int)(current * 100 / needed);
    }
}
