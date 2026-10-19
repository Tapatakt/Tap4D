using System.Diagnostics;
using ComputeSharp;
using Tap4D.Geometry;
using Tap4D.Scene;

namespace Tap4D.Rendering.Raycast;

/// <summary>
/// GPU raycasting renderer: pass 1 (<see cref="RaycastVoxelShader"/>) casts the rays of
/// every voxel of the spherical virtual screen over the screen-tiling candidate lists
/// into the voxel buffer, pass 2 (<see cref="CompositeShader"/>) composites each real
/// pixel by alternating the hit slices in ana-kata order across frames.
/// Buffers are reused across frames.
/// </summary>
/// <remarks>
/// Creates a renderer for images of the given size in pixels.
/// </remarks>
/// <param name="width">The image width in pixels.</param>
/// <param name="height">The image height in pixels.</param>
/// <param name="slices">The number of ana-kata slices of the virtual screen.</param>
/// <param name="subRays">The number of coverage sub-rays per slice.</param>
/// <param name="maxFps">The frame rate limit; zero means no limit.</param>
public class GpuRaycastRenderer(int width, int height, int slices = 64, int subRays = 1, int maxFps = 0) : IRenderer, IDisposable
{
    private const float CullMargin = 1e-4f;

    private readonly GraphicsDevice _device = GraphicsDevice.GetDefault();
    private readonly double _minFrameMs = maxFps > 0 ? 1000.0 / maxFps : 0;
    private readonly Stopwatch _throttleWatch = Stopwatch.StartNew();
    private long _lastRenderMs = long.MinValue;
    private ReadOnlyBuffer<GpuTetrahedron>? _visibleBuffer;
    private ReadOnlyBuffer<GpuTetrahedron>? _occluderBuffer;
    private ReadOnlyBuffer<GpuSegment>? _segmentBuffer;
    private ReadOnlyBuffer<int>? _visibleOffsetsBuffer;
    private ReadOnlyBuffer<int>? _visibleIndicesBuffer;
    private ReadOnlyBuffer<int>? _occluderOffsetsBuffer;
    private ReadOnlyBuffer<int>? _occluderIndicesBuffer;
    private ReadOnlyBuffer<int>? _segmentOffsetsBuffer;
    private ReadOnlyBuffer<int>? _segmentIndicesBuffer;
    private ReadWriteBuffer<float>? _depths;
    private ReadWriteBuffer<float>? _coverage;
    private ReadWriteBuffer<int>? _output;
    private int[]? _readback;
    private byte[]? _frame;


    /// <summary>The image width in pixels.</summary>
    public int Width { get; } = width;

    /// <summary>The image height in pixels.</summary>
    public int Height { get; } = height;

    /// <summary>The number of ana-kata slices of the virtual screen.</summary>
    public int SliceCount { get; } = slices;

    /// <summary>The number of coverage sub-rays per slice.</summary>
    public int SubRays { get; } = subRays;

    /// <summary>The height of the virtual screen in world units.</summary>
    public float ViewHeight { get; set; } = 2f;

    /// <summary>The ana-kata anisotropy of segments: smaller values make them fatter in W.</summary>
    public float SegmentWScale { get; set; } = 1f;

