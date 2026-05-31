using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Wex.Funding.Domain.Exceptions;

namespace Wex.Funding.Api.Middleware;

internal sealed class ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        logger.LogError(exception, "Unhandled exception for {Method} {Path}: {Message}",
            context.Request.Method, context.Request.Path, exception.Message);

        var (statusCode, title, detail) = exception switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found", "The requested resource was not found."),
            InvalidCurrencyException => (StatusCodes.Status422UnprocessableEntity, "Invalid Currency", "The supplied currency code is not valid."),
            CurrencyMismatchException => (StatusCodes.Status422UnprocessableEntity, "Currency Mismatch", "The transaction currency does not match the card's currency."),
            InvalidAmountException => (StatusCodes.Status422UnprocessableEntity, "Invalid Amount", "The supplied amount is not valid."),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Domain Rule Violation", "A business rule was violated."),
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request", "The request contained invalid data."),
            HttpRequestException => (StatusCodes.Status503ServiceUnavailable, "Service Unavailable", "An upstream service is temporarily unavailable."),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.")
        };

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
