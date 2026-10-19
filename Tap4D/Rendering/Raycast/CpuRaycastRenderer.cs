using System.Diagnostics;
using System.Numerics;
using Tap4D.Geometry;
using Tap4D.Scene;

namespace Tap4D.Rendering.Raycast;

/// <summary>
/// CPU reference for the raycasting renderer: casts one ray per voxel of the virtual
/// screen (spherical, radial perspective), then composites each real pixel by
/// alternating the hit slices in ana-kata order across frames.
/// </summary>
/// <remarks>
/// Creates a renderer for images of the given size in pixels.
/// </remarks>
/// <param name="width">The image width in pixels.</param>
/// <param name="height">The image height in pixels.</param>
/// <param name="slices">The number of ana-kata slices of the virtual screen.</param>
/// <param name="subRays">The number of coverage sub-rays per slice.</param>
/// <param name="maxFps">The frame rate limit; zero means no limit.</param>
public class CpuRaycastRenderer(int width, int height, int slices = 64, int subRays = 1, int maxFps = 0) : IRenderer
{
    private const float Empty = 0f;

    private const float CullMargin = 1e-4f;

    private readonly double _minFrameMs = maxFps > 0 ? 1000.0 / maxFps : 0;
    private readonly Stopwatch _throttleWatch = Stopwatch.StartNew();
    private long _lastRenderMs = long.MinValue;
    private byte[]? _cachedFrame;


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
    /// When frame rate limiting is enabled and the call comes too early, returns the cached
    /// frame instead.
    /// </summary>
    /// <param name="scene">The scene to render.</param>
    /// <param name="camera">The camera to render from.</param>
    /// <returns>The BGRA pixel buffer.</returns>
    public byte[] Render(Scene4 scene, Camera4 camera)
    {
        long now = _throttleWatch.ElapsedMilliseconds;
        if (_minFrameMs > 0 && now - _lastRenderMs < _minFrameMs && _cachedFrame != null)
            return _cachedFrame;
        _lastRenderMs = now;
        CameraSpaceGeometry.TransformOnly(scene, camera, out List<Tetrahedron> visible, out List<Tetrahedron> occluders, out List<Segment4> segments);
        AngularRanges[] visibleRanges = [.. visible.Select(t => AngularRanges.Compute(t, camera.Focal))];
        AngularRanges[] occluderRanges = [.. occluders.Select(t => AngularRanges.Compute(t, camera.Focal))];
        AngularRanges[] segmentRanges = [.. segments.Select(s => AngularRanges.ComputeWithRadius(s, camera.Focal))];
        VoxelScreen screen = new(Width, Height, ViewHeight, camera.Focal, SliceCount, MinW, MaxW);
        float footprintK = (ViewHeight / Height + 0.5f * screen.SliceWidth * SegmentWScale) / camera.Focal;
        float[] depths = new float[Width * Height * SliceCount];
        float[] coverage = new float[depths.Length];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                for (int s = 0; s < SliceCount; s++)
                    CastVoxel(visible, occluders, segments, visibleRanges, occluderRanges, segmentRanges, screen, x, y, s, camera.Near, footprintK, depths, coverage);
        byte[] buffer = Composite(screen, depths, coverage);
        _cachedFrame = buffer;
        FrameIndex++;
        return buffer;
    }

    /// <summary>
    /// Casts the sub-rays of one voxel and records its state: a hit with the nearest
    /// depth and the coverage fraction, or empty (fully blocked rays count as empty).
    /// Primitives whose angular ana-kata range does not overlap the slice are skipped.
    /// </summary>
    private void CastVoxel(
        List<Tetrahedron> visible, List<Tetrahedron> occluders, List<Segment4> segments,
        AngularRanges[] visibleRanges, AngularRanges[] occluderRanges, AngularRanges[] segmentRanges,
        VoxelScreen screen, int x, int y, int slice, float near, float footprintK, float[] depths, float[] coverage)
    {
        int voxel = (y * Width + x) * SliceCount + slice;
        float wLo = screen.MinW + slice * screen.SliceWidth - CullMargin;
        float wHi = wLo + screen.SliceWidth + 2 * CullMargin;
        float pixelX = screen.PixelX(x);
        float pixelY = screen.PixelY(y);
        float half = screen.ViewHeight / Height * 0.5f + CullMargin;
        int hits = 0;
        float best = float.MaxValue;
        for (int sub = 0; sub < SubRays; sub++)
        {
            if (!screen.TryGetDirection(pixelX, pixelY, screen.SliceW(slice, sub, SubRays), out Vector4 u))
                continue;
            if (Cast(visible, occluders, segments, visibleRanges, occluderRanges, segmentRanges, pixelX, pixelY, half, u, wLo, wHi, near, SegmentWScale, footprintK, out float depth, out bool occluder) && !occluder)
            {
                hits++;
                best = MathF.Min(best, depth);
            }
        }
        if (hits > 0)
        {
            depths[voxel] = best;
            coverage[voxel] = (float)hits / SubRays;
        }
    }

