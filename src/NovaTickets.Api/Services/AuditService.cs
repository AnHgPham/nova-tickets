using System.Security.Claims;
using System.Text.Json;
using NovaTickets.Api.Data;
using NovaTickets.Api.Domain;

namespace NovaTickets.Api.Services;

public sealed class AuditService(AppDbContext db, IHttpContextAccessor accessor)
{
    public async Task WriteAsync(string action, string entityType, object? entityId = null, object? data = null)
    {
        var context = accessor.HttpContext;
        Guid? userId = Guid.TryParse(context?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context?.User.FindFirstValue("sub"), out var parsed) ? parsed : null;
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId?.ToString(),
            DataJson = data is null ? null : JsonSerializer.Serialize(data),
            IpAddress = context?.Connection.RemoteIpAddress?.ToString()
        });
        await db.SaveChangesAsync();
    }
}
