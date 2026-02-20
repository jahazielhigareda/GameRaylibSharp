using MapEditor.Maps;
using Raylib_cs;
using System.Numerics;

namespace MapEditor;

public enum EditMode { Paint, Fill, Select, Hand }

public sealed class MapEditorWindow
{
    private MapDocument    _doc;
    private CommandHistory _history = new();
    private TilePalette    _palette = new();

    private float _camX = 4f, _camY = 4f, _zoom = 2f;
    private const int CanvasX = 170, CanvasY = 32;

    private int      _floor      = 0;
    private EditMode _mode       = EditMode.Paint;
    private bool     _isPainting = false;
    private string   _status     = "Ready  |  B=Paint  Space=Pan  M=Select  Ctrl+Click=Fill  Ctrl+S=Save";

    private bool    _hasSelection = false;
    private bool    _selecting    = false;
    private int     _selX, _selY, _selW = 1, _selH = 1;
    private ushort[,]? _clipIds;
    private ushort[,]? _clipFlags;

    private Vector2 _panStart;
    private bool    _panning = false;

    public MapEditorWindow(MapDocument doc) => _doc = doc;

    public void Update()
    {
        HandleKeyboard();
        HandleMouse();
        Raylib.ClearBackground(new Color((byte)20,(byte)20,(byte)20,(byte)255));
        DrawCanvas();
        DrawPalette();
        DrawTopBar();
        DrawStatusBar();
    }

