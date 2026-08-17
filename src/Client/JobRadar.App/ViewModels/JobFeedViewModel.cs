using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JobRadar.App.Services;

namespace JobRadar.App.ViewModels;

public sealed partial class JobFeedViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly JobHubClient _hub;
    private readonly SessionService _session;

    public ObservableCollection<NotificationDto> Notifications { get; } = new();

    [ObservableProperty]
    private bool isBusy;

    public JobFeedViewModel(ApiClient api, JobHubClient hub, SessionService session)
    {
        _api = api;
        _hub = hub;
        _session = session;
        _hub.JobMatched += OnJobMatchedLive;
    }

    private void OnJobMatchedLive(JobMatchedPayload payload)
    {
        var userId = _session.UserId ?? Guid.Empty;

        // Fires the instant the Notifications service pushes over SignalR - dispatch onto the
        // UI thread since this callback runs on a SignalR background thread.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Notifications.Insert(0, new NotificationDto(
                payload.Id, userId, Guid.Empty,
                payload.JobTitle, payload.Company, payload.Location, payload.Url, payload.MatchedAt, false));
        });
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_session.UserId is not { } userId)
            return;

        IsBusy = true;
        try
        {
            var history = await _api.GetNotificationsAsync(userId);
            Notifications.Clear();
            foreach (var notification in history)
                Notifications.Add(notification);

            await _hub.ConnectAsync(userId);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
