using FluentValidation;

namespace SalesSaaS.Features.Customers.Commands;

public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("El TenantId es obligatorio.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre o razón social es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre no puede superar los 150 caracteres.");

        RuleFor(x => x.DocumentType)
            .NotEmpty().WithMessage("El tipo de documento es obligatorio.");

        RuleFor(x => x.DocumentNumber)
            .NotEmpty().WithMessage("El número de documento es obligatorio.")
            .MaximumLength(15).WithMessage("El documento no puede tener más de 15 caracteres.")
            .Matches(@"^[0-9\-]+$").WithMessage("El documento solo puede contener números y guiones.");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
            .WithMessage("El formato del correo electrónico no es válido.");

        RuleFor(x => x.CreditLimit)
            .GreaterThanOrEqualTo(0).WithMessage("El límite de crédito no puede ser negativo.");
    }
}