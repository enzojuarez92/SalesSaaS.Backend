using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Controllers;

public sealed record UpdateProfileRequest(
    [Required, StringLength(100)] string FirstName,
    [Required, StringLength(100)] string LastName,
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required] string CurrentPassword,
    [StringLength(128, MinimumLength = 12), RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "La contraseña debe incluir mayúscula, minúscula y número.")] string? NewPassword);

[ApiController, Route("api/profile"), Authorize]
public sealed class ProfileController(ApplicationDbContext db, ICurrentUser current, IPasswordHasher<User> hasher) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == current.UserId, ct);
        return Ok(new { user.FirstName, user.LastName, user.Email });
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(x => x.Id == current.UserId, ct);
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            throw new InvalidOperationException("La contraseña actual no es correcta.");
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            throw new InvalidOperationException("Nombre y apellido son obligatorios.");
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Id != user.Id && x.Email == email, ct))
            throw new InvalidOperationException("Ese email ya está registrado.");
        user.FirstName = request.FirstName.Trim(); user.LastName = request.LastName.Trim(); user.Email = email;
        if (!string.IsNullOrEmpty(request.NewPassword)) user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        user.TokenVersion++;
        // Password/email changes invalidate sessions in every tenant of this user.
        await db.RefreshTokens.IgnoreQueryFilters().Where(x => x.UserId == user.Id && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow), ct);
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Perfil actualizado. Iniciá sesión con tus datos actuales." });
    }
}
