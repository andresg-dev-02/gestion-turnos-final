using System.Linq.Expressions;
using FluentValidation;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Auditoria;
using GestionTurnos.Api.Domain.Entities.Auditoria;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.CQRS.Auditoria;

// =====================================================================
// 5. CONSULTA DE AUDITORÍA (auditoria.bitacora_negocio - RF-08, RG-09)
// Solo consulta, por empleado, fecha o tipo de acción, paginada y ordenada por fecha_hora DESC.
// =====================================================================

[RequirePermission(PermisosSistema.AuditoriaConsultar)]
public sealed record ConsultarBitacoraQuery(
    long? IdEmpleado = null,
    long? IdUsuario = null,
    string? CodTipoAccion = null,
    string? Esquema = null,
    string? Tabla = null,
    DateTimeOffset? FechaDesde = null,
    DateTimeOffset? FechaHasta = null,
    int PageNumber = 1,
    int PageSize = 25) : IRequest<Result<PagedResult<BitacoraNegocioDto>>>;

public sealed class ConsultarBitacoraQueryValidator : AbstractValidator<ConsultarBitacoraQuery>
{
    public ConsultarBitacoraQueryValidator()
    {
        RuleFor(x => x.FechaHasta)
            .GreaterThanOrEqualTo(x => x.FechaDesde!.Value)
            .When(x => x.FechaDesde.HasValue && x.FechaHasta.HasValue)
            .WithMessage("La fecha final debe ser mayor o igual a la fecha inicial.");
        RuleFor(x => x.CodTipoAccion).MaximumLength(20);
        RuleFor(x => x.Esquema).MaximumLength(63);
        RuleFor(x => x.Tabla).MaximumLength(63);
    }
}

public sealed class ConsultarBitacoraQueryHandler
    : IRequestHandler<ConsultarBitacoraQuery, Result<PagedResult<BitacoraNegocioDto>>>
{
    private readonly GestionTurnosContext _context;

    public ConsultarBitacoraQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<PagedResult<BitacoraNegocioDto>>> Handle(
        ConsultarBitacoraQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Paginacion.NormalizarPagina(request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        IQueryable<BitacoraNegocio> query = _context.BitacoraNegocio.AsNoTracking();

        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(b => b.IdEmpleado == request.IdEmpleado.Value);
        }

        if (request.IdUsuario.HasValue)
        {
            query = query.Where(b => b.IdUsuario == request.IdUsuario.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CodTipoAccion))
        {
            var accion = request.CodTipoAccion.Trim().ToUpperInvariant();
            query = query.Where(b => b.CodTipoAccion == accion);
        }

        if (!string.IsNullOrWhiteSpace(request.Esquema))
        {
            var esquema = request.Esquema.Trim().ToLowerInvariant();
            query = query.Where(b => b.Esquema == esquema);
        }

        if (!string.IsNullOrWhiteSpace(request.Tabla))
        {
            var tabla = request.Tabla.Trim().ToLowerInvariant();
            query = query.Where(b => b.Tabla == tabla);
        }

        if (request.FechaDesde.HasValue)
        {
            query = query.Where(b => b.FechaHora >= request.FechaDesde.Value);
        }

        if (request.FechaHasta.HasValue)
        {
            query = query.Where(b => b.FechaHora <= request.FechaHasta.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(b => b.FechaHora)
            .ThenByDescending(b => b.IdBitacora)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(BitacoraProyeccion.ADto)
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<BitacoraNegocioDto>(items, totalCount, pageNumber, pageSize));
    }
}

/// <summary>
/// Proyección única de la bitácora a DTO (incluye el correo del usuario que hizo el cambio).
/// </summary>
internal static class BitacoraProyeccion
{
    public static readonly Expression<Func<BitacoraNegocio, BitacoraNegocioDto>> ADto = b => new BitacoraNegocioDto(
        b.IdBitacora,
        b.FechaHora,
        b.IdUsuario,
        b.Usuario != null ? b.Usuario.Correo : null,
        b.CodTipoAccion,
        b.TipoAccion.Nombre,
        b.Esquema,
        b.Tabla,
        b.IdRegistro,
        b.IdEmpleado,
        b.Empleado != null ? b.Empleado.Nombres + " " + b.Empleado.Apellidos : null,
        b.ValorAnterior,
        b.ValorNuevo,
        b.Descripcion,
        b.IpOrigen);
}

// =====================================================================
// 15. HISTORIAL DE UN REGISTRO (permiso: auditoria.consultar)
// Todos los cambios de un registro concreto por esquema, tabla e identificador,
// del más reciente al más antiguo. Usa el índice ix_bitacora_registro.
// =====================================================================

[RequirePermission(PermisosSistema.AuditoriaConsultar)]
public sealed record ObtenerHistorialRegistroQuery(
    string Esquema,
    string Tabla,
    string IdRegistro,
    int PageNumber = 1,
    int PageSize = 25) : IRequest<Result<PagedResult<BitacoraNegocioDto>>>;

public sealed class ObtenerHistorialRegistroQueryValidator : AbstractValidator<ObtenerHistorialRegistroQuery>
{
    public ObtenerHistorialRegistroQueryValidator()
    {
        RuleFor(x => x.Esquema).NotEmpty().MaximumLength(63);
        RuleFor(x => x.Tabla).NotEmpty().MaximumLength(63);
        RuleFor(x => x.IdRegistro).NotEmpty().MaximumLength(60);
    }
}

public sealed class ObtenerHistorialRegistroQueryHandler
    : IRequestHandler<ObtenerHistorialRegistroQuery, Result<PagedResult<BitacoraNegocioDto>>>
{
    private readonly GestionTurnosContext _context;

    public ObtenerHistorialRegistroQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<PagedResult<BitacoraNegocioDto>>> Handle(
        ObtenerHistorialRegistroQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Paginacion.NormalizarPagina(request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        var esquema = request.Esquema.Trim().ToLowerInvariant();
        var tabla = request.Tabla.Trim().ToLowerInvariant();
        var idRegistro = request.IdRegistro.Trim();

        var query = _context.BitacoraNegocio
            .AsNoTracking()
            .Where(b => b.Esquema == esquema && b.Tabla == tabla && b.IdRegistro == idRegistro);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(b => b.FechaHora)
            .ThenByDescending(b => b.IdBitacora)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(BitacoraProyeccion.ADto)
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<BitacoraNegocioDto>(items, totalCount, pageNumber, pageSize));
    }
}

