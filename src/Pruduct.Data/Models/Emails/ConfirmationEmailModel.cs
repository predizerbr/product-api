namespace Pruduct.Data.Models.Emails;

public class ConfirmationEmailModel
{
    public string UserName { get; set; } = string.Empty;
    public string ConfirmUrl { get; set; } = string.Empty;
}
