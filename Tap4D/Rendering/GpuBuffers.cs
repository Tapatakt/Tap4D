using ComputeSharp;

namespace Tap4D.Rendering;

/// <summary>
/// Manages persistent GPU buffers for the renderers.
/// </summary>
internal static class GpuBuffers
{
    /// <summary>
    /// Ensures the buffer has room for at least the given number of elements,
    /// reallocating with headroom when it does not.
    /// </summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="buffer">The buffer field to ensure.</param>
    /// <param name="count">The required element count.</param>
    /// <returns>The buffer.</returns>
    public static ReadOnlyBuffer<T> EnsureCapacity<T>(GraphicsDevice device, ref ReadOnlyBuffer<T>? buffer, int count)
        where T : unmanaged
    {
        if (buffer == null || buffer.Length < count)
        {
            buffer?.Dispose();
            buffer = device.AllocateReadOnlyBuffer<T>(Math.Max(count * 2, 64));
        }
        return buffer;
    }
}
