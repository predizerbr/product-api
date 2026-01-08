using FluentValidation;
using Pruduct.Contracts.Auth;

namespace Pruduct.Business.Validators;

public class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
