using System.Numerics;
using Tap4D.Geometry;
using Tap4D.Rendering;
using Tap4D.Scene;

namespace Tap4D.Tests.Rendering;

/// <summary>
/// Tests for <see cref="CpuRenderer"/>. All scenes are rendered with a 16x16 pixel image
/// and the default camera (at the origin, focal 2, near 0.1).
/// </summary>
public class CpuRendererTests
{
    private const int Size = 16;

    /// <summary>
    /// Verifies that an empty scene renders as a fully black image.
    /// </summary>
    [Fact]
    public void EmptySceneIsBlack()
    {
        CpuRenderer renderer = new(Size, Size);
        byte[] buffer = renderer.Render(new Scene4(), new Camera4());
        Assert.All(buffer, b => Assert.Equal(0, b));
    }

    /// <summary>
    /// Verifies that a tetrahedron in front of the camera covers the center pixel
    /// but not a corner pixel.
    /// </summary>
    [Fact]
    public void TetrahedronCoversCenterButNotCorner()
    {
        Scene4 scene = new();
        scene.Tetrahedra.Add(new(
            new Vector4(-1, -1, 4, 0),
            new Vector4(1, -1, 4, 0),
            new Vector4(0, 1, 4, 0),
            new Vector4(0, 0, 4, 1)));
        CpuRenderer renderer = new(Size, Size);
        byte[] buffer = renderer.Render(scene, new Camera4());
        Assert.True(PixelRed(buffer, Size / 2, Size / 2) + PixelBlue(buffer, Size / 2, Size / 2) > 0);
        Assert.Equal(0, PixelRed(buffer, 0, 0) + PixelBlue(buffer, 0, 0));
    }

    /// <summary>
    /// Verifies that an occluder hides the tetrahedron behind it permanently,
    /// on every alternation frame.
    /// </summary>
    [Fact]
    public void OccluderHidesPermanently()
    {
        Scene4 scene = OccluderTestScene();
        CpuRenderer renderer = new(Size, Size);
        for (int frame = 0; frame < 3; frame++)
        {
            renderer.FrameIndex = frame;
            byte[] buffer = renderer.Render(scene, new Camera4());
            Assert.Equal(0, PixelRed(buffer, Size / 2, Size / 2) + PixelBlue(buffer, Size / 2, Size / 2));
        }
    }

    /// <summary>
    /// Verifies that a surface in front of an occluder is shown on every frame,
    /// while the surface behind the occluder never appears.
    /// </summary>
    [Fact]
    public void SurfaceInFrontOfOccluderShowsOnAllFrames()
    {
        Scene4 scene = OccluderTestScene();
        scene.Tetrahedra.Add(new(
            new Vector4(-0.5f, -0.5f, 2, 1),
            new Vector4(0.5f, -0.5f, 2, 1),
            new Vector4(0, 0.5f, 2, 1),
            new Vector4(0, 0, 2, 2)));
        CpuRenderer renderer = new(Size, Size);
        byte[] frame0 = renderer.Render(scene, new Camera4());
        renderer.FrameIndex = 1;
        byte[] frame1 = renderer.Render(scene, new Camera4());
        int center = (Size / 2 * Size + Size / 2) * 4;
        Assert.True(frame0[center + 2] > 0 || frame0[center] > 0);
        Assert.Equal(frame0[center], frame1[center]);
        Assert.Equal(frame0[center + 2], frame1[center + 2]);
    }

    /// <summary>
    /// Verifies that a tetrahedron fully beyond the ana-kata bound of the virtual screen
    /// (screenW &gt; MaxW) is clipped away entirely.
    /// </summary>
    [Fact]
    public void ObjectBeyondAnaKataRangeIsInvisible()
    {
        Scene4 scene = new();
        scene.Tetrahedra.Add(new(
            new Vector4(-1, -1, 4, 5),
            new Vector4(1, -1, 4, 5),
            new Vector4(0, 1, 4, 5),
            new Vector4(0, 0, 4, 6)));
        byte[] buffer = new CpuRenderer(Size, Size).Render(scene, new Camera4());
        Assert.All(buffer, b => Assert.Equal(0, b));
    }

