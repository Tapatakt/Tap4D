using System.Numerics;
using Tap4D.Geometry;
using Tap4D.Tests.Maths;

namespace Tap4D.Tests.Geometry;

/// <summary>
/// Tests for <see cref="Tesseract"/>.
/// </summary>
public class TesseractTests
{
    /// <summary>
    /// Verifies that the tesseract boundary consists of 48 tetrahedra (8 cells, 6 each).
    /// </summary>
    [Fact]
    public void BoundaryHas48Tetrahedra()
    {
        Assert.Equal(48, Tesseract.Tetrahedra().Count);
    }

    /// <summary>
    /// Verifies that the tetrahedralization tiles the boundary without gaps or overlaps:
    /// the total 3D volume equals 8 cubical cells of side 2 * halfSize.
    /// </summary>
    [Fact]
    public void TetrahedraTileBoundaryVolume()
    {
        const float halfSize = 1.5f;
        float totalVolume = Tesseract.Tetrahedra(halfSize).Sum(SimplexVolume.Of);
        float cellSide = 2 * halfSize;
        Assert.Equal(8 * cellSide * cellSide * cellSide, totalVolume, 1e-2f);
    }

    /// <summary>
    /// Verifies that every tetrahedron vertex is a tesseract vertex (all coordinates are +/- halfSize).
    /// </summary>
    [Fact]
    public void TetrahedraUseOnlyTesseractVertices()
    {
        const float halfSize = 1f;
        foreach (Tetrahedron tetra in Tesseract.Tetrahedra(halfSize))
        foreach (Vector4 vertex in new[] { tetra.A, tetra.B, tetra.C, tetra.D })
        {
            Assert.True(MathF.Abs(MathF.Abs(vertex.X) - halfSize) < 1e-6f);
            Assert.True(MathF.Abs(MathF.Abs(vertex.Y) - halfSize) < 1e-6f);
            Assert.True(MathF.Abs(MathF.Abs(vertex.Z) - halfSize) < 1e-6f);
            Assert.True(MathF.Abs(MathF.Abs(vertex.W) - halfSize) < 1e-6f);
        }
    }

    /// <summary>
    /// Verifies that the tesseract has 32 edges.
    /// </summary>
    [Fact]
    public void Has32Edges()
    {
        Assert.Equal(32, Tesseract.Edges().Count);
    }

    /// <summary>
    /// Verifies that every edge connects two vertices differing in exactly one coordinate.
    /// </summary>
    [Fact]
    public void EdgesConnectAdjacentVertices()
    {
        const float halfSize = 1f;
        foreach (Segment4 edge in Tesseract.Edges(halfSize))
        {
            Assert.Equal(2 * halfSize, Vector4.Distance(edge.Start, edge.End), 1e-5f);
        }
    }
}
