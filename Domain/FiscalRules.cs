namespace SalesSaaS.Domain;

public enum FiscalTaxCondition { ResponsibleRegistered, Monotributo, ExemptOrFinal }

public static class FiscalRules
{
    public static FiscalTaxCondition Parse(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Contains("MONOTRIB") || normalized.Contains("SIMPLIFICADO")) return FiscalTaxCondition.Monotributo;
        if (normalized.Contains("RESPONSABLE") && normalized.Contains("INSCRIP")) return FiscalTaxCondition.ResponsibleRegistered;
        return FiscalTaxCondition.ExemptOrFinal;
    }

    public static AfipVoucherType ExpectedInvoice(string? issuerTaxCondition, string? customerTaxCondition) =>
        Parse(issuerTaxCondition) == FiscalTaxCondition.ResponsibleRegistered
            ? Parse(customerTaxCondition) is FiscalTaxCondition.ResponsibleRegistered or FiscalTaxCondition.Monotributo ? AfipVoucherType.InvoiceA : AfipVoucherType.InvoiceB
            : AfipVoucherType.InvoiceC;

    public static AfipVoucherType ExpectedCreditNote(AfipVoucherType original) => original switch
    {
        AfipVoucherType.InvoiceA => AfipVoucherType.CreditNoteA,
        AfipVoucherType.InvoiceB => AfipVoucherType.CreditNoteB,
        AfipVoucherType.InvoiceC => AfipVoucherType.CreditNoteC,
        _ => throw new InvalidOperationException("La nota de crédito sólo puede asociarse a una factura A, B o C.")
    };

    public static int VatId(decimal rate) => rate switch
    {
        0m => 3,
        10.5m => 4,
        21m => 5,
        _ => throw new InvalidOperationException("La alícuota de IVA debe ser 0%, 10,5% o 21%.")
    };
}
