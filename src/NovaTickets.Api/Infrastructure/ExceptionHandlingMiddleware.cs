using System.Text.Json;

namespace NovaTickets.Api.Infrastructure;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException ex)
        {
            await WriteProblem(context, ex.StatusCode, ex.Code, ex.Message);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected or cancelled navigation; do not report this as an application 500.
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 499;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblem(context, 500, "internal_error", "Hệ thống gặp lỗi. Vui lòng thử lại sau.");
        }
    }

    private static async Task WriteProblem(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            type = $"https://novaticket.vn/errors/{code}",
            title = message,
            status,
            code,
            traceId = context.TraceIdentifier
        }));
    }
}
