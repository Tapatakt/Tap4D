using ComputeSharp;

namespace Tap4D.Rendering.Raycast;

/// <summary>
/// Pass 1 of the raycasting renderer: one thread per voxel of the virtual screen.
/// Casts the voxel's sub-rays and records the voxel state into the depth buffer
/// (0 = empty, &gt; 0 = nearest hit depth) and the coverage buffer.
/// </summary>
/// <remarks>
/// Creates the shader with all captured values.
/// </remarks>
[ThreadGroupSize(64, 1, 1)]
[GeneratedComputeShaderDescriptor]
public readonly partial struct RaycastVoxelShader(
    ReadOnlyBuffer<GpuTetrahedron> visibleTetrahedra,
    ReadOnlyBuffer<GpuTetrahedron> occluderTetrahedra,
    ReadOnlyBuffer<GpuSegment> segments,
    ReadWriteBuffer<float> depths,
    ReadWriteBuffer<float> coverage,
    int visibleCount, int occluderCount, int segmentCount,
    int width, int height, int sliceCount, int subRays,
    float scale, float focal, float minW, float sliceWidth, float near, float segmentWScale, float footprintK) : IComputeShader
{
    private const float Epsilon = 1e-12f;

    private const float HitMargin = 1e-6f;

    private const float CullMargin = 1e-4f;

    private readonly ReadOnlyBuffer<GpuTetrahedron> visibleTetrahedra = visibleTetrahedra;
    private readonly ReadOnlyBuffer<GpuTetrahedron> occluderTetrahedra = occluderTetrahedra;
    private readonly ReadOnlyBuffer<GpuSegment> segments = segments;
    private readonly ReadWriteBuffer<float> depths = depths;
    private readonly ReadWriteBuffer<float> coverage = coverage;
    private readonly int visibleCount = visibleCount;
    private readonly int occluderCount = occluderCount;
    private readonly int segmentCount = segmentCount;
    private readonly int width = width;
    private readonly int height = height;
    private readonly int sliceCount = sliceCount;
    private readonly int subRays = subRays;
    private readonly float scale = scale;
    private readonly float focal = focal;
    private readonly float minW = minW;
    private readonly float sliceWidth = sliceWidth;
    private readonly float near = near;

    /// <summary>
    /// Casts the sub-rays of all slices of one pixel and records the voxel states.
    /// Dispatched 1D over pixels: the voxel count exceeds the 65535-groups limit,
    /// and each thread loops over the ana-kata slices instead.
    /// The X/Y culling is done once per pixel, packed into bitmasks; the per-slice
    /// loop then only checks a mask bit and the ana-kata range.
    /// </summary>
    public void Execute()
    {
        int pixel = ThreadIds.X;
        float pixelX = ((pixel % width) + 0.5f - width / 2f) * scale;
        float pixelY = (height / 2f - (pixel / width) - 0.5f) * scale;
        float half = scale * 0.5f + CullMargin;
        uint visLo = 0, visHi = 0, occLo = 0, occHi = 0, seg = 0;
        for (int i = 0; i < visibleCount; i++)
            if (OverlapsXY(visibleTetrahedra[i], pixelX, pixelY, half))
                SetBit(ref visLo, ref visHi, i);
        for (int i = 0; i < occluderCount; i++)
            if (OverlapsXY(occluderTetrahedra[i], pixelX, pixelY, half))
                SetBit(ref occLo, ref occHi, i);
        for (int i = 0; i < segmentCount; i++)
            if (OverlapsXY(segments[i], pixelX, pixelY, half))
                seg |= 1u << i;
        for (int slice = 0; slice < sliceCount; slice++)
            CastSlice(pixel, pixelX, pixelY, slice, visLo, visHi, occLo, occHi, seg);
    }

    /// <summary>
    /// Sets the bit for a primitive index in a 64-bit mask passed as two 32-bit halves.
    /// </summary>
    private static void SetBit(ref uint lo, ref uint hi, int i)
    {
        if (i < 32)
            lo |= 1u << i;
        else
            hi |= 1u << (i - 32);
    }

    /// <summary>
    /// Checks a primitive index in a 64-bit mask passed as two 32-bit halves.
    /// </summary>
    private static bool HasBit(uint lo, uint hi, int i) =>
        (i < 32 ? lo & (1u << i) : hi & (1u << (i - 32))) != 0;

    /// <summary>
    /// Checks whether a primitive's X/Y angular bounds overlap the pixel's footprint.
    /// </summary>
    private static bool OverlapsXY(GpuTetrahedron t, float pixelX, float pixelY, float half) =>
        t.MaxX >= pixelX - half && t.MinX <= pixelX + half
        && t.MaxY >= pixelY - half && t.MinY <= pixelY + half;

    /// <summary>
    /// Checks whether a segment's X/Y angular bounds overlap the pixel's footprint.
    /// </summary>
    private static bool OverlapsXY(GpuSegment s, float pixelX, float pixelY, float half) =>
        s.MaxX >= pixelX - half && s.MinX <= pixelX + half
        && s.MaxY >= pixelY - half && s.MinY <= pixelY + half;

    /// <summary>
    /// Casts the sub-rays of one voxel and records its state. Primitives whose angular
    /// ana-kata range does not overlap the slice are skipped.
    /// </summary>
    private void CastSlice(int pixel, float pixelX, float pixelY, int slice, uint visLo, uint visHi, uint occLo, uint occHi, uint seg)
    {
        int voxel = pixel * sliceCount + slice;
        float wLo = minW + slice * sliceWidth - CullMargin;
        float wHi = wLo + sliceWidth + 2 * CullMargin;
        int hits = 0;
        float best = float.MaxValue;
        for (int sub = 0; sub < subRays; sub++)
        {
            float w = minW + (slice + (sub + 0.5f) / subRays) * sliceWidth;
            float rest = focal * focal - pixelX * pixelX - pixelY * pixelY - w * w;
            if (rest <= 0)
                continue;
            Float4 u = new Float4(pixelX, pixelY, Hlsl.Sqrt(rest), w) / focal;
            if (Cast(u, wLo, wHi, visLo, visHi, occLo, occHi, seg, out float depth, out bool occluder) && !occluder)
            {
                hits++;
                best = Hlsl.Min(best, depth);
            }
        }
        depths[voxel] = hits > 0 ? best : 0f;
        coverage[voxel] = (float)hits / subRays;
    }

    /// <summary>
    /// Finds the nearest hit along a ray over the primitives selected by the X/Y bitmask
    /// and the slice's ana-kata interval; occluders participate but only block.
    /// </summary>
    private bool Cast(Float4 u, float wLo, float wHi, uint visLo, uint visHi, uint occLo, uint occHi, uint seg, out float depth, out bool isOccluder)
    {
        depth = float.MaxValue;
        isOccluder = false;
        float d;
        for (int i = 0; i < visibleCount; i++)
            if (HasBit(visLo, visHi, i) && visibleTetrahedra[i].MaxW >= wLo && visibleTetrahedra[i].MinW <= wHi
                && TryHitTetrahedron(visibleTetrahedra[i], u, out d) && d < depth)
            {
                depth = d;
                isOccluder = false;
            }
        for (int i = 0; i < occluderCount; i++)
            if (HasBit(occLo, occHi, i) && occluderTetrahedra[i].MaxW >= wLo && occluderTetrahedra[i].MinW <= wHi
                && TryHitTetrahedron(occluderTetrahedra[i], u, out d) && d < depth)
            {
                depth = d;
                isOccluder = true;
            }
        for (int i = 0; i < segmentCount; i++)
            if ((seg & (1u << i)) != 0 && segments[i].MaxW >= wLo && segments[i].MinW <= wHi
                && TryHitSegment(segments[i], u, out d) && d < depth)
            {
                depth = d;
                isOccluder = false;
            }
        return depth < float.MaxValue;
    }

    /// <summary>
    /// Finds where a ray hits a tetrahedron: solves the barycentric system via Cramer's rule,
    /// exactly like the CPU renderer.
    /// </summary>
    private bool TryHitTetrahedron(GpuTetrahedron tetra, Float4 u, out float depth)
    {
        depth = 0;
        float det = Det4(tetra.A, tetra.B, tetra.C, tetra.D);
        if (Hlsl.Abs(det) < Epsilon)
            return false;
        float l0 = Det4(u, tetra.B, tetra.C, tetra.D) / det;
        float l1 = Det4(tetra.A, u, tetra.C, tetra.D) / det;
        float l2 = Det4(tetra.A, tetra.B, u, tetra.D) / det;
        float l3 = Det4(tetra.A, tetra.B, tetra.C, u) / det;
        float sum = l0 + l1 + l2 + l3;
        if (sum <= Epsilon)
            return false;
        float t = 1f / sum;
        if (l0 * t < -HitMargin || l1 * t < -HitMargin || l2 * t < -HitMargin || l3 * t < -HitMargin)
            return false;
        if (t < near)
            return false;
        depth = t;
        return true;
    }

    /// <summary>
    /// Computes the determinant of a 4x4 matrix with the given columns, expanding along the X row.
    /// </summary>
    private static float Det4(Float4 a, Float4 b, Float4 c, Float4 d) =>
        a.X * Det3(b, c, d) - b.X * Det3(a, c, d) + c.X * Det3(a, b, d) - d.X * Det3(a, b, c);

    /// <summary>
    /// Computes the determinant of the 3x3 matrix formed by the Y, Z, W components of the columns.
    /// </summary>
    private static float Det3(Float4 a, Float4 b, Float4 c) =>
        a.Y * (b.Z * c.W - b.W * c.Z) - a.Z * (b.Y * c.W - b.W * c.Y) + a.W * (b.Y * c.Z - b.Z * c.Y);

    /// <summary>
    /// Finds the closest approach between a ray and a segment (cylinder of world-space radius),
    /// exactly like the CPU renderer.
    /// </summary>
    private bool TryHitSegment(GpuSegment s, Float4 u, out float depth)
    {
        u.W *= segmentWScale;
        s.Start.W *= segmentWScale;
        s.End.W *= segmentWScale;
        depth = 0;
        Float4 diff = s.End - s.Start;
        float uu = Hlsl.Dot(u, u);
        float ud = Hlsl.Dot(u, diff);
        float dd = Hlsl.Dot(diff, diff);
        float ua = Hlsl.Dot(u, s.Start);
        float ad = Hlsl.Dot(s.Start, diff);
        float det = uu * dd - ud * ud;
        float t;
        float ss;
        if (det < Epsilon)
        {
            // Ray parallel to the segment (or a degenerate segment): check both endpoints.
            if (EndpointHit(s.Start, u, uu, s.Radius, out depth))
                return true;
            return EndpointHit(s.End, u, uu, s.Radius, out depth);
        }
        t = (ua * dd - ud * ad) / det;
        ss = (ua * ud - ad * uu) / det;
        if (ss < 0)
        {
            ss = 0;
            t = ua / uu;
        }
        else if (ss > 1)
        {
            ss = 1;
            t = Hlsl.Dot(u, s.End) / uu;
        }
        if (t < near)
            return false;
        float effective = s.Radius + t * footprintK;
        Float4 closest = s.Start + ss * diff - t * u;
        if (Hlsl.Dot(closest, closest) > effective * effective)
            return false;
        depth = t;
        return true;
    }

    /// <summary>
    /// Checks the closest approach of the ray to a single endpoint (parallel case).
    /// </summary>
    private bool EndpointHit(Float4 point, Float4 u, float uu, float radius, out float depth)
    {
        depth = 0;
        float t = Hlsl.Dot(u, point) / uu;
        if (t < near)
            return false;
        float effective = radius + t * footprintK;
        Float4 closest = point - t * u;
        if (Hlsl.Dot(closest, closest) > effective * effective)
            return false;
        depth = t;
        return true;
    }
}
