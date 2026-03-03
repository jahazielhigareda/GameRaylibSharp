using Raylib_cs;

namespace Client.GUI;

/// <summary>
/// Manages a z-ordered list of <see cref="GuiWindow"/> instances.
///
/// Z-order convention: index 0 = bottom (drawn first), last index = top (drawn last,
/// processed first for input).
///
/// Usage per frame:
///   1. <see cref="Update"/> — process mouse events.
///   2. <see cref="Render"/> — draw all windows.
/// </summary>
public class GuiWindowManager
{
    private readonly List<GuiWindow> _windows = new();

    /// <summary>Register a window. Windows are drawn in insertion order (lowest z first).</summary>
    public void AddWindow(GuiWindow window) => _windows.Add(window);

    /// <summary>
    /// Find a registered window by its title. Returns null if not found.
    /// </summary>
    public GuiWindow? FindByTitle(string title) =>
        _windows.Find(w => w.Title == title);

    /// <summary>
    /// Process mouse interaction in reverse z-order (topmost window first).
    /// Implements drag, minimize, close, and bring-to-front.
    /// </summary>
    public void Update()
    {
        var  mousePos  = Raylib.GetMousePosition();
        int  mx        = (int)mousePos.X;
        int  my        = (int)mousePos.Y;
        bool lPressed  = Raylib.IsMouseButtonPressed(MouseButton.Left);
        bool lReleased = Raylib.IsMouseButtonReleased(MouseButton.Left);

        bool clickConsumed = false;

        // Process topmost window first (reverse iteration)
        for (int i = _windows.Count - 1; i >= 0; i--)
        {
            var win = _windows[i];
            if (!win.Visible) continue;

            // Continue updating an in-progress drag regardless of z-order
            if (win.IsDragging)
            {
                win.UpdateDrag(mx, my);
                if (lReleased) win.EndDrag();
                clickConsumed = true;
                continue;
            }

            if (clickConsumed) continue;

            if (lPressed && win.InWindow(mx, my))
            {
                clickConsumed = true;

                // Bring to front (move to end of list)
                if (i < _windows.Count - 1)
                {
                    _windows.RemoveAt(i);
                    _windows.Add(win);
                }

                // Buttons take priority over drag
                if (win.InCloseButton(mx, my))
                    win.Close();
                else if (win.InMinimizeButton(mx, my))
                    win.ToggleMinimize();
                else if (win.InTitleBar(mx, my))
                    win.BeginDrag(mx, my);
            }
        }
    }

    /// <summary>Draw all visible windows bottom-to-top (index 0 first).</summary>
    public void Render()
    {
        foreach (var win in _windows)
            win.Draw();
    }
}
