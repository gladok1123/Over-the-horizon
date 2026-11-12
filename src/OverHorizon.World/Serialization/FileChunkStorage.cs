using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Serialization;

/// <summary>
/// Asynchronous disk chunk storage backed by <see cref="System.IO.Pipelines"/> and atomic file replacement.
/// </summary>
public sealed class FileChunkStorage : IChunkStorage
{
    private readonly string _chunksDirectory;
    private readonly CoreRegistries _registries;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileChunkStorage"/> class.
    /// </summary>
    /// <param name="worldDirectory">Root directory for world save data.</param>
    /// <param name="registries">Frozen domain registries.</param>
    public FileChunkStorage(string worldDirectory, CoreRegistries registries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldDirectory);
        ArgumentNullException.ThrowIfNull(registries);

        _chunksDirectory = Path.Combine(worldDirectory, "chunks");
        _registries = registries;
        _ = Directory.CreateDirectory(_chunksDirectory);
    }

    /// <inheritdoc />
    public async ValueTask<ChunkColumn?> LoadChunkAsync(ChunkPos position, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var filePath = GetChunkFilePath(position);
        if (!File.Exists(filePath))
        {
            return null;
        }

        var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using (stream.ConfigureAwait(false))
        {
            var reader = PipeReader.Create(stream);
            var accumulator = new ArrayBufferWriter<byte>(4096);

            try
            {
                while (true)
                {
                    var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                    var buffer = result.Buffer;

                    foreach (var segment in buffer)
                    {
                        accumulator.Write(segment.Span);
                    }

                    reader.AdvanceTo(buffer.End);
                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            }
            finally
            {
                await reader.CompleteAsync().ConfigureAwait(false);
            }

            return ChunkNbtSerializer.Deserialize(accumulator.WrittenSpan, _registries);
        }
    }

    /// <inheritdoc />
    public async ValueTask SaveChunkAsync(ChunkColumn chunk, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        cancellationToken.ThrowIfCancellationRequested();

        var finalPath = GetChunkFilePath(chunk.Position);
        var tempPath = $"{finalPath}.{Guid.NewGuid():N}.tmp";

        var stream = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous);
        await using (stream.ConfigureAwait(false))
        {
            var writer = PipeWriter.Create(stream);
            try
            {
                ChunkNbtSerializer.Serialize(writer, chunk, _registries);
                _ = await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await writer.CompleteAsync().ConfigureAwait(false);
            }
        }

        File.Move(tempPath, finalPath, overwrite: true);
        chunk.MarkClean();
    }

    private string GetChunkFilePath(ChunkPos pos)
    {
        var fileName = string.Create(CultureInfo.InvariantCulture, $"c.{pos.X}.{pos.Z}.nbt");
        return Path.Combine(_chunksDirectory, fileName);
    }
}
