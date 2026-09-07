using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Infrastructure;
using SalesSaaS.Application.Security;

namespace SalesSaaS.Features.Customers.Queries
{
    public record GetCustomerByIdQuery(Guid CustomerId, Guid TenantId) : IRequest<CustomerDto?>, ITenantScopedRequest;
    public class GetCustomerByIdQueryHandler : IRequestHandler<GetCustomerByIdQuery, CustomerDto?>
    {
        private readonly ApplicationDbContext _context;

        public GetCustomerByIdQueryHandler(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CustomerDto?> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
        {
            return await _context.Customers
                .Where(c => c.Id == request.CustomerId && c.TenantId == request.TenantId)
                .Select(c => new CustomerDto(
                        c.Id,
                        c.Name,
                        c.DocumentType,
                        c.DocumentNumber,
                        c.TaxCondition,
                        c.Email,
                        c.Phone,
                        c.Address,
                        c.City,
                        c.State,
                        c.PostalCode,
                        c.CreditLimit,
                        c.AllowCredit,
                        c.IsActive
                    ))
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
