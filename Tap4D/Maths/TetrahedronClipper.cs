using System.Numerics;
using Tap4D.Geometry;

namespace Tap4D.Maths;

/// <summary>
/// Clips tetrahedra against half-spaces (the near plane of the camera and the ana-kata
/// bounds of the virtual screen). Clipped parts are re-tetrahedralized, so the output
/// is always a list of whole tetrahedra.
/// </summary>
public static class TetrahedronClipper
{
    /// <summary>
    /// Clips a tetrahedron against the half-space Z &gt;= near.
    /// </summary>
    /// <param name="tetrahedron">The tetrahedron in camera coordinates.</param>
    /// <param name="near">The distance from the camera to the near plane.</param>
    /// <returns>
    /// From 0 to 3 tetrahedra covering the kept part; an empty list if the tetrahedron
    /// lies fully behind the near plane.
    /// </returns>
    public static List<Tetrahedron> ClipNearPlane(Tetrahedron tetrahedron, float near) =>
        ClipHalfSpace(tetrahedron, Vector4.UnitZ, near);

    /// <summary>
    /// Clips a tetrahedron against the half-space dot(v, normal) &gt;= offset.
    /// </summary>
    /// <param name="tetrahedron">The tetrahedron.</param>
    /// <param name="normal">The half-space normal (need not be normalized).</param>
    /// <param name="offset">The half-space offset along the normal.</param>
    /// <returns>
    /// From 0 to 3 tetrahedra covering the kept part; an empty list if the tetrahedron
    /// lies fully outside the half-space.
    /// </returns>
    public static List<Tetrahedron> ClipHalfSpace(Tetrahedron tetrahedron, Vector4 normal, float offset)
    {
        Vector4[] vertices = [tetrahedron.A, tetrahedron.B, tetrahedron.C, tetrahedron.D];
        float[] distances = [.. vertices.Select(v => Vector4.Dot(v, normal) - offset)];
        List<int> inside = [];
        for (int i = 0; i < 4; i++)
            if (distances[i] >= 0)
                inside.Add(i);

        return inside.Count switch
        {
            4 => [tetrahedron],
            0 => [],
            1 => ClipOneInside(vertices, distances, inside[0]),
            2 => ClipTwoInside(vertices, distances, inside[0], inside[1]),
            _ => ClipThreeInside(vertices, distances, inside),
        };
    }

    /// <summary>
    /// Finds the point where the edge between an inside and an outside vertex crosses
    /// the clipping plane.
    /// </summary>
    /// <param name="inside">The vertex inside the half-space.</param>
    /// <param name="outside">The vertex outside the half-space.</param>
    /// <param name="insideDistance">The signed distance of the inside vertex from the plane.</param>
    /// <param name="outsideDistance">The signed distance of the outside vertex from the plane.</param>
    /// <returns>The intersection point on the plane.</returns>
    private static Vector4 IntersectEdge(Vector4 inside, Vector4 outside, float insideDistance, float outsideDistance)
    {
        float t = insideDistance / (insideDistance - outsideDistance);
        return inside + t * (outside - inside);
    }

    /// <summary>
    /// Returns the tetrahedron vertex indices not present in <paramref name="inside"/>.
    /// </summary>
    /// <param name="inside">The indices of vertices inside the half-space.</param>
    /// <returns>The indices of the outside vertices.</returns>
    private static List<int> OutsideIndices(List<int> inside)
    {
        List<int> outside = [];
        for (int i = 0; i < 4; i++)
            if (!inside.Contains(i))
                outside.Add(i);
        return outside;
    }

    /// <summary>
    /// Clips a tetrahedron that has exactly one vertex inside the half-space.
    /// The kept part is a single smaller tetrahedron.
    /// </summary>
    private static List<Tetrahedron> ClipOneInside(Vector4[] vertices, float[] distances, int i0)
    {
        List<int> outside = OutsideIndices([i0]);
        Vector4 p0 = IntersectEdge(vertices[i0], vertices[outside[0]], distances[i0], distances[outside[0]]);
        Vector4 p1 = IntersectEdge(vertices[i0], vertices[outside[1]], distances[i0], distances[outside[1]]);
        Vector4 p2 = IntersectEdge(vertices[i0], vertices[outside[2]], distances[i0], distances[outside[2]]);
        return [new(vertices[i0], p0, p1, p2)];
    }

    /// <summary>
    /// Clips a tetrahedron that has exactly two vertices inside the half-space.
    /// The kept part is a wedge, split into three tetrahedra by fanning from the first inside vertex.
    /// </summary>
    private static List<Tetrahedron> ClipTwoInside(Vector4[] vertices, float[] distances, int i0, int i1)
    {
        List<int> outside = OutsideIndices([i0, i1]);
        Vector4 a = IntersectEdge(vertices[i0], vertices[outside[0]], distances[i0], distances[outside[0]]);
        Vector4 b = IntersectEdge(vertices[i0], vertices[outside[1]], distances[i0], distances[outside[1]]);
        Vector4 c = IntersectEdge(vertices[i1], vertices[outside[0]], distances[i1], distances[outside[0]]);
        Vector4 d = IntersectEdge(vertices[i1], vertices[outside[1]], distances[i1], distances[outside[1]]);
        return [new(vertices[i0], vertices[i1], c, d), new(vertices[i0], a, b, d), new(vertices[i0], a, d, c)];
    }

    /// <summary>
    /// Clips a tetrahedron that has exactly three vertices inside the half-space.
    /// The kept part is a triangular prism, split into three tetrahedra.
    /// </summary>
    private static List<Tetrahedron> ClipThreeInside(Vector4[] vertices, float[] distances, List<int> inside)
    {
        int o = OutsideIndices(inside)[0];
        Vector4 a = IntersectEdge(vertices[inside[0]], vertices[o], distances[inside[0]], distances[o]);
        Vector4 b = IntersectEdge(vertices[inside[1]], vertices[o], distances[inside[1]], distances[o]);
        Vector4 c = IntersectEdge(vertices[inside[2]], vertices[o], distances[inside[2]], distances[o]);
        return [
            new(vertices[inside[0]], vertices[inside[1]], vertices[inside[2]], c),
            new(vertices[inside[0]], vertices[inside[1]], c, b),
            new(vertices[inside[0]], a, b, c),
        ];
    }
}
