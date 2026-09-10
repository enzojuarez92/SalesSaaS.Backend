using FluentValidation;

namespace SalesSaaS.Features.Customers.Commands;

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.Name).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(150);
        RuleFor(command => command.DocumentType).NotEmpty().MaximumLength(20);
        RuleFor(command => command.DocumentNumber).NotEmpty().MaximumLength(15).Matches(@"^\d+$");
        When(command => string.Equals(command.DocumentType?.Trim(), "CUIT", StringComparison.OrdinalIgnoreCase) || string.Equals(command.DocumentType?.Trim(), "CUIL", StringComparison.OrdinalIgnoreCase), () =>
            RuleFor(command => command.DocumentNumber).Must(SalesSaaS.Application.Validation.ArgentineTaxId.IsValid)
                .WithMessage("El CUIT/CUIL debe contener exactamente 11 dígitos numéricos y ser válido."));
        When(command => string.Equals(command.DocumentType?.Trim(), "DNI", StringComparison.OrdinalIgnoreCase), () =>
            RuleFor(command => command.DocumentNumber).Length(8).WithMessage("El DNI debe contener exactamente 8 dígitos numéricos."));
        RuleFor(command => command.TaxCondition).NotEmpty().MaximumLength(50);
        RuleFor(command => command.Email).EmailAddress().When(command => !string.IsNullOrWhiteSpace(command.Email));
        RuleFor(command => command.CreditLimit).GreaterThanOrEqualTo(0);
        RuleFor(command => command.LegalName).MaximumLength(150);
    }
}
