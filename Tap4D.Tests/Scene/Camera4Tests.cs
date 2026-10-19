using System.Numerics;
using Tap4D.Maths;
using Tap4D.Scene;

namespace Tap4D.Tests.Scene;

/// <summary>
/// Tests for <see cref="Camera4"/>.
/// </summary>
public class Camera4Tests
{
    /// <summary>
    /// Verifies that an unrotated camera moves along the world axes.
    /// </summary>
    [Fact]
    public void UnrotatedCameraMovesAlongWorldAxes()
    {
        Camera4 camera = new();
        camera.MoveLocal(new Vector4(1, 2, 3, 4));
        Vector4Assert.Equal(new Vector4(1, 2, 3, 4), camera.Position);
    }

    /// <summary>
    /// Verifies that a camera rotated 90 degrees in the XW plane moves along world W
    /// when moved along its local X axis.
    /// </summary>
    [Fact]
    public void RotatedCameraMovesAlongLocalAxes()
    {
        Camera4 camera = new();
        camera.Rotate(RotationPlane.XW, MathF.PI / 2);
        camera.MoveLocal(Vector4.UnitX);
        Vector4Assert.Equal(Vector4.UnitW, camera.Position);
    }

    /// <summary>
    /// Verifies that two rotations by 90 degrees in the XZ plane turn the camera around:
    /// moving forward along local Z then moves it along world -Z.
    /// </summary>
    [Fact]
    public void Two90DegreeRotationsTurnCameraAround()
    {
        Camera4 camera = new();
        camera.Rotate(RotationPlane.XZ, MathF.PI / 2);
        camera.Rotate(RotationPlane.XZ, MathF.PI / 2);
        camera.MoveLocal(Vector4.UnitZ);
        Vector4Assert.Equal(-Vector4.UnitZ, camera.Position);
    }
}
