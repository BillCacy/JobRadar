using JobRadar.Contracts.Models;

namespace JobRadar.Contracts.Events;

/// <summary>Published by JobRadar.Users whenever a user creates or re-activates a saved search.</summary>
public sealed record SearchCriteriaSaved(SearchCriteria Criteria);

/// <summary>Published by JobRadar.Users whenever a saved search is deleted or deactivated.</summary>
public sealed record SearchCriteriaDeleted(Guid CriteriaId, Guid UserId);
