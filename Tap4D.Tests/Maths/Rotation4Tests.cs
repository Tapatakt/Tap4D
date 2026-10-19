using System.Numerics;
using Tap4D.Maths;

namespace Tap4D.Tests.Maths;

/// <summary>
/// Tests for <see cref="Rotation4"/>.
/// </summary>
public class Rotation4Tests
{
    /// <summary>
    /// Verifies that rotating the X axis by 90 degrees in the XY plane yields the Y axis.
    /// </summary>
    [Fact]
    public void RotateXyBy90DegreesMapsXToY()
    {
        Matrix4x4 rotation = Rotation4.Create(RotationPlane.XY, MathF.PI / 2);
        Vector4 result = Vector4.Transform(Vector4.UnitX, rotation);
        Vector4Assert.Equal(Vector4.UnitY, result);
    }

    /// <summary>
    /// Verifies that a rotation affects only the two axes of its plane.
    /// </summary>
    [Fact]
    public void RotationLeavesOtherAxesUnchanged()
    {
        Matrix4x4 rotation = Rotation4.Create(RotationPlane.XW, 0.7f);
        Vector4Assert.Equal(Vector4.UnitY, Vector4.Transform(Vector4.UnitY, rotation));
        Vector4Assert.Equal(Vector4.UnitZ, Vector4.Transform(Vector4.UnitZ, rotation));
    }

    /// <summary>
    /// Verifies that rotations in all six planes preserve vector length.
    /// </summary>
    [Fact]
    public void RotationPreservesLength()
    {
        Vector4 v = new(1, -2, 3, 0.5f);
        foreach (RotationPlane plane in Enum.GetValues<RotationPlane>())
        {
            Vector4 rotated = Vector4.Transform(v, Rotation4.Create(plane, 1.1f));
            Assert.Equal(v.Length(), rotated.Length(), 1e-4f);
        }
    }

    /// <summary>
    /// Verifies that rotating by an angle and then by its negation returns the original vector.
    /// </summary>
    [Fact]
    public void RotationByAngleAndBackIsIdentity()
    {
        Vector4 v = new(1, -2, 3, 0.5f);
        Matrix4x4 forward = Rotation4.Create(RotationPlane.ZW, 0.9f);
        Matrix4x4 backward = Rotation4.Create(RotationPlane.ZW, -0.9f);
        Vector4 result = Vector4.Transform(Vector4.Transform(v, forward), backward);
        Vector4Assert.Equal(v, result);
    }
}
