using FluentValidation;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Auditoria;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GestionTurnos.Api.Application.CQRS.Reportes;

// =====================================================================
// 14. REPORTES: HORAS POR CATEGORÍA, VACACIONES, PERMISOS Y AUSENCIAS
// (permiso: reportes.consultar). Solo lectura, rango de fechas obligatorio
// de máximo un año y detalle paginado.
// =====================================================================

/// <summary>
/// Horas por categoría (ordinaria, extra, recargos) por empleado:
/// suma asistencia.detalle_horas de los registros cuya entrada cae en el rango (zona horaria de la empresa).
/// </summary>
[RequirePermission(PermisosSistema.ReportesConsultar)]
public sealed record ReporteHorasPorCategoriaQuery(
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    long? IdEmpleado = null,
    int? IdArea = null,
    int? IdCargo = null,
    int? IdTurno = null,
    string? CodCategoriaHora = null,
    int PageNumber = 1,
    int PageSize = 50)
    : IRequest<Result<PagedResult<ReporteHorasCategoriaDto>>>, IReporteConRangoFechas;

public sealed class ReporteHorasPorCategoriaQueryValidator : AbstractValidator<ReporteHorasPorCategoriaQuery>
{
    public ReporteHorasPorCategoriaQueryValidator()
    {
        this.AplicarReglasRangoReporte();
    }
}

public sealed class ReporteHorasPorCategoriaQueryHandler
    : IRequestHandler<ReporteHorasPorCategoriaQuery, Result<PagedResult<ReporteHorasCategoriaDto>>>
{
    private readonly GestionTurnosContext _context;
    private readonly IConfiguration _configuration;

    public ReporteHorasPorCategoriaQueryHandler(GestionTurnosContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<Result<PagedResult<ReporteHorasCategoriaDto>>> Handle(
        ReporteHorasPorCategoriaQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Paginacion.NormalizarPagina(request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize, 1, RangoFechasReporte.PageSizeMaximo);
        var (desde, hasta) = RangoFechasReporte.AInstantes(_configuration, request.FechaDesde, request.FechaHasta);

        var query = from dh in _context.DetallesHoras.AsNoTracking()
                    join r in _context.RegistrosAsistencia.AsNoTracking() on dh.IdRegistro equals r.IdRegistro
                    where r.HoraEntrada >= desde && r.HoraEntrada < hasta
                    select new
                    {
                        r.IdEmpleado,
                        r.Empleado.NumeroDocumento,
                        NombreEmpleado = r.Empleado.Nombres + " " + r.Empleado.Apellidos,
                        r.Empleado.IdArea,
                        NombreArea = r.Empleado.Area.Nombre,
                        r.Empleado.IdCargo,
                        NombreCargo = r.Empleado.Cargo.Nombre,
                        IdTurno = r.Asignacion != null ? (int?)r.Asignacion.IdTurno : null,
                        dh.CodCategoriaHora,
                        NombreCategoria = dh.CategoriaHora.Nombre,
                        dh.CategoriaHora.FactorPago,
                        dh.Minutos
                    };

        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(x => x.IdEmpleado == request.IdEmpleado.Value);
        }

        if (request.IdArea.HasValue)
        {
            query = query.Where(x => x.IdArea == request.IdArea.Value);
        }

        if (request.IdCargo.HasValue)
        {
            query = query.Where(x => x.IdCargo == request.IdCargo.Value);
        }

        if (request.IdTurno.HasValue)
        {
            query = query.Where(x => x.IdTurno == request.IdTurno.Value);
        }

        var categoria = RangoFechasReporte.NormalizarCodigo(request.CodCategoriaHora);
        if (categoria is not null)
        {
            query = query.Where(x => x.CodCategoriaHora == categoria);
        }

        var agrupado = query
            .GroupBy(x => new
            {
                x.IdEmpleado,
                x.NumeroDocumento,
                x.NombreEmpleado,
                x.NombreArea,
                x.NombreCargo,
                x.CodCategoriaHora,
                x.NombreCategoria,
                x.FactorPago
            })
            .Select(g => new
            {
                g.Key.IdEmpleado,
                g.Key.NumeroDocumento,
                g.Key.NombreEmpleado,
                g.Key.NombreArea,
                g.Key.NombreCargo,
                g.Key.CodCategoriaHora,
                g.Key.NombreCategoria,
                g.Key.FactorPago,
                TotalMinutos = g.Sum(x => x.Minutos)
            });

        var totalCount = await agrupado.CountAsync(cancellationToken);

        var filas = await agrupado
            .OrderBy(x => x.NombreEmpleado)
            .ThenBy(x => x.IdEmpleado)
            .ThenBy(x => x.CodCategoriaHora)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = filas
            .Select(x => new ReporteHorasCategoriaDto(
                x.IdEmpleado,
                x.NumeroDocumento,
                x.NombreEmpleado,
                x.NombreArea,
                x.NombreCargo,
                x.CodCategoriaHora,
                x.NombreCategoria,
                x.FactorPago,
                x.TotalMinutos,
                Math.Round(x.TotalMinutos / 60m, 2)))
            .ToList();

        return Result.Success(new PagedResult<ReporteHorasCategoriaDto>(items, totalCount, pageNumber, pageSize));
    }
}

