using Tap4D.Scene;

namespace Tap4D.Tests.Scene;

/// <summary>
/// Tests for <see cref="AnaKataPalette"/>.
/// </summary>
public class AnaKataPaletteTests
{
    /// <summary>
    /// Verifies that the minimum ana-kata maps to pure red and the maximum to pure blue.
    /// </summary>
    [Fact]
    public void RangeEndsMapToRedAndBlue()
    {
        AnaKataPalette palette = new();
        Assert.Equal((255, 0, 0), palette.ColorFor(palette.MinW));
        Assert.Equal((0, 0, 255), palette.ColorFor(palette.MaxW));
    }

    /// <summary>
    /// Verifies that values outside the palette range are clamped.
    /// </summary>
    [Fact]
    public void OutOfRangeIsClamped()
    {
        AnaKataPalette palette = new();
        Assert.Equal((255, 0, 0), palette.ColorFor(-100));
        Assert.Equal((0, 0, 255), palette.ColorFor(100));
    }

    /// <summary>
    /// Verifies that the green channel is taken from the palette.
    /// </summary>
    [Fact]
    public void GreenChannelIsConfigurable()
    {
        AnaKataPalette palette = new() { Green = 10 };
        Assert.Equal(10, palette.ColorFor(0).G);
    }
}
