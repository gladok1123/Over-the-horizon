using FluentAssertions;
using OverHorizon.Client;
using Silk.NET.Core.Loader;
using Xunit;

namespace OverHorizon.Tests;

public sealed class SingleFileNativeResolverTests
{
    [Fact]
    public void ResolveCandidatePaths_FindsWindowsLinuxAndMacNativeLibraries_AndIgnoresRootedInputs()
    {
        var tempExtractDir = Path.Combine(Path.GetTempPath(), "overhorizon-native-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempExtractDir);
        try
        {
            var glfwWin = Path.Combine(tempExtractDir, "glfw3.dll");
            var openAlWin = Path.Combine(tempExtractDir, "soft_oal.dll");
            var openAlLinux = Path.Combine(tempExtractDir, "libopenal.so.1");
            var glfwLinux = Path.Combine(tempExtractDir, "libglfw.so.3.3");
            var openAlMac = Path.Combine(tempExtractDir, "libopenal.1.dylib");

            File.WriteAllBytes(glfwWin, [0x4D, 0x5A]);
            File.WriteAllBytes(openAlWin, [0x4D, 0x5A]);
            File.WriteAllBytes(openAlLinux, [0x7F, 0x45, 0x4C, 0x46]);
            File.WriteAllBytes(glfwLinux, [0x7F, 0x45, 0x4C, 0x46]);
            File.WriteAllBytes(openAlMac, [0xCF, 0xFA, 0xED, 0xFE]);

            SilkSingleFileNativeResolver.ResolveCandidatePaths("glfw3.dll", [tempExtractDir])
                .Should().ContainSingle().Which.Should().Be(glfwWin);
            SilkSingleFileNativeResolver.ResolveCandidatePaths("soft_oal.dll", [tempExtractDir])
                .Should().ContainSingle().Which.Should().Be(openAlWin);
            SilkSingleFileNativeResolver.ResolveCandidatePaths("libopenal.so", [tempExtractDir])
                .Should().ContainSingle().Which.Should().Be(openAlLinux);
            SilkSingleFileNativeResolver.ResolveCandidatePaths("libglfw.so.3", [tempExtractDir])
                .Should().ContainSingle().Which.Should().Be(glfwLinux);
            SilkSingleFileNativeResolver.ResolveCandidatePaths("libopenal.dylib", [tempExtractDir])
                .Should().ContainSingle().Which.Should().Be(openAlMac);

            SilkSingleFileNativeResolver.ResolveCandidatePaths(glfwWin, [tempExtractDir])
                .Should().BeEmpty("already-rooted candidate paths must not be re-expanded");
        }
        finally
        {
            Directory.Delete(tempExtractDir, recursive: true);
        }
    }

    [Fact]
    public void EnsureRegistered_IsIdempotent_AndIntegratesWithSilkDefaultPathResolver()
    {
        var defaultResolver = PathResolver.Default.Should().BeOfType<DefaultPathResolver>().Subject;
        SilkSingleFileNativeResolver.EnsureRegistered();
        var countAfterFirst = defaultResolver.Resolvers.Count;
        SilkSingleFileNativeResolver.EnsureRegistered();
        defaultResolver.Resolvers.Count.Should().Be(countAfterFirst);

        var tempExtractDir = Path.Combine(Path.GetTempPath(), "overhorizon-bundle-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempExtractDir);
        var originalDirs = AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") as string;
        try
        {
            var probeFile = Path.Combine(tempExtractDir, "overhorizon_singlefile_probe.dll");
            File.WriteAllBytes(probeFile, [0x01, 0x02]);

            var updatedDirs = string.IsNullOrEmpty(originalDirs)
                ? tempExtractDir
                : tempExtractDir + Path.PathSeparator + originalDirs;
            AppContext.SetData("NATIVE_DLL_SEARCH_DIRECTORIES", updatedDirs);

            var targets = PathResolver.Default.EnumeratePossibleLibraryLoadTargets("overhorizon_singlefile_probe.dll").ToArray();
            targets.Should().Contain(probeFile);
        }
        finally
        {
            AppContext.SetData("NATIVE_DLL_SEARCH_DIRECTORIES", originalDirs);
            Directory.Delete(tempExtractDir, recursive: true);
        }
    }

    [Fact]
    public void ParseOptions_RecognizesVerifyNativesFlag()
    {
        var options = ClientBootstrap.ParseOptions(["--verify-natives"]);
        options.VerifyNativesOnly.Should().BeTrue();
    }
}
