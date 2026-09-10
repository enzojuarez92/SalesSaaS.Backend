using FluentValidation;

namespace SalesSaaS.Features.Products.Queries;

public sealed class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>
{
    public GetProductsQueryValidator()
    {
        RuleFor(query => query.TenantId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.StockStatus).Must(value => string.IsNullOrWhiteSpace(value) || new[] { "low", "available", "out" }.Contains(value.Trim().ToLowerInvariant())).WithMessage("El estado de stock no es válido.");
    }
}
