using System.Numerics;
using Tap4D.Geometry;

namespace Tap4D.Rendering.Raycast;

/// <summary>
/// Intersects a ray from the camera origin with a tetrahedron: solves the barycentric
/// system [V0 V1 V2 V3] λ = u via Cramer's rule; the ray hits when the scaled solution
/// has all barycentric coordinates non-negative and positive ray parameter.
/// </summary>
public static class RayTetrahedronIntersection
{
    private const float Epsilon = 1e-12f;

    /// <summary>
    /// Barycentric hit margin: the tetrahedron is treated as slightly expanded, so a ray
    /// never slips through the seam between two adjacent tetrahedra.
    /// </summary>
    private const float HitMargin = 1e-6f;

    /// <summary>
    /// Finds where a ray hits a tetrahedron.
    /// </summary>
    /// <param name="tetrahedron">The tetrahedron in camera space.</param>
    /// <param name="direction">The unit ray direction in camera space.</param>
    /// <param name="minDepth">The minimum accepted radial distance (the near distance).</param>
    /// <param name="depth">The radial distance from the camera to the hit point.</param>
    /// <returns>True if the ray hits the tetrahedron beyond the near distance.</returns>
    public static bool TryIntersect(Tetrahedron tetrahedron, Vector4 direction, float minDepth, out float depth)
    {
        depth = 0;
        float det = Det4(tetrahedron.A, tetrahedron.B, tetrahedron.C, tetrahedron.D);
        if (MathF.Abs(det) < Epsilon)
            return false;
        float l0 = Det4(direction, tetrahedron.B, tetrahedron.C, tetrahedron.D) / det;
        float l1 = Det4(tetrahedron.A, direction, tetrahedron.C, tetrahedron.D) / det;
        float l2 = Det4(tetrahedron.A, tetrahedron.B, direction, tetrahedron.D) / det;
        float l3 = Det4(tetrahedron.A, tetrahedron.B, tetrahedron.C, direction) / det;
        float sum = l0 + l1 + l2 + l3;
        if (sum <= Epsilon)
            return false; // the hit point lies behind the camera or at infinity
        float t = 1f / sum;
        if (l0 * t < -HitMargin || l1 * t < -HitMargin || l2 * t < -HitMargin || l3 * t < -HitMargin)
            return false;
        if (t < minDepth)
            return false;
        depth = t;
        return true;
    }

    /// <summary>
    /// Computes the determinant of a 4x4 matrix with the given columns,
    /// expanding along the X row.
    /// </summary>
    private static float Det4(Vector4 a, Vector4 b, Vector4 c, Vector4 d) =>
        a.X * Det3Yzw(b, c, d) - b.X * Det3Yzw(a, c, d) + c.X * Det3Yzw(a, b, d) - d.X * Det3Yzw(a, b, c);

    /// <summary>
    /// Computes the determinant of the 3x3 matrix formed by the Y, Z, W components
    /// of the given columns.
    /// </summary>
    private static float Det3Yzw(Vector4 a, Vector4 b, Vector4 c) =>
        a.Y * (b.Z * c.W - b.W * c.Z) - a.Z * (b.Y * c.W - b.W * c.Y) + a.W * (b.Y * c.Z - b.Z * c.Y);
}
