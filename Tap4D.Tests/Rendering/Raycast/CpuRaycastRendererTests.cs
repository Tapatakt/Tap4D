using System.Numerics;
using Tap4D.Geometry;
using Tap4D.Rendering.Raycast;
using Tap4D.Scene;

namespace Tap4D.Tests.Rendering.Raycast;

/// <summary>
/// Tests for <see cref="CpuRaycastRenderer"/>. All scenes are rendered at 32x32 pixels
/// with 8 slices and the default camera (at the origin, focal 2, near 0.1).
/// </summary>
public class CpuRaycastRendererTests
{
    private const int Size = 32;
    private const int Slices = 8;

    /// <summary>
    /// Verifies that an empty scene renders as a fully black image.
    /// </summary>
    [Fact]
    public void EmptySceneIsBlack()
    {
        CpuRaycastRenderer renderer = new(Size, Size, Slices);
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
            new Vector4(0, 0, 4, 0.6f)));
        CpuRaycastRenderer renderer = new(Size, Size, Slices);
        byte[] buffer = renderer.Render(scene, new Camera4());
        Assert.True(PixelRed(buffer, Size / 2, Size / 2) + PixelBlue(buffer, Size / 2, Size / 2) > 0);
        Assert.Equal(0, PixelRed(buffer, 0, 0) + PixelBlue(buffer, 0, 0));
    }

    /// <summary>
    /// Verifies the frame alternation in ana-kata order: two tetrahedra at different W
    /// covering the center pixel show the red one on frame 0 and the blue one on frame 1.
    /// </summary>
    [Fact]
    public void FramesAlternateInWOrder()
    {
        Scene4 scene = new();
        scene.Tetrahedra.Add(new( // screenW in [-0.4, -0.1], slice 3
            new Vector4(-1, -1, 4, -0.8f),
            new Vector4(1, -1, 4, -0.8f),
            new Vector4(0, 1, 4, -0.8f),
            new Vector4(0, 0, 4, -0.2f)));
        scene.Tetrahedra.Add(new( // screenW in [0.13, 0.37], slice 4
            new Vector4(-1, -1, 5, 0.325f),
            new Vector4(1, -1, 5, 0.325f),
            new Vector4(0, 1, 5, 0.325f),
            new Vector4(0, 0, 5, 0.925f)));
        CpuRaycastRenderer renderer = new(Size, Size, Slices);
        byte[] frame0 = renderer.Render(scene, new Camera4());
        renderer.FrameIndex = 1;
        byte[] frame1 = renderer.Render(scene, new Camera4());
        int center = (Size / 2 * Size + Size / 2) * 4;
        Assert.True(frame0[center + 2] > frame0[center], "Frame 0 should show the red (low W) candidate");
        Assert.True(frame1[center] > frame1[center + 2], "Frame 1 should show the blue (high W) candidate");
    }

    /// <summary>
    /// Verifies that an occluder nearer along the ray blocks the voxel on every frame.
    /// </summary>
    [Fact]
    public void OccluderBlocksRay()
    {
        Scene4 scene = new();
        scene.Tetrahedra.Add(new(
            new Vector4(-1, -1, 6, -1),
            new Vector4(1, -1, 6, 1),
            new Vector4(0, 1, 6, 0),
            new Vector4(0, 0, 6, 0)));
        scene.Occluders.Add(new(
            new Vector4(-2, -2, 3, -1),
            new Vector4(2, -2, 3, 1),
            new Vector4(0, 2, 3, 0),
            new Vector4(0, 0, 3, 0)));
        CpuRaycastRenderer renderer = new(Size, Size, Slices);
        for (int frame = 0; frame < 3; frame++)
        {
            renderer.FrameIndex = frame;
            byte[] buffer = renderer.Render(scene, new Camera4());
            Assert.Equal(0, PixelRed(buffer, Size / 2, Size / 2) + PixelBlue(buffer, Size / 2, Size / 2));
        }
    }

    /// <summary>
    /// Verifies the W-parallax of the radial projection: moving the camera along W
    /// changes the image shape (the colored pixel count shrinks), which the planar
    /// projection would never do.
    /// </summary>
    [Fact]
    public void StrafingInWChangesImageShape()
    {
        Scene4 scene = new();
        scene.Tetrahedra.Add(new(
            new Vector4(-1, -1, 4, 0),
            new Vector4(1, -1, 4, 0),
            new Vector4(0, 1, 4, 0),
            new Vector4(0, 0, 4, 2)));
        CpuRaycastRenderer renderer = new(Size, Size, Slices);
        byte[] before = renderer.Render(scene, new Camera4());
        byte[] after = renderer.Render(scene, new Camera4 { Position = new Vector4(0, 0, 0, -1) });
        int countBefore = CountColored(before);
        int countAfter = CountColored(after);
        Assert.NotEqual(countBefore, countAfter);
        Assert.True(countAfter < countBefore, "Strafing away in W should shrink the image");
    }

    /// <summary>
    /// Returns the red channel of a pixel from a BGRA buffer.
    /// </summary>
    private static int PixelRed(byte[] buffer, int x, int y) => buffer[(y * Size + x) * 4 + 2];

    /// <summary>
    /// Returns the blue channel of a pixel from a BGRA buffer.
    /// </summary>
    private static int PixelBlue(byte[] buffer, int x, int y) => buffer[(y * Size + x) * 4];

    /// <summary>
    /// Counts pixels with a non-zero alpha channel.
    /// </summary>
    private static int CountColored(byte[] buffer)
    {
        int count = 0;
        for (int i = 3; i < buffer.Length; i += 4)
            if (buffer[i] != 0)
                count++;
        return count;
    }
}
