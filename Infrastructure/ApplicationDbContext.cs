using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure;

public class ApplicationDbContext : DbContext
{
    private readonly ICurrentUser _currentUser;
    private bool _isWritingAudit;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUser currentUser) : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantFiscalProfile> TenantFiscalProfiles => Set<TenantFiscalProfile>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<SaaSInvoice> SaaSInvoices => Set<SaaSInvoice>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<User> Users => Set<User>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Quote> Quotes => Set<Quote>(); public DbSet<QuoteItem> QuoteItems => Set<QuoteItem>();
    public DbSet<Invoice> Invoices => Set<Invoice>(); public DbSet<CustomerAccountEntry> CustomerAccountEntries => Set<CustomerAccountEntry>();
    public DbSet<Supplier> Suppliers => Set<Supplier>(); public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>(); public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>(); public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>(); public DbSet<SupplierAccountEntry> SupplierAccountEntries => Set<SupplierAccountEntry>(); public DbSet<CashRegisterSession> CashRegisterSessions => Set<CashRegisterSession>(); public DbSet<CashMovement> CashMovements => Set<CashMovement>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AddAuditLogs();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AddAuditLogs();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void AddAuditLogs()
    {
        if (_isWritingAudit) return;
        _isWritingAudit = true;
        try
        {
            ChangeTracker.DetectChanges();
            var entries = ChangeTracker.Entries().Where(entry => entry.Entity is not AuditLog && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
            foreach (var entry in entries)
            {
                var tenantProperty = entry.Properties.FirstOrDefault(property => property.Metadata.Name == "TenantId");
                if (tenantProperty?.CurrentValue is not Guid tenantId || tenantId == Guid.Empty) continue;
                var action = entry.State == EntityState.Added ? AuditAction.Create : entry.State == EntityState.Deleted ? AuditAction.Delete : AuditAction.Update;
                var values = entry.Properties.Where(property => !IsSensitive(property.Metadata.Name) && (entry.State != EntityState.Modified || property.IsModified)).ToDictionary(property => property.Metadata.Name, property => new { Old = entry.State == EntityState.Added ? null : property.OriginalValue, New = entry.State == EntityState.Deleted ? null : property.CurrentValue });
                AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), TenantId = tenantId, UserId = _currentUser.UserId, EntityName = entry.Metadata.ClrType.Name, Action = action, ChangesJson = JsonSerializer.Serialize(values), TimestampUtc = DateTime.UtcNow });
            }
        }
        finally { _isWritingAudit = false; }
    }

    private static bool IsSensitive(string propertyName) => propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase) || propertyName.Contains("Token", StringComparison.OrdinalIgnoreCase) || propertyName.Contains("Certificate", StringComparison.OrdinalIgnoreCase) || propertyName.Contains("PrivateKey", StringComparison.OrdinalIgnoreCase) || propertyName.Contains("Passphrase", StringComparison.OrdinalIgnoreCase);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<Product>().HasQueryFilter(product =>
            !_currentUser.TenantId.HasValue || product.TenantId == _currentUser.TenantId);
        modelBuilder.Entity<TenantFiscalProfile>().HasQueryFilter(profile =>
            !_currentUser.TenantId.HasValue || profile.TenantId == _currentUser.TenantId);
        modelBuilder.Entity<TenantSubscription>().HasQueryFilter(subscription =>
            !_currentUser.TenantId.HasValue || subscription.TenantId == _currentUser.TenantId);
        modelBuilder.Entity<SaaSInvoice>().HasQueryFilter(invoice =>
            !_currentUser.TenantId.HasValue || invoice.TenantId == _currentUser.TenantId);
        modelBuilder.Entity<Notification>().HasQueryFilter(notification =>
            !_currentUser.TenantId.HasValue || notification.TenantId == _currentUser.TenantId);
        modelBuilder.Entity<AuditLog>().HasQueryFilter(log =>
            !_currentUser.TenantId.HasValue || log.TenantId == _currentUser.TenantId);
        modelBuilder.Entity<Customer>().HasQueryFilter(customer =>
            !_currentUser.TenantId.HasValue || customer.TenantId == _currentUser.TenantId);
        modelBuilder.Entity<Order>().HasQueryFilter(order =>
            !_currentUser.TenantId.HasValue || order.TenantId == _currentUser.TenantId);
        modelBuilder.Entity<OrderItem>().HasQueryFilter(item =>
            !_currentUser.TenantId.HasValue || item.Order!.TenantId == _currentUser.TenantId);
        modelBuilder.Entity<TenantMembership>().HasQueryFilter(membership =>
            !_currentUser.TenantId.HasValue || membership.TenantId == _currentUser.TenantId);
        modelBuilder.Entity<RefreshToken>().HasQueryFilter(token =>
            !_currentUser.TenantId.HasValue || token.TenantId == _currentUser.TenantId);
        modelBuilder.Entity<Category>().HasQueryFilter(category => !_currentUser.TenantId.HasValue || category.TenantId == _currentUser.TenantId);
        modelBuilder.Entity<Brand>().HasQueryFilter(brand => !_currentUser.TenantId.HasValue || brand.TenantId == _currentUser.TenantId);
        modelBuilder.Entity<Warehouse>().HasQueryFilter(warehouse => !_currentUser.TenantId.HasValue || warehouse.TenantId == _currentUser.TenantId);
        modelBuilder.Entity<StockMovement>().HasQueryFilter(movement => !_currentUser.TenantId.HasValue || movement.TenantId == _currentUser.TenantId);
        modelBuilder.Entity<Quote>().HasQueryFilter(item => !_currentUser.TenantId.HasValue || item.TenantId == _currentUser.TenantId);
        modelBuilder.Entity<QuoteItem>().HasQueryFilter(item => !_currentUser.TenantId.HasValue || item.TenantId == _currentUser.TenantId);
        modelBuilder.Entity<Invoice>().HasQueryFilter(item => !_currentUser.TenantId.HasValue || item.TenantId == _currentUser.TenantId);
        modelBuilder.Entity<CustomerAccountEntry>().HasQueryFilter(item => !_currentUser.TenantId.HasValue || item.TenantId == _currentUser.TenantId);
        modelBuilder.Entity<Supplier>().HasQueryFilter(item => !_currentUser.TenantId.HasValue || item.TenantId == _currentUser.TenantId); modelBuilder.Entity<PurchaseOrder>().HasQueryFilter(item => !_currentUser.TenantId.HasValue || item.TenantId == _currentUser.TenantId); modelBuilder.Entity<PurchaseOrderItem>().HasQueryFilter(item => !_currentUser.TenantId.HasValue || item.TenantId == _currentUser.TenantId); modelBuilder.Entity<PurchaseInvoice>().HasQueryFilter(item => !_currentUser.TenantId.HasValue || item.TenantId == _currentUser.TenantId); modelBuilder.Entity<SupplierAccountEntry>().HasQueryFilter(item => !_currentUser.TenantId.HasValue || item.TenantId == _currentUser.TenantId); modelBuilder.Entity<CashRegisterSession>().HasQueryFilter(item => !_currentUser.TenantId.HasValue || item.TenantId == _currentUser.TenantId); modelBuilder.Entity<CashMovement>().HasQueryFilter(item => !_currentUser.TenantId.HasValue || item.TenantId == _currentUser.TenantId);
    }
}
