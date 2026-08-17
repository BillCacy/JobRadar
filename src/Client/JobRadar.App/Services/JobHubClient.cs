using Microsoft.AspNetCore.SignalR.Client;

namespace JobRadar.App.Services;

public sealed record JobMatchedPayload(Guid Id, string JobTitle, string Company, string Location, string Url, DateTimeOffset MatchedAt);

/// <summary>
/// The real-time half of the app. Opens one persistent SignalR connection to
/// JobRadar.Notifications (proxied through the gateway), joins the caller's per-user group, and
/// raises JobMatched the instant the server pushes - no polling, no pull-to-refresh required for
/// a live match to show up while the app is open.
/// </summary>
public sealed class JobHubClient : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private Guid? _currentUserId;

    public event Action<JobMatchedPayload>? JobMatched;

    public JobHubClient(string baseUrl)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(baseUrl), "hubs/job-alerts"))
            .WithAutomaticReconnect()
            .Build();

        _connection.On<JobMatchedPayload>("JobMatched", payload => JobMatched?.Invoke(payload));

        // Automatic reconnect gets you a *new* connection id, which means the server-side group
        // membership from Subscribe() is gone too - rejoin every time or silently stop getting pushes.
        _connection.Reconnected += async _ =>
        {
            if (_currentUserId is { } id)
                await _connection.InvokeAsync("Subscribe", id);
        };
    }

    public async Task ConnectAsync(Guid userId, CancellationToken ct = default)
    {
        _currentUserId = userId;
        if (_connection.State == HubConnectionState.Disconnected)
            await _connection.StartAsync(ct);

        await _connection.InvokeAsync("Subscribe", userId, ct);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
