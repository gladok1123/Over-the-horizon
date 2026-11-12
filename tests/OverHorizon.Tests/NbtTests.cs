using System.Buffers.Binary;
using FluentAssertions;
using OverHorizon.Core.Nbt;
using Xunit;

namespace OverHorizon.Tests;

public sealed class NbtTests
{
    [Fact]
    public void NbtWriterAndReader_RoundTripAllTwelveTagTypesDeterministically()
    {
        var nested = new NbtCompoundTag()
            .SetString("label", "Привет, OverHorizon!");

        var list = new NbtListTag(
            NbtTagType.IntTag,
            [new NbtIntTag(10), new NbtIntTag(20), new NbtIntTag(30)]);

        var root = new NbtCompoundTag()
            .Set("b", new NbtByteTag(-42))
            .Set("s", new NbtShortTag(12345))
            .SetInt32("i", -987654321)
            .SetInt64("l", 0x1122334455667788L)
            .Set("f", new NbtFloatTag(3.25f))
            .SetDouble("d", -128.0625)
            .Set("ba", new NbtByteArrayTag([1, 2, 3, 255]))
            .SetString("str", "chunk_section")
            .Set("list", list)
            .Set("emptyList", new NbtListTag())
            .Set("nested", nested)
            .Set("ia", new NbtIntArrayTag([-1, 0, 42]))
            .Set("la", new NbtLongArrayTag([long.MinValue, 0L, long.MaxValue]));

        var firstBytes = NbtWriter.WriteToBytes("LevelData", root);
        var (parsedName, parsedRoot) = NbtReader.ReadRoot(firstBytes);

        parsedName.Should().Be("LevelData");
        parsedRoot.Count.Should().Be(root.Count);
        parsedRoot.TryGet<NbtCompoundTag>("nested", out var parsedNested).Should().BeTrue();
        parsedNested!.TryGet<NbtStringTag>("label", out var label).Should().BeTrue();
        label!.Value.Should().Be("Привет, OverHorizon!");

        var secondBytes = NbtWriter.WriteToBytes(parsedName, parsedRoot);
        secondBytes.Should().Equal(firstBytes);
    }

    [Fact]
    public void NbtReader_RejectsOversizedLengthHeaderWithoutAllocating()
    {
        // Craft a root Compound containing a ByteArray claiming 2,000,000,000 bytes in a tiny buffer.
        var malicious = new byte[14];
        malicious[0] = (byte)NbtTagType.CompoundTag;
        BinaryPrimitives.WriteUInt16BigEndian(malicious.AsSpan(1, 2), 0); // root name ""
        malicious[3] = (byte)NbtTagType.ByteArrayTag;
        BinaryPrimitives.WriteUInt16BigEndian(malicious.AsSpan(4, 2), 1);
        malicious[6] = (byte)'x';
        BinaryPrimitives.WriteInt32BigEndian(malicious.AsSpan(7, 4), 2_000_000_000);
        malicious[11] = (byte)NbtTagType.End;

        var act = () => NbtReader.ReadRoot(malicious);
        act.Should().Throw<NbtFormatException>();
    }

    [FsCheck.Xunit.Property]
    public void NbtReader_TryReadRoot_NeverThrowsUnhandledExceptionOnArbitraryFuzzBytes(byte[]? fuzzBytes)
    {
        var input = fuzzBytes ?? [];
        var ok = NbtReader.TryReadRoot(input, out var name, out var root, maxDepth: 16);

        if (ok)
        {
            name.Should().NotBeNull();
            root.Should().NotBeNull();
        }
        else
        {
            root.Should().BeNull();
        }
    }
}
