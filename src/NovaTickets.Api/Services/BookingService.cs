using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NovaTickets.Api.Contracts;
using NovaTickets.Api.Data;
using NovaTickets.Api.Domain;
using NovaTickets.Api.Hubs;
using NovaTickets.Api.Infrastructure;

namespace NovaTickets.Api.Services;

public sealed class BookingService(AppDbContext db, IHubContext<SeatHub> hub, ILogger<BookingService> logger, AuditService audit, EmailOutboxService emails)
{
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(7);

    public async Task<HoldSeatsResponse> HoldAsync(Guid userId, HoldSeatsRequest request, CancellationToken cancellationToken)
    {
        var seatIds = request.SeatIds.Distinct().Order().ToArray();
        if (seatIds.Length == 0 || seatIds.Length != request.SeatIds.Count)
            throw new ApiException(400, "invalid_seats", "Danh sách ghế không hợp lệ hoặc bị trùng.");

        var performance = await db.Performances.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.PerformanceId, cancellationToken)
            ?? throw new ApiException(404, "performance_not_found", "Không tìm thấy suất diễn.");
        var now = DateTime.UtcNow;
        if (performance.Status != PerformanceStatus.OnSale || now < performance.SalesStartUtc || now > performance.SalesEndUtc)
            throw new ApiException(409, "sales_closed", "Suất diễn hiện không mở bán.");

