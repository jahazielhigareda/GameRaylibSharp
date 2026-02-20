using MessagePack;

namespace Shared.Network;

/// <summary>
/// Protocol version 1 handler.
///
/// Serialization : MessagePack (standard resolver)
/// Compression   : MessagePack built-in LZ4 block compression.
///                 No extra NuGet package needed – MessagePack 3.x ships LZ4
///                 via <see cref="MessagePackCompression.Lz4Block"/>.
/// </summary>
public sealed class ProtocolV1Handler : IProtocolHandler
{
    public byte Version => 1;

    // Plain options – for small / already-cheap packets
    private static readonly MessagePackSerializerOptions PlainOptions =
        MessagePackSerializerOptions.Standard;

    // LZ4 block options – for large payloads like WorldState
    private static readonly MessagePackSerializerOptions LZ4Options =
        MessagePackSerializerOptions.Standard
            .WithCompression(MessagePackCompression.Lz4Block);

    public byte[] SerializePayload<T>(T message, bool compress)
    {
        var opts = compress ? LZ4Options : PlainOptions;
        return MessagePackSerializer.Serialize(message, opts);
    }

    public T DeserializePayload<T>(ReadOnlyMemory<byte> payload, bool compressed)
    {
        var opts = compressed ? LZ4Options : PlainOptions;
        return MessagePackSerializer.Deserialize<T>(payload, opts);
    }
}
