using GestionTurnos.Api.Application.Common;
using GestionTurnos.Api.Application.Common.Notificaciones;
using GestionTurnos.Api.Application.CQRS.Novedades;
using GestionTurnos.Api.Application.CQRS.Turnos;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Application.Notificaciones;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionTurnos.Api.Application.Behaviors.Notificaciones;

// =====================================================================
// RESOLUCIÓN DE SOLICITUDES (vacaciones, permisos/novedades, cambio de turno)
// Los handlers solo confirman si la solicitud estaba PENDIENTE, así que basta con
// leer el estado final después del commit: no hace falta captura previa.
// =====================================================================

/// <summary>
/// Vacaciones aprobadas o rechazadas: el empleado recibe el resultado, las fechas y, si aplica, el motivo.
/// </summary>
public sealed class NotificarResolucionVacacionesBehavior
    : NotificacionCorreoBehavior<ResolverSolicitudVacacionesCommand, Result, object>
{
    private readonly GestionTurnosContext _context;
    private readonly INotificadorCorreo _notificador;

    public NotificarResolucionVacacionesBehavior(
        GestionTurnosContext context,
        INotificadorCorreo notificador,
        IOptionsMonitor<NotificacionesSettings> settings,
        ILogger<NotificarResolucionVacacionesBehavior> logger)
        : base(settings, logger)
    {
        _context = context;
        _notificador = notificador;
    }

    protected override bool EventoHabilitado(NotificacionesEventosSettings eventos) => eventos.VacacionesResueltas;

    protected override async Task NotificarAsync(
        ResolverSolicitudVacacionesCommand request,
        Result response,
        object? captura,
        CancellationToken cancellationToken)
    {
        var solicitud = await ConsultasSolicitudCorreo.VacacionesAsync(_context, request.IdSolicitudVacaciones, cancellationToken);
        if (solicitud is null || string.IsNullOrWhiteSpace(solicitud.Correo))
        {
            Logger.LogWarning("Sin destinatario para notificar la solicitud de vacaciones {Id}.", request.IdSolicitudVacaciones);
            return;
        }

        bool aprobada = solicitud.Estado == EstadosSolicitud.Aprobada;

        await _notificador.EncolarAsync(
            PlantillasCorreo.SolicitudResuelta,
            new DestinatarioCorreo(solicitud.Correo, solicitud.Nombre),
            aprobada ? "Sus vacaciones fueron aprobadas" : "Su solicitud de vacaciones no fue aprobada",
            new SolicitudResueltaModelo(
                solicitud.Nombre,
                $"Su solicitud de vacaciones #{solicitud.Id} fue revisada.",
                aprobada ? "Aprobada" : "Rechazada",
                aprobada,
                solicitud.Detalles(),
                aprobada ? null : "Motivo indicado por el administrador:",
                aprobada ? null : solicitud.Motivo,
                aprobada
                    ? "Si tenía turnos programados en esas fechas, fueron anulados. Puede consultar su calendario actualizado en el sistema."
                    : "Si tiene dudas, comuníquese con el área de talento humano."),
            cancellationToken);
    }
}

