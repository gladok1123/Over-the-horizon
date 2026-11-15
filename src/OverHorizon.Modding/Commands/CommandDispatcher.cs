using System.Collections.Concurrent;

namespace OverHorizon.Modding.Commands;

/// <summary>
/// Thread-safe slash-command registry and permission-enforcing dispatcher with a built-in <c>/help</c> command.
/// </summary>
public sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly ConcurrentDictionary<string, CommandRegistration> _commands = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandDispatcher"/> class with the built-in <c>help</c> command.
    /// </summary>
    public CommandDispatcher()
    {
        Register(new CommandRegistration(
            Name: "help",
            Description: "Lists available commands.",
            Usage: "/help",
            RequiredPermission: PermissionLevel.Everyone,
            Executor: (ctx, _) =>
            {
                var visible = Commands
                    .Where(c => ctx.Sender.Permission >= c.RequiredPermission)
                    .Select(static c => $"{c.Usage} - {c.Description}");
                ctx.Sender.Reply(string.Join(" | ", visible));
                return ValueTask.FromResult(true);
            }));
    }

    /// <inheritdoc />
    public IReadOnlyList<CommandRegistration> Commands =>
        _commands.Values.OrderBy(static c => c.Name, StringComparer.Ordinal).ToArray();

    /// <inheritdoc />
    public void Register(CommandRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (string.IsNullOrWhiteSpace(registration.Name))
        {
            throw new ArgumentException("Command name cannot be empty.", nameof(registration));
        }

        var normalized = registration.Name.Trim().TrimStart('/');
        var cleanReg = registration with { Name = normalized };

        if (!_commands.TryAdd(normalized, cleanReg))
        {
            throw new InvalidOperationException($"Duplicate command registration '/{normalized}'.");
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> ExecuteAsync(
        CommandSender sender,
        string commandLine,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return false;
        }

        var trimmed = commandLine.Trim();
        if (trimmed.StartsWith('/'))
        {
            trimmed = trimmed[1..].TrimStart();
        }

        if (trimmed.Length == 0)
        {
            return false;
        }

        var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmdName = tokens[0];
        var args = tokens.Length > 1 ? tokens[1..] : [];

        if (!_commands.TryGetValue(cmdName, out var registration))
        {
            sender.Reply($"Unknown command '/{cmdName}'. Type /help for a list of commands.");
            return false;
        }

        if (sender.Permission < registration.RequiredPermission)
        {
            sender.Reply($"Insufficient permissions to execute '/{cmdName}' (requires {registration.RequiredPermission}).");
            return false;
        }

        var invocation = new CommandInvocation(sender, cmdName, args);
        return await registration.Executor(invocation, cancellationToken).ConfigureAwait(false);
    }
}
