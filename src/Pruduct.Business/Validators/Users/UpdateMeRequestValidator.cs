using FluentValidation;
using Pruduct.Contracts.Users;

namespace Pruduct.Business.Validators.Users;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        When(x => x.Name is not null, () => RuleFor(x => x.Name).NotEmpty());
        When(x => x.Username is not null, () => RuleFor(x => x.Username).NotEmpty());
        When(x => x.Email is not null, () => RuleFor(x => x.Email).NotEmpty().EmailAddress());

        When(
            x => !string.IsNullOrWhiteSpace(x.Password),
            () =>
            {
                RuleFor(x => x.Password).MinimumLength(6);
                RuleFor(x => x.ConfirmPassword)
                    .Equal(x => x.Password)
                    .WithMessage("Passwords must match");
            }
        );

        When(x => x.Cpf is not null, () => RuleFor(x => x.Cpf).NotEmpty());
        When(x => x.PhoneNumber is not null, () => RuleFor(x => x.PhoneNumber).NotEmpty());
    }
}
