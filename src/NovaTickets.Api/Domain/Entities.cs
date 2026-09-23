using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NovaTickets.Api.Domain;

public enum UserRole { Customer, Staff, Admin }
public enum EventStatus { Draft, Published, Cancelled, Completed }
public enum PerformanceStatus { Draft, OnSale, SoldOut, Cancelled, Completed }
public enum SeatStatus { Available, Held, Sold, Blocked }
public enum BookingStatus { PendingPayment, Paid, Cancelled, Expired, Refunded }
public enum PaymentStatus { Pending, Succeeded, Failed, Refunded }
public enum EmailDeliveryStatus { Pending, Sent, Failed }

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(320)] public required string Email { get; set; }
    [MaxLength(200)] public required string PasswordHash { get; set; }
    [MaxLength(120)] public required string FullName { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;
    public bool IsActive { get; set; } = true;
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Venue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(160)] public required string Name { get; set; }
    [MaxLength(120)] public required string City { get; set; }
    [MaxLength(300)] public required string Address { get; set; }
    [MaxLength(1200)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
}

public sealed class Seat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VenueId { get; set; }
    public Venue? Venue { get; set; }
    public Guid? SeatAreaId { get; set; }
    public SeatArea? SeatArea { get; set; }
    public Guid? TicketTypeId { get; set; }
    public TicketType? TicketType { get; set; }
    [MaxLength(40)] public required string Section { get; set; }
    [MaxLength(12)] public required string RowLabel { get; set; }
    public int Number { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    [MaxLength(40)] public required string PriceTier { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SeatArea
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VenueId { get; set; }
    public Venue? Venue { get; set; }
    [MaxLength(40)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    [MaxLength(20)] public required string Color { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class TicketType
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(40)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    [MaxLength(600)] public string? Description { get; set; }
    [MaxLength(20)] public required string Color { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal BasePrice { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PerformanceTicketType
{
    public Guid PerformanceId { get; set; }
    public Performance? Performance { get; set; }
    public Guid TicketTypeId { get; set; }
    public TicketType? TicketType { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Price { get; set; }
    public int? Capacity { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Event
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(180)] public required string Title { get; set; }
    [MaxLength(200)] public required string Slug { get; set; }
    [MaxLength(160)] public required string Artist { get; set; }
    [MaxLength(3000)] public required string Description { get; set; }
    [MaxLength(800)] public required string HeroImageUrl { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal MinPrice { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<Performance> Performances { get; set; } = new List<Performance>();
}

public sealed class Performance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Event? Event { get; set; }
    public Guid VenueId { get; set; }
    public Venue? Venue { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime DoorsOpenAtUtc { get; set; }
    public DateTime SalesStartUtc { get; set; }
    public DateTime SalesEndUtc { get; set; }
    public PerformanceStatus Status { get; set; } = PerformanceStatus.Draft;
    public ICollection<SeatInventory> SeatInventory { get; set; } = new List<SeatInventory>();
}

public sealed class SeatInventory
{
    public Guid PerformanceId { get; set; }
    public Performance? Performance { get; set; }
    public Guid SeatId { get; set; }
    public Seat? Seat { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Price { get; set; }
    public SeatStatus Status { get; set; } = SeatStatus.Available;
    [MaxLength(80)] public string? HoldToken { get; set; }
    public Guid? HeldByUserId { get; set; }
    public DateTime? HoldExpiresAtUtc { get; set; }
    public Guid? BookingId { get; set; }
    public long Version { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(20)] public required string BookingCode { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid PerformanceId { get; set; }
    public Performance? Performance { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.PendingPayment;
    [Column(TypeName = "decimal(18,2)")] public decimal TotalAmount { get; set; }
    [MaxLength(100)] public required string IdempotencyKey { get; set; }
    [MaxLength(120)] public required string CustomerName { get; set; }
    [MaxLength(320)] public required string CustomerEmail { get; set; }
    [MaxLength(30)] public required string CustomerPhone { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<EmailOutbox> Emails { get; set; } = new List<EmailOutbox>();
}

public sealed class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }
    public Guid PerformanceId { get; set; }
    public Guid SeatId { get; set; }
    public Seat? Seat { get; set; }
    [MaxLength(32)] public required string TicketCode { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Price { get; set; }
    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }
    [MaxLength(40)] public required string Provider { get; set; }
    [MaxLength(100)] public required string Reference { get; set; }
    [MaxLength(100)] public required string IdempotencyKey { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
}

public sealed class EmailOutbox
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }
    [MaxLength(60)] public required string MessageType { get; set; }
    [MaxLength(320)] public required string ToEmail { get; set; }
    [MaxLength(200)] public required string Subject { get; set; }
    public required string HtmlBody { get; set; }
    public required string TextBody { get; set; }
    [MaxLength(40)] public required string Provider { get; set; }
    public EmailDeliveryStatus Status { get; set; } = EmailDeliveryStatus.Pending;
    public int AttemptCount { get; set; }
    [MaxLength(1000)] public string? LastError { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; }
}

public sealed class AuditLog
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    [MaxLength(80)] public required string Action { get; set; }
    [MaxLength(80)] public required string EntityType { get; set; }
    [MaxLength(80)] public string? EntityId { get; set; }
    [MaxLength(4000)] public string? DataJson { get; set; }
    [MaxLength(64)] public string? IpAddress { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SystemSetting
{
    [MaxLength(100)] public required string Key { get; set; }
    [MaxLength(128)] public required string ValueHash { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
