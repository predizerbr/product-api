using FluentValidation;
using Pruduct.Contracts.Payments;

namespace Pruduct.Business.Validators.Payments;

public class CreatePaymentMethodRequestValidator : AbstractValidator<CreatePaymentMethodRequest>
{
    public CreatePaymentMethodRequestValidator()
    {
        RuleFor(x => x.Type).NotEmpty();

        When(
            x => IsType(x, "PIX"),
            () =>
            {
                RuleFor(x => x.PixKey).NotEmpty();
            }
        );

        When(
            x => IsType(x, "CARD"),
            () =>
            {
                RuleFor(x => x.CardBrand).NotEmpty();
                RuleFor(x => x.CardLast4).NotEmpty().Length(4);
                RuleFor(x => x.CardExpMonth).NotEmpty().InclusiveBetween(1, 12);
                RuleFor(x => x.CardExpYear).NotEmpty().InclusiveBetween(2000, 2100);
                RuleFor(x => x.CardHolderName).NotEmpty();
            }
        );

        When(
            x => IsType(x, "BANK_ACCOUNT"),
            () =>
            {
                RuleFor(x => x.BankCode).NotEmpty();
                RuleFor(x => x.Agency).NotEmpty();
                RuleFor(x => x.AccountNumber).NotEmpty();
            }
        );
    }

    private static bool IsType(CreatePaymentMethodRequest request, string type) =>
        string.Equals(request.Type, type, StringComparison.OrdinalIgnoreCase);
}
