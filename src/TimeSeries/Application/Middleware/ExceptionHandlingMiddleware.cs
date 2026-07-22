using TimeSeries.Application.Exceptions;

namespace TimeSeries.Application.Middleware;

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
        catch (CsvValidationException exception)
        {
            if (context.Response.HasStarted)
                return;

            context.Response.StatusCode =
                StatusCodes.Status400BadRequest;

            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                status = StatusCodes.Status400BadRequest,
                reason = exception.Reason,
                details = exception.Details,
                lineNumber = exception.LineNumber
            });
        }
        catch (ConflictException exception)
        {
            if (context.Response.HasStarted)
                return;

            context.Response.StatusCode =
                StatusCodes.Status409Conflict;

            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                status = StatusCodes.Status409Conflict,
                reason = "Конфликт",
                details = exception.Message
            });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Непредвиденная ошибка.");

            if (context.Response.HasStarted)
                return;

            context.Response.StatusCode =
                StatusCodes.Status500InternalServerError;

            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                status = StatusCodes.Status500InternalServerError,
                reason = "Внутренняя ошибка сервера",
                details = "Произошла непредвиденная ошибка."
            });
        }
    }
}