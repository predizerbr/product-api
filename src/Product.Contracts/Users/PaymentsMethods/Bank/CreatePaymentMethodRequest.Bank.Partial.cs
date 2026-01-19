namespace Product.Contracts.Users.PaymentsMethods;

public partial class CreatePaymentMethodRequest
{
    public string? BankCode { get; set; }
    public string? BankName { get; set; }
    public string? Agency { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountDigit { get; set; }
    public string? AccountType { get; set; }
}
