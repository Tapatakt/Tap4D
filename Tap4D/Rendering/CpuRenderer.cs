using System.Diagnostics;
using Tap4D.Geometry;
using Tap4D.Scene;

namespace Tap4D.Rendering;

/// <summary>
/// Reference CPU renderer: slow, but simple. Serves as the ground truth for the GPU renderer.
/// For each pixel it collects all surface points projecting onto it, sorts them by depth
/// and alternates between them across frames via <see cref="FrameIndex"/>.
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
public class CpuRenderer(int width, int height, int maxFps = 0) : IRenderer
{
    private readonly double _minFrameMs = maxFps > 0 ? 1000.0 / maxFps : 0;
    private readonly Stopwatch _throttleWatch = Stopwatch.StartNew();
    private long _lastRenderMs = long.MinValue;
    private byte[]? _cachedFrame;


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

    private const float DepthMergeEpsilon = 1e-3f;

    private const float WMergeEpsilon = 1e-3f;

    /// <summary>
    /// Renders the scene into a BGRA pixel buffer (4 bytes per pixel, row-major, top to bottom).
    /// When frame rate limiting is enabled and the call comes too early, returns the cached
    /// frame instead. <see cref="FrameIndex"/> is incremented after every actual render.
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
        CameraSpaceGeometry.Prepare(scene, camera, MinW, MaxW, out List<Tetrahedron> visible, out List<Tetrahedron> occluders, out List<Segment4> segments);
        byte[] buffer = new byte[Width * Height * 4];
        float scale = ViewHeight / Height;
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            RenderPixel(visible, occluders, segments, x, y, scale, camera.Focal, buffer);
        _cachedFrame = buffer;
        FrameIndex++;
        return buffer;
    }

    /// <summary>
    /// Renders one pixel: collects candidates, sorts them by depth (stably, so ties keep
    /// the primitive order, matching the GPU renderer), truncates at the first occluder
    /// (occluders are never shown and hide everything behind them permanently)
    /// and picks the candidate for the current frame.
    /// </summary>
    /// <param name="visible">The clipped camera-space visible tetrahedra.</param>
    /// <param name="occluders">The clipped camera-space occluder tetrahedra.</param>
    /// <param name="segments">The camera-space segments.</param>
    /// <param name="x">The pixel column.</param>
    /// <param name="y">The pixel row.</param>
    /// <param name="scale">The virtual screen units per pixel.</param>
    /// <param name="focal">The focal distance of the camera.</param>
    /// <param name="buffer">The BGRA pixel buffer.</param>
    private void RenderPixel(
        List<Tetrahedron> visible, List<Tetrahedron> occluders, List<Segment4> segments,
        int x, int y, float scale, float focal, byte[] buffer)
    {
        float pixelX = (x + 0.5f - Width / 2f) * scale;
        float pixelY = (Height / 2f - y - 0.5f) * scale;
        List<RenderCandidate> candidates =
        [
            .. CollectCandidates(visible, occluders, segments, pixelX, pixelY, focal)
                .OrderBy(c => c.Depth)
                .TakeWhile(c => !c.IsOccluder),
        ];
        candidates = MergeDuplicates(candidates);
        if (candidates.Count == 0)
            return;
        RenderCandidate chosen = candidates[FrameIndex % candidates.Count];
        (byte r, byte g, byte b) = Palette.ColorFor(chosen.ScreenW);
        int offset = (y * Width + x) * 4;
        buffer[offset] = b;
        buffer[offset + 1] = g;
        buffer[offset + 2] = r;
        buffer[offset + 3] = 255;
    }

    /// <summary>
    /// Merges adjacent candidates with near-equal depth and ana-kata: they are the same
    /// virtual pixel reported by adjacent tetrahedra of one surface. Input must be sorted
    /// by depth; merging compares against the last kept candidate.
    /// </summary>
    /// <param name="sorted">The candidates sorted by depth.</param>
    /// <returns>The deduplicated candidates.</returns>
    private static List<RenderCandidate> MergeDuplicates(List<RenderCandidate> sorted)
    {
        List<RenderCandidate> result = [];
        foreach (RenderCandidate c in sorted)
            if (result.Count == 0
                || MathF.Abs(c.Depth - result[^1].Depth) > DepthMergeEpsilon
                || MathF.Abs(c.ScreenW - result[^1].ScreenW) > WMergeEpsilon)
                result.Add(c);
        return result;
    }

    /// <summary>
    /// Collects all surface points projecting onto a pixel.
    /// </summary>
    /// <param name="visible">The clipped camera-space visible tetrahedra.</param>
    /// <param name="occluders">The clipped camera-space occluder tetrahedra.</param>
    /// <param name="segments">The camera-space segments.</param>
    /// <param name="pixelX">The X coordinate of the pixel on the virtual screen.</param>
    /// <param name="pixelY">The Y coordinate of the pixel on the virtual screen.</param>
    /// <param name="focal">The focal distance of the camera.</param>
    /// <returns>The unsorted candidates for the pixel.</returns>
    private static List<RenderCandidate> CollectCandidates(
        List<Tetrahedron> visible, List<Tetrahedron> occluders, List<Segment4> segments,
        float pixelX, float pixelY, float focal)
    {
        List<RenderCandidate> candidates = [];
        foreach (Tetrahedron t in visible)
            if (TetraPixelIntersection.TryIntersect(t, pixelX, pixelY, focal, out float depth, out float screenW))
                candidates.Add(new(depth, screenW, false));
        foreach (Tetrahedron t in occluders)
            if (TetraPixelIntersection.TryIntersect(t, pixelX, pixelY, focal, out float depth, out float screenW))
                candidates.Add(new(depth, screenW, true));
        foreach (Segment4 s in segments)
            if (SegmentPixelIntersection.TryIntersect(s.Start, s.End, s.Radius, pixelX, pixelY, focal, out float depth, out float screenW))
                candidates.Add(new(depth, screenW, false));
        return candidates;
    }
}
