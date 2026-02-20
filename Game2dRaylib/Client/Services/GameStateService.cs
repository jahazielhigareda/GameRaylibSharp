namespace Client.Services;

public class GameStateService
{
    public int LocalId { get; set; } = -1;
    public int  Tick         { get; set; }
    public byte CurrentFloorZ { get; set; } = 7; // Tibia ground floor default
}
