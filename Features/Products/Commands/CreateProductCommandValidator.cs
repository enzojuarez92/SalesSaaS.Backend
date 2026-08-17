using FluentValidation;

namespace SalesSaaS.Features.Products.Commands;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El TenantId es obligatorio.");
        RuleFor(command => command.Sku).NotEmpty().MaximumLength(50);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.Price).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Cost).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Stock).GreaterThanOrEqualTo(0);
        RuleFor(command => command.MinimumStockAlert).GreaterThanOrEqualTo(0);
    }
}
