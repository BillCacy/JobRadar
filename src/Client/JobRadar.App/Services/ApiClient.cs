using System.Net.Http.Json;
using JobRadar.Contracts.Models;

namespace JobRadar.App.Services;

// These request/response shapes intentionally mirror (but don't share code with) the DTOs
// defined in JobRadar.Users' and JobRadar.Notifications' controllers - the client owns its own
// view of the wire contract at the edge, same as any other consumer of the gateway would.
public sealed record RegisterUserRequest(string Email, string DisplayName);
public sealed record UserResponse(Guid Id, string Email, string DisplayName);

public sealed record CreateCriteriaRequest(
    string Keywords,
    string Location,
    decimal? MinSalary,
    bool RemoteOnly,
    JobTypeFilter JobType,
    List<string>? ExcludeKeywords);

public sealed record NotificationDto(
    Guid Id,
    Guid UserId,
    Guid CriteriaId,
    string JobTitle,
    string Company,
    string Location,
    string Url,
    DateTimeOffset MatchedAt,
    bool IsRead);

/// <summary>Thin typed wrapper over the gateway's REST surface. Everything goes through the
/// YARP gateway on :8080 - the client never talks to an individual backend service directly.</summary>
public sealed class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task<UserResponse> RegisterAsync(string email, string displayName, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/users", new RegisterUserRequest(email, displayName), ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserResponse>(cancellationToken: ct))!;
    }

    public async Task<List<SearchCriteria>> GetCriteriaAsync(Guid userId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/criteria");
        request.Headers.Add("X-User-Id", userId.ToString());
        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<SearchCriteria>>(cancellationToken: ct) ?? [];
    }

    public async Task CreateCriteriaAsync(Guid userId, CreateCriteriaRequest request, CancellationToken ct = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/criteria")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Add("X-User-Id", userId.ToString());
        var response = await _http.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteCriteriaAsync(Guid userId, Guid criteriaId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/criteria/{criteriaId}");
        request.Headers.Add("X-User-Id", userId.ToString());
        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<NotificationDto>> GetNotificationsAsync(Guid userId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/notifications?take=50");
        request.Headers.Add("X-User-Id", userId.ToString());
        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<NotificationDto>>(cancellationToken: ct) ?? [];
    }
}
