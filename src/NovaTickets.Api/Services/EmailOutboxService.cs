using System.Net;
using Microsoft.EntityFrameworkCore;
using NovaTickets.Api.Contracts;
using NovaTickets.Api.Data;
using NovaTickets.Api.Domain;
using NovaTickets.Api.Infrastructure;

namespace NovaTickets.Api.Services;

public sealed class EmailOutboxService(AppDbContext db, ILogger<EmailOutboxService> logger, AuditService audit)
{
    public const string BookingConfirmation = "BookingConfirmation";
    public const string OfflineProvider = "OfflinePreview";

    public async Task<EmailOutbox> QueueBookingConfirmationAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var existing = await db.EmailOutbox.FirstOrDefaultAsync(x => x.BookingId == bookingId && x.MessageType == BookingConfirmation, cancellationToken);
        if (existing is not null)
        {
            if (existing.Status != EmailDeliveryStatus.Sent) await DispatchAsync(existing.Id, cancellationToken);
            return existing;
        }

        var booking = await db.Bookings.AsNoTracking()
            .Include(x => x.Performance)!.ThenInclude(x => x!.Event)
            .Include(x => x.Performance)!.ThenInclude(x => x!.Venue)
            .Include(x => x.Tickets).ThenInclude(x => x.Seat)
            .FirstOrDefaultAsync(x => x.Id == bookingId, cancellationToken)
            ?? throw new ApiException(404, "booking_not_found", "Không tìm thấy booking để tạo email.");

