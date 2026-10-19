using System.Numerics;
using Tap4D.Geometry;
using Tap4D.Maths;
using Tap4D.Scene;

namespace Tap4D.Rendering;

/// <summary>
/// Prepares scene geometry for rendering: transforms it into camera space and clips it
/// against the near plane and the ana-kata bounds of the virtual screen.
/// Shared by the CPU and GPU renderers.
/// </summary>
internal static class CameraSpaceGeometry
{
    /// <summary>
    /// Transforms and clips all scene geometry into camera space.
    /// </summary>
    /// <param name="scene">The scene in world space.</param>
    /// <param name="camera">The camera.</param>
    /// <param name="minW">
    /// The lower ana-kata bound of the virtual screen: points with screenW &lt; minW are clipped away.
    /// </param>
    /// <param name="maxW">
    /// The upper ana-kata bound of the virtual screen: points with screenW &gt; maxW are clipped away.
    /// </param>
    /// <param name="visible">The clipped visible tetrahedra.</param>
    /// <param name="occluders">The clipped occluder tetrahedra.</param>
    /// <param name="segments">The clipped segments.</param>
    public static void Prepare(
        Scene4 scene, Camera4 camera, float minW, float maxW,
        out List<Tetrahedron> visible, out List<Tetrahedron> occluders, out List<Segment4> segments)
    {
        // screenW = focal·W/Z, so minW ≤ screenW ≤ maxW is the pair of half-spaces
        // 0 ≤ maxW·Z - focal·W and 0 ≤ focal·W - minW·Z: planes through the camera origin.
        Vector4 maxPlane = new(0, 0, maxW, -camera.Focal);
        Vector4 minPlane = new(0, 0, -minW, camera.Focal);
        visible = ClipAll(scene.Tetrahedra, camera, minPlane, maxPlane);
        occluders = ClipAll(scene.Occluders, camera, minPlane, maxPlane);
        segments = CollectSegments(scene, camera, minPlane, maxPlane);
    }

    /// <summary>
    /// Transforms all scene geometry into camera space without any clipping
    /// (for the raycasting renderer, which needs no geometric clipping).
    /// </summary>
    /// <param name="scene">The scene in world space.</param>
    /// <param name="camera">The camera.</param>
    /// <param name="visible">The camera-space visible tetrahedra.</param>
    /// <param name="occluders">The camera-space occluder tetrahedra.</param>
    /// <param name="segments">The camera-space segments.</param>
    public static void TransformOnly(
        Scene4 scene, Camera4 camera,
        out List<Tetrahedron> visible, out List<Tetrahedron> occluders, out List<Segment4> segments)
    {
        visible = [.. scene.Tetrahedra.Select(t => ToCameraSpace(t, camera))];
        occluders = [.. scene.Occluders.Select(t => ToCameraSpace(t, camera))];
        segments =
        [
            .. scene.Segments.Select(s => new Segment4(
                CameraSpace.ToCamera(s.Start, camera.Position, camera.Orientation),
                CameraSpace.ToCamera(s.End, camera.Position, camera.Orientation),
                s.Radius)),
        ];
    }

    /// <summary>
    /// Transforms a list of tetrahedra to camera space and clips them against the near
    /// plane and the ana-kata bounds.
    /// </summary>
    /// <param name="source">The tetrahedra in world space.</param>
    /// <param name="camera">The camera.</param>
    /// <param name="minPlane">The lower ana-kata bound plane normal.</param>
    /// <param name="maxPlane">The upper ana-kata bound plane normal.</param>
    /// <returns>The clipped camera-space tetrahedra.</returns>
    private static List<Tetrahedron> ClipAll(List<Tetrahedron> source, Camera4 camera, Vector4 minPlane, Vector4 maxPlane)
    {
        List<Tetrahedron> result = [];
        foreach (Tetrahedron t in source)
        foreach (Tetrahedron c1 in TetrahedronClipper.ClipNearPlane(ToCameraSpace(t, camera), camera.Near))
        foreach (Tetrahedron c2 in TetrahedronClipper.ClipHalfSpace(c1, maxPlane, 0))
        foreach (Tetrahedron c3 in TetrahedronClipper.ClipHalfSpace(c2, minPlane, 0))
            result.Add(c3);
        return result;
    }

    /// <summary>
    /// Transforms a tetrahedron into camera space.
    /// </summary>
    /// <param name="t">The tetrahedron in world space.</param>
    /// <param name="camera">The camera.</param>
    /// <returns>The tetrahedron in camera space.</returns>
    private static Tetrahedron ToCameraSpace(Tetrahedron t, Camera4 camera) => new(
        CameraSpace.ToCamera(t.A, camera.Position, camera.Orientation),
        CameraSpace.ToCamera(t.B, camera.Position, camera.Orientation),
        CameraSpace.ToCamera(t.C, camera.Position, camera.Orientation),
        CameraSpace.ToCamera(t.D, camera.Position, camera.Orientation));

    /// <summary>
    /// Transforms all segments to camera space and trims them to the near plane
    /// and the ana-kata bounds.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="camera">The camera.</param>
    /// <param name="minPlane">The lower ana-kata bound plane normal.</param>
    /// <param name="maxPlane">The upper ana-kata bound plane normal.</param>
    /// <returns>The camera-space segments inside the view volume.</returns>
    private static List<Segment4> CollectSegments(Scene4 scene, Camera4 camera, Vector4 minPlane, Vector4 maxPlane)
    {
        List<Segment4> result = [];
        foreach (Segment4 s in scene.Segments)
        {
            Vector4 a = CameraSpace.ToCamera(s.Start, camera.Position, camera.Orientation);
            Vector4 b = CameraSpace.ToCamera(s.End, camera.Position, camera.Orientation);
            if (TryClipSegment(ref a, ref b, Vector4.UnitZ, camera.Near)
                && TryClipSegment(ref a, ref b, maxPlane, 0)
                && TryClipSegment(ref a, ref b, minPlane, 0))
                result.Add(new(a, b, s.Radius));
        }
        return result;
    }

    /// <summary>
    /// Trims a segment to the half-space dot(v, normal) &gt;= offset.
    /// </summary>
    /// <param name="a">The first endpoint; moved to the plane if outside.</param>
    /// <param name="b">The second endpoint; moved to the plane if outside.</param>
    /// <param name="normal">The half-space normal.</param>
    /// <param name="offset">The half-space offset along the normal.</param>
    /// <returns>False if the segment lies fully outside the half-space.</returns>
    private static bool TryClipSegment(ref Vector4 a, ref Vector4 b, Vector4 normal, float offset)
    {
        float da = Vector4.Dot(a, normal) - offset;
        float db = Vector4.Dot(b, normal) - offset;
        if (da < 0 && db < 0)
            return false;
        if (da < 0)
            a += da / (da - db) * (b - a);
        else if (db < 0)
            b += db / (db - da) * (a - b);
        return true;
    }
}
