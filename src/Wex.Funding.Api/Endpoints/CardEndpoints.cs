using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Wex.Funding.Api.Validators;
using Wex.Funding.Application.Common;
using Wex.Funding.Application.Cards;
using Wex.Funding.Application.Transactions;

namespace Wex.Funding.Api.Endpoints;

internal static class CardEndpoints
{
    public static IEndpointRouteBuilder MapCardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cards").WithTags("Cards");

        group.MapPost("/", CreateCardAsync)
            .WithName("CreateCard")
            .WithSummary("Create a card with a credit limit")
            .Produces(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPost("/{cardId:guid}/transactions", RecordTransactionAsync)
            .WithName("RecordTransaction")
            .WithSummary("Record a purchase transaction against a card")
            .Produces(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{cardId:guid}/balance", GetCardBalanceAsync)
            .WithName("GetCardBalance")
            .WithSummary("Get a card's available balance in a specified currency")
            .Produces<GetCardBalanceInCurrencyOutput>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static async Task<IResult> CreateCardAsync(
        [FromBody] CreateCardRequest request,
        IValidator<CreateCardRequest> validator,
        CreateCard createCard,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var card = await createCard.ExecuteAsync(
            new CreateCardInput(request.CreditLimitAmount, request.CreditLimitCurrency),
            cancellationToken);

        return Results.Created($"/api/cards/{card.Id}", card);
    }

    private static async Task<IResult> RecordTransactionAsync(
        Guid cardId,
        [FromBody] RecordTransactionRequest request,
        IValidator<RecordTransactionRequest> validator,
        RecordTransaction useCase,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var transaction = await useCase.ExecuteAsync(
            new RecordTransactionInput(
                cardId,
                request.Description,
                request.TransactionDate,
                request.Amount,
                request.Currency),
            cancellationToken);

        return Results.Created($"/api/transactions/{transaction.Id}", transaction);
    }

    private static async Task<IResult> GetCardBalanceAsync(
        Guid cardId,
        [FromQuery] string currency,
        GetCardBalanceInCurrency useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new GetCardBalanceInCurrencyInput(cardId, currency),
            cancellationToken);

        return result switch
        {
            Result<GetCardBalanceInCurrencyOutput, GetBalanceError>.Ok ok =>
                Results.Ok(ok.Value),
            Result<GetCardBalanceInCurrencyOutput, GetBalanceError>.Err { Error: CardNotFoundError e } =>
                Results.NotFound(new { message = $"Card {e.CardId} not found." }),
            Result<GetCardBalanceInCurrencyOutput, GetBalanceError>.Err { Error: BalanceRateNotAvailableError e } =>
                Results.UnprocessableEntity(new { message = e.Message }),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
