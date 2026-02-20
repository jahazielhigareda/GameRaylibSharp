using MapEditor.Maps;
using Raylib_cs;

namespace MapEditor;

public readonly record struct PaletteEntry(string Name, ushort GroundId, ushort Flags, Color Color);

public sealed class TilePalette
{
    public static readonly PaletteEntry[] Entries =
    {
        new("Grass", 1231, (ushort)TileFlags.Walkable,        new Color((byte)34,  (byte)139, (byte)34,  (byte)255)),
        new("Water", 4608, (ushort)TileFlags.None,             new Color((byte)0,   (byte)0,   (byte)139, (byte)255)),
        new("Stone", 1055, (ushort)TileFlags.None,             new Color((byte)105, (byte)105, (byte)105, (byte)255)),
        new("Tree",  2700, (ushort)TileFlags.BlockProjectile,  new Color((byte)0,   (byte)100, (byte)0,   (byte)255)),
        new("Wall",  1,    (ushort)TileFlags.None,             new Color((byte)50,  (byte)50,  (byte)50,  (byte)255)),
        new("Sand",    231,  (ushort)TileFlags.Walkable,         new Color((byte)194, (byte)178, (byte)128, (byte)255)),
        new("StairUp", 420,  (ushort)TileFlags.Walkable,         new Color((byte)180, (byte)150, (byte)80,  (byte)255)),
        new("StairDn", 421,  (ushort)TileFlags.Walkable,         new Color((byte)150, (byte)100, (byte)60,  (byte)255)),
        new("Rope",   3866,  (ushort)TileFlags.Walkable,         new Color((byte)160, (byte)120, (byte)60,  (byte)255)),
    };

    public int SelectedIndex { get; private set; } = 0;
    public PaletteEntry Selected => Entries[SelectedIndex];

    public void Draw(int px, int py)
    {
        const int TS = 32, LH = 16, PAD = 4;
        Raylib.DrawRectangle(px-4, py-24, TS+24, (TS+LH+PAD)*Entries.Length+28,
            new Color((byte)30,(byte)30,(byte)30,(byte)220));
        Raylib.DrawText("PALETTE", px, py-18, 13,
            new Color((byte)200,(byte)200,(byte)200,(byte)255));

        for (int i = 0; i < Entries.Length; i++)
        {
            var e  = Entries[i];
            int ey = py + i*(TS+LH+PAD);
            Raylib.DrawRectangle(px, ey, TS, TS, e.Color);
            if (i == SelectedIndex)
                Raylib.DrawRectangleLines(px-2, ey-2, TS+4, TS+4,
                    new Color((byte)255,(byte)255,(byte)0,(byte)255));
            else
                Raylib.DrawRectangleLines(px, ey, TS, TS,
                    new Color((byte)80,(byte)80,(byte)80,(byte)255));
            Raylib.DrawText(e.Name, px, ey+TS+2, 12,
                new Color((byte)200,(byte)200,(byte)200,(byte)255));

            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                var mp = Raylib.GetMousePosition();
                if (mp.X >= px && mp.X <= px+TS && mp.Y >= ey && mp.Y <= ey+TS)
                    SelectedIndex = i;
            }
        }
    }
}