/// <summary>
/// Vacaciones aprobadas que el administrador anula antes de iniciar: el empleado debe saberlo para no ausentarse.
/// </summary>
public sealed class NotificarAnulacionVacacionesBehavior
    : NotificacionCorreoBehavior<AnularVacacionesAprobadasCommand, Result, object>
{
    private readonly GestionTurnosContext _context;
    private readonly INotificadorCorreo _notificador;

    public NotificarAnulacionVacacionesBehavior(
        GestionTurnosContext context,
        INotificadorCorreo notificador,
        IOptionsMonitor<NotificacionesSettings> settings,
        ILogger<NotificarAnulacionVacacionesBehavior> logger)
        : base(settings, logger)
    {
        _context = context;
        _notificador = notificador;
    }

    protected override bool EventoHabilitado(NotificacionesEventosSettings eventos) => eventos.VacacionesAnuladas;

    protected override async Task NotificarAsync(
        AnularVacacionesAprobadasCommand request,
        Result response,
        object? captura,
        CancellationToken cancellationToken)
    {
        var solicitud = await ConsultasSolicitudCorreo.VacacionesAsync(_context, request.IdSolicitudVacaciones, cancellationToken);
        if (solicitud is null || string.IsNullOrWhiteSpace(solicitud.Correo))
        {
            Logger.LogWarning("Sin destinatario para notificar la anulación de vacaciones {Id}.", request.IdSolicitudVacaciones);
            return;
        }

        await _notificador.EncolarAsync(
            PlantillasCorreo.SolicitudResuelta,
            new DestinatarioCorreo(solicitud.Correo, solicitud.Nombre),
            $"Sus vacaciones del {solicitud.FechaInicio:dd/MM/yyyy} fueron anuladas",
            new SolicitudResueltaModelo(
                solicitud.Nombre,
                $"Las vacaciones que tenía aprobadas (solicitud #{solicitud.Id}) fueron anuladas por el administrador.",
                "Anuladas",
                false,
                solicitud.Detalles(),
                "Motivo de la anulación:",
                solicitud.Motivo ?? request.Motivo.Trim(),
                "Los días vuelven a su saldo disponible. Debe presentarse según la programación de turnos vigente; consulte su calendario en el sistema."),
            cancellationToken);
    }
}