        var message = RenderBookingConfirmation(booking);
        var email = new EmailOutbox
        {
            BookingId = booking.Id,
            MessageType = BookingConfirmation,
            ToEmail = booking.CustomerEmail,
            Subject = message.Subject,
            HtmlBody = message.Html,
            TextBody = message.Text,
            Provider = OfflineProvider,
            Status = EmailDeliveryStatus.Pending
        };
        db.EmailOutbox.Add(email);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.Entry(email).State = EntityState.Detached;
            email = await db.EmailOutbox.SingleAsync(x => x.BookingId == bookingId && x.MessageType == BookingConfirmation, cancellationToken);
        }

        await DispatchAsync(email.Id, cancellationToken);
        return email;
    }

    public async Task<EmailOutbox> DispatchAsync(Guid id, CancellationToken cancellationToken)
    {
        var email = await db.EmailOutbox.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ApiException(404, "email_not_found", "Không tìm thấy email trong Outbox.");
        if (email.Status == EmailDeliveryStatus.Sent) return email;

        email.AttemptCount++;
        email.UpdatedAtUtc = DateTime.UtcNow;
        try
        {
            // OfflinePreview is an intentional demo transport: the rendered message is persisted
            // and can be opened from the customer account or admin console without external SMTP.
            email.Status = EmailDeliveryStatus.Sent;
            email.SentAtUtc = DateTime.UtcNow;
            email.LastError = null;
            await db.SaveChangesAsync(cancellationToken);
            await audit.WriteAsync("EmailDispatched", nameof(EmailOutbox), email.Id, new { email.BookingId, email.ToEmail, email.Provider, email.AttemptCount });
            logger.LogInformation("Offline email {EmailId} captured for {Recipient}", email.Id, email.ToEmail);
        }
        catch (Exception exception)
        {
            email.Status = EmailDeliveryStatus.Failed;
            email.LastError = exception.Message.Length > 1000 ? exception.Message[..1000] : exception.Message;
            email.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            logger.LogError(exception, "Could not dispatch email {EmailId}", email.Id);
        }
        return email;
    }

    public async Task<EmailPreview> GetPreviewAsync(Guid id, Guid userId, bool adminOrStaff, CancellationToken cancellationToken)
    {
        var email = await db.EmailOutbox.AsNoTracking().Include(x => x.Booking)
            .FirstOrDefaultAsync(x => x.Id == id && (adminOrStaff || x.Booking!.UserId == userId), cancellationToken)
            ?? throw new ApiException(404, "email_not_found", "Không tìm thấy email xác nhận.");
        return ToPreview(email);
    }

    public async Task<EmailPreview> GetBookingPreviewAsync(Guid bookingId, Guid userId, bool adminOrStaff, CancellationToken cancellationToken)
    {
        var email = await db.EmailOutbox.AsNoTracking().Include(x => x.Booking)
            .FirstOrDefaultAsync(x => x.BookingId == bookingId && x.MessageType == BookingConfirmation && (adminOrStaff || x.Booking!.UserId == userId), cancellationToken)
            ?? throw new ApiException(404, "email_not_found", "Booking chưa có email xác nhận.");
        return ToPreview(email);
    }

    public static EmailPreview ToPreview(EmailOutbox email) => new(email.Id, email.BookingId, email.Booking?.BookingCode ?? "", email.ToEmail, email.Subject, email.HtmlBody, email.TextBody, email.Status, email.Provider, email.AttemptCount, email.CreatedAtUtc, email.SentAtUtc, email.LastError);

    private static (string Subject, string Html, string Text) RenderBookingConfirmation(Booking booking)
    {
        var evt = booking.Performance?.Event ?? throw new InvalidOperationException("Booking event is missing.");
        var venue = booking.Performance?.Venue ?? throw new InvalidOperationException("Booking venue is missing.");
        var customer = WebUtility.HtmlEncode(booking.CustomerName);
        var eventTitle = WebUtility.HtmlEncode(evt.Title);
        var venueName = WebUtility.HtmlEncode(venue.Name);
        var bookingCode = WebUtility.HtmlEncode(booking.BookingCode);
        var ticketRows = string.Join("", booking.Tickets.OrderBy(x => x.Seat!.RowLabel).ThenBy(x => x.Seat!.Number).Select(ticket =>
            $"<tr><td style='padding:10px;border-bottom:1px solid #e5e7eb'>{WebUtility.HtmlEncode(ticket.Seat!.Section)} · {WebUtility.HtmlEncode(ticket.Seat.RowLabel)}{ticket.Seat.Number}</td><td style='padding:10px;border-bottom:1px solid #e5e7eb;font-family:monospace'>{WebUtility.HtmlEncode(ticket.TicketCode)}</td><td style='padding:10px;border-bottom:1px solid #e5e7eb;text-align:right'>{ticket.Price:N0} ₫</td></tr>"));
        var ticketText = string.Join(Environment.NewLine, booking.Tickets.OrderBy(x => x.Seat!.RowLabel).ThenBy(x => x.Seat!.Number).Select(ticket =>
            $"- {ticket.Seat!.Section} · {ticket.Seat.RowLabel}{ticket.Seat.Number} | {ticket.TicketCode} | {ticket.Price:N0} ₫"));
        var localTime = booking.Performance!.StartsAtUtc.ToString("dd/MM/yyyy HH:mm 'UTC'");
        var subject = $"[NOVA Tickets] Xác nhận đơn {booking.BookingCode}";
        var html = $"""
            <!doctype html><html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>{WebUtility.HtmlEncode(subject)}</title></head>
            <body style="margin:0;background:#f3f4f6;font-family:Arial,sans-serif;color:#111827"><div style="max-width:680px;margin:32px auto;background:#fff;border-radius:18px;overflow:hidden;border:1px solid #e5e7eb">
            <div style="background:#141622;color:#fff;padding:26px 32px"><div style="font-size:12px;letter-spacing:2px;color:#d8ff57">NOVA TICKETS · OFFLINE EMAIL DEMO</div><h1 style="margin:10px 0 0;font-size:25px">Đặt vé thành công</h1></div>
            <div style="padding:30px 32px"><p>Xin chào <strong>{customer}</strong>,</p><p>Đơn <strong>{bookingCode}</strong> của bạn đã được xác nhận và vé đã được phát hành.</p>
            <div style="background:#f7f8fa;border-radius:12px;padding:18px;margin:22px 0"><h2 style="margin:0 0 8px;font-size:20px">{eventTitle}</h2><p style="margin:4px 0">Thời gian: {localTime}</p><p style="margin:4px 0">Địa điểm: {venueName}</p></div>
            <table style="width:100%;border-collapse:collapse"><thead><tr><th style="text-align:left;padding:10px;border-bottom:2px solid #111827">Ghế</th><th style="text-align:left;padding:10px;border-bottom:2px solid #111827">Mã vé</th><th style="text-align:right;padding:10px;border-bottom:2px solid #111827">Giá</th></tr></thead><tbody>{ticketRows}</tbody></table>
            <p style="font-size:20px;text-align:right"><strong>Tổng cộng: {booking.TotalAmount:N0} ₫</strong></p><p style="font-size:13px;color:#6b7280">Đây là thư được giữ trong Offline Email Outbox phục vụ demo. Khi tích hợp SMTP/Resend, cùng nội dung này sẽ được chuyển tới hộp thư thật.</p></div></div></body></html>
            """;
        var text = $"""
            NOVA TICKETS - XÁC NHẬN ĐẶT VÉ

            Xin chào {booking.CustomerName},
            Đơn {booking.BookingCode} đã được xác nhận.

            Sự kiện: {evt.Title}
            Thời gian: {localTime}
            Địa điểm: {venue.Name}

            {ticketText}

            Tổng cộng: {booking.TotalAmount:N0} ₫

            Offline Email Demo: thư được lưu trong Outbox và có thể xem từ tài khoản hoặc trang quản trị.
            """;
        return (subject, html, text);
    }
}
