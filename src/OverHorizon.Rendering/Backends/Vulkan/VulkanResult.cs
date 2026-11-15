using Silk.NET.Vulkan;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// Turns non-success Vulkan return codes into descriptive managed exceptions so a failed GPU call is never
/// silently ignored by the render loop.
/// </summary>
internal static class VulkanResult
{
    /// <summary>
    /// Throws when <paramref name="result"/> is not <see cref="Result.Success"/>.
    /// </summary>
    /// <param name="result">Result returned by a Vulkan call.</param>
    /// <param name="operation">Name of the Vulkan call, used in the error message.</param>
    public static void Ensure(Result result, string operation)
    {
        if (result != Result.Success)
        {
            throw new InvalidOperationException($"Vulkan call '{operation}' failed with {result}.");
        }
    }
}
