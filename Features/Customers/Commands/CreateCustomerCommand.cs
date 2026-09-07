using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;
using SalesSaaS.Application.Security;

namespace SalesSaaS.Features.Customers.Commands;

public record CreateCustomerCommand(
    Guid TenantId,
    string Name,
    string DocumentType,
    string DocumentNumber,
    string TaxCondition,
    string Email,
    string Phone,
    string Address,
    string City,
    string State,
    string PostalCode,
    decimal CreditLimit,
    bool AllowCredit,
    string LegalName = ""
) : IRequest<Guid>, ITenantScopedRequest;

public class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, Guid>
{
    private readonly ApplicationDbContext _context;

    public CreateCustomerCommandHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.Customers
            .AnyAsync(c => c.TenantId == request.TenantId && c.DocumentNumber == request.DocumentNumber, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"El documento '{request.DocumentNumber}' ya está registrado para este negocio.");
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Name = request.Name,
            LegalName = string.IsNullOrWhiteSpace(request.LegalName) ? request.Name : request.LegalName,
            DocumentType = request.DocumentType, 
            DocumentNumber = request.DocumentNumber,
            TaxCondition = request.TaxCondition,
            Email = request.Email ?? string.Empty,
            Phone = request.Phone ?? string.Empty,
            Address = request.Address ?? string.Empty,
            City = request.City ?? string.Empty,
            State = request.State ?? string.Empty,
            PostalCode = request.PostalCode ?? string.Empty,
            CreditLimit = request.CreditLimit,
            AllowCredit = request.AllowCredit,
            CreatedAt = DateTime.UtcNow
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(cancellationToken);

        return customer.Id;
    }
}
