using System.Diagnostics;
using System.Numerics;
using System.Text;
using ComputeSharp;
using Tap4D.Geometry;
using Tap4D.Maths;
using Tap4D.Rendering;
using Tap4D.Rendering.Raycast;
using Tap4D.Scene;
using Tap4D.Viewer;

namespace Tap4D.Tests.Rendering;

/// <summary>
/// Profiling harness for the raycasting renderer: mimics the viewer loop with a moving
/// camera (so every frame differs), then repeats with a static camera as a baseline.
/// Both loops render fully every frame; the static one benefits from warmer CPU caches.
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
        sb.AppendLine($"moving: {movingMs / (double)frames:F2} ms/frame (everything: CPU prep, tiling, upload, voxel pass, composite, readback)");
        sb.AppendLine($"static: {staticMs / (double)frames:F2} ms/frame (same work, constant camera, warmer caches)");
        sb.AppendLine($"angular ranges alone: {rangesMs / (double)frames:F2} ms/frame");
        File.WriteAllText("viewer_profile.txt", sb.ToString());
        Assert.True(true);
    }

    /// <summary>
    /// Profiles the 625-tesseract grid scene (30000 occluders, 20000 segments):
    /// a few moving-camera frames, plus the CPU-side prep (ranges and tiling) separately.
    /// </summary>
    [Fact]
    public void ProfileGridScene()
    {
        if (!GraphicsDevice.EnumerateDevices().Any())
            return;
        const int frames = 3;
        Scene4 scene = DemoScene.Build(DemoScene.Mode.Wireframe, DemoScene.Layout.Grid625);
        GpuRaycastRenderer renderer = new(800, 800, 64);
        renderer.Palette = new AnaKataPalette { MinW = -1.2f, MaxW = 1.2f };
        renderer.ViewHeight = 2.5f;
        renderer.SegmentWScale = 0.25f;
        Camera4 camera = new() { Position = new Vector4(0, 0, -4, 0) };
        renderer.Render(scene, camera); // warmup
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < frames; i++)
            renderer.Render(scene, camera);
        sw.Stop();
        double totalMs = sw.ElapsedMilliseconds / (double)frames;
        sw.Restart();
        ComputeRanges(scene, 2f);
        sw.Stop();
        double rangesMs = sw.ElapsedMilliseconds;
        string report = $"grid 625: {totalMs:F2} ms/frame total, angular ranges {rangesMs:F2} ms/frame";
        File.WriteAllText("grid_profile.txt", report + Environment.NewLine);
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
