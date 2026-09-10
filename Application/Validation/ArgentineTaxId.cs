namespace SalesSaaS.Application.Validation;

/// <summary>Validates an Argentine CUIT/CUIL without accepting separators or non-numeric characters.</summary>
public static class ArgentineTaxId
{
    private static readonly int[] Weights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 11 || !value.All(char.IsDigit)) return false;

        var sum = 0;
        for (var index = 0; index < Weights.Length; index++) sum += (value[index] - '0') * Weights[index];

        var verifier = 11 - sum % 11;
        if (verifier == 11) verifier = 0;
        if (verifier == 10) verifier = 9;
        return verifier == value[10] - '0';
    }
}
