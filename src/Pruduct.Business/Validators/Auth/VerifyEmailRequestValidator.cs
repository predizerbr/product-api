using FluentValidation;
using Pruduct.Contracts.Auth;

namespace Pruduct.Business.Validators;

public class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
    }
}
