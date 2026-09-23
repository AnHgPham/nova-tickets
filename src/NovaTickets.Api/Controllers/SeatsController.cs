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
[Route("api/seats")]
[Authorize(Roles = "Admin,Staff")]
public sealed class SeatsController(AppDbContext db, AuditService audit) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid venueId, CancellationToken cancellationToken) => Ok(await db.Seats.AsNoTracking().Where(x => x.VenueId == venueId).OrderBy(x => x.RowLabel).ThenBy(x => x.Number).ToListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) => Ok(await db.Seats.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new ApiException(404, "seat_not_found", "Không tìm thấy ghế."));

    [HttpPost]
    public async Task<IActionResult> Create(SeatRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Venues.AnyAsync(x => x.Id == request.VenueId, cancellationToken)) throw new ApiException(404, "venue_not_found", "Không tìm thấy địa điểm.");
        var seat = new Seat { VenueId = request.VenueId, Section = request.Section.Trim(), RowLabel = request.RowLabel.Trim().ToUpperInvariant(), Number = request.Number, PositionX = request.PositionX, PositionY = request.PositionY, PriceTier = request.PriceTier.Trim(), IsActive = request.IsActive };
        db.Seats.Add(seat); await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Create", nameof(Seat), seat.Id, new { seat.RowLabel, seat.Number }); return CreatedAtAction(nameof(Get), new { id = seat.Id }, seat);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SeatRequest request, CancellationToken cancellationToken)
    {
        var seat = await db.Seats.FindAsync([id], cancellationToken) ?? throw new ApiException(404, "seat_not_found", "Không tìm thấy ghế.");
        if (seat.VenueId != request.VenueId && await db.SeatInventory.AnyAsync(x => x.SeatId == id, cancellationToken)) throw new ApiException(409, "seat_in_use", "Không thể đổi địa điểm của ghế đã được mở bán.");
        seat.VenueId = request.VenueId; seat.Section = request.Section.Trim(); seat.RowLabel = request.RowLabel.Trim().ToUpperInvariant(); seat.Number = request.Number; seat.PositionX = request.PositionX; seat.PositionY = request.PositionY; seat.PriceTier = request.PriceTier.Trim(); seat.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Update", nameof(Seat), seat.Id, new { seat.RowLabel, seat.Number }); return Ok(seat);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var seat = await db.Seats.FindAsync([id], cancellationToken) ?? throw new ApiException(404, "seat_not_found", "Không tìm thấy ghế.");
        if (await db.SeatInventory.AnyAsync(x => x.SeatId == id, cancellationToken)) throw new ApiException(409, "seat_in_use", "Không thể xóa ghế đã được mở bán; hãy vô hiệu hóa ghế.");
        db.Seats.Remove(seat); await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Delete", nameof(Seat), id); return NoContent();
    }
}
