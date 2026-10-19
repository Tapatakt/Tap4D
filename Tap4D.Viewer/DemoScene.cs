using System.Numerics;
using Tap4D.Geometry;
using Tap4D.Scene;

namespace Tap4D.Viewer;

/// <summary>
/// Builds the demo scenes: a single tesseract, or a 5x5x5x5 grid of small tesseracts
/// forming one big tesseract (a stress test with 30000 tetrahedra and 20000 edges).
/// The scenes are static, so they are built once and cached.
/// </summary>
internal static class DemoScene
{
    private const int GridSide = 5;
    private const float GridHalfSize = 0.2f;
    private const float GridRadius = 0.002f;

    private static readonly Dictionary<(Mode, Layout), Scene4> Cache = [];

    /// <summary>The display mode: solid surface, occluder wireframe, or edges only.</summary>
    internal enum Mode
    {
        /// <summary>Visible tetrahedra and edges.</summary>
        Solid,

        /// <summary>Occluder tetrahedra and edges: hidden-line wireframe.</summary>
        Wireframe,

        /// <summary>Edges only: pure wireframe without occlusion.</summary>
        Lines,
    }

    /// <summary>The scene layout: one tesseract or a grid of 625 forming one big tesseract.</summary>
    internal enum Layout
    {
        /// <summary>A single tesseract at the origin.</summary>
        Single,

        /// <summary>A 5x5x5x5 grid of small tesseracts forming one big tesseract at the origin.</summary>
        Grid625,
    }

    /// <summary>
    /// Builds (or returns the cached) demo scene at the origin.
    /// </summary>
    /// <param name="mode">The display mode.</param>
    /// <param name="layout">The scene layout.</param>
    /// <returns>The scene.</returns>
    public static Scene4 Build(Mode mode, Layout layout)
    {
        if (!Cache.TryGetValue((mode, layout), out Scene4? scene))
            Cache[(mode, layout)] = scene = layout == Layout.Single ? BuildSingle(mode) : BuildGrid(mode);
        return scene;
    }

    /// <summary>
    /// Builds the single-tesseract scene.
    /// </summary>
    private static Scene4 BuildSingle(Mode mode)
    {
        Scene4 scene = new();
        if (mode == Mode.Solid)
            scene.Tetrahedra.AddRange(Tesseract.Tetrahedra());
        if (mode == Mode.Wireframe)
            scene.Occluders.AddRange(Tesseract.Tetrahedra());
        scene.Segments.AddRange(Tesseract.Edges(radius: 0.01f));
        return scene;
    }

    /// <summary>
    /// Builds the grid scene: small tesseracts touching face-to-face, centered at the
    /// origin, with the same total size as the single tesseract.
    /// </summary>
    private static Scene4 BuildGrid(Mode mode)
    {
        Scene4 scene = new();
        List<Tetrahedron> tetrahedra = Tesseract.Tetrahedra(GridHalfSize);
        List<Segment4> edges = Tesseract.Edges(GridHalfSize, GridRadius);
        for (int x = 0; x < GridSide; x++)
        for (int y = 0; y < GridSide; y++)
        for (int z = 0; z < GridSide; z++)
        for (int w = 0; w < GridSide; w++)
            AddTesseract(scene, mode, tetrahedra, edges,
                new Vector4(x - 2, y - 2, z - 2, w - 2) * (2 * GridHalfSize));
        return scene;
    }

    /// <summary>
    /// Adds one grid cell's tesseract, translated by the offset, to the scene.
    /// </summary>
    private static void AddTesseract(Scene4 scene, Mode mode, List<Tetrahedron> tetrahedra, List<Segment4> edges, Vector4 offset)
    {
        if (mode == Mode.Solid)
            scene.Tetrahedra.AddRange(Offset(tetrahedra, offset));
        if (mode == Mode.Wireframe)
            scene.Occluders.AddRange(Offset(tetrahedra, offset));
        scene.Segments.AddRange(Offset(edges, offset));
    }

    /// <summary>
    /// Translates tetrahedra by the offset.
    /// </summary>
    private static List<Tetrahedron> Offset(List<Tetrahedron> source, Vector4 offset) =>
        [.. source.Select(t => new Tetrahedron(t.A + offset, t.B + offset, t.C + offset, t.D + offset))];

    /// <summary>
    /// Translates segments by the offset.
    /// </summary>
    private static List<Segment4> Offset(List<Segment4> source, Vector4 offset) =>
        [.. source.Select(s => new Segment4(s.Start + offset, s.End + offset, s.Radius))];
}
