namespace Pruduct.Business.Options;

public class EmailSettings
{
    public const string SectionName = "EmailSettings";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
    public bool UseDefaultCredentials { get; set; }
}
