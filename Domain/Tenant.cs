using System.ComponentModel.DataAnnotations;

namespace SalesSaaS.Domain
{
    public class Tenant
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty; 

        [Required]
        [StringLength(20)]
        public string TaxId { get; set; } = string.Empty; 
        public string? LegalName { get; set; }
        public string? TaxCondition { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? LogoUrl { get; set; }
        public string? BusinessCategory { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
