using System.Numerics;

namespace Tap4D.Maths;

/// <summary>
/// Transforms points between world space and the coordinate system of a camera.
/// The camera looks along its local Z axis, so depth is Z and ana-kata is W in camera space.
/// </summary>
public static class CameraSpace
{
    /// <summary>
    /// Transforms a world-space point into camera space.
    /// </summary>
    /// <param name="point">The point in world coordinates.</param>
    /// <param name="cameraPosition">The camera position in world coordinates.</param>
    /// <param name="cameraOrientation">
    /// The camera orientation: a rotation matrix whose rows are the camera axes in world coordinates.
    /// </param>
    /// <returns>The point in camera coordinates.</returns>
    public static Vector4 ToCamera(Vector4 point, Vector4 cameraPosition, Matrix4x4 cameraOrientation) =>
        Vector4.Transform(point - cameraPosition, Matrix4x4.Transpose(cameraOrientation));

    /// <summary>
    /// Transforms a camera-space point back into world space.
    /// </summary>
    /// <param name="point">The point in camera coordinates.</param>
    /// <param name="cameraPosition">The camera position in world coordinates.</param>
    /// <param name="cameraOrientation">
    /// The camera orientation: a rotation matrix whose rows are the camera axes in world coordinates.
    /// </param>
    /// <returns>The point in world coordinates.</returns>
    public static Vector4 ToWorld(Vector4 point, Vector4 cameraPosition, Matrix4x4 cameraOrientation) =>
        Vector4.Transform(point, cameraOrientation) + cameraPosition;
}
