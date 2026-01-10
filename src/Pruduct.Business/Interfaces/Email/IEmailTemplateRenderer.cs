namespace Pruduct.Business.Interfaces.Email;

public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync<TModel>(
        string templateName,
        TModel model,
        CancellationToken ct = default
    );
}
