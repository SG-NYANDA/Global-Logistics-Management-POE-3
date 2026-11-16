using System.Net.Mail;
using GLMS.Contracts;
using GLMS.Web.Models;
using GLMS.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;

namespace GLMS.Api.Services;

public sealed class AccountService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    EmailVerificationService verificationService,
    ITokenService tokenService,
    ILogger<AccountService> logger) : IAccountService
{
    public async Task RegisterAsync(RegisterAccountRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                throw new ApiOperationException(StatusCodes.Status409Conflict, "Account already exists", "An account with this email address already exists.");
            }

            var detail = string.Join(" ", result.Errors.Select(error => error.Description));
            throw new ApiOperationException(StatusCodes.Status400BadRequest, "Invalid account", detail);
        }

        try
        {
            await verificationService.SendCodeAsync(user, enforceResendInterval: false, cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or SmtpException)
        {
            logger.LogError(exception, "Could not send account verification email for user {UserId}", user.Id);
            throw new ApiOperationException(StatusCodes.Status503ServiceUnavailable, "Verification delivery unavailable", "Your account was created, but the verification code could not be delivered. Request a new code after email delivery is configured.");
        }
    }

    public async Task<AccountTokenResponse> LoginAsync(LoginAccountRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            throw new ApiOperationException(StatusCodes.Status401Unauthorized, "Sign-in failed", "Email or password is incorrect, or the email address has not been verified.");
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            var detail = result.IsLockedOut
                ? "Sign-in is temporarily locked after too many attempts. Try again later."
                : "Email or password is incorrect, or the email address has not been verified.";
            throw new ApiOperationException(StatusCodes.Status401Unauthorized, "Sign-in failed", detail);
        }

        return tokenService.Create(user);
    }

    public async Task<AccountTokenResponse> VerifyAsync(VerifyAccountRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.EmailConfirmed
            || !await verificationService.VerifyCodeAsync(user, request.Code, cancellationToken))
        {
            throw new ApiOperationException(StatusCodes.Status400BadRequest, "Invalid verification code", "The code is invalid or expired. Request a new code and try again.");
        }

        return tokenService.Create(user);
    }

    public async Task ResendVerificationAsync(ResendAccountVerificationRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.EmailConfirmed)
        {
            return;
        }

        try
        {
            await verificationService.SendCodeAsync(user, enforceResendInterval: true, cancellationToken);
        }
        catch (VerificationResendTooSoonException)
        {
            throw new ApiOperationException(StatusCodes.Status429TooManyRequests, "Please wait before requesting another code", "You can request another verification code once per minute.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or SmtpException)
        {
            logger.LogError(exception, "Could not resend account verification email for user {UserId}", user.Id);
            throw new ApiOperationException(StatusCodes.Status503ServiceUnavailable, "Verification delivery unavailable", "Email delivery is temporarily unavailable. Please try again later.");
        }
    }
}
