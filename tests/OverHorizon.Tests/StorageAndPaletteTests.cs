using FluentAssertions;
using OverHorizon.Core.Blocks;
using OverHorizon.World.Storage;
using Xunit;

namespace OverHorizon.Tests;

public sealed class StorageAndPaletteTests
{
    [FsCheck.Xunit.Property]
    public void BitStorage_PreservesAllElementsAcrossBitWidths(byte rawBits, ushort seedVal)
    {
        var bits = (rawBits % 16) + 1;
        var maxVal = (1 << bits) - 1;
        const int length = 130;

        var storage = new BitStorage(bits, length);
        var expected = new int[length];

        for (var i = 0; i < length; i++)
        {
            var v = ((i * 37) + seedVal) & maxVal;
            expected[i] = v;
            storage.Set(i, v);
        }

        for (var i = 0; i < length; i++)
        {
            storage.Get(i).Should().Be(expected[i]);
        }
    }

    [Fact]
    public void PalettedContainer_PromotesSingleToIndirectToDirectAndSupportsCopyOnWrite()
    {
        var container = new PalettedContainer<BlockState>(
            capacity: 4096,
            initialValue: BlockState.Air,
            minIndirectBits: 4,
            maxIndirectBits: 8,
            directBits: 16,
            toGlobalId: static s => s.Id,
            fromGlobalId: static id => new BlockState((ushort)id));

        container.IsSingleValue.Should().BeTrue();
        container.BitsPerEntry.Should().Be(0);

        // Promote to 4-bit indirect palette.
        container.Set(0, new BlockState(1));
        container.BitsPerEntry.Should().Be(4);

        // Insert > 256 distinct block states to trigger promotion to 16-bit direct mode.
        for (ushort i = 0; i < 300; i++)
        {
            container.Set(i, new BlockState(i));
        }

        container.BitsPerEntry.Should().Be(16);

        var snapshotCopy = container.Clone();
        container.Set(10, new BlockState(999));

        snapshotCopy.Get(10).Should().Be(new BlockState(10), "cloned snapshot must be isolated from subsequent mutations");
        container.Get(10).Should().Be(new BlockState(999));

        for (ushort i = 0; i < 300; i++)
        {
            if (i != 10)
            {
                container.Get(i).Should().Be(new BlockState(i));
            }
        }
    }

    [Fact]
    public void ChunkSection_TracksNonEmptyCountAndPacksFourBitLightNibbles()
    {
        var section = new ChunkSection(sectionY: 0);
        section.IsOnlyAir.Should().BeTrue();

        var stone = new BlockState(1);
        var dirt = new BlockState(2);

        _ = section.SetBlockState(3, 4, 5, stone);
        section.NonEmptyBlockCount.Should().Be(1);

        // Overwriting non-air with non-air keeps NonEmptyBlockCount == 1.
        _ = section.SetBlockState(3, 4, 5, dirt);
        section.NonEmptyBlockCount.Should().Be(1);

        // Clearing back to air restores IsOnlyAir.
        _ = section.SetBlockState(3, 4, 5, BlockState.Air);
        section.IsOnlyAir.Should().BeTrue();

        section.SetSkyLight(0, 0, 0, 15);
        section.SetSkyLight(1, 0, 0, 6);
        section.SetBlockLight(15, 15, 15, 13);

        section.GetSkyLight(0, 0, 0).Should().Be(15);
        section.GetSkyLight(1, 0, 0).Should().Be(6);
        section.GetBlockLight(15, 15, 15).Should().Be(13);
    }
}
