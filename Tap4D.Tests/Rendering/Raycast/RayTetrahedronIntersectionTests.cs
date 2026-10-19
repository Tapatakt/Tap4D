using System.Numerics;
using Tap4D.Geometry;
using Tap4D.Rendering.Raycast;

namespace Tap4D.Tests.Rendering.Raycast;

/// <summary>
/// Tests for <see cref="RayTetrahedronIntersection"/>.
/// </summary>
public class RayTetrahedronIntersectionTests
{
    private static readonly Tetrahedron Sample = new(
        new Vector4(0, 0, 2, 0),
        new Vector4(1, 0, 2, 0),
        new Vector4(0, 1, 2, 0),
        new Vector4(0, 0, 2, 1));

    /// <summary>
    /// Verifies that a ray through the centroid hits at the centroid's distance.
    /// </summary>
    [Fact]
    public void CentroidRayHits()
    {
        Vector4 centroid = new(0.25f, 0.25f, 2, 0.25f);
        Vector4 direction = Vector4.Normalize(centroid);
        bool hit = RayTetrahedronIntersection.TryIntersect(Sample, direction, 0.1f, out float depth);
        Assert.True(hit);
        Assert.Equal(centroid.Length(), depth, 1e-4f);
    }

    /// <summary>
    /// Verifies that a ray whose W at the tetra's depth is beyond the tetra misses.
    /// </summary>
    [Fact]
    public void RayOutsideInWMisses()
    {
        Vector4 direction = Vector4.Normalize(new Vector4(0.25f, 0.25f, 2, 5));
        bool hit = RayTetrahedronIntersection.TryIntersect(Sample, direction, 0.1f, out _);
        Assert.False(hit);
    }

    /// <summary>
    /// Verifies that a ray pointing away from the tetra misses.
    /// </summary>
    [Fact]
    public void BackwardRayMisses()
    {
        Vector4 direction = Vector4.Normalize(new Vector4(0.25f, 0.25f, -2, 0.25f));
        bool hit = RayTetrahedronIntersection.TryIntersect(Sample, direction, 0.1f, out _);
        Assert.False(hit);
    }

    /// <summary>
    /// Verifies that a hit closer than the near distance is rejected.
    /// </summary>
    [Fact]
    public void NearerThanNearMisses()
    {
        Vector4 direction = Vector4.Normalize(new Vector4(0.25f, 0.25f, 2, 0.25f));
        bool hit = RayTetrahedronIntersection.TryIntersect(Sample, direction, 3f, out _);
        Assert.False(hit);
    }
}
