using System.Globalization;
using FluentValidation;
using GestionTurnos.Api.Application.Common;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Parametrizacion;
using GestionTurnos.Api.Domain.Entities.Parametrizacion;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.CQRS.Parametrizacion;

// =====================================================================
// CATÁLOGOS DE PARAMETRIZACIÓN (Sección 5: filtrar activo = true para formularios)
// =====================================================================

public sealed record ListarTiposDocumentoQuery(bool SoloActivos = true)
    : IRequest<Result<IReadOnlyList<CatalogoItemDto>>>;

public sealed class ListarTiposDocumentoQueryHandler
    : IRequestHandler<ListarTiposDocumentoQuery, Result<IReadOnlyList<CatalogoItemDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarTiposDocumentoQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<IReadOnlyList<CatalogoItemDto>>> Handle(
        ListarTiposDocumentoQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.TiposDocumento.AsNoTracking();
        if (request.SoloActivos)
        {
            query = query.Where(x => x.Activo);
        }

        var items = await query
            .OrderBy(x => x.Nombre)
            .Select(x => new CatalogoItemDto(x.CodTipoDocumento, x.Nombre, x.Activo))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CatalogoItemDto>>(items);
    }
}

public sealed record ListarTiposContratoQuery(bool SoloActivos = true)
    : IRequest<Result<IReadOnlyList<CatalogoItemDto>>>;

public sealed class ListarTiposContratoQueryHandler
    : IRequestHandler<ListarTiposContratoQuery, Result<IReadOnlyList<CatalogoItemDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarTiposContratoQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<IReadOnlyList<CatalogoItemDto>>> Handle(
        ListarTiposContratoQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.TiposContrato.AsNoTracking();
        if (request.SoloActivos)
        {
            query = query.Where(x => x.Activo);
        }

        var items = await query
            .OrderBy(x => x.Nombre)
            .Select(x => new CatalogoItemDto(x.CodTipoContrato, x.Nombre, x.Activo))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CatalogoItemDto>>(items);
    }
}

public sealed record ListarTiposNovedadQuery(bool SoloActivos = true, bool SoloPermitidosEmpleado = false)
    : IRequest<Result<IReadOnlyList<TipoNovedadDto>>>;

public sealed class ListarTiposNovedadQueryHandler
    : IRequestHandler<ListarTiposNovedadQuery, Result<IReadOnlyList<TipoNovedadDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarTiposNovedadQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<IReadOnlyList<TipoNovedadDto>>> Handle(
        ListarTiposNovedadQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.TiposNovedad.AsNoTracking();
        if (request.SoloActivos)
        {
            query = query.Where(x => x.Activo);
        }

        if (request.SoloPermitidosEmpleado)
        {
            var permitidos = TiposNovedadCodigos.PermitidosParaEmpleado.ToList();
            query = query.Where(x => permitidos.Contains(x.CodTipoNovedad));
        }

        var items = await query
            .OrderBy(x => x.Nombre)
            .Select(x => new TipoNovedadDto(
                x.CodTipoNovedad,
                x.Nombre,
                x.RemuneradoDefecto,
                x.AfectaHorasDefecto,
                x.RequiereAprobacion,
                x.RequiereSoporte,
                x.Activo))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<TipoNovedadDto>>(items);
    }
}

public sealed record ListarCategoriasHoraQuery(bool SoloActivos = true)
    : IRequest<Result<IReadOnlyList<CategoriaHoraDto>>>;

public sealed class ListarCategoriasHoraQueryHandler
    : IRequestHandler<ListarCategoriasHoraQuery, Result<IReadOnlyList<CategoriaHoraDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarCategoriasHoraQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<IReadOnlyList<CategoriaHoraDto>>> Handle(
        ListarCategoriasHoraQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.CategoriasHora.AsNoTracking();
        if (request.SoloActivos)
        {
            query = query.Where(x => x.Activo);
        }

        var items = await query
            .OrderBy(x => x.CodCategoriaHora)
            .Select(x => new CategoriaHoraDto(x.CodCategoriaHora, x.Nombre, x.FactorPago, x.Activo))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CategoriaHoraDto>>(items);
    }
}