    /// <summary>The frame counter driving the alternation of slices; incremented after every actual render.</summary>
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
    /// When frame rate limiting is enabled and the call comes too early, returns the cached frame.
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
        CameraSpaceGeometry.TransformOnly(scene, camera, out List<Tetrahedron> visible, out List<Tetrahedron> occluders, out List<Segment4> segments);
        AngularRanges[] visibleRanges = [.. visible.Select(t => AngularRanges.Compute(t, camera.Focal))];
        AngularRanges[] occluderRanges = [.. occluders.Select(t => AngularRanges.Compute(t, camera.Focal))];
        AngularRanges[] segmentRanges = [.. segments.Select(s => AngularRanges.ComputeWithRadius(s, camera.Focal))];
        float pixelSize = ViewHeight / Height;
        float sliceWidth = (MaxW - MinW) / SliceCount;
        ScreenTiling tiling = ScreenTiling.Build(Width, Height, pixelSize,
            0.5f * pixelSize + CullMargin, 1.5f * pixelSize + 0.5f * sliceWidth * SegmentWScale + CullMargin,
            visibleRanges, occluderRanges, segmentRanges);
        EnsureFrameBuffers();
        UploadGeometry(visible, occluders, segments, visibleRanges, occluderRanges, segmentRanges);
        UploadTiling(tiling);
        DispatchVoxelPass(camera);
        DispatchCompositePass(camera);
        _output!.CopyTo(_readback!);
        Buffer.BlockCopy(_readback!, 0, _frame!, 0, _frame!.Length);
        FrameIndex++;
        return _frame!;
    }

    /// <summary>
    /// Releases the GPU buffers.
    /// </summary>
    public void Dispose()
    {
        _visibleBuffer?.Dispose();
        _occluderBuffer?.Dispose();
        _segmentBuffer?.Dispose();
        _visibleOffsetsBuffer?.Dispose();
        _visibleIndicesBuffer?.Dispose();
        _occluderOffsetsBuffer?.Dispose();
        _occluderIndicesBuffer?.Dispose();
        _segmentOffsetsBuffer?.Dispose();
        _segmentIndicesBuffer?.Dispose();
        _depths?.Dispose();
        _coverage?.Dispose();
        _output?.Dispose();
    }

    /// <summary>
    /// Uploads the camera-space geometry (with angular ana-kata bounds for slice culling)
    /// into persistent GPU buffers, growing them when needed.
    /// </summary>
    private void UploadGeometry(List<Tetrahedron> visible, List<Tetrahedron> occluders, List<Segment4> segments,
        AngularRanges[] visibleRanges, AngularRanges[] occluderRanges, AngularRanges[] segmentRanges)
    {
        GpuBuffers.EnsureCapacity(_device, ref _visibleBuffer, visible.Count);
        GpuBuffers.EnsureCapacity(_device, ref _occluderBuffer, occluders.Count);
        GpuBuffers.EnsureCapacity(_device, ref _segmentBuffer, segments.Count);
        if (visible.Count > 0)
            _visibleBuffer!.CopyFrom(ToGpu(visible, visibleRanges));
        if (occluders.Count > 0)
            _occluderBuffer!.CopyFrom(ToGpu(occluders, occluderRanges));
        if (segments.Count > 0)
            _segmentBuffer!.CopyFrom(ToGpu(segments, segmentRanges));
    }

    /// <summary>
    /// Uploads the screen tiling candidate lists into persistent GPU buffers.
    /// </summary>
    private void UploadTiling(ScreenTiling tiling)
    {
        UploadIntBuffer(ref _visibleOffsetsBuffer, tiling.VisibleOffsets);
        UploadIntBuffer(ref _visibleIndicesBuffer, tiling.VisibleIndices);
        UploadIntBuffer(ref _occluderOffsetsBuffer, tiling.OccluderOffsets);
        UploadIntBuffer(ref _occluderIndicesBuffer, tiling.OccluderIndices);
        UploadIntBuffer(ref _segmentOffsetsBuffer, tiling.SegmentOffsets);
        UploadIntBuffer(ref _segmentIndicesBuffer, tiling.SegmentIndices);
    }

    /// <summary>
    /// Uploads one int array into a persistent GPU buffer, growing it when needed.
    /// Empty arrays are skipped: zero-length uploads are invalid, and the offsets
    /// guarantee the buffer is never read in that case.
    /// </summary>
    private void UploadIntBuffer(ref ReadOnlyBuffer<int>? buffer, int[] data)
    {
        GpuBuffers.EnsureCapacity(_device, ref buffer, data.Length);
        if (data.Length > 0)
            buffer!.CopyFrom(data);
    }

    /// <summary>
    /// Allocates the persistent voxel and output buffers on first use.
    /// </summary>
    private void EnsureFrameBuffers()
    {
        _depths ??= _device.AllocateReadWriteBuffer<float>(Width * Height * SliceCount);
        _coverage ??= _device.AllocateReadWriteBuffer<float>(Width * Height * SliceCount);
        _output ??= _device.AllocateReadWriteBuffer<int>(Width * Height);
        _readback ??= new int[Width * Height];
        _frame ??= new byte[Width * Height * 4];
    }

    /// <summary>
    /// Dispatches the voxel raycasting pass.
    /// </summary>
    private void DispatchVoxelPass(Camera4 camera)
    {
        float sliceWidth = (MaxW - MinW) / SliceCount;
        float footprintK = (ViewHeight / Height + 0.5f * sliceWidth * SegmentWScale) / camera.Focal;
        RaycastVoxelShader voxelShader = new(
            _visibleBuffer!, _occluderBuffer!, _segmentBuffer!, _depths!, _coverage!,
            _visibleOffsetsBuffer!, _visibleIndicesBuffer!,
            _occluderOffsetsBuffer!, _occluderIndicesBuffer!,
            _segmentOffsetsBuffer!, _segmentIndicesBuffer!,
            Width, Height, SliceCount, SubRays,
            (Width + ScreenTiling.TileSize - 1) / ScreenTiling.TileSize, ScreenTiling.TileSize,
            ViewHeight / Height, camera.Focal, MinW, sliceWidth, camera.Near, SegmentWScale, footprintK);
        _device.For(Width * Height, voxelShader);
    }

    /// <summary>
    /// Dispatches the compositing pass.
    /// </summary>
    private void DispatchCompositePass(Camera4 camera)
    {
        float sliceWidth = (MaxW - MinW) / SliceCount;
        CompositeShader compositeShader = new(
            _depths!, _coverage!, _output!,
            SliceCount, FrameIndex, MinW, sliceWidth, camera.Focal,
            Palette.MinW, 1f / (Palette.MaxW - Palette.MinW), Palette.Green);
        _device.For(Width * Height, compositeShader);
    }

    /// <summary>
    /// Converts tetrahedra to the GPU layout, attaching the angular ana-kata bounds.
    /// </summary>
    private static GpuTetrahedron[] ToGpu(List<Tetrahedron> source, AngularRanges[] ranges) =>
        [
            .. source.Select((t, i) => new GpuTetrahedron
            {
                A = t.A, B = t.B, C = t.C, D = t.D,
                MinW = ranges[i].W.Min, MaxW = ranges[i].W.Max,
            }),
        ];

    /// <summary>
    /// Converts segments to the GPU layout, attaching the angular ana-kata bounds.
    /// </summary>
    private static GpuSegment[] ToGpu(List<Segment4> source, AngularRanges[] ranges) =>
        [
            .. source.Select((s, i) => new GpuSegment
            {
                Start = s.Start, End = s.End, Radius = s.Radius,
                MinW = ranges[i].W.Min, MaxW = ranges[i].W.Max,
            }),
        ];
}
