using System.Numerics;
using Tap4D.Rendering;

namespace Tap4D.Tests.Rendering;

/// <summary>
/// Tests for <see cref="SegmentPixelIntersection"/> (cylinder semantics, world-space radius).
/// </summary>
public class SegmentPixelIntersectionTests
{
    /// <summary>
    /// Verifies a hit on a segment crossing the pixel's ray plane: the segment runs along X
    /// at Z = 4, and pixel (0.05, 0) with focal 2 corresponds to X = 0.1 at that depth,
    /// crossed at t = 0.5.
    /// </summary>
    [Fact]
    public void HitOnAxis()
    {
        Vector4 start = new(0.3f, 0, 4, 0);
        Vector4 end = new(-0.1f, 0, 4, 0);
        bool hit = SegmentPixelIntersection.TryIntersect(start, end, 0.01f, 0.05f, 0, 2, out float depth, out float screenW);
        Assert.True(hit);
        Assert.Equal(4f, depth, 1e-4f);
        Assert.Equal(0f, screenW, 1e-4f);
    }

    /// <summary>
    /// Verifies a hit within the radius: the segment runs along W at X = 0.2, Z = 4;
    /// pixel (0.104, 0) passes 0.008 world units from it.
    /// </summary>
    [Fact]
    public void HitWithinRadius()
    {
        Vector4 start = new(0.2f, 0, 4, -1);
        Vector4 end = new(0.2f, 0, 4, 1);
        bool hit = SegmentPixelIntersection.TryIntersect(start, end, 0.01f, 0.104f, 0, 2, out float depth, out float screenW);
        Assert.True(hit);
        Assert.Equal(4f, depth, 1e-4f);
        Assert.Equal(-0.5f, screenW, 1e-4f);
    }

    /// <summary>
    /// Verifies a miss just outside the radius: pixel (0.106, 0) passes 0.012 world units away.
    /// </summary>
    [Fact]
    public void MissBeyondRadius()
    {
        Vector4 start = new(0.2f, 0, 4, -1);
        Vector4 end = new(0.2f, 0, 4, 1);
        bool hit = SegmentPixelIntersection.TryIntersect(start, end, 0.01f, 0.106f, 0, 2, out _, out _);
        Assert.False(hit);
    }

    /// <summary>
    /// Verifies that the per-segment radius is respected: with radius 0.001 the pixel
    /// 0.008 world units away misses.
    /// </summary>
    [Fact]
    public void SmallerRadiusMisses()
    {
        Vector4 start = new(0.2f, 0, 4, -1);
        Vector4 end = new(0.2f, 0, 4, 1);
        bool hit = SegmentPixelIntersection.TryIntersect(start, end, 0.001f, 0.104f, 0, 2, out _, out _);
        Assert.False(hit);
    }

    /// <summary>
    /// Verifies that perspective makes the same world radius cover more screen when closer:
    /// two segments projecting to the same screen position (X/Z equal), at Z = 2 and Z = 4;
    /// pixel (0.108, 0) is 0.008 world units from the nearer one (hit) and 0.016 from
    /// the farther one (miss).
    /// </summary>
    [Fact]
    public void RadiusScalesWithPerspective()
    {
        Vector4 nearStart = new(0.1f, 0, 2, -1);
        Vector4 nearEnd = new(0.1f, 0, 2, 1);
        Vector4 farStart = new(0.2f, 0, 4, -1);
        Vector4 farEnd = new(0.2f, 0, 4, 1);
        Assert.True(SegmentPixelIntersection.TryIntersect(nearStart, nearEnd, 0.01f, 0.108f, 0, 2, out _, out _));
        Assert.False(SegmentPixelIntersection.TryIntersect(farStart, farEnd, 0.01f, 0.108f, 0, 2, out _, out _));
    }
}
