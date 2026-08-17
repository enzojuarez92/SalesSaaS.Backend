using FluentValidation;

namespace SalesSaaS.Features.Tenants.Commands;

public sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.TaxId).NotEmpty().MaximumLength(20);
    }
}
