using JobRadar.Contracts.Models;

namespace JobRadar.Users.Models;

/// <summary>Persistence model. Mapped to/from the shared JobRadar.Contracts.Models.SearchCriteria
/// (which is what actually travels in events and API responses) so this table can evolve
/// independently of the wire contract.</summary>
public class SearchCriteriaEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Keywords { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal? MinSalary { get; set; }
    public bool RemoteOnly { get; set; }
    public JobTypeFilter JobType { get; set; } = JobTypeFilter.Any;

    /// <summary>Stored as a comma-separated string to avoid a second EF-owned table for a v1.</summary>
    public string ExcludeKeywordsCsv { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

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
        IsActive = IsActive,
        CreatedAt = CreatedAt
    };
}
