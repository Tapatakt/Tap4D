using System.Numerics;

namespace Tap4D.Rendering;

/// <summary>
/// Intersects segments with screen pixels, treating each segment as a cylinder of the
/// given world-space radius: a pixel is hit when the 2-plane of its sight rays
/// (X = pixelX * Z / focal, Y = pixelY * Z / focal) passes within the radius of the segment.
/// </summary>
public static class SegmentPixelIntersection
{
    private const float Epsilon = 1e-12f;

    /// <summary>
    /// Finds the closest point between a camera-space segment and the pixel's ray plane,
    /// and whether it is within the segment's radius.
    /// </summary>
    /// <param name="start">The first endpoint in camera space (Z &gt; 0).</param>
    /// <param name="end">The second endpoint in camera space (Z &gt; 0).</param>
    /// <param name="radius">The cylinder radius in world units.</param>
    /// <param name="pixelX">The X coordinate of the pixel on the virtual screen.</param>
    /// <param name="pixelY">The Y coordinate of the pixel on the virtual screen.</param>
    /// <param name="focal">The focal distance of the camera.</param>
    /// <param name="depth">The camera-space Z of the closest point.</param>
    /// <param name="screenW">
    /// The ana-kata coordinate of the closest point on the virtual screen
    /// (perspective-correct: interpolated in camera space, then divided by depth).
    /// </param>
    /// <returns>True if the closest point is within the radius.</returns>
    public static bool TryIntersect(
        Vector4 start, Vector4 end, float radius,
        float pixelX, float pixelY, float focal, out float depth, out float screenW)
    {
        depth = 0;
        screenW = 0;
        float rx = pixelX / focal;
        float ry = pixelY / focal;
        // dist²(p, plane) = eᵀ G⁻¹ e with residuals e = (p.X - rx·p.Z, p.Y - ry·p.Z)
        // and Gram matrix G of the plane normals; G⁻¹ = [[g22, -g12], [-g12, g11]] / det.
        float g11 = 1 + rx * rx;
        float g22 = 1 + ry * ry;
        float g12 = rx * ry;
        float det = g11 * g22 - g12 * g12;
        float e1a = start.X - rx * start.Z;
        float e2a = start.Y - ry * start.Z;
        float e1d = end.X - rx * end.Z - e1a;
        float e2d = end.Y - ry * end.Z - e2a;
        float a = g22 * e1d * e1d - 2 * g12 * e1d * e2d + g11 * e2d * e2d;
        float halfB = g22 * e1a * e1d - g12 * (e1a * e2d + e2a * e1d) + g11 * e2a * e2d;
        float t = a < Epsilon ? 0 : Math.Clamp(-halfB / a, 0f, 1f);
        float e1 = e1a + t * e1d;
        float e2 = e2a + t * e2d;
        float distSq = (g22 * e1 * e1 - 2 * g12 * e1 * e2 + g11 * e2 * e2) / det;
        if (distSq > radius * radius)
            return false;
        depth = start.Z + t * (end.Z - start.Z);
        screenW = focal * (start.W + t * (end.W - start.W)) / depth;
        return true;
    }
}
