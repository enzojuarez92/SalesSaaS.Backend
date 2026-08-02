using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Customers.Commands;
public record DeleteCustomerCommand(Guid Id, Guid TenantId) : IRequest<bool>;

public class DeleteCustomerCommandHandler : IRequestHandler<DeleteCustomerCommand, bool>
{
    private readonly ApplicationDbContext _context;

    public DeleteCustomerCommandHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.TenantId == request.TenantId, cancellationToken);

        if (customer == null)
        {
            return false;
        }

        customer.IsActive = false;
        customer.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}