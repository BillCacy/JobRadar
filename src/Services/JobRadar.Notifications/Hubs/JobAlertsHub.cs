using Microsoft.AspNetCore.SignalR;

namespace JobRadar.Notifications.Hubs;

/// <summary>
/// The real-time piece: the MAUI client opens a SignalR connection here and calls Subscribe()
/// with its UserId, joining a per-user group. JobMatchedConsumer then pushes straight to that
/// group the moment Matching finds something — no polling from the client at all.
///
/// No real auth here either (see UsersController) — a production version would authenticate
/// the hub connection (JWT bearer token) and derive the user id from the token instead of
/// trusting whatever the client passes to Subscribe().
/// </summary>
public class JobAlertsHub : Hub
{
    public Task Subscribe(Guid userId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(userId));

    public static string GroupName(Guid userId) => $"user-{userId}";
}
