using System.Runtime.CompilerServices;
using FluentAssertions;
using OverHorizon.Core.Math;
using Xunit;

namespace OverHorizon.Tests;

public sealed class MathTests
{
    [Fact]
    public void Direction_OppositeTwice_ReturnsOriginalDirection()
    {
        foreach (var dir in DirectionExtensions.All)
        {
            dir.Opposite().Opposite().Should().Be(dir);
            (dir.Normal() + dir.Opposite().Normal()).Should().Be(Vec3i.Zero);
        }
    }

    [Fact]
    public void BlockPos_NegativeCoordinates_MapToFloorChunkAndPositiveLocalOffset()
    {
        var pos = new BlockPos(-1, -64, -17);

        pos.ToChunkPos().Should().Be(new ChunkPos(-1, -2));
        pos.ToSectionPos().Should().Be(new SectionPos(-1, -4, -2));
        pos.SectionLocalX.Should().Be(15);
        pos.SectionLocalY.Should().Be(0);
        pos.SectionLocalZ.Should().Be(15);
    }

    [FsCheck.Xunit.Property]
    public void BlockPos_ChunkDecomposition_ReconstructsExactWorldCoordinates(int x, int z)
    {
        var pos = new BlockPos(x, 64, z);
        var chunk = pos.ToChunkPos();

        var reconstructed = chunk.GetBlockPos(pos.SectionLocalX, 64, pos.SectionLocalZ);
        reconstructed.Should().Be(pos);
    }

    [FsCheck.Xunit.Property]
    public void BlockPos_PackAndUnpackInt64_RoundTripsWithinValidWorldBounds(short rawX, short rawY, short rawZ)
    {
        var x = rawX * 512;
        var y = System.Math.Clamp((int)rawY, -2048, 2047);
        var z = rawZ * 512;

        var original = new BlockPos(x, y, z);
        var unpacked = BlockPos.UnpackInt64(original.PackInt64());

        unpacked.Should().Be(original);
    }

    [FsCheck.Xunit.Property]
    public void ChunkPos_PackAndUnpackInt64_RoundTripsAllInt32Pairs(int x, int z)
    {
        var original = new ChunkPos(x, z);
        ChunkPos.UnpackInt64(original.PackInt64()).Should().Be(original);
    }

    [Fact]
    public void BoundingBox_IntersectsAndSweptExpansion_BehaveDeterministically()
    {
        Unsafe.SizeOf<BlockPos>().Should().Be(12);

        var unit = BoundingBox.FromBlock(new BlockPos(0, 64, 0));
        var adjacent = BoundingBox.FromBlock(new BlockPos(1, 64, 0));
        var overlapping = BoundingBox.FromEntityFeet(new Vec3d(0.5, 64.0, 0.5), 0.6, 1.8);

        unit.Intersects(adjacent).Should().BeFalse("touching faces do not overlap interiors");
        unit.Intersects(overlapping).Should().BeTrue();
        unit.Contains(new Vec3d(0.5, 64.5, 0.5)).Should().BeTrue();

        var swept = unit.ExpandTowards(new Vec3d(2.0, -1.0, 0.0));
        swept.MinX.Should().Be(0.0);
        swept.MaxX.Should().Be(3.0);
        swept.MinY.Should().Be(63.0);
        swept.MaxY.Should().Be(65.0);
    }
}