/// <summary>
/// Saldo de vacaciones por empleado (novedades.v_saldo_vacaciones) junto con los días
/// aprobados y pendientes de las solicitudes que se cruzan con el rango.
/// El filtro de estado es el estado del empleado.
/// </summary>
[RequirePermission(PermisosSistema.ReportesConsultar)]
public sealed record ReporteSaldosVacacionesQuery(
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    long? IdEmpleado = null,
    int? IdArea = null,
    int? IdCargo = null,
    string? CodEstadoEmpleado = null,
    int PageNumber = 1,
    int PageSize = 50)
    : IRequest<Result<PagedResult<ReporteSaldoVacacionesDto>>>, IReporteConRangoFechas;

public sealed class ReporteSaldosVacacionesQueryValidator : AbstractValidator<ReporteSaldosVacacionesQuery>
{
    public ReporteSaldosVacacionesQueryValidator()
    {
        this.AplicarReglasRangoReporte();
    }
}

public sealed class ReporteSaldosVacacionesQueryHandler
    : IRequestHandler<ReporteSaldosVacacionesQuery, Result<PagedResult<ReporteSaldoVacacionesDto>>>
{
    private readonly GestionTurnosContext _context;

    public ReporteSaldosVacacionesQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<PagedResult<ReporteSaldoVacacionesDto>>> Handle(
        ReporteSaldosVacacionesQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Paginacion.NormalizarPagina(request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize, 1, RangoFechasReporte.PageSizeMaximo);
        var fechaDesde = request.FechaDesde;
        var fechaHasta = request.FechaHasta;

        var empleados = _context.Empleados.AsNoTracking();

        if (request.IdEmpleado.HasValue)
        {
            empleados = empleados.Where(e => e.IdEmpleado == request.IdEmpleado.Value);
        }

        if (request.IdArea.HasValue)
        {
            empleados = empleados.Where(e => e.IdArea == request.IdArea.Value);
        }

        if (request.IdCargo.HasValue)
        {
            empleados = empleados.Where(e => e.IdCargo == request.IdCargo.Value);
        }

        var estadoEmpleado = RangoFechasReporte.NormalizarCodigo(request.CodEstadoEmpleado);
        if (estadoEmpleado is not null)
        {
            empleados = empleados.Where(e => e.CodEstadoEmpleado == estadoEmpleado);
        }

        var query = from e in empleados
                    join s in _context.VistaSaldosVacaciones.AsNoTracking() on e.IdEmpleado equals s.IdEmpleado
                    select new { Empleado = e, Saldo = s };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Empleado.Nombres)
            .ThenBy(x => x.Empleado.Apellidos)
            .ThenBy(x => x.Empleado.IdEmpleado)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ReporteSaldoVacacionesDto(
                x.Empleado.IdEmpleado,
                x.Empleado.NumeroDocumento,
                x.Empleado.Nombres + " " + x.Empleado.Apellidos,
                x.Empleado.Area.Nombre,
                x.Empleado.Cargo.Nombre,
                x.Empleado.CodEstadoEmpleado,
                x.Saldo.DiasOtorgados,
                x.Saldo.DiasUtilizados,
                x.Saldo.DiasEnSolicitud,
                x.Saldo.DiasDisponibles,
                _context.SolicitudesVacaciones
                    .Where(v => v.IdEmpleado == x.Empleado.IdEmpleado &&
                                v.CodEstadoSolicitud == EstadosSolicitud.Aprobada &&
                                v.FechaInicio <= fechaHasta && v.FechaFin >= fechaDesde)
                    .Sum(v => (decimal?)v.DiasSolicitados) ?? 0m,
                _context.SolicitudesVacaciones
                    .Where(v => v.IdEmpleado == x.Empleado.IdEmpleado &&
                                v.CodEstadoSolicitud == EstadosSolicitud.Pendiente &&
                                v.FechaInicio <= fechaHasta && v.FechaFin >= fechaDesde)
                    .Sum(v => (decimal?)v.DiasSolicitados) ?? 0m))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<ReporteSaldoVacacionesDto>(items, totalCount, pageNumber, pageSize));
    }
}

