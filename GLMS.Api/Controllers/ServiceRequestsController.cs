using GLMS.Contracts;
using GLMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GLMS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/service-requests")]
public sealed class ServiceRequestsController(IGlmsDataService dataService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ServiceRequestResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ServiceRequestResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await dataService.GetServiceRequestsAsync(cancellationToken));

    [HttpGet("{id:int}", Name = "GetServiceRequestById")]
    [ProducesResponseType<ServiceRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceRequestResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var request = await dataService.GetServiceRequestAsync(id, cancellationToken);
        return request is null ? NotFound() : Ok(request);
    }

    [HttpPost]
    [ProducesResponseType<ServiceRequestResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ServiceRequestResponse>> Create(
        CreateServiceRequestRequest request,
        CancellationToken cancellationToken)
    {
        var serviceRequest = await dataService.CreateServiceRequestAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = serviceRequest.Id }, serviceRequest);
    }

    [HttpGet("current-rate")]
    [ProducesResponseType<ExchangeRateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ExchangeRateResponse>> CurrentRate(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(new ExchangeRateResponse(await dataService.GetCurrentRateAsync(cancellationToken)));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Currency service unavailable");
        }
    }
}
