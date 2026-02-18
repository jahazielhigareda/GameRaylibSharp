namespace Client.ECS.Components;

/// <summary>
/// Almacena los skills recibidos del servidor para mostrar en el HUD.
/// </summary>
public class SkillsDataComponent
{
    public int FistLevel       { get; set; }
    public int FistPercent     { get; set; }
    public int ClubLevel       { get; set; }
    public int ClubPercent     { get; set; }
    public int SwordLevel      { get; set; }
    public int SwordPercent    { get; set; }
    public int AxeLevel        { get; set; }
    public int AxePercent      { get; set; }
    public int DistanceLevel   { get; set; }
    public int DistancePercent { get; set; }
    public int ShieldingLevel  { get; set; }
    public int ShieldingPercent { get; set; }
    public int FishingLevel    { get; set; }
    public int FishingPercent  { get; set; }
    public int MagicLevel      { get; set; }
    public int MagicPercent    { get; set; }
}
