namespace Tap4D.Scene;

/// <summary>
/// Maps ana-kata screen coordinates to colors: from pure red at <see cref="MinW"/>
/// to pure blue at <see cref="MaxW"/>. The green channel is reserved for 3D textures.
/// </summary>
public class AnaKataPalette()
{
    /// <summary>The ana-kata value shown as pure red.</summary>
    public float MinW { get; set; } = -2f;

    /// <summary>The ana-kata value shown as pure blue.</summary>
    public float MaxW { get; set; } = 2f;

    /// <summary>The constant green channel (reserved for 3D textures).</summary>
    public byte Green { get; set; }

    /// <summary>
    /// Maps an ana-kata screen coordinate to an RGB color, clamped to the palette range.
    /// </summary>
    /// <param name="screenW">The ana-kata coordinate on the virtual screen.</param>
    /// <returns>The color for the given ana-kata value.</returns>
    public (byte R, byte G, byte B) ColorFor(float screenW)
    {
        float t = Math.Clamp((screenW - MinW) / (MaxW - MinW), 0f, 1f);
        return ((byte)(255f * (1f - t)), Green, (byte)(255f * t));
    }
}
