using System.Numerics;
using Tap4D.Maths;

namespace Tap4D.Scene;

/// <summary>
/// A 4D camera: position, orientation, focal distance and near plane.
/// In camera space the camera looks along the Z axis, so depth is Z and ana-kata is W.
/// </summary>
public class Camera4
{
    /// <summary>The camera position in world coordinates.</summary>
    public Vector4 Position { get; set; }

    /// <summary>
    /// The camera orientation: a rotation matrix whose rows are the camera axes in world coordinates.
    /// </summary>
    public Matrix4x4 Orientation { get; set; } = Matrix4x4.Identity;

    /// <summary>The distance from the camera to the virtual 3D screen along the Z axis.</summary>
    public float Focal { get; set; } = 2f;

    /// <summary>The distance from the camera to the near clipping plane.</summary>
    public float Near { get; set; } = 0.02f;

    /// <summary>
    /// Moves the camera along its own axes (e.g. a delta of (0, 0, 1, 0) moves it forward).
    /// </summary>
    /// <param name="delta">The movement in camera-local coordinates.</param>
    public void MoveLocal(Vector4 delta) => Position += Vector4.Transform(delta, Orientation);

    /// <summary>
    /// Rotates the camera in one of its local coordinate planes.
    /// </summary>
    /// <param name="plane">The camera-local plane to rotate in.</param>
    /// <param name="angle">The rotation angle in radians.</param>
    public void Rotate(RotationPlane plane, float angle) =>
        Orientation = Rotation4.Create(plane, angle) * Orientation;
}
