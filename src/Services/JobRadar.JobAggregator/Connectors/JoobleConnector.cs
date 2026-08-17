using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using JobRadar.Contracts.Models;
using Microsoft.Extensions.Options;

namespace JobRadar.JobAggregator.Connectors;

public sealed class JoobleOptions
{
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Talks to the Jooble Jobs API (https://jooble.org/api/about). Free API key on request.
/// Second connector, included mainly to prove IJobConnector is genuinely pluggable — Jooble's
/// API doesn't return structured salary the way Adzuna does, so SalaryMin/Max stay null here
/// and MinSalary-filtered searches will effectively only match Adzuna postings until that's
/// improved (e.g. by parsing the free-text salary field).
/// </summary>
public sealed class JoobleConnector(HttpClient http, IOptions<JoobleOptions> options, ILogger<JoobleConnector> logger)
    : IJobConnector
{
    private readonly JoobleOptions _options = options.Value;

    public string Name => "jooble";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<IReadOnlyList<JobPosting>> SearchAsync(JobSearchQuery query, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            logger.LogWarning("Jooble connector called without an ApiKey configured — skipping.");
            return [];
        }

        try
        {
            var response = await http.PostAsJsonAsync($"api/{_options.ApiKey}", new
            {
                keywords = query.Keywords,
                location = query.Location
            }, ct);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<JoobleSearchResponse>(cancellationToken: ct);
            if (result?.Jobs is null)
                return [];

            return result.Jobs.Take(query.MaxResults).Select(MapToJobPosting).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Jooble search failed for keywords '{Keywords}'", query.Keywords);
            return [];
        }
    }

    private JobPosting MapToJobPosting(JoobleJob job)
    {
        // Jooble doesn't always return a stable numeric id — fall back to a hash of the link
        // so DedupeKey stays consistent across polls for the same posting.
        var externalId = !string.IsNullOrWhiteSpace(job.Id) ? job.Id! : HashOf(job.Link ?? job.Title ?? "");

        return new JobPosting
        {
            ExternalId = externalId,
            Source = Name,
            Title = job.Title ?? "(untitled)",
            Company = job.Company ?? "(unknown)",
            Location = job.Location ?? "",
            Description = job.Snippet,
            Url = job.Link ?? "",
            SalaryMin = null,
            SalaryMax = null,
            IsRemote = (job.Location?.Contains("remote", StringComparison.OrdinalIgnoreCase) ?? false)
                       || (job.Title?.Contains("remote", StringComparison.OrdinalIgnoreCase) ?? false),
            JobType = job.Type,
            PostedAt = DateTimeOffset.TryParse(job.Updated, out var updated) ? updated : null
        };
    }

    private static string HashOf(string input)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }

    // --- Jooble wire DTOs ---------------------------------------------------

    private sealed class JoobleSearchResponse
    {
        [JsonPropertyName("jobs")] public List<JoobleJob>? Jobs { get; set; }
    }

    private sealed class JoobleJob
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("company")] public string? Company { get; set; }
        [JsonPropertyName("location")] public string? Location { get; set; }
        [JsonPropertyName("snippet")] public string? Snippet { get; set; }
        [JsonPropertyName("link")] public string? Link { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("updated")] public string? Updated { get; set; }
    }
}
