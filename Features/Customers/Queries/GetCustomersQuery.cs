using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Customers.Queries
{
    public record GetCustomersQuery(Guid TenantId) : IRequest<List<CustomerDto>>;
    public record CustomerDto(
        Guid Id,
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
    );

    public class GetCustomersQueryHandler : IRequestHandler<GetCustomersQuery, List<CustomerDto>>
    {
        private readonly ApplicationDbContext _context;

        public GetCustomersQueryHandler(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<CustomerDto>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
        {
            return await _context.Customers
                .Where(c => c.TenantId == request.TenantId)
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
                .ToListAsync(cancellationToken);
        }
    }
   
}
