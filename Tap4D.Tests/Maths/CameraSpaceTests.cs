using System.Numerics;
using Tap4D.Maths;

namespace Tap4D.Tests.Maths;

/// <summary>
/// Tests for <see cref="CameraSpace"/>.
/// </summary>
public class CameraSpaceTests
{
    /// <summary>
    /// Verifies that an unrotated camera at the origin leaves points unchanged.
    /// </summary>
    [Fact]
    public void IdentityCameraLeavesPointUnchanged()
    {
        Vector4 point = new(1, 2, 3, 4);
        Vector4 result = CameraSpace.ToCamera(point, Vector4.Zero, Matrix4x4.Identity);
        Vector4Assert.Equal(point, result);
    }

    /// <summary>
    /// Verifies that camera translation shifts points by the negated camera position.
    /// </summary>
    [Fact]
    public void TranslatedCameraSubtractsPosition()
    {
        Vector4 point = new(1, 2, 3, 4);
        Vector4 position = new(1, 1, 1, 1);
        Vector4 result = CameraSpace.ToCamera(point, position, Matrix4x4.Identity);
        Vector4Assert.Equal(new Vector4(0, 1, 2, 3), result);
    }

    /// <summary>
    /// Verifies that a camera rotated 90 degrees in the XW plane maps world X to local -W.
    /// </summary>
    [Fact]
    public void RotatedCameraMapsWorldAxesToLocalAxes()
    {
        Matrix4x4 orientation = Rotation4.Create(RotationPlane.XW, MathF.PI / 2);
        Vector4 worldX = new(1, 0, 0, 0);
        Vector4 local = CameraSpace.ToCamera(worldX, Vector4.Zero, orientation);
        Vector4Assert.Equal(new Vector4(0, 0, 0, -1), local);
    }

    /// <summary>
    /// Verifies that transforming to camera space and back returns the original point.
    /// </summary>
    [Fact]
    public void ToCameraAndBackIsIdentity()
    {
        Matrix4x4 orientation = Rotation4.Create(RotationPlane.YZ, 0.6f);
        Vector4 position = new(5, -3, 2, 1);
        Vector4 point = new(1, 2, 3, 4);
        Vector4 roundTrip = CameraSpace.ToWorld(CameraSpace.ToCamera(point, position, orientation), position, orientation);
        Vector4Assert.Equal(point, roundTrip);
    }
}
