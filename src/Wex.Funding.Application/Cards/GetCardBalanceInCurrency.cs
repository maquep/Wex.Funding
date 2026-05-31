using Wex.Funding.Application.Common;
using Wex.Funding.Application.Currencies;
using Wex.Funding.Domain.Cards;
using Wex.Funding.Domain.Currencies;

namespace Wex.Funding.Application.Cards;

public sealed record GetCardBalanceInCurrencyInput(Guid CardId, string TargetCurrency);

public sealed record GetCardBalanceInCurrencyOutput(
    Guid CardId,
    decimal CreditLimitAmount,
    string CreditLimitCurrency,
    decimal TotalSpentAmount,
    decimal AvailableBalanceAmount,
    string TargetCurrency,
    decimal ExchangeRate,
    DateOnly RateRecordDate,
    decimal ConvertedAvailableBalance);

public abstract record GetBalanceError;
public sealed record CardNotFoundError(Guid CardId) : GetBalanceError;
public sealed record BalanceRateNotAvailableError(string Message) : GetBalanceError;

public sealed class GetCardBalanceInCurrency(
    ICardRepository cardRepository,
    ITreasuryRatesClient treasuryRatesClient)
{
    public async Task<Result<GetCardBalanceInCurrencyOutput, GetBalanceError>> ExecuteAsync(
        GetCardBalanceInCurrencyInput input,
        CancellationToken cancellationToken = default)
    {
        var cardId = new CardId(input.CardId);
        var card = await cardRepository.GetByIdAsync(cardId, cancellationToken);

        if (card is null)
        {
            return new Result<GetCardBalanceInCurrencyOutput, GetBalanceError>.Err(
                new CardNotFoundError(input.CardId));
        }

        var totalSpent = await cardRepository.SumTransactionsAsync(cardId, cancellationToken);
        var available = card.CreditLimit.Subtract(totalSpent);

        var targetCurrency = new CurrencyCode(input.TargetCurrency);

        var rateLookup = await treasuryRatesClient.GetLatestRateAsync(targetCurrency, cancellationToken);

        if (!rateLookup.Found)
        {
            return new Result<GetCardBalanceInCurrencyOutput, GetBalanceError>.Err(
                new BalanceRateNotAvailableError(
                    $"No exchange rate is available for {targetCurrency}."));
        }

        var convertedBalance = available.Amount * rateLookup.Rate;

        return new Result<GetCardBalanceInCurrencyOutput, GetBalanceError>.Ok(
            new GetCardBalanceInCurrencyOutput(
                card.Id.Value,
                card.CreditLimit.Amount,
                card.CreditLimit.Currency.Value,
                totalSpent.Amount,
                available.Amount,
                targetCurrency.Value,
                rateLookup.Rate,
                rateLookup.RecordDate,
                Math.Round(convertedBalance, 4)));
    }
}
