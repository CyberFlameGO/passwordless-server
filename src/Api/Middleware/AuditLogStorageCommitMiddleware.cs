using Passwordless.Service.AuditLog.Loggers;

namespace Passwordless.Api.Middleware;

public class AuditLogStorageCommitMiddleware
{
    private readonly RequestDelegate _next;

    public AuditLogStorageCommitMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AuditLoggerProvider auditLogger)
    {
        await _next(context);

        var logger = await auditLogger.Create();

        await logger.FlushAsync();
    }
}