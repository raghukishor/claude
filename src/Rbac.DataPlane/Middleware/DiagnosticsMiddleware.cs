using System.Diagnostics;
using Rbac.DataPlane.Diagnostics;

namespace Rbac.DataPlane.Middleware;

public class DiagnosticsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DiagnosticsMiddleware> _logger;

    public DiagnosticsMiddleware(RequestDelegate next, ILogger<DiagnosticsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Generate or retrieve Request ID
        var requestId = context.Request.Headers["X-Request-Id"].FirstOrDefault() 
                        ?? Guid.NewGuid().ToString();

        // Set AsyncLocal context
        DiagnosticsContext.Current.RequestId = requestId;
        
        // Add ID to response headers
        context.Response.Headers["X-Request-Id"] = requestId;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "API {Method} {Path} completed in {Duration}ms. RequestId: {RequestId}",
                context.Request.Method,
                context.Request.Path,
                stopwatch.ElapsedMilliseconds,
                requestId);
        }
    }
}
