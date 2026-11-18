namespace GestionTurnos.Api.Application.Interfaces;

public interface ITemplateRenderer
{
    Task<string> RenderAsync<TModel>(string htmlContent, TModel model, CancellationToken cancellationToken = default);
}

