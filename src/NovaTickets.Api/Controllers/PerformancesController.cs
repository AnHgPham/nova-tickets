using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NovaTickets.Api.Contracts;
using NovaTickets.Api.Data;
using NovaTickets.Api.Domain;
using NovaTickets.Api.Infrastructure;
using NovaTickets.Api.Services;

namespace NovaTickets.Api.Controllers;

[ApiController]
[Route("api/performances")]
public sealed class PerformancesController(AppDbContext db, AuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? eventId, CancellationToken cancellationToken)
    {
        var query = db.Performances.AsNoTracking().Include(x => x.Event).Include(x => x.Venue).AsQueryable();
        if (eventId.HasValue) query = query.Where(x => x.EventId == eventId.Value);
        return Ok(await query.OrderBy(x => x.StartsAtUtc).Select(x => new { x.Id, x.EventId, eventTitle = x.Event!.Title, x.VenueId, venueName = x.Venue!.Name, x.StartsAtUtc, x.DoorsOpenAtUtc, x.SalesStartUtc, x.SalesEndUtc, x.Status }).ToListAsync(cancellationToken));
    }

    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
    public async Task<IActionResult> Create(PerformanceRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        if (!await db.Events.AnyAsync(x => x.Id == request.EventId, cancellationToken)) throw new ApiException(404, "event_not_found", "Không tìm thấy sự kiện.");
        var seats = await db.Seats.Where(x => x.VenueId == request.VenueId && x.IsActive).ToListAsync(cancellationToken);
        if (seats.Count == 0) throw new ApiException(409, "venue_has_no_seats", "Địa điểm chưa có ghế hoạt động.");
        var performance = new Performance { EventId = request.EventId, VenueId = request.VenueId, StartsAtUtc = request.StartsAtUtc, DoorsOpenAtUtc = request.DoorsOpenAtUtc, SalesStartUtc = request.SalesStartUtc, SalesEndUtc = request.SalesEndUtc, Status = request.Status };
        db.Performances.Add(performance);
        db.SeatInventory.AddRange(seats.Select(seat => new SeatInventory { Performance = performance, SeatId = seat.Id, Price = seat.PriceTier.Equals("VIP", StringComparison.OrdinalIgnoreCase) ? request.VipPrice : request.StandardPrice }));
        await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Create", nameof(Performance), performance.Id, new { performance.EventId, performance.StartsAtUtc }); return CreatedAtAction(nameof(List), new { eventId = request.EventId }, performance);
    }

    [Authorize(Roles = "Admin,Staff")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, PerformanceRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var performance = await db.Performances.FindAsync([id], cancellationToken) ?? throw new ApiException(404, "performance_not_found", "Không tìm thấy suất diễn.");
        if (performance.VenueId != request.VenueId && await db.SeatInventory.AnyAsync(x => x.PerformanceId == id && x.Status != SeatStatus.Available, cancellationToken)) throw new ApiException(409, "performance_has_sales", "Không thể đổi địa điểm sau khi ghế đã được giữ hoặc bán.");
        performance.EventId = request.EventId; performance.VenueId = request.VenueId; performance.StartsAtUtc = request.StartsAtUtc; performance.DoorsOpenAtUtc = request.DoorsOpenAtUtc; performance.SalesStartUtc = request.SalesStartUtc; performance.SalesEndUtc = request.SalesEndUtc; performance.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Update", nameof(Performance), performance.Id, new { performance.Status, performance.StartsAtUtc }); return Ok(performance);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var performance = await db.Performances.FindAsync([id], cancellationToken) ?? throw new ApiException(404, "performance_not_found", "Không tìm thấy suất diễn.");
        if (await db.Bookings.AnyAsync(x => x.PerformanceId == id, cancellationToken)) throw new ApiException(409, "performance_has_bookings", "Không thể xóa suất diễn đã có booking.");
        db.Performances.Remove(performance); await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Delete", nameof(Performance), id); return NoContent();
    }

    private static void Validate(PerformanceRequest request)
    {
        if (request.DoorsOpenAtUtc >= request.StartsAtUtc || request.SalesStartUtc >= request.SalesEndUtc || request.SalesEndUtc > request.StartsAtUtc)
            throw new ApiException(400, "invalid_schedule", "Mốc thời gian của suất diễn không hợp lệ.");
    }
}
