using System.Numerics;
using Tap4D.Geometry;

namespace Tap4D.Rendering.Raycast;

/// <summary>
/// Computes conservative (in fact exact) bounds of the angular coordinate focal · p.Axis / |p|
/// over primitives, for per-voxel culling in the raycaster.
/// The extremes over a tetrahedron are attained at a vertex, at an edge-interior point,
/// or at the perpendicular foot of a face plane.
/// </summary>
public static class AngularBounds
{
    /// <summary>
    /// Computes the exact range of the angular coordinate over a tetrahedron.
    /// </summary>
    /// <param name="tetrahedron">The tetrahedron in camera space.</param>
    /// <param name="focal">The focal distance (sphere radius) of the camera.</param>
    /// <param name="axis">The coordinate axis: 0 = X, 1 = Y, 2 = Z, 3 = W.</param>
    /// <returns>The (min, max) of focal · p.Axis / |p| over the tetrahedron.</returns>
    public static (float Min, float Max) RangeForAxis(Tetrahedron tetrahedron, float focal, int axis)
    {
        float lo = float.MaxValue;
        float hi = float.MinValue;
        Vector4[] v = [tetrahedron.A, tetrahedron.B, tetrahedron.C, tetrahedron.D];
        foreach (Vector4 p in v)
            Update(p, focal, axis, ref lo, ref hi);
        EdgeExtremum(v[0], v[1], focal, axis, ref lo, ref hi);
        EdgeExtremum(v[0], v[2], focal, axis, ref lo, ref hi);
        EdgeExtremum(v[0], v[3], focal, axis, ref lo, ref hi);
        EdgeExtremum(v[1], v[2], focal, axis, ref lo, ref hi);
        EdgeExtremum(v[1], v[3], focal, axis, ref lo, ref hi);
        EdgeExtremum(v[2], v[3], focal, axis, ref lo, ref hi);
        FaceExtremum(v[0], v[1], v[2], focal, axis, ref lo, ref hi);
        FaceExtremum(v[0], v[1], v[3], focal, axis, ref lo, ref hi);
        FaceExtremum(v[0], v[2], v[3], focal, axis, ref lo, ref hi);
        FaceExtremum(v[1], v[2], v[3], focal, axis, ref lo, ref hi);
        return (lo, hi);
    }

    /// <summary>
    /// Computes the exact range of the angular ana-kata (W) coordinate over a tetrahedron.
    /// </summary>
    public static (float Min, float Max) WRange(Tetrahedron tetrahedron, float focal) =>
        RangeForAxis(tetrahedron, focal, 3);

    /// <summary>
    /// Computes the exact range of the angular coordinate over a segment.
    /// </summary>
    /// <param name="segment">The segment in camera space.</param>
    /// <param name="focal">The focal distance (sphere radius) of the camera.</param>
    /// <param name="axis">The coordinate axis: 0 = X, 1 = Y, 2 = Z, 3 = W.</param>
    /// <returns>The (min, max) of focal · p.Axis / |p| over the segment.</returns>
    public static (float Min, float Max) RangeForAxis(Segment4 segment, float focal, int axis)
    {
        float lo = float.MaxValue;
        float hi = float.MinValue;
        Update(segment.Start, focal, axis, ref lo, ref hi);
        Update(segment.End, focal, axis, ref lo, ref hi);
        EdgeExtremum(segment.Start, segment.End, focal, axis, ref lo, ref hi);
        return (lo, hi);
    }

    /// <summary>
    /// Computes the exact range of the angular ana-kata (W) coordinate over a segment.
    /// </summary>
    public static (float Min, float Max) WRange(Segment4 segment, float focal) =>
        RangeForAxis(segment, focal, 3);

    /// <summary>
    /// Computes the range of the angular coordinate over a segment, expanded by
    /// the angular footprint of its cylinder radius (estimated from the axis's closest
    /// approach to the origin).
    /// </summary>
    /// <param name="segment">The segment in camera space.</param>
    /// <param name="focal">The focal distance (sphere radius) of the camera.</param>
    /// <param name="axis">The coordinate axis: 0 = X, 1 = Y, 2 = Z, 3 = W.</param>
    /// <returns>The conservative (min, max) of focal · p.Axis / |p| over the cylinder.</returns>
    public static (float Min, float Max) RangeForAxisWithRadius(Segment4 segment, float focal, int axis)
    {
        (float lo, float hi) = RangeForAxis(segment, focal, axis);
        Vector4 d = segment.End - segment.Start;
        float dd = Vector4.Dot(d, d);
        float t = dd < 1e-12f ? 0 : Math.Clamp(-Vector4.Dot(segment.Start, d) / dd, 0f, 1f);
        float minDist = (segment.Start + t * d).Length();
        float margin = focal * segment.Radius / MathF.Max(minDist, 1e-6f);
        return (lo - margin, hi + margin);
    }

