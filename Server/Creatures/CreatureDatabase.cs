using Shared.Creatures;

namespace Server.Creatures;

/// <summary>
/// In-memory registry of all <see cref="CreatureTemplate"/> definitions.
/// Populated at startup with hardcoded data; later can load from JSON/XML.
/// </summary>
public sealed class CreatureDatabase
{
    private readonly Dictionary<string, CreatureTemplate> _byName
        = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ushort, CreatureTemplate> _byId = new();

    public CreatureDatabase() => RegisterDefaults();

    public bool TryGet(string name, out CreatureTemplate template)
        => _byName.TryGetValue(name, out template!);

    public bool TryGet(ushort id, out CreatureTemplate template)
        => _byId.TryGetValue(id, out template!);

    public CreatureTemplate Get(string name)
        => _byName.TryGetValue(name, out var t) ? t
           : throw new KeyNotFoundException($"Creature '{name}' not found in database.");

    public IEnumerable<CreatureTemplate> All => _byName.Values;

    public void Register(CreatureTemplate template)
    {
        _byName[template.Name] = template;
        _byId[template.Id]     = template;
    }

    private void RegisterDefaults()
    {
        Register(new CreatureTemplate
        {
            Id = 1, Name = "Rat",
            MaxHP = 20, MaxMP = 0, Experience = 5, Speed = 160,
            Armor = 0, Defense = 0, AttackMin = 1, AttackMax = 4,
            LookRange = 3, ChaseRange = 5, Behavior = CreatureBehavior.Melee,
            LootTable = new[] { new LootEntry { ItemId = 3031, Chance = 1.00f, MinCount = 1, MaxCount = 1 } },
        });

        Register(new CreatureTemplate
        {
            Id = 2, Name = "Cave Rat",
            MaxHP = 50, MaxMP = 0, Experience = 15, Speed = 170,
            Armor = 1, Defense = 1, AttackMin = 3, AttackMax = 8,
            LookRange = 4, ChaseRange = 6, Behavior = CreatureBehavior.Melee,
            LootTable = new[]
            {
                new LootEntry { ItemId = 3031, Chance = 1.00f, MinCount = 1, MaxCount = 3 },
                new LootEntry { ItemId = 5908, Chance = 0.30f, MinCount = 1, MaxCount = 1 },
            },
        });

        Register(new CreatureTemplate
        {
            Id = 3, Name = "Goblin",
            MaxHP = 75, MaxMP = 0, Experience = 25, Speed = 180,
            Armor = 2, Defense = 2, AttackMin = 5, AttackMax = 12,
            LookRange = 5, ChaseRange = 8, Behavior = CreatureBehavior.Melee,
            LootTable = new[]
            {
                new LootEntry { ItemId = 3031, Chance = 1.00f, MinCount = 1, MaxCount = 5 },
                new LootEntry { ItemId = 3012, Chance = 0.15f, MinCount = 1, MaxCount = 1 },
                new LootEntry { ItemId = 3049, Chance = 0.10f, MinCount = 1, MaxCount = 1 },
            },
        });

        Register(new CreatureTemplate
        {
            Id = 4, Name = "Troll",
            MaxHP = 150, MaxMP = 0, Experience = 40, Speed = 150,
            Armor = 4, Defense = 3, AttackMin = 8, AttackMax = 18,
            LookRange = 5, ChaseRange = 10, Behavior = CreatureBehavior.Melee,
            LootTable = new[]
            {
                new LootEntry { ItemId = 3031, Chance = 1.00f, MinCount = 1,  MaxCount = 15 },
                new LootEntry { ItemId = 3560, Chance = 0.50f, MinCount = 1,  MaxCount = 5  },
                new LootEntry { ItemId = 3012, Chance = 0.20f, MinCount = 1,  MaxCount = 1  },
            },
        });

        Register(new CreatureTemplate
        {
            Id = 5, Name = "Wolf",
            MaxHP = 100, MaxMP = 0, Experience = 30, Speed = 200,
            Armor = 1, Defense = 1, AttackMin = 6, AttackMax = 14,
            LookRange = 6, ChaseRange = 10, Behavior = CreatureBehavior.Melee,
            LootTable = new[]
            {
                new LootEntry { ItemId = 3560, Chance = 0.80f, MinCount = 1, MaxCount = 3 },
                new LootEntry { ItemId = 5901, Chance = 0.40f, MinCount = 1, MaxCount = 1 },
            },
        });
    }
}
