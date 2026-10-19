using ComputeSharp;

namespace Tap4D.Rendering.Raycast;

/// <summary>
/// Pass 2 of the raycasting renderer: one thread per screen pixel. Walks the voxel
/// buffer slices in ana-kata order and shows the hit slice for the current frame.
/// The color comes from the ana-kata coordinate of the hit point (the hit depth times
/// the ray's ana-kata direction component), not from the slice's screen coordinate,
/// and the brightness is scaled by the coverage fraction.
/// </summary>
/// <remarks>
/// Creates the shader with all captured values.
/// </remarks>
[ThreadGroupSize(64, 1, 1)]
[GeneratedComputeShaderDescriptor]
public readonly partial struct CompositeShader(
    ReadWriteBuffer<float> depths,
    ReadWriteBuffer<float> coverage,
    ReadWriteBuffer<int> output,
    int sliceCount, int frameIndex,
    float minW, float sliceWidth, float focal,
    float paletteMinW, float invPaletteRange, int green) : IComputeShader
{
    private readonly ReadWriteBuffer<float> depths = depths;
    private readonly ReadWriteBuffer<float> coverage = coverage;
    private readonly ReadWriteBuffer<int> output = output;
    private readonly int sliceCount = sliceCount;
    private readonly int frameIndex = frameIndex;
    private readonly float minW = minW;
    private readonly float sliceWidth = sliceWidth;
    private readonly float focal = focal;
    private readonly float paletteMinW = paletteMinW;
    private readonly float invPaletteRange = invPaletteRange;
    private readonly int green = green;


    /// <summary>
    /// Composites one pixel.
    /// </summary>
    public void Execute()
    {
        int baseVoxel = ThreadIds.X * sliceCount;
        int count = 0;
        for (int s = 0; s < sliceCount; s++)
            if (depths[baseVoxel + s] > 0)
                count++;
        if (count == 0)
        {
            output[ThreadIds.X] = 0;
            return;
        }
        int wanted = frameIndex % count;
        int index = 0;
        int chosen = -1;
        for (int s = 0; s < sliceCount; s++)
            if (depths[baseVoxel + s] > 0 && index++ == wanted)
                chosen = s;
        if (chosen < 0)
        {
            output[ThreadIds.X] = 0;
            return;
        }
        float sliceW = minW + (chosen + 0.5f) * sliceWidth;
        float hitW = depths[baseVoxel + chosen] * sliceW / focal;
        float t = Hlsl.Clamp((hitW - paletteMinW) * invPaletteRange, 0f, 1f);
        float bright = coverage[baseVoxel + chosen];
        int r = (int)(255f * (1f - t) * bright);
        int b = (int)(255f * t * bright);
        int g = (int)(green * bright);
        output[ThreadIds.X] = (255 << 24) | (r << 16) | (g << 8) | b;
    }
}
