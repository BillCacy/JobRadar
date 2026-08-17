using JobRadar.Contracts.Models;

namespace JobRadar.JobAggregator.Models;

/// <summary>
/// JobAggregator's own read-optimized copy of a SearchCriteria. Built entirely from consuming
/// SearchCriteriaSaved/Deleted events published by JobRadar.Users — this service never calls
/// Users' API directly. That's the "database per service" / event-carried-state-transfer
/// pattern: each service owns exactly the data it needs to do its job.
/// </summary>
public class ActiveWatchEntity
{
    /// <summary>Same value as the originating SearchCriteria.Id.</summary>
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Keywords { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal? MinSalary { get; set; }
    public bool RemoteOnly { get; set; }
    public JobTypeFilter JobType { get; set; } = JobTypeFilter.Any;
    public string ExcludeKeywordsCsv { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastPolledAt { get; set; }

    public SearchCriteria ToContract() => new()
    {
        Id = Id,
        UserId = UserId,
        Keywords = Keywords,
        Location = Location,
        MinSalary = MinSalary,
        RemoteOnly = RemoteOnly,
        JobType = JobType,
        ExcludeKeywords = ExcludeKeywordsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList(),
        IsActive = IsActive
    };
}
