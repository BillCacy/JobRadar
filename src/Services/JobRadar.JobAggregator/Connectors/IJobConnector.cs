using JobRadar.Contracts.Models;

namespace JobRadar.JobAggregator.Connectors;

public sealed record JobSearchQuery(string Keywords, string Location, int MaxResults = 20);

/// <summary>
/// Everything JobAggregator knows about a job site. Add a new source by implementing this
/// interface and registering it in Program.cs — nothing else in the pipeline needs to change,
/// since PollActiveWatchesJob just iterates every registered IJobConnector.
/// </summary>
public interface IJobConnector
{
    /// <summary>Short lowercase name used as JobPosting.Source, e.g. "adzuna".</summary>
    string Name { get; }

    /// <summary>Whether this connector has the config it needs (API key, etc.) to actually run.</summary>
    bool IsConfigured { get; }

    Task<IReadOnlyList<JobPosting>> SearchAsync(JobSearchQuery query, CancellationToken ct);
}
