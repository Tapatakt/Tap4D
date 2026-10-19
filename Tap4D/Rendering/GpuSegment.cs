using ComputeSharp;

namespace Tap4D.Rendering;

/// <summary>
/// A segment laid out for GPU upload, in camera space, trimmed to the near plane.
/// </summary>
public struct GpuSegment
{
    /// <summary>The first endpoint.</summary>
    public Float4 Start;

    /// <summary>The second endpoint.</summary>
    public Float4 End;

    /// <summary>The cylinder radius in world units.</summary>
    public float Radius;

    /// <summary>The lower bound of the angular ana-kata coordinate (raycaster culling).</summary>
    public float MinW;

    /// <summary>The upper bound of the angular ana-kata coordinate (raycaster culling).</summary>
    public float MaxW;

    /// <summary>The lower bound of the angular X coordinate (raycaster culling).</summary>
    public float MinX;

    /// <summary>The upper bound of the angular X coordinate (raycaster culling).</summary>
    public float MaxX;

    /// <summary>The lower bound of the angular Y coordinate (raycaster culling).</summary>
    public float MinY;

    /// <summary>The upper bound of the angular Y coordinate (raycaster culling).</summary>
    public float MaxY;

    /// <summary>Padding for the HLSL 16-byte struct alignment.</summary>
    public float Padding0;
}