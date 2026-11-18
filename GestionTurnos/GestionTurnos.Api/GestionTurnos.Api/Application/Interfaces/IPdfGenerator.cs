namespace GestionTurnos.Api.Application.Interfaces;

public interface IPdfGenerator
{
    byte[] GenerateFromHtml(string htmlContent);
}

