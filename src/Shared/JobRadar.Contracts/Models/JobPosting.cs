namespace JobRadar.Contracts.Models;

/// <summary>
/// Normalized shape every job connector (Adzuna, Jooble, ...) maps its raw response into.
/// Nothing downstream of JobAggregator needs to know which source a posting came from.
/// </summary>
public sealed class JobPosting
{
    /// <summary>Id as assigned by the source (NOT globally unique on its own).</summary>
    public required string ExternalId { get; init; }

    /// <summary>Connector name, e.g. "adzuna" or "jooble". Combined with ExternalId this is unique.</summary>
    public required string Source { get; init; }

    public required string Title { get; init; }
    public required string Company { get; init; }
    public required string Location { get; init; }
    public string? Description { get; init; }
    public required string Url { get; init; }
    public decimal? SalaryMin { get; init; }
    public decimal? SalaryMax { get; init; }
    public bool IsRemote { get; init; }
    public string? JobType { get; init; }
    public DateTimeOffset? PostedAt { get; init; }

    /// <summary>Stable dedupe key used across the pipeline (Matching's "seen jobs" table, etc.).</summary>
    public string DedupeKey => $"{Source}:{ExternalId}";
}
