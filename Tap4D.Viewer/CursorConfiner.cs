using System.Runtime.InteropServices;
using System.Windows;

namespace Tap4D.Viewer;

/// <summary>
/// Confines the mouse cursor to the window's client area while the window is active,
/// so first-person mouse control cannot lose or misplace the cursor.
/// The confinement is released when the window is deactivated or closed.
/// </summary>
internal sealed class CursorConfiner
{
    private readonly Window _window;

    /// <summary>
    /// Attaches cursor confinement to the window.
    /// </summary>
    /// <param name="window">The window to confine the cursor to.</param>
    public CursorConfiner(Window window)
    {
        _window = window;
        _window.Activated += (_, _) => Confine();
        _window.Deactivated += (_, _) => Release();
        _window.StateChanged += (_, _) => Confine();
        _window.LocationChanged += (_, _) => Confine();
        _window.SizeChanged += (_, _) => Confine();
        _window.Closed += (_, _) => Release();
        Confine();
    }

    /// <summary>
    /// Clips the cursor to the current client rectangle of the window.
    /// </summary>
    private void Confine()
    {
        if (!_window.IsActive || _window.WindowState == WindowState.Minimized)
            return;
        Point topLeft = _window.PointToScreen(new Point(0, 0));
        Point bottomRight = _window.PointToScreen(new Point(_window.ActualWidth, _window.ActualHeight));
        Rect rect = new((int)topLeft.X, (int)topLeft.Y, (int)bottomRight.X, (int)bottomRight.Y);
        ClipCursor(ref rect);
    }

    /// <summary>
    /// Releases the cursor confinement.
    /// </summary>
    private static void Release() => ClipCursor(IntPtr.Zero);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect(int left, int top, int right, int bottom)
    {
        public int Left = left;
        public int Top = top;
        public int Right = right;
        public int Bottom = bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool ClipCursor(ref Rect lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClipCursor(IntPtr lpRect);
}