/// <summary>
/// Permisos y demás novedades aprobadas o rechazadas. El permiso personal es un tipo de novedad
/// (PERMISO_PERSONAL): solo cambia la redacción del correo.
/// </summary>
public sealed class NotificarResolucionNovedadBehavior
    : NotificacionCorreoBehavior<ResolverNovedadCommand, Result, object>
{
    private readonly GestionTurnosContext _context;
    private readonly INotificadorCorreo _notificador;
    private readonly IConfiguration _configuration;

    public NotificarResolucionNovedadBehavior(
        GestionTurnosContext context,
        INotificadorCorreo notificador,
        IConfiguration configuration,
        IOptionsMonitor<NotificacionesSettings> settings,
        ILogger<NotificarResolucionNovedadBehavior> logger)
        : base(settings, logger)
    {
        _context = context;
        _notificador = notificador;
        _configuration = configuration;
    }

    protected override bool EventoHabilitado(NotificacionesEventosSettings eventos) => eventos.NovedadResuelta;

    protected override async Task NotificarAsync(
        ResolverNovedadCommand request,
        Result response,
        object? captura,
        CancellationToken cancellationToken)
    {
        var novedad = await _context.Novedades
            .AsNoTracking()
            .Where(n => n.IdNovedad == request.IdNovedad)
            .Select(n => new
            {
                n.IdNovedad,
                n.CodTipoNovedad,
                Tipo = n.TipoNovedad.Nombre,
                n.Inicio,
                n.Fin,
                n.Remunerado,
                n.CodEstadoSolicitud,
                n.MotivoRechazo,
                Nombre = n.Empleado.Nombres + " " + n.Empleado.Apellidos,
                Correo = n.Empleado.CorreoContacto ?? (n.Empleado.Usuario != null ? n.Empleado.Usuario.Correo : null)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (novedad is null || string.IsNullOrWhiteSpace(novedad.Correo))
        {
            Logger.LogWarning("Sin destinatario para notificar la novedad {Id}.", request.IdNovedad);
            return;
        }

        bool aprobada = novedad.CodEstadoSolicitud == EstadosSolicitud.Aprobada;
        bool esPermiso = novedad.CodTipoNovedad == TiposNovedadCodigos.PermisoPersonal;
        string sujeto = esPermiso ? "permiso" : "novedad";
        var zona = TimeZoneHelper.ResolveEmpresaTimeZone(_configuration);

        var detalles = new List<DetalleCorreoItem>
        {
            new("Tipo", novedad.Tipo),
            new("Desde", FormatoCorreo.FechaHora(novedad.Inicio, zona)),
            new("Hasta", FormatoCorreo.FechaHora(novedad.Fin, zona))
        };

        if (aprobada)
        {
            detalles.Add(new("Remunerado", novedad.Remunerado ? "Sí" : "No"));
        }

        string asunto = (aprobada, esPermiso) switch
        {
            (true, true) => "Su permiso fue aprobado",
            (true, false) => $"Su novedad ({novedad.Tipo}) fue aprobada",
            (false, true) => "Su solicitud de permiso no fue aprobada",
            (false, false) => $"Su novedad ({novedad.Tipo}) no fue aprobada"
        };

        await _notificador.EncolarAsync(
            PlantillasCorreo.SolicitudResuelta,
            new DestinatarioCorreo(novedad.Correo, novedad.Nombre),
            asunto,
            new SolicitudResueltaModelo(
                novedad.Nombre,
                $"Su solicitud de {sujeto} #{novedad.IdNovedad} fue revisada.",
                esPermiso ? (aprobada ? "Aprobado" : "Rechazado") : (aprobada ? "Aprobada" : "Rechazada"),
                aprobada,
                detalles,
                aprobada ? null : "Motivo indicado por el administrador:",
                aprobada ? null : novedad.MotivoRechazo ?? request.MotivoRechazo,
                aprobada
                    ? "Los turnos futuros que se cruzaban con este periodo pudieron ser anulados. Consulte su calendario actualizado en el sistema."
                    : "Debe presentarse a sus turnos según la programación vigente. Si tiene dudas, comuníquese con el área de talento humano."),
            cancellationToken);
    }
}

/// <summary>
/// Cambio de turno aprobado o rechazado.
///  - Aprobado: se notifica al solicitante y al compañero, cada uno con el turno que entrega y el que recibe.
///  - Rechazado: solo al solicitante (para el compañero nada cambia).
/// Las asignaciones originales quedan ANULADA tras la aprobación pero siguen existiendo, por eso se leen después.
/// </summary>
public sealed class NotificarResolucionCambioTurnoBehavior
    : NotificacionCorreoBehavior<ResolverCambioTurnoCommand, Result, object>
{
    private readonly GestionTurnosContext _context;
    private readonly INotificadorCorreo _notificador;

    public NotificarResolucionCambioTurnoBehavior(
        GestionTurnosContext context,
        INotificadorCorreo notificador,
        IOptionsMonitor<NotificacionesSettings> settings,
        ILogger<NotificarResolucionCambioTurnoBehavior> logger)
        : base(settings, logger)
    {
        _context = context;
        _notificador = notificador;
    }

    protected override bool EventoHabilitado(NotificacionesEventosSettings eventos) => eventos.CambioTurnoResuelto;

    protected override async Task NotificarAsync(
        ResolverCambioTurnoCommand request,
        Result response,
        object? captura,
        CancellationToken cancellationToken)
    {
        var solicitud = await _context.SolicitudesCambioTurno
            .AsNoTracking()
            .Where(s => s.IdSolicitudCambio == request.IdSolicitudCambio)
            .Select(s => new { s.IdSolicitudCambio, s.IdAsignacionSolicitante, s.IdAsignacionCompanero, s.CodEstadoSolicitud, s.MotivoRechazo })
            .FirstOrDefaultAsync(cancellationToken);

        if (solicitud is null)
        {
            return;
        }

        var propia = await ConsultasAsignacionCorreo.ObtenerAsync(_context, solicitud.IdAsignacionSolicitante, cancellationToken);
        var companero = await ConsultasAsignacionCorreo.ObtenerAsync(_context, solicitud.IdAsignacionCompanero, cancellationToken);
        if (propia is null || companero is null)
        {
            return;
        }

        bool aprobada = solicitud.CodEstadoSolicitud == EstadosSolicitud.Aprobada;

        if (!aprobada)
        {
            await EnviarAsync(propia, companero, solicitud.IdSolicitudCambio, aprobada: false,
                solicitud.MotivoRechazo ?? request.MotivoRechazo, esSolicitante: true, cancellationToken);
            return;
        }

        await EnviarAsync(propia, companero, solicitud.IdSolicitudCambio, aprobada: true, null, esSolicitante: true, cancellationToken);
        await EnviarAsync(companero, propia, solicitud.IdSolicitudCambio, aprobada: true, null, esSolicitante: false, cancellationToken);
    }

    private async Task EnviarAsync(
        AsignacionCorreo entrega,
        AsignacionCorreo recibe,
        long idSolicitud,
        bool aprobada,
        string? motivo,
        bool esSolicitante,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entrega.Correo))
        {
            Logger.LogWarning("El empleado {IdEmpleado} no tiene correo; no se notificó el cambio de turno {Id}.", entrega.IdEmpleado, idSolicitud);
            return;
        }

        var turnoEntrega = entrega.ComoItem();
        var turnoRecibe = recibe.ComoItem();

        var detalles = new List<DetalleCorreoItem>
        {
            new(aprobada ? "Turno que entrega" : "Su turno", $"{turnoEntrega.Fecha} · {turnoEntrega.Turno} ({turnoEntrega.Horario})"),
            new(aprobada ? "Turno que recibe" : "Turno solicitado", $"{turnoRecibe.Fecha} · {turnoRecibe.Turno} ({turnoRecibe.Horario})"),
            new(esSolicitante ? "Compañero" : "Solicitado por", recibe.NombreEmpleado)
        };

        string mensaje = esSolicitante
            ? $"Su solicitud de cambio de turno #{idSolicitud} fue revisada."
            : $"Se aprobó un cambio de turno (#{idSolicitud}) solicitado por {recibe.NombreEmpleado} que lo involucra.";

        await _notificador.EncolarAsync(
            PlantillasCorreo.SolicitudResuelta,
            new DestinatarioCorreo(entrega.Correo, entrega.NombreEmpleado),
            aprobada ? "Cambio de turno aprobado" : "Su cambio de turno no fue aprobado",
            new SolicitudResueltaModelo(
                entrega.NombreEmpleado,
                mensaje,
                aprobada ? "Aprobado" : "Rechazado",
                aprobada,
                detalles,
                aprobada ? null : "Motivo indicado por el administrador:",
                motivo,
                aprobada
                    ? "Su calendario ya refleja el intercambio. Preséntese en el turno que recibe."
                    : "Su turno original se mantiene sin cambios."),
            cancellationToken);
    }
}

