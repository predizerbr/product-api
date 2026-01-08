using FluentValidation;
using Pruduct.Contracts.Users;

namespace Pruduct.Business.Validators;

public class UpdateAvatarRequestValidator : AbstractValidator<UpdateAvatarRequest>
{
    public UpdateAvatarRequestValidator()
    {
        RuleFor(x => x.AvatarUrl).NotEmpty();
    }
}
