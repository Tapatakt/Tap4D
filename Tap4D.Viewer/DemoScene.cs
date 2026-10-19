using System.Numerics;
using Tap4D.Geometry;
using Tap4D.Maths;
using Tap4D.Scene;

namespace Tap4D.Viewer;

/// <summary>
/// Builds the demo scene (a tesseract at the origin) for the current display mode
/// and auto-rotation angle.
/// </summary>
internal static class DemoScene
{
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

    /// <summary>
    /// Builds the tesseract scene at the origin.
    /// </summary>
    /// <param name="mode">The display mode.</param>
    /// <returns>The scene.</returns>
    public static Scene4 Build(Mode mode)
    {
        Scene4 scene = new();
        if (mode == Mode.Solid)
            scene.Tetrahedra.AddRange(Tesseract.Tetrahedra());
        if (mode == Mode.Wireframe)
            scene.Occluders.AddRange(Tesseract.Tetrahedra());
        scene.Segments.AddRange(Tesseract.Edges(radius: 0.01f));
        return scene;
    }
}
