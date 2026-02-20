namespace Server.ECS.Components;

/// <summary>FSM states for creature AI.</summary>
public enum CreatureState : byte
{
    Idle   = 0,  // wandering near spawn
    Alert  = 1,  // noticed a player, short delay before chasing
    Chase  = 2,  // actively pursuing target
    Attack = 3,  // within melee range, executing attacks
    Flee   = 4,  // HP < 15%, running away
    Return = 5,  // lost target, walking back to spawn
    Dead   = 6,  // waiting for respawn
}
