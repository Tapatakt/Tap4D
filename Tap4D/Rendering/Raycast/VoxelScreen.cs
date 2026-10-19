using System.Numerics;

namespace Tap4D.Rendering.Raycast;

/// <summary>
/// The spherical virtual screen of the raycasting renderer: every voxel (x, y, slice)
/// corresponds to one ray from the camera origin. Direction coordinates on the sphere
/// are (pixelX, pixelY, sqrt(focal² - pixelX² - pixelY² - w²), w) / focal.
/// </summary>
/// <remarks>
/// Creates the voxel screen geometry.
/// </remarks>
/// <param name="width">The image width in pixels.</param>
/// <param name="height">The image height in pixels.</param>
/// <param name="viewHeight">The height of the virtual screen in world units.</param>
/// <param name="focal">The focal distance (sphere radius) of the camera.</param>
/// <param name="sliceCount">The number of ana-kata slices.</param>
/// <param name="minW">The lower ana-kata bound of the screen.</param>
/// <param name="maxW">The upper ana-kata bound of the screen.</param>
public class VoxelScreen(int width, int height, float viewHeight, float focal, int sliceCount, float minW, float maxW)
{

    /// <summary>The image width in pixels.</summary>
    public int Width { get; } = width;

    /// <summary>The image height in pixels.</summary>
    public int Height { get; } = height;

    /// <summary>The height of the virtual screen in world units.</summary>
    public float ViewHeight { get; } = viewHeight;

    /// <summary>The focal distance (sphere radius) of the camera.</summary>
    public float Focal { get; } = focal;

    /// <summary>The number of ana-kata slices.</summary>
    public int SliceCount { get; } = sliceCount;

    /// <summary>The lower ana-kata bound of the screen.</summary>
    public float MinW { get; } = minW;

    /// <summary>The upper ana-kata bound of the screen.</summary>
    public float MaxW { get; } = maxW;

    /// <summary>The width of one ana-kata slice.</summary>
    public float SliceWidth => (MaxW - MinW) / SliceCount;

    /// <summary>The X coordinate of a pixel on the virtual screen.</summary>
    /// <param name="x">The pixel column.</param>
    /// <returns>The virtual screen X coordinate of the pixel center.</returns>
    public float PixelX(int x) => (x + 0.5f - Width / 2f) * (ViewHeight / Height);

    /// <summary>The Y coordinate of a pixel on the virtual screen.</summary>
    /// <param name="y">The pixel row.</param>
    /// <returns>The virtual screen Y coordinate of the pixel center.</returns>
    public float PixelY(int y) => (Height / 2f - y - 0.5f) * (ViewHeight / Height);

    /// <summary>
    /// The ana-kata coordinate of a sub-ray within a slice: sub-rays are stratified
    /// evenly across the slice width.
    /// </summary>
    /// <param name="slice">The slice index.</param>
    /// <param name="sub">The sub-ray index within the slice.</param>
    /// <param name="subRays">The number of sub-rays per slice.</param>
    /// <returns>The ana-kata coordinate of the sub-ray.</returns>
    public float SliceW(int slice, int sub, int subRays) =>
        MinW + (slice + (sub + 0.5f) / subRays) * SliceWidth;

    /// <summary>
    /// Computes the unit direction of the ray for the given screen coordinates.
    /// </summary>
    /// <param name="pixelX">The X coordinate on the virtual screen.</param>
    /// <param name="pixelY">The Y coordinate on the virtual screen.</param>
    /// <param name="w">The ana-kata coordinate on the virtual screen.</param>
    /// <param name="direction">The unit ray direction in camera space.</param>
    /// <returns>False if the coordinates lie beyond the front hemisphere.</returns>
    public bool TryGetDirection(float pixelX, float pixelY, float w, out Vector4 direction)
    {
        float rest = Focal * Focal - pixelX * pixelX - pixelY * pixelY - w * w;
        if (rest <= 0)
        {
            direction = default;
            return false;
        }
        direction = new Vector4(pixelX, pixelY, MathF.Sqrt(rest), w) / Focal;
        return true;
    }
}
