namespace Tap4D.Rendering;

/// <summary>
/// A surface point competing for a screen pixel.
/// Overlapping candidates are sorted by depth and alternated across frames.
/// </summary>
/// <param name="depth">The camera-space Z of the point; smaller is closer.</param>
/// <param name="screenW">The ana-kata coordinate of the point on the virtual screen.</param>
/// <param name="isOccluder">True for occluders: never shown, and hide everything behind them.</param>
public readonly struct RenderCandidate(float depth, float screenW, bool isOccluder)
{
    /// <summary>The camera-space Z of the point; smaller is closer.</summary>
    public float Depth { get; } = depth;

    /// <summary>The ana-kata coordinate of the point on the virtual screen.</summary>
    public float ScreenW { get; } = screenW;

    /// <summary>True for occluders: never shown, and hide everything behind them.</summary>
    public bool IsOccluder { get; } = isOccluder;
}
