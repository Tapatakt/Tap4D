namespace Tap4D.Rendering.Raycast;

/// <summary>
/// Screen-space tiling for primitive culling in the raycasters: every tile of the screen
/// holds the indices of the primitives whose angular X/Y bounds (expanded by a margin)
/// overlap it. Built on the CPU once per frame and shared by both raycast renderers;
/// replaces fixed-size candidate bitmasks, so scenes of any size are supported.
/// </summary>
public class ScreenTiling
{
    /// <summary>The tile edge length in pixels.</summary>
    public const int TileSize = 16;

    private ScreenTiling(int tilesX, int tilesY, int[] visibleOffsets, int[] visibleIndices,
        int[] occluderOffsets, int[] occluderIndices, int[] segmentOffsets, int[] segmentIndices)
    {
        TilesX = tilesX;
        TilesY = tilesY;
        VisibleOffsets = visibleOffsets;
        VisibleIndices = visibleIndices;
        OccluderOffsets = occluderOffsets;
        OccluderIndices = occluderIndices;
        SegmentOffsets = segmentOffsets;
        SegmentIndices = segmentIndices;
    }

    /// <summary>The number of tile columns.</summary>
    public int TilesX { get; }

    /// <summary>The number of tile rows.</summary>
    public int TilesY { get; }

    /// <summary>Prefix sums of the per-tile visible tetrahedra counts (length TilesX·TilesY + 1).</summary>
    public int[] VisibleOffsets { get; }

    /// <summary>The visible tetrahedra candidate indices, grouped by tile.</summary>
    public int[] VisibleIndices { get; }

    /// <summary>Prefix sums of the per-tile occluder tetrahedra counts (length TilesX·TilesY + 1).</summary>
    public int[] OccluderOffsets { get; }

    /// <summary>The occluder tetrahedra candidate indices, grouped by tile.</summary>
    public int[] OccluderIndices { get; }

    /// <summary>Prefix sums of the per-tile segment counts (length TilesX·TilesY + 1).</summary>
    public int[] SegmentOffsets { get; }

    /// <summary>The segment candidate indices, grouped by tile.</summary>
    public int[] SegmentIndices { get; }

    /// <summary>
    /// Builds the tiling for a frame. The margins are in angular (virtual screen)
    /// coordinates and must cover the ray footprint: the tetrahedra margin is half
    /// a pixel, the segment margin additionally covers the ana-kata slice half-width
    /// times the segment anisotropy (the fat-ray inflation divided by the hit distance
    /// and multiplied by the focal distance, which cancels out to a constant).
    /// </summary>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="pixelSize">The angular size of a pixel (ViewHeight / Height).</param>
    /// <param name="tetraMargin">The X/Y bounds expansion for tetrahedra.</param>
    /// <param name="segmentMargin">The X/Y bounds expansion for segments.</param>
    /// <param name="visible">The angular bounds of the visible tetrahedra.</param>
    /// <param name="occluders">The angular bounds of the occluder tetrahedra.</param>
    /// <param name="segments">The angular bounds of the segments.</param>
    /// <returns>The tiling for the frame.</returns>
    public static ScreenTiling Build(int width, int height, float pixelSize, float tetraMargin, float segmentMargin,
        AngularRanges[] visible, AngularRanges[] occluders, AngularRanges[] segments)
    {
        int tilesX = (width + TileSize - 1) / TileSize;
        int tilesY = (height + TileSize - 1) / TileSize;
        (int[] visibleOffsets, int[] visibleIndices) = BuildCategory(visible, tetraMargin, width, height, pixelSize, tilesX, tilesY);
        (int[] occluderOffsets, int[] occluderIndices) = BuildCategory(occluders, tetraMargin, width, height, pixelSize, tilesX, tilesY);
        (int[] segmentOffsets, int[] segmentIndices) = BuildCategory(segments, segmentMargin, width, height, pixelSize, tilesX, tilesY);
        return new(tilesX, tilesY, visibleOffsets, visibleIndices, occluderOffsets, occluderIndices, segmentOffsets, segmentIndices);
    }

    /// <summary>
    /// Builds the per-tile candidate lists of one primitive category: two passes
    /// (count, then fill) over the primitives' tile rectangles.
    /// </summary>
    private static (int[] Offsets, int[] Indices) BuildCategory(
        AngularRanges[] ranges, float margin, int width, int height, float pixelSize, int tilesX, int tilesY)
    {
        int tiles = tilesX * tilesY;
        int[] offsets = new int[tiles + 1];
        foreach (AngularRanges r in ranges)
            foreach (int tile in TileRect(r, margin, width, height, pixelSize, tilesX, tilesY))
                offsets[tile + 1]++;
        for (int t = 0; t < tiles; t++)
            offsets[t + 1] += offsets[t];
        int[] indices = new int[offsets[tiles]];
        int[] cursors = [.. offsets[..tiles]];
        for (int i = 0; i < ranges.Length; i++)
            foreach (int tile in TileRect(ranges[i], margin, width, height, pixelSize, tilesX, tilesY))
                indices[cursors[tile]++] = i;
        return (offsets, indices);
    }

    /// <summary>
    /// Enumerates the tiles overlapped by a primitive's angular X/Y bounds with margin.
    /// </summary>
    private static IEnumerable<int> TileRect(
        AngularRanges r, float margin, int width, int height, float pixelSize, int tilesX, int tilesY)
    {
        float tileUnits = pixelSize * TileSize;
        float originX = width * 0.5f * pixelSize;
        float originY = height * 0.5f * pixelSize;
        int tx0 = TileIndex(r.X.Min - margin + originX, tileUnits, tilesX);
        int tx1 = TileIndex(r.X.Max + margin + originX, tileUnits, tilesX);
        // Tile rows count down the screen while the angular Y counts up: flip the axis.
        int ty0 = TileIndex(originY - r.Y.Max - margin, tileUnits, tilesY);
        int ty1 = TileIndex(originY - r.Y.Min + margin, tileUnits, tilesY);
        for (int ty = ty0; ty <= ty1; ty++)
        for (int tx = tx0; tx <= tx1; tx++)
            yield return ty * tilesX + tx;
    }

    /// <summary>
    /// Clamps an angular coordinate's tile index into the tile grid.
    /// </summary>
    private static int TileIndex(float coordinate, float tileUnits, int tiles) =>
        Math.Clamp((int)MathF.Floor(coordinate / tileUnits), 0, tiles - 1);
}
