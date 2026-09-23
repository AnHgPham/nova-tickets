using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NovaTickets.Api.Contracts;
using NovaTickets.Api.Data;
using NovaTickets.Api.Domain;
using NovaTickets.Api.Infrastructure;
using NovaTickets.Api.Security;
using NovaTickets.Api.Services;

namespace NovaTickets.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AppDbContext db, IPasswordHasher<User> passwordHasher, JwtTokenService tokens, AuditService audit) : ControllerBase
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var fullName = request.FullName.Trim();
        var phone = request.Phone.Trim();
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(phone))
            throw new ApiException(400, "invalid_profile", "Họ tên và số điện thoại không được chỉ chứa khoảng trắng.");
        if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
            throw new ApiException(409, "email_exists", "Email đã được sử dụng.");
        if (!IsStrongPassword(request.Password))
            throw new ApiException(400, "weak_password", "Mật khẩu tối thiểu 10 ký tự và phải có chữ hoa, chữ thường, số cùng ký tự đặc biệt.");

        var user = new User { Email = email, FullName = fullName, Phone = phone, PasswordHash = "" };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Register", nameof(User), user.Id, new { user.Email });
        return Ok(SignIn(user));
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        var now = DateTime.UtcNow;
        if (user is not null && user.LockoutEndUtc > now)
        {
            await audit.WriteAsync("LoginBlocked", nameof(User), user.Id, new { user.Email, user.LockoutEndUtc });
            throw new ApiException(423, "account_locked", "Tài khoản tạm khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau 15 phút.");
        }
        if (user is null || !user.IsActive)
        {
            await audit.WriteAsync("LoginFailed", nameof(User), null, new { Email = email });
            throw new ApiException(401, "invalid_credentials", "Email hoặc mật khẩu không chính xác.");
        }

        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= MaxFailedAttempts)
            {
                user.AccessFailedCount = 0;
                user.LockoutEndUtc = now.Add(LockoutDuration);
            }
            user.UpdatedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            await audit.WriteAsync("LoginFailed", nameof(User), user.Id, new { user.Email, user.LockoutEndUtc });
            if (user.LockoutEndUtc.HasValue)
                throw new ApiException(423, "account_locked", "Tài khoản tạm khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau 15 phút.");
            throw new ApiException(401, "invalid_credentials", "Email hoặc mật khẩu không chính xác.");
        }

        user.AccessFailedCount = 0;
        user.LockoutEndUtc = null;
        user.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("LoginSucceeded", nameof(User), user.Id, new { user.Email });
        return Ok(SignIn(user));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserView>> Me(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new ApiException(401, "user_not_found", "Tài khoản không còn tồn tại.");
        return Ok(ToView(user));
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("nova_access");
        return NoContent();
    }

    private AuthResponse SignIn(User user)
    {
        var token = tokens.Create(user);
        var expiresAt = DateTime.UtcNow.AddHours(8);
        Response.Cookies.Append("nova_access", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = expiresAt,
            IsEssential = true,
            Path = "/"
        });
        return new AuthResponse(ToView(user), expiresAt);
    }

    private static UserView ToView(User user) => new(user.Id, user.Email, user.FullName, user.Phone, user.Role, user.IsActive);
    private static bool IsStrongPassword(string password) => password.Length >= 10 && password.Length <= 128 && password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit) && password.Any(ch => !char.IsLetterOrDigit(ch));
}
