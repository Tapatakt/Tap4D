using System.Numerics;
using ComputeSharp;
using Tap4D.Geometry;
using Tap4D.Rendering;
using Tap4D.Scene;

namespace Tap4D.Tests.Rendering;

/// <summary>
/// Tests for <see cref="GpuRenderer"/>, comparing its output against the CPU reference renderer.
/// Silently passes when no D3D12 device is available.
/// </summary>
public class GpuRendererTests
{
    private const int Size = 64;
    private const int FrameCount = 3;

    /// <summary>
    /// Whether a D3D12 device is available on this machine.
    /// </summary>
    private static bool GpuAvailable => GraphicsDevice.EnumerateDevices().Any();

    /// <summary>
    /// Verifies that on a simple scene (one tetrahedron, one occluder, one segment)
    /// the GPU output matches the CPU reference almost exactly.
    /// </summary>
    [Fact]
    public void SimpleSceneMatchesAlmostExactly()
    {
        if (!GpuAvailable)
            return;
        Scene4 scene = new();
        scene.Tetrahedra.Add(new(
            new Vector4(-1, -1, 5, -1),
            new Vector4(1, -1, 5, 0),
            new Vector4(0, 1, 5, 1),
            new Vector4(0, 0, 5, 0)));
        scene.Occluders.Add(new(
            new Vector4(-0.4f, -0.4f, 3, 0),
            new Vector4(0.4f, -0.4f, 3, 0),
            new Vector4(0, 0.4f, 3, 0),
            new Vector4(0, 0, 3, 0.5f)));
        scene.Segments.Add(new(new Vector4(-1, 1, 4, -1), new Vector4(1, 1, 4, 1), 0.05f));
        Camera4 camera = new();
        byte[] expected = new CpuRenderer(Size, Size) { FrameIndex = 1 }.Render(scene, camera);
        byte[] actual = new GpuRenderer(Size, Size) { FrameIndex = 1 }.Render(scene, camera);
        int mismatches = CountFrameMismatches(actual, [expected]);
        Assert.True(CountColoredPixels(expected) > 100, "The scene should cover a visible part of the image");
        Assert.True(mismatches < Size * Size / 50, $"Too many mismatched pixels: {mismatches}");
    }

    /// <summary>
    /// Verifies that on the tesseract scene every GPU pixel matches a color the CPU renderer
    /// produces for some alternation frame. Face-on tesseract cells give intersection segments
    /// of constant depth, where rounding picks either endpoint — both are valid candidates,
    /// so frame-exact comparison is impossible, but the candidate color set must match.
    /// </summary>
    [Fact]
    public void TesseractGpuColorsComeFromCpuCandidateSet()
    {
        if (!GpuAvailable)
            return;
        Scene4 scene = BuildTesseractScene();
        byte[][] cpuFrames = [.. Enumerable.Range(0, 32).Select(f => RenderCpu(scene, f))];
        Assert.True(CountColoredPixels(cpuFrames[0]) > 500, "The scene should cover a visible part of the image");
        for (int frame = 0; frame < FrameCount; frame++)
        {
            byte[] gpu = RenderGpu(scene, frame);
            int mismatches = CountFrameMismatches(gpu, cpuFrames);
            Assert.True(mismatches < Size * Size / 100, $"Frame {frame}: {mismatches} pixels match no CPU frame");
        }
    }

    /// <summary>
    /// Builds the tesseract scene with edges and one occluder tetrahedron in front of it.
    /// </summary>
    /// <returns>The test scene.</returns>
    private static Scene4 BuildTesseractScene()
    {
        Scene4 scene = new();
        scene.Tetrahedra.AddRange(Tesseract.Tetrahedra());
        scene.Segments.AddRange(Tesseract.Edges(radius: 0.03f));
        scene.Occluders.Add(new(
            new Vector4(-0.3f, -0.3f, -2, 0),
            new Vector4(0.3f, -0.3f, -2, 0),
            new Vector4(0, 0.3f, -2, 0),
            new Vector4(0, 0, -2, 0.3f)));
        return scene;
    }

    /// <summary>
    /// Renders the tesseract scene camera: at (0, 0, -4, 0), tilted in the XW plane.
    /// </summary>
    /// <returns>The test camera.</returns>
    private static Camera4 TesseractCamera()
    {
        Camera4 camera = new() { Position = new Vector4(0, 0, -4, 0) };
        camera.Rotate(Tap4D.Maths.RotationPlane.XW, 0.25f);
        return camera;
    }

    /// <summary>
    /// Renders one frame with the CPU renderer, using the tesseract camera.
    /// </summary>
    private static byte[] RenderCpu(Scene4 scene, int frame) =>
        new CpuRenderer(Size, Size) { FrameIndex = frame }.Render(scene, TesseractCamera());

    /// <summary>
    /// Renders one frame with the GPU renderer, using the tesseract camera.
    /// </summary>
    private static byte[] RenderGpu(Scene4 scene, int frame) =>
        new GpuRenderer(Size, Size) { FrameIndex = frame }.Render(scene, TesseractCamera());

    /// <summary>
    /// Counts the pixels of <paramref name="actual"/> that match no reference frame
    /// within a small per-channel tolerance.
    /// </summary>
    /// <param name="actual">The buffer under test.</param>
    /// <param name="referenceFrames">The reference buffers for the alternation frames.</param>
    /// <returns>The number of pixels matching no reference frame.</returns>
    private static int CountFrameMismatches(byte[] actual, byte[][] referenceFrames)
    {
        int mismatches = 0;
        for (int i = 0; i < actual.Length; i += 4)
            if (!referenceFrames.Any(frame => PixelClose(frame, actual, i)))
                mismatches++;
        return mismatches;
    }

    /// <summary>
    /// Whether a pixel of two BGRA buffers matches within a per-channel tolerance of 2.
    /// </summary>
    private static bool PixelClose(byte[] a, byte[] b, int offset)
    {
        for (int c = 0; c < 4; c++)
            if (Math.Abs(a[offset + c] - b[offset + c]) > 2)
                return false;
        return true;
    }

    /// <summary>
    /// Counts pixels with a non-zero alpha channel.
    /// </summary>
    /// <param name="buffer">The BGRA buffer.</param>
    /// <returns>The number of colored pixels.</returns>
    private static int CountColoredPixels(byte[] buffer)
    {
        int count = 0;
        for (int i = 3; i < buffer.Length; i += 4)
            if (buffer[i] != 0)
                count++;
        return count;
    }
}
