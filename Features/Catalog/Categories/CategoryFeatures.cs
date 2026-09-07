using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Catalog.Categories;

public sealed record CreateCategoryCommand(Guid TenantId, string Name, string? Description) : IRequest<Guid>, ITenantScopedRequest;
public sealed record GetCategoriesQuery(Guid TenantId) : IRequest<IReadOnlyList<CategoryDto>>, ITenantScopedRequest;
public sealed record CategoryDto(Guid Id, string Name, string? Description, bool IsActive);

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100).WithMessage("El nombre de la categoría es obligatorio y no puede superar los 100 caracteres.");
        RuleFor(command => command.Description).MaximumLength(500).WithMessage("La descripción no puede superar los 500 caracteres.");
    }
}

public sealed class CreateCategoryCommandHandler(ApplicationDbContext context) : IRequestHandler<CreateCategoryCommand, Guid>
{
    public async Task<Guid> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await context.Categories.AnyAsync(category => category.TenantId == request.TenantId && category.Name == name, cancellationToken))
            throw new InvalidOperationException("Ya existe una categoría con ese nombre.");
        var category = new Category { Id = Guid.NewGuid(), TenantId = request.TenantId, Name = name, Description = request.Description?.Trim() };
        context.Categories.Add(category);
        await context.SaveChangesAsync(cancellationToken);
        return category.Id;
    }
}

public sealed class GetCategoriesQueryHandler(ApplicationDbContext context) : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    public async Task<IReadOnlyList<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken) =>
        await context.Categories.AsNoTracking().Where(category => category.TenantId == request.TenantId).OrderBy(category => category.Name)
            .Select(category => new CategoryDto(category.Id, category.Name, category.Description, category.IsActive)).ToListAsync(cancellationToken);
}
