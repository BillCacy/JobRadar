using JobRadar.Users.Data;
using JobRadar.Users.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Users.Controllers;

public record RegisterUserRequest(string Email, string DisplayName);
public record UserResponse(Guid Id, string Email, string DisplayName);

/// <summary>
/// Deliberately not "real" auth. Registering just upserts a row by email and hands back a
/// UserId; the client stores it and sends it as the X-User-Id header on every other call.
/// Swap this for ASP.NET Core Identity + JWT if this ever leaves "portfolio project" status.
/// </summary>
[ApiController]
[Route("api/users")]
public class UsersController(UsersDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<UserResponse>> Register(RegisterUserRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("Email is required.");

        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);
        if (existing is not null)
            return Ok(new UserResponse(existing.Id, existing.Email, existing.DisplayName));

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Email : request.DisplayName
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return Ok(new UserResponse(user.Id, user.Email, user.DisplayName));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Get(Guid id, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([id], ct);
        return user is null
            ? NotFound()
            : Ok(new UserResponse(user.Id, user.Email, user.DisplayName));
    }
}
