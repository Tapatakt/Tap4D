using System.Numerics;
using Tap4D.Geometry;

namespace Tap4D.Rendering;

/// <summary>
/// Intersects tetrahedra with the set of camera-space points projecting to a given screen pixel
/// (the 2-plane X = pixelX * Z / focal, Y = pixelY * Z / focal). The intersection is a segment;
/// its nearer endpoint is the candidate shown at the pixel.
/// </summary>
public static class TetraPixelIntersection
{
    private const float Epsilon = 1e-6f;

    private const float TieEpsilon = 1e-4f;

    /// <summary>
    /// Barycentric hit margin: the tetrahedron is treated as slightly expanded, so a ray
    /// never slips through the seam between two adjacent tetrahedra.
    /// </summary>
    private const float HitMargin = 1e-6f;

    /// <summary>
    /// Finds the nearest point of a tetrahedron projecting to the given pixel.
    /// </summary>
    /// <param name="tetrahedron">The tetrahedron in camera space (all vertices with Z &gt; 0).</param>
    /// <param name="pixelX">The X coordinate of the pixel on the virtual screen.</param>
    /// <param name="pixelY">The Y coordinate of the pixel on the virtual screen.</param>
    /// <param name="focal">The focal distance of the camera.</param>
    /// <param name="depth">The camera-space Z of the nearest point.</param>
    /// <param name="screenW">The ana-kata coordinate of the nearest point on the virtual screen.</param>
    /// <returns>True if the tetrahedron projects onto the pixel.</returns>
    public static bool TryIntersect(
        Tetrahedron tetrahedron, float pixelX, float pixelY, float focal, out float depth, out float screenW)
    {
        depth = 0;
        screenW = 0;
        Vector4[] v = [tetrahedron.A, tetrahedron.B, tetrahedron.C, tetrahedron.D];
        float[] xs = new float[4];
        float[] ys = new float[4];
        for (int i = 0; i < 4; i++)
        {
            xs[i] = v[i].X - pixelX / focal * v[i].Z;
            ys[i] = v[i].Y - pixelY / focal * v[i].Z;
        }
        float[] direction = NullVector(xs, ys);
        if (MaxAbs(direction) < Epsilon)
            return false;
        float[] lambda = ParticularSolution(xs, ys, direction);
        if (!FeasibleInterval(lambda, direction, out float tMin, out float tMax))
            return false;
        Vector4 atMin = PointAt(v, lambda, direction, tMin);
        Vector4 atMax = PointAt(v, lambda, direction, tMax);
        Vector4 nearest = PickNearer(atMin, atMax);
        depth = nearest.Z;
        screenW = focal * nearest.W / nearest.Z;
        return true;
    }

    /// <summary>
    /// Picks the nearer of the two intersection segment endpoints. When the depths are
    /// equal within a tie epsilon (face-on geometry), the endpoint with the smaller W wins,
    /// so that the choice is stable across CPU and GPU rounding differences.
    /// </summary>
    /// <param name="a">The first endpoint.</param>
    /// <param name="b">The second endpoint.</param>
    /// <returns>The endpoint to show at the pixel.</returns>
    private static Vector4 PickNearer(Vector4 a, Vector4 b)
    {
        float dz = a.Z - b.Z;
        if (dz < -TieEpsilon)
            return a;
        if (dz > TieEpsilon)
            return b;
        return a.W <= b.W ? a : b;
    }

    /// <summary>
    /// Computes the null vector of the 3x4 matrix with rows (xs, ys, 1) via signed minors.
    /// </summary>
    /// <param name="xs">The modified X coordinates of the four vertices.</param>
    /// <param name="ys">The modified Y coordinates of the four vertices.</param>
    /// <returns>The direction of the barycentric solution line.</returns>
    private static float[] NullVector(float[] xs, float[] ys)
    {
        float[] d = new float[4];
        for (int i = 0; i < 4; i++)
            d[i] = (i % 2 == 0 ? 1 : -1) * Minor(xs, ys, i);
        return d;
    }

