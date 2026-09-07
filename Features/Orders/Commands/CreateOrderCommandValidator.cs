using FluentValidation;

namespace SalesSaaS.Features.Orders.Commands;

public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El TenantId es obligatorio.");
        RuleFor(command => command.CustomerId).NotEmpty().WithMessage("El ID del cliente es obligatorio.");
        RuleFor(command => command.WarehouseId).NotEmpty().WithMessage("El ID del depósito es obligatorio.");
        RuleFor(command => command.Items).NotEmpty().WithMessage("La venta debe contener al menos un producto.");
        RuleFor(command => command.Items)
            .Must(items => items.Select(item => item.ProductId).Distinct().Count() == items.Count)
            .When(command => command.Items is not null)
            .WithMessage("Un producto solo puede aparecer una vez en la venta.");
        RuleForEach(command => command.Items).ChildRules(item =>
        {
            item.RuleFor(orderItem => orderItem.ProductId).NotEmpty().WithMessage("El ID del producto es obligatorio.");
            item.RuleFor(orderItem => orderItem.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
        });
    }
}
