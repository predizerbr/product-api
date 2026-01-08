namespace Pruduct.Business.Options;

public class EmailOptions
{
    public const string SectionName = "Email";
    public string FromEmail { get; set; } = "no-reply@product.local";
    public string FromName { get; set; } = "Product";
    public string VerifyEmailUrlBase { get; set; } = string.Empty;
}
