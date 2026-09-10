using FluentValidation;
using SalesSaaS.Application.Validation;

namespace SalesSaaS.Features.Tenants.Commands;

public sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(command => command.Name).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(150).WithMessage("El nombre del negocio es obligatorio y no puede superar los 150 caracteres.");
        RuleFor(command => command.TaxId).Must(ArgentineTaxId.IsValid).WithMessage("El CUIT debe contener exactamente 11 dígitos numéricos y ser válido.");
    }
}
