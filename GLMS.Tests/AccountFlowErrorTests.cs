using System.IdentityModel.Tokens.Jwt;
using System.Net;
using GLMS.Api.Services;
using GLMS.Contracts;
using GLMS.Web.Controllers;
using GLMS.Web.Models;
using GLMS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace GLMS.Tests;

public sealed class AccountFlowErrorTests
{
    [Fact]
    public void TokenServiceUsesJwtDefaultsWhenIssuerAndAudienceAreNotConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "integration-test-only-signing-key-must-be-at-least-32-bytes"
            })
            .Build();
        var token = new TokenService(configuration).Create(new ApplicationUser
        {
            Id = "test-user",
            Email = "test@example.test"
        });
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.AccessToken);

        Assert.Equal("GLMS.Api", jwt.Issuer);
        Assert.Contains("GLMS.Web", jwt.Audiences);
    }

    [Fact]
    public async Task VerifyDoesNotReportApiServerErrorsAsInvalidCodes()
    {
        var apiClient = new StubAccountApiClient((_, _) =>
            Task.FromException<AccountTokenResponse>(
                new ApiClientException(HttpStatusCode.InternalServerError, "Token creation failed.")));
        var controller = new AccountController(apiClient);

        await Assert.ThrowsAsync<ApiClientException>(() => controller.Verify(
            new VerifyEmailCodeViewModel { Email = "test@example.test", Code = "123456" },
            CancellationToken.None));
    }

    private sealed class StubAccountApiClient(
        Func<VerifyAccountRequest, CancellationToken, Task<AccountTokenResponse>> verify) : IAccountApiClient
    {
        public Task RegisterAsync(RegisterAccountRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AccountTokenResponse> LoginAsync(LoginAccountRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AccountTokenResponse> VerifyAsync(VerifyAccountRequest request, CancellationToken cancellationToken) =>
            verify(request, cancellationToken);

        public Task ResendVerificationAsync(
            ResendAccountVerificationRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
