using FluentValidation;

namespace SalesSaaS.Features.Products.Commands;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El TenantId es obligatorio.");
        RuleFor(command => command.Sku).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(50).WithMessage("El SKU es obligatorio y no puede superar los 50 caracteres.");
        RuleFor(command => command.Name).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(150).WithMessage("El nombre del producto es obligatorio y no puede superar los 150 caracteres.");
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.Price).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Cost).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Stock).GreaterThanOrEqualTo(0);
        RuleFor(command => command.InitialWarehouseId).NotEmpty().When(command => command.Stock > 0).WithMessage("El depósito es obligatorio cuando se carga stock inicial.");
        RuleFor(command => command.MinimumStockAlert).GreaterThanOrEqualTo(0);
    }
}
