using System.Globalization;
using System.Security.Cryptography;
using GLMS.Web.Data;
using GLMS.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GLMS.Web.Services;

public sealed class EmailVerificationService(
    GlmsDbContext db,
    UserManager<ApplicationUser> userManager,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IAccountEmailSender emailSender)
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendInterval = TimeSpan.FromMinutes(1);
    private const int MaximumFailedAttempts = 5;

    public async Task SendCodeAsync(
        ApplicationUser user,
        bool enforceResendInterval,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var challenge = await db.EmailVerificationChallenges
            .SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);

        if (enforceResendInterval
            && challenge is not null
            && now - challenge.CreatedUtc < ResendInterval)
        {
            throw new VerificationResendTooSoonException();
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000)
            .ToString("D6", CultureInfo.InvariantCulture);

        if (challenge is null)
        {
            challenge = new EmailVerificationChallenge { UserId = user.Id };
            db.EmailVerificationChallenges.Add(challenge);
        }

        challenge.CodeHash = passwordHasher.HashPassword(user, code);
        challenge.CreatedUtc = now;
        challenge.ExpiresUtc = now.Add(CodeLifetime);
        challenge.FailedAttempts = 0;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await emailSender.SendVerificationCodeAsync(user.Email!, code, cancellationToken);
        }
        catch
        {
            db.EmailVerificationChallenges.Remove(challenge);
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> VerifyCodeAsync(
        ApplicationUser user,
        string code,
        CancellationToken cancellationToken)
    {
        var challenge = await db.EmailVerificationChallenges
            .SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (challenge is null)
        {
            return false;
        }

        if (challenge.ExpiresUtc <= DateTime.UtcNow
            || challenge.FailedAttempts >= MaximumFailedAttempts)
        {
            db.EmailVerificationChallenges.Remove(challenge);
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }

        var verification = passwordHasher.VerifyHashedPassword(user, challenge.CodeHash, code);
        if (verification == PasswordVerificationResult.Failed)
        {
            challenge.FailedAttempts++;
            if (challenge.FailedAttempts >= MaximumFailedAttempts)
            {
                db.EmailVerificationChallenges.Remove(challenge);
            }

            await db.SaveChangesAsync(cancellationToken);
            return false;
        }

        user.EmailConfirmed = true;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join("; ", updateResult.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Could not verify the account email: {errors}");
        }

        db.EmailVerificationChallenges.Remove(challenge);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed class VerificationResendTooSoonException : Exception;
