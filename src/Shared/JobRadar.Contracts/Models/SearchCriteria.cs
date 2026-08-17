namespace JobRadar.Contracts.Models;

public enum JobTypeFilter
{
    Any = 0,
    FullTime = 1,
    PartTime = 2,
    Contract = 3
}

/// <summary>
/// A user's saved search. Keywords/Location are sent straight to the job connectors as the query;
/// the rest (MinSalary, RemoteOnly, JobType, ExcludeKeywords) are applied client-side by the
/// Matching service, since most free job-connector APIs don't support that level of filtering.
/// </summary>
public sealed class SearchCriteria
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required string Keywords { get; init; }
    public required string Location { get; init; }
    public decimal? MinSalary { get; init; }
    public bool RemoteOnly { get; init; }
    public JobTypeFilter JobType { get; init; } = JobTypeFilter.Any;
    public List<string> ExcludeKeywords { get; init; } = new();
    public bool IsActive { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
