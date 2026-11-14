namespace OverHorizon.Modding.Commands;

/// <summary>
/// Registers and dispatches slash-commands with permission enforcement.
/// </summary>
public interface ICommandDispatcher
{
    /// <summary>Gets all registered commands sorted alphabetically by name.</summary>
    IReadOnlyList<CommandRegistration> Commands { get; }

    /// <summary>
    /// Registers a new command (throwing if a command with the same name already exists).
    /// </summary>
    /// <param name="registration">Command definition.</param>
    void Register(CommandRegistration registration);

    /// <summary>
    /// Parses and executes <paramref name="commandLine"/> (with or without leading <c>'/'</c>) on behalf of <paramref name="sender"/>.
    /// </summary>
    /// <param name="sender">Command invoker.</param>
    /// <param name="commandLine">Raw command text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the command existed, passed permission checks, and succeeded.</returns>
    ValueTask<bool> ExecuteAsync(
        CommandSender sender,
        string commandLine,
        CancellationToken cancellationToken = default);
}
