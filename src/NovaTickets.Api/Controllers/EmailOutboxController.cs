using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NovaTickets.Api.Contracts;
using NovaTickets.Api.Data;
using NovaTickets.Api.Domain;
using NovaTickets.Api.Services;

namespace NovaTickets.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class EmailOutboxController(AppDbContext db, EmailOutboxService emails) : ControllerBase
{
    [Authorize]
    [HttpGet("bookings/{bookingId:guid}/confirmation-email")]
    public Task<EmailPreview> BookingEmail(Guid bookingId, CancellationToken cancellationToken) =>
        emails.GetBookingPreviewAsync(bookingId, BookingService.CurrentUserId(User), User.IsInRole("Admin") || User.IsInRole("Staff"), cancellationToken);

    [Authorize(Roles = "Admin,Staff")]
    [HttpGet("admin/email-outbox")]
    public async Task<ActionResult<IReadOnlyList<EmailOutboxListItem>>> List([FromQuery] EmailDeliveryStatus? status, CancellationToken cancellationToken)
    {
        var query = db.EmailOutbox.AsNoTracking().Include(x => x.Booking).AsQueryable();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        var result = await query.OrderByDescending(x => x.CreatedAtUtc).Take(200)
            .Select(x => new EmailOutboxListItem(x.Id, x.BookingId, x.Booking!.BookingCode, x.ToEmail, x.Subject, x.Status, x.Provider, x.AttemptCount, x.CreatedAtUtc, x.SentAtUtc, x.LastError))
            .ToListAsync(cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = "Admin,Staff")]
    [HttpGet("admin/email-outbox/{id:guid}")]
    public Task<EmailPreview> Get(Guid id, CancellationToken cancellationToken) =>
        emails.GetPreviewAsync(id, BookingService.CurrentUserId(User), true, cancellationToken);

    [Authorize(Roles = "Admin,Staff")]
    [HttpPost("admin/email-outbox/{id:guid}/retry")]
    public async Task<ActionResult<EmailPreview>> Retry(Guid id, CancellationToken cancellationToken)
    {
        var email = await db.EmailOutbox.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (email is null) return NotFound();
        email.Status = EmailDeliveryStatus.Pending;
        email.SentAtUtc = null;
        email.LastError = null;
        await db.SaveChangesAsync(cancellationToken);
        await emails.DispatchAsync(id, cancellationToken);
        return Ok(await emails.GetPreviewAsync(id, BookingService.CurrentUserId(User), true, cancellationToken));
    }

    [Authorize(Roles = "Admin,Staff")]
    [HttpPost("admin/bookings/{bookingId:guid}/confirmation-email")]
    public async Task<ActionResult<EmailPreview>> CreateForBooking(Guid bookingId, CancellationToken cancellationToken)
    {
        var email = await emails.QueueBookingConfirmationAsync(bookingId, cancellationToken);
        return Ok(await emails.GetPreviewAsync(email.Id, BookingService.CurrentUserId(User), true, cancellationToken));
    }
}
