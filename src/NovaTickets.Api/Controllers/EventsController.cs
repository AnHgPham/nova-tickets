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
[Route("api/events")]
public sealed class EventsController(AppDbContext db, AuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] bool includeDrafts = false, CancellationToken cancellationToken = default)
    {
        var isAdmin = User.IsInRole(UserRole.Admin.ToString());
        var query = db.Events.AsNoTracking().AsSplitQuery().Include(x => x.Performances).ThenInclude(x => x.Venue).AsQueryable();
        if (!(includeDrafts && isAdmin)) query = query.Where(x => x.Status == EventStatus.Published);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x => x.Title.Contains(keyword) || x.Artist.Contains(keyword));
        }
        var events = await query.OrderBy(x => x.Performances.Min(p => p.StartsAtUtc)).ToListAsync(cancellationToken);
        return Ok(events.Select(ToView));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var evt = await db.Events.AsNoTracking().Include(x => x.Performances).ThenInclude(x => x.Venue).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ApiException(404, "event_not_found", "Không tìm thấy sự kiện.");
        if (evt.Status != EventStatus.Published && !User.IsInRole(UserRole.Admin.ToString())) return NotFound();
        return Ok(ToView(evt));
    }

    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
    public async Task<IActionResult> Create(EventRequest request, CancellationToken cancellationToken)
    {
        if (await db.Events.AnyAsync(x => x.Slug == request.Slug, cancellationToken))
            throw new ApiException(409, "slug_exists", "Slug sự kiện đã tồn tại.");
        var evt = new Event
        {
            Title = request.Title,
            Slug = request.Slug,
            Artist = request.Artist,
            Description = request.Description,
            HeroImageUrl = request.HeroImageUrl
        };
        Apply(evt, request);
        db.Events.Add(evt);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Create", nameof(Event), evt.Id, new { evt.Title });
        return CreatedAtAction(nameof(Get), new { id = evt.Id }, evt);
    }

    [Authorize(Roles = "Admin,Staff")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, EventRequest request, CancellationToken cancellationToken)
    {
        var evt = await db.Events.FindAsync([id], cancellationToken) ?? throw new ApiException(404, "event_not_found", "Không tìm thấy sự kiện.");
        if (await db.Events.AnyAsync(x => x.Slug == request.Slug && x.Id != id, cancellationToken))
            throw new ApiException(409, "slug_exists", "Slug sự kiện đã tồn tại.");
        Apply(evt, request);
        evt.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Update", nameof(Event), evt.Id, new { evt.Title });
        return Ok(evt);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var evt = await db.Events.Include(x => x.Performances).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ApiException(404, "event_not_found", "Không tìm thấy sự kiện.");
        if (evt.Performances.Any()) throw new ApiException(409, "event_has_performances", "Không thể xóa sự kiện đã có suất diễn; hãy chuyển sang trạng thái Cancelled.");
        db.Events.Remove(evt);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Delete", nameof(Event), id);
        return NoContent();
    }

    private static object ToView(Event evt) => new
    {
        evt.Id, evt.Title, evt.Slug, evt.Artist, evt.Description, evt.HeroImageUrl, evt.MinPrice, evt.Status,
        performances = evt.Performances.OrderBy(x => x.StartsAtUtc).Select(x => new { x.Id, x.StartsAtUtc, x.DoorsOpenAtUtc, x.SalesStartUtc, x.SalesEndUtc, x.Status, x.VenueId, venue = x.Venue is null ? null : new { x.Venue.Name, x.Venue.City, x.Venue.Address } })
    };

    private static void Apply(Event evt, EventRequest request)
    {
        evt.Title = request.Title.Trim(); evt.Slug = request.Slug.Trim().ToLowerInvariant(); evt.Artist = request.Artist.Trim();
        evt.Description = request.Description.Trim(); evt.HeroImageUrl = request.HeroImageUrl.Trim(); evt.MinPrice = request.MinPrice; evt.Status = request.Status;
    }
}
