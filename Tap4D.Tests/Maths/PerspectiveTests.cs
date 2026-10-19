using System.Numerics;
using Tap4D.Maths;

namespace Tap4D.Tests.Maths;

/// <summary>
/// Tests for <see cref="Perspective"/>.
/// </summary>
public class PerspectiveTests
{
    /// <summary>
    /// Verifies the perspective division of a known point.
    /// </summary>
    [Fact]
    public void ProjectDividesByDepth()
    {
        Vector4 point = new(2, 3, 4, 5);
        Vector4 projected = Perspective.Project(point, 2);
        Vector4Assert.Equal(new Vector4(1, 1.5f, 4, 2.5f), projected);
    }

    /// <summary>
    /// Verifies that a point lying on the screen plane (Z = focal) keeps its X, Y and W.
    /// </summary>
    [Fact]
    public void PointOnScreenPlaneMapsToItself()
    {
        Vector4 point = new(2, 3, 2, -1);
        Vector4 projected = Perspective.Project(point, 2);
        Vector4Assert.Equal(new Vector4(2, 3, 2, -1), projected);
    }
}
