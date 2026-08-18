using JobRadar.Contracts.Events;
using JobRadar.Contracts.Models;
using JobRadar.Users.Data;
using JobRadar.Users.Models;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Users.Controllers;

public record CreateCriteriaRequest(
    string Keywords,
    string Location,
    decimal? MinSalary,
    bool RemoteOnly,
    JobTypeFilter JobType,
    List<string>? ExcludeKeywords);

[ApiController]
[Route("api/criteria")]
public class CriteriaController(UsersDbContext db, ISendEndpointProvider sendEndpointProvider) : ControllerBase
{
    // Simplified auth: the caller identifies itself with X-User-Id. See UsersController for why.
    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        return Request.Headers.TryGetValue("X-User-Id", out var raw) && Guid.TryParse(raw, out userId);
    }

    [HttpGet]
    public async Task<ActionResult<List<SearchCriteria>>> GetMine(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized("Missing or invalid X-User-Id header.");

        var mine = await db.SearchCriteria
            .Where(c => c.UserId == userId && c.IsActive)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        return Ok(mine.Select(c => c.ToContract()).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<SearchCriteria>> Create(CreateCriteriaRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized("Missing or invalid X-User-Id header.");
        if (string.IsNullOrWhiteSpace(request.Keywords))
            return BadRequest("Keywords is required.");

        var entity = new SearchCriteriaEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Keywords = request.Keywords.Trim(),
            Location = request.Location?.Trim() ?? string.Empty,
            MinSalary = request.MinSalary,
            RemoteOnly = request.RemoteOnly,
            JobType = request.JobType,
            ExcludeKeywordsCsv = string.Join(',', request.ExcludeKeywords ?? []),
            IsActive = true
        };

        db.SearchCriteria.Add(entity);
        await db.SaveChangesAsync(ct);

        // Tell JobAggregator there's a new watch to poll. It keeps its own local copy of
        // active criteria built purely from this event stream (event-carried state transfer) -
        // it never calls back into this service.
        await sendEndpointProvider.Send(new SearchCriteriaSaved(entity.ToContract()), ct);

        return CreatedAtAction(nameof(GetMine), entity.ToContract());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized("Missing or invalid X-User-Id header.");

        var entity = await db.SearchCriteria.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);
        if (entity is null)
            return NotFound();

        entity.IsActive = false;
        await db.SaveChangesAsync(ct);

        await sendEndpointProvider.Send(new SearchCriteriaDeleted(entity.Id, entity.UserId), ct);

        return NoContent();
    }
}
