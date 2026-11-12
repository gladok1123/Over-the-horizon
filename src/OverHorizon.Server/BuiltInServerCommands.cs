using System.Globalization;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Modding.Commands;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Sessions;
using OverHorizon.World;

namespace OverHorizon.Server;

/// <summary>
/// Registers standard built-in administrative and diagnostic slash-commands (<c>/seed</c>, <c>/tp</c>, <c>/setblock</c>, <c>/op</c>).
/// </summary>
public static class BuiltInServerCommands
{
    /// <summary>
    /// Registers built-in commands into <paramref name="dispatcher"/>.
    /// </summary>
    /// <param name="dispatcher">Target command dispatcher.</param>
    /// <param name="world">World subsystem.</param>
    /// <param name="gameplay">Gameplay subsystem.</param>
    /// <param name="registries">Frozen domain registries.</param>
    /// <param name="playBridge">Player session bridge.</param>
    /// <param name="broadcasterResolver">Lazy resolver for <see cref="INetworkBroadcaster"/>.</param>
    public static void RegisterAll(
        ICommandDispatcher dispatcher,
        IWorldSubsystem world,
        IGameplaySubsystem gameplay,
        CoreRegistries registries,
        ServerPlaySessionBridge playBridge,
        Func<INetworkBroadcaster> broadcasterResolver)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(gameplay);
        ArgumentNullException.ThrowIfNull(registries);
        ArgumentNullException.ThrowIfNull(playBridge);
        ArgumentNullException.ThrowIfNull(broadcasterResolver);

        dispatcher.Register(new CommandRegistration(
            Name: "seed",
            Description: "Displays the deterministic 64-bit world seed.",
            Usage: "/seed",
            RequiredPermission: PermissionLevel.Everyone,
            Executor: (ctx, _) =>
            {
                ctx.Sender.Reply($"Seed: [{gameplay.WorldSeed}]");
                return ValueTask.FromResult(true);
            }));

        dispatcher.Register(new CommandRegistration(
            Name: "tp",
            Description: "Teleports the executing player to X Y Z.",
            Usage: "/tp <x> <y> <z>",
            RequiredPermission: PermissionLevel.GameMaster,
            Executor: (ctx, _) =>
            {
                if (ctx.Sender.SessionId is not { } sessionId)
                {
                    ctx.Sender.Reply("Only an in-world player can execute /tp without a target.");
                    return ValueTask.FromResult(false);
                }

                if (ctx.Arguments.Count != 3 ||
                    !double.TryParse(ctx.Arguments[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                    !double.TryParse(ctx.Arguments[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
                    !double.TryParse(ctx.Arguments[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                {
                    ctx.Sender.Reply("Usage: /tp <x> <y> <z>");
                    return ValueTask.FromResult(false);
                }

                var dest = new Vec3d(x, y, z);
                if (!playBridge.TryTeleportPlayer(sessionId, dest, broadcasterResolver()))
                {
                    ctx.Sender.Reply("Teleport failed: player session not active.");
                    return ValueTask.FromResult(false);
                }

                ctx.Sender.Reply(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Teleported {ctx.Sender.SenderName} to ({x:F1}, {y:F1}, {z:F1})."));
                return ValueTask.FromResult(true);
            }));

        dispatcher.Register(new CommandRegistration(
            Name: "setblock",
            Description: "Sets the block at X Y Z to a registered block key.",
            Usage: "/setblock <x> <y> <z> <namespace:block>",
            RequiredPermission: PermissionLevel.GameMaster,
            Executor: (ctx, _) =>
            {
                if (ctx.Arguments.Count != 4 ||
                    !int.TryParse(ctx.Arguments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
                    !int.TryParse(ctx.Arguments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
                    !int.TryParse(ctx.Arguments[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var z))
                {
                    ctx.Sender.Reply("Usage: /setblock <x> <y> <z> <namespace:block>");
                    return ValueTask.FromResult(false);
                }

                ResourceKey key;
                try
                {
                    key = ResourceKey.Parse(ctx.Arguments[3]);
                }
                catch (FormatException)
                {
                    ctx.Sender.Reply($"Invalid block key '{ctx.Arguments[3]}'.");
                    return ValueTask.FromResult(false);
                }

                if (!registries.Blocks.TryGet(key, out var blockDef))
                {
                    ctx.Sender.Reply($"Unknown block '{key}'.");
                    return ValueTask.FromResult(false);
                }

                var pos = new BlockPos(x, y, z);
                if (!world.SetBlockState(pos, blockDef.DefaultState))
                {
                    ctx.Sender.Reply("Target chunk is not loaded or Y is out of bounds.");
                    return ValueTask.FromResult(false);
                }

                broadcasterResolver().BroadcastToPlaySessions(new BlockUpdatePacket(pos, blockDef.DefaultState.Id));
                ctx.Sender.Reply($"Set block at ({x}, {y}, {z}) to {key}.");
                return ValueTask.FromResult(true);
            }));

        dispatcher.Register(new CommandRegistration(
            Name: "op",
            Description: "Grants Admin permission to an online player.",
            Usage: "/op <username>",
            RequiredPermission: PermissionLevel.Admin,
            Executor: (ctx, _) =>
            {
                if (ctx.Arguments.Count != 1)
                {
                    ctx.Sender.Reply("Usage: /op <username>");
                    return ValueTask.FromResult(false);
                }

                var targetName = ctx.Arguments[0];
                if (!playBridge.TrySetPlayerPermission(targetName, PermissionLevel.Admin))
                {
                    ctx.Sender.Reply($"Player '{targetName}' is not online.");
                    return ValueTask.FromResult(false);
                }

                ctx.Sender.Reply($"Made {targetName} a server operator.");
                return ValueTask.FromResult(true);
            }));
    }
}
