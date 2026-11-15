using BenchmarkDotNet.Attributes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Modding.Commands;
using OverHorizon.Modding.Events;

namespace OverHorizon.Benchmarks;

[MemoryDiagnoser]
public class ModdingBenchmarks
{
    private EventBus _eventBus = null!;
    private CommandDispatcher _dispatcher = null!;
    private CommandSender _consoleSender = null!;
    private long _counter;

    [GlobalSetup]
    public void Setup()
    {
        _eventBus = new EventBus();
        _ = _eventBus.Subscribe<BlockBreakEvent>(
            (ref BlockBreakEvent _) => _counter++,
            EventPriority.Low);
        _ = _eventBus.Subscribe<BlockBreakEvent>(
            (ref BlockBreakEvent _) => _counter++,
            EventPriority.Normal);
        _ = _eventBus.Subscribe<BlockBreakEvent>(
            (ref BlockBreakEvent _) => _counter++,
            EventPriority.High);
        _ = _eventBus.Subscribe<BlockBreakEvent>(
            (ref BlockBreakEvent _) => _counter++,
            EventPriority.Monitor);

        _dispatcher = new CommandDispatcher();
        _dispatcher.Register(new CommandRegistration(
            Name: "noop",
            Description: "Benchmark no-op command",
            Usage: "/noop <arg>",
            RequiredPermission: PermissionLevel.Everyone,
            Executor: static (_, _) => ValueTask.FromResult(true)));

        _consoleSender = new CommandSender("Console", null, PermissionLevel.Owner, null, static _ => { });
    }

    [Benchmark]
    public bool PublishBlockBreakEvent4Subscribers()
    {
        var ev = new BlockBreakEvent(1L, "Bench", new BlockPos(4, 64, 4), BlockState.Air);
        _eventBus.Publish(ref ev);
        return ev.IsCancelled;
    }

    [Benchmark]
    public ValueTask<bool> ExecuteRegisteredSlashCommandAsync() =>
        _dispatcher.ExecuteAsync(_consoleSender, "/noop hello");
}
