using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Tap4D.Rendering;
using Tap4D.Rendering.Raycast;
using Tap4D.Scene;

namespace Tap4D.Viewer;

/// <summary>
/// The main window: renders the demo tesseract scene every frame into a WriteableBitmap,
/// alternating overlapping surfaces by frame. The mouse is captured to the window center
/// for first-person look control.
/// </summary>
public partial class MainWindow : Window
{
    private IRenderer _renderer;
    private readonly WriteableBitmap _bitmap;
    private readonly Camera4 _camera = new() { Position = new Vector4(0, 0, -4, 0) };
    private readonly FpsCameraController _controller = new();

    /// <summary>
    /// The current camera position. Internal to allow integration tests to verify movement.
    /// </summary>
    internal Vector4 CameraPositionForTest => _camera.Position;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private DemoScene.Mode _mode = DemoScene.Mode.Wireframe;
    private bool _raycast = true;
    private long _lastFrame;
    private int _fpsLastFrameIndex;
    private long _fpsWindowStart;

    /// <summary>
    /// Creates the window, picking the GPU renderers when a D3D12 device is available
    /// and falling back to the slower CPU reference renderer at reduced resolution otherwise.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        int size = GpuRenderer.IsAvailable ? 800 : 384;
        _renderer = CreateRenderer(size);
        _bitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
        RenderImage.Source = _bitmap;
        CompositionTarget.Rendering += OnRender;
    }

    /// <summary>
    /// Creates the current renderer: the GPU raycaster by default, the planar GPU renderer
    /// when toggled, or the planar CPU reference renderer when no GPU is available.
    /// </summary>
    /// <param name="size">The image size in pixels.</param>
    /// <returns>The renderer.</returns>
    private IRenderer CreateRenderer(int size)
    {
        IRenderer renderer = !GpuRenderer.IsAvailable
            ? new CpuRenderer(size, size, maxFps: 60)
            : _raycast
                ? new GpuRaycastRenderer(size, size, slices: 64, maxFps: 60)
                : new GpuRenderer(size, size, maxFps: 60);
        renderer.ViewHeight = 2.5f;
        renderer.MinW = -1.2f;
        renderer.MaxW = 1.2f;
        renderer.Palette = new AnaKataPalette { MinW = -1.2f, MaxW = 1.2f };
        if (renderer is GpuRaycastRenderer raycast)
            raycast.SegmentWScale = 0.25f;
        return renderer;
    }

    /// <summary>
    /// Subscribes to raw mouse input and confines the cursor once the window handle exists.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _ = new RawMouseInput(this, _controller.OnMouseDelta);
        _ = new CursorConfiner(this);
    }

    /// <summary>
    /// Releases renderer resources.
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        (_renderer as IDisposable)?.Dispose();
        base.OnClosed(e);
    }

    /// <summary>
    /// Renders and presents one frame. Internal to allow integration tests to drive it.
    /// </summary>
    internal void OnRender(object? sender, EventArgs e)
    {
        long now = _clock.ElapsedTicks;
        float dt = (float)(now - _lastFrame) / Stopwatch.Frequency;
        _lastFrame = now;
        _controller.Update(_camera, dt);
        byte[] pixels = _renderer.Render(DemoScene.Build(_mode), _camera);
        _bitmap.WritePixels(new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight), pixels, _bitmap.PixelWidth * 4, 0);
        UpdateFps(now);
    }

    /// <summary>
    /// Updates the FPS counter in the window title twice per second, based on the number
    /// of frames the renderer actually produced.
    /// </summary>
    /// <param name="now">The current timestamp in stopwatch ticks.</param>
    private void UpdateFps(long now)
    {
        double elapsed = (double)(now - _fpsWindowStart) / Stopwatch.Frequency;
        if (elapsed >= 0.5)
        {
            double fps = (_renderer.FrameIndex - _fpsLastFrameIndex) / elapsed;
            _fpsLastFrameIndex = _renderer.FrameIndex;
            Title = $"Tap4D Viewer ({_renderer.GetType().Name}, {fps:F0} fps, near={_camera.Near:F3}, z={_camera.Position.Z:F2})";
            _fpsWindowStart = now;
        }
    }

    /// <summary>
    /// Handles key presses: M cycles the display mode, P toggles between the planar and
    /// the raycasting renderer, +/- scale the near plane distance for calibration,
    /// Escape closes the window, the rest go to the camera controller.
    /// </summary>
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.M && !e.IsRepeat)
            _mode = (DemoScene.Mode)(((int)_mode + 1) % 3);
        else if (e.Key == Key.P && !e.IsRepeat && GpuRenderer.IsAvailable)
        {
            (_renderer as IDisposable)?.Dispose();
            _raycast = !_raycast;
            _renderer = CreateRenderer(_bitmap.PixelWidth);
        }
        else if (e.Key is Key.OemPlus or Key.Add)
            _camera.Near = MathF.Min(_camera.Near * 1.2f, 2f);
        else if (e.Key is Key.OemMinus or Key.Subtract)
            _camera.Near = MathF.Max(_camera.Near / 1.2f, 0.001f);
        else if (e.Key == Key.Escape)
            Close();
        else
            _controller.Press(e.Key);
    }

    /// <summary>
    /// Handles key releases.
    /// </summary>
    private void OnKeyUp(object sender, KeyEventArgs e) => _controller.Release(e.Key);

    /// <summary>
    /// Handles mouse wheel rotation (XW plane).
    /// </summary>
    private void OnMouseWheel(object sender, MouseWheelEventArgs e) => _controller.OnWheel(e.Delta);

    /// <summary>
    /// Handles the additional mouse buttons (lean in the YW plane).
    /// </summary>
    private void OnMouseButton(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.XButton1)
            _controller.OnLeanButton(-1, e.ButtonState == MouseButtonState.Pressed);
        else if (e.ChangedButton == MouseButton.XButton2)
            _controller.OnLeanButton(1, e.ButtonState == MouseButtonState.Pressed);
    }
}
