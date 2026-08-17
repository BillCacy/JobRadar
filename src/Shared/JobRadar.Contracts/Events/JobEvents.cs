using JobRadar.Contracts.Models;

namespace JobRadar.Contracts.Events;

/// <summary>
/// Published by JobRadar.JobAggregator after it polls connectors for one active SearchCriteria.
/// Carries the full Criteria object so Matching never has to call back out for filter details.
/// </summary>
public sealed record JobsFetched(SearchCriteria Criteria, IReadOnlyList<JobPosting> Jobs, DateTimeOffset FetchedAt);

/// <summary>Published by JobRadar.Matching when a fetched posting passes a user's filters.</summary>
public sealed record JobMatched(Guid UserId, Guid CriteriaId, JobPosting Job, DateTimeOffset MatchedAt);
