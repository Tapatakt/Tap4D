using System.Diagnostics;
using System.Numerics;
using System.Text;
using ComputeSharp;
using Tap4D.Geometry;
using Tap4D.Maths;
using Tap4D.Rendering;
using Tap4D.Rendering.Raycast;
using Tap4D.Scene;

namespace Tap4D.Tests.Rendering;

/// <summary>
/// Profiling harness for the raycasting renderer: mimics the viewer loop with a moving
/// camera (so every frame differs), then compares against the static-camera loop to
/// separate the voxel pass cost from compositing/readback/CPU-side work.
/// </summary>
public class ViewerProfile
{
    /// <summary>
    /// Profiles moving and static camera loops and writes the breakdown to a file.
    /// </summary>
    [Fact]
    public void ProfileViewerLoop()
    {
        if (!GraphicsDevice.EnumerateDevices().Any())
            return;
        const int frames = 30;
        Scene4 scene = new();
        scene.Occluders.AddRange(Tesseract.Tetrahedra());
        scene.Segments.AddRange(Tesseract.Edges(radius: 0.01f));
        GpuRaycastRenderer renderer = new(800, 800, 64);
        renderer.Palette = new AnaKataPalette { MinW = -1.2f, MaxW = 1.2f };
        renderer.ViewHeight = 2.5f;
        Camera4 CameraAt(float angle) => new()
        {
            Position = new Vector4(0, 0, -4, 0),
            Orientation = Rotation4.Create(RotationPlane.XW, angle),
        };
        renderer.Render(scene, CameraAt(0)); // warmup
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < frames; i++)
            renderer.Render(scene, CameraAt(i * 0.03f));
        sw.Stop();
        long movingMs = sw.ElapsedMilliseconds;
        sw.Restart();
        for (int i = 0; i < frames; i++)
            renderer.Render(scene, CameraAt(0));
        sw.Stop();
        long staticMs = sw.ElapsedMilliseconds;
        sw.Restart();
        for (int i = 0; i < frames; i++)
            ComputeRanges(scene, 2f);
        sw.Stop();
        long rangesMs = sw.ElapsedMilliseconds;
        StringBuilder sb = new();
        sb.AppendLine($"moving: {movingMs / (double)frames:F2} ms/frame (everything: CPU prep, upload, voxel pass, composite, readback)");
        sb.AppendLine($"static: {staticMs / (double)frames:F2} ms/frame (composite + readback + light CPU)");
        sb.AppendLine($"voxel pass + upload + ranges: {(movingMs - staticMs) / (double)frames:F2} ms/frame");
        sb.AppendLine($"angular ranges alone: {rangesMs / (double)frames:F2} ms/frame");
        File.WriteAllText("viewer_profile.txt", sb.ToString());
        Assert.True(true);
    }

    /// <summary>
    /// Computes the angular bounds for all scene primitives, as done at upload time.
    /// </summary>
    private static void ComputeRanges(Scene4 scene, float focal)
    {
        foreach (Tetrahedron t in scene.Tetrahedra)
            _ = AngularRanges.Compute(t, focal);
        foreach (Tetrahedron t in scene.Occluders)
            _ = AngularRanges.Compute(t, focal);
        foreach (Segment4 s in scene.Segments)
            _ = AngularRanges.ComputeWithRadius(s, focal);
    }
}
