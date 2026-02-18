namespace Server.ECS.Components;

public class InputComponent
{
    public bool Up    { get; set; }
    public bool Down  { get; set; }
    public bool Left  { get; set; }
    public bool Right { get; set; }
    public int  Tick  { get; set; }
}
