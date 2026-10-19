using System.Numerics;
using Tap4D.Rendering.Raycast;

namespace Tap4D.Tests.Rendering.Raycast;

/// <summary>
/// Tests for <see cref="RaySegmentIntersection"/> (cylinder semantics, world-space radius).
/// The sample segment runs along W at X = 0.5, Z = 4.
/// </summary>
public class RaySegmentIntersectionTests
{
    private static readonly Vector4 Start = new(0.5f, 0, 4, -1);
    private static readonly Vector4 End = new(0.5f, 0, 4, 1);
    private const float Radius = 0.05f;

    /// <summary>
    /// Verifies that a ray straight at the segment hits at the axis distance.
    /// </summary>
    [Fact]
    public void RayThroughAxisHits()
    {
        Vector4 direction = Vector4.Normalize(new Vector4(0.5f, 0, 4, 0));
        bool hit = RaySegmentIntersection.TryIntersect(Start, End, Radius, direction, 0.1f, 1f, 0f, out float depth);
        Assert.True(hit);
        Assert.Equal(MathF.Sqrt(16.25f), depth, 1e-4f);
    }

    /// <summary>
    /// Verifies a hit within the radius: the ray passes ~0.04 world units from the axis.
    /// </summary>
    [Fact]
    public void RayWithinRadiusHits()
    {
        Vector4 direction = Vector4.Normalize(new Vector4(0.54f, 0, 4, 0));
        bool hit = RaySegmentIntersection.TryIntersect(Start, End, Radius, direction, 0.1f, 1f, 0f, out _);
        Assert.True(hit);
    }

    /// <summary>
    /// Verifies a miss beyond the radius: the ray passes ~0.2 world units from the axis.
    /// </summary>
    [Fact]
    public void RayBeyondRadiusMisses()
    {
        Vector4 direction = Vector4.Normalize(new Vector4(0.7f, 0, 4, 0));
        bool hit = RaySegmentIntersection.TryIntersect(Start, End, Radius, direction, 0.1f, 1f, 0f, out _);
        Assert.False(hit);
    }

    /// <summary>
    /// Verifies the ana-kata anisotropy: the ray misses the segment's XZ projection exactly
    /// and is 0.10 world units away only in W, so with scale 1 it misses (radius 0.05),
    /// but with scale 0.25 the W delta shrinks to 0.025 and it hits.
    /// </summary>
    [Fact]
    public void AnisotropyWidensInW()
    {
        Vector4 start = new(0.3f, 0, 4, 0);
        Vector4 end = new(0.7f, 0, 4, 0);
        Vector4 direction = Vector4.Normalize(new Vector4(0.5f, 0, 4, 0.1f));
        Assert.False(RaySegmentIntersection.TryIntersect(start, end, Radius, direction, 0.1f, 1f, 0f, out _));
        Assert.True(RaySegmentIntersection.TryIntersect(start, end, Radius, direction, 0.1f, 0.25f, 0f, out _));
    }

    /// <summary>
    /// Verifies the voxel footprint inflation: the ray passes ~0.08 world units from the axis,
    /// beyond the radius 0.05, but the footprint of 0.01 per unit of depth (~0.04 at the hit
    /// distance) widens the tube enough to hit.
    /// </summary>
    [Fact]
    public void FootprintWidensWithDistance()
    {
        Vector4 direction = Vector4.Normalize(new Vector4(0.58f, 0, 4, 0));
        Assert.False(RaySegmentIntersection.TryIntersect(Start, End, Radius, direction, 0.1f, 1f, 0f, out _));
        Assert.True(RaySegmentIntersection.TryIntersect(Start, End, Radius, direction, 0.1f, 1f, 0.01f, out _));
    }
}
