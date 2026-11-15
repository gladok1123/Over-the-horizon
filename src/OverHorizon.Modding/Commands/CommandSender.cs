using OverHorizon.Core.Math;

namespace OverHorizon.Modding.Commands;

/// <summary>
/// Describes the actor (player or server console) invoking a command.
/// </summary>
/// <param name="SenderName">Display name of the sender.</param>
/// <param name="SessionId">Network session ID (<see langword="null"/> for server console).</param>
/// <param name="Permission">Granted <see cref="PermissionLevel"/>.</param>
/// <param name="Position">Optional world position of the sender.</param>
/// <param name="Reply">Callback delivering feedback lines to the sender.</param>
public sealed record CommandSender(
    string SenderName,
    long? SessionId,
    PermissionLevel Permission,
    Vec3d? Position,
    Action<string> Reply);
