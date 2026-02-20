namespace Client.Services;

public class GameStateService
{
    public int  LocalId        { get; set; } = -1;
    public int  Tick           { get; set; }
    public byte CurrentFloorZ  { get; set; } = 7;

    /// <summary>Network ID of the currently targeted entity (0 = none).</summary>
    public int  TargetedEntityId { get; set; } = 0;
}
