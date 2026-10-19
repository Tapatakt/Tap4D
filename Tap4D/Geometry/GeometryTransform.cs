using System.Numerics;

namespace Tap4D.Geometry;

/// <summary>
/// Applies 4D transformations to scene geometry.
/// </summary>
public static class GeometryTransform
{
    /// <summary>
    /// Transforms all vertices of a tetrahedron by a matrix.
    /// </summary>
    /// <param name="t">The tetrahedron.</param>
    /// <param name="m">The transformation matrix.</param>
    /// <returns>The transformed tetrahedron.</returns>
    public static Tetrahedron Transform(Tetrahedron t, Matrix4x4 m) => new(
        Vector4.Transform(t.A, m),
        Vector4.Transform(t.B, m),
        Vector4.Transform(t.C, m),
        Vector4.Transform(t.D, m));

    /// <summary>
    /// Transforms both endpoints of a segment by a matrix.
    /// </summary>
    /// <param name="s">The segment.</param>
    /// <param name="m">The transformation matrix.</param>
    /// <returns>The transformed segment.</returns>
    public static Segment4 Transform(Segment4 s, Matrix4x4 m) =>
        new(Vector4.Transform(s.Start, m), Vector4.Transform(s.End, m), s.Radius);

    /// <summary>
    /// Transforms a sequence of tetrahedra by a matrix.
    /// </summary>
    /// <param name="source">The tetrahedra.</param>
    /// <param name="m">The transformation matrix.</param>
    /// <returns>The transformed tetrahedra.</returns>
    public static List<Tetrahedron> TransformAll(IEnumerable<Tetrahedron> source, Matrix4x4 m) =>
        [.. source.Select(t => Transform(t, m))];

    /// <summary>
    /// Transforms a sequence of segments by a matrix.
    /// </summary>
    /// <param name="source">The segments.</param>
    /// <param name="m">The transformation matrix.</param>
    /// <returns>The transformed segments.</returns>
    public static List<Segment4> TransformAll(IEnumerable<Segment4> source, Matrix4x4 m) =>
        [.. source.Select(s => Transform(s, m))];
}
