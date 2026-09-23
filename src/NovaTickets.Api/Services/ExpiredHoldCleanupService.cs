using Microsoft.EntityFrameworkCore;
using NovaTickets.Api.Data;
using NovaTickets.Api.Domain;

namespace NovaTickets.Api.Services;

public sealed class ExpiredHoldCleanupService(AppDbContext db, AuditService audit, ILogger<ExpiredHoldCleanupService> logger)
{
    public async Task<int> ReleaseExpiredAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var released = await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE `SeatInventory`
            SET `Status` = 'Available', `HoldToken` = NULL, `HeldByUserId` = NULL,
                `HoldExpiresAtUtc` = NULL, `Version` = `Version` + 1, `UpdatedAtUtc` = {now}
            WHERE `Status` = 'Held' AND `HoldExpiresAtUtc` IS NOT NULL AND `HoldExpiresAtUtc` < {now}", cancellationToken);
        if (released > 0)
        {
            logger.LogInformation("Released {Released} expired seat holds", released);
            await audit.WriteAsync("ReleaseExpiredHolds", nameof(SeatInventory), null, new { released, now });
        }
        return released;
    }
}