    /// <summary>
    /// Computes the 3x3 minor obtained by skipping column i from the rows (xs, ys, 1).
    /// </summary>
    /// <param name="xs">The modified X coordinates.</param>
    /// <param name="ys">The modified Y coordinates.</param>
    /// <param name="skip">The skipped column index.</param>
    /// <returns>The determinant of the 3x3 minor.</returns>
    private static float Minor(float[] xs, float[] ys, int skip)
    {
        int j1 = skip == 0 ? 1 : 0;
        int j3 = skip == 3 ? 2 : 3;
        int j2 = 6 - skip - j1 - j3;
        return xs[j1] * (ys[j2] - ys[j3]) - xs[j2] * (ys[j1] - ys[j3]) + xs[j3] * (ys[j1] - ys[j2]);
    }

    /// <summary>
    /// Finds one barycentric solution by setting the coordinate with the largest
    /// direction component to zero and solving the remaining 3x3 system via Cramer's rule.
    /// </summary>
    /// <param name="xs">The modified X coordinates.</param>
    /// <param name="ys">The modified Y coordinates.</param>
    /// <param name="direction">The barycentric solution line direction.</param>
    /// <returns>A particular barycentric solution.</returns>
    private static float[] ParticularSolution(float[] xs, float[] ys, float[] direction)
    {
        int skip = 0;
        for (int i = 1; i < 4; i++)
            if (MathF.Abs(direction[i]) > MathF.Abs(direction[skip]))
                skip = i;
        int j1 = skip == 0 ? 1 : 0;
        int j3 = skip == 3 ? 2 : 3;
        int j2 = 6 - skip - j1 - j3;
        float det = Minor(xs, ys, skip);
        float[] lambda = new float[4];
        lambda[j1] = (xs[j2] * ys[j3] - xs[j3] * ys[j2]) / det;
        lambda[j2] = -(xs[j1] * ys[j3] - xs[j3] * ys[j1]) / det;
        lambda[j3] = (xs[j1] * ys[j2] - xs[j2] * ys[j1]) / det;
        return lambda;
    }

    /// <summary>
    /// Finds the parameter interval where all barycentric coordinates are at least
    /// -HitMargin (the tetrahedron is slightly expanded to close seams).
    /// </summary>
    /// <param name="lambda">A particular barycentric solution.</param>
    /// <param name="direction">The barycentric solution line direction.</param>
    /// <param name="tMin">The lower end of the feasible interval.</param>
    /// <param name="tMax">The upper end of the feasible interval.</param>
    /// <returns>True if the feasible interval is non-empty.</returns>
    private static bool FeasibleInterval(float[] lambda, float[] direction, out float tMin, out float tMax)
    {
        tMin = float.NegativeInfinity;
        tMax = float.PositiveInfinity;
        for (int i = 0; i < 4; i++)
        {
            if (direction[i] > Epsilon)
                tMin = MathF.Max(tMin, -(lambda[i] + HitMargin) / direction[i]);
            else if (direction[i] < -Epsilon)
                tMax = MathF.Min(tMax, -(lambda[i] + HitMargin) / direction[i]);
            else if (lambda[i] < -HitMargin)
                return false;
        }
        return tMin <= tMax;
    }

    /// <summary>
    /// Evaluates the point of the tetrahedron at a parameter of the barycentric solution line.
    /// </summary>
    /// <param name="v">The four vertices.</param>
    /// <param name="lambda">A particular barycentric solution.</param>
    /// <param name="direction">The barycentric solution line direction.</param>
    /// <param name="t">The line parameter.</param>
    /// <returns>The point in camera space.</returns>
    private static Vector4 PointAt(Vector4[] v, float[] lambda, float[] direction, float t)
    {
        Vector4 p = Vector4.Zero;
        for (int i = 0; i < 4; i++)
            p += (lambda[i] + t * direction[i]) * v[i];
        return p;
    }

    /// <summary>
    /// Returns the largest absolute component of a vector.
    /// </summary>
    /// <param name="d">The vector.</param>
    /// <returns>The largest absolute component.</returns>
    private static float MaxAbs(float[] d) => d.Max(MathF.Abs);
}
