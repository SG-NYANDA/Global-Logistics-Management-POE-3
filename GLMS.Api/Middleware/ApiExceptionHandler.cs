using GLMS.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GLMS.Api.Middleware;

public sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ApiOperationException apiException)
        {
            return false;
        }

        logger.LogWarning(exception, "API request failed: {Title}", apiException.Title);
        httpContext.Response.StatusCode = apiException.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = apiException.StatusCode,
            Title = apiException.Title,
            Detail = apiException.Message,
            Instance = httpContext.Request.Path
        }, cancellationToken);
        return true;
    }
}
