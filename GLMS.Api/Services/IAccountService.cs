using GLMS.Contracts;

namespace GLMS.Api.Services;

public interface IAccountService
{
    Task RegisterAsync(RegisterAccountRequest request, CancellationToken cancellationToken);
    Task<AccountTokenResponse> LoginAsync(LoginAccountRequest request, CancellationToken cancellationToken);
    Task<AccountTokenResponse> VerifyAsync(VerifyAccountRequest request, CancellationToken cancellationToken);
    Task ResendVerificationAsync(ResendAccountVerificationRequest request, CancellationToken cancellationToken);
}
