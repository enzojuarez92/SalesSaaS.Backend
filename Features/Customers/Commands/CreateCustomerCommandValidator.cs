using FluentValidation;

namespace SalesSaaS.Features.Customers.Commands;

public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("El TenantId es obligatorio.");

        RuleFor(x => x.Name)
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("El nombre o razón social es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre no puede superar los 150 caracteres.");

        RuleFor(x => x.DocumentType)
            .NotEmpty().WithMessage("El tipo de documento es obligatorio.");

        RuleFor(x => x.DocumentNumber)
            .NotEmpty().WithMessage("El número de documento es obligatorio.")
            .MaximumLength(15).WithMessage("El documento no puede tener más de 15 caracteres.")
            .Matches(@"^\d+$").WithMessage("El documento solo puede contener números.");

        When(x => string.Equals(x.DocumentType?.Trim(), "CUIT", StringComparison.OrdinalIgnoreCase) || string.Equals(x.DocumentType?.Trim(), "CUIL", StringComparison.OrdinalIgnoreCase), () =>
            RuleFor(x => x.DocumentNumber).Must(SalesSaaS.Application.Validation.ArgentineTaxId.IsValid)
                .WithMessage("El CUIT/CUIL debe contener exactamente 11 dígitos numéricos y ser válido."));
        When(x => string.Equals(x.DocumentType?.Trim(), "DNI", StringComparison.OrdinalIgnoreCase), () =>
            RuleFor(x => x.DocumentNumber).Length(8).WithMessage("El DNI debe contener exactamente 8 dígitos numéricos."));

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
            .WithMessage("El formato del correo electrónico no es válido.");

        RuleFor(x => x.CreditLimit)
            .GreaterThanOrEqualTo(0).WithMessage("El límite de crédito no puede ser negativo.");

        RuleFor(x => x.LegalName).MaximumLength(150).WithMessage("La razón social no puede superar los 150 caracteres.");
    }
}
