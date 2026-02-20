using System.Buffers;

namespace Shared.Network;

/// <summary>
/// Thin, thread-safe wrapper around <see cref="ArrayPool{T}.Shared"/>.
///
/// Usage:
/// <code>
///   using var buf = PacketBufferPool.Rent(size);
///   // use buf.Span[0..buf.Length-1]
///   // automatically returned to pool when the using block exits
/// </code>
/// </summary>
public static class PacketBufferPool
{
    /// <summary>Rents a buffer of at least <paramref name="minimumLength"/> bytes.</summary>
    public static PooledBuffer Rent(int minimumLength)
    {
        var arr = ArrayPool<byte>.Shared.Rent(minimumLength);
        return new PooledBuffer(arr, minimumLength);
    }

    /// <summary>Returns a rented buffer to the pool.</summary>
    public static void Return(PooledBuffer buf)
        => ArrayPool<byte>.Shared.Return(buf.Array, clearArray: false);
}

/// <summary>
/// Represents a rented buffer.  Always pair with <see cref="PacketBufferPool.Return"/>
/// or use inside a <c>using</c> block (implements <see cref="IDisposable"/>).
/// </summary>
public readonly struct PooledBuffer : IDisposable
{
    /// <summary>The underlying rented array (may be larger than <see cref="Length"/>).</summary>
    public readonly byte[] Array;

    /// <summary>The logical length requested.</summary>
    public readonly int Length;

    internal PooledBuffer(byte[] array, int length)
    {
        Array  = array;
        Length = length;
    }

    public Span<byte>         Span         => Array.AsSpan(0, Length);
    public Memory<byte>       Memory       => Array.AsMemory(0, Length);
    public ReadOnlySpan<byte> ReadOnlySpan => Array.AsSpan(0, Length);

    public Span<byte> Slice(int start, int length) => Array.AsSpan(start, length);

    public void Dispose() => PacketBufferPool.Return(this);
}
