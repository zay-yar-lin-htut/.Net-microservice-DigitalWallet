using System.Net;
using System.Text.Json;
using IdentityService.Services;

namespace IdentityService.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (EmailAlreadyExistsException exception)
        {
            await WriteErrorAsync(context, HttpStatusCode.Conflict, exception.Message);
        }
        catch (InvalidCredentialsException)
        {
            await WriteErrorAsync(
                context,
                HttpStatusCode.Unauthorized,
                "Invalid email or password.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled identity service exception.");
            await WriteErrorAsync(
                context,
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred.");
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string message)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            message,
            statusCode = (int)statusCode,
            traceId = context.TraceIdentifier
        }));
    }
}
