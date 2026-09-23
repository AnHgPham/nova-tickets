using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NovaTickets.Api.Contracts;
using NovaTickets.Api.Data;
using NovaTickets.Api.Domain;
using NovaTickets.Api.Infrastructure;
using NovaTickets.Api.Services;

namespace NovaTickets.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class BookingController(AppDbContext db, BookingService bookingService, AuditService audit, ExpiredHoldCleanupService cleanup) : ControllerBase
{
    [HttpGet("performances/{performanceId:guid}/seats")]
    public async Task<IActionResult> SeatMap(Guid performanceId, CancellationToken cancellationToken)
    {
        await cleanup.ReleaseExpiredAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var seats = await db.SeatInventory.AsNoTracking().Where(x => x.PerformanceId == performanceId).Include(x => x.Seat)
            .OrderBy(x => x.Seat!.RowLabel).ThenBy(x => x.Seat!.Number)
            .Select(x => new
            {
                x.SeatId, x.Seat!.Section, x.Seat.RowLabel, x.Seat.Number, x.Seat.PositionX, x.Seat.PositionY, x.Price,
                status = x.Status == SeatStatus.Held && x.HoldExpiresAtUtc < now ? SeatStatus.Available : x.Status,
                holdExpiresAtUtc = x.Status == SeatStatus.Held && x.HoldExpiresAtUtc >= now ? x.HoldExpiresAtUtc : null
            }).ToListAsync(cancellationToken);
        if (seats.Count == 0) throw new ApiException(404, "performance_not_found", "Không tìm thấy sơ đồ ghế cho suất diễn.");
        return Ok(seats);
    }

    [Authorize]
    [EnableRateLimiting("booking")]
    [HttpPost("seat-holds")]
    public Task<HoldSeatsResponse> Hold(HoldSeatsRequest request, CancellationToken cancellationToken) => bookingService.HoldAsync(BookingService.CurrentUserId(User), request, cancellationToken);

    [Authorize]
    [EnableRateLimiting("booking")]
    [HttpDelete("seat-holds/{holdToken}")]
    public async Task<IActionResult> Release(string holdToken, CancellationToken cancellationToken)
    {
        await bookingService.ReleaseAsync(BookingService.CurrentUserId(User), holdToken, cancellationToken); return NoContent();
    }

    [Authorize]
    [EnableRateLimiting("booking")]
    [HttpPost("bookings")]
    public Task<BookingView> Confirm(ConfirmBookingRequest request, CancellationToken cancellationToken) => bookingService.ConfirmAsync(BookingService.CurrentUserId(User), request, cancellationToken);

    [Authorize]
    [HttpGet("bookings/mine")]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        var userId = BookingService.CurrentUserId(User);
        var ids = await db.Bookings.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAtUtc).Select(x => x.Id).ToListAsync(cancellationToken);
        var result = new List<BookingView>(); foreach (var id in ids) result.Add(await bookingService.GetViewAsync(id, userId, false, cancellationToken)); return Ok(result);
    }

    [Authorize]
    [HttpGet("bookings/{id:guid}")]
    public Task<BookingView> Get(Guid id, CancellationToken cancellationToken) => bookingService.GetViewAsync(id, BookingService.CurrentUserId(User), User.IsInRole("Admin"), cancellationToken);

    [Authorize(Roles = "Admin,Staff")]
    [HttpGet("admin/bookings")]
    public async Task<IActionResult> AdminList([FromQuery] BookingStatus? status, CancellationToken cancellationToken)
    {
        var query = db.Bookings.AsNoTracking().Include(x => x.User).Include(x => x.Performance)!.ThenInclude(x => x!.Event).AsQueryable();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        return Ok(await query.OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.BookingCode, x.Status, x.TotalAmount, x.CreatedAtUtc, customer = x.User!.FullName, eventTitle = x.Performance!.Event!.Title }).ToListAsync(cancellationToken));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("admin/bookings/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateBookingStatusRequest request, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.FindAsync([id], cancellationToken) ?? throw new ApiException(404, "booking_not_found", "Không tìm thấy booking.");
        booking.Status = request.Status; booking.UpdatedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("UpdateStatus", nameof(Booking), booking.Id, new { booking.Status }); return Ok(booking);
    }

    [Authorize(Roles = "Admin,Staff")]
    [HttpPut("admin/bookings/{id:guid}")]
    public async Task<IActionResult> UpdateContact(Guid id, UpdateBookingContactRequest request, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.FindAsync([id], cancellationToken) ?? throw new ApiException(404, "booking_not_found", "Không tìm thấy booking.");
        if (booking.Status is BookingStatus.Cancelled or BookingStatus.Refunded) throw new ApiException(409, "booking_locked", "Không thể cập nhật booking đã hủy hoặc hoàn tiền.");
        booking.CustomerName = request.CustomerName.Trim(); booking.CustomerEmail = request.CustomerEmail.Trim().ToLowerInvariant(); booking.CustomerPhone = request.CustomerPhone.Trim(); booking.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("UpdateContact", nameof(Booking), booking.Id); return Ok(booking);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("admin/bookings/{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.Include(x => x.Tickets).FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new ApiException(404, "booking_not_found", "Không tìm thấy booking.");
        if (booking.Status == BookingStatus.Paid) throw new ApiException(409, "refund_required", "Booking đã thanh toán phải được hoàn tiền trước khi hủy.");
        if (booking.Status == BookingStatus.Cancelled) return NoContent();
        var inventory = await db.SeatInventory.Where(x => x.BookingId == id).ToListAsync(cancellationToken);
        foreach (var seat in inventory)
        {
            seat.Status = SeatStatus.Available; seat.BookingId = null; seat.UpdatedAtUtc = DateTime.UtcNow; seat.Version++;
        }
        booking.Status = BookingStatus.Cancelled; booking.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Cancel", nameof(Booking), booking.Id, new { ReleasedSeats = inventory.Count }); return NoContent();
    }
}
