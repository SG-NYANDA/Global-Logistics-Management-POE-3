namespace GLMS.Web.Services;

public interface IAccountEmailSender
{
    Task SendVerificationCodeAsync(string email, string code, CancellationToken cancellationToken);
}
