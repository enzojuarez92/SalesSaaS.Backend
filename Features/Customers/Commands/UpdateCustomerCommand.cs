using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;
using SalesSaaS.Application.Security;

namespace SalesSaaS.Features.Customers.Commands;

public record UpdateCustomerCommand(
    Guid Id,
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
    bool IsActive
) : IRequest<Customer?>, ITenantScopedRequest;
public class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand, Customer?>
{
    private readonly ApplicationDbContext _context;

    public UpdateCustomerCommandHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Customer?> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.TenantId == request.TenantId, cancellationToken);

        if (customer == null)
        {
            return null; 
        }

        customer.Name = request.Name;
        customer.DocumentType = request.DocumentType;
        customer.DocumentNumber = request.DocumentNumber;
        customer.TaxCondition = request.TaxCondition;
        customer.Email = request.Email;
        customer.Phone = request.Phone;
        customer.Address = request.Address;
        customer.City = request.City;
        customer.State = request.State;
        customer.PostalCode = request.PostalCode;
        customer.CreditLimit = request.CreditLimit;
        customer.AllowCredit = request.AllowCredit;
        customer.IsActive = request.IsActive;

        customer.UpdatedAt = DateTime.UtcNow; 

        await _context.SaveChangesAsync(cancellationToken);

        return customer;
    }
}
