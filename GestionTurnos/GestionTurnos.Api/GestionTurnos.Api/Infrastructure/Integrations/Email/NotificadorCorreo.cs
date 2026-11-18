using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using GestionTurnos.Api.Application.Common.Notificaciones;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Shared.EMAIL;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionTurnos.Api.Infrastructure.Integrations.Email;

/// <summary>
/// Renderiza la plantilla (Scriban, mediante el <see cref="ITemplateRenderer"/> existente), la envuelve en el layout
/// común, genera la alternativa en texto plano y la deja en <see cref="ColaCorreos"/>.
/// </summary>
public sealed partial class NotificadorCorreo : INotificadorCorreo
{
    private const string Layout = "_layout";
    private static readonly ConcurrentDictionary<string, string> CachePlantillas = new();

    private static readonly Lazy<byte[]?> LogoBytesLazy = new(() =>
    {
        // 1. Intentar recurso embebido "Email.Logo_APP.png"
        try
        {
            var stream = typeof(NotificadorCorreo).Assembly.GetManifestResourceStream("Email.Logo_APP.png");
            if (stream != null)
            {
                using (stream)
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }
        catch { }

        // 2. Intentar archivo en disco (desarrollo local)
        var rutas = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Templates", "Email", "Logo_APP.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Templates", "Email", "Logo_APP.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "GestionTurnos.Api", "Infrastructure", "Templates", "Email", "Logo_APP.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Logo_APP.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "GestionTurnos.IU", "public", "Logo_APP.png")
        };
        foreach (var ruta in rutas)
        {
            try
            {
                if (File.Exists(ruta))
                {
                    return File.ReadAllBytes(ruta);
                }
            }
            catch { }
        }
        return null;
    });

    private readonly ITemplateRenderer _renderer;
    private readonly ColaCorreos _cola;
    private readonly IOptionsMonitor<NotificacionesSettings> _settings;
    private readonly ILogger<NotificadorCorreo> _logger;

    public NotificadorCorreo(
        ITemplateRenderer renderer,
        ColaCorreos cola,
        IOptionsMonitor<NotificacionesSettings> settings,
        ILogger<NotificadorCorreo> logger)
    {
        _renderer = renderer;
        _cola = cola;
        _settings = settings;
        _logger = logger;
    }

    public async Task<bool> EncolarAsync(
        string plantilla,
        DestinatarioCorreo destinatario,
        string asunto,
        object modelo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinatario.Correo))
        {
            _logger.LogWarning("Correo [{Plantilla}] omitido: destinatario sin dirección.", plantilla);
            return false;
        }

        try
        {
            var settings = _settings.CurrentValue;
            var baseUrl = settings.UrlAplicacion?.TrimEnd('/');
            var logoBytes = LogoBytesLazy.Value;
            var tieneLogo = logoBytes != null && logoBytes.Length > 0;

            var sistema = new
            {
                Nombre = settings.NombreSistema,
                Url = settings.UrlAplicacion,
                Anio = DateTime.UtcNow.Year,
                LogoCid = tieneLogo ? "logo_empresa" : null,
                LogoUrl = !string.IsNullOrWhiteSpace(baseUrl) ? $"{baseUrl}/Logo_APP.png" : null
            };

            var cuerpo = await _renderer.RenderAsync(ObtenerPlantilla(plantilla), new { Modelo = modelo, Sistema = sistema }, cancellationToken);
            var html = await _renderer.RenderAsync(ObtenerPlantilla(Layout), new { Asunto = asunto, Contenido = cuerpo, Sistema = sistema }, cancellationToken);

            var recursos = new List<EmailLinkedResource>();
            if (tieneLogo)
            {
                recursos.Add(new EmailLinkedResource("logo_empresa", "image/png", logoBytes!));
            }

            var mensaje = new EmailMessage(
                destinatario.Correo.Trim(),
                destinatario.Nombre,
                $"{asunto} | {settings.NombreSistema}",
                html,
                HtmlATexto(html),
                recursos.Count > 0 ? recursos : null)
            {
                Category = plantilla
            };

            if (_cola.TryEncolar(mensaje))
            {
                return true;
            }

            _logger.LogError("Cola de correos llena. Correo [{Plantilla}] para {Recipient} descartado.",
                plantilla, EmailMask.Mask(destinatario.Correo));
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo preparar el correo [{Plantilla}] para {Recipient}.",
                plantilla, EmailMask.Mask(destinatario.Correo));
            return false;
        }
    }

    /// <summary>
    /// En desarrollo revisa el sistema de archivos para cambios en caliente;
    /// si no existe en disco, lee el recurso embebido en el ensamblado (producción/publicación).
    /// </summary>
    private static string ObtenerPlantilla(string nombre)
    {
        // 1. Probar en disco (para desarrollo local sin recompilar plantillas)
        var rutasDisco = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Templates", "Email", $"{nombre}.html"),
            Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Templates", "Email", $"{nombre}.html"),
            Path.Combine(Directory.GetCurrentDirectory(), "GestionTurnos.Api", "Infrastructure", "Templates", "Email", $"{nombre}.html")
        };

        foreach (var ruta in rutasDisco)
        {
            try
            {
                if (File.Exists(ruta))
                {
                    return File.ReadAllText(ruta);
                }
            }
            catch { }
        }

        // 2. Fallback a recurso embebido en el ensamblado
        return CachePlantillas.GetOrAdd(nombre, static n =>
        {
            var recurso = $"Email.{n}.html";
            using var stream = typeof(NotificadorCorreo).Assembly.GetManifestResourceStream(recurso)
                ?? throw new InvalidOperationException($"No se encontró la plantilla de correo embebida '{recurso}'.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
    }

    /// <summary>Alternativa en texto plano (mejora la entregabilidad y la lectura en clientes sin HTML).</summary>
    private static string HtmlATexto(string html)
    {
        var texto = EstilosYComentarios().Replace(html, string.Empty);
        texto = SaltosDeBloque().Replace(texto, "\n");
        texto = Etiquetas().Replace(texto, string.Empty);
        texto = WebUtility.HtmlDecode(texto);
        texto = EspaciosHorizontales().Replace(texto, " ");
        texto = LineasVacias().Replace(texto, "\n\n");
        return texto.Trim();
    }

    [GeneratedRegex(@"<(style|head)[\s\S]*?</\1>|<!--[\s\S]*?-->", RegexOptions.IgnoreCase)]
    private static partial Regex EstilosYComentarios();

    [GeneratedRegex(@"<br\s*/?>|</(p|div|h[1-6]|tr|li|table)>", RegexOptions.IgnoreCase)]
    private static partial Regex SaltosDeBloque();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Etiquetas();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex EspaciosHorizontales();

    [GeneratedRegex(@"(\s*\n){3,}")]
    private static partial Regex LineasVacias();
}
