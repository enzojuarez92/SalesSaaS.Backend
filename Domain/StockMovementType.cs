namespace SalesSaaS.Domain;

public enum StockMovementType
{
    Receipt = 1,
    Issue = 2,
    AdjustmentIncrease = 3,
    AdjustmentDecrease = 4,
    TransferOut = 5,
    TransferIn = 6
}
