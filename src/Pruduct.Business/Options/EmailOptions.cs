namespace Pruduct.Business.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;

    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    public string FromName { get; set; } = "";
    public string FromEmail { get; set; } = "";
}
