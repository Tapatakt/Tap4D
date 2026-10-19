using System.Numerics;
using ComputeSharp;
using Tap4D.Geometry;
using Tap4D.Rendering;
using Tap4D.Rendering.Raycast;
using Tap4D.Scene;

namespace Tap4D.Tests.Rendering.Raycast;

/// <summary>
/// Tests for <see cref="GpuRaycastRenderer"/>, comparing its output against the CPU
/// raycasting reference. Silently passes when no D3D12 device is available.
/// </summary>
public class GpuRaycastRendererTests
{
    private const int Size = 64;
    private const int Slices = 16;

    /// <summary>
    /// Whether a D3D12 device is available on this machine.
    /// </summary>
    private static bool GpuAvailable => GraphicsDevice.EnumerateDevices().Any();

    /// <summary>
    /// Verifies that the GPU raycaster produces nearly the same image as the CPU reference
    /// on a wireframe tesseract scene, for two alternation frames. The voxel model has no
    /// candidate ties, so the frames should match almost everywhere.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void GpuMatchesCpu(int frameIndex)
    {
        if (!GpuAvailable)
            return;
        Scene4 scene = new();
        scene.Tetrahedra.AddRange(Tesseract.Tetrahedra());
        scene.Segments.AddRange(Tesseract.Edges(radius: 0.01f));
        Camera4 camera = new() { Position = new Vector4(0, 0, -4, 0) };
        byte[] expected = new CpuRaycastRenderer(Size, Size, Slices) { FrameIndex = frameIndex }.Render(scene, camera);
        byte[] actual = new GpuRaycastRenderer(Size, Size, Slices) { FrameIndex = frameIndex }.Render(scene, camera);
        int mismatches = CountMismatches(expected, actual);
        Assert.True(CountColoredPixels(expected) > 100, "The scene should cover a visible part of the image");
        Assert.True(mismatches < Size * Size / 100, $"Too many mismatched pixels: {mismatches}");
    }

    /// <summary>
    /// Counts pixels whose channels differ by more than 2 between two BGRA buffers.
    /// </summary>
    private static int CountMismatches(byte[] expected, byte[] actual)
    {
        int mismatches = 0;
        for (int i = 0; i < expected.Length; i += 4)
        {
            int diff = Math.Abs(expected[i] - actual[i])
                + Math.Abs(expected[i + 1] - actual[i + 1])
                + Math.Abs(expected[i + 2] - actual[i + 2])
                + Math.Abs(expected[i + 3] - actual[i + 3]);
            if (diff > 8)
                mismatches++;
        }
        return mismatches;
    }

    /// <summary>
    /// Counts pixels with a non-zero alpha channel.
    /// </summary>
    private static int CountColoredPixels(byte[] buffer)
    {
        int count = 0;
        for (int i = 3; i < buffer.Length; i += 4)
            if (buffer[i] != 0)
                count++;
        return count;
    }
}
