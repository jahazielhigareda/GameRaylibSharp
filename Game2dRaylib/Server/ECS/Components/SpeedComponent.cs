namespace Server.ECS.Components;

/// <summary>
/// Velocidad del personaje. Determina el cooldown entre pasos.
/// </summary>
public class SpeedComponent
{
    public float Speed { get; set; } = Shared.Constants.BaseSpeed;
}
