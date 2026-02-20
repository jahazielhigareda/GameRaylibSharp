namespace Client.ECS.Components;

/// <summary>
/// Attached to transient effect entities (spells, particles, magic).
/// EffectRenderSystem draws and ages these each frame.
/// </summary>
public struct EffectComponent
{
    public ushort EffectId;
    public float  Lifetime;     // total duration in seconds
    public float  Age;          // seconds elapsed since spawn
    public float  Alpha;        // current opacity [0..1]

    public bool   IsExpired => Age >= Lifetime;
    public float  Progress  => Lifetime > 0 ? Age / Lifetime : 1f;
}
