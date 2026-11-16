using GLMS.Contracts;
using GLMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GLMS.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/account")]
public sealed class AccountController(IAccountService accountService) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MessageResponse>> Register(
        RegisterAccountRequest request,
        CancellationToken cancellationToken)
    {
        await accountService.RegisterAsync(request, cancellationToken);
        return Accepted(new MessageResponse("If account creation succeeds, a verification code has been sent."));
    }

    [HttpPost("login")]
    [ProducesResponseType<AccountTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AccountTokenResponse>> Login(
        LoginAccountRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await accountService.LoginAsync(request, cancellationToken));
    }

    [HttpPost("verify")]
    [ProducesResponseType<AccountTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AccountTokenResponse>> Verify(
        VerifyAccountRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await accountService.VerifyAsync(request, cancellationToken));
    }

    [HttpPost("resend-verification")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<MessageResponse>> ResendVerification(
        ResendAccountVerificationRequest request,
        CancellationToken cancellationToken)
    {
        await accountService.ResendVerificationAsync(request, cancellationToken);
        return Accepted(new MessageResponse("If the account requires verification, a code has been sent."));
    }
}
