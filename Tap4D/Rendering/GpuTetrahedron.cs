using ComputeSharp;

namespace Tap4D.Rendering;

/// <summary>
/// A tetrahedron laid out for GPU upload, in camera space, clipped to the near plane.
/// </summary>
public struct GpuTetrahedron
{
    /// <summary>The first vertex.</summary>
    public Float4 A;

    /// <summary>The second vertex.</summary>
    public Float4 B;

    /// <summary>The third vertex.</summary>
    public Float4 C;

    /// <summary>The fourth vertex.</summary>
    public Float4 D;

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

    /// <summary>Padding for the HLSL 16-byte struct alignment.</summary>
    public float Padding1;
}