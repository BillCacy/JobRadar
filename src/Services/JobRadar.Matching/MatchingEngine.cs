using JobRadar.Contracts.Models;

namespace JobRadar.Matching;

/// <summary>
/// Pure, easily-unit-testable filter logic. Keywords/Location are already applied upstream
/// (they're what got sent to the connector as the search query) — this only applies the
/// finer-grained filters most free job APIs don't support server-side.
/// </summary>
public static class MatchingEngine
{
    public static bool IsMatch(SearchCriteria criteria, JobPosting job)
    {
        if (criteria.RemoteOnly && !job.IsRemote)
            return false;

        if (criteria.MinSalary is { } minSalary)
        {
            var jobSalary = job.SalaryMax ?? job.SalaryMin;
            // If the source didn't give us salary data at all (e.g. Jooble), we can't filter
            // on it — let it through rather than silently dropping every unsalaried posting.
            if (jobSalary is not null && jobSalary < minSalary)
                return false;
        }

        if (criteria.JobType != JobTypeFilter.Any && !string.IsNullOrWhiteSpace(job.JobType))
        {
            if (!JobTypeMatches(criteria.JobType, job.JobType))
                return false;
        }

        if (criteria.ExcludeKeywords.Count > 0)
        {
            var haystack = $"{job.Title} {job.Description}";
            if (criteria.ExcludeKeywords.Any(kw =>
                    !string.IsNullOrWhiteSpace(kw) && haystack.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        return true;
    }

    private static bool JobTypeMatches(JobTypeFilter wanted, string raw)
    {
        var normalized = raw.Replace("_", " ").Replace("-", " ").Trim().ToLowerInvariant();
        return wanted switch
        {
            JobTypeFilter.FullTime => normalized.Contains("full"),
            JobTypeFilter.PartTime => normalized.Contains("part"),
            JobTypeFilter.Contract => normalized.Contains("contract") || normalized.Contains("temp"),
            _ => true
        };
    }
}
