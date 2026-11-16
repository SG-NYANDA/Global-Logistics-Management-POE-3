using GLMS.Contracts;
using GLMS.Api.Services;
using GLMS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GLMS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/contracts")]
public sealed class ContractsController(IGlmsDataService dataService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ContractResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ContractResponse>>> GetAll(
        [FromQuery] ContractStatus? status,
        [FromQuery] DateTimeOffset? fromDate,
        [FromQuery] DateTimeOffset? toDate,
        CancellationToken cancellationToken)
    {
        if (fromDate > toDate)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid date range",
                Detail = "fromDate must be on or before toDate."
            });
        }

        return Ok(await dataService.GetContractsAsync(status, fromDate, toDate, cancellationToken));
    }

    [HttpGet("{id:int}", Name = "GetContractById")]
    [ProducesResponseType<ContractResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var contract = await dataService.GetContractAsync(id, cancellationToken);
        return contract is null ? NotFound() : Ok(contract);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [ProducesResponseType<ContractResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContractResponse>> Create(
        [FromForm] CreateContractRequest request,
        CancellationToken cancellationToken)
    {
        var contract = await dataService.CreateContractAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = contract.Id }, contract);
    }

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType<ContractResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractResponse>> UpdateStatus(
        int id,
        UpdateContractStatusRequest request,
        CancellationToken cancellationToken)
    {
        var contract = await dataService.UpdateContractStatusAsync(id, request.Status, cancellationToken);
        return contract is null ? NotFound() : Ok(contract);
    }

    [HttpGet("{id:int}/agreement")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAgreement(int id, CancellationToken cancellationToken)
    {
        var agreement = await dataService.GetAgreementAsync(id, cancellationToken);
        return agreement is null
            ? NotFound()
            : File(agreement, "application/pdf", $"contract-{id}-agreement.pdf");
    }
}
