using FluentValidation;
using Pruduct.Contracts.Auth;

namespace Pruduct.Business.Validators.Auth;

public class GoogleLoginRequestValidator : AbstractValidator<GoogleLoginRequest>
{
    public GoogleLoginRequestValidator()
    {
        RuleFor(x => x.IdToken).NotEmpty();
    }
}