/// <summary>
/// Solicitudes de vacaciones que se cruzan con el rango (cualquier estado si no se filtra).
/// </summary>
[RequirePermission(PermisosSistema.ReportesConsultar)]
public sealed record ReporteSolicitudesVacacionesQuery(
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    long? IdEmpleado = null,
    int? IdArea = null,
    int? IdCargo = null,
    string? CodEstadoSolicitud = null,
    int PageNumber = 1,
    int PageSize = 50)
    : IRequest<Result<PagedResult<ReporteSolicitudVacacionesDto>>>, IReporteConRangoFechas;

public sealed class ReporteSolicitudesVacacionesQueryValidator : AbstractValidator<ReporteSolicitudesVacacionesQuery>
{
    public ReporteSolicitudesVacacionesQueryValidator()
    {
        this.AplicarReglasRangoReporte();
    }
}

public sealed class ReporteSolicitudesVacacionesQueryHandler
    : IRequestHandler<ReporteSolicitudesVacacionesQuery, Result<PagedResult<ReporteSolicitudVacacionesDto>>>
{
    private readonly GestionTurnosContext _context;

    public ReporteSolicitudesVacacionesQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<PagedResult<ReporteSolicitudVacacionesDto>>> Handle(
        ReporteSolicitudesVacacionesQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Paginacion.NormalizarPagina(request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize, 1, RangoFechasReporte.PageSizeMaximo);

        var query = _context.SolicitudesVacaciones
            .AsNoTracking()
            .Where(v => v.FechaInicio <= request.FechaHasta && v.FechaFin >= request.FechaDesde);

        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(v => v.IdEmpleado == request.IdEmpleado.Value);
        }

        if (request.IdArea.HasValue)
        {
            query = query.Where(v => v.Empleado.IdArea == request.IdArea.Value);
        }

        if (request.IdCargo.HasValue)
        {
            query = query.Where(v => v.Empleado.IdCargo == request.IdCargo.Value);
        }

        var estado = RangoFechasReporte.NormalizarCodigo(request.CodEstadoSolicitud);
        if (estado is not null)
        {
            query = query.Where(v => v.CodEstadoSolicitud == estado);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(v => v.FechaInicio)
            .ThenByDescending(v => v.IdSolicitudVacaciones)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new ReporteSolicitudVacacionesDto(
                v.IdSolicitudVacaciones,
                v.IdEmpleado,
                v.Empleado.NumeroDocumento,
                v.Empleado.Nombres + " " + v.Empleado.Apellidos,
                v.Empleado.Area.Nombre,
                v.Empleado.Cargo.Nombre,
                v.FechaInicio,
                v.FechaFin,
                v.DiasSolicitados,
                v.CodEstadoSolicitud,
                v.EstadoSolicitud.Nombre,
                v.SolicitadoEn,
                v.ResueltoEn,
                v.MotivoRechazo))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<ReporteSolicitudVacacionesDto>(items, totalCount, pageNumber, pageSize));
    }
}

