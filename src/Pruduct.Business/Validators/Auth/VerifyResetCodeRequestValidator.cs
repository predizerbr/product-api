using FluentValidation;
using Pruduct.Contracts.Auth;

namespace Pruduct.Business.Validators.Auth;

public class VerifyResetCodeRequestValidator : AbstractValidator<VerifyResetCodeRequest>
{
    public VerifyResetCodeRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.ResetCode).NotEmpty();
    }
}
