using Tap4D.Geometry;

namespace Tap4D.Scene;

/// <summary>
/// A 4D scene: the tetrahedra and segments to render.
/// </summary>
public class Scene4
{
    /// <summary>Visible tetrahedra, colored by ana-kata.</summary>
    public List<Tetrahedron> Tetrahedra { get; } = [];

    /// <summary>Occluder tetrahedra: invisible themselves, but hide everything behind them.</summary>
    public List<Tetrahedron> Occluders { get; } = [];

    /// <summary>Line segments, colored by ana-kata interpolated along their length.</summary>
    public List<Segment4> Segments { get; } = [];
}
