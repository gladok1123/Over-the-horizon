using System.Text.Json;
using OverHorizon.Core.Biomes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;
using OverHorizon.Modding.Loading;
using OverHorizon.Modding.Manifest;

namespace OverHorizon.Modding.DataPacks;

/// <summary>
/// AOT-compatible JSON datapack loader converting <see cref="DataPackBundle"/> files into <see cref="IModInitializer"/> instances.
/// </summary>
public static class DataPackLoader
{
    /// <summary>
    /// Parses a single JSON datapack document into an executable <see cref="IModInitializer"/>.
    /// </summary>
    /// <param name="json">UTF-16 JSON document string.</param>
    /// <returns>Initialized <see cref="IModInitializer"/>.</returns>
    public static IModInitializer ParseBundle(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        DataPackBundle? bundle;
        try
        {
            bundle = JsonSerializer.Deserialize(json, DataPackJsonContext.Default.DataPackBundle);
        }
        catch (JsonException ex)
        {
            throw new ModLoadException("Invalid JSON syntax in datapack bundle.", ex);
        }

        if (bundle?.Manifest is null || string.IsNullOrWhiteSpace(bundle.Manifest.Id))
        {
            throw new ModLoadException("Datapack bundle is missing required manifest.id.");
        }

        return new BundleModInitializer(bundle);
    }

    /// <summary>
    /// Discovers and parses all <c>*.datapack.json</c> bundles inside <paramref name="directoryPath"/> in alphabetical order.
    /// </summary>
    /// <param name="directoryPath">Root datapacks directory.</param>
    /// <returns>List of parsed <see cref="IModInitializer"/> packs.</returns>
    public static IReadOnlyList<IModInitializer> LoadFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!Directory.Exists(directoryPath))
        {
            return [];
        }

        var files = Directory.GetFiles(directoryPath, "*.datapack.json", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);

        var packs = new List<IModInitializer>(files.Length);
        for (var i = 0; i < files.Length; i++)
        {
            var json = File.ReadAllText(files[i]);
            packs.Add(ParseBundle(json));
        }

        return packs;
    }

    private sealed class BundleModInitializer(DataPackBundle bundle) : IModInitializer
    {
        public ModManifest Manifest => bundle.Manifest;

        public void Initialize(IModContext context)
        {
            if (bundle.Blocks is { } blocks)
            {
                for (var i = 0; i < blocks.Count; i++)
                {
                    var b = blocks[i];
                    var key = ResourceKey.Parse(b.Key);
                    var def = new BlockDefinition(
                        key,
                        isSolid: b.IsSolid,
                        isOpaque: b.IsOpaque,
                        lightEmission: Math.Min((byte)15, b.LightEmission),
                        hardness: b.Hardness);
                    _ = context.Registries.RegisterBlock(def, registerBlockItem: b.RegisterItem);
                }
            }

            if (bundle.Items is { } items)
            {
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var key = ResourceKey.Parse(item.Key);
                    ResourceKey? placedKey = string.IsNullOrWhiteSpace(item.PlacedBlockKey)
                        ? null
                        : ResourceKey.Parse(item.PlacedBlockKey);
                    _ = context.Registries.RegisterItem(
                        new ItemDefinition(key, Math.Clamp(item.MaxStackSize, 1, 64), placedKey));
                }
            }

            if (bundle.Biomes is { } biomes)
            {
                for (var i = 0; i < biomes.Count; i++)
                {
                    var bio = biomes[i];
                    var key = ResourceKey.Parse(bio.Key);
                    var surface = context.Registries.GetDefaultBlockState(ResourceKey.Parse(bio.SurfaceBlockKey));
                    var under = context.Registries.GetDefaultBlockState(ResourceKey.Parse(bio.UnderSurfaceBlockKey));
                    _ = context.Registries.RegisterBiome(
                        new BiomeDefinition(key, bio.Temperature, bio.Downfall, surface, under));
                }
            }
        }
    }
}
