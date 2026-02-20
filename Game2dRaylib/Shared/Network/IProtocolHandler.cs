using Shared.Packets;

namespace Shared.Network;

/// <summary>
/// Pluggable handler for a specific protocol version.
/// Implement this interface to add or change how a version's packets are
/// serialized / deserialized without touching the rest of the pipeline.
/// </summary>
public interface IProtocolHandler
{
    byte Version { get; }

    /// <summary>Serialize <paramref name="message"/> to raw payload bytes.</summary>
    byte[] SerializePayload<T>(T message, bool compress);

    /// <summary>Deserialize payload bytes back into <typeparamref name="T"/>.</summary>
    T DeserializePayload<T>(ReadOnlyMemory<byte> payload, bool compressed);
}

/// <summary>
/// Registry that maps <see cref="IProtocolHandler.Version"/> to a handler instance.
/// Falls back to the highest registered version when an exact match is missing.
/// </summary>
public sealed class ProtocolHandlerRegistry
{
    private readonly SortedDictionary<byte, IProtocolHandler> _handlers = new();

    public void Register(IProtocolHandler handler)
        => _handlers[handler.Version] = handler;

    /// <summary>Returns the handler for <paramref name="version"/>, or the latest if unknown.</summary>
    public IProtocolHandler Resolve(byte version)
    {
        if (_handlers.TryGetValue(version, out var h)) return h;
        return _handlers.Count > 0
            ? _handlers.Last().Value
            : throw new InvalidOperationException("No protocol handlers registered.");
    }

    public IProtocolHandler Current => Resolve(NetworkPacket.CurrentVersion);
}
