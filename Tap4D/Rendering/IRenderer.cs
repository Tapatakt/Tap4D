using Tap4D.Scene;

namespace Tap4D.Rendering;

/// <summary>
/// A scene renderer producing BGRA pixel buffers, with frame alternation
/// for overlapping surfaces.
/// </summary>
public interface IRenderer
{
    /// <summary>The image width in pixels.</summary>
    int Width { get; }

    /// <summary>The image height in pixels.</summary>
    int Height { get; }

    /// <summary>The height of the virtual screen in world units.</summary>
    float ViewHeight { get; set; }

    /// <summary>
    /// The frame counter driving the alternation of overlapping surfaces;
    /// incremented by the renderer after every actual render.
    /// </summary>
    int FrameIndex { get; set; }

    /// <summary>The mapping from ana-kata to color.</summary>
    AnaKataPalette Palette { get; set; }

    /// <summary>The lower ana-kata bound of the virtual screen.</summary>
    float MinW { get; set; }

    /// <summary>The upper ana-kata bound of the virtual screen.</summary>
    float MaxW { get; set; }

    /// <summary>
    /// Renders the scene into a BGRA pixel buffer (4 bytes per pixel, row-major, top to bottom).
    /// </summary>
    /// <param name="scene">The scene to render.</param>
    /// <param name="camera">The camera to render from.</param>
    /// <returns>The BGRA pixel buffer.</returns>
    byte[] Render(Scene4 scene, Camera4 camera);
}
