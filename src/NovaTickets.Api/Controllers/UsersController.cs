using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NovaTickets.Api.Contracts;
using NovaTickets.Api.Data;
using NovaTickets.Api.Domain;
using NovaTickets.Api.Infrastructure;
using NovaTickets.Api.Services;

namespace NovaTickets.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public sealed class UsersController(AppDbContext db, AuditService audit, IPasswordHasher<User> passwordHasher) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.Users.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Select(x => new UserView(x.Id, x.Email, x.FullName, x.Phone, x.Role, x.IsActive)).ToListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) => Ok(await db.Users.AsNoTracking().Where(x => x.Id == id).Select(x => new UserView(x.Id, x.Email, x.FullName, x.Phone, x.Role, x.IsActive)).FirstOrDefaultAsync(cancellationToken) ?? throw new ApiException(404, "user_not_found", "Không tìm thấy người dùng."));

    [HttpPost]
    public async Task<IActionResult> Create(AdminCreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken)) throw new ApiException(409, "email_exists", "Email đã tồn tại.");
        var user = new User { Email = email, FullName = request.FullName.Trim(), Phone = request.Phone.Trim(), Role = request.Role, PasswordHash = "" };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user); await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("AdminCreate", "User", user.Id, new { user.Email, user.Role });
        return CreatedAtAction(nameof(Get), new { id = user.Id }, new UserView(user.Id, user.Email, user.FullName, user.Phone, user.Role, user.IsActive));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([id], cancellationToken) ?? throw new ApiException(404, "user_not_found", "Không tìm thấy người dùng.");
        user.Role = request.Role; user.IsActive = request.IsActive; user.UpdatedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("UpdateRoleOrStatus", "User", user.Id, new { user.Role, user.IsActive }); return Ok(new UserView(user.Id, user.Email, user.FullName, user.Phone, user.Role, user.IsActive));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([id], cancellationToken) ?? throw new ApiException(404, "user_not_found", "Không tìm thấy người dùng.");
        user.IsActive = false; user.UpdatedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Deactivate", "User", user.Id, new { user.Email }); return NoContent();
    }
}
