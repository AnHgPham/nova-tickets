using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NovaTickets.Api.Data;
using NovaTickets.Api.Services;

namespace NovaTickets.Api.Controllers;

public sealed record ScheduledCleanupRequest(string? Key);

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/scheduled")]
public sealed class ScheduledMaintenanceController(AppDbContext db, ExpiredHoldCleanupService cleanup, ILogger<ScheduledMaintenanceController> logger) : ControllerBase
{
    [HttpPost("release-expired-holds")]
    public async Task<IActionResult> ReleaseExpiredHolds([FromBody] ScheduledCleanupRequest? request, CancellationToken cancellationToken)
    {
        var received = Request.Headers["X-Nova-Cleanup-Key"].FirstOrDefault() ?? request?.Key ?? Request.Query["key"].FirstOrDefault();
        var expectedHash = await db.SystemSettings.AsNoTracking().Where(x => x.Key == "scheduler_cleanup_key").Select(x => x.ValueHash).FirstOrDefaultAsync(cancellationToken);
        var receivedHash = string.IsNullOrWhiteSpace(received) ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(received))).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(expectedHash) || string.IsNullOrWhiteSpace(receivedHash) || !FixedTimeEquals(expectedHash, receivedHash))
        {
            logger.LogWarning("Rejected scheduled cleanup request.");
            return Forbid();
        }
        try
        {
            var released = await cleanup.ReleaseExpiredAsync(cancellationToken);
            return Ok(new { ok = true, released, utc = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scheduled expired-hold cleanup failed.");
            return StatusCode(500, new { error = ex.Message, timestamp = DateTime.UtcNow });
        }
    }

    private static bool FixedTimeEquals(string expected, string received) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(received));
}
