using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Nbt;
using OverHorizon.Core.Registry;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Serialization;

/// <summary>
/// Deterministic binary NBT serializer and deserializer for <see cref="ChunkColumn"/>s using symbolic block/biome palettes.
/// </summary>
public static class ChunkNbtSerializer
{
    /// <summary>
    /// Writes <paramref name="chunk"/> in binary NBT format into <paramref name="writer"/>.
    /// </summary>
    /// <param name="writer">Destination byte buffer writer.</param>
    /// <param name="chunk">Chunk column to serialize.</param>
    /// <param name="registries">Domain registries used to resolve symbolic block and biome keys.</param>
    public static void Serialize(IBufferWriter<byte> writer, ChunkColumn chunk, CoreRegistries registries)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(registries);

        var root = WriteToCompound(chunk, registries);
        NbtWriter.WriteRoot(writer, "Chunk", root);
    }

    /// <summary>
    /// Serializes <paramref name="chunk"/> into a byte array.
    /// </summary>
    /// <param name="chunk">Chunk column to serialize.</param>
    /// <param name="registries">Domain registries.</param>
    /// <returns>Serialized NBT bytes.</returns>
    public static byte[] SerializeToBytes(ChunkColumn chunk, CoreRegistries registries)
    {
        var buffer = new ArrayBufferWriter<byte>(4096);
        Serialize(buffer, chunk, registries);
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Deserializes a <see cref="ChunkColumn"/> from binary NBT <paramref name="data"/>.
    /// </summary>
    /// <param name="data">Binary NBT span.</param>
    /// <param name="registries">Domain registries used to resolve symbolic block and biome keys.</param>
    /// <returns>Reconstructed <see cref="ChunkColumn"/>.</returns>
    public static ChunkColumn Deserialize(ReadOnlySpan<byte> data, CoreRegistries registries)
    {
        ArgumentNullException.ThrowIfNull(registries);

        var (_, root) = NbtReader.ReadRoot(data);
        return ReadFromCompound(root, registries);
    }

    private static NbtCompoundTag WriteToCompound(ChunkColumn chunk, CoreRegistries registries)
    {
        var sectionTags = new List<NbtTag>(ChunkColumn.SectionCount);
        for (var s = 0; s < ChunkColumn.SectionCount; s++)
        {
            sectionTags.Add(SerializeSection(chunk.GetSection(s), registries));
        }

        return new NbtCompoundTag()
            .SetInt32("xPos", chunk.Position.X)
            .SetInt32("zPos", chunk.Position.Z)
            .SetInt64("LastModifiedTick", chunk.LastModifiedTick)
            .Set("Sections", new NbtListTag(NbtTagType.CompoundTag, sectionTags));
    }

    private static NbtCompoundTag SerializeSection(ChunkSection section, CoreRegistries registries)
    {
        var tag = new NbtCompoundTag()
            .Set("Y", new NbtByteTag(unchecked((sbyte)section.SectionY)));

        SerializeBiomes(section, registries, tag);

        if (!section.IsOnlyAir)
        {
            SerializeBlocks(section, registries.Blocks, tag);
        }

        var skySpan = section.GetSkyLightSpanOrEmpty();
        if (!skySpan.IsEmpty)
        {
            _ = tag.Set("SkyLight", new NbtByteArrayTag(skySpan));
        }

        var blockSpan = section.GetBlockLightSpanOrEmpty();
        if (!blockSpan.IsEmpty)
        {
            _ = tag.Set("BlockLight", new NbtByteArrayTag(blockSpan));
        }

        return tag;
    }

    private static void SerializeBiomes(ChunkSection section, CoreRegistries registries, NbtCompoundTag target)
    {
        var paletteList = new List<NbtTag>(4);
        var paletteMap = new Dictionary<ushort, int>(4);
        Span<int> indices = stackalloc int[ChunkSection.BiomeCapacity];

        var idx = 0;
        for (var qy = 0; qy < 4; qy++)
        {
            for (var qz = 0; qz < 4; qz++)
            {
                for (var qx = 0; qx < 4; qx++)
                {
                    var biomeId = section.GetBiomeId(qx, qy, qz);
                    if (!paletteMap.TryGetValue(biomeId, out var slot))
                    {
                        slot = paletteList.Count;
                        paletteMap[biomeId] = slot;
                        paletteList.Add(new NbtStringTag(registries.Biomes.GetKey(biomeId).ToString()));
                    }

                    indices[idx++] = slot;
                }
            }
        }

        _ = target.Set("BiomePalette", new NbtListTag(NbtTagType.StringTag, paletteList));
        if (paletteList.Count > 1)
        {
            _ = target.Set("BiomeData", new NbtIntArrayTag(indices));
        }
    }

    private static void SerializeBlocks(ChunkSection section, IBlockRegistry blocks, NbtCompoundTag target)
    {
        var paletteTags = new List<NbtTag>(16);
        var paletteMap = new Dictionary<BlockState, int>(16);
        var slotIndices = ArrayPool<int>.Shared.Rent(ChunkSection.BlockCapacity);

        try
        {
            for (var ly = 0; ly < 16; ly++)
            {
                for (var lz = 0; lz < 16; lz++)
                {
                    for (var lx = 0; lx < 16; lx++)
                    {
                        var state = section.GetBlockState(lx, ly, lz);
                        if (!paletteMap.TryGetValue(state, out var slot))
                        {
                            slot = paletteTags.Count;
                            paletteMap[state] = slot;
                            paletteTags.Add(EncodeStatePaletteEntry(blocks.GetStateMetadata(state)));
                        }

                        slotIndices[ChunkSection.GetBlockIndex(lx, ly, lz)] = slot;
                    }
                }
            }

            _ = target.Set("BlockPalette", new NbtListTag(NbtTagType.CompoundTag, paletteTags));

            if (paletteTags.Count > 1)
            {
                var bits = Math.Max(4, BitOperations.Log2((uint)(paletteTags.Count - 1)) + 1);
                var packed = new BitStorage(bits, ChunkSection.BlockCapacity);
                for (var i = 0; i < ChunkSection.BlockCapacity; i++)
                {
                    packed.Set(i, slotIndices[i]);
                }

                var longSpan = MemoryMarshal.Cast<ulong, long>(packed.RawWords);
                _ = target.SetInt32("BlockBits", bits);
                _ = target.Set("BlockData", new NbtLongArrayTag(longSpan));
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(slotIndices, clearArray: false);
        }
    }

    private static NbtCompoundTag EncodeStatePaletteEntry(BlockStateMetadata meta)
    {
        var entry = new NbtCompoundTag().SetString("Name", meta.BlockKey.ToString());
        if (meta.Properties.Count > 0)
        {
            var props = new NbtCompoundTag();
            foreach (var (k, v) in meta.Properties.OrderBy(static kv => kv.Key, StringComparer.Ordinal))
            {
                _ = props.SetString(k, v);
            }

            _ = entry.Set("Properties", props);
        }

        return entry;
    }

    private static ChunkColumn ReadFromCompound(NbtCompoundTag root, CoreRegistries registries)
    {
        if (!root.TryGet<NbtIntTag>("xPos", out var xTag) || !root.TryGet<NbtIntTag>("zPos", out var zTag))
        {
            throw new NbtFormatException("Chunk NBT is missing xPos or zPos.");
        }

        var chunk = new ChunkColumn(new ChunkPos(xTag.Value, zTag.Value));
        if (root.TryGet<NbtLongTag>("LastModifiedTick", out var tickTag))
        {
            chunk.LastModifiedTick = tickTag.Value;
        }

        if (root.TryGet<NbtListTag>("Sections", out var sectionsList))
        {
            foreach (var item in sectionsList.Items)
            {
                if (item is NbtCompoundTag secTag)
                {
                    DeserializeSection(chunk, secTag, registries);
                }
            }
        }

        chunk.RecomputeHeightmaps(registries.Blocks);
        chunk.MarkClean();
        return chunk;
    }

    private static void DeserializeSection(ChunkColumn chunk, NbtCompoundTag secTag, CoreRegistries registries)
    {
        if (!secTag.TryGet<NbtByteTag>("Y", out var yTag))
        {
            return;
        }

        var sectionIndex = yTag.Value - ChunkColumn.MinSectionY;
        if ((uint)sectionIndex >= ChunkColumn.SectionCount)
        {
            return;
        }

        var section = chunk.GetSection(sectionIndex);
        DeserializeBiomes(section, secTag, registries);
        DeserializeBlocks(section, secTag, registries.Blocks);

        var skyBytes = secTag.TryGet<NbtByteArrayTag>("SkyLight", out var skyTag)
            ? skyTag.Values.AsSpan()
            : ReadOnlySpan<byte>.Empty;

        var blockBytes = secTag.TryGet<NbtByteArrayTag>("BlockLight", out var blkTag)
            ? blkTag.Values.AsSpan()
            : ReadOnlySpan<byte>.Empty;

        section.LoadLightNibbleArrays(skyBytes, blockBytes);
    }

    private static void DeserializeBiomes(ChunkSection section, NbtCompoundTag secTag, CoreRegistries registries)
    {
        if (!secTag.TryGet<NbtListTag>("BiomePalette", out var paletteList) || paletteList.Items.IsEmpty)
        {
            return;
        }

        Span<ushort> paletteIds = stackalloc ushort[paletteList.Items.Length];
        for (var i = 0; i < paletteList.Items.Length; i++)
        {
            var keyStr = (paletteList.Items[i] as NbtStringTag)?.Value ?? string.Empty;
            var id = ResourceKey.TryParse(keyStr, out var key) ? registries.Biomes.GetId(key) : 0;
            paletteIds[i] = id >= 0 ? (ushort)id : (ushort)0;
        }

        if (paletteIds.Length == 1)
        {
            for (var qy = 0; qy < 4; qy++)
            {
                for (var qz = 0; qz < 4; qz++)
                {
                    for (var qx = 0; qx < 4; qx++)
                    {
                        section.SetBiomeId(qx, qy, qz, paletteIds[0]);
                    }
                }
            }

            return;
        }

        if (!secTag.TryGet<NbtIntArrayTag>("BiomeData", out var dataTag) ||
            dataTag.Values.Length != ChunkSection.BiomeCapacity)
        {
            throw new NbtFormatException("Malformed BiomeData in chunk section.");
        }

        var idx = 0;
        for (var qy = 0; qy < 4; qy++)
        {
            for (var qz = 0; qz < 4; qz++)
            {
                for (var qx = 0; qx < 4; qx++)
                {
                    var slot = dataTag.Values[idx++];
                    if ((uint)slot >= (uint)paletteIds.Length)
                    {
                        throw new NbtFormatException("Out-of-range biome palette index.");
                    }

                    section.SetBiomeId(qx, qy, qz, paletteIds[slot]);
                }
            }
        }
    }

    private static void DeserializeBlocks(ChunkSection section, NbtCompoundTag secTag, IBlockRegistry blocks)
    {
        if (!secTag.TryGet<NbtListTag>("BlockPalette", out var paletteList) || paletteList.Items.IsEmpty)
        {
            return;
        }

        var states = new BlockState[paletteList.Items.Length];
        for (var i = 0; i < states.Length; i++)
        {
            states[i] = DecodeStatePaletteEntry(paletteList.Items[i] as NbtCompoundTag, blocks);
        }

        if (states.Length == 1)
        {
            if (!states[0].IsAir)
            {
                for (var ly = 0; ly < 16; ly++)
                {
                    for (var lz = 0; lz < 16; lz++)
                    {
                        for (var lx = 0; lx < 16; lx++)
                        {
                            _ = section.SetBlockState(lx, ly, lz, states[0]);
                        }
                    }
                }
            }

            return;
        }

        if (!secTag.TryGet<NbtIntTag>("BlockBits", out var bitsTag) ||
            !secTag.TryGet<NbtLongArrayTag>("BlockData", out var dataTag))
        {
            throw new NbtFormatException("Missing BlockBits or BlockData for multi-entry section palette.");
        }

        var ulongSpan = MemoryMarshal.Cast<long, ulong>(dataTag.Values.AsSpan());
        var storage = new BitStorage(bitsTag.Value, ChunkSection.BlockCapacity, ulongSpan);

        for (var ly = 0; ly < 16; ly++)
        {
            for (var lz = 0; lz < 16; lz++)
            {
                for (var lx = 0; lx < 16; lx++)
                {
                    var slot = storage.Get(ChunkSection.GetBlockIndex(lx, ly, lz));
                    if ((uint)slot >= (uint)states.Length)
                    {
                        throw new NbtFormatException("Out-of-range block palette index.");
                    }

                    var state = states[slot];
                    if (!state.IsAir)
                    {
                        _ = section.SetBlockState(lx, ly, lz, state);
                    }
                }
            }
        }
    }

    private static BlockState DecodeStatePaletteEntry(NbtCompoundTag? entry, IBlockRegistry blocks)
    {
        if (entry is null || !entry.TryGet<NbtStringTag>("Name", out var nameTag))
        {
            return BlockState.Air;
        }

        if (!ResourceKey.TryParse(nameTag.Value, out var key) || !blocks.TryGet(key, out var def))
        {
            return BlockState.Air;
        }

        var state = def.DefaultState;
        if (entry.TryGet<NbtCompoundTag>("Properties", out var propsTag))
        {
            foreach (var (propName, propValTag) in propsTag.Entries)
            {
                if (propValTag is NbtStringTag strVal)
                {
                    state = blocks.WithProperty(state, propName, strVal.Value);
                }
            }
        }

        return state;
    }
}
