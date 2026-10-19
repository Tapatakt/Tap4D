using System.Numerics;

namespace Tap4D.Maths;

/// <summary>
/// The six coordinate planes in which a 4D rotation can occur.
/// </summary>
public enum RotationPlane
{
    /// <summary>The plane spanned by the X and Y axes.</summary>
    XY,

    /// <summary>The plane spanned by the X and Z axes.</summary>
    XZ,

    /// <summary>The plane spanned by the X and W axes.</summary>
    XW,

    /// <summary>The plane spanned by the Y and Z axes.</summary>
    YZ,

    /// <summary>The plane spanned by the Y and W axes.</summary>
    YW,

    /// <summary>The plane spanned by the Z and W axes.</summary>
    ZW,
}

/// <summary>
/// Creates rotation matrices for the six planes of 4D space.
/// </summary>
public static class Rotation4
{
    /// <summary>
    /// Creates a matrix rotating by <paramref name="angle"/> radians in the given plane,
    /// leaving the other two axes unchanged.
    /// </summary>
    /// <param name="plane">The coordinate plane to rotate in.</param>
    /// <param name="angle">The rotation angle in radians.</param>
    /// <returns>A 4x4 rotation matrix for use with <see cref="Vector4.Transform(Vector4, Matrix4x4)"/>.</returns>
    public static Matrix4x4 Create(RotationPlane plane, float angle)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        (int first, int second) = PlaneIndices(plane);
        float[] m = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
        m[first * 4 + first] = cos;
        m[first * 4 + second] = sin;
        m[second * 4 + first] = -sin;
        m[second * 4 + second] = cos;
        return new(
            m[0], m[1], m[2], m[3],
            m[4], m[5], m[6], m[7],
            m[8], m[9], m[10], m[11],
            m[12], m[13], m[14], m[15]);
    }

    /// <summary>
    /// Returns the two axis indices spanned by a rotation plane.
    /// </summary>
    /// <param name="plane">The rotation plane.</param>
    /// <returns>The indices of the two axes of the plane.</returns>
    private static (int First, int Second) PlaneIndices(RotationPlane plane) => plane switch
    {
        RotationPlane.XY => (0, 1),
        RotationPlane.XZ => (0, 2),
        RotationPlane.XW => (0, 3),
        RotationPlane.YZ => (1, 2),
        RotationPlane.YW => (1, 3),
        RotationPlane.ZW => (2, 3),
        _ => throw new ArgumentOutOfRangeException(nameof(plane)),
    };
}
