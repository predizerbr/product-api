using System.Reflection;
using FluentEmail.Core.Interfaces;
using Pruduct.Business.Interfaces.Email;

namespace Pruduct.Business.Services.Mailers;

public class RazorEmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly ITemplateRenderer _renderer;
    private readonly Assembly _assembly;

    public RazorEmailTemplateRenderer(ITemplateRenderer renderer)
    {
        _renderer = renderer;
        _assembly = typeof(RazorEmailTemplateRenderer).Assembly;
    }

    public async Task<string> RenderAsync<TModel>(
        string templateName,
        TModel model,
        CancellationToken ct = default
    )
    {
        var resourceName = $"{_assembly.GetName().Name}.Templates.Emails.{templateName}.cshtml";
        await using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Email template not found: {resourceName}");
        }

        using var reader = new StreamReader(stream);
        var template = await reader.ReadToEndAsync(ct);
        return await _renderer.ParseAsync(template, model, isHtml: true);
    }
}
