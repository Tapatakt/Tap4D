using Tap4D.Rendering.Raycast;

namespace Tap4D.Tests.Rendering.Raycast;

/// <summary>
/// Tests for <see cref="ScreenTiling"/>.
/// </summary>
public class ScreenTilingTests
{
    /// <summary>
    /// Verifies that a primitive lands exactly in the tiles its angular bounds overlap:
    /// a top-right primitive goes to tile row 0, column 3 of a 4x4 grid, and a
    /// bottom-left one to row 3, column 0 (tile rows count down, angular Y counts up).
    /// </summary>
    [Fact]
    public void PrimitiveLandsInOverlappedTilesOnly()
    {
        AngularRanges[] ranges =
        [
            new((0.2f, 0.3f), (0.2f, 0.3f), (-1f, 1f)),
            new((-0.3f, -0.2f), (-0.3f, -0.2f), (-1f, 1f)),
        ];
        ScreenTiling tiling = ScreenTiling.Build(64, 64, 0.01f, 0f, 0f, ranges, [], []);
        Assert.Equal(4, tiling.TilesX);
        Assert.Equal(4, tiling.TilesY);
        Assert.Equal(2, tiling.VisibleIndices.Length);
        AssertCandidates(tiling, 3, 0);
        AssertCandidates(tiling, 12, 1);
    }

    /// <summary>
    /// Verifies that an empty scene produces zero counts everywhere.
    /// </summary>
    [Fact]
    public void EmptySceneHasEmptyTiles()
    {
        ScreenTiling tiling = ScreenTiling.Build(64, 64, 0.01f, 0f, 0f, [], [], []);
        Assert.All(tiling.VisibleOffsets, o => Assert.Equal(0, o));
        Assert.Empty(tiling.VisibleIndices);
    }

    /// <summary>
    /// Asserts that the given tile holds exactly the given primitive index.
    /// </summary>
    private static void AssertCandidates(ScreenTiling tiling, int tile, int expected)
    {
        int start = tiling.VisibleOffsets[tile];
        int end = tiling.VisibleOffsets[tile + 1];
        Assert.Equal(start + 1, end);
        Assert.Equal(expected, tiling.VisibleIndices[start]);
    }
}
