using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.Shaderc;

namespace OverHorizon.ShaderCompiler;

/// <summary>
/// Build-time GLSL to SPIR-V compiler for the Vulkan render backend.
/// <para>
/// Every shader in the rendering project is compiled <b>before</b> the game is built, so the shipped game
/// only ever loads finished SPIR-V binaries: no shader compiler runs while the player is playing, and the
/// runtime never depends on a GLSL front end.
/// </para>
/// </summary>
internal static class Program
{
    /// <summary>
    /// Entry point.
    /// </summary>
    /// <param name="args">[0] = input shader directory, [1] = output SPIR-V directory.</param>
    /// <returns><c>0</c> on success, <c>2</c> on a usage error.</returns>
    private static unsafe int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: OverHorizon.ShaderCompiler <shader-input-dir> <spirv-output-dir>");
            return 2;
        }

        var inputDir = Path.GetFullPath(args[0]);
        var outputDir = Path.GetFullPath(args[1]);

        if (!Directory.Exists(inputDir))
        {
            Console.Error.WriteLine($"shader input directory '{inputDir}' does not exist");
            return 2;
        }

        Directory.CreateDirectory(outputDir);

        var api = Shaderc.GetApi();
        var compiler = api.CompilerInitialize();
        var options = api.CompileOptionsInitialize();
        var compiled = 0;

        try
        {
            api.CompileOptionsSetSourceLanguage(options, SourceLanguage.Glsl);
            api.CompileOptionsSetTargetEnv(options, TargetEnv.Vulkan, (uint)EnvVersion.Vulkan11);
            api.CompileOptionsSetTargetSpirv(options, SpirvVersion.Shaderc13);
            api.CompileOptionsSetOptimizationLevel(options, OptimizationLevel.Performance);

            foreach (var file in Directory.EnumerateFiles(inputDir).OrderBy(static f => f, StringComparer.Ordinal))
            {
                var kind = ShaderKindFor(Path.GetExtension(file));
                if (kind is null)
                {
                    continue;
                }

                var source = File.ReadAllText(file);
                var spirv = Compile(api, compiler, options, source, Path.GetFileName(file), kind.Value);
                var destination = Path.Combine(outputDir, Path.GetFileName(file) + ".spv");
                var unchanged = File.Exists(destination) && File.ReadAllBytes(destination).AsSpan().SequenceEqual(spirv);
                if (!unchanged)
                {
                    WriteAtomically(destination, spirv);
                    Console.Out.WriteLine($"[ShaderCompiler] {Path.GetFileName(file)} -> {spirv.Length} bytes of SPIR-V");
                }

                compiled++;
            }
        }
        finally
        {
            api.CompileOptionsRelease(options);
            api.CompilerRelease(compiler);
        }

        if (compiled == 0)
        {
            Console.Error.WriteLine($"no .vert/.frag shaders found in '{inputDir}'");
            return 2;
        }

        return 0;
    }

    private static ShaderKind? ShaderKindFor(string extension) => extension switch
    {
        ".vert" => ShaderKind.VertexShader,
        ".frag" => ShaderKind.FragmentShader,
        _ => null,
    };

    private static unsafe byte[] Compile(
        Shaderc api,
        Compiler* compiler,
        CompileOptions* options,
        string source,
        string fileName,
        ShaderKind kind)
    {
        var sourceBytes = Encoding.UTF8.GetBytes(source);
        fixed (byte* sourcePtr = sourceBytes)
        {
            var result = api.CompileIntoSpv(
                compiler,
                sourcePtr,
                (nuint)sourceBytes.Length,
                kind,
                fileName,
                "main",
                options);

            try
            {
                var status = api.ResultGetCompilationStatus(result);
                if (status != CompilationStatus.Success)
                {
                    var message = Marshal.PtrToStringUTF8((nint)api.ResultGetErrorMessage(result));
                    throw new InvalidOperationException($"shader '{fileName}' failed to compile: {message}");
                }

                var length = (int)api.ResultGetLength(result);
                if (length <= 0)
                {
                    throw new InvalidOperationException($"shader '{fileName}' produced an empty SPIR-V module");
                }

                var spirv = new byte[length];
                fixed (byte* destination = spirv)
                {
                    Buffer.MemoryCopy(api.ResultGetBytes(result), destination, length, length);
                }

                return spirv;
            }
            finally
            {
                api.ResultRelease(result);
            }
        }
    }

    private static void WriteAtomically(string destination, byte[] contents)
    {
        var temporary = destination + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(contents);
        }

        File.Move(temporary, destination, overwrite: true);
    }
}
