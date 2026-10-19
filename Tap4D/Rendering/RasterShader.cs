using ComputeSharp;

namespace Tap4D.Rendering;

/// <summary>
/// The rasterization compute shader: one thread per pixel. Mirrors the CPU reference
/// renderer exactly — collects all surface points projecting onto the pixel, orders them
/// by depth (ties keep primitive order) and picks the one for the current frame.
/// </summary>
/// <remarks>
/// Creates the shader with all captured values.
/// </remarks>
[ThreadGroupSize(64, 1, 1)]
[GeneratedComputeShaderDescriptor]
public readonly partial struct RasterShader(
    ReadOnlyBuffer<GpuTetrahedron> visibleTetrahedra,
    ReadOnlyBuffer<GpuTetrahedron> occluderTetrahedra,
    ReadOnlyBuffer<GpuSegment> segments,
    ReadWriteBuffer<int> output,
    int visibleCount, int occluderCount, int segmentCount,
    int width, int height, float scale, float focal,
    int frameIndex, float minW, float invWRange, int green) : IComputeShader
{
    private const float Epsilon = 1e-6f;

    private const float TieEpsilon = 1e-4f;

    private const float DepthMergeEpsilon = 1e-3f;

    private const float WMergeEpsilon = 1e-3f;

    /// <summary>
    /// Barycentric hit margin: the tetrahedron is treated as slightly expanded, so a ray
    /// never slips through the seam between two adjacent tetrahedra.
    /// </summary>
    private const float HitMargin = 1e-6f;

    private readonly ReadOnlyBuffer<GpuTetrahedron> visibleTetrahedra = visibleTetrahedra;
    private readonly ReadOnlyBuffer<GpuTetrahedron> occluderTetrahedra = occluderTetrahedra;
    private readonly ReadOnlyBuffer<GpuSegment> segments = segments;
    private readonly ReadWriteBuffer<int> output = output;
    private readonly int visibleCount = visibleCount;
    private readonly int occluderCount = occluderCount;
    private readonly int segmentCount = segmentCount;
    private readonly int width = width;
    private readonly int height = height;
    private readonly float scale = scale;
    private readonly float focal = focal;
    private readonly int frameIndex = frameIndex;
    private readonly float minW = minW;
    private readonly float invWRange = invWRange;
    private readonly int green = green;

    /// <summary>
    /// Renders one pixel. Only candidates in front of the nearest occluder take part
    /// in the frame alternation; occluders themselves are never shown and hide
    /// everything behind them permanently.
    /// </summary>
    public void Execute()
    {
        int x = ThreadIds.X % width;
        int y = ThreadIds.X / width;
        float pixelX = (x + 0.5f - width / 2f) * scale;
        float pixelY = (height / 2f - y - 0.5f) * scale;
        int count = CountVisible(pixelX, pixelY);
        if (count == 0)
        {
            output[ThreadIds.X] = 0;
            return;
        }
        SelectCandidate(pixelX, pixelY, frameIndex % count, out _, out float screenW);
        output[ThreadIds.X] = PackColor(screenW);
    }

    /// <summary>
    /// Counts the visible candidates in front of the nearest occluder by walking
    /// the candidates in depth order until an occluder or the end of the list,
    /// merging duplicates (near-equal depth and ana-kata) into one.
    /// </summary>
    /// <param name="pixelX">The X coordinate of the pixel on the virtual screen.</param>
    /// <param name="pixelY">The Y coordinate of the pixel on the virtual screen.</param>
    /// <returns>The number of deduplicated visible candidates before the first occluder.</returns>
    private int CountVisible(float pixelX, float pixelY)
    {
        float prevDepth = float.MinValue;
        int prevIndex = -1;
        float keptDepth = 0;
        float keptW = 0;
        int count = 0;
        float d, w;
        bool occluder;
        while (SelectNext(pixelX, pixelY, ref prevDepth, ref prevIndex, out d, out w, out occluder) && !occluder)
            if (count == 0 || !IsDuplicate(d, w, keptDepth, keptW))
            {
                count++;
                keptDepth = d;
                keptW = w;
            }
        return count;
    }

    /// <summary>
    /// Finds the k-th deduplicated candidate in depth order, walking the raw candidates
    /// and merging duplicates (near-equal depth and ana-kata) into one.
    /// </summary>
    /// <param name="pixelX">The X coordinate of the pixel on the virtual screen.</param>
    /// <param name="pixelY">The Y coordinate of the pixel on the virtual screen.</param>
    /// <param name="k">The index of the deduplicated candidate in depth order.</param>
    /// <param name="depth">The camera-space Z of the selected candidate.</param>
    /// <param name="screenW">The ana-kata coordinate of the selected candidate.</param>
    private void SelectCandidate(float pixelX, float pixelY, int k, out float depth, out float screenW)
    {
        float prevDepth = float.MinValue;
        int prevIndex = -1;
        float keptDepth = 0;
        float keptW = 0;
        int kept = -1;
        depth = 0;
        screenW = 0;
        float d, w;
        bool occluder;
        while (SelectNext(pixelX, pixelY, ref prevDepth, ref prevIndex, out d, out w, out occluder) && !occluder)
            if (kept < 0 || !IsDuplicate(d, w, keptDepth, keptW))
            {
                kept++;
                keptDepth = d;
                keptW = w;
                if (kept == k)
                {
                    depth = d;
                    screenW = w;
                    return;
                }
            }
    }

    /// <summary>
    /// Whether a candidate is a duplicate of the last kept one: near-equal depth and ana-kata.
    /// </summary>
    private static bool IsDuplicate(float depth, float screenW, float keptDepth, float keptW) =>
        Hlsl.Abs(depth - keptDepth) <= DepthMergeEpsilon && Hlsl.Abs(screenW - keptW) <= WMergeEpsilon;

    /// <summary>
    /// Finds the first candidate in depth order strictly after (prevDepth, prevIndex).
    /// </summary>
    /// <param name="pixelX">The X coordinate of the pixel on the virtual screen.</param>
    /// <param name="pixelY">The Y coordinate of the pixel on the virtual screen.</param>
    /// <param name="prevDepth">The depth of the previously selected candidate.</param>
    /// <param name="prevIndex">The primitive index of the previously selected candidate.</param>
    /// <param name="depth">The depth of the next candidate.</param>
    /// <param name="screenW">The ana-kata coordinate of the next candidate.</param>
    /// <param name="isOccluder">Whether the next candidate is an occluder.</param>
    /// <returns>True if a next candidate exists.</returns>
    private bool SelectNext(
        float pixelX, float pixelY, ref float prevDepth, ref int prevIndex,
        out float depth, out float screenW, out bool isOccluder)
    {
        float bestDepth = float.MaxValue;
        int bestIndex = int.MaxValue;
        float bestW = 0;
        bool bestOccluder = false;
        float d, w;
        for (int i = 0; i < visibleCount; i++)
            if (TryHitTetrahedron(visibleTetrahedra[i], pixelX, pixelY, out d, out w)
                && IsBetter(d, i, prevDepth, prevIndex, bestDepth, bestIndex))
            {
                bestDepth = d;
                bestIndex = i;
                bestW = w;
                bestOccluder = false;
            }
        for (int i = 0; i < occluderCount; i++)
            if (TryHitTetrahedron(occluderTetrahedra[i], pixelX, pixelY, out d, out w)
                && IsBetter(d, visibleCount + i, prevDepth, prevIndex, bestDepth, bestIndex))
            {
                bestDepth = d;
                bestIndex = visibleCount + i;
                bestW = w;
                bestOccluder = true;
            }
        for (int i = 0; i < segmentCount; i++)
            if (TryHitSegment(segments[i], pixelX, pixelY, out d, out w)
                && IsBetter(d, visibleCount + occluderCount + i, prevDepth, prevIndex, bestDepth, bestIndex))
            {
                bestDepth = d;
                bestIndex = visibleCount + occluderCount + i;
                bestW = w;
                bestOccluder = false;
            }
        depth = bestDepth;
        screenW = bestW;
        isOccluder = bestOccluder;
        prevDepth = bestDepth;
        prevIndex = bestIndex;
        return bestIndex != int.MaxValue;
    }

    /// <summary>
    /// Compares a candidate lexicographically by (depth, primitive index):
    /// it must come after the previously selected candidate and before the current best.
    /// </summary>
    private static bool IsBetter(float d, int index, float prevDepth, int prevIndex, float bestDepth, int bestIndex) =>
        (d > prevDepth || (d == prevDepth && index > prevIndex))
        && (d < bestDepth || (d == bestDepth && index < bestIndex));

    /// <summary>
    /// Maps an ana-kata screen coordinate to a packed BGRA color.
    /// </summary>
    /// <param name="screenW">The ana-kata coordinate on the virtual screen.</param>
    /// <returns>The packed BGRA color.</returns>
    private int PackColor(float screenW)
    {
        float t = Hlsl.Clamp((screenW - minW) * invWRange, 0f, 1f);
        int r = (int)(255f * (1f - t));
        int b = (int)(255f * t);
        return (255 << 24) | (r << 16) | (green << 8) | b;
    }

    /// <summary>
    /// Finds the nearest point of a tetrahedron projecting to the pixel, solving the
    /// barycentric equations of the 2-plane intersection exactly like the CPU renderer.
    /// </summary>
    /// <param name="tetra">The tetrahedron in camera space.</param>
    /// <param name="pixelX">The X coordinate of the pixel on the virtual screen.</param>
    /// <param name="pixelY">The Y coordinate of the pixel on the virtual screen.</param>
    /// <param name="depth">The camera-space Z of the nearest point.</param>
    /// <param name="screenW">The ana-kata coordinate of the nearest point.</param>
    /// <returns>True if the tetrahedron projects onto the pixel.</returns>
    private bool TryHitTetrahedron(GpuTetrahedron tetra, float pixelX, float pixelY, out float depth, out float screenW)
    {
        depth = 0;
        screenW = 0;
        float rx = pixelX / focal;
        float ry = pixelY / focal;
        float x0 = tetra.A.X - rx * tetra.A.Z, y0 = tetra.A.Y - ry * tetra.A.Z;
        float x1 = tetra.B.X - rx * tetra.B.Z, y1 = tetra.B.Y - ry * tetra.B.Z;
        float x2 = tetra.C.X - rx * tetra.C.Z, y2 = tetra.C.Y - ry * tetra.C.Z;
        float x3 = tetra.D.X - rx * tetra.D.Z, y3 = tetra.D.Y - ry * tetra.D.Z;
        float d0 = x1 * (y2 - y3) - x2 * (y1 - y3) + x3 * (y1 - y2);
        float d1 = -(x0 * (y2 - y3) - x2 * (y0 - y3) + x3 * (y0 - y2));
        float d2 = x0 * (y1 - y3) - x1 * (y0 - y3) + x3 * (y0 - y1);
        float d3 = -(x0 * (y1 - y2) - x1 * (y0 - y2) + x2 * (y0 - y1));
        float maxAbs = Hlsl.Max(Hlsl.Max(Hlsl.Abs(d0), Hlsl.Abs(d1)), Hlsl.Max(Hlsl.Abs(d2), Hlsl.Abs(d3)));
        if (maxAbs < Epsilon)
            return false;
        float l0 = 0, l1 = 0, l2 = 0, l3 = 0;
        SolveParticular(x0, y0, x1, y1, x2, y2, x3, y3, d0, d1, d2, d3, maxAbs, ref l0, ref l1, ref l2, ref l3);
        if (!FeasibleInterval(l0, l1, l2, l3, d0, d1, d2, d3, out float tMin, out float tMax))
            return false;
        Float4 atMin = (l0 + tMin * d0) * tetra.A + (l1 + tMin * d1) * tetra.B + (l2 + tMin * d2) * tetra.C + (l3 + tMin * d3) * tetra.D;
        Float4 atMax = (l0 + tMax * d0) * tetra.A + (l1 + tMax * d1) * tetra.B + (l2 + tMax * d2) * tetra.C + (l3 + tMax * d3) * tetra.D;
        Float4 nearest = PickNearer(atMin, atMax);
        depth = nearest.Z;
        screenW = focal * nearest.W / nearest.Z;
        return true;
    }

    /// <summary>
    /// Picks the nearer of the two intersection segment endpoints. When the depths are
    /// equal within a tie epsilon (face-on geometry), the endpoint with the smaller W wins,
    /// so that the choice is stable across CPU and GPU rounding differences.
    /// </summary>
    private static Float4 PickNearer(Float4 a, Float4 b)
    {
        float dz = a.Z - b.Z;
        if (dz < -TieEpsilon)
            return a;
        if (dz > TieEpsilon)
            return b;
        return a.W <= b.W ? a : b;
    }

    /// <summary>
    /// Finds one barycentric solution by setting the coordinate with the largest
    /// direction component to zero and solving the remaining 3x3 system via Cramer's rule.
    /// </summary>
    private static void SolveParticular(
        float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3,
        float d0, float d1, float d2, float d3, float maxAbs,
        ref float l0, ref float l1, ref float l2, ref float l3)
    {
        if (Hlsl.Abs(d0) == maxAbs)
            Solve3x3(x1, y1, x2, y2, x3, y3, ref l1, ref l2, ref l3);
        else if (Hlsl.Abs(d1) == maxAbs)
            Solve3x3(x0, y0, x2, y2, x3, y3, ref l0, ref l2, ref l3);
        else if (Hlsl.Abs(d2) == maxAbs)
            Solve3x3(x0, y0, x1, y1, x3, y3, ref l0, ref l1, ref l3);
        else
            Solve3x3(x0, y0, x1, y1, x2, y2, ref l0, ref l1, ref l2);
    }

    /// <summary>
    /// Solves the system with rows (x, y, 1) and right-hand side (0, 0, 1) via Cramer's rule.
    /// </summary>
    private static void Solve3x3(
        float xa, float ya, float xb, float yb, float xc, float yc,
        ref float la, ref float lb, ref float lc)
    {
        float det = xa * (yb - yc) - xb * (ya - yc) + xc * (ya - yb);
        la = (xb * yc - xc * yb) / det;
        lb = -(xa * yc - xc * ya) / det;
        lc = (xa * yb - xb * ya) / det;
    }

    /// <summary>
    /// Finds the parameter interval where all barycentric coordinates are non-negative.
    /// </summary>
    private static bool FeasibleInterval(
        float l0, float l1, float l2, float l3, float d0, float d1, float d2, float d3,
        out float tMin, out float tMax)
    {
        tMin = float.MinValue;
        tMax = float.MaxValue;
        if (!ClampInterval(l0, d0, ref tMin, ref tMax))
            return false;
        if (!ClampInterval(l1, d1, ref tMin, ref tMax))
            return false;
        if (!ClampInterval(l2, d2, ref tMin, ref tMax))
            return false;
        if (!ClampInterval(l3, d3, ref tMin, ref tMax))
            return false;
        return tMin <= tMax;
    }

    /// <summary>
    /// Narrows the feasible interval by one constraint, with the tetrahedron slightly
    /// expanded by HitMargin to close seams.
    /// </summary>
    private static bool ClampInterval(float lambda, float direction, ref float tMin, ref float tMax)
    {
        if (direction > Epsilon)
            tMin = Hlsl.Max(tMin, -(lambda + HitMargin) / direction);
        else if (direction < -Epsilon)
            tMax = Hlsl.Min(tMax, -(lambda + HitMargin) / direction);
        else if (lambda < -HitMargin)
            return false;
        return true;
    }

    /// <summary>
    /// Finds whether a segment covers the pixel as a cylinder of its world-space radius:
    /// computes the closest point between the segment and the pixel's ray plane
    /// exactly like the CPU renderer.
    /// </summary>
    /// <param name="s">The segment in camera space.</param>
    /// <param name="pixelX">The X coordinate of the pixel on the virtual screen.</param>
    /// <param name="pixelY">The Y coordinate of the pixel on the virtual screen.</param>
    /// <param name="depth">The camera-space Z of the closest point.</param>
    /// <param name="screenW">The ana-kata coordinate of the closest point.</param>
    /// <returns>True if the closest point is within the radius.</returns>
    private bool TryHitSegment(GpuSegment s, float pixelX, float pixelY, out float depth, out float screenW)
    {
        depth = 0;
        screenW = 0;
        float rx = pixelX / focal;
        float ry = pixelY / focal;
        float g11 = 1 + rx * rx;
        float g22 = 1 + ry * ry;
        float g12 = rx * ry;
        float det = g11 * g22 - g12 * g12;
        float e1a = s.Start.X - rx * s.Start.Z;
        float e2a = s.Start.Y - ry * s.Start.Z;
        float e1d = s.End.X - rx * s.End.Z - e1a;
        float e2d = s.End.Y - ry * s.End.Z - e2a;
        float a = g22 * e1d * e1d - 2 * g12 * e1d * e2d + g11 * e2d * e2d;
        float halfB = g22 * e1a * e1d - g12 * (e1a * e2d + e2a * e1d) + g11 * e2a * e2d;
        float t = a < 1e-12f ? 0 : Hlsl.Clamp(-halfB / a, 0f, 1f);
        float e1 = e1a + t * e1d;
        float e2 = e2a + t * e2d;
        float distSq = (g22 * e1 * e1 - 2 * g12 * e1 * e2 + g11 * e2 * e2) / det;
        if (distSq > s.Radius * s.Radius)
            return false;
        depth = s.Start.Z + t * (s.End.Z - s.Start.Z);
        screenW = focal * (s.Start.W + t * (s.End.W - s.Start.W)) / depth;
        return true;
    }
}
