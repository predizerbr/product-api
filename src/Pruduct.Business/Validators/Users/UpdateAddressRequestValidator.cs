using FluentValidation;
using Pruduct.Contracts.Users;

namespace Pruduct.Business.Validators;

public class UpdateAddressRequestValidator : AbstractValidator<UpdateAddressRequest>
{
    public UpdateAddressRequestValidator()
    {
        When(x => x.ZipCode is not null, () => RuleFor(x => x.ZipCode).NotEmpty());
        When(x => x.Street is not null, () => RuleFor(x => x.Street).NotEmpty());
        When(x => x.Neighborhood is not null, () => RuleFor(x => x.Neighborhood).NotEmpty());
        When(x => x.City is not null, () => RuleFor(x => x.City).NotEmpty());
        When(x => x.State is not null, () => RuleFor(x => x.State).NotEmpty());
    }
}
