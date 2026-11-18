using GestionTurnos.Api.Application.Interfaces;
using WkHtmlToPdfDotNet;
using WkHtmlToPdfDotNet.Contracts;

namespace GestionTurnos.Api.Infrastructure.Services;

public class DinkToPdfGenerator : IPdfGenerator
{
    private readonly IConverter _converter;

    public DinkToPdfGenerator(IConverter converter)
    {
        _converter = converter;
    }

    public byte[] GenerateFromHtml(string htmlContent)
    {
        var doc = new HtmlToPdfDocument()
        {
            GlobalSettings = {
                ColorMode = ColorMode.Color,
                Orientation = Orientation.Portrait,
                PaperSize = PaperKind.A4,
                Margins = new MarginSettings { Top = 10, Bottom = 10, Left = 10, Right = 10 }
            },
            Objects = {
                new ObjectSettings() {
                    PagesCount = true,
                    HtmlContent = htmlContent,
                    WebSettings = { DefaultEncoding = "utf-8" }
                }
            }
        };

        return _converter.Convert(doc);
    }
}

