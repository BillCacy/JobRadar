using JobRadar.Notifications.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Notifications.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController(NotificationsDbContext db) : ControllerBase
{
    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        return Request.Headers.TryGetValue("X-User-Id", out var raw) && Guid.TryParse(raw, out userId);
    }

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] int take = 50, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized("Missing or invalid X-User-Id header.");

        var notifications = await db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.MatchedAt)
            .Take(take)
            .ToListAsync(ct);

        return Ok(notifications);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized("Missing or invalid X-User-Id header.");

        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);
        if (notification is null)
            return NotFound();

        notification.IsRead = true;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
