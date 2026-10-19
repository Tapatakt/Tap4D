using ComputeSharp;

namespace Tap4D.Rendering.Raycast;

/// <summary>
/// Pass 1 of the raycasting renderer: one thread per screen pixel, looping over the
/// ana-kata slices. Casts the voxel's sub-rays over the candidates of the pixel's
/// screen tile and records the voxel state into the depth buffer (0 = empty,
/// &gt; 0 = nearest hit depth) and the coverage buffer.
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
    ReadOnlyBuffer<int> visibleOffsets,
    ReadOnlyBuffer<int> visibleIndices,
    ReadOnlyBuffer<int> occluderOffsets,
    ReadOnlyBuffer<int> occluderIndices,
    ReadOnlyBuffer<int> segmentOffsets,
    ReadOnlyBuffer<int> segmentIndices,
    int width, int height, int sliceCount, int subRays, int tilesX, int tileSize,
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
    private readonly ReadOnlyBuffer<int> visibleOffsets = visibleOffsets;
    private readonly ReadOnlyBuffer<int> visibleIndices = visibleIndices;
    private readonly ReadOnlyBuffer<int> occluderOffsets = occluderOffsets;
    private readonly ReadOnlyBuffer<int> occluderIndices = occluderIndices;
    private readonly ReadOnlyBuffer<int> segmentOffsets = segmentOffsets;
    private readonly ReadOnlyBuffer<int> segmentIndices = segmentIndices;
    private readonly int width = width;
    private readonly int height = height;
    private readonly int sliceCount = sliceCount;
    private readonly int subRays = subRays;
    private readonly int tilesX = tilesX;
    private readonly int tileSize = tileSize;
    private readonly float scale = scale;
    private readonly float focal = focal;
    private readonly float minW = minW;
    private readonly float sliceWidth = sliceWidth;
    private readonly float near = near;

    /// <summary>
    /// Casts the sub-rays of all slices of one pixel and records the voxel states.
    /// Dispatched 1D over pixels: the voxel count exceeds the 65535-groups limit,
    /// and each thread loops over the ana-kata slices instead.
    /// The X/Y culling is done by the screen tiling built on the CPU; the per-slice
    /// loop only checks the ana-kata range of each tile candidate.
    /// </summary>
    public void Execute()
    {
        int pixel = ThreadIds.X;
        float pixelX = ((pixel % width) + 0.5f - width / 2f) * scale;
        float pixelY = (height / 2f - (pixel / width) - 0.5f) * scale;
        int tile = (pixel / width / tileSize) * tilesX + (pixel % width) / tileSize;
        for (int slice = 0; slice < sliceCount; slice++)
            CastSlice(pixel, pixelX, pixelY, tile, slice);
    }

    /// <summary>
    /// Casts the sub-rays of one voxel and records its state. Tile candidates whose
    /// angular ana-kata range does not overlap the slice are skipped.
    /// </summary>
    private void CastSlice(int pixel, float pixelX, float pixelY, int tile, int slice)
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
            if (Cast(u, tile, wLo, wHi, out float depth, out bool occluder) && !occluder)
            {
                hits++;
                best = Hlsl.Min(best, depth);
            }
        }
        depths[voxel] = hits > 0 ? best : 0f;
        coverage[voxel] = (float)hits / subRays;
    }

    /// <summary>
    /// Finds the nearest hit along a ray over the tile's candidates overlapping the
    /// slice's ana-kata interval; occluders participate but only block.
    /// </summary>
    private bool Cast(Float4 u, int tile, float wLo, float wHi, out float depth, out bool isOccluder)
    {
        depth = float.MaxValue;
        isOccluder = false;
        float d;
        for (int k = visibleOffsets[tile]; k < visibleOffsets[tile + 1]; k++)
        {
            int i = visibleIndices[k];
            if (visibleTetrahedra[i].MaxW >= wLo && visibleTetrahedra[i].MinW <= wHi
                && TryHitTetrahedron(visibleTetrahedra[i], u, out d) && d < depth)
            {
                depth = d;
                isOccluder = false;
            }
        }
        for (int k = occluderOffsets[tile]; k < occluderOffsets[tile + 1]; k++)
        {
            int i = occluderIndices[k];
            if (occluderTetrahedra[i].MaxW >= wLo && occluderTetrahedra[i].MinW <= wHi
                && TryHitTetrahedron(occluderTetrahedra[i], u, out d) && d < depth)
            {
                depth = d;
                isOccluder = true;
            }
        }
        for (int k = segmentOffsets[tile]; k < segmentOffsets[tile + 1]; k++)
        {
            int i = segmentIndices[k];
            if (segments[i].MaxW >= wLo && segments[i].MinW <= wHi
                && TryHitSegment(segments[i], u, out d) && d < depth)
            {
                depth = d;
                isOccluder = false;
            }
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
