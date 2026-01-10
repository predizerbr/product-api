namespace Pruduct.Data.Models.Emails;

public class ForgotPasswordEmailModel
{
    public string UserName { get; set; } = string.Empty;
    public string ResetCode { get; set; } = string.Empty;
}