    /// <summary>
    /// Computes the range of the angular ana-kata coordinate over a segment, expanded by
    /// the angular footprint of its cylinder radius.
    /// </summary>
    public static (float Min, float Max) WRangeWithRadius(Segment4 segment, float focal) =>
        RangeForAxisWithRadius(segment, focal, 3);

    /// <summary>
    /// Extends the range with the value at a vertex.
    /// </summary>
    private static void Update(Vector4 p, float focal, int axis, ref float lo, ref float hi)
    {
        float h = focal * Component(p, axis) / p.Length();
        lo = MathF.Min(lo, h);
        hi = MathF.Max(hi, h);
    }

    /// <summary>
    /// Extends the range with the interior extreme of an edge: the derivative of
    /// focal·Axis/|p| along the edge is linear, so the extreme has a closed form.
    /// </summary>
    private static void EdgeExtremum(Vector4 a, Vector4 b, float focal, int axis, ref float lo, ref float hi)
    {
        Vector4 d = b - a;
        float da = Component(d, axis);
        float aa = Component(a, axis);
        float n0 = da * Vector4.Dot(a, a) - aa * Vector4.Dot(a, d);
        float n1 = da * Vector4.Dot(a, d) - aa * Vector4.Dot(d, d);
        if (MathF.Abs(n1) < 1e-12f)
            return;
        float t = -n0 / n1;
        if (t > 0 && t < 1)
            Update(a + t * d, focal, axis, ref lo, ref hi);
    }

    /// <summary>
    /// Extends the range with the perpendicular foot of a face plane from the origin,
    /// if that foot lies inside the face triangle.
    /// </summary>
    private static void FaceExtremum(Vector4 a, Vector4 b, Vector4 c, float focal, int axis, ref float lo, ref float hi)
    {
        Vector4 u = b - a;
        Vector4 v = c - a;
        float uu = Vector4.Dot(u, u);
        float uv = Vector4.Dot(u, v);
        float vv = Vector4.Dot(v, v);
        float det = uu * vv - uv * uv;
        if (MathF.Abs(det) < 1e-12f)
            return;
        float au = Vector4.Dot(a, u);
        float av = Vector4.Dot(a, v);
        float alpha = (-au * vv + av * uv) / det;
        float beta = (-av * uu + au * uv) / det;
        if (alpha >= 0 && beta >= 0 && alpha + beta <= 1)
            Update(a + alpha * u + beta * v, focal, axis, ref lo, ref hi);
    }

    /// <summary>
    /// Returns the coordinate of a vector by axis index (0 = X, 1 = Y, 2 = Z, 3 = W).
    /// </summary>
    private static float Component(Vector4 p, int axis) => axis switch
    {
        0 => p.X,
        1 => p.Y,
        2 => p.Z,
        _ => p.W,
    };
}

/// <summary>
/// The angular bounds of a primitive for all three screen axes, for per-voxel culling.
/// </summary>
/// <param name="X">The range of focal · p.X / |p|.</param>
/// <param name="Y">The range of focal · p.Y / |p|.</param>
/// <param name="W">The range of focal · p.W / |p|.</param>
public readonly record struct AngularRanges(
    (float Min, float Max) X,
    (float Min, float Max) Y,
    (float Min, float Max) W)
{
    /// <summary>
    /// Computes the angular bounds of a tetrahedron for all three screen axes.
    /// </summary>
    public static AngularRanges Compute(Tetrahedron tetrahedron, float focal) => new(
        AngularBounds.RangeForAxis(tetrahedron, focal, 0),
        AngularBounds.RangeForAxis(tetrahedron, focal, 1),
        AngularBounds.RangeForAxis(tetrahedron, focal, 3));

    /// <summary>
    /// Computes the angular bounds of a segment for all three screen axes,
    /// expanded by the radius footprint.
    /// </summary>
    public static AngularRanges ComputeWithRadius(Segment4 segment, float focal) => new(
        AngularBounds.RangeForAxisWithRadius(segment, focal, 0),
        AngularBounds.RangeForAxisWithRadius(segment, focal, 1),
        AngularBounds.RangeForAxisWithRadius(segment, focal, 3));

    /// <summary>
    /// Checks whether the ana-kata range overlaps the slice's interval.
    /// The X/Y culling is done by the screen tiling, so only W is checked here.
    /// </summary>
    public bool OverlapsW(float wLo, float wHi) => W.Max >= wLo && W.Min <= wHi;
}
