using Microsoft.EntityFrameworkCore;
using NovaTickets.Api.Domain;

namespace NovaTickets.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<SeatArea> SeatAreas => Set<SeatArea>();
    public DbSet<TicketType> TicketTypes => Set<TicketType>();
    public DbSet<PerformanceTicketType> PerformanceTicketTypes => Set<PerformanceTicketType>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Performance> Performances => Set<Performance>();
    public DbSet<SeatInventory> SeatInventory => Set<SeatInventory>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<EmailOutbox> EmailOutbox => Set<EmailOutbox>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().ToTable("AppUsers");
        modelBuilder.Entity<SystemSetting>().HasKey(x => x.Key);
        modelBuilder.Entity<User>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<Event>().HasIndex(x => x.Slug).IsUnique();
        modelBuilder.Entity<Seat>().HasIndex(x => new { x.VenueId, x.Section, x.RowLabel, x.Number }).IsUnique();
        modelBuilder.Entity<SeatArea>().HasIndex(x => new { x.VenueId, x.Code }).IsUnique();
        modelBuilder.Entity<TicketType>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<PerformanceTicketType>().HasKey(x => new { x.PerformanceId, x.TicketTypeId });
        modelBuilder.Entity<SeatInventory>().HasKey(x => new { x.PerformanceId, x.SeatId });
        modelBuilder.Entity<SeatInventory>().HasIndex(x => new { x.PerformanceId, x.Status });
        modelBuilder.Entity<SeatInventory>().HasIndex(x => x.HoldToken);
        modelBuilder.Entity<Booking>().HasIndex(x => x.BookingCode).IsUnique();
        modelBuilder.Entity<Booking>().HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
        modelBuilder.Entity<Ticket>().HasIndex(x => x.TicketCode).IsUnique();
        modelBuilder.Entity<Ticket>().HasIndex(x => new { x.PerformanceId, x.SeatId }).IsUnique();
        modelBuilder.Entity<Payment>().HasIndex(x => x.Reference).IsUnique();
        modelBuilder.Entity<Payment>().HasIndex(x => new { x.BookingId, x.IdempotencyKey }).IsUnique();
        modelBuilder.Entity<EmailOutbox>().HasIndex(x => new { x.BookingId, x.MessageType }).IsUnique();
        modelBuilder.Entity<EmailOutbox>().HasIndex(x => new { x.Status, x.CreatedAtUtc });

        modelBuilder.Entity<Event>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<Performance>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<SeatInventory>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<Booking>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<Payment>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<EmailOutbox>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<User>().Property(x => x.Role).HasConversion<string>();

        modelBuilder.Entity<SeatInventory>()
            .HasOne(x => x.Seat).WithMany().HasForeignKey(x => x.SeatId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SeatInventory>()
            .HasOne(x => x.Performance).WithMany(x => x.SeatInventory).HasForeignKey(x => x.PerformanceId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Ticket>()
            .HasOne(x => x.Seat).WithMany().HasForeignKey(x => x.SeatId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Seat>()
            .HasOne(x => x.SeatArea).WithMany().HasForeignKey(x => x.SeatAreaId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Seat>()
            .HasOne(x => x.TicketType).WithMany().HasForeignKey(x => x.TicketTypeId).OnDelete(DeleteBehavior.SetNull);
    }
}
