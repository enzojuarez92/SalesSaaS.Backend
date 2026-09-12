using FluentValidation;

namespace SalesSaaS.Features.Products.Commands;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty().WithMessage("El ID del producto es obligatorio.");
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El TenantId es obligatorio.");
        RuleFor(command => command.Sku).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(50).WithMessage("El SKU es obligatorio y no puede superar los 50 caracteres.");
        RuleFor(command => command.Name).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(150).WithMessage("El nombre del producto es obligatorio y no puede superar los 150 caracteres.");
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.Price).GreaterThan(0);
        RuleFor(command => command.Cost).GreaterThanOrEqualTo(0);
        RuleFor(command => command.VatRate).Must(rate => rate is 0m or 10.5m or 21m)
            .WithMessage("La alícuota de IVA debe ser 0%, 10,5% o 21%.");
        RuleFor(command => command.Stock).GreaterThanOrEqualTo(0);
        RuleFor(command => command.MinimumStockAlert).GreaterThanOrEqualTo(0);
    }
}
