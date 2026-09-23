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
[Route("api")]
public sealed class CatalogController(AppDbContext db, AuditService audit) : ControllerBase
{
    [HttpGet("seat-areas")]
    public async Task<IActionResult> Areas([FromQuery] Guid? venueId, CancellationToken cancellationToken)
    {
        var query = db.SeatAreas.AsNoTracking().AsQueryable();
        if (venueId.HasValue) query = query.Where(x => x.VenueId == venueId.Value);
        return Ok(await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken));
    }

    [HttpGet("seat-areas/{id:guid}")]
    public async Task<IActionResult> Area(Guid id, CancellationToken cancellationToken) => Ok(await db.SeatAreas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new ApiException(404, "area_not_found", "Không tìm thấy khu vực."));

    [Authorize(Roles = "Admin,Staff")]
    [HttpPost("seat-areas")]
    public async Task<IActionResult> CreateArea(SeatAreaRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Venues.AnyAsync(x => x.Id == request.VenueId, cancellationToken)) throw new ApiException(404, "venue_not_found", "Không tìm thấy địa điểm.");
        if (await db.SeatAreas.AnyAsync(x => x.VenueId == request.VenueId && x.Code == request.Code, cancellationToken)) throw new ApiException(409, "area_code_exists", "Mã khu vực đã tồn tại.");
        var area = new SeatArea { VenueId = request.VenueId, Code = request.Code.Trim().ToUpperInvariant(), Name = request.Name.Trim(), Color = request.Color, SortOrder = request.SortOrder, IsActive = request.IsActive };
        db.SeatAreas.Add(area); await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Create", nameof(SeatArea), area.Id, new { area.Code, area.Name }); return Created($"/api/seat-areas/{area.Id}", area);
    }

    [Authorize(Roles = "Admin,Staff")]
    [HttpPut("seat-areas/{id:guid}")]
    public async Task<IActionResult> UpdateArea(Guid id, SeatAreaRequest request, CancellationToken cancellationToken)
    {
        var area = await db.SeatAreas.FindAsync([id], cancellationToken) ?? throw new ApiException(404, "area_not_found", "Không tìm thấy khu vực.");
        if (await db.SeatAreas.AnyAsync(x => x.VenueId == request.VenueId && x.Code == request.Code && x.Id != id, cancellationToken)) throw new ApiException(409, "area_code_exists", "Mã khu vực đã tồn tại.");
        area.VenueId = request.VenueId; area.Code = request.Code.Trim().ToUpperInvariant(); area.Name = request.Name.Trim(); area.Color = request.Color; area.SortOrder = request.SortOrder; area.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Update", nameof(SeatArea), id, new { area.Code, area.Name }); return Ok(area);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("seat-areas/{id:guid}")]
    public async Task<IActionResult> DeleteArea(Guid id, CancellationToken cancellationToken)
    {
        var area = await db.SeatAreas.FindAsync([id], cancellationToken) ?? throw new ApiException(404, "area_not_found", "Không tìm thấy khu vực.");
        if (await db.Seats.AnyAsync(x => x.SeatAreaId == id, cancellationToken)) throw new ApiException(409, "area_in_use", "Khu vực đang được gán cho ghế; hãy vô hiệu hóa thay vì xóa.");
        db.SeatAreas.Remove(area); await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Delete", nameof(SeatArea), id); return NoContent();
    }

    [HttpGet("ticket-types")]
    public async Task<IActionResult> TicketTypes(CancellationToken cancellationToken) => Ok(await db.TicketTypes.AsNoTracking().OrderBy(x => x.BasePrice).ToListAsync(cancellationToken));

    [HttpGet("ticket-types/{id:guid}")]
    public async Task<IActionResult> TicketType(Guid id, CancellationToken cancellationToken) => Ok(await db.TicketTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new ApiException(404, "ticket_type_not_found", "Không tìm thấy loại vé."));

    [Authorize(Roles = "Admin,Staff")]
    [HttpPost("ticket-types")]
    public async Task<IActionResult> CreateTicketType(TicketTypeRequest request, CancellationToken cancellationToken)
    {
        if (await db.TicketTypes.AnyAsync(x => x.Code == request.Code, cancellationToken)) throw new ApiException(409, "ticket_type_code_exists", "Mã loại vé đã tồn tại.");
        var item = new TicketType { Code = request.Code.Trim().ToUpperInvariant(), Name = request.Name.Trim(), Description = request.Description?.Trim(), Color = request.Color, BasePrice = request.BasePrice, IsActive = request.IsActive };
        db.TicketTypes.Add(item); await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Create", nameof(TicketType), item.Id, new { item.Code, item.Name }); return Created($"/api/ticket-types/{item.Id}", item);
    }

    [Authorize(Roles = "Admin,Staff")]
    [HttpPut("ticket-types/{id:guid}")]
    public async Task<IActionResult> UpdateTicketType(Guid id, TicketTypeRequest request, CancellationToken cancellationToken)
    {
        var item = await db.TicketTypes.FindAsync([id], cancellationToken) ?? throw new ApiException(404, "ticket_type_not_found", "Không tìm thấy loại vé.");
        if (await db.TicketTypes.AnyAsync(x => x.Code == request.Code && x.Id != id, cancellationToken)) throw new ApiException(409, "ticket_type_code_exists", "Mã loại vé đã tồn tại.");
        item.Code = request.Code.Trim().ToUpperInvariant(); item.Name = request.Name.Trim(); item.Description = request.Description?.Trim(); item.Color = request.Color; item.BasePrice = request.BasePrice; item.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Update", nameof(TicketType), id, new { item.Code, item.Name }); return Ok(item);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("ticket-types/{id:guid}")]
    public async Task<IActionResult> DeleteTicketType(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.TicketTypes.FindAsync([id], cancellationToken) ?? throw new ApiException(404, "ticket_type_not_found", "Không tìm thấy loại vé.");
        if (await db.Seats.AnyAsync(x => x.TicketTypeId == id, cancellationToken) || await db.PerformanceTicketTypes.AnyAsync(x => x.TicketTypeId == id, cancellationToken)) throw new ApiException(409, "ticket_type_in_use", "Loại vé đang được sử dụng; hãy vô hiệu hóa thay vì xóa.");
        db.TicketTypes.Remove(item); await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Delete", nameof(TicketType), id); return NoContent();
    }

    [HttpGet("performances/{performanceId:guid}/ticket-types")]
    public async Task<IActionResult> PerformancePrices(Guid performanceId, CancellationToken cancellationToken) => Ok(await db.PerformanceTicketTypes.AsNoTracking().Where(x => x.PerformanceId == performanceId).Include(x => x.TicketType).Select(x => new { x.PerformanceId, x.TicketTypeId, x.Price, x.Capacity, x.IsActive, ticketType = x.TicketType }).ToListAsync(cancellationToken));

    [HttpGet("performances/{performanceId:guid}/ticket-types/{ticketTypeId:guid}")]
    public async Task<IActionResult> PerformancePrice(Guid performanceId, Guid ticketTypeId, CancellationToken cancellationToken) => Ok(await db.PerformanceTicketTypes.AsNoTracking().Include(x => x.TicketType).FirstOrDefaultAsync(x => x.PerformanceId == performanceId && x.TicketTypeId == ticketTypeId, cancellationToken) ?? throw new ApiException(404, "performance_price_not_found", "Không tìm thấy cấu hình giá."));

    [Authorize(Roles = "Admin,Staff")]
    [HttpPost("performances/{performanceId:guid}/ticket-types")]
    public async Task<IActionResult> CreatePerformancePrice(Guid performanceId, PerformanceTicketTypeRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Performances.AnyAsync(x => x.Id == performanceId, cancellationToken)) throw new ApiException(404, "performance_not_found", "Không tìm thấy suất diễn.");
        if (!await db.TicketTypes.AnyAsync(x => x.Id == request.TicketTypeId, cancellationToken)) throw new ApiException(404, "ticket_type_not_found", "Không tìm thấy loại vé.");
        if (await db.PerformanceTicketTypes.AnyAsync(x => x.PerformanceId == performanceId && x.TicketTypeId == request.TicketTypeId, cancellationToken)) throw new ApiException(409, "performance_price_exists", "Cấu hình giá đã tồn tại.");
        var item = new PerformanceTicketType { PerformanceId = performanceId, TicketTypeId = request.TicketTypeId, Price = request.Price, Capacity = request.Capacity, IsActive = request.IsActive };
        db.PerformanceTicketTypes.Add(item); await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Create", nameof(PerformanceTicketType), $"{performanceId}:{request.TicketTypeId}", new { request.Price, request.Capacity }); return Created($"/api/performances/{performanceId}/ticket-types/{request.TicketTypeId}", item);
    }

    [Authorize(Roles = "Admin,Staff")]
    [HttpPut("performances/{performanceId:guid}/ticket-types")]
    public async Task<IActionResult> UpsertPerformancePrice(Guid performanceId, PerformanceTicketTypeRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Performances.AnyAsync(x => x.Id == performanceId, cancellationToken)) throw new ApiException(404, "performance_not_found", "Không tìm thấy suất diễn.");
        if (!await db.TicketTypes.AnyAsync(x => x.Id == request.TicketTypeId, cancellationToken)) throw new ApiException(404, "ticket_type_not_found", "Không tìm thấy loại vé.");
        var item = await db.PerformanceTicketTypes.FindAsync([performanceId, request.TicketTypeId], cancellationToken) ?? throw new ApiException(404, "performance_price_not_found", "Không tìm thấy cấu hình giá để cập nhật.");
        item.Price = request.Price; item.Capacity = request.Capacity; item.IsActive = request.IsActive; await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Upsert", nameof(PerformanceTicketType), $"{performanceId}:{request.TicketTypeId}", new { request.Price, request.Capacity }); return Ok(item);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("performances/{performanceId:guid}/ticket-types/{ticketTypeId:guid}")]
    public async Task<IActionResult> DeletePerformancePrice(Guid performanceId, Guid ticketTypeId, CancellationToken cancellationToken)
    {
        var item = await db.PerformanceTicketTypes.FindAsync([performanceId, ticketTypeId], cancellationToken) ?? throw new ApiException(404, "performance_price_not_found", "Không tìm thấy cấu hình giá.");
        db.PerformanceTicketTypes.Remove(item); await db.SaveChangesAsync(cancellationToken); await audit.WriteAsync("Delete", nameof(PerformanceTicketType), $"{performanceId}:{ticketTypeId}"); return NoContent();
    }

    [Authorize(Roles = "Admin,Staff")]
    [HttpGet("admin/performance-ticket-types")]
    public async Task<IActionResult> AdminPerformancePrices(CancellationToken cancellationToken) => Ok(await db.PerformanceTicketTypes.AsNoTracking()
        .Include(x => x.Performance)!.ThenInclude(x => x!.Event)
        .Include(x => x.TicketType)
        .OrderBy(x => x.Performance!.StartsAtUtc)
        .Select(x => new { id = x.PerformanceId.ToString() + ":" + x.TicketTypeId.ToString(), x.PerformanceId, performanceTitle = x.Performance!.Event!.Title, x.TicketTypeId, ticketTypeName = x.TicketType!.Name, x.Price, x.Capacity, x.IsActive })
        .ToListAsync(cancellationToken));

    [Authorize(Roles = "Admin,Staff")]
    [HttpPost("admin/performance-ticket-types")]
    public Task<IActionResult> AdminCreatePerformancePrice(AdminPerformanceTicketTypeRequest request, CancellationToken cancellationToken) =>
        CreatePerformancePrice(request.PerformanceId, new PerformanceTicketTypeRequest(request.TicketTypeId, request.Price, request.Capacity, request.IsActive), cancellationToken);

    [Authorize(Roles = "Admin,Staff")]
    [HttpPut("admin/performance-ticket-types/{performanceId:guid}/{ticketTypeId:guid}")]
    public Task<IActionResult> AdminUpdatePerformancePrice(Guid performanceId, Guid ticketTypeId, AdminPerformanceTicketTypeRequest request, CancellationToken cancellationToken)
    {
        if (request.PerformanceId != performanceId || request.TicketTypeId != ticketTypeId) throw new ApiException(400, "resource_key_mismatch", "Khóa trong URL và body không khớp.");
        return UpsertPerformancePrice(performanceId, new PerformanceTicketTypeRequest(ticketTypeId, request.Price, request.Capacity, request.IsActive), cancellationToken);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("admin/performance-ticket-types/{performanceId:guid}/{ticketTypeId:guid}")]
    public Task<IActionResult> AdminDeletePerformancePrice(Guid performanceId, Guid ticketTypeId, CancellationToken cancellationToken) => DeletePerformancePrice(performanceId, ticketTypeId, cancellationToken);
}
