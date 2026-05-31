using Microsoft.AspNetCore.Mvc;
using Wex.Funding.Application.Common;
using Wex.Funding.Application.Transactions;

namespace Wex.Funding.Api.Endpoints;

internal static class TransactionEndpoints
{
    public static IEndpointRouteBuilder MapTransactionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/transactions").WithTags("Transactions");

        group.MapGet("/{transactionId:guid}", GetTransactionInCurrencyAsync)
            .WithName("GetTransactionInCurrency")
            .WithSummary("Retrieve a transaction converted to a specified currency")
            .Produces<GetTransactionInCurrencyOutput>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static async Task<IResult> GetTransactionInCurrencyAsync(
        Guid transactionId,
        [FromQuery] string currency,
        GetTransactionInCurrency useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new GetTransactionInCurrencyInput(transactionId, currency),
            cancellationToken);

        return result switch
        {
           Result<GetTransactionInCurrencyOutput, GetTransactionError>.Ok ok =>
                Results.Ok(ok.Value),
           Result<GetTransactionInCurrencyOutput, GetTransactionError>.Err { Error: TransactionNotFoundError e } =>
                Results.NotFound(new { message = $"Transaction {e.TransactionId} not found." }),
           Result<GetTransactionInCurrencyOutput, GetTransactionError>.Err { Error: NoRateAvailableError e } =>
                Results.UnprocessableEntity(new { message = e.Message }),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
