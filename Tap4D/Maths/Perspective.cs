using System.Numerics;

namespace Tap4D.Maths;

/// <summary>
/// Perspective projection from 4D camera space onto the three-dimensional screen at Z = focal.
/// </summary>
public static class Perspective
{
    /// <summary>
    /// Projects a camera-space point onto the virtual 3D screen.
    /// </summary>
    /// <param name="cameraSpacePoint">The point in camera coordinates; must have Z &gt; 0.</param>
    /// <param name="focal">The distance from the camera to the screen along the Z axis.</param>
    /// <returns>
    /// (screenX, screenY, depth, screenW), where screenX = focal * X / Z,
    /// screenY = focal * Y / Z, depth = Z and screenW = focal * W / Z (ana-kata on the screen).
    /// </returns>
    public static Vector4 Project(Vector4 cameraSpacePoint, float focal)
    {
        float scale = focal / cameraSpacePoint.Z;
        return new(
            cameraSpacePoint.X * scale,
            cameraSpacePoint.Y * scale,
            cameraSpacePoint.Z,
            cameraSpacePoint.W * scale);
    }
}
