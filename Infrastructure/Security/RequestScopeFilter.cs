using Microsoft.AspNetCore.Mvc.Filters;
using SalesSaaS.Application.Exceptions;
using SalesSaaS.Application.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace SalesSaaS.Infrastructure.Security;

public sealed class RequestScopeFilter(ICurrentUser user, ApplicationDbContext db) : IAsyncActionFilter
{
    private static readonly HashSet<string> ScopedControllers = new(StringComparer.OrdinalIgnoreCase)
    { "AuditLogs", "Products", "CashRegisters", "Orders", "Sales", "Invoices", "Afip", "Cash", "StockMovements", "Reports", "Dashboard", "Analytics", "Purchases", "Customers", "Suppliers" };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (user.IsAuthenticated)
        {
            var controller = context.RouteData.Values["controller"]?.ToString() ?? "";
            if (ScopedControllers.Contains(controller) && !user.WarehouseId.HasValue)
                throw new InvalidOperationException("Seleccioná una sucursal para continuar.");
            foreach (var (name, value) in context.ActionArguments)
            {
                Check(name, value);
                if (value is null || value is string || value.GetType().IsValueType) continue;
                foreach (var property in value.GetType().GetProperties().Where(p => p.GetIndexParameters().Length == 0))
                    if (property.Name is "TenantId" or "WarehouseId" or "InitialWarehouseId" or "SourceWarehouseId")
                        Check(property.Name, property.GetValue(value));
            }
        }
        var transactional = new[] { "CashRegisters", "Orders", "Cash", "StockMovements", "Products", "Customers", "Purchases", "Profile" }
            .Contains(context.RouteData.Values["controller"]?.ToString())
            && context.HttpContext.Request.Method is "POST" or "PUT" or "DELETE";
        if (!transactional) { await next(); return; }
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, context.HttpContext.RequestAborted);
        var result = await next();
        if (result.Exception is null && ((result.Result as IStatusCodeActionResult)?.StatusCode ?? 200) < 400)
            await transaction.CommitAsync(context.HttpContext.RequestAborted);
    }

    private void Check(string name, object? value)
    {
        if (value is not Guid id) return;
        if (name.Equals("TenantId", StringComparison.OrdinalIgnoreCase) && id != user.TenantId)
            throw new ForbiddenAccessException("La solicitud no pertenece al negocio autenticado.");
        if ((name.Equals("WarehouseId", StringComparison.OrdinalIgnoreCase) || name is "InitialWarehouseId" or "SourceWarehouseId")
            && id != user.WarehouseId)
            throw new ForbiddenAccessException("La solicitud no corresponde al depósito seleccionado.");
    }
}
