using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NovaTickets.Api.Domain;

namespace NovaTickets.Api.Data;

public sealed class DatabaseSeeder(AppDbContext db, IPasswordHasher<User> passwordHasher, ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync()
    {
        if (!await db.Users.AnyAsync())
        {
            var admin = new User { Email = "admin@novatickets.vn", FullName = "Quản trị NovaTickets", Phone = "0900000000", PasswordHash = "", Role = UserRole.Admin };
            admin.PasswordHash = passwordHasher.HashPassword(admin, Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "Admin@12345");
            var customer = new User { Email = "demo@novatickets.vn", FullName = "Khách hàng Demo", Phone = "0912345678", PasswordHash = "", Role = UserRole.Customer };
            customer.PasswordHash = passwordHasher.HashPassword(customer, "Demo@12345");
            db.Users.AddRange(admin, customer);
        }

        if (!await db.Events.AnyAsync())
        {
            var venue = new Venue
            {
                Name = "NOVA Arena",
                City = "TP. Hồ Chí Minh",
                Address = "Khu đô thị Sala, TP. Thủ Đức",
                Description = "Không gian biểu diễn hiện đại với sơ đồ ghế trực quan."
            };
            var seats = new List<Seat>();
            for (var row = 0; row < 6; row++)
            {
                for (var number = 1; number <= 10; number++)
                {
                    seats.Add(new Seat
                    {
                        Venue = venue,
                        Section = row < 2 ? "VIP" : "STANDARD",
                        RowLabel = ((char)('A' + row)).ToString(),
                        Number = number,
                        PositionX = number * 8,
                        PositionY = 14 + row * 12,
                        PriceTier = row < 2 ? "VIP" : "STANDARD"
                    });
                }
            }
            db.Seats.AddRange(seats);

            var events = new[]
            {
                new Event { Title = "NEON SYMPHONY 2026", Slug = "neon-symphony-2026", Artist = "MINH x The Aurora", Description = "Đêm nhạc điện tử kết hợp giao hưởng và visual art trong không gian đa tầng cảm xúc.", HeroImageUrl = "/manus-storage/hero-concert_81d2138b.jpg", MinPrice = 690000, Status = EventStatus.Published },
                new Event { Title = "MIDNIGHT PULSE", Slug = "midnight-pulse", Artist = "Lina & Friends", Description = "Một hành trình pop, R&B và những bản phối chỉ xuất hiện trong đêm diễn đặc biệt này.", HeroImageUrl = "/manus-storage/countdown-stage_0ded13a8.jpg", MinPrice = 590000, Status = EventStatus.Published },
                new Event { Title = "CITY LIGHTS FEST", Slug = "city-lights-fest", Artist = "Multi-artist festival", Description = "Lễ hội âm nhạc thành thị với đội hình nghệ sĩ trẻ và sân khấu ánh sáng xuyên đêm.", HeroImageUrl = "/manus-storage/neon-crowd_f037808e.jpg", MinPrice = 490000, Status = EventStatus.Published }
            };
            db.Events.AddRange(events);
            await db.SaveChangesAsync();

            foreach (var (evt, index) in events.Select((evt, index) => (evt, index)))
            {
                var performance = new Performance
                {
                    EventId = evt.Id,
                    VenueId = venue.Id,
                    StartsAtUtc = DateTime.UtcNow.Date.AddDays(12 + index * 7).AddHours(13),
                    DoorsOpenAtUtc = DateTime.UtcNow.Date.AddDays(12 + index * 7).AddHours(11),
                    SalesStartUtc = DateTime.UtcNow.AddDays(-2),
                    SalesEndUtc = DateTime.UtcNow.AddDays(12 + index * 7).AddHours(12),
                    Status = PerformanceStatus.OnSale
                };
                db.Performances.Add(performance);
                foreach (var seat in seats)
                {
                    db.SeatInventory.Add(new SeatInventory
                    {
                        Performance = performance,
                        SeatId = seat.Id,
                        Price = seat.PriceTier == "VIP" ? 1490000 : 690000,
                        Status = SeatStatus.Available
                    });
                }
            }
        }

        if (!await db.TicketTypes.AnyAsync())
        {
            db.TicketTypes.AddRange(
                new TicketType { Code = "STANDARD", Name = "Standard", Description = "Khu ghế tiêu chuẩn với tầm nhìn trực diện sân khấu.", Color = "#7C8BFF", BasePrice = 690000 },
                new TicketType { Code = "VIP", Name = "VIP Experience", Description = "Vị trí gần sân khấu, lối vào ưu tiên và quà lưu niệm.", Color = "#D8FF57", BasePrice = 1490000 });
            await db.SaveChangesAsync();
        }

        var defaultVenue = await db.Venues.FirstOrDefaultAsync();
        if (defaultVenue is not null && !await db.SeatAreas.AnyAsync(x => x.VenueId == defaultVenue.Id))
        {
            var vipArea = new SeatArea { VenueId = defaultVenue.Id, Code = "VIP", Name = "VIP Floor", Color = "#D8FF57", SortOrder = 1 };
            var standardArea = new SeatArea { VenueId = defaultVenue.Id, Code = "STANDARD", Name = "Khán đài Standard", Color = "#7C8BFF", SortOrder = 2 };
            db.SeatAreas.AddRange(vipArea, standardArea);
            await db.SaveChangesAsync();

            var ticketTypes = await db.TicketTypes.ToDictionaryAsync(x => x.Code);
            var existingSeats = await db.Seats.Where(x => x.VenueId == defaultVenue.Id).ToListAsync();
            foreach (var seat in existingSeats)
            {
                var isVip = seat.PriceTier == "VIP";
                seat.SeatAreaId = isVip ? vipArea.Id : standardArea.Id;
                seat.TicketTypeId = ticketTypes[isVip ? "VIP" : "STANDARD"].Id;
            }

            var performances = await db.Performances.ToListAsync();
            foreach (var performance in performances)
            {
                foreach (var type in ticketTypes.Values)
                {
                    db.PerformanceTicketTypes.Add(new PerformanceTicketType
                    {
                        PerformanceId = performance.Id,
                        TicketTypeId = type.Id,
                        Price = type.BasePrice,
                        Capacity = type.Code == "VIP" ? 20 : 40
                    });
                }
            }
        }

        const string cleanupSettingKey = "scheduler_cleanup_key";
        if (!await db.SystemSettings.AnyAsync(x => x.Key == cleanupSettingKey))
        {
            var secret = Environment.GetEnvironmentVariable("NOVA_SCHEDULER_KEY") ?? Environment.GetEnvironmentVariable("JWT_SECRET") ?? "development-cleanup-key";
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();
            db.SystemSettings.Add(new SystemSetting { Key = cleanupSettingKey, ValueHash = hash });
        }

        await db.SaveChangesAsync();
        logger.LogInformation("NovaTickets seed completed.");
    }
}
