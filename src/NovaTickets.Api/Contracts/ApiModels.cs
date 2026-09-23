using System.ComponentModel.DataAnnotations;
using NovaTickets.Api.Domain;

namespace NovaTickets.Api.Contracts;

public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(320)] string Email,
    [Required, MinLength(10), MaxLength(128)] string Password,
    [Required, MaxLength(120)] string FullName,
    [Required, MaxLength(30)] string Phone);

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record UserView(Guid Id, string Email, string FullName, string? Phone, UserRole Role, bool IsActive);
public sealed record AuthResponse(UserView User, DateTime ExpiresAtUtc);

public sealed record VenueRequest(
    [Required, MaxLength(160)] string Name,
    [Required, MaxLength(120)] string City,
    [Required, MaxLength(300)] string Address,
    [MaxLength(1200)] string? Description,
    bool IsActive = true);

public sealed record SeatRequest(
    Guid VenueId,
    [Required, MaxLength(40)] string Section,
    [Required, MaxLength(12)] string RowLabel,
    [Range(1, 1000)] int Number,
    [Range(0, 100)] int PositionX,
    [Range(0, 100)] int PositionY,
    [Required, MaxLength(40)] string PriceTier,
    bool IsActive = true);

public sealed record SeatAreaRequest(
    Guid VenueId,
    [Required, MaxLength(40)] string Code,
    [Required, MaxLength(120)] string Name,
    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$")] string Color,
    int SortOrder,
    bool IsActive = true);

public sealed record TicketTypeRequest(
    [Required, MaxLength(40)] string Code,
    [Required, MaxLength(120)] string Name,
    [MaxLength(600)] string? Description,
    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$")] string Color,
    [Range(0, 1000000000)] decimal BasePrice,
    bool IsActive = true);

public sealed record PerformanceTicketTypeRequest(
    Guid TicketTypeId,
    [Range(0, 1000000000)] decimal Price,
    [Range(1, 1000000)] int? Capacity,
    bool IsActive = true);

public sealed record AdminPerformanceTicketTypeRequest(
    Guid PerformanceId,
    Guid TicketTypeId,
    [Range(0, 1000000000)] decimal Price,
    [Range(1, 1000000)] int? Capacity,
    bool IsActive = true);

public sealed record EventRequest(
    [Required, MaxLength(180)] string Title,
    [Required, MaxLength(200)] string Slug,
    [Required, MaxLength(160)] string Artist,
    [Required, MaxLength(3000)] string Description,
    [Required, MaxLength(800)] string HeroImageUrl,
    [Range(0, 1000000000)] decimal MinPrice,
    EventStatus Status);

public sealed record PerformanceRequest(
    Guid EventId,
    Guid VenueId,
    DateTime StartsAtUtc,
    DateTime DoorsOpenAtUtc,
    DateTime SalesStartUtc,
    DateTime SalesEndUtc,
    PerformanceStatus Status,
    [Range(0, 1000000000)] decimal StandardPrice,
    [Range(0, 1000000000)] decimal VipPrice);

public sealed record HoldSeatsRequest(Guid PerformanceId, [MinLength(1), MaxLength(8)] IReadOnlyList<Guid> SeatIds);
public sealed record HoldSeatsResponse(string HoldToken, DateTime ExpiresAtUtc, IReadOnlyList<Guid> SeatIds, decimal TotalAmount);

public sealed record ConfirmBookingRequest(
    [Required, MaxLength(80)] string HoldToken,
    [Required, MaxLength(100)] string IdempotencyKey,
    [Required, MaxLength(120)] string CustomerName,
    [Required, EmailAddress, MaxLength(320)] string CustomerEmail,
    [Required, MaxLength(30)] string CustomerPhone);

public sealed record BookingView(
    Guid Id,
    string BookingCode,
    BookingStatus Status,
    decimal TotalAmount,
    DateTime ExpiresAtUtc,
    DateTime CreatedAtUtc,
    string EventTitle,
    DateTime StartsAtUtc,
    string VenueName,
    IReadOnlyList<TicketView> Tickets,
    EmailDeliverySummary? ConfirmationEmail);

public sealed record TicketView(Guid Id, string TicketCode, string Section, string RowLabel, int SeatNumber, decimal Price);
public sealed record EmailDeliverySummary(Guid Id, EmailDeliveryStatus Status, string Provider, int AttemptCount, DateTime? SentAtUtc);
public sealed record EmailOutboxListItem(Guid Id, Guid BookingId, string BookingCode, string ToEmail, string Subject, EmailDeliveryStatus Status, string Provider, int AttemptCount, DateTime CreatedAtUtc, DateTime? SentAtUtc, string? LastError);
public sealed record EmailPreview(Guid Id, Guid BookingId, string BookingCode, string ToEmail, string Subject, string HtmlBody, string TextBody, EmailDeliveryStatus Status, string Provider, int AttemptCount, DateTime CreatedAtUtc, DateTime? SentAtUtc, string? LastError);
public sealed record UpdateUserRequest(UserRole Role, bool IsActive);
public sealed record AdminCreateUserRequest(
    [Required, EmailAddress, MaxLength(320)] string Email,
    [Required, MinLength(10), MaxLength(128)] string Password,
    [Required, MaxLength(120)] string FullName,
    [Required, MaxLength(30)] string Phone,
    UserRole Role);
public sealed record UpdateBookingStatusRequest(BookingStatus Status);
public sealed record UpdateBookingContactRequest(
    [Required, MaxLength(120)] string CustomerName,
    [Required, EmailAddress, MaxLength(320)] string CustomerEmail,
    [Required, MaxLength(30)] string CustomerPhone);