    /// <summary>
    /// Verifies that a tetrahedron straddling the ana-kata bound is clipped to it:
    /// the part within the screen stays visible.
    /// </summary>
    [Fact]
    public void ObjectStraddlingAnaKataBoundIsPartiallyVisible()
    {
        Scene4 scene = new();
        scene.Tetrahedra.Add(new(
            new Vector4(-1, -1, 4, 0),
            new Vector4(1, -1, 4, 0),
            new Vector4(0, 1, 4, 0),
            new Vector4(0, 0, 4, 8)));
        byte[] buffer = new CpuRenderer(Size, Size).Render(scene, new Camera4());
        Assert.True(PixelRed(buffer, Size / 2, Size / 2) + PixelBlue(buffer, Size / 2, Size / 2) > 0);
        Assert.Equal(0, PixelRed(buffer, 0, 0) + PixelBlue(buffer, 0, 0));
    }

    /// <summary>
    /// Builds a scene with a visible tetrahedron at depth 6 hidden behind an occluder at depth 3.
    /// </summary>
    /// <returns>The test scene.</returns>
    private static Scene4 OccluderTestScene()
    {
        Scene4 scene = new();
        scene.Tetrahedra.Add(new(
            new Vector4(-2, -2, 6, 1),
            new Vector4(2, -2, 6, 1),
            new Vector4(0, 2, 6, 1),
            new Vector4(0, 0, 6, 2)));
        scene.Occluders.Add(new(
            new Vector4(-0.5f, -0.5f, 3, 0),
            new Vector4(0.5f, -0.5f, 3, 0),
            new Vector4(0, 0.5f, 3, 0),
            new Vector4(0, 0, 3, 1)));
        return scene;
    }

    /// <summary>
    /// Verifies that duplicate candidates (same point reported by two identical tetrahedra)
    /// are merged into one and do not waste alternation frames.
    /// </summary>
    [Fact]
    public void DuplicateCandidatesAreMerged()
    {
        Tetrahedron front = new(
            new Vector4(-1, -1, 5, 0),
            new Vector4(1, -1, 5, 0),
            new Vector4(0, 1, 5, 0),
            new Vector4(0, 0, 5, 0.5f));
        Tetrahedron behind = new(
            new Vector4(-1, -1, 7, 2),
            new Vector4(1, -1, 7, 2),
            new Vector4(0, 1, 7, 2),
            new Vector4(0, 0, 7, 2));
        Scene4 scene = new();
        scene.Tetrahedra.Add(front);
        scene.Tetrahedra.Add(front); // exact duplicate
        scene.Tetrahedra.Add(behind);
        CpuRenderer renderer = new(Size, Size);
        int center = (Size / 2 * Size + Size / 2) * 4;
        byte[] frame0 = renderer.Render(scene, new Camera4());
        renderer.FrameIndex = 1;
        byte[] frame1 = renderer.Render(scene, new Camera4());
        renderer.FrameIndex = 2;
        byte[] frame2 = renderer.Render(scene, new Camera4());
        Assert.True(frame1[center] > frame0[center], "Frame 1 should show the bluer tetrahedron behind");
        Assert.Equal(frame0[center], frame2[center]);
        Assert.Equal(frame0[center + 2], frame2[center + 2]);
    }

    /// <summary>
    /// Verifies that with frame rate limiting, a call coming too early returns the cached
    /// frame without re-rendering or advancing the frame counter.
    /// </summary>
    [Fact]
    public void ThrottledRenderReturnsCachedFrame()
    {
        CpuRenderer renderer = new(Size, Size, maxFps: 1);
        Scene4 scene = new();
        Camera4 camera = new();
        byte[] first = renderer.Render(scene, camera);
        byte[] second = renderer.Render(scene, camera);
        Assert.Same(first, second);
        Assert.Equal(1, renderer.FrameIndex);
    }

    /// <summary>
    /// Returns the red channel of a pixel from a BGRA buffer.
    /// </summary>
    /// <param name="buffer">The BGRA pixel buffer.</param>
    /// <param name="x">The pixel column.</param>
    /// <param name="y">The pixel row.</param>
    /// <returns>The red channel value.</returns>
    private static int PixelRed(byte[] buffer, int x, int y) => buffer[(y * Size + x) * 4 + 2];

    /// <summary>
    /// Returns the blue channel of a pixel from a BGRA buffer.
    /// </summary>
    /// <param name="buffer">The BGRA pixel buffer.</param>
    /// <param name="x">The pixel column.</param>
    /// <param name="y">The pixel row.</param>
    /// <returns>The blue channel value.</returns>
    private static int PixelBlue(byte[] buffer, int x, int y) => buffer[(y * Size + x) * 4];
}
