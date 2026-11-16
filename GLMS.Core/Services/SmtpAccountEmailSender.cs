using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace GLMS.Web.Services;

public sealed class SmtpAccountEmailSender(
    IOptions<SmtpEmailOptions> options) : IAccountEmailSender
{
    public async Task SendVerificationCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Host)
            || string.IsNullOrWhiteSpace(settings.FromAddress))
        {
            throw new InvalidOperationException(
                "Email delivery is not configured. Set SmtpEmail:Host and SmtpEmail:FromAddress.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName),
            Subject = "Your GLMS verification code",
            Body = $"Your GLMS verification code is {code}. It expires in 10 minutes. Do not share this code.",
            IsBodyHtml = false
        };
        message.To.Add(email);

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}
