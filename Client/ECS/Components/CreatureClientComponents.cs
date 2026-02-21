namespace Client.ECS.Components;

/// <summary>Tag marking an entity as a remote creature (not the local player).</summary>
public struct CreatureClientTag { }

/// <summary>Health percentage received from server snapshot (0-100).</summary>
public struct CreatureHpComponent
{
    public byte HpPct;
}

/// <summary>Display name shown above the creature.</summary>
public struct CreatureNameComponent
{
    public string Name;
}
