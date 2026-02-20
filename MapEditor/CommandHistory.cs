using MapEditor.Maps;

namespace MapEditor;

public interface IEditorCommand
{
    string Description { get; }
    void Execute(MapDocument doc);
    void Undo(MapDocument doc);
}

public sealed class CommandHistory
{
    private readonly Stack<IEditorCommand> _undo = new();
    private readonly Stack<IEditorCommand> _redo = new();

    public int    UndoCount       => _undo.Count;
    public int    RedoCount       => _redo.Count;
    public string LastDescription => _undo.Count > 0 ? _undo.Peek().Description : "";

    public void Execute(IEditorCommand cmd, MapDocument doc)
    { cmd.Execute(doc); _undo.Push(cmd); _redo.Clear(); }

    public void Undo(MapDocument doc)
    { if (_undo.Count == 0) return; var c = _undo.Pop(); c.Undo(doc); _redo.Push(c); }

    public void Redo(MapDocument doc)
    { if (_redo.Count == 0) return; var c = _redo.Pop(); c.Execute(doc); _undo.Push(c); }
}

public sealed class PaintCommand : IEditorCommand
{
    private readonly int _x, _y, _floor;
    private readonly ushort _newId, _newFlags;
    private ushort _oldId, _oldFlags;
    public string Description => $"Paint ({_x},{_y})={_newId}";

    public PaintCommand(int x, int y, int floor, ushort id, ushort flags)
        => (_x,_y,_floor,_newId,_newFlags) = (x,y,floor,id,flags);

    public void Execute(MapDocument doc)
    {
        _oldId    = doc.Tiles[_x,_y,_floor].GroundItemId;
        _oldFlags = (ushort)doc.Tiles[_x,_y,_floor].Flags;
        doc.Tiles[_x,_y,_floor].GroundItemId = _newId;
        doc.Tiles[_x,_y,_floor].Flags        = (TileFlags)_newFlags;
        doc.MarkDirty();
    }

    public void Undo(MapDocument doc)
    {
        doc.Tiles[_x,_y,_floor].GroundItemId = _oldId;
        doc.Tiles[_x,_y,_floor].Flags        = (TileFlags)_oldFlags;
        doc.MarkDirty();
    }
}

public sealed class FloodFillCommand : IEditorCommand
{
    private readonly int _sx, _sy, _floor;
    private readonly ushort _newId, _newFlags;
    private List<(int x,int y,ushort id,ushort flags)> _affected = new();
    public string Description => $"Fill ({_sx},{_sy})={_newId}";

    public FloodFillCommand(int sx, int sy, int floor, ushort id, ushort flags)
        => (_sx,_sy,_floor,_newId,_newFlags) = (sx,sy,floor,id,flags);

    public void Execute(MapDocument doc)
    {
        _affected.Clear();
        ushort targetId = doc.Tiles[_sx,_sy,_floor].GroundItemId;
        if (targetId == _newId) return;
        var queue   = new Queue<(int,int)>();
        var visited = new HashSet<(int,int)>();
        queue.Enqueue((_sx,_sy));
        while (queue.Count > 0)
        {
            var (x,y) = queue.Dequeue();
            if (x<0||x>=doc.Width||y<0||y>=doc.Height||!visited.Add((x,y))) continue;
            if (doc.Tiles[x,y,_floor].GroundItemId != targetId) continue;
            _affected.Add((x,y,doc.Tiles[x,y,_floor].GroundItemId,(ushort)doc.Tiles[x,y,_floor].Flags));
            doc.Tiles[x,y,_floor].GroundItemId = _newId;
            doc.Tiles[x,y,_floor].Flags        = (TileFlags)_newFlags;
            queue.Enqueue((x-1,y)); queue.Enqueue((x+1,y));
            queue.Enqueue((x,y-1)); queue.Enqueue((x,y+1));
        }
        doc.MarkDirty();
    }

    public void Undo(MapDocument doc)
    {
        foreach (var (x,y,id,flags) in _affected)
        {
            doc.Tiles[x,y,_floor].GroundItemId = id;
            doc.Tiles[x,y,_floor].Flags        = (TileFlags)flags;
        }
        doc.MarkDirty();
    }
}

public sealed class CopyPasteCommand : IEditorCommand
{
    private readonly int _dx, _dy, _floor;
    private readonly ushort[,] _srcIds, _srcFlags;
    private ushort[,]? _oldIds, _oldFlags;
    public string Description => $"Paste at ({_dx},{_dy})";

    public CopyPasteCommand(int dx, int dy, int floor, ushort[,] ids, ushort[,] flags)
        => (_dx,_dy,_floor,_srcIds,_srcFlags) = (dx,dy,floor,ids,flags);

    public void Execute(MapDocument doc)
    {
        int w = _srcIds.GetLength(0), h = _srcIds.GetLength(1);
        _oldIds = new ushort[w,h]; _oldFlags = new ushort[w,h];
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            int tx = _dx+x, ty = _dy+y;
            if (tx<0||tx>=doc.Width||ty<0||ty>=doc.Height) continue;
            _oldIds[x,y]   = doc.Tiles[tx,ty,_floor].GroundItemId;
            _oldFlags[x,y] = (ushort)doc.Tiles[tx,ty,_floor].Flags;
            doc.Tiles[tx,ty,_floor].GroundItemId = _srcIds[x,y];
            doc.Tiles[tx,ty,_floor].Flags        = (TileFlags)_srcFlags[x,y];
        }
        doc.MarkDirty();
    }

    public void Undo(MapDocument doc)
    {
        if (_oldIds == null) return;
        int w = _srcIds.GetLength(0), h = _srcIds.GetLength(1);
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            int tx = _dx+x, ty = _dy+y;
            if (tx<0||tx>=doc.Width||ty<0||ty>=doc.Height) continue;
            doc.Tiles[tx,ty,_floor].GroundItemId = _oldIds[x,y];
            doc.Tiles[tx,ty,_floor].Flags        = (TileFlags)_oldFlags![x,y];
        }
        doc.MarkDirty();
    }
}
