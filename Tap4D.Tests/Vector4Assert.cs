using System.Numerics;

namespace Tap4D.Tests;

/// <summary>
/// Asserts approximate equality of <see cref="Vector4"/> values.
/// </summary>
internal static class Vector4Assert
{
    /// <summary>
    /// Asserts that two vectors are equal within a tolerance.
    /// </summary>
    /// <param name="expected">The expected vector.</param>
    /// <param name="actual">The actual vector.</param>
    /// <param name="tolerance">The maximum allowed distance between the vectors.</param>
    public static void Equal(Vector4 expected, Vector4 actual, float tolerance = 1e-5f)
    {
        Assert.True(
            Vector4.DistanceSquared(expected, actual) < tolerance * tolerance,
            $"Expected {expected}, got {actual}");
    }
}
