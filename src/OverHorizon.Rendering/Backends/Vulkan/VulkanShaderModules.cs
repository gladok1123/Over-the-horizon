using System.Reflection;
using Silk.NET.Vulkan;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// Loads the precompiled SPIR-V modules embedded in this assembly and turns them into Vulkan shader modules.
/// Nothing here parses or compiles GLSL: the binaries were produced ahead of time by the shader compiler tool,
/// so the game never pays for shader compilation while it is running.
/// </summary>
internal sealed unsafe class VulkanShaderModules : IDisposable
{
    private const string ResourcePrefix = "OverHorizon.Rendering.Shaders.";

    private readonly Vk _vk;
    private readonly Device _device;
    private readonly Dictionary<string, ShaderModule> _modules = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanShaderModules"/> class and creates every module.
    /// </summary>
    /// <param name="vk">Vulkan API.</param>
    /// <param name="device">Logical device.</param>
    public VulkanShaderModules(Vk vk, Device device)
    {
        _vk = vk;
        _device = device;

        foreach (var name in ModuleNames.Names)
        {
            _modules[name] = CreateModule(name);
        }
    }

    /// <summary>Gets the names of every shader module that must be present.</summary>
    public static IReadOnlyList<string> Names => ModuleNames.All;

    /// <summary>
    /// Gets the shader module for one precompiled SPIR-V binary.
    /// </summary>
    /// <param name="name">Resource file name, for example <c>chunk.vert.spv</c>.</param>
    /// <returns>The created shader module.</returns>
    public ShaderModule Get(string name) =>
        _modules.TryGetValue(name, out var module)
            ? module
            : throw new InvalidOperationException($"SPIR-V module '{name}' was never loaded.");

    /// <summary>
    /// Reads one precompiled SPIR-V binary out of the assembly manifest.
    /// </summary>
    /// <param name="name">Resource file name, for example <c>chunk.vert.spv</c>.</param>
    /// <returns>The raw SPIR-V words.</returns>
    public static byte[] LoadSpirV(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + name)
            ?? throw new InvalidOperationException(
                $"Precompiled shader '{name}' is missing from the rendering assembly. Run the shader compiler tool.");

        var buffer = new byte[stream.Length];
        stream.ReadExactly(buffer);
        return buffer;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var module in _modules.Values)
        {
            _vk.DestroyShaderModule(_device, module, null);
        }

        _modules.Clear();
    }

    private ShaderModule CreateModule(string name)
    {
        var spirv = LoadSpirV(name);
        fixed (byte* code = spirv)
        {
            var createInfo = new ShaderModuleCreateInfo
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)spirv.Length,
                PCode = (uint*)code,
            };

            ShaderModule module;
            VulkanResult.Ensure(
                _vk.CreateShaderModule(_device, in createInfo, null, out module),
                $"vkCreateShaderModule({name})");

            return module;
        }
    }

    private static class ModuleNames
    {
        public static readonly string[] All =
        [
            "chunk.vert.spv",
            "chunk.frag.spv",
            "overlay.vert.spv",
            "overlay.frag.spv",
            "ui.vert.spv",
            "ui.frag.spv",
        ];
    }
}
