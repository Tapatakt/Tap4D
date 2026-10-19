using System.Numerics;
using System.Windows.Input;
using Tap4D.Scene;
using Tap4D.Viewer;

namespace Tap4D.Tests.Viewer;

/// <summary>
/// Tests for <see cref="FpsCameraController"/>.
/// </summary>
public class FpsCameraControllerTests
{
    /// <summary>
    /// Verifies that holding W moves the camera forward along Z.
    /// </summary>
    [Fact]
    public void HoldingWMovesForward()
    {
        FpsCameraController controller = new();
        Camera4 camera = new();
        controller.Press(Key.W);
        for (int i = 0; i < 10; i++)
            controller.Update(camera, 0.1f);
        Assert.True(camera.Position.Z > 0.9f, $"Expected forward movement, got {camera.Position}");
    }

    /// <summary>
    /// Verifies that the camera stops moving after the key is released.
    /// </summary>
    [Fact]
    public void ReleaseStopsMovement()
    {
        FpsCameraController controller = new();
        Camera4 camera = new();
        controller.Press(Key.W);
        controller.Update(camera, 0.1f);
        controller.Release(Key.W);
        float z = camera.Position.Z;
        controller.Update(camera, 0.1f);
        Assert.Equal(z, camera.Position.Z, 1e-5f);
    }

    /// <summary>
    /// Verifies that after an XW flip of ~180 degrees a rightward mouse motion still turns
    /// the view toward screen right (yaw is camera-local, not objective: with the objective
    /// XZ yaw the XW flip made the mouse look inverted).
    /// </summary>
    [Fact]
    public void MouseTurnsTowardScreenRightAfterXwFlip()
    {
        FpsCameraController controller = new();
        Camera4 camera = new();
        for (int i = 0; i < 16; i++)
            controller.OnWheel(120);
        controller.Update(camera, 0.01f);
        Vector4 forwardBefore = Vector4.Transform(Vector4.UnitZ, camera.Orientation);
        Vector4 right = Vector4.Transform(Vector4.UnitX, camera.Orientation);
        controller.OnMouseDelta(100, 0);
        controller.Update(camera, 0.01f);
        Vector4 forwardAfter = Vector4.Transform(Vector4.UnitZ, camera.Orientation);
        float sideways = Vector4.Dot(forwardAfter - forwardBefore, right);
        Assert.True(sideways > 0, $"Mouse right must turn toward screen right, sideways={sideways}");
    }
}
