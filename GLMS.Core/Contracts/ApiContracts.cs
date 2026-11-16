using System.ComponentModel.DataAnnotations;
using GLMS.Web.Models;
using Microsoft.AspNetCore.Http;

namespace GLMS.Contracts;

/// <summary>Client details returned by the GLMS API.</summary>
public sealed record ClientResponse(int Id, string Name, string ContactDetails, string Region, int ContractCount);

/// <summary>Client data accepted when creating a client.</summary>
public sealed record CreateClientRequest
{
    [Required, StringLength(150)]
    public required string Name { get; init; }

    [Required, StringLength(250)]
    public required string ContactDetails { get; init; }

    [Required, StringLength(100)]
    public required string Region { get; init; }
}

/// <summary>Contract details returned by the GLMS API.</summary>
public sealed record ContractResponse(
    int Id,
    string Reference,
    string ServiceLevel,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    ContractStatus Status,
    string AgreementFileName,
    int ClientId,
    string ClientName);

/// <summary>Contract form fields and agreement accepted when creating a contract.</summary>
public sealed record CreateContractRequest
{
    [Required, StringLength(100)]
    public required string Reference { get; init; }

    [Required, StringLength(150)]
    public required string ServiceLevel { get; init; }

    public DateTimeOffset StartDate { get; init; }
    public DateTimeOffset EndDate { get; init; }
    public ContractStatus Status { get; init; } = ContractStatus.Draft;

    [Range(1, int.MaxValue)]
    public int ClientId { get; init; }

    [Required]
    public required IFormFile Agreement { get; init; }
}

/// <summary>Contract status update accepted by the GLMS API.</summary>
public sealed record UpdateContractStatusRequest
{
    [Required]
    public ContractStatus Status { get; init; }
}

/// <summary>Client information associated with a service request.</summary>
public sealed record ServiceRequestContractResponse(int Id, string Reference, string ClientName);

/// <summary>Service request details returned by the GLMS API.</summary>
public sealed record ServiceRequestResponse(
    int Id,
    string Description,
    decimal UsdCost,
    decimal ExchangeRate,
    decimal LocalCost,
    ServiceRequestStatus Status,
    DateTimeOffset CreatedUtc,
    int ContractId,
    ServiceRequestContractResponse Contract);

/// <summary>Service request data accepted when creating a request.</summary>
public sealed record CreateServiceRequestRequest
{
    [Range(1, int.MaxValue)]
    public int ContractId { get; init; }

    [Required, StringLength(1000)]
    public required string Description { get; init; }

    [Range(typeof(decimal), "0", "999999999999")]
    public decimal UsdCost { get; init; }
}

/// <summary>Summary counts and recent requests returned for the dashboard.</summary>
public sealed record DashboardResponse(
    int ClientCount,
    int ActiveContractCount,
    int OpenRequestCount,
    IReadOnlyList<ServiceRequestResponse> RecentRequests);

/// <summary>USD to ZAR exchange rate response.</summary>
public sealed record ExchangeRateResponse(decimal Rate);

/// <summary>Generic API acknowledgement.</summary>
public sealed record MessageResponse(string Message);

/// <summary>Credentials used to register an account.</summary>
public sealed record RegisterAccountRequest
{
    [Required, EmailAddress, StringLength(256)]
    public required string Email { get; init; }

    [Required, StringLength(100, MinimumLength = 10)]
    public required string Password { get; init; }
}

/// <summary>Credentials used to sign in.</summary>
public sealed record LoginAccountRequest
{
    [Required, EmailAddress, StringLength(256)]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}

/// <summary>Email and one-time code used to verify a new account.</summary>
public sealed record VerifyAccountRequest
{
    [Required, EmailAddress, StringLength(256)]
    public required string Email { get; init; }

    [Required, RegularExpression(@"^\d{6}$")]
    public required string Code { get; init; }
}

/// <summary>Email address used to request another account verification code.</summary>
public sealed record ResendAccountVerificationRequest
{
    [Required, EmailAddress, StringLength(256)]
    public required string Email { get; init; }
}

/// <summary>Bearer token returned after successful account authentication.</summary>
public sealed record AccountTokenResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, string Email);
