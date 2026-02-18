namespace Client.ECS.Components;

/// <summary>
/// Almacena los stats recibidos del servidor para mostrar en el HUD.
/// </summary>
public class StatsDataComponent
{
    public int   Level       { get; set; } = 1;
    public long  Experience  { get; set; }
    public long  ExpToNext   { get; set; }
    public int   CurrentHP   { get; set; }
    public int   MaxHP       { get; set; }
    public int   CurrentMP   { get; set; }
    public int   MaxMP       { get; set; }
    public int   Capacity    { get; set; }
    public int   MaxCapacity { get; set; }
    public int   Soul        { get; set; }
    public int   Stamina     { get; set; }
    public byte  Vocation    { get; set; }
    public float Speed       { get; set; }
}
