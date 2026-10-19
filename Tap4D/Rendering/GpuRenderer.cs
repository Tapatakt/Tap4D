using System.Diagnostics;
using ComputeSharp;
using Tap4D.Geometry;
using Tap4D.Scene;

namespace Tap4D.Rendering;

/// <summary>
/// GPU renderer: uploads the clipped camera-space geometry and runs <see cref="RasterShader"/>,
/// one thread per pixel. Mirrors <see cref="CpuRenderer"/> exactly.
/// Buffers are reused across frames to avoid per-frame allocation churn.
/// </summary>
/// <remarks>
/// Creates a renderer for images of the given size in pixels.
/// </remarks>
/// <param name="width">The image width in pixels.</param>
/// <param name="height">The image height in pixels.</param>
/// <param name="maxFps">
/// The frame rate limit; calls faster than this return the cached frame without
/// re-rendering. Zero (the default) means no limit.
/// </param>
public class GpuRenderer(int width, int height, int maxFps = 0) : IRenderer, IDisposable
{
    private readonly GraphicsDevice _device = GraphicsDevice.GetDefault();
    private readonly double _minFrameMs = maxFps > 0 ? 1000.0 / maxFps : 0;
    private readonly Stopwatch _throttleWatch = Stopwatch.StartNew();
    private long _lastRenderMs = long.MinValue;
    private ReadOnlyBuffer<GpuTetrahedron>? _visibleBuffer;
    private ReadOnlyBuffer<GpuTetrahedron>? _occluderBuffer;
    private ReadOnlyBuffer<GpuSegment>? _segmentBuffer;
    private ReadWriteBuffer<int>? _output;
    private int[]? _readback;
    private byte[]? _frame;


    /// <summary>
    /// Whether a D3D12 device is available on this machine.
    /// </summary>
    public static bool IsAvailable => GraphicsDevice.EnumerateDevices().Any();

    /// <summary>The image width in pixels.</summary>
    public int Width { get; } = width;

    /// <summary>The image height in pixels.</summary>
    public int Height { get; } = height;

    /// <summary>The height of the virtual screen in world units.</summary>
    public float ViewHeight { get; set; } = 2f;

    /// <summary>The frame counter driving the alternation of overlapping surfaces.</summary>
    public int FrameIndex { get; set; }

    /// <summary>The mapping from ana-kata to color.</summary>
    public AnaKataPalette Palette { get; set; } = new();

    /// <summary>The lower ana-kata bound of the virtual screen.</summary>
    public float MinW { get; set; } = -2f;

    /// <summary>The upper ana-kata bound of the virtual screen.</summary>
    public float MaxW { get; set; } = 2f;

    /// <summary>
    /// Renders the scene into a BGRA pixel buffer (4 bytes per pixel, row-major, top to bottom).
    /// The returned buffer is reused across calls: copy it if you need to retain a frame.
    /// </summary>
    /// <param name="scene">The scene to render.</param>
    /// <param name="camera">The camera to render from.</param>
    /// <returns>The BGRA pixel buffer (reused across calls).</returns>
    public byte[] Render(Scene4 scene, Camera4 camera)
    {
        long now = _throttleWatch.ElapsedMilliseconds;
        if (_minFrameMs > 0 && now - _lastRenderMs < _minFrameMs && _frame != null)
            return _frame;
        _lastRenderMs = now;
        CameraSpaceGeometry.Prepare(scene, camera, MinW, MaxW, out List<Tetrahedron> visible, out List<Tetrahedron> occluders, out List<Segment4> segments);
        UploadGeometry(visible, occluders, segments);
        _output ??= _device.AllocateReadWriteBuffer<int>(Width * Height);
        _readback ??= new int[Width * Height];
        _frame ??= new byte[Width * Height * 4];
        RasterShader shader = new(
            _visibleBuffer!, _occluderBuffer!, _segmentBuffer!, _output,
            visible.Count, occluders.Count, segments.Count,
            Width, Height, ViewHeight / Height, camera.Focal,
            FrameIndex, MinW, 1f / (MaxW - MinW), Palette.Green);
        _device.For(Width * Height, shader);
        _output.CopyTo(_readback);
        Buffer.BlockCopy(_readback, 0, _frame, 0, _frame.Length);
        FrameIndex++;
        return _frame;
    }

    /// <summary>
    /// Releases the GPU buffers.
    /// </summary>
    public void Dispose()
    {
        _visibleBuffer?.Dispose();
        _occluderBuffer?.Dispose();
        _segmentBuffer?.Dispose();
        _output?.Dispose();
    }

    /// <summary>
    /// Uploads the prepared geometry into persistent GPU buffers, growing them when needed.
    /// </summary>
    private void UploadGeometry(List<Tetrahedron> visible, List<Tetrahedron> occluders, List<Segment4> segments)
    {
        GpuBuffers.EnsureCapacity(_device, ref _visibleBuffer, visible.Count);
        GpuBuffers.EnsureCapacity(_device, ref _occluderBuffer, occluders.Count);
        GpuBuffers.EnsureCapacity(_device, ref _segmentBuffer, segments.Count);
        if (visible.Count > 0)
            _visibleBuffer!.CopyFrom(ToGpu(visible));
        if (occluders.Count > 0)
            _occluderBuffer!.CopyFrom(ToGpu(occluders));
        if (segments.Count > 0)
            _segmentBuffer!.CopyFrom(ToGpu(segments));
    }

    /// <summary>
    /// Converts tetrahedra to the GPU layout.
    /// </summary>
    /// <param name="source">The clipped camera-space tetrahedra.</param>
    /// <returns>The GPU tetrahedra.</returns>
    private static GpuTetrahedron[] ToGpu(List<Tetrahedron> source) =>
        [.. source.Select(t => new GpuTetrahedron { A = t.A, B = t.B, C = t.C, D = t.D })];

    /// <summary>
    /// Converts segments to the GPU layout.
    /// </summary>
    /// <param name="source">The clipped camera-space segments.</param>
    /// <returns>The GPU segments.</returns>
    private static GpuSegment[] ToGpu(List<Segment4> source) =>
        [.. source.Select(s => new GpuSegment { Start = s.Start, End = s.End, Radius = s.Radius })];
}
