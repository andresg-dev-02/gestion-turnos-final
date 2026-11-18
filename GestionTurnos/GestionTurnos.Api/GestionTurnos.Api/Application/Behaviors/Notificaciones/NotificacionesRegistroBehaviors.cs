using GestionTurnos.Api.Application.Common.Notificaciones;
using GestionTurnos.Api.Application.CQRS.Seguridad.Registro;
using GestionTurnos.Api.Application.DTOs.Seguridad;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Application.Notificaciones;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionTurnos.Api.Application.Behaviors.Notificaciones;

/// <summary>
/// Correo de aprobación de registro: se envía al correo de la cuenta creada cuando
/// <see cref="AprobarSolicitudRegistroCommand"/> confirma la transacción.
/// </summary>
public sealed class NotificarAprobacionRegistroBehavior
    : NotificacionCorreoBehavior<AprobarSolicitudRegistroCommand, Result<AprobacionRegistroResultDto>, object>
{
    private readonly GestionTurnosContext _context;
    private readonly INotificadorCorreo _notificador;

    public NotificarAprobacionRegistroBehavior(
        GestionTurnosContext context,
        INotificadorCorreo notificador,
        IOptionsMonitor<NotificacionesSettings> settings,
        ILogger<NotificarAprobacionRegistroBehavior> logger)
        : base(settings, logger)
    {
        _context = context;
        _notificador = notificador;
    }

    protected override bool EventoHabilitado(NotificacionesEventosSettings eventos) => eventos.RegistroAprobado;

    protected override async Task NotificarAsync(
        AprobarSolicitudRegistroCommand request,
        Result<AprobacionRegistroResultDto> response,
        object? captura,
        CancellationToken cancellationToken)
    {
        var datos = await _context.Empleados
            .AsNoTracking()
            .Where(e => e.IdEmpleado == response.Value.IdEmpleadoCreado)
            .Select(e => new
            {
                Nombre = e.Nombres + " " + e.Apellidos,
                Correo = e.Usuario!.Correo,
                Area = e.Area.Nombre,
                Cargo = e.Cargo.Nombre,
                e.FechaIngreso
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (datos is null)
        {
            Logger.LogWarning("No se encontró el empleado {IdEmpleado} para notificar la aprobación.", response.Value.IdEmpleadoCreado);
            return;
        }

        await _notificador.EncolarAsync(
            PlantillasCorreo.RegistroAprobado,
            new DestinatarioCorreo(datos.Correo, datos.Nombre),
            "Su registro fue aprobado",
            new RegistroAprobadoModelo(datos.Nombre, datos.Correo, datos.Area, datos.Cargo, FormatoCorreo.Fecha(datos.FechaIngreso)),
            cancellationToken);
    }
}

/// <summary>
/// Complemento natural de la aprobación: informa al solicitante el rechazo y su motivo
/// (hoy solo lo descubriría al intentar iniciar sesión).
/// </summary>
public sealed class NotificarRechazoRegistroBehavior
    : NotificacionCorreoBehavior<RechazarSolicitudRegistroCommand, Result, object>
{
    private readonly GestionTurnosContext _context;
    private readonly INotificadorCorreo _notificador;

    public NotificarRechazoRegistroBehavior(
        GestionTurnosContext context,
        INotificadorCorreo notificador,
        IOptionsMonitor<NotificacionesSettings> settings,
        ILogger<NotificarRechazoRegistroBehavior> logger)
        : base(settings, logger)
    {
        _context = context;
        _notificador = notificador;
    }

    protected override bool EventoHabilitado(NotificacionesEventosSettings eventos) => eventos.RegistroRechazado;

    protected override async Task NotificarAsync(
        RechazarSolicitudRegistroCommand request,
        Result response,
        object? captura,
        CancellationToken cancellationToken)
    {
        var solicitud = await _context.SolicitudesRegistro
            .AsNoTracking()
            .Where(s => s.IdSolicitudRegistro == request.IdSolicitudRegistro)
            .Select(s => new { Nombre = s.Nombres + " " + s.Apellidos, s.Correo, s.MotivoRechazo })
            .FirstOrDefaultAsync(cancellationToken);

        if (solicitud is null)
        {
            return;
        }

        await _notificador.EncolarAsync(
            PlantillasCorreo.RegistroRechazado,
            new DestinatarioCorreo(solicitud.Correo, solicitud.Nombre),
            "Resultado de su solicitud de registro",
            new RegistroRechazadoModelo(solicitud.Nombre, solicitud.MotivoRechazo ?? request.MotivoRechazo),
            cancellationToken);
    }
}