internal sealed record SolicitudVacacionesCorreo(
    long Id,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    decimal Dias,
    string Estado,
    string? Motivo,
    string Nombre,
    string? Correo)
{
    public IReadOnlyList<DetalleCorreoItem> Detalles() =>
    [
        new("Desde", FormatoCorreo.Fecha(FechaInicio)),
        new("Hasta", FormatoCorreo.Fecha(FechaFin)),
        new("Días hábiles", FormatoCorreo.Dias(Dias))
    ];
}

internal static class ConsultasSolicitudCorreo
{
    public static Task<SolicitudVacacionesCorreo?> VacacionesAsync(
        GestionTurnosContext context, long idSolicitud, CancellationToken cancellationToken) =>
        context.SolicitudesVacaciones
            .AsNoTracking()
            .Where(s => s.IdSolicitudVacaciones == idSolicitud)
            .Select(s => new SolicitudVacacionesCorreo(
                s.IdSolicitudVacaciones,
                s.FechaInicio,
                s.FechaFin,
                s.DiasSolicitados,
                s.CodEstadoSolicitud,
                s.MotivoRechazo,
                s.Empleado.Nombres + " " + s.Empleado.Apellidos,
                s.Empleado.CorreoContacto ?? (s.Empleado.Usuario != null ? s.Empleado.Usuario.Correo : null)))
            .FirstOrDefaultAsync(cancellationToken);
}
