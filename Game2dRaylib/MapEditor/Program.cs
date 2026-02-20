using MapEditor;
using MapEditor.Maps;
using Raylib_cs;

Raylib.InitWindow(1280, 800, "GMAP Map Editor");
Raylib.SetTargetFPS(60);

MapDocument doc = TryLoadExisting() ?? CreateSampleMap();
var editor = new MapEditorWindow(doc);

while (!Raylib.WindowShouldClose())
{
    Raylib.BeginDrawing();
    editor.Update();
    Raylib.EndDrawing();
}

Raylib.CloseWindow();

// ── Helpers ───────────────────────────────────────────────────────────────────

static MapDocument? TryLoadExisting()
{
    string[] paths = { "sample.map", "../Server/Maps/world.map" };
    foreach (var p in paths)
    {
        if (!File.Exists(p)) continue;
        try { return MapDocument.Load(p); } catch { }
    }
    return null;
}

static MapDocument CreateSampleMap()
{
    const ushort W = 64, H = 64;
    var doc = new MapDocument(W, H, 8, 7);

    void Set(int x, int y, int z, ushort id, TileFlags f)
    {
        if (x<0||x>=W||y<0||y>=H) return;
        doc.Tiles[x,y,z].GroundItemId = id;
        doc.Tiles[x,y,z].Flags        = f;
        doc.Tiles[x,y,z].Items      ??= Array.Empty<ItemInstance>();
    }

    // Ground floor: fill grass
    for (int x=0;x<W;x++) for (int y=0;y<H;y++) Set(x,y,0,1231,TileFlags.Walkable);

    // Stone border
    for (int x=0;x<W;x++) { Set(x,0,0,1,TileFlags.None); Set(x,H-1,0,1,TileFlags.None); }
    for (int y=0;y<H;y++) { Set(0,y,0,1,TileFlags.None); Set(W-1,y,0,1,TileFlags.None); }

    // Circular lake
    for (int x=0;x<W;x++) for (int y=0;y<H;y++)
        if (Math.Sqrt(Math.Pow(x-32,2)+Math.Pow(y-32,2))<7)
            Set(x,y,0,4608,TileFlags.None);

    // Stone path
    int[] py = {20,21,20,19,18,19,20,21,22,21,20,19,20,21,22,23,22,21,20,19};
    for (int i=0;i<py.Length;i++) Set(5+i*2,py[i],0,1055,TileFlags.Walkable);

    // Forest patches
    var rng = new Random(42);
    foreach (var (ox,oy) in new[]{(8,8),(48,10),(12,45),(50,50),(28,14)})
        for (int dx=0;dx<6;dx++) for (int dy=0;dy<6;dy++)
            if (rng.NextDouble()>0.35) Set(ox+dx,oy+dy,0,2700,TileFlags.BlockProjectile);

    // Houses
    foreach (var (hx,hy) in new[]{(15,15),(40,20),(18,42)})
    {
        for (int dx=0;dx<6;dx++) { Set(hx+dx,hy,0,1,TileFlags.None); Set(hx+dx,hy+5,0,1,TileFlags.None); }
        for (int dy=0;dy<6;dy++) { Set(hx,hy+dy,0,1,TileFlags.None); Set(hx+5,hy+dy,0,1,TileFlags.None); }
        for (int dx=1;dx<5;dx++) for (int dy=1;dy<5;dy++) Set(hx+dx,hy+dy,0,1055,TileFlags.Walkable);
        Set(hx+2,hy+5,0,1055,TileFlags.Walkable); // door
    }

    // Spawn (sand)
    Set(W/2,H/2,0,231,TileFlags.Walkable);

    // Underground: stone
    for (int z=1;z<8;z++) for (int x=0;x<W;x++) for (int y=0;y<H;y++) Set(x,y,z,1055,TileFlags.None);

    doc.FilePath = "sample.map";
    doc.Save("sample.map");
    return doc;
}
