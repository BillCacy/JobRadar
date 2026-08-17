namespace JobRadar.Matching.Models;

/// <summary>
/// One row per (user, job) pair we've already turned into a JobMatched event. Aggregator
/// re-fetches the same live postings on every poll cycle, so without this table a user would
/// get re-notified about the same job every 10 minutes.
/// </summary>
public class SeenJobEntity
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CriteriaId { get; set; }

    /// <summary>JobPosting.DedupeKey, e.g. "adzuna:123456".</summary>
    public string JobDedupeKey { get; set; } = string.Empty;

    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;
}
