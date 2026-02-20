namespace Client.ECS.Components;

/// <summary>Arch struct component – stats received from server for HUD display.</summary>
public struct StatsDataComponent
{
    public int   Level;
    public long  Experience;
    public long  ExpToNext;
    public int   CurrentHP;
    public int   MaxHP;
    public int   CurrentMP;
    public int   MaxMP;
    public int   Capacity;
    public int   MaxCapacity;
    public int   Soul;
    public int   Stamina;
    public byte  Vocation;
    public float Speed;

    public StatsDataComponent() { Level = 1; }
}
