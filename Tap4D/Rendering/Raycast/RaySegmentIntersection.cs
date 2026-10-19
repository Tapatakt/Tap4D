using System.Numerics;

namespace Tap4D.Rendering.Raycast;

/// <summary>
/// Intersects a ray from the camera origin with a segment treated as a 4D cylinder:
/// finds the closest point between the ray and the segment and compares the distance
/// with the radius.
/// </summary>
public static class RaySegmentIntersection
{
    private const float Epsilon = 1e-12f;

    /// <summary>
    /// Finds the closest approach between a ray and a segment.
    /// The W components are scaled by <paramref name="wScale"/> before solving, making the
    /// tube anisotropic: fatter in the ana-kata direction by 1/wScale to compensate for
    /// the low ana-kata resolution (wScale = 1 is the plain round cylinder).
    /// The ray represents the whole voxel's direction box, so the effective radius is
    /// inflated by <paramref name="footprintK"/> times the hit distance: this guarantees
    /// that adjacent voxels' rays tile the view cone without gaps.
    /// </summary>
    /// <param name="start">The first endpoint in camera space.</param>
    /// <param name="end">The second endpoint in camera space.</param>
    /// <param name="radius">The cylinder radius in world units.</param>
    /// <param name="direction">The unit ray direction in camera space.</param>
    /// <param name="minDepth">The minimum accepted radial distance (the near distance).</param>
    /// <param name="wScale">The ana-kata anisotropy factor: smaller means fatter in W.</param>
    /// <param name="footprintK">The voxel angular footprint coefficient (radians, W-scaled).</param>
    /// <param name="depth">The radial distance from the camera to the closest ray point.</param>
    /// <returns>True if the closest approach is within the radius beyond the near distance.</returns>
    public static bool TryIntersect(
        Vector4 start, Vector4 end, float radius, Vector4 direction, float minDepth, float wScale, float footprintK, out float depth)
    {
        direction.W *= wScale;
        start.W *= wScale;
        end.W *= wScale;
        depth = 0;
        Vector4 d = end - start;
        float uu = Vector4.Dot(direction, direction);
        float ud = Vector4.Dot(direction, d);
        float dd = Vector4.Dot(d, d);
        float ua = Vector4.Dot(direction, start);
        float ad = Vector4.Dot(start, d);
        float det = uu * dd - ud * ud;
        float t;
        float s;
        if (det < Epsilon)
        {
            // Ray parallel to the segment (or a degenerate segment): check both endpoints.
            if (!TryEndpoint(start, direction, uu, radius, minDepth, footprintK, out depth)
                && !TryEndpoint(end, direction, uu, radius, minDepth, footprintK, out depth))
                return false;
            return true;
        }
        t = (ua * dd - ud * ad) / det;
        s = (ua * ud - ad * uu) / det;
        if (s < 0)
            (s, t) = (0, ua / uu);
        else if (s > 1)
            (s, t) = (1, Vector4.Dot(direction, end) / uu);
        if (t < minDepth)
            return false;
        float effective = radius + t * footprintK;
        Vector4 closest = start + s * d - t * direction;
        if (closest.LengthSquared() > effective * effective)
            return false;
        depth = t;
        return true;
    }

    /// <summary>
    /// Checks the closest approach of the ray to a single endpoint (parallel case).
    /// </summary>
    private static bool TryEndpoint(
        Vector4 point, Vector4 direction, float uu, float radius, float minDepth, float footprintK, out float depth)
    {
        depth = 0;
        float t = Vector4.Dot(direction, point) / uu;
        if (t < minDepth)
            return false;
        float effective = radius + t * footprintK;
        Vector4 closest = point - t * direction;
        if (closest.LengthSquared() > effective * effective)
            return false;
        depth = t;
        return true;
    }
}
