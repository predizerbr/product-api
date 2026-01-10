namespace Pruduct.Business.Services.Mailers
{
    public class NotifyService
    {
        private readonly IEmailSender _email;

        public NotifyService(IEmailSender email) => _email = email;

        public Task Welcome(string to) =>
            _email.SendAsync(to, "Bem-vindo", "<h1>Olá!</h1><p>Conta criada.</p>");
    }
}
