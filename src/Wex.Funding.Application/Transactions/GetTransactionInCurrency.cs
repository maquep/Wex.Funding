using Wex.Funding.Application.Common;
using Wex.Funding.Application.Currencies;
using Wex.Funding.Domain.Currencies;
using Wex.Funding.Domain.Transactions;

namespace Wex.Funding.Application.Transactions;

public sealed record GetTransactionInCurrencyInput(Guid TransactionId, string TargetCurrency);

public sealed record GetTransactionInCurrencyOutput(
    Guid Id,
    string Description,
    DateOnly TransactionDate,
    decimal OriginalAmount,
    string OriginalCurrency,
    decimal ExchangeRate,
    DateOnly RateRecordDate,
    decimal ConvertedAmount,
    string TargetCurrency);

public abstract record GetTransactionError;
public sealed record TransactionNotFoundError(Guid TransactionId) : GetTransactionError;
public sealed record NoRateAvailableError(string Message) : GetTransactionError;

public sealed class GetTransactionInCurrency(
    ITransactionRepository transactionRepository,
    ITreasuryRatesClient treasuryRatesClient)
{
    public async Task<Result<GetTransactionInCurrencyOutput, GetTransactionError>> ExecuteAsync(
        GetTransactionInCurrencyInput transactionInput,
        CancellationToken cancellationToken = default)
    {
        var transactionId = new TransactionId(transactionInput.TransactionId);
        var transaction = await transactionRepository.GetByIdAsync(transactionId, cancellationToken);

        if (transaction is null)
        {
            return new Result<GetTransactionInCurrencyOutput, GetTransactionError>.Err(
                new TransactionNotFoundError(transactionInput.TransactionId));
        }

        var targetCurrency = new CurrencyCode(transactionInput.TargetCurrency);

        var rateLookup = await treasuryRatesClient.GetRateOnOrBeforeAsync(
            targetCurrency,
            transaction.TransactionDate,
            cancellationToken);

        if (!rateLookup.Found)
        {
            return new Result<GetTransactionInCurrencyOutput, GetTransactionError>.Err(
                new NoRateAvailableError(
                    $"No exchange rate for {targetCurrency} is available within 6 months of {transaction.TransactionDate}."));
        }

        var convertedAmount = transaction.Amount.Amount * rateLookup.Rate;

        return new Result<GetTransactionInCurrencyOutput, GetTransactionError>.Ok(
            new GetTransactionInCurrencyOutput(
                transaction.Id.Value,
                transaction.Description,
                transaction.TransactionDate,
                transaction.Amount.Amount,
                transaction.Amount.Currency.Value,
                rateLookup.Rate,
                rateLookup.RecordDate,
                Math.Round(convertedAmount, 4),
                targetCurrency.Value));
    }
}
