using System.Numerics;
using Tap4D.Geometry;
using Tap4D.Rendering;

namespace Tap4D.Tests.Rendering;

/// <summary>
/// Tests for <see cref="TetraPixelIntersection"/>.
/// </summary>
public class TetraPixelIntersectionTests
{
    private static readonly Tetrahedron Sample = new(
        new Vector4(-1, 0, 2, -2),
        new Vector4(1, 0, 6, 2),
        new Vector4(0, 1, 3, 4),
        new Vector4(0, -1, 4, 0));

    /// <summary>
    /// Verifies a worked example: the intersection segment at pixel (0, 0) runs from
    /// the midpoint of edge CD (depth 3.5, W = 2) to the midpoint of edge AB (depth 4),
    /// so the nearer endpoint has depth 3.5 and screenW = 2 / 3.5 = 4/7.
    /// </summary>
    [Fact]
    public void HitReturnsNearestEndpoint()
    {
        bool hit = TetraPixelIntersection.TryIntersect(Sample, 0, 0, 1, out float depth, out float screenW);
        Assert.True(hit);
        Assert.Equal(3.5f, depth, 1e-4f);
        Assert.Equal(4f / 7f, screenW, 1e-4f);
    }

    /// <summary>
    /// Verifies that a pixel far outside the tetrahedron projection misses.
    /// </summary>
    [Fact]
    public void FarPixelMisses()
    {
        bool hit = TetraPixelIntersection.TryIntersect(Sample, 10, 10, 1, out _, out _);
        Assert.False(hit);
    }
}
