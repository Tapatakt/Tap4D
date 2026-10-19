using System.Numerics;
using Tap4D.Geometry;
using Tap4D.Maths;

namespace Tap4D.Tests.Maths;

/// <summary>
/// Tests for <see cref="TetrahedronClipper"/>.
/// </summary>
public class TetrahedronClipperTests
{
    private static readonly Tetrahedron Sample = new(
        new Vector4(0, 0, 2, 0),
        new Vector4(1, 0, 0, 1),
        new Vector4(0, 1, 3, -1),
        new Vector4(0.5f, -1, 1, 2));

    /// <summary>
    /// Verifies that a tetrahedron fully in front of the near plane is returned unchanged.
    /// </summary>
    [Fact]
    public void FullyInFrontReturnsOriginal()
    {
        List<Tetrahedron> result = TetrahedronClipper.ClipNearPlane(Sample, -0.5f);
        Assert.Single(result);
        Vector4Assert.Equal(Sample.A, result[0].A);
        Vector4Assert.Equal(Sample.D, result[0].D);
    }

    /// <summary>
    /// Verifies that a tetrahedron fully behind the near plane yields nothing.
    /// </summary>
    [Fact]
    public void FullyBehindReturnsEmpty()
    {
        List<Tetrahedron> result = TetrahedronClipper.ClipNearPlane(Sample, 4f);
        Assert.Empty(result);
    }

    /// <summary>
    /// Verifies clipping against a half-space with an arbitrary normal (X &gt;= 0.2):
    /// two vertices inside produce three pieces, all inside the half-space.
    /// </summary>
    [Fact]
    public void HalfSpaceClipWithArbitraryNormal()
    {
        List<Tetrahedron> result = TetrahedronClipper.ClipHalfSpace(Sample, Vector4.UnitX, 0.2f);
        Assert.Equal(3, result.Count);
        foreach (Tetrahedron piece in result)
        foreach (Vector4 v in new[] { piece.A, piece.B, piece.C, piece.D })
            Assert.True(v.X >= 0.2f - 1e-5f, $"Vertex {v} outside the half-space");
    }

    /// <summary>
    /// Verifies that with one vertex inside, the result is a single tetrahedron
    /// whose three new vertices lie exactly on the near plane.
    /// </summary>
    [Fact]
    public void OneInsideReturnsSingleTetrahedronOnPlane()
    {
        List<Tetrahedron> result = TetrahedronClipper.ClipNearPlane(Sample, 2.5f);
        Tetrahedron clipped = Assert.Single(result);
        Vector4Assert.Equal(Sample.C, clipped.A);
        Assert.Equal(2.5f, clipped.B.Z, 1e-5f);
        Assert.Equal(2.5f, clipped.C.Z, 1e-5f);
        Assert.Equal(2.5f, clipped.D.Z, 1e-5f);
    }

    /// <summary>
    /// Verifies the kept volume with one vertex inside: the kept tetrahedron is scaled
    /// by the edge fraction along each of the three edges from the inside vertex.
    /// </summary>
    [Fact]
    public void OneInsideVolumeMatchesEdgeFractions()
    {
        List<Tetrahedron> pieces = TetrahedronClipper.ClipNearPlane(Sample, 2.5f);
        float expected = SimplexVolume.Of(Sample) * 0.5f * (0.5f / 3f) * 0.25f;
        Assert.Equal(expected, pieces.Sum(SimplexVolume.Of), 1e-4f);
    }

    /// <summary>
    /// Verifies the kept volume with three vertices inside: the removed piece is a tetrahedron
    /// scaled by the edge fraction along each of the three edges from the outside vertex.
    /// </summary>
    [Fact]
    public void ThreeInsideVolumeMatchesEdgeFractions()
    {
        List<Tetrahedron> pieces = TetrahedronClipper.ClipNearPlane(Sample, 0.9f);
        Assert.Equal(3, pieces.Count);
        float expected = SimplexVolume.Of(Sample) * (1f - 0.45f * 0.3f * 0.9f);
        Assert.Equal(expected, pieces.Sum(SimplexVolume.Of), 1e-4f);
    }

    /// <summary>
    /// Verifies the kept volume with two vertices inside by computing the removed wedge
    /// independently, fanning from the outside vertex B.
    /// </summary>
    [Fact]
    public void TwoInsideVolumeMatchesComplement()
    {
        const float near = 1.5f;
        List<Tetrahedron> pieces = TetrahedronClipper.ClipNearPlane(Sample, near);
        Assert.Equal(3, pieces.Count);
        float removed = RemovedWedgeVolume(near);
        float expected = SimplexVolume.Of(Sample) - removed;
        Assert.Equal(expected, pieces.Sum(SimplexVolume.Of), 1e-4f);
    }

    /// <summary>
    /// Computes the volume of the part of the sample tetrahedron behind the near plane
    /// (vertices B and D are outside) as three tetrahedra fanned from vertex B.
    /// </summary>
    /// <param name="near">The near plane distance.</param>
    /// <returns>The volume of the removed wedge.</returns>
    private static float RemovedWedgeVolume(float near)
    {
        Vector4 pAB = IntersectEdge(Sample.A, Sample.B, near);
        Vector4 pAD = IntersectEdge(Sample.A, Sample.D, near);
        Vector4 pCB = IntersectEdge(Sample.C, Sample.B, near);
        Vector4 pCD = IntersectEdge(Sample.C, Sample.D, near);
        return SimplexVolume.Of(new(Sample.B, Sample.D, pAD, pCD))
             + SimplexVolume.Of(new(Sample.B, pAB, pAD, pCD))
             + SimplexVolume.Of(new(Sample.B, pAB, pCD, pCB));
    }

    /// <summary>
    /// Finds the point where the edge between two vertices crosses the plane Z = near.
    /// </summary>
    /// <param name="first">The first vertex.</param>
    /// <param name="second">The second vertex.</param>
    /// <param name="near">The plane distance.</param>
    /// <returns>The intersection point on the plane.</returns>
    private static Vector4 IntersectEdge(Vector4 first, Vector4 second, float near)
    {
        float t = (first.Z - near) / (first.Z - second.Z);
        return first + t * (second - first);
    }
}

/// <summary>
/// Computes the 3D volume of a tetrahedron embedded in 4D space.
/// </summary>
internal static class SimplexVolume
{
    /// <summary>
    /// Computes the volume via the Gram determinant of the three edge vectors from vertex A.
    /// </summary>
    /// <param name="tetrahedron">The tetrahedron.</param>
    /// <returns>The 3D volume of the tetrahedron.</returns>
    public static float Of(Tetrahedron tetrahedron)
    {
        Vector4 e1 = tetrahedron.B - tetrahedron.A;
        Vector4 e2 = tetrahedron.C - tetrahedron.A;
        Vector4 e3 = tetrahedron.D - tetrahedron.A;
        float a = Vector4.Dot(e1, e1);
        float b = Vector4.Dot(e1, e2);
        float c = Vector4.Dot(e1, e3);
        float d = Vector4.Dot(e2, e2);
        float e = Vector4.Dot(e2, e3);
        float f = Vector4.Dot(e3, e3);
        float determinant = a * (d * f - e * e) - b * (b * f - c * e) + c * (b * e - c * d);
        return MathF.Sqrt(determinant) / 6f;
    }
}
