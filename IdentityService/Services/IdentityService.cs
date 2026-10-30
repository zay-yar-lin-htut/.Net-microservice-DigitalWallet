using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IdentityService.Contracts;
using IdentityService.Data;
using IdentityService.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Services;

public sealed class IdentityService(
    IdentityDbContext db,
    IPasswordHasher<User> passwordHasher,
    IConfiguration configuration) : IIdentityService
{
    private readonly string issuer = configuration["Jwt:Issuer"]
        ?? throw new InvalidOperationException("JWT Issuer Missing");

    private readonly string audience = configuration["Jwt:Audience"]
        ?? throw new InvalidOperationException("JWT Audience Missing");

    private readonly SymmetricSecurityKey signingKey = CreateSigningKey(configuration);

    public async Task<UserResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var exists = await db.Users
            .AnyAsync(user => user.Email == email, cancellationToken);

        if (exists)
        {
            throw new EmailAlreadyExistsException();
        }

        var user = new User { Email = email };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return new UserResponse(user.Id, user.Email, user.CreatedAtUtc);
    }

    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users
            .SingleOrDefaultAsync(item => item.Email == email, cancellationToken);

        if (user is null)
        {
            throw new InvalidCredentialsException();
        }

        var verificationResult = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            throw new InvalidCredentialsException();
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(cancellationToken);
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(15);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email)
        };

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(
                signingKey,
                SecurityAlgorithms.HmacSha256));

        return new LoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            expiresAt);
    }

    private static SymmetricSecurityKey CreateSigningKey(IConfiguration configuration)
    {
        var secret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT Secret Missing");

        var bytes = Encoding.UTF8.GetBytes(secret);
        if (bytes.Length < 32)
        {
            throw new InvalidOperationException("JWT Secret must contain at least 32 bytes.");
        }

        return new SymmetricSecurityKey(bytes);
    }
}
