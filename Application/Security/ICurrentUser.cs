namespace SalesSaaS.Application.Security;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid? TenantId { get; }
    Guid? WarehouseId => null;
}
