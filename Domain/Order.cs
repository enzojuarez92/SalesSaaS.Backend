using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesSaaS.Domain;

public class Order
{
    [Key]
    public Guid Id { get; set; }
    public Guid? RequestId { get; set; }
    public string? RequestFingerprint { get; set; }

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid CustomerId { get; set; }
    public Guid WarehouseId { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    public string Status { get; set; } = "Completed"; // "Completed", "Cancelled", etc.

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Relaciones
    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public virtual Customer? Customer { get; set; }
    public Warehouse? Warehouse { get; set; }

    public virtual ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
