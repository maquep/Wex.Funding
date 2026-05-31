using FluentValidation;
using Wex.Funding.Infrastructure.ExternalServices.Treasury;

namespace Wex.Funding.Api.Validators;

public sealed record CreateCardRequest(decimal CreditLimitAmount, string CreditLimitCurrency);

public sealed class CreateCardRequestValidator : AbstractValidator<CreateCardRequest>
{
    public CreateCardRequestValidator()
    {
        RuleFor(x => x.CreditLimitAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CreditLimitCurrency)
            .NotEmpty()
            .Matches("^[A-Z]{3}$").WithMessage("Currency must be a 3-letter ISO 4217 code.")
            .Must(CurrencyNameMap.IsKnown)
            .WithMessage("Currency is not supported for exchange rate conversion.");
    }
}
