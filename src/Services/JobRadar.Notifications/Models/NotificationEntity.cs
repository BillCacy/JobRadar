namespace JobRadar.Notifications.Models;

public class NotificationEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CriteriaId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTimeOffset MatchedAt { get; set; }
    public bool IsRead { get; set; }
}
