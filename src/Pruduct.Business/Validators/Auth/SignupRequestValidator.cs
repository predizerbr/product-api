using FluentValidation;
using Pruduct.Contracts.Auth;

namespace Pruduct.Business.Validators.Auth;

public class SignupRequestValidator : AbstractValidator<SignupRequest>
{
    public SignupRequestValidator()
    {
        // Username may be omitted; backend will derive it from the email local-part when missing.
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.ConfirmPassword).Equal(x => x.Password).WithMessage("Passwords must match");
    }
}
