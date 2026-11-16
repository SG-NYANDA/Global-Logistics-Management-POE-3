using GLMS.Contracts;
using GLMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GLMS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/clients")]
public sealed class ClientsController(IGlmsDataService dataService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ClientResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClientResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await dataService.GetClientsAsync(cancellationToken));

    [HttpGet("{id:int}", Name = "GetClientById")]
    [ProducesResponseType<ClientResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var client = await dataService.GetClientAsync(id, cancellationToken);
        return client is null ? NotFound() : Ok(client);
    }

    [HttpPost]
    [ProducesResponseType<ClientResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClientResponse>> Create(
        CreateClientRequest request,
        CancellationToken cancellationToken)
    {
        var client = await dataService.CreateClientAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = client.Id }, client);
    }
}
