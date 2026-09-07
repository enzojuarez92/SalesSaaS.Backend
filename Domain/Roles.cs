namespace SalesSaaS.Domain;

public static class Roles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Seller = "Seller";
    public const string Warehouse = "Warehouse";

    public const string Sales = Owner + "," + Admin + "," + Seller;
    public const string Inventory = Owner + "," + Admin + "," + Warehouse;
    public const string Administration = Owner + "," + Admin;
}
