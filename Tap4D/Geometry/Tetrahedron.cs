using System.Numerics;

namespace Tap4D.Geometry;

/// <summary>
/// A tetrahedron embedded in 4D space: the base primitive of scene geometry.
/// Composite polytopes are built from tetrahedra, like 3D meshes are built from triangles.
/// </summary>
/// <param name="a">The first vertex.</param>
/// <param name="b">The second vertex.</param>
/// <param name="c">The third vertex.</param>
/// <param name="d">The fourth vertex.</param>
public readonly struct Tetrahedron(Vector4 a, Vector4 b, Vector4 c, Vector4 d)
{
    /// <summary>The first vertex.</summary>
    public Vector4 A { get; } = a;

    /// <summary>The second vertex.</summary>
    public Vector4 B { get; } = b;

    /// <summary>The third vertex.</summary>
    public Vector4 C { get; } = c;

    /// <summary>The fourth vertex.</summary>
    public Vector4 D { get; } = d;
}
