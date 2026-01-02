using System.Net;
using Newtonsoft.Json;

namespace Rbac.DataPlane.Middleware;

public class UnhandledExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UnhandledExceptionHandler> _logger;

    public UnhandledExceptionHandler(RequestDelegate next, ILogger<UnhandledExceptionHandler> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        var statusCode = HttpStatusCode.InternalServerError;
        var errorCode = "InternalServerError";
        var message = "An internal server error occurred.";

        switch (exception)
        {
            case ArgumentException:
                statusCode = HttpStatusCode.BadRequest;
                errorCode = "BadRequest";
                message = exception.Message;
                break;
            case KeyNotFoundException:
                statusCode = HttpStatusCode.NotFound;
                errorCode = "NotFound";
                message = exception.Message;
                break;
            case UnauthorizedAccessException:
                statusCode = HttpStatusCode.Unauthorized;
                errorCode = "Unauthorized";
                message = "Unauthorized access.";
                break;
            // Add custom exceptions here if needed
        }

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            ErrorCode = errorCode,
            Message = message
        };

        return context.Response.WriteAsync(JsonConvert.SerializeObject(response));
    }
}
