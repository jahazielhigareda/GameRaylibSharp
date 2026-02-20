using MessagePack;

namespace Shared.Packets;

/// <summary>
/// Paquete enviado del servidor al cliente con los stats actualizados del jugador.
/// </summary>
[MessagePackObject]
public class StatsUpdatePacket
{
    [Key(0)]  public int   PlayerId    { get; set; }
    [Key(1)]  public int   Level       { get; set; }
    [Key(2)]  public long  Experience  { get; set; }
    [Key(3)]  public long  ExpToNext   { get; set; }
    [Key(4)]  public int   CurrentHP   { get; set; }
    [Key(5)]  public int   MaxHP       { get; set; }
    [Key(6)]  public int   CurrentMP   { get; set; }
    [Key(7)]  public int   MaxMP       { get; set; }
    [Key(8)]  public int   Capacity    { get; set; }
    [Key(9)]  public int   MaxCapacity { get; set; }
    [Key(10)] public int   Soul        { get; set; }
    [Key(11)] public int   Stamina     { get; set; }  // En minutos
    [Key(12)] public byte  Vocation    { get; set; }
    [Key(13)] public float Speed       { get; set; }
}
