using System.Buffers;
using System.Buffers.Binary;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Serialization;

/// <summary>
/// Compact binary wire serializer and deserializer for streaming <see cref="ChunkSnapshot"/> columns over the network.
/// </summary>
public static class ChunkNetworkSerializer
{
    private const byte WireFormatVersion = 1;
    private const byte FlagHasBlocks = 1 << 0;
    private const byte FlagHasSkyLight = 1 << 1;
    private const byte FlagHasBlockLight = 1 << 2;

    /// <summary>
    /// Serializes all 24 sections (blocks, 4x4x4 biomes, sky light, and block light) of <paramref name="snapshot"/> into <paramref name="output"/>.
    /// </summary>
    /// <param name="snapshot">Immutable chunk column snapshot.</param>
    /// <param name="output">Destination buffer writer.</param>
    public static void SerializeSnapshot(ChunkSnapshot snapshot, IBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(output);

        var header = output.GetSpan(1);
        header[0] = WireFormatVersion;
        output.Advance(1);

        var sections = snapshot.Sections;
        for (var s = 0; s < ChunkColumn.SectionCount; s++)
        {
            var sec = sections[s];
            var skySpan = sec.SkyLightSpanOrEmpty;
            var blockSpan = sec.BlockLightSpanOrEmpty;

            byte flags = 0;
            if (!sec.IsOnlyAir)
            {
                flags |= FlagHasBlocks;
            }

            if (!skySpan.IsEmpty)
            {
                flags |= FlagHasSkyLight;
            }

            if (!blockSpan.IsEmpty)
            {
                flags |= FlagHasBlockLight;
            }

            // Section header: NonEmptyBlockCount (2) + Flags (1) + 64 biomes * 2 bytes (128) = 131 bytes.
            var metaSpan = output.GetSpan(3 + 128);
            BinaryPrimitives.WriteUInt16BigEndian(metaSpan[..2], sec.NonEmptyBlockCount);
            metaSpan[2] = flags;

            var offset = 3;
            for (var qy = 0; qy < 4; qy++)
            {
                for (var qz = 0; qz < 4; qz++)
                {
                    for (var qx = 0; qx < 4; qx++)
                    {
                        BinaryPrimitives.WriteUInt16BigEndian(metaSpan.Slice(offset, 2), sec.GetBiomeId(qx, qy, qz));
                        offset += 2;
                    }
                }
            }

            output.Advance(3 + 128);

            if ((flags & FlagHasBlocks) != 0)
            {
                var blockBytes = output.GetSpan(4096 * 2);
                var bOffset = 0;
                for (var ly = 0; ly < 16; ly++)
                {
                    for (var lz = 0; lz < 16; lz++)
                    {
                        for (var lx = 0; lx < 16; lx++)
                        {
                            BinaryPrimitives.WriteUInt16BigEndian(
                                blockBytes.Slice(bOffset, 2),
                                sec.GetBlockState(lx, ly, lz).Id);
                            bOffset += 2;
                        }
                    }
                }

                output.Advance(4096 * 2);
            }

            if ((flags & FlagHasSkyLight) != 0)
            {
                var destSky = output.GetSpan(2048);
                skySpan.CopyTo(destSky);
                output.Advance(2048);
            }

            if ((flags & FlagHasBlockLight) != 0)
            {
                var destBlk = output.GetSpan(2048);
                blockSpan.CopyTo(destBlk);
                output.Advance(2048);
            }
        }
    }

