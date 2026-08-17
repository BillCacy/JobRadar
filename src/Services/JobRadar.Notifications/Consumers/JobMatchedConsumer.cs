using JobRadar.Contracts.Events;
using JobRadar.Notifications.Data;
using JobRadar.Notifications.Hubs;
using JobRadar.Notifications.Models;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace JobRadar.Notifications.Consumers;

public class JobMatchedConsumer(
    NotificationsDbContext db,
    IHubContext<JobAlertsHub> hub,
    ILogger<JobMatchedConsumer> logger) : IConsumer<JobMatched>
{
    public async Task Consume(ConsumeContext<JobMatched> context)
    {
        var msg = context.Message;

        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = msg.UserId,
            CriteriaId = msg.CriteriaId,
            JobTitle = msg.Job.Title,
            Company = msg.Job.Company,
            Location = msg.Job.Location,
            Url = msg.Job.Url,
            MatchedAt = msg.MatchedAt
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync(context.CancellationToken);

        // Live push. If the user isn't connected right now this is a no-op for SignalR, but the
        // row above means GET /api/notifications will still show it next time they open the app.
        await hub.Clients.Group(JobAlertsHub.GroupName(msg.UserId)).SendAsync("JobMatched", new
        {
            notification.Id,
            notification.JobTitle,
            notification.Company,
            notification.Location,
            notification.Url,
            notification.MatchedAt
        }, context.CancellationToken);

        logger.LogInformation("Notified user {UserId} about '{Title}' at {Company}",
            msg.UserId, msg.Job.Title, msg.Job.Company);
    }
}
