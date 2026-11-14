using System.Diagnostics;

namespace OverHorizon.Client;

/// <summary>
/// Maintains recent chat messages and multiplayer notifications with automatic HUD fade-out.
/// </summary>
internal sealed class ClientChatHistory
{
    private const int MaxEntries = 24;
    private const double HudVisibilitySeconds = 10.0;

    private readonly List<(string Text, long TimestampTicks)> _entries = new(MaxEntries);

    public void AddMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var clean = message.Length > 110 ? message[..110] : message;
        if (_entries.Count >= MaxEntries)
        {
            _entries.RemoveAt(0);
        }

        _entries.Add((clean, Stopwatch.GetTimestamp()));
    }

    public IReadOnlyList<string> GetVisibleLines(bool chatInputOpen)
    {
        if (_entries.Count == 0)
        {
            return [];
        }

        var now = Stopwatch.GetTimestamp();
        var maxLines = chatInputOpen ? 10 : 6;
        var result = new List<string>(maxLines);
        var start = Math.Max(0, _entries.Count - maxLines);

        for (var i = start; i < _entries.Count; i++)
        {
            var (text, ts) = _entries[i];
            if (chatInputOpen || Stopwatch.GetElapsedTime(ts, now).TotalSeconds <= HudVisibilitySeconds)
            {
                result.Add(text);
            }
        }

        return result;
    }
}
