using GestionTurnos.Api.Application.Common.Notificaciones;
using GestionTurnos.Api.Application.CQRS.Turnos;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Application.Notificaciones;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionTurnos.Api.Application.Behaviors.Notificaciones;

/// <summary>
/// Datos de una asignación necesarios para notificar a su empleado.
/// </summary>
internal sealed record AsignacionCorreo(
    long IdAsignacion,
    long IdEmpleado,
    string Estado,
    DateOnly Fecha,
    int IdTurno,
    string Turno,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    bool CruzaMedianoche,
    string NombreEmpleado,
    string? Correo)
{
    public TurnoCorreoItem ComoItem() => FormatoCorreo.Turno(Fecha, Turno, HoraInicio, HoraFin, CruzaMedianoche);
}

internal static class ConsultasAsignacionCorreo
{
    /// <summary>
    /// Proyección común. El destino es el correo de contacto del empleado y, si no tiene, el de su cuenta.
    /// </summary>
    public static IQueryable<AsignacionCorreo> Proyectar(IQueryable<Domain.Entities.Turnos.Asignacion> query) =>
        query.AsNoTracking().Select(a => new AsignacionCorreo(
            a.IdAsignacion,
            a.IdEmpleado,
            a.CodEstadoAsignacion,
            a.Fecha,
            a.IdTurno,
            a.Turno.Nombre,
            a.Turno.HoraInicio,
            a.Turno.HoraFin,
            a.Turno.CruzaMedianoche,
            a.Empleado.Nombres + " " + a.Empleado.Apellidos,
            a.Empleado.CorreoContacto ?? (a.Empleado.Usuario != null ? a.Empleado.Usuario.Correo : null)));

    public static Task<AsignacionCorreo?> ObtenerAsync(GestionTurnosContext context, long idAsignacion, CancellationToken cancellationToken) =>
        Proyectar(context.Asignaciones.Where(a => a.IdAsignacion == idAsignacion))
            .FirstOrDefaultAsync(cancellationToken);
}

// =====================================================================
// PUBLICACIÓN DE LA PROGRAMACIÓN (rotación de horario)
// =====================================================================

/// <summary>
/// Al publicar (PROGRAMADA → CONFIRMADA) cada empleado afectado recibe UN correo con sus turnos del periodo.
/// Se capturan antes los ids que el handler va a publicar para no reenviar turnos publicados en otra ocasión.
/// Los turnos en estado PROGRAMADA no se notifican: aún no son visibles para el empleado.
/// </summary>
public sealed class NotificarPublicacionTurnosBehavior
    : NotificacionCorreoBehavior<PublicarAsignacionesTurnoCommand, Result<int>, IReadOnlyList<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly INotificadorCorreo _notificador;

    public NotificarPublicacionTurnosBehavior(
        GestionTurnosContext context,
        INotificadorCorreo notificador,
        IOptionsMonitor<NotificacionesSettings> settings,
        ILogger<NotificarPublicacionTurnosBehavior> logger)
        : base(settings, logger)
    {
        _context = context;
        _notificador = notificador;
    }

    protected override bool EventoHabilitado(NotificacionesEventosSettings eventos) => eventos.TurnosPublicados;

    protected override bool RequiereCaptura => true;

    protected override async Task<IReadOnlyList<long>?> CapturarAntesAsync(
        PublicarAsignacionesTurnoCommand request,
        CancellationToken cancellationToken)
    {
        // Mismo filtro que PublicarAsignacionesTurnoCommandHandler.
        var query = _context.Asignaciones.AsNoTracking()
            .Where(a => a.Fecha >= request.FechaDesde &&
                        a.Fecha <= request.FechaHasta &&
                        a.CodEstadoAsignacion == EstadosAsignacion.Programada);

        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(a => a.IdEmpleado == request.IdEmpleado.Value);
        }

        if (request.IdArea.HasValue)
        {
            query = query.Where(a => a.Empleado.IdArea == request.IdArea.Value);
        }

        var ids = await query.Select(a => a.IdAsignacion).ToListAsync(cancellationToken);
        return ids.Count == 0 ? null : ids;
    }

    protected override async Task NotificarAsync(
        PublicarAsignacionesTurnoCommand request,
        Result<int> response,
        IReadOnlyList<long>? idsPrevios,
        CancellationToken cancellationToken)
    {
        if (response.Value == 0 || idsPrevios is null)
        {
            return;
        }

        // Solo las que efectivamente quedaron CONFIRMADA en esta publicación.
        var publicadas = await ConsultasAsignacionCorreo.Proyectar(
                _context.Asignaciones.Where(a => idsPrevios.Contains(a.IdAsignacion) &&
                                                 a.CodEstadoAsignacion == EstadosAsignacion.Confirmada &&
                                                 a.Empleado.CodEstadoEmpleado == EstadosEmpleado.Activo))
            .ToListAsync(cancellationToken);

        foreach (var grupo in publicadas.GroupBy(a => a.IdEmpleado))
        {
            var primero = grupo.First();
            if (string.IsNullOrWhiteSpace(primero.Correo))
            {
                Logger.LogWarning("El empleado {IdEmpleado} no tiene correo; no se notificó su programación.", grupo.Key);
                continue;
            }

            var turnos = grupo
                .OrderBy(a => a.Fecha).ThenBy(a => a.HoraInicio)
                .ToList();

            await _notificador.EncolarAsync(
                PlantillasCorreo.TurnosPublicados,
                new DestinatarioCorreo(primero.Correo, primero.NombreEmpleado),
                $"Su programación de turnos: {turnos[0].Fecha:dd/MM} al {turnos[^1].Fecha:dd/MM}",
                new TurnosPublicadosModelo(
                    primero.NombreEmpleado,
                    FormatoCorreo.Fecha(turnos[0].Fecha),
                    FormatoCorreo.Fecha(turnos[^1].Fecha),
                    turnos.Select(t => t.ComoItem()).ToList()),
                cancellationToken);
        }
    }
}