/// <summary>
/// Permisos, incapacidades y ausencias (novedades.novedad) que se cruzan con el rango
/// en la zona horaria de la empresa (cualquier estado si no se filtra).
/// </summary>
[RequirePermission(PermisosSistema.ReportesConsultar)]
public sealed record ReportePermisosAusenciasQuery(
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    long? IdEmpleado = null,
    int? IdArea = null,
    int? IdCargo = null,
    string? CodTipoNovedad = null,
    string? CodEstadoSolicitud = null,
    bool? Remunerado = null,
    int PageNumber = 1,
    int PageSize = 50)
    : IRequest<Result<PagedResult<ReportePermisoAusenciaDto>>>, IReporteConRangoFechas;

public sealed class ReportePermisosAusenciasQueryValidator : AbstractValidator<ReportePermisosAusenciasQuery>
{
    public ReportePermisosAusenciasQueryValidator()
    {
        this.AplicarReglasRangoReporte();
    }
}

public sealed class ReportePermisosAusenciasQueryHandler
    : IRequestHandler<ReportePermisosAusenciasQuery, Result<PagedResult<ReportePermisoAusenciaDto>>>
{
    private readonly GestionTurnosContext _context;
    private readonly IConfiguration _configuration;

    public ReportePermisosAusenciasQueryHandler(GestionTurnosContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<Result<PagedResult<ReportePermisoAusenciaDto>>> Handle(
        ReportePermisosAusenciasQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Paginacion.NormalizarPagina(request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize, 1, RangoFechasReporte.PageSizeMaximo);
        var (desde, hasta) = RangoFechasReporte.AInstantes(_configuration, request.FechaDesde, request.FechaHasta);

        var query = _context.Novedades
            .AsNoTracking()
            .Where(n => n.Inicio < hasta && n.Fin > desde);

        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(n => n.IdEmpleado == request.IdEmpleado.Value);
        }

        if (request.IdArea.HasValue)
        {
            query = query.Where(n => n.Empleado.IdArea == request.IdArea.Value);
        }

        if (request.IdCargo.HasValue)
        {
            query = query.Where(n => n.Empleado.IdCargo == request.IdCargo.Value);
        }

        var tipo = RangoFechasReporte.NormalizarCodigo(request.CodTipoNovedad);
        if (tipo is not null)
        {
            query = query.Where(n => n.CodTipoNovedad == tipo);
        }

        var estado = RangoFechasReporte.NormalizarCodigo(request.CodEstadoSolicitud);
        if (estado is not null)
        {
            query = query.Where(n => n.CodEstadoSolicitud == estado);
        }

        if (request.Remunerado.HasValue)
        {
            query = query.Where(n => n.Remunerado == request.Remunerado.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var filas = await query
            .OrderByDescending(n => n.Inicio)
            .ThenByDescending(n => n.IdNovedad)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                n.IdNovedad,
                n.IdEmpleado,
                n.Empleado.NumeroDocumento,
                NombreEmpleado = n.Empleado.Nombres + " " + n.Empleado.Apellidos,
                NombreArea = n.Empleado.Area.Nombre,
                NombreCargo = n.Empleado.Cargo.Nombre,
                n.CodTipoNovedad,
                NombreTipoNovedad = n.TipoNovedad.Nombre,
                n.Inicio,
                n.Fin,
                n.Remunerado,
                n.AfectaHoras,
                n.CodEstadoSolicitud,
                NombreEstadoSolicitud = n.EstadoSolicitud.Nombre,
                n.RegistradoEn,
                n.ResueltoEn,
                n.MotivoRechazo
            })
            .ToListAsync(cancellationToken);

        // La duración se calcula en memoria para no depender de funciones de fecha propias del proveedor.
        var items = filas
            .Select(n => new ReportePermisoAusenciaDto(
                n.IdNovedad,
                n.IdEmpleado,
                n.NumeroDocumento,
                n.NombreEmpleado,
                n.NombreArea,
                n.NombreCargo,
                n.CodTipoNovedad,
                n.NombreTipoNovedad,
                n.Inicio,
                n.Fin,
                (int)(n.Fin - n.Inicio).TotalMinutes,
                n.Remunerado,
                n.AfectaHoras,
                n.CodEstadoSolicitud,
                n.NombreEstadoSolicitud,
                n.RegistradoEn,
                n.ResueltoEn,
                n.MotivoRechazo))
            .ToList();

        return Result.Success(new PagedResult<ReportePermisoAusenciaDto>(items, totalCount, pageNumber, pageSize));
    }
}
