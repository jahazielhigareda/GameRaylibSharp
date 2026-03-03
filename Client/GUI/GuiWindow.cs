using Raylib_cs;

namespace Client.GUI;

/// <summary>
/// A draggable, minimizable, closeable game window.
///
/// The window consists of:
///   • A title bar (drag handle + minimize button + close button)
///   • An optional content area rendered via the <see cref="DrawContent"/> delegate.
///
/// All interaction is driven by <see cref="GuiWindowManager"/>; this class
/// only holds state and provides drawing.
/// </summary>
public class GuiWindow
{
    private const int TitleBarH  = 18;
    private const int ButtonSize = 14;
    private const int ButtonPad  = 2;

    public string Title   { get; set; }
    public int    X       { get; set; }
    public int    Y       { get; set; }
    public int    Width   { get; }
    public int    Height  { get; }
    public bool   Visible { get; set; } = true;
    public bool   Minimized { get; private set; }

    // Drag state
    private bool _dragging;
    private int  _dragOffsetX, _dragOffsetY;

    /// <summary>
    /// Invoked each frame when the window is visible and not minimized.
    /// Parameters: inner x, inner y, inner width, inner height.
    /// </summary>
    public Action<int, int, int, int>? DrawContent { get; set; }

    public GuiWindow(string title, int x, int y, int width, int height)
    {
        Title  = title;
        X      = x;
        Y      = y;
        Width  = width;
        Height = height;
    }

    // ── Internal state (read by GuiWindowManager) ─────────────────────────

    internal bool IsDragging => _dragging;

    // ── Interaction (called by GuiWindowManager) ──────────────────────────

    internal void BeginDrag(int mouseX, int mouseY)
    {
        _dragging    = true;
        _dragOffsetX = mouseX - X;
        _dragOffsetY = mouseY - Y;
    }

    internal void UpdateDrag(int mouseX, int mouseY)
    {
        X = mouseX - _dragOffsetX;
        Y = mouseY - _dragOffsetY;
    }

    internal void EndDrag()          => _dragging = false;
    internal void ToggleMinimize()   => Minimized = !Minimized;
    internal void Close()            => Visible   = false;

    // ── Hit tests ─────────────────────────────────────────────────────────

    /// <summary>True when the mouse is anywhere inside the window frame.</summary>
    internal bool InWindow(int mx, int my)
    {
        int totalH = Minimized ? TitleBarH : Height;
        return mx >= X && mx < X + Width && my >= Y && my < Y + totalH;
    }

    internal bool InTitleBar(int mx, int my) =>
        mx >= X && mx < X + Width && my >= Y && my < Y + TitleBarH;

    internal bool InMinimizeButton(int mx, int my)
    {
        int bx = X + Width - (ButtonSize + ButtonPad) * 2;
        return mx >= bx && mx < bx + ButtonSize &&
               my >= Y + ButtonPad && my < Y + ButtonPad + ButtonSize;
    }

    internal bool InCloseButton(int mx, int my)
    {
        int bx = X + Width - ButtonSize - ButtonPad;
        return mx >= bx && mx < bx + ButtonSize &&
               my >= Y + ButtonPad && my < Y + ButtonPad + ButtonSize;
    }

    // ── Drawing ───────────────────────────────────────────────────────────

    public void Draw()
    {
        if (!Visible) return;

        int totalH = Minimized ? TitleBarH : Height;

        // Window background
        Raylib.DrawRectangle(X, Y, Width, totalH, new Color(20, 20, 30, 220));
        Raylib.DrawRectangleLines(X, Y, Width, totalH, new Color(100, 100, 120, 255));

        // Title bar background
        Raylib.DrawRectangle(X + 1, Y + 1, Width - 2, TitleBarH - 2,
            new Color(50, 50, 80, 255));

        // Title text
        Raylib.DrawText(Title, X + 6, Y + 3, 12, Color.Gold);

        // Minimize button  (─ or +)
        int minBx = X + Width - (ButtonSize + ButtonPad) * 2;
        Raylib.DrawRectangle(minBx, Y + ButtonPad, ButtonSize, ButtonSize,
            new Color(80, 80, 40, 255));
        Raylib.DrawText(Minimized ? "+" : "-", minBx + 4, Y + ButtonPad + 2, 10, Color.Yellow);

        // Close button (x)
        int closeBx = X + Width - ButtonSize - ButtonPad;
        Raylib.DrawRectangle(closeBx, Y + ButtonPad, ButtonSize, ButtonSize,
            new Color(120, 30, 30, 255));
        Raylib.DrawText("x", closeBx + 4, Y + ButtonPad + 2, 10, Color.White);

        if (Minimized) return;

        // Separator line below title bar
        Raylib.DrawLine(X + 1, Y + TitleBarH, X + Width - 1, Y + TitleBarH,
            new Color(80, 80, 100, 200));

        // Content area
        int innerX = X + 6;
        int innerY = Y + TitleBarH + 4;
        int innerW = Width - 12;
        int innerH = Height - TitleBarH - 8;
        DrawContent?.Invoke(innerX, innerY, innerW, innerH);
    }
}
