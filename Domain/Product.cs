using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesSaaS.Domain
{
    public class Product
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid TenantId { get; set; }
        public Guid? CategoryId { get; set; }
        public Guid? BrandId { get; set; }

        [Required]
        [StringLength(50)]
        public string Sku { get; set; } = string.Empty; 

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty; 

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; } 

        public int Stock { get; set; }

        public int MinimumStockAlert { get; set; } 

        // SQL Server la actualiza en cada modificación y EF la usa para evitar
        // que dos ventas concurrentes sobrescriban el stock entre sí.
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(TenantId))]
        public virtual Tenant? Tenant { get; set; }
        public Category? Category { get; set; }
        public Brand? Brand { get; set; }
    }
}
