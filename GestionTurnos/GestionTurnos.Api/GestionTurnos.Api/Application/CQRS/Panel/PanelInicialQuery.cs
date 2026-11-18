using GestionTurnos.Api.Application.Common;
using GestionTurnos.Api.Application.DTOs.Panel;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Errors.Seguridad;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GestionTurnos.Api.Application.CQRS.Panel;

/// <summary>
/// Panel inicial del administrador (FLUJO_BD_ADMIN sección 3): seis contadores en una sola consulta.
/// Accesible para quien tenga al menos un permiso administrativo; un Empleado recibe 403.
/// </summary>
public sealed record ObtenerPanelInicialQuery : IRequest<Result<PanelInicialDto>>, IRequireAuthentication;

public sealed class ObtenerPanelInicialQueryHandler : IRequestHandler<ObtenerPanelInicialQuery, Result<PanelInicialDto>>
{
    private readonly GestionTurnosContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IConfiguration _configuration;

    public ObtenerPanelInicialQueryHandler(
        GestionTurnosContext context,
        ICurrentUserService currentUserService,
        IConfiguration configuration)
    {
        _context = context;
        _currentUserService = currentUserService;
        _configuration = configuration;
    }

    public async Task<Result<PanelInicialDto>> Handle(ObtenerPanelInicialQuery request, CancellationToken cancellationToken)
    {
        if (!PermisosSistema.Todos.Any(_currentUserService.TienePermiso))
        {
            return Result.Failure<PanelInicialDto>(AuthErrors.Forbidden);
        }

        // "Hoy" en la zona horaria de la empresa (equivale a current_date con la zona fijada en la sesión).
        var hoy = TimeZoneHelper.GetCurrentDateInEmpresaTimeZone(_configuration);
        var pendiente = EstadosSolicitud.Pendiente;
        var activo = EstadosEmpleado.Activo;
        var sinAsistencia = true;

        // SQL válido en PostgreSQL y SQL Server: alias entre comillas dobles y conteos convertidos a int.
        var panel = await _context.Database
            .SqlQuery<PanelInicialDto>($"""
                SELECT
                  CAST((SELECT count(*) FROM seguridad.solicitud_registro   WHERE cod_estado_solicitud = {pendiente}) AS int) AS "RegistrosPendientes",
                  CAST((SELECT count(*) FROM novedades.solicitud_vacaciones WHERE cod_estado_solicitud = {pendiente}) AS int) AS "VacacionesPendientes",
                  CAST((SELECT count(*) FROM novedades.novedad              WHERE cod_estado_solicitud = {pendiente}) AS int) AS "NovedadesPendientes",
                  CAST((SELECT count(*) FROM organizacion.empleado          WHERE cod_estado_empleado  = {activo})    AS int) AS "EmpleadosActivos",
                  CAST((SELECT count(*) FROM asistencia.v_horas_por_turno   WHERE fecha = {hoy})                      AS int) AS "TurnosHoy",
                  CAST((SELECT count(*) FROM asistencia.v_horas_por_turno   WHERE fecha = {hoy} AND sin_asistencia = {sinAsistencia}) AS int) AS "SinAsistenciaHoy"
                """)
            .ToListAsync(cancellationToken);

        return Result.Success(panel.Single());
    }
}
