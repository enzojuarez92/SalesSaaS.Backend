namespace SalesSaaS.Domain;

public class Customer
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    // 📄 Datos de Identificación
    public string Name { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty; // "DNI", "CUIT", "CUIL", "Pasaporte"
    public string DocumentNumber { get; set; } = string.Empty;
    public string TaxCondition { get; set; } = string.Empty; // "Responsable Inscripto", "Monotributo", "Consumidor Final"

    // 📞 Contacto
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    // 📍 Ubicación / Domicilio Fiscal (Opcionales para la DB)
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;

    // 💼 Datos Comerciales
    public decimal CreditLimit { get; set; } = 0;
    public bool AllowCredit { get; set; } = false;

    // ⚙️ Estado y Auditoría
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}