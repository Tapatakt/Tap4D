using System.Numerics;

namespace Tap4D.Geometry;

/// <summary>
/// Builds tesseracts (4D hypercubes) out of tetrahedra and segments.
/// Serves as the reference object for tests and the viewer.
/// </summary>
public static class Tesseract
{
    private static readonly int[][] Permutations =
    [
        [0, 1, 2], [0, 2, 1], [1, 0, 2], [1, 2, 0], [2, 0, 1], [2, 1, 0],
    ];

    /// <summary>
    /// Builds the boundary of a tesseract centered at the origin: 8 cubical cells,
    /// each split into 6 tetrahedra, 48 in total.
    /// </summary>
    /// <param name="halfSize">Half the edge length of the tesseract.</param>
    /// <returns>The tetrahedra forming the boundary of the tesseract.</returns>
    public static List<Tetrahedron> Tetrahedra(float halfSize = 1f)
    {
        List<Tetrahedron> result = [];
        for (int axis = 0; axis < 4; axis++)
        {
            AddCell(result, axis, -halfSize, halfSize);
            AddCell(result, axis, halfSize, halfSize);
        }
        return result;
    }

    /// <summary>
    /// Builds the 32 edges of a tesseract centered at the origin.
    /// </summary>
    /// <param name="halfSize">Half the edge length of the tesseract.</param>
    /// <param name="radius">The cylinder radius of the edges, in world units.</param>
    /// <returns>The edges of the tesseract as segments.</returns>
    public static List<Segment4> Edges(float halfSize = 1f, float radius = 0.01f)
    {
        List<Segment4> result = [];
        for (int from = 0; from < 16; from++)
        for (int bit = 0; bit < 4; bit++)
        {
            int to = from | (1 << bit);
            if (to > from)
                result.Add(new(Vertex(from, halfSize), Vertex(to, halfSize), radius));
        }
        return result;
    }

    /// <summary>
    /// Splits one cubical cell of the tesseract boundary into 6 tetrahedra,
    /// one per monotone corner path along the cell diagonal.
    /// </summary>
    /// <param name="result">The list to add the tetrahedra to.</param>
    /// <param name="fixedAxis">The axis on which the cell is fixed.</param>
    /// <param name="fixedValue">The coordinate value on the fixed axis.</param>
    /// <param name="h">Half the edge length.</param>
    private static void AddCell(List<Tetrahedron> result, int fixedAxis, float fixedValue, float h)
    {
        int[] freeAxes = [0, 1, 2, 3];
        freeAxes = [.. freeAxes.Where(a => a != fixedAxis)];
        foreach (int[] order in Permutations)
        {
            Vector4 c0 = Corner(fixedAxis, fixedValue, freeAxes, order, 0, h);
            Vector4 c1 = Corner(fixedAxis, fixedValue, freeAxes, order, 1, h);
            Vector4 c2 = Corner(fixedAxis, fixedValue, freeAxes, order, 2, h);
            Vector4 c3 = Corner(fixedAxis, fixedValue, freeAxes, order, 3, h);
            result.Add(new(c0, c1, c2, c3));
        }
    }

    /// <summary>
    /// Builds one corner of a cubical cell along a monotone path: all free coordinates start
    /// at the low side, then the first <paramref name="highCount"/> path axes are flipped high.
    /// </summary>
    /// <param name="fixedAxis">The axis on which the cell is fixed.</param>
    /// <param name="fixedValue">The coordinate value on the fixed axis.</param>
    /// <param name="freeAxes">The three axes that vary within the cell.</param>
    /// <param name="order">The path: the order in which free axes are flipped.</param>
    /// <param name="highCount">How many path axes are flipped to the high side.</param>
    /// <param name="h">Half the edge length.</param>
    /// <returns>The corner vertex.</returns>
    private static Vector4 Corner(int fixedAxis, float fixedValue, int[] freeAxes, int[] order, int highCount, float h)
    {
        float[] coords = [-h, -h, -h, -h];
        coords[fixedAxis] = fixedValue;
        for (int step = 0; step < highCount; step++)
            coords[freeAxes[order[step]]] = h;
        return new(coords[0], coords[1], coords[2], coords[3]);
    }

    /// <summary>
    /// Builds one vertex of the tesseract from a 4-bit mask: a set bit means the high side.
    /// </summary>
    /// <param name="mask">The 4-bit mask of vertex coordinates.</param>
    /// <param name="h">Half the edge length.</param>
    /// <returns>The vertex.</returns>
    private static Vector4 Vertex(int mask, float h) => new(
        (mask & 1) != 0 ? h : -h,
        (mask & 2) != 0 ? h : -h,
        (mask & 4) != 0 ? h : -h,
        (mask & 8) != 0 ? h : -h);
}
