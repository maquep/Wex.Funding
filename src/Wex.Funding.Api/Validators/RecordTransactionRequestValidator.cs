using FluentValidation;
using Wex.Funding.Infrastructure.ExternalServices.Treasury;

namespace Wex.Funding.Api.Validators;

public sealed record RecordTransactionRequest(
    string Description,
    DateOnly TransactionDate,
    decimal Amount,
    string Currency);

public sealed class RecordTransactionRequestValidator : AbstractValidator<RecordTransactionRequest>
{
    public RecordTransactionRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.TransactionDate).LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Transaction date cannot be in the future.");
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Matches("^[A-Z]{3}$").WithMessage("Currency must be a 3-letter ISO 4217 code.")
            .Must(CurrencyNameMap.IsKnown)
            .WithMessage("Currency is not supported for exchange rate conversion.");
    }
}
