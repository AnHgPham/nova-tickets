using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NovaTickets.Api.Data;
using NovaTickets.Api.Services;

namespace NovaTickets.Api.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/test")]
public sealed class TestingController(AppDbContext db, ExpiredHoldCleanupService cleanup, IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("expire-and-cleanup/{holdToken}")]
    public async Task<IActionResult> ExpireAndCleanup(string holdToken, CancellationToken cancellationToken)
    {
        if (!environment.IsEnvironment("Testing")) return NotFound();
        var now = DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE `SeatInventory` SET `HoldExpiresAtUtc` = {now.AddSeconds(-1)} WHERE `HoldToken` = {holdToken}", cancellationToken);
        var released = await cleanup.ReleaseExpiredAsync(cancellationToken);
        return Ok(new { released });
    }

    [HttpPost("bookings/{id:guid}/email-failed")]
    public async Task<IActionResult> MarkEmailFailed(Guid id, CancellationToken cancellationToken)
    {
        if (!environment.IsEnvironment("Testing")) return NotFound();
        var email = await db.EmailOutbox.FirstOrDefaultAsync(x => x.BookingId == id && x.MessageType == EmailOutboxService.BookingConfirmation, cancellationToken);
        if (email is null) return NotFound();
        email.Status = NovaTickets.Api.Domain.EmailDeliveryStatus.Failed;
        email.LastError = "intentional integration-test failure";
        email.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { emailId = email.Id, status = email.Status, attemptCount = email.AttemptCount });
    }

    [HttpGet("bookings/{id:guid}/artifacts")]
    public async Task<IActionResult> BookingArtifacts(Guid id, CancellationToken cancellationToken)
    {
        if (!environment.IsEnvironment("Testing")) return NotFound();
        var booking = await db.Bookings.AsNoTracking().Include(x => x.Tickets).Include(x => x.Payments).Include(x => x.Emails).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (booking is null) return NotFound();
        var email = booking.Emails.SingleOrDefault();
        return Ok(new
        {
            ticketCount = booking.Tickets.Count,
            paymentCount = booking.Payments.Count,
            paymentIdempotencyKey = booking.Payments.SingleOrDefault()?.IdempotencyKey,
            emailCount = booking.Emails.Count,
            emailStatus = email?.Status,
            emailProvider = email?.Provider,
            emailAttemptCount = email?.AttemptCount,
            emailContainsBookingCode = email?.HtmlBody.Contains(booking.BookingCode) == true,
            emailContainsTicketCode = booking.Tickets.All(ticket => email != null && email.HtmlBody.Contains(ticket.TicketCode))
        });
    }
}
