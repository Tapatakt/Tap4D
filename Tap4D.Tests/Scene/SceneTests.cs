using System.Numerics;
using Tap4D.Geometry;
using Tap4D.Scene;

namespace Tap4D.Tests.Scene;

/// <summary>
/// Tests for <see cref="Scene"/>.
/// </summary>
public class SceneTests
{
    /// <summary>
    /// Verifies that a scene can hold all three kinds of primitives.
    /// </summary>
    [Fact]
    public void SceneHoldsAllPrimitiveKinds()
    {
        Scene4 scene = new();
        scene.Tetrahedra.AddRange(Tesseract.Tetrahedra());
        scene.Occluders.Add(new Tetrahedron(Vector4.Zero, Vector4.UnitX, Vector4.UnitY, Vector4.UnitZ));
        scene.Segments.AddRange(Tesseract.Edges());
        Assert.Equal(48, scene.Tetrahedra.Count);
        Assert.Single(scene.Occluders);
        Assert.Equal(32, scene.Segments.Count);
    }
}
