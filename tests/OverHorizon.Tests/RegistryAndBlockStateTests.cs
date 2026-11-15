using System.Runtime.CompilerServices;
using FluentAssertions;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;
using Xunit;

namespace OverHorizon.Tests;

public sealed class RegistryAndBlockStateTests
{
    [Fact]
    public void ResourceKey_ParsesDefaultAndExplicitNamespaces_RejectsInvalidCharacters()
    {
        var implicitNs = ResourceKey.Parse("stone");
        implicitNs.Namespace.Should().Be("overhorizon");
        implicitNs.Path.Should().Be("stone");
        implicitNs.ToString().Should().Be("overhorizon:stone");

        var explicitNs = ResourceKey.Parse("modpack:ores/tin_ore");
        explicitNs.Namespace.Should().Be("modpack");
        explicitNs.Path.Should().Be("ores/tin_ore");

        ResourceKey.TryParse("InvalidUpper:stone", out _).Should().BeFalse();
        ResourceKey.TryParse("overhorizon:", out _).Should().BeFalse();
        ResourceKey.TryParse(":stone", out _).Should().BeFalse();
    }

    [Fact]
    public void BlockState_IsTwoBytesAndSupportsInternedPropertyTransitions()
    {
        Unsafe.SizeOf<BlockState>().Should().Be(sizeof(ushort));

        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;

        blocks.IsFrozen.Should().BeTrue();
        var air = blocks.GetDefaultState(BuiltInBlocks.Air);
        air.Should().Be(BlockState.Air);
        air.IsAir.Should().BeTrue();

        var grassDefault = blocks.GetDefaultState(BuiltInBlocks.GrassBlock);
        blocks.GetStateMetadata(grassDefault).ToString().Should().Be("overhorizon:grass_block[snowy=false]");

        var snowyGrass = blocks.WithProperty(grassDefault, "snowy", "true");
        snowyGrass.Should().NotBe(grassDefault);
        blocks.GetStateMetadata(snowyGrass).ToString().Should().Be("overhorizon:grass_block[snowy=true]");

        var reverted = blocks.WithProperty(snowyGrass, "snowy", "false");
        reverted.Should().Be(grassDefault);

        blocks.GetRequired(BuiltInBlocks.Water).States.Should().HaveCount(16);
        blocks.GetRequired(BuiltInBlocks.OakLog).States.Should().HaveCount(3);
    }

    [Fact]
    public void FrozenRegistry_RejectsPostFreezeRegistration()
    {
        var blocks = BuiltInBlocks.CreateFrozen();
        var act = () => blocks.Register(new BlockDefinition(ResourceKey.OverHorizon("extra")));

        act.Should().Throw<InvalidOperationException>();
    }

    [FsCheck.Xunit.Property]
    public void ItemStack_Split_ConservesTotalItemCount(ushort rawItem, byte initialCount, byte splitAmount)
    {
        var itemId = (ushort)((rawItem % 10) + 1);
        var stack = new ItemStack(itemId, initialCount);

        var (taken, remaining) = stack.Split(splitAmount);
        (taken.Count + remaining.Count).Should().Be(stack.Count);
    }
}
