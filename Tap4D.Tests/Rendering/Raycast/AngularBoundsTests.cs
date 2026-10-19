using System.Numerics;
using Tap4D.Geometry;
using Tap4D.Rendering.Raycast;

namespace Tap4D.Tests.Rendering.Raycast;

/// <summary>
/// Tests for <see cref="AngularBounds"/>.
/// </summary>
public class AngularBoundsTests
{
    /// <summary>
    /// Verifies the edge-interior extremum: on the segment from (1,0,0,1) to (0,0,1,0.5),
    /// the angular W coordinate peaks at t = 1/3 with 2·(5/6)/√1.25.
    /// </summary>
    [Fact]
    public void EdgeInteriorExtremum()
    {
        Segment4 segment = new(new Vector4(1, 0, 0, 1), new Vector4(0, 0, 1, 0.5f), 0.01f);
        (float lo, float hi) = AngularBounds.WRange(segment, 2);
        Assert.Equal(2 * (5f / 6f) / MathF.Sqrt(1.25f), hi, 1e-4f);
        Assert.Equal(2 * 0.5f / MathF.Sqrt(1.25f), lo, 1e-4f);
    }

    /// <summary>
    /// Verifies the face-interior extremum: the face ABC has its perpendicular foot at
    /// (0,0,0,1), where the angular W coordinate is exactly the focal distance.
    /// </summary>
    [Fact]
    public void FaceInteriorExtremum()
    {
        Tetrahedron tetrahedron = new(
            new Vector4(1, 0, 0, 1),
            new Vector4(-1, 0, 0, 1),
            new Vector4(0, 1, 0, 1),
            new Vector4(0, 0, 1, 0.5f));
        (float _, float hi) = AngularBounds.WRange(tetrahedron, 2);
        Assert.Equal(2f, hi, 1e-4f);
    }

    /// <summary>
    /// Verifies that the range of a tesseract tetrahedron contains the angular W coordinate
    /// of every sampled interior point.
    /// </summary>
    [Fact]
    public void RangeContainsSampledInteriorPoints()
    {
        Tetrahedron tetrahedron = Tesseract.Tetrahedra()[0];
        (float lo, float hi) = AngularBounds.WRange(tetrahedron, 2);
        Random random = new(42);
        for (int i = 0; i < 1000; i++)
        {
            float a = random.NextSingle();
            float b = random.NextSingle() * (1 - a);
            float c = random.NextSingle() * (1 - a - b);
            Vector4 p = tetrahedron.A * a + tetrahedron.B * b + tetrahedron.C * c + tetrahedron.D * (1 - a - b - c);
            float h = 2 * p.W / p.Length();
            Assert.True(h >= lo - 1e-4f && h <= hi + 1e-4f, $"Point {p} has angular W {h} outside [{lo}, {hi}]");
        }
    }
}
