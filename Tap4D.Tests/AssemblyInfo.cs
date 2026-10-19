// ComputeSharp's pipeline cache has a race when shaders are created concurrently
// on the same device (ConditionalWeakTable.Add throws), so tests run sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
