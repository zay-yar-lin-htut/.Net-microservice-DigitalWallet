using System.Net;
using System.Text.Json;
using WalletService.Services;

namespace WalletService.Middleware;

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
        catch (WalletNotFoundException exception)
        {
            await WriteErrorAsync(context, HttpStatusCode.NotFound, exception.Message);
        }
        catch (WalletAlreadyExistsException exception)
        {
            await WriteErrorAsync(context, HttpStatusCode.Conflict, exception.Message);
        }
        catch (InsufficientBalanceException exception)
        {
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled wallet service exception.");
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
