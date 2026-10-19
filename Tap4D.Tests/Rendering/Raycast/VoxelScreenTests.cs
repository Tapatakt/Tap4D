using System.Numerics;
using Tap4D.Rendering.Raycast;

namespace Tap4D.Tests.Rendering.Raycast;

/// <summary>
/// Tests for <see cref="VoxelScreen"/>.
/// </summary>
public class VoxelScreenTests
{
    private static readonly VoxelScreen Screen = new(32, 32, 2, 2, 8, -2, 2);

    /// <summary>
    /// Verifies that the center of the screen at zero ana-kata points straight ahead.
    /// </summary>
    [Fact]
    public void CenterDirectionIsStraightAhead()
    {
        bool ok = Screen.TryGetDirection(0, 0, 0, out Vector4 direction);
        Assert.True(ok);
        Vector4Assert.Equal(Vector4.UnitZ, direction);
    }

    /// <summary>
    /// Verifies that the ray direction is unit and its sphere coordinates reproduce
    /// the requested screen coordinates.
    /// </summary>
    [Fact]
    public void DirectionIsUnitAndMatchesCoordinates()
    {
        bool ok = Screen.TryGetDirection(0.3f, -0.4f, 0.5f, out Vector4 direction);
        Assert.True(ok);
        Assert.Equal(1f, direction.Length(), 1e-5f);
        Assert.Equal(0.3f, direction.X * Screen.Focal, 1e-5f);
        Assert.Equal(-0.4f, direction.Y * Screen.Focal, 1e-5f);
        Assert.Equal(0.5f, direction.W * Screen.Focal, 1e-5f);
    }

    /// <summary>
    /// Verifies that coordinates beyond the front hemisphere have no ray.
    /// </summary>
    [Fact]
    public void BeyondHemisphereRejected()
    {
        Assert.False(Screen.TryGetDirection(1.95f, 0, 0.5f, out _));
        Assert.True(Screen.TryGetDirection(1.9f, 0, 0, out _));
    }

    /// <summary>
    /// Verifies that sub-rays are stratified across the slice.
    /// </summary>
    [Fact]
    public void SubRaysStratifySlice()
    {
        float width = Screen.SliceWidth;
        Assert.Equal(Screen.MinW + 0.25f * width, Screen.SliceW(0, 0, 2), 1e-5f);
        Assert.Equal(Screen.MinW + 0.75f * width, Screen.SliceW(0, 1, 2), 1e-5f);
        Assert.Equal(Screen.MinW + 2.5f * width, Screen.SliceW(2, 0, 1), 1e-5f);
    }
}
