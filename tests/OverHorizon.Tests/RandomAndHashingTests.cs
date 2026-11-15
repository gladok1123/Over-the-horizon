using FluentAssertions;
using OverHorizon.Shared.Hashing;
using OverHorizon.Shared.Random;
using Xunit;

namespace OverHorizon.Tests;

public sealed class RandomAndHashingTests
{
    [Fact]
    public void Xoroshiro128PlusPlus_ProducesIdenticalSequenceFromSameSeed()
    {
        var rngA = new Xoroshiro128PlusPlus(0UL);
        var rngB = new Xoroshiro128PlusPlus(0UL);

        for (var i = 0; i < 128; i++)
        {
            var valA = rngA.NextUInt64();
            var valB = rngB.NextUInt64();
            valA.Should().Be(valB);
        }

        rngA.NextUInt64().Should().NotBe(0UL);
    }

    [FsCheck.Xunit.Property]
    public void Xoroshiro128PlusPlus_BoundedIntAndDouble_StayInRange(ulong seed, ushort rawBound)
    {
        var bound = (rawBound % 1000) + 1;
        var rng = new Xoroshiro128PlusPlus(seed);

        var n = rng.NextInt32(bound);
        var d = rng.NextDouble();
        var f = rng.NextSingle();

        n.Should().BeInRange(0, bound - 1);
        d.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThan(1.0);
        f.Should().BeGreaterThanOrEqualTo(0.0f).And.BeLessThan(1.0f);
    }

    [Fact]
    public void Fnv1a64_NormalizesNegativeZeroAndNaNBitRepresentations()
    {
        var posZero = Fnv1a64.Create();
        posZero.AppendDouble(+0.0);

        var negZero = Fnv1a64.Create();
        negZero.AppendDouble(-0.0);

        posZero.Value.Should().Be(negZero.Value);

        var nanA = Fnv1a64.Create();
        nanA.AppendDouble(double.NaN);

        var nanB = Fnv1a64.Create();
        nanB.AppendDouble(BitConverter.Int64BitsToDouble(0x7FF8000000000123L));

        nanA.Value.Should().Be(nanB.Value);
    }
}
