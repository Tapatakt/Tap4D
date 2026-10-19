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
}
