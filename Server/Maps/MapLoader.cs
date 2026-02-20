using Microsoft.Extensions.Logging;
using Server.Events;

namespace Server.Maps;

/// <summary>
/// DI-injectable service that loads .map files and fires MapLoadedEvent.
/// </summary>
public sealed class MapLoader
{
    private readonly ILogger<MapLoader> _logger;
    private readonly EventBus           _eventBus;

    public MapData? CurrentMap { get; private set; }

    public MapLoader(ILogger<MapLoader> logger, EventBus eventBus)
    {
        _logger   = logger;
        _eventBus = eventBus;
    }

    /// <summary>Load map synchronously (called from GameLoop startup).</summary>
    public MapData Load(string path)
    {
        _logger.LogInformation("Loading map: {Path}", path);

        MapData map;
        if (File.Exists(path))
        {
            map = MapSerializer.Read(path);
            _logger.LogInformation(
                "Map loaded: {W}×{H}×{F} (ground floor {G})",
                map.Width, map.Height, map.Floors, map.GroundFloor);
        }
        else
        {
            _logger.LogWarning("Map file not found at {Path}, generating default.", path);
            map = MapGenerator.Generate();
            MapSerializer.Write(map, path);
            _logger.LogInformation("Generated and saved default map to {Path}", path);
        }

        CurrentMap = map;

        _eventBus.Publish(new MapLoadedEvent
        {
            Width  = map.Width,
            Height = map.Height,
            Floors = map.Floors,
            Path   = path,
        });

        return map;
    }
}