    /// <summary>
    /// Convenience helper returning the serialized <see cref="ChunkSnapshot"/> payload as a byte array.
    /// </summary>
    /// <param name="snapshot">Chunk snapshot to serialize.</param>
    /// <returns>Serialized payload bytes.</returns>
    public static byte[] SerializeSnapshotToArray(ChunkSnapshot snapshot)
    {
        var writer = new ArrayBufferWriter<byte>(16384);
        SerializeSnapshot(snapshot, writer);
        return writer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Reconstructs a full <see cref="ChunkColumn"/> (including heightmaps) from a network chunk payload.
    /// </summary>
    /// <param name="position">Chunk coordinates.</param>
    /// <param name="payload">Serialized section and light bytes.</param>
    /// <param name="blocks">Frozen block registry used to recompute column heightmaps.</param>
    /// <returns>Hydrated <see cref="ChunkColumn"/>.</returns>
    public static ChunkColumn DeserializeColumn(
        ChunkPos position,
        ReadOnlySpan<byte> payload,
        IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        if (payload.IsEmpty || payload[0] != WireFormatVersion)
        {
            throw new InvalidDataException("Unsupported or empty network chunk payload.");
        }

        var cursor = 1;
        var column = new ChunkColumn(position);

        for (var s = 0; s < ChunkColumn.SectionCount; s++)
        {
            EnsureRemaining(payload, cursor, 3 + 128);
            var flags = payload[cursor + 2];
            cursor += 3;

            var sec = column.GetSection(s);
            for (var qy = 0; qy < 4; qy++)
            {
                for (var qz = 0; qz < 4; qz++)
                {
                    for (var qx = 0; qx < 4; qx++)
                    {
                        var biomeId = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(cursor, 2));
                        sec.SetBiomeId(qx, qy, qz, biomeId);
                        cursor += 2;
                    }
                }
            }

            if ((flags & FlagHasBlocks) != 0)
            {
                EnsureRemaining(payload, cursor, 4096 * 2);
                for (var ly = 0; ly < 16; ly++)
                {
                    for (var lz = 0; lz < 16; lz++)
                    {
                        for (var lx = 0; lx < 16; lx++)
                        {
                            var stateId = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(cursor, 2));
                            cursor += 2;
                            if (stateId != 0)
                            {
                                _ = sec.SetBlockState(lx, ly, lz, new BlockState(stateId));
                            }
                        }
                    }
                }
            }

            if ((flags & FlagHasSkyLight) != 0)
            {
                EnsureRemaining(payload, cursor, 2048);
                UnpackNibblesIntoSkyLight(sec, payload.Slice(cursor, 2048));
                cursor += 2048;
            }

            if ((flags & FlagHasBlockLight) != 0)
            {
                EnsureRemaining(payload, cursor, 2048);
                UnpackNibblesIntoBlockLight(sec, payload.Slice(cursor, 2048));
                cursor += 2048;
            }
        }

        if (cursor != payload.Length)
        {
            throw new InvalidDataException("Trailing unexpected bytes in network chunk payload.");
        }

        column.RecomputeHeightmaps(blocks);
        column.MarkClean();
        return column;
    }

    private static void UnpackNibblesIntoSkyLight(ChunkSection sec, ReadOnlySpan<byte> nibbles)
    {
        for (var idx = 0; idx < 4096; idx++)
        {
            var packed = nibbles[idx >> 1];
            var level = (byte)((idx & 1) == 0 ? (packed & 0x0F) : ((packed >> 4) & 0x0F));
            if (level != 0)
            {
                sec.SetSkyLight(idx & 15, (idx >> 8) & 15, (idx >> 4) & 15, level);
            }
        }
    }

    private static void UnpackNibblesIntoBlockLight(ChunkSection sec, ReadOnlySpan<byte> nibbles)
    {
        for (var idx = 0; idx < 4096; idx++)
        {
            var packed = nibbles[idx >> 1];
            var level = (byte)((idx & 1) == 0 ? (packed & 0x0F) : ((packed >> 4) & 0x0F));
            if (level != 0)
            {
                sec.SetBlockLight(idx & 15, (idx >> 8) & 15, (idx >> 4) & 15, level);
            }
        }
    }

    private static void EnsureRemaining(ReadOnlySpan<byte> payload, int cursor, int needed)
    {
        if (cursor > payload.Length - needed)
        {
            throw new InvalidDataException("Truncated network chunk payload.");
        }
    }
}
