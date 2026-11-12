namespace OverHorizon.Modding.Commands;

/// <summary>
/// Parsed command execution context passed to command handlers.
/// </summary>
/// <param name="Sender">Actor invoking the command.</param>
/// <param name="CommandName">Lowercase command name without leading slash.</param>
/// <param name="Arguments">Whitespace-delimited command arguments.</param>
public sealed record CommandInvocation(
    CommandSender Sender,
    string CommandName,
    IReadOnlyList<string> Arguments);