    /// <summary>
    /// Finds the nearest hit along a ray over the primitives overlapping the slice's
    /// ana-kata interval and the pixel's angular footprint; occluders participate but only block.
    /// </summary>
    private static bool Cast(
        List<Tetrahedron> visible, List<Tetrahedron> occluders, List<Segment4> segments,
        AngularRanges[] visibleRanges, AngularRanges[] occluderRanges, AngularRanges[] segmentRanges,
        float pixelX, float pixelY, float half, Vector4 u, float wLo, float wHi, float near, float wScale, float footprintK, out float depth, out bool isOccluder)
    {
        depth = float.MaxValue;
        isOccluder = false;
        for (int i = 0; i < visible.Count; i++)
            if (visibleRanges[i].Overlaps(pixelX, pixelY, half, wLo, wHi)
                && RayTetrahedronIntersection.TryIntersect(visible[i], u, near, out float d) && d < depth)
                (depth, isOccluder) = (d, false);
        for (int i = 0; i < occluders.Count; i++)
            if (occluderRanges[i].Overlaps(pixelX, pixelY, half, wLo, wHi)
                && RayTetrahedronIntersection.TryIntersect(occluders[i], u, near, out float d) && d < depth)
                (depth, isOccluder) = (d, true);
        for (int i = 0; i < segments.Count; i++)
            if (segmentRanges[i].Overlaps(pixelX, pixelY, half, wLo, wHi)
                && RaySegmentIntersection.TryIntersect(segments[i].Start, segments[i].End, segments[i].Radius, u, near, wScale, footprintK, out float d) && d < depth)
                (depth, isOccluder) = (d, false);
        return depth < float.MaxValue;
    }

    /// <summary>
    /// Composites the voxel buffer into the final BGRA image: per pixel, the hit slices
    /// are alternated in ana-kata order across frames.
    /// </summary>
    private byte[] Composite(VoxelScreen screen, float[] depths, float[] coverage)
    {
        byte[] buffer = new byte[Width * Height * 4];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                CompositePixel(screen, depths, coverage, x, y, buffer);
        return buffer;
    }

    /// <summary>
    /// Composites one pixel: picks the hit slice for the current frame in ana-kata order
    /// and colors it by the ana-kata coordinate of the hit point (depth times the ray's
    /// ana-kata direction component), not by the slice's screen coordinate.
    /// </summary>
    private void CompositePixel(VoxelScreen screen, float[] depths, float[] coverage, int x, int y, byte[] buffer)
    {
        int baseVoxel = (y * Width + x) * SliceCount;
        int target = -1;
        int count = 0;
        for (int s = 0; s < SliceCount; s++)
            if (depths[baseVoxel + s] > Empty)
                count++;
        if (count == 0)
            return;
        int wanted = FrameIndex % count;
        int index = 0;
        for (int s = 0; s < SliceCount; s++)
            if (depths[baseVoxel + s] > Empty && index++ == wanted)
                target = s;
        if (target < 0)
            return;
        float hitW = depths[baseVoxel + target] * screen.SliceW(target, 0, 1) / screen.Focal;
        (byte r, byte g, byte b) = Palette.ColorFor(hitW);
        float bright = coverage[baseVoxel + target];
        int offset = (y * Width + x) * 4;
        buffer[offset] = (byte)(b * bright);
        buffer[offset + 1] = (byte)(g * bright);
        buffer[offset + 2] = (byte)(r * bright);
        buffer[offset + 3] = 255;
    }
}
