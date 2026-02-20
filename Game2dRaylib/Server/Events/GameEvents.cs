namespace Server.Events;

public struct CreatureMoved
{
    public int EntityId;
    public int FromX;
    public int FromY;
    public int ToX;
    public int ToY;
}

public struct PlayerLevelUp
{
    public int PlayerId;
    public int NewLevel;
    public int NewMaxHP;
    public int NewMaxMP;
}

public struct CreatureDied
{
    public int EntityId;
    public int KillerId;
    public int TileX;
    public int TileY;
}

public struct EntityEnteredCell
{
    public int EntityId;
    public int CellX;
    public int CellY;
    public int TileX;
    public int TileY;
}

public struct PlayerDisconnectedEvent
{
    public int NetworkId;
}

public struct MapLoadedEvent
{
    public ushort Width;
    public ushort Height;
    public byte   Floors;
    public string Path;
}
