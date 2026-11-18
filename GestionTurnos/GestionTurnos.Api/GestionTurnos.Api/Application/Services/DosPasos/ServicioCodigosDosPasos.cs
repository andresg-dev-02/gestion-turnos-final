using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Application.Notificaciones;
using GestionTurnos.Shared.EMAIL;
using Microsoft.Extensions.Options;

namespace GestionTurnos.Api.Application.Services.DosPasos;

/// <summary>
/// Propósitos de los códigos: separan los espacios de claves para que un código de activación no sirva para iniciar sesión.
/// </summary>
public static class PropositosCodigo
{
    public const string InicioSesion = "login";
    public const string Activacion = "activacion";
}

public sealed record DesafioDosPasosDto(string CorreoEnmascarado, DateTimeOffset ExpiraEn);

public interface IServicioCodigosDosPasos
{
    /// <summary>Emite un código y lo envía por correo. Devuelve <c>null</c> si no se pudo encolar el correo (el desafío se descarta).</summary>
    Task<DesafioDosPasosDto?> EmitirYEnviarAsync(
        string proposito,
        string clave,
        string? carga,
        DestinatarioCorreo destinatario,
        CancellationToken cancellationToken = default);

    /// <summary>Reenvía un código nuevo para un desafío vigente. <c>false</c> si no aplica (sin desafío, en espera o sin reenvíos).</summary>
    Task<bool> ReenviarAsync(
        string proposito,
        string clave,
        Func<string?, DestinatarioCorreo?> destinatarioDesdeCarga,
        CancellationToken cancellationToken = default);
}

public sealed class ServicioCodigosDosPasos : IServicioCodigosDosPasos
{
    private readonly ICodigoVerificacionStore _store;
    private readonly INotificadorCorreo _notificador;
    private readonly IOptionsMonitor<DosPasosSettings> _settings;

    public ServicioCodigosDosPasos(
        ICodigoVerificacionStore store,
        INotificadorCorreo notificador,
        IOptionsMonitor<DosPasosSettings> settings)
    {
        _store = store;
        _notificador = notificador;
        _settings = settings;
    }

    public async Task<DesafioDosPasosDto?> EmitirYEnviarAsync(
        string proposito,
        string clave,
        string? carga,
        DestinatarioCorreo destinatario,
        CancellationToken cancellationToken = default)
    {
        var emitido = await _store.EmitirAsync(proposito, clave, carga, cancellationToken);

        if (!await EnviarAsync(proposito, emitido.Codigo, destinatario, cancellationToken))
        {
            await _store.DescartarAsync(proposito, clave, CancellationToken.None);
            return null;
        }

        return new DesafioDosPasosDto(EmailMask.Mask(destinatario.Correo), emitido.ExpiraEn);
    }

    public async Task<bool> ReenviarAsync(
        string proposito,
        string clave,
        Func<string?, DestinatarioCorreo?> destinatarioDesdeCarga,
        CancellationToken cancellationToken = default)
    {
        var reemitido = await _store.ReemitirAsync(proposito, clave, cancellationToken);
        if (reemitido is null)
        {
            return false;
        }

        var destinatario = destinatarioDesdeCarga(reemitido.Carga);
        return destinatario is not null &&
               await EnviarAsync(proposito, reemitido.Codigo, destinatario, cancellationToken);
    }

    private Task<bool> EnviarAsync(string proposito, string codigo, DestinatarioCorreo destinatario, CancellationToken cancellationToken)
    {
        var minutos = _settings.CurrentValue.MinutosVigencia;
        var (titulo, mensaje) = proposito == PropositosCodigo.Activacion
            ? ("Activación de la verificación en dos pasos",
               "Recibimos una solicitud para activar la verificación en dos pasos en su cuenta. Use este código para confirmarla.")
            : ("Código de inicio de sesión",
               "Use este código para completar su inicio de sesión.");

        var modelo = new CodigoVerificacionModelo(
            destinatario.Nombre ?? destinatario.Correo, titulo, mensaje, codigo, minutos);

        // El código va en el cuerpo, no en el asunto: los asuntos aparecen en notificaciones de la pantalla bloqueada.
        return _notificador.EncolarAsync(
            PlantillasCorreo.CodigoVerificacion, destinatario, titulo, modelo, cancellationToken);
    }
}
