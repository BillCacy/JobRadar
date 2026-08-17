using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JobRadar.Contracts.Models;
using Microsoft.Extensions.Options;

namespace JobRadar.JobAggregator.Connectors;

public sealed class AdzunaOptions
{
    public string AppId { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;

    /// <summary>Adzuna scopes search by country code: us, gb, ca, au, de, ... </summary>
    public string Country { get; set; } = "us";
}

/// <summary>
/// Talks to the Adzuna Search API (https://developer.adzuna.com/). Free tier: sign up for
/// an app_id/app_key, ~250 calls/day at time of writing — plenty for a polling demo.
/// Response shape reflects Adzuna's docs; re-check them if fields come back empty, third-party
/// APIs drift over time.
/// </summary>
public sealed class AdzunaConnector(HttpClient http, IOptions<AdzunaOptions> options, ILogger<AdzunaConnector> logger)
    : IJobConnector
{
    private readonly AdzunaOptions _options = options.Value;

    public string Name => "adzuna";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.AppId) && !string.IsNullOrWhiteSpace(_options.AppKey);

    public async Task<IReadOnlyList<JobPosting>> SearchAsync(JobSearchQuery query, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            logger.LogWarning("Adzuna connector called without AppId/AppKey configured — skipping.");
            return [];
        }

        var url = $"v1/api/jobs/{_options.Country}/search/1" +
                  $"?app_id={Uri.EscapeDataString(_options.AppId)}" +
                  $"&app_key={Uri.EscapeDataString(_options.AppKey)}" +
                  $"&results_per_page={query.MaxResults}" +
                  $"&what={Uri.EscapeDataString(query.Keywords)}" +
                  (string.IsNullOrWhiteSpace(query.Location) ? "" : $"&where={Uri.EscapeDataString(query.Location)}") +
                  "&content-type=application/json";

        try
        {
            var response = await http.GetFromJsonAsync<AdzunaSearchResponse>(url, ct);
            if (response?.Results is null)
                return [];

            return response.Results.Select(MapToJobPosting).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Adzuna search failed for keywords '{Keywords}'", query.Keywords);
            return [];
        }
    }

    private JobPosting MapToJobPosting(AdzunaJob job) => new()
    {
        ExternalId = job.Id ?? Guid.NewGuid().ToString(),
        Source = Name,
        Title = job.Title ?? "(untitled)",
        Company = job.Company?.DisplayName ?? "(unknown)",
        Location = job.Location?.DisplayName ?? "",
        Description = job.Description,
        Url = job.RedirectUrl ?? "",
        SalaryMin = job.SalaryMin,
        SalaryMax = job.SalaryMax,
        IsRemote = (job.Location?.DisplayName?.Contains("remote", StringComparison.OrdinalIgnoreCase) ?? false)
                   || (job.Title?.Contains("remote", StringComparison.OrdinalIgnoreCase) ?? false),
        JobType = job.ContractTime,
        PostedAt = job.Created
    };

    // --- Adzuna wire DTOs -------------------------------------------------

    private sealed class AdzunaSearchResponse
    {
        [JsonPropertyName("results")]
        public List<AdzunaJob>? Results { get; set; }
    }

    private sealed class AdzunaJob
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("company")] public AdzunaCompany? Company { get; set; }
        [JsonPropertyName("location")] public AdzunaLocation? Location { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("redirect_url")] public string? RedirectUrl { get; set; }
        [JsonPropertyName("salary_min")] public decimal? SalaryMin { get; set; }
        [JsonPropertyName("salary_max")] public decimal? SalaryMax { get; set; }
        [JsonPropertyName("contract_time")] public string? ContractTime { get; set; }
        [JsonPropertyName("created")] public DateTimeOffset? Created { get; set; }
    }

    private sealed class AdzunaCompany
    {
        [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
    }

    private sealed class AdzunaLocation
    {
        [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
    }
}