[RequirePermission(PermisosSistema.ParametrosEditar)]
public sealed record GuardarTipoDocumentoCommand(
    string CodTipoDocumento,
    string Nombre,
    bool Activo = true) : IRequest<Result>;

public sealed class GuardarTipoDocumentoCommandValidator : AbstractValidator<GuardarTipoDocumentoCommand>
{
    public GuardarTipoDocumentoCommandValidator()
    {
        RuleFor(x => x.CodTipoDocumento).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(60);
    }
}

public sealed class GuardarTipoDocumentoCommandHandler : IRequestHandler<GuardarTipoDocumentoCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public GuardarTipoDocumentoCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(GuardarTipoDocumentoCommand request, CancellationToken cancellationToken)
    {
        var codigo = request.CodTipoDocumento.Trim().ToUpperInvariant();
        var existente = await _context.TiposDocumento.FirstOrDefaultAsync(t => t.CodTipoDocumento == codigo, cancellationToken);
        if (existente is null)
        {
            await _context.TiposDocumento.AddAsync(new TipoDocumento
            {
                CodTipoDocumento = codigo,
                Nombre = request.Nombre.Trim(),
                Activo = request.Activo
            }, cancellationToken);
        }
        else
        {
            existente.Nombre = request.Nombre.Trim();
            existente.Activo = request.Activo;
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

[RequirePermission(PermisosSistema.ParametrosEditar)]
public sealed record GuardarTipoContratoCommand(
    string CodTipoContrato,
    string Nombre,
    bool Activo = true) : IRequest<Result>;

public sealed class GuardarTipoContratoCommandValidator : AbstractValidator<GuardarTipoContratoCommand>
{
    public GuardarTipoContratoCommandValidator()
    {
        RuleFor(x => x.CodTipoContrato).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(60);
    }
}

public sealed class GuardarTipoContratoCommandHandler : IRequestHandler<GuardarTipoContratoCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public GuardarTipoContratoCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(GuardarTipoContratoCommand request, CancellationToken cancellationToken)
    {
        var codigo = request.CodTipoContrato.Trim().ToUpperInvariant();
        var existente = await _context.TiposContrato.FirstOrDefaultAsync(t => t.CodTipoContrato == codigo, cancellationToken);
        if (existente is null)
        {
            await _context.TiposContrato.AddAsync(new TipoContrato
            {
                CodTipoContrato = codigo,
                Nombre = request.Nombre.Trim(),
                Activo = request.Activo
            }, cancellationToken);
        }
        else
        {
            existente.Nombre = request.Nombre.Trim();
            existente.Activo = request.Activo;
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

[RequirePermission(PermisosSistema.ParametrosEditar)]
public sealed record GuardarTipoNovedadCommand(
    string CodTipoNovedad,
    string Nombre,
    bool RemuneradoDefecto,
    bool AfectaHorasDefecto,
    bool RequiereAprobacion = true,
    bool RequiereSoporte = false,
    bool Activo = true) : IRequest<Result>;

public sealed class GuardarTipoNovedadCommandValidator : AbstractValidator<GuardarTipoNovedadCommand>
{
    public GuardarTipoNovedadCommandValidator()
    {
        RuleFor(x => x.CodTipoNovedad).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(80);
    }
}

public sealed class GuardarTipoNovedadCommandHandler : IRequestHandler<GuardarTipoNovedadCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public GuardarTipoNovedadCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(GuardarTipoNovedadCommand request, CancellationToken cancellationToken)
    {
        var codigo = request.CodTipoNovedad.Trim().ToUpperInvariant();
        var existente = await _context.TiposNovedad.FirstOrDefaultAsync(t => t.CodTipoNovedad == codigo, cancellationToken);
        if (existente is null)
        {
            await _context.TiposNovedad.AddAsync(new TipoNovedad
            {
                CodTipoNovedad = codigo,
                Nombre = request.Nombre.Trim(),
                RemuneradoDefecto = request.RemuneradoDefecto,
                AfectaHorasDefecto = request.AfectaHorasDefecto,
                RequiereAprobacion = request.RequiereAprobacion,
                RequiereSoporte = request.RequiereSoporte,
                Activo = request.Activo
            }, cancellationToken);
        }
        else
        {
            existente.Nombre = request.Nombre.Trim();
            existente.RemuneradoDefecto = request.RemuneradoDefecto;
            existente.AfectaHorasDefecto = request.AfectaHorasDefecto;
            existente.RequiereAprobacion = request.RequiereAprobacion;
            existente.RequiereSoporte = request.RequiereSoporte;
            existente.Activo = request.Activo;
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

[RequirePermission(PermisosSistema.ParametrosEditar)]
public sealed record GuardarCategoriaHoraCommand(
    string CodCategoriaHora,
    string Nombre,
    decimal FactorPago = 1.00m,
    bool Activo = true) : IRequest<Result>;

public sealed class GuardarCategoriaHoraCommandValidator : AbstractValidator<GuardarCategoriaHoraCommand>
{
    public GuardarCategoriaHoraCommandValidator()
    {
        RuleFor(x => x.CodCategoriaHora).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(80);
        RuleFor(x => x.FactorPago).GreaterThanOrEqualTo(0);
    }
}

public sealed class GuardarCategoriaHoraCommandHandler : IRequestHandler<GuardarCategoriaHoraCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public GuardarCategoriaHoraCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(GuardarCategoriaHoraCommand request, CancellationToken cancellationToken)
    {
        var codigo = request.CodCategoriaHora.Trim().ToUpperInvariant();
        var existente = await _context.CategoriasHora.FirstOrDefaultAsync(c => c.CodCategoriaHora == codigo, cancellationToken);
        if (existente is null)
        {
            await _context.CategoriasHora.AddAsync(new CategoriaHora
            {
                CodCategoriaHora = codigo,
                Nombre = request.Nombre.Trim(),
                FactorPago = request.FactorPago,
                Activo = request.Activo
            }, cancellationToken);
        }
        else
        {
            existente.Nombre = request.Nombre.Trim();
            existente.FactorPago = request.FactorPago;
            existente.Activo = request.Activo;
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

// =====================================================================
// DÍAS FESTIVOS (parametrizacion.dia_festivo)
// =====================================================================

public sealed record ListarDiasFestivosQuery(int? Anio = null)
    : IRequest<Result<IReadOnlyList<DiaFestivoDto>>>;

public sealed class ListarDiasFestivosQueryHandler
    : IRequestHandler<ListarDiasFestivosQuery, Result<IReadOnlyList<DiaFestivoDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarDiasFestivosQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<IReadOnlyList<DiaFestivoDto>>> Handle(
        ListarDiasFestivosQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.DiasFestivos.AsNoTracking();

        if (request.Anio.HasValue)
        {
            var desde = new DateOnly(request.Anio.Value, 1, 1);
            var hasta = new DateOnly(request.Anio.Value, 12, 31);
            query = query.Where(d => d.Fecha >= desde && d.Fecha <= hasta);
        }

        var items = await query
            .OrderBy(d => d.Fecha)
            .Select(d => new DiaFestivoDto(d.Fecha, d.Nombre))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<DiaFestivoDto>>(items);
    }
}

[RequirePermission(PermisosSistema.ParametrosEditar)]
public sealed record GuardarDiaFestivoCommand(
    DateOnly Fecha,
    string Nombre) : IRequest<Result>;

public sealed class GuardarDiaFestivoCommandValidator : AbstractValidator<GuardarDiaFestivoCommand>
{
    public GuardarDiaFestivoCommandValidator()
    {
        RuleFor(x => x.Fecha).NotEmpty();
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
    }
}

public sealed class GuardarDiaFestivoCommandHandler : IRequestHandler<GuardarDiaFestivoCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public GuardarDiaFestivoCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(GuardarDiaFestivoCommand request, CancellationToken cancellationToken)
    {
        var existente = await _context.DiasFestivos
            .FirstOrDefaultAsync(d => d.Fecha == request.Fecha, cancellationToken);

        if (existente is null)
        {
            await _context.DiasFestivos.AddAsync(new DiaFestivo
            {
                Fecha = request.Fecha,
                Nombre = request.Nombre.Trim()
            }, cancellationToken);
        }
        else
        {
            existente.Nombre = request.Nombre.Trim();
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

/// <summary>
/// Borra un día festivo. Es la única entidad de negocio con borrado físico (Sección 13); el disparador
/// no audita esta tabla y el festivo no tiene historial que conservar.
/// </summary>
[RequirePermission(PermisosSistema.ParametrosEditar)]
public sealed record EliminarDiaFestivoCommand(DateOnly Fecha) : IRequest<Result>;

public sealed class EliminarDiaFestivoCommandHandler : IRequestHandler<EliminarDiaFestivoCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public EliminarDiaFestivoCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(EliminarDiaFestivoCommand request, CancellationToken cancellationToken)
    {
        var festivo = await _context.DiasFestivos
            .FirstOrDefaultAsync(d => d.Fecha == request.Fecha, cancellationToken);

        if (festivo is null)
        {
            return Result.Failure(new Error(
                "Parametrizacion.FestivoNoEncontrado",
                "No hay un día festivo registrado en esa fecha.",
                ErrorType.NotFound));
        }

        _context.DiasFestivos.Remove(festivo);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

// =====================================================================
// 6.2 LEER Y CAMBIAR PARÁMETROS (RG-10 - permiso: parametros.editar)
// =====================================================================

[RequirePermission(PermisosSistema.ParametrosEditar)]
public sealed record ListarParametrosQuery(DateOnly? FechaReferencia = null)
    : IRequest<Result<IReadOnlyList<ParametroDto>>>;

public sealed class ListarParametrosQueryHandler
    : IRequestHandler<ListarParametrosQuery, Result<IReadOnlyList<ParametroDto>>>
{
    private readonly GestionTurnosContext _context;
    private readonly IConfiguration _configuration;

    public ListarParametrosQueryHandler(GestionTurnosContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<Result<IReadOnlyList<ParametroDto>>> Handle(
        ListarParametrosQuery request,
        CancellationToken cancellationToken)
    {
        // "Hoy" en la zona horaria de la empresa: en UTC, después de las 19:00 de Bogotá ya sería mañana.
        var fecha = request.FechaReferencia ?? TimeZoneHelper.GetCurrentDateInEmpresaTimeZone(_configuration);

        var parametros = await _context.Parametros
            .AsNoTracking()
            .OrderBy(p => p.CodParametro)
            .Select(p => new
            {
                p.CodParametro,
                p.Nombre,
                p.Descripcion,
                p.TipoDato,
                p.Editable,
                Vigente = p.Valores
                    .Where(v => v.VigenteDesde <= fecha && (v.VigenteHasta == null || v.VigenteHasta >= fecha))
                    .OrderByDescending(v => v.VigenteDesde)
                    .Select(v => new { v.Valor, v.VigenteDesde })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var result = parametros
            .Select(p => new ParametroDto(
                p.CodParametro,
                p.Nombre,
                p.Descripcion,
                p.TipoDato,
                p.Editable,
                p.Vigente?.Valor,
                p.Vigente?.VigenteDesde))
            .ToList();

        return Result.Success<IReadOnlyList<ParametroDto>>(result);
    }
}

[RequirePermission(PermisosSistema.ParametrosEditar)]
public sealed record ListarHistorialParametroQuery(string CodParametro)
    : IRequest<Result<IReadOnlyList<ParametroValorDto>>>;

public sealed class ListarHistorialParametroQueryHandler
    : IRequestHandler<ListarHistorialParametroQuery, Result<IReadOnlyList<ParametroValorDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarHistorialParametroQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<IReadOnlyList<ParametroValorDto>>> Handle(
        ListarHistorialParametroQuery request,
        CancellationToken cancellationToken)
    {
        var codigo = request.CodParametro.Trim().ToUpperInvariant();

        var valores = await _context.ParametroValores
            .AsNoTracking()
            .Where(v => v.CodParametro == codigo)
            .OrderByDescending(v => v.VigenteDesde)
            .Select(v => new ParametroValorDto(
                v.IdParametroValor,
                v.CodParametro,
                v.Valor,
                v.VigenteDesde,
                v.VigenteHasta))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ParametroValorDto>>(valores);
    }
}

/// <summary>
/// Sección 6.2: Cambiar un valor de parámetro.
/// No edita la fila vigente: en una transacción cierra la vigente (vigente_hasta = nueva_fecha - 1) e inserta la nueva.
/// Solo si parametro.editable es verdadero.
/// </summary>
[RequirePermission(PermisosSistema.ParametrosEditar)]
public sealed record CambiarValorParametroCommand(
    string CodParametro,
    string Valor,
    DateOnly VigenteDesde) : IRequest<Result<long>>;

public sealed class CambiarValorParametroCommandValidator : AbstractValidator<CambiarValorParametroCommand>
{
    public CambiarValorParametroCommandValidator()
    {
        RuleFor(x => x.CodParametro).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Valor).NotEmpty().MaximumLength(200);
        RuleFor(x => x.VigenteDesde).NotEmpty();
    }
}

public sealed class CambiarValorParametroCommandHandler : IRequestHandler<CambiarValorParametroCommand, Result<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public CambiarValorParametroCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<long>> Handle(CambiarValorParametroCommand request, CancellationToken cancellationToken)
    {
        var codigo = request.CodParametro.Trim().ToUpperInvariant();
        var nuevoValor = request.Valor.Trim();

        var parametro = await _context.Parametros
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.CodParametro == codigo, cancellationToken);

        if (parametro is null)
        {
            return Result.Failure<long>(new Error(
                "Parametrizacion.ParametroNoEncontrado",
                "El parámetro especificado no existe.",
                ErrorType.NotFound));
        }

        if (!parametro.Editable)
        {
            return Result.Failure<long>(new Error(
                "Parametrizacion.ParametroNoEditable",
                $"El parámetro '{codigo}' no es editable.",
                ErrorType.Conflict));
        }

        if (!ConversorParametros.EsValido(parametro.TipoDato, nuevoValor))
        {
            return Result.Failure<long>(new Error(
                "Parametrizacion.TipoDatoInvalido",
                $"El valor '{nuevoValor}' no cumple con el tipo de dato '{parametro.TipoDato}' del parámetro.",
                ErrorType.Failure));
        }

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Bloquea el parámetro: dos cambios simultáneos no pueden cerrar la misma vigencia
            // ni dejar vigencias cruzadas (en PostgreSQL lo garantiza además la restricción de exclusión, 23P01).
            await _context.BloquearPorIdAsync<Parametro>(codigo, cancellationToken);

            // Buscar la fila vigente abierta o que cubra la fecha de inicio de la nueva vigencia
            var vigenteAbierta = await _context.ParametroValores
                .Where(v => v.CodParametro == codigo &&
                            (v.VigenteHasta == null || v.VigenteHasta >= request.VigenteDesde))
                .OrderByDescending(v => v.VigenteDesde)
                .FirstOrDefaultAsync(cancellationToken);

            if (vigenteAbierta is not null)
            {
                if (request.VigenteDesde <= vigenteAbierta.VigenteDesde)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<long>(new Error(
                        "Parametrizacion.VigenciaCruzada",
                        "Ya hay un valor vigente para ese parámetro en esas fechas.",
                        ErrorType.Conflict));
                }

                // Cerrar la vigente (vigente_hasta = nueva_fecha - 1)
                vigenteAbierta.VigenteHasta = request.VigenteDesde.AddDays(-1);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var nuevaFila = new ParametroValor
            {
                CodParametro = codigo,
                Valor = nuevoValor,
                VigenteDesde = request.VigenteDesde,
                VigenteHasta = null
            };

            await _context.ParametroValores.AddAsync(nuevaFila, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result.Success(nuevaFila.IdParametroValor);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<long>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

