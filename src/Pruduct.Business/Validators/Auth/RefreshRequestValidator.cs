using FluentValidation;
using Pruduct.Contracts.Auth;

namespace Pruduct.Business.Validators;

public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