// =====================================================================
// MODIFICACIÓN DE UN TURNO YA PUBLICADO
// =====================================================================

/// <summary>
/// Si se modifica un turno CONFIRMADO (ya publicado) y cambia la fecha o el turno, el empleado recibe el antes y el después.
/// </summary>
public sealed class NotificarModificacionTurnoBehavior
    : NotificacionCorreoBehavior<ModificarAsignacionTurnoCommand, Result, AsignacionCorreoCaptura>
{
    private readonly GestionTurnosContext _context;
    private readonly INotificadorCorreo _notificador;

    public NotificarModificacionTurnoBehavior(
        GestionTurnosContext context,
        INotificadorCorreo notificador,
        IOptionsMonitor<NotificacionesSettings> settings,
        ILogger<NotificarModificacionTurnoBehavior> logger)
        : base(settings, logger)
    {
        _context = context;
        _notificador = notificador;
    }

    protected override bool EventoHabilitado(NotificacionesEventosSettings eventos) => eventos.TurnoModificado;

    protected override bool RequiereCaptura => true;

    protected override async Task<AsignacionCorreoCaptura?> CapturarAntesAsync(
        ModificarAsignacionTurnoCommand request,
        CancellationToken cancellationToken)
    {
        var previa = await ConsultasAsignacionCorreo.ObtenerAsync(_context, request.IdAsignacion, cancellationToken);
        return previa is { Estado: EstadosAsignacion.Confirmada } ? new AsignacionCorreoCaptura(previa) : null;
    }

    protected override async Task NotificarAsync(
        ModificarAsignacionTurnoCommand request,
        Result response,
        AsignacionCorreoCaptura? captura,
        CancellationToken cancellationToken)
    {
        var anterior = captura!.Asignacion;
        var nueva = await ConsultasAsignacionCorreo.ObtenerAsync(_context, request.IdAsignacion, cancellationToken);

        // Un cambio solo de observación no es un cambio de horario.
        if (nueva is null || string.IsNullOrWhiteSpace(nueva.Correo) ||
            (nueva.Fecha == anterior.Fecha && nueva.IdTurno == anterior.IdTurno))
        {
            return;
        }

        await _notificador.EncolarAsync(
            PlantillasCorreo.TurnoModificado,
            new DestinatarioCorreo(nueva.Correo, nueva.NombreEmpleado),
            $"Cambio en su turno del {anterior.Fecha:dd/MM/yyyy}",
            new TurnoModificadoModelo(nueva.NombreEmpleado, anterior.ComoItem(), nueva.ComoItem()),
            cancellationToken);
    }
}

// =====================================================================
// ANULACIÓN DE UN TURNO YA PUBLICADO
// =====================================================================

/// <summary>
/// Si se anula un turno CONFIRMADO, el empleado recibe la fecha, el turno y el motivo.
/// </summary>
public sealed class NotificarAnulacionTurnoBehavior
    : NotificacionCorreoBehavior<AnularAsignacionTurnoCommand, Result, AsignacionCorreoCaptura>
{
    private readonly GestionTurnosContext _context;
    private readonly INotificadorCorreo _notificador;

    public NotificarAnulacionTurnoBehavior(
        GestionTurnosContext context,
        INotificadorCorreo notificador,
        IOptionsMonitor<NotificacionesSettings> settings,
        ILogger<NotificarAnulacionTurnoBehavior> logger)
        : base(settings, logger)
    {
        _context = context;
        _notificador = notificador;
    }

    protected override bool EventoHabilitado(NotificacionesEventosSettings eventos) => eventos.TurnoAnulado;

    protected override bool RequiereCaptura => true;

    protected override async Task<AsignacionCorreoCaptura?> CapturarAntesAsync(
        AnularAsignacionTurnoCommand request,
        CancellationToken cancellationToken)
    {
        var previa = await ConsultasAsignacionCorreo.ObtenerAsync(_context, request.IdAsignacion, cancellationToken);
        return previa is { Estado: EstadosAsignacion.Confirmada } ? new AsignacionCorreoCaptura(previa) : null;
    }

    protected override async Task NotificarAsync(
        AnularAsignacionTurnoCommand request,
        Result response,
        AsignacionCorreoCaptura? captura,
        CancellationToken cancellationToken)
    {
        var anulada = captura!.Asignacion;
        if (string.IsNullOrWhiteSpace(anulada.Correo))
        {
            return;
        }

        await _notificador.EncolarAsync(
            PlantillasCorreo.TurnoAnulado,
            new DestinatarioCorreo(anulada.Correo, anulada.NombreEmpleado),
            $"Su turno del {anulada.Fecha:dd/MM/yyyy} fue anulado",
            new TurnoAnuladoModelo(anulada.NombreEmpleado, anulada.ComoItem(), request.Observacion?.Trim() ?? string.Empty),
            cancellationToken);
    }
}

/// <summary>
/// Envoltorio público de la captura previa (el genérico de la clase base exige un tipo accesible).
/// </summary>
public sealed class AsignacionCorreoCaptura
{
    internal AsignacionCorreoCaptura(AsignacionCorreo asignacion) => Asignacion = asignacion;

    internal AsignacionCorreo Asignacion { get; }
}
