using FluentValidation;
using Pruduct.Contracts.Wallet;

namespace Pruduct.Business.Validators.Wallet;

public class AmountRequestValidator : AbstractValidator<AmountRequest>
{
    public AmountRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}
