using System.Numerics;

namespace Tap4D.Geometry;

/// <summary>
/// A line segment in 4D space, rendered as a cylinder around it.
/// </summary>
/// <param name="start">The first endpoint.</param>
/// <param name="end">The second endpoint.</param>
/// <param name="radius">The cylinder radius in world units.</param>
public readonly struct Segment4(Vector4 start, Vector4 end, float radius)
{
    /// <summary>The first endpoint.</summary>
    public Vector4 Start { get; } = start;

    /// <summary>The second endpoint.</summary>
    public Vector4 End { get; } = end;

    /// <summary>The cylinder radius in world units.</summary>
    public float Radius { get; } = radius;
}
