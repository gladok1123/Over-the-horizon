namespace OverHorizon.Modding.Commands;

/// <summary>
/// Immutable descriptor and asynchronous handler for a server command.
/// </summary>
/// <param name="Name">Primary lowercase command name (without <c>'/'</c>).</param>
/// <param name="Description">Short help summary.</param>
/// <param name="Usage">Usage template (e.g. <c>"/tp &lt;x&gt; &lt;y&gt; &lt;z&gt;"</c>).</param>
/// <param name="RequiredPermission">Minimum <see cref="PermissionLevel"/> required.</param>
/// <param name="Executor">Asynchronous command handler returning <see langword="true"/> on success.</param>
public sealed record CommandRegistration(
    string Name,
    string Description,
    string Usage,
    PermissionLevel RequiredPermission,
    Func<CommandInvocation, CancellationToken, ValueTask<bool>> Executor);
