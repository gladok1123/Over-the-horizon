using System.Globalization;
using FluentAssertions;
using OverHorizon.Core.Registry;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Textures;
using Xunit;

namespace OverHorizon.Tests;

/// <summary>
/// Guards the <c>512x512</c> block &amp; item atlas geometry (v0.11 Nether expansion) and the
/// tile-index &lt;-&gt; UV arithmetic that the GLSL chunk shader duplicates on the GPU.
/// </summary>
public sealed class V11NetherAndAtlasTests
{
    [Fact]
    public void BlockAtlas_Is512By512WithInternallyConsistentTileGeometry()
    {
        ProceduralAtlasGenerator.AtlasDimension.Should().Be(512);
        ProceduralAtlasGenerator.TileDimension.Should().Be(16);
        ProceduralAtlasGenerator.TilesPerRow.Should().Be(32);
        ProceduralAtlasGenerator.TilesPerRow.Should().Be(ProceduralAtlasGenerator.AtlasDimension / ProceduralAtlasGenerator.TileDimension);
        ProceduralAtlasGenerator.TileCount.Should().Be(1024);
        (1 << ProceduralAtlasGenerator.TilesPerRowShift).Should().Be(ProceduralAtlasGenerator.TilesPerRow);
    }

    [Fact]
    public void ChunkFragmentShader_DerivesAtlasUvFromTheCurrentTileGrid()
    {
        var mask = (ProceduralAtlasGenerator.TilesPerRow - 1).ToString(CultureInfo.InvariantCulture);
        var shift = ProceduralAtlasGenerator.TilesPerRowShift.ToString(CultureInfo.InvariantCulture);
        var dimension = ProceduralAtlasGenerator.AtlasDimension.ToString(CultureInfo.InvariantCulture);

        var shader = SilkOpenGlShaderSources.ChunkFragmentShader;
        shader.Should().Contain($"tileIdx & {mask}", "the shader must mask tile columns with the live tiles-per-row");
        shader.Should().Contain($"tileIdx >> {shift}", "the shader must shift tile rows with the live tiles-per-row shift");
        shader.Should().Contain($"/ {dimension}.0", "the shader must normalize atlas UVs by the live atlas dimension");
    }

    [Fact]
    public void GeneratedAtlas_FillsTheEnlargedPixelBufferAndResolvesDistinctTiles()
    {
        var registries = CoreRegistries.CreateDefault();
        var atlas = ProceduralAtlasGenerator.Generate(registries.Blocks);

        atlas.Width.Should().Be(ProceduralAtlasGenerator.AtlasDimension);
        atlas.Height.Should().Be(ProceduralAtlasGenerator.AtlasDimension);
        atlas.TilesPerRow.Should().Be(ProceduralAtlasGenerator.TilesPerRow);
        atlas.RgbaPixels.Length.Should().Be(ProceduralAtlasGenerator.AtlasDimension * ProceduralAtlasGenerator.AtlasDimension * 4);
    }
}
