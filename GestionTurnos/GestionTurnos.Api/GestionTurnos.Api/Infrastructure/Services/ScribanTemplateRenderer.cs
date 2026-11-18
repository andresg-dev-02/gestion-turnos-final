using GestionTurnos.Api.Application.Interfaces;
using Scriban;

namespace GestionTurnos.Api.Infrastructure.Services;

public class ScribanTemplateRenderer : ITemplateRenderer
{
    public async Task<string> RenderAsync<TModel>(string htmlContent, TModel model, CancellationToken cancellationToken = default)
    {
        var template = Template.Parse(htmlContent);

        if (template.HasErrors)
        {
            throw new InvalidOperationException("Error parsing template: " + string.Join("; ", template.Messages));
        }

        return await template.RenderAsync(model);
    }
}

