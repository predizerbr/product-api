using FluentValidation;
using Pruduct.Contracts.Auth;

namespace Pruduct.Business.Validators.Auth;

public class ResendConfirmationEmailRequestValidator
    : AbstractValidator<ResendConfirmationEmailRequest>
{
    public ResendConfirmationEmailRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