        var token = $"hold_{Guid.NewGuid():N}";
        var expiresAt = now.Add(HoldDuration);
        var held = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            var total = 0m;
            foreach (var seatId in seatIds)
            {
                var affected = await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE `SeatInventory`
                SET `Status` = 'Held', `HoldToken` = {token}, `HeldByUserId` = {userId},
                    `HoldExpiresAtUtc` = {expiresAt}, `BookingId` = NULL, `Version` = `Version` + 1,
                    `UpdatedAtUtc` = {now}
                WHERE `PerformanceId` = {request.PerformanceId} AND `SeatId` = {seatId}
                  AND (`Status` = 'Available' OR (`Status` = 'Held' AND `HoldExpiresAtUtc` < {now}))", cancellationToken);
                if (affected != 1)
                {
                    logger.LogWarning("Seat conflict for performance {PerformanceId}, seat {SeatId}, user {UserId}", request.PerformanceId, seatId, userId);
                    throw new ApiException(409, "seat_unavailable", "Một hoặc nhiều ghế vừa được người khác chọn. Vui lòng chọn ghế khác.");
                }
                total += await db.SeatInventory.Where(x => x.PerformanceId == request.PerformanceId && x.SeatId == seatId)
                    .Select(x => x.Price).SingleAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return new HoldSeatsResponse(token, expiresAt, seatIds, total);
        });
        await audit.WriteAsync("HoldSeats", nameof(SeatInventory), token, new { request.PerformanceId, seatIds, expiresAt });
        await hub.Clients.Group($"performance:{request.PerformanceId}").SendAsync("SeatsChanged", new { seatIds, status = "Held", expiresAt }, cancellationToken);
        return held;
    }

    public async Task ReleaseAsync(Guid userId, string holdToken, CancellationToken cancellationToken)
    {
        var rows = await db.SeatInventory.Where(x => x.HoldToken == holdToken && x.HeldByUserId == userId && x.Status == SeatStatus.Held).ToListAsync(cancellationToken);
        if (rows.Count == 0) return;
        var performanceId = rows[0].PerformanceId;
        var seatIds = rows.Select(x => x.SeatId).ToArray();
        foreach (var row in rows)
        {
            row.Status = SeatStatus.Available;
            row.HoldToken = null;
            row.HeldByUserId = null;
            row.HoldExpiresAtUtc = null;
            row.Version++;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("ReleaseSeats", nameof(SeatInventory), holdToken, new { performanceId, seatIds });
        await hub.Clients.Group($"performance:{performanceId}").SendAsync("SeatsChanged", new { seatIds, status = "Available" }, cancellationToken);
    }

    public async Task<BookingView> ConfirmAsync(Guid userId, ConfirmBookingRequest request, CancellationToken cancellationToken)
    {
        var existing = await db.Bookings.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (existing is not null) return await GetViewAsync(existing.Id, userId, false, cancellationToken);

        var now = DateTime.UtcNow;
        var completed = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var heldSeats = await db.SeatInventory
            .Where(x => x.HoldToken == request.HoldToken && x.HeldByUserId == userId && x.Status == SeatStatus.Held && x.HoldExpiresAtUtc > now)
            .OrderBy(x => x.SeatId).ToListAsync(cancellationToken);
        if (heldSeats.Count == 0)
            throw new ApiException(409, "hold_expired", "Thời gian giữ ghế đã hết hoặc ghế không thuộc phiên của bạn.");

        var performanceId = heldSeats[0].PerformanceId;
        if (heldSeats.Any(x => x.PerformanceId != performanceId))
            throw new ApiException(400, "invalid_hold", "Phiên giữ ghế không hợp lệ.");

        var booking = new Booking
        {
            BookingCode = $"NV{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(100000, 999999)}",
            UserId = userId,
            PerformanceId = performanceId,
            Status = BookingStatus.Paid,
            TotalAmount = heldSeats.Sum(x => x.Price),
            IdempotencyKey = request.IdempotencyKey,
            CustomerName = request.CustomerName.Trim(),
            CustomerEmail = request.CustomerEmail.Trim().ToLowerInvariant(),
            CustomerPhone = request.CustomerPhone.Trim(),
            ExpiresAtUtc = now.AddMinutes(15)
        };
        db.Bookings.Add(booking);
        foreach (var inventory in heldSeats)
        {
            inventory.Status = SeatStatus.Sold;
            inventory.BookingId = booking.Id;
            inventory.HoldToken = null;
            inventory.HeldByUserId = null;
            inventory.HoldExpiresAtUtc = null;
            inventory.Version++;
            inventory.UpdatedAtUtc = now;
            db.Tickets.Add(new Ticket
            {
                BookingId = booking.Id,
                PerformanceId = performanceId,
                SeatId = inventory.SeatId,
                Price = inventory.Price,
                TicketCode = $"TKT-{Guid.NewGuid():N}"[..20].ToUpperInvariant()
            });
        }
        db.Payments.Add(new Payment
        {
            BookingId = booking.Id,
            Provider = "InternalCheckout",
            Reference = $"PAY-{Guid.NewGuid():N}",
            IdempotencyKey = request.IdempotencyKey,
            Status = PaymentStatus.Succeeded,
            Amount = booking.TotalAmount,
            CompletedAtUtc = now
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ApiException(409, "booking_conflict", "Ghế đã được đặt bởi giao dịch khác hoặc yêu cầu đã được xử lý.");
        }
        return new ConfirmedBooking(booking.Id, performanceId, heldSeats.Select(x => x.SeatId).ToArray(), booking.BookingCode, booking.TotalAmount);
        });

        await hub.Clients.Group($"performance:{completed.PerformanceId}").SendAsync("SeatsChanged", new { seatIds = completed.SeatIds, status = "Sold" }, cancellationToken);
        await audit.WriteAsync("ConfirmBooking", nameof(Booking), completed.BookingId, new { completed.BookingCode, completed.TotalAmount, completed.SeatIds });
        await emails.QueueBookingConfirmationAsync(completed.BookingId, cancellationToken);
        return await GetViewAsync(completed.BookingId, userId, false, cancellationToken);
    }

    public async Task<BookingView> GetViewAsync(Guid bookingId, Guid userId, bool admin, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.AsNoTracking()
            .Include(x => x.Performance)!.ThenInclude(x => x!.Event)
            .Include(x => x.Performance)!.ThenInclude(x => x!.Venue)
            .Include(x => x.Tickets).ThenInclude(x => x.Seat)
            .Include(x => x.Emails)
            .FirstOrDefaultAsync(x => x.Id == bookingId && (admin || x.UserId == userId), cancellationToken)
            ?? throw new ApiException(404, "booking_not_found", "Không tìm thấy đơn đặt vé.");
        return new BookingView(
            booking.Id, booking.BookingCode, booking.Status, booking.TotalAmount, booking.ExpiresAtUtc, booking.CreatedAtUtc,
            booking.Performance!.Event!.Title, booking.Performance.StartsAtUtc, booking.Performance.Venue!.Name,
            booking.Tickets.Select(t => new TicketView(t.Id, t.TicketCode, t.Seat!.Section, t.Seat.RowLabel, t.Seat.Number, t.Price)).ToArray(),
            booking.Emails.Where(x => x.MessageType == EmailOutboxService.BookingConfirmation).OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new EmailDeliverySummary(x.Id, x.Status, x.Provider, x.AttemptCount, x.SentAtUtc)).FirstOrDefault());
    }

    public static Guid CurrentUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : throw new ApiException(401, "invalid_identity", "Phiên đăng nhập không hợp lệ.");
    }

    private sealed record ConfirmedBooking(Guid BookingId, Guid PerformanceId, Guid[] SeatIds, string BookingCode, decimal TotalAmount);
}