    private void HandleKeyboard()
    {
        bool ctrl = Raylib.IsKeyDown(KeyboardKey.LeftControl) ||
                    Raylib.IsKeyDown(KeyboardKey.RightControl);

        for (int i = 0; i < 8; i++)
            if (Raylib.IsKeyPressed(KeyboardKey.F1 + i)) _floor = i;

        if (ctrl && Raylib.IsKeyPressed(KeyboardKey.Z)) { _history.Undo(_doc); _status = "Undo"; }
        if (ctrl && Raylib.IsKeyPressed(KeyboardKey.Y)) { _history.Redo(_doc); _status = "Redo"; }
        if (ctrl && Raylib.IsKeyPressed(KeyboardKey.S)) Save();
        if (ctrl && Raylib.IsKeyPressed(KeyboardKey.O)) Open();
        if (ctrl && Raylib.IsKeyPressed(KeyboardKey.C)) CopySelection();
        if (ctrl && Raylib.IsKeyPressed(KeyboardKey.V)) PasteClipboard();

        if (!ctrl)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.B))     _mode = EditMode.Paint;
            if (Raylib.IsKeyPressed(KeyboardKey.Space)) _mode = EditMode.Hand;
            if (Raylib.IsKeyPressed(KeyboardKey.M))     _mode = EditMode.Select;
        }

        float spd = 3f / _zoom;
        if (Raylib.IsKeyDown(KeyboardKey.W)) _camY += spd;
        if (Raylib.IsKeyDown(KeyboardKey.S)) _camY -= spd;
        if (Raylib.IsKeyDown(KeyboardKey.A)) _camX += spd;
        if (Raylib.IsKeyDown(KeyboardKey.D)) _camX -= spd;

        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0) _zoom = Math.Clamp(_zoom * (wheel > 0 ? 1.15f : 0.87f), 0.25f, 10f);
    }

    private void HandleMouse()
    {
        var  mp       = Raylib.GetMousePosition();
        int  sw       = Raylib.GetScreenWidth();
        int  sh       = Raylib.GetScreenHeight();
        bool onCanvas = mp.X > CanvasX && mp.Y > CanvasY &&
                        mp.X < sw - 2   && mp.Y < sh - 22;

        // Middle-drag pan
        if (Raylib.IsMouseButtonPressed(MouseButton.Middle))  { _panStart = mp; _panning = true; }
        if (Raylib.IsMouseButtonReleased(MouseButton.Middle)) _panning = false;
        if (_panning && _mode != EditMode.Hand)
        {
            var d = Vector2.Subtract(mp, _panStart);
            _camX += d.X / _zoom; _camY += d.Y / _zoom; _panStart = mp;
        }

        if (!onCanvas) return;

        int ts    = (int)(32 * _zoom);
        int tileX = (int)Math.Floor((mp.X - CanvasX - _camX * _zoom) / ts);
        int tileY = (int)Math.Floor((mp.Y - CanvasY - _camY * _zoom) / ts);
        bool inBounds = tileX >= 0 && tileX < _doc.Width &&
                        tileY >= 0 && tileY < _doc.Height;

        // Hand mode: left-drag pan
        if (_mode == EditMode.Hand)
        {
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))  { _panStart = mp; _panning = true; }
            if (Raylib.IsMouseButtonReleased(MouseButton.Left)) _panning = false;
            if (_panning)
            {
                var d = Vector2.Subtract(mp, _panStart);
                _camX += d.X / _zoom; _camY += d.Y / _zoom; _panStart = mp;
            }
            return;
        }

        if (_mode == EditMode.Paint)
        {
            bool ctrl = Raylib.IsKeyDown(KeyboardKey.LeftControl);
            if (ctrl && Raylib.IsMouseButtonPressed(MouseButton.Left) && inBounds)
            {
                var sel = _palette.Selected;
                _history.Execute(new FloodFillCommand(tileX,tileY,_floor,sel.GroundId,sel.Flags), _doc);
                _status = $"FloodFill ({tileX},{tileY})";
            }
            else if (!ctrl && Raylib.IsMouseButtonDown(MouseButton.Left) && inBounds)
            {
                var sel = _palette.Selected;
                if (!_isPainting || _doc.Tiles[tileX,tileY,_floor].GroundItemId != sel.GroundId)
                    _history.Execute(new PaintCommand(tileX,tileY,_floor,sel.GroundId,sel.Flags), _doc);
                _isPainting = true;
                _status = $"Painted ({tileX},{tileY}) id={sel.GroundId}";
            }
            if (Raylib.IsMouseButtonReleased(MouseButton.Left)) _isPainting = false;
        }
        else if (_mode == EditMode.Select)
        {
            if (Raylib.IsMouseButtonPressed(MouseButton.Left) && inBounds)
            { _selX=tileX; _selY=tileY; _selW=1; _selH=1; _selecting=true; }
            if (_selecting && Raylib.IsMouseButtonDown(MouseButton.Left) && inBounds)
            { _selW=Math.Max(1,tileX-_selX+1); _selH=Math.Max(1,tileY-_selY+1); }
            if (Raylib.IsMouseButtonReleased(MouseButton.Left))
            { _selecting=false; _hasSelection=true; _status=$"Selected ({_selX},{_selY}) {_selW}x{_selH}"; }
        }
    }

    private void DrawCanvas()
    {
        int ts = Math.Max(1, (int)(32 * _zoom));
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();

        Raylib.BeginScissorMode(CanvasX, CanvasY, sw-CanvasX, sh-CanvasY-22);

        int minTX = Math.Max(0, (int)Math.Floor(-_camX)-1);
        int minTY = Math.Max(0, (int)Math.Floor(-_camY)-1);
        int maxTX = Math.Min(_doc.Width -1, minTX+(sw-CanvasX)/ts+2);
        int maxTY = Math.Min(_doc.Height-1, minTY+(sh-CanvasY-22)/ts+2);

        for (int ty = minTY; ty <= maxTY; ty++)
        for (int tx = minTX; tx <= maxTX; tx++)
        {
            int sx = CanvasX+(int)((tx+_camX)*ts);
            int sy = CanvasY+(int)((ty+_camY)*ts);
            var tile = _doc.Tiles[tx,ty,_floor];
            Raylib.DrawRectangle(sx,sy,ts,ts, TileColor(tile.GroundItemId));
            if (tile.GroundItemId==2700 && ts>=6)
                Raylib.DrawRectangle(sx+ts/4,sy-ts/3,ts/2,ts*2/3,
                    new Color((byte)0,(byte)50,(byte)0,(byte)210));
            if (ts >= 4)
                Raylib.DrawRectangleLines(sx,sy,ts,ts,
                    new Color((byte)45,(byte)45,(byte)45,(byte)100));
        }

        if (_hasSelection)
        {
            int sx = CanvasX+(int)((_selX+_camX)*ts);
            int sy = CanvasY+(int)((_selY+_camY)*ts);
            Raylib.DrawRectangle(sx,sy,_selW*ts,_selH*ts,
                new Color((byte)255,(byte)255,(byte)0,(byte)35));
            Raylib.DrawRectangleLines(sx,sy,_selW*ts,_selH*ts,
                new Color((byte)255,(byte)255,(byte)0,(byte)220));
        }

        Raylib.EndScissorMode();
        Raylib.DrawRectangleLines(CanvasX,CanvasY,sw-CanvasX,sh-CanvasY-22,
            new Color((byte)80,(byte)80,(byte)80,(byte)255));
    }

    private static Color TileColor(ushort id) => id switch
    {
        1231 => new Color((byte)34, (byte)139,(byte)34, (byte)255),
        4608 => new Color((byte)0,  (byte)0,  (byte)160,(byte)255),
        1055 => new Color((byte)110,(byte)110,(byte)110,(byte)255),
        2700 => new Color((byte)30, (byte)100,(byte)30, (byte)255),
        1    => new Color((byte)45, (byte)45, (byte)45, (byte)255),
        231  => new Color((byte)194,(byte)178,(byte)128,(byte)255),
        420  => new Color((byte)180,(byte)150,(byte)80, (byte)255),
        421  => new Color((byte)150,(byte)100,(byte)60, (byte)255),
        3866 => new Color((byte)160,(byte)120,(byte)60, (byte)255),
        _    => new Color((byte)55, (byte)55, (byte)55, (byte)255),
    };

    private void DrawPalette() => _palette.Draw(6, 55);

    private void DrawTopBar()
    {
        int sw = Raylib.GetScreenWidth();
        Raylib.DrawRectangle(0,0,sw,30, new Color((byte)38,(byte)38,(byte)38,(byte)255));

        string title = (_doc.FilePath != null ? Path.GetFileName(_doc.FilePath) : "Untitled")
                     + (_doc.IsDirty ? " *" : "");
        Raylib.DrawText($"GMAP Editor  |  {title}  |  Floor:{_floor+1}/8  |  Mode:{_mode}  |  Zoom:{_zoom:F1}x",
            6, 8, 13, new Color((byte)215,(byte)215,(byte)215,(byte)255));

        // Floor buttons (top-right)
        int bx = sw-170;
        for (int i = 0; i < 8; i++)
        {
            Color bc = (i==_floor)
                ? new Color((byte)90,(byte)170,(byte)90,(byte)255)
                : new Color((byte)65,(byte)65,(byte)65,(byte)255);
            Raylib.DrawRectangle(bx+i*20,2,18,24,bc);
            Raylib.DrawText($"{i+1}", bx+i*20+5,7,11,
                new Color((byte)220,(byte)220,(byte)220,(byte)255));
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                var mp = Raylib.GetMousePosition();
                if (mp.X>=bx+i*20&&mp.X<=bx+i*20+18&&mp.Y>=2&&mp.Y<=26) _floor=i;
            }
        }
    }

    private void DrawStatusBar()
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        Raylib.DrawRectangle(0,sh-22,sw,22,new Color((byte)33,(byte)33,(byte)33,(byte)255));

        var  mp    = Raylib.GetMousePosition();
        int  ts    = Math.Max(1,(int)(32*_zoom));
        int  tileX = (int)Math.Floor((mp.X-CanvasX-_camX*_zoom)/ts);
        int  tileY = (int)Math.Floor((mp.Y-CanvasY-_camY*_zoom)/ts);
        ushort gid = (tileX>=0&&tileX<_doc.Width&&tileY>=0&&tileY<_doc.Height)
                   ? _doc.Tiles[tileX,tileY,_floor].GroundItemId : (ushort)0;

        Raylib.DrawText(
            $"({tileX},{tileY}) id={gid}  Undo:{_history.UndoCount} Redo:{_history.RedoCount}" +
            $"  {_doc.Width}x{_doc.Height}  {_status}",
            6,sh-17,12,new Color((byte)175,(byte)175,(byte)175,(byte)255));
    }

    private void Save()
    {
        string path = _doc.FilePath ?? "sample.map";
        try   { _doc.Save(path); _status = $"Saved {Path.GetFileName(path)}"; }
        catch (Exception ex) { _status = $"Save error: {ex.Message}"; }
    }

    private void Open()
    {
        string[] candidates = { "sample.map", "../Server/Maps/world.map" };
        foreach (var p in candidates)
        {
            if (!File.Exists(p)) continue;
            try { _doc=MapDocument.Load(p); _history=new(); _status=$"Opened {Path.GetFileName(p)}"; return; }
            catch { }
        }
        _status = "No map file found (sample.map / world.map)";
    }

    private void CopySelection()
    {
        if (!_hasSelection) { _status="Nothing selected"; return; }
        (_clipIds,_clipFlags) = _doc.CopyRegion(_selX,_selY,_selW,_selH,_floor);
        _status = $"Copied {_selW}x{_selH}";
    }

    private void PasteClipboard()
    {
        if (_clipIds==null) { _status="Clipboard empty"; return; }
        var mp    = Raylib.GetMousePosition();
        int ts    = Math.Max(1,(int)(32*_zoom));
        int tileX = (int)Math.Floor((mp.X-CanvasX-_camX*_zoom)/ts);
        int tileY = (int)Math.Floor((mp.Y-CanvasY-_camY*_zoom)/ts);
        _history.Execute(new CopyPasteCommand(tileX,tileY,_floor,_clipIds,_clipFlags!),_doc);
        _status = $"Pasted at ({tileX},{tileY})";
    }
}
