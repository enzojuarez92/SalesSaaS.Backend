using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Catalog.Categories;

public sealed record CreateCategoryCommand(Guid TenantId, string Name, string? Description) : IRequest<Guid>, ITenantScopedRequest;
public sealed record GetCategoriesQuery(Guid TenantId) : IRequest<IReadOnlyList<CategoryDto>>, ITenantScopedRequest;
public sealed record UpdateCategoryCommand(Guid Id, Guid TenantId, string Name, string? Description, bool IsActive) : IRequest<bool>, ITenantScopedRequest;
public sealed record DeleteCategoryCommand(Guid Id, Guid TenantId) : IRequest<bool>, ITenantScopedRequest;
public sealed record CategoryDto(Guid Id, string Name, string? Description, bool IsActive);

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(command => command.Name).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(100).WithMessage("El nombre de la categoría es obligatorio y no puede superar los 100 caracteres.");
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

public sealed class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator() { RuleFor(command => command.Id).NotEmpty(); RuleFor(command => command.TenantId).NotEmpty(); RuleFor(command => command.Name).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(100); RuleFor(command => command.Description).MaximumLength(500); }
}
public sealed class UpdateCategoryCommandHandler(ApplicationDbContext context) : IRequestHandler<UpdateCategoryCommand, bool>
{
    public async Task<bool> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await context.Categories.SingleOrDefaultAsync(item => item.Id == request.Id && item.TenantId == request.TenantId, cancellationToken); if (category is null) return false;
        var name = request.Name.Trim(); if (await context.Categories.AnyAsync(item => item.TenantId == request.TenantId && item.Id != request.Id && item.Name == name, cancellationToken)) throw new InvalidOperationException("Ya existe una categoría con ese nombre.");
        category.Name = name; category.Description = request.Description?.Trim(); category.IsActive = request.IsActive; await context.SaveChangesAsync(cancellationToken); return true;
    }
}
public sealed class DeleteCategoryCommandHandler(ApplicationDbContext context) : IRequestHandler<DeleteCategoryCommand, bool>
{
    public async Task<bool> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await context.Categories.SingleOrDefaultAsync(item => item.Id == request.Id && item.TenantId == request.TenantId, cancellationToken); if (category is null) return false;
        if (await context.Products.AnyAsync(item => item.TenantId == request.TenantId && item.CategoryId == category.Id, cancellationToken)) throw new InvalidOperationException("No se puede eliminar una categoría con productos asociados.");
        context.Categories.Remove(category); await context.SaveChangesAsync(cancellationToken); return true;
    }
}
