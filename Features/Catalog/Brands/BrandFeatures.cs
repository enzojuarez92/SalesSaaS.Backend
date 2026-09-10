using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Catalog.Brands;

public sealed record CreateBrandCommand(Guid TenantId, string Name) : IRequest<Guid>, ITenantScopedRequest;
public sealed record GetBrandsQuery(Guid TenantId) : IRequest<IReadOnlyList<BrandDto>>, ITenantScopedRequest;
public sealed record BrandDto(Guid Id, string Name, bool IsActive);

public sealed class CreateBrandCommandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(command => command.Name).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(100).WithMessage("El nombre de la marca es obligatorio y no puede superar los 100 caracteres.");
    }
}

public sealed class CreateBrandCommandHandler(ApplicationDbContext context) : IRequestHandler<CreateBrandCommand, Guid>
{
    public async Task<Guid> Handle(CreateBrandCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await context.Brands.AnyAsync(brand => brand.TenantId == request.TenantId && brand.Name == name, cancellationToken))
            throw new InvalidOperationException("Ya existe una marca con ese nombre.");
        var brand = new Brand { Id = Guid.NewGuid(), TenantId = request.TenantId, Name = name };
        context.Brands.Add(brand);
        await context.SaveChangesAsync(cancellationToken);
        return brand.Id;
    }
}

public sealed class GetBrandsQueryHandler(ApplicationDbContext context) : IRequestHandler<GetBrandsQuery, IReadOnlyList<BrandDto>>
{
    public async Task<IReadOnlyList<BrandDto>> Handle(GetBrandsQuery request, CancellationToken cancellationToken) =>
        await context.Brands.AsNoTracking().Where(brand => brand.TenantId == request.TenantId).OrderBy(brand => brand.Name)
            .Select(brand => new BrandDto(brand.Id, brand.Name, brand.IsActive)).ToListAsync(cancellationToken);
}
