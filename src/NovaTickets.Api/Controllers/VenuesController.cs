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
[Route("api/venues")]
public sealed class VenuesController(AppDbContext db, AuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.Venues.AsNoTracking().Select(x => new { x.Id, x.Name, x.City, x.Address, x.Description, x.IsActive, seatCount = x.Seats.Count }).ToListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) => Ok(await db.Venues.AsNoTracking().Include(x => x.Seats).FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new ApiException(404, "venue_not_found", "Không tìm thấy địa điểm."));

    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
    public async Task<IActionResult> Create(VenueRequest request, CancellationToken cancellationToken)
    {
        var venue = new Venue { Name = request.Name.Trim(), City = request.City.Trim(), Address = request.Address.Trim(), Description = request.Description?.Trim(), IsActive = request.IsActive };
        db.Venues.Add(venue); await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Create", nameof(Venue), venue.Id, new { venue.Name });
        return CreatedAtAction(nameof(Get), new { id = venue.Id }, venue);
    }

    [Authorize(Roles = "Admin,Staff")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, VenueRequest request, CancellationToken cancellationToken)
    {
        var venue = await db.Venues.FindAsync([id], cancellationToken) ?? throw new ApiException(404, "venue_not_found", "Không tìm thấy địa điểm.");
        venue.Name = request.Name.Trim(); venue.City = request.City.Trim(); venue.Address = request.Address.Trim(); venue.Description = request.Description?.Trim(); venue.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Update", nameof(Venue), venue.Id, new { venue.Name }); return Ok(venue);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var venue = await db.Venues.Include(x => x.Seats).FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new ApiException(404, "venue_not_found", "Không tìm thấy địa điểm.");
        if (await db.Performances.AnyAsync(x => x.VenueId == id, cancellationToken)) throw new ApiException(409, "venue_in_use", "Không thể xóa địa điểm đã có suất diễn.");
        db.Seats.RemoveRange(venue.Seats); db.Venues.Remove(venue); await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Delete", nameof(Venue), id); return NoContent();
    }
}
