using GLMS.Contracts;
using GLMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GLMS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(IGlmsDataService dataService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DashboardResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardResponse>> Get(CancellationToken cancellationToken) =>
        Ok(await dataService.GetDashboardAsync(cancellationToken));
}
