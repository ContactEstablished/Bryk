using System.Net;
using System.Text.Json;
using Bryk.Application.Exceptions;

namespace Bryk.API.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            {
                // Client disconnected — don't log as an error, don't try to write a response.
                context.Response.StatusCode = 499;
                return;
            }

            logger.LogError(ex, "Unhandled exception occurred.");

            context.Response.ContentType = "application/json";

            if (ex is ValidationException validationException)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

                var body = JsonSerializer.Serialize(new
                {
                    status = (int)HttpStatusCode.BadRequest,
                    error = "One or more validation errors occurred.",
                    errors = validationException.Errors,
                    traceId = context.TraceIdentifier
                }, JsonOptions);

                await context.Response.WriteAsync(body);
                return;
            }

            var (statusCode, message) = ex switch
            {
                KeyNotFoundException => (HttpStatusCode.NotFound, "The requested resource was not found."),
                ArgumentException => (HttpStatusCode.BadRequest, "Invalid request."),
                InvalidOperationException => (HttpStatusCode.Conflict, "The operation could not be completed due to the current state of the resource."),
                _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
            };

            context.Response.StatusCode = (int)statusCode;

            var responseBody = JsonSerializer.Serialize(new
            {
                status = (int)statusCode,
                error = message,
                traceId = context.TraceIdentifier
            }, JsonOptions);

            await context.Response.WriteAsync(responseBody);
        }
    }
}
