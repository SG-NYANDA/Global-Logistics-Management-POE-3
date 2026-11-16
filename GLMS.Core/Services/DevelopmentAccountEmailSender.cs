using Microsoft.Extensions.Logging;

namespace GLMS.Web.Services;

public sealed class DevelopmentAccountEmailSender(
    Microsoft.Extensions.Logging.ILogger<DevelopmentAccountEmailSender> logger) : IAccountEmailSender
{
    public Task SendVerificationCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken)
    {
        var atIndex = email.IndexOf('@');
        var maskedEmail = atIndex > 1
            ? $"{email[..1]}***{email[atIndex..]}"
            : "***";

        logger.LogWarning(
            "DEVELOPMENT ONLY: GLMS email verification code for {Email} is {VerificationCode}. It expires in 10 minutes.",
            maskedEmail,
            code);

        return Task.CompletedTask;
    }
}
