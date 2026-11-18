using FluentValidation;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Novedades;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Application.Services;
using GestionTurnos.Api.Domain.Entities.Novedades;
using GestionTurnos.Api.Domain.Entities.Organizacion;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.CQRS.Novedades;

// =====================================================================
// 3.8 SOLICITAR PERMISO O REPORTAR NOVEDAD (EMPLEADO - RF-07)
// =====================================================================

/// <summary>
/// Sección 3.8:
/// 1. El empleado solo puede usar INCAPACIDAD, CALAMIDAD y PERMISO_PERSONAL; las ausencias las registra el administrador.
/// 2. Si requiere_soporte es verdadero, exige url_soporte.
/// 3. Copia remunerado_defecto y afecta_horas_defecto a remunerado y afecta_horas.
/// 4. Las novedades creadas por el empleado siempre nacen PENDIENTE.
/// </summary>
public sealed record SolicitarMiNovedadCommand(
    string CodTipoNovedad,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    string? Observaciones,
    string? UrlSoporte) : IRequest<Result<long>>, IRequireAuthentication;

public sealed class SolicitarMiNovedadCommandValidator : AbstractValidator<SolicitarMiNovedadCommand>
{
    public SolicitarMiNovedadCommandValidator()
    {
        RuleFor(x => x.CodTipoNovedad)
            .NotEmpty()
            .MaximumLength(30)
            .Must(c => TiposNovedadCodigos.PermitidosParaEmpleado.Contains(c.Trim()))
            .WithMessage("El empleado solo puede solicitar INCAPACIDAD, CALAMIDAD o PERMISO_PERSONAL.");

        RuleFor(x => x.Inicio).NotEmpty();
        RuleFor(x => x.Fin)
            .NotEmpty()
            .GreaterThan(x => x.Inicio)
            .WithMessage("La fecha/hora de fin debe ser posterior a la de inicio.");

        RuleFor(x => x.UrlSoporte)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.UrlSoporte));
    }
}

public sealed class SolicitarMiNovedadCommandHandler : IRequestHandler<SolicitarMiNovedadCommand, Result<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public SolicitarMiNovedadCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result<long>> Handle(SolicitarMiNovedadCommand request, CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<long>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;
        var idUsuario = _currentUserService.UserId!.Value;
        var codTipo = request.CodTipoNovedad.Trim().ToUpperInvariant();

        // 1. Lee el tipo en parametrizacion.tipo_novedad (debe estar activo)
        var tipoNovedad = await _context.TiposNovedad
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.CodTipoNovedad == codTipo && t.Activo, cancellationToken);

        if (tipoNovedad is null)
        {
            return Result.Failure<long>(new Error(
                "Novedades.TipoNovedadInvalido",
                "El tipo de novedad seleccionado no existe o fue desactivado.",
                ErrorType.NotFound));
        }

        // 2. Si requiere_soporte es verdadero, exige url_soporte
        if (tipoNovedad.RequiereSoporte && string.IsNullOrWhiteSpace(request.UrlSoporte))
        {
            return Result.Failure<long>(new Error(
                "Novedades.SoporteRequerido",
                $"El tipo de novedad '{tipoNovedad.Nombre}' exige adjuntar la URL del soporte documental.",
                ErrorType.Failure));
        }

        // 3 y 4. Copia remunerado_defecto y afecta_horas_defecto; nace siempre PENDIENTE
        var novedad = new Novedad
        {
            IdEmpleado = idEmpleado,
            CodTipoNovedad = codTipo,
            Inicio = request.Inicio,
            Fin = request.Fin,
            Remunerado = tipoNovedad.RemuneradoDefecto,
            AfectaHoras = tipoNovedad.AfectaHorasDefecto,
            CodEstadoSolicitud = EstadosSolicitud.Pendiente,
            Observaciones = string.IsNullOrWhiteSpace(request.Observaciones) ? null : request.Observaciones.Trim(),
            UrlSoporte = string.IsNullOrWhiteSpace(request.UrlSoporte) ? null : request.UrlSoporte.Trim(),
            RegistradoPor = idUsuario,
            RegistradoEn = DateTimeOffset.UtcNow
        };

        try
        {
            await _context.Novedades.AddAsync(novedad, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(novedad.IdNovedad);
        }
        catch (Exception ex)
        {
            return Result.Failure<long>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

/// <summary>
/// Sección 3.8 punto 5: Cancelación de novedad por el empleado (solo PENDIENTE y filtrando por id_empleado).
/// </summary>
public sealed record CancelarMiNovedadCommand(long IdNovedad) : IRequest<Result>, IRequireAuthentication;

public sealed class CancelarMiNovedadCommandHandler : IRequestHandler<CancelarMiNovedadCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public CancelarMiNovedadCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result> Handle(CancelarMiNovedadCommand request, CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        var novedad = await _context.Novedades
            .FirstOrDefaultAsync(
                n => n.IdNovedad == request.IdNovedad &&
                     n.IdEmpleado == idEmpleado &&
                     n.CodEstadoSolicitud == EstadosSolicitud.Pendiente,
                cancellationToken);

        if (novedad is null)
        {
            return Result.Failure(new Error(
                "Novedades.NovedadNoEncontrada",
                "La novedad no fue encontrada o ya no se encuentra pendiente.",
                ErrorType.NotFound));
        }

        novedad.CodEstadoSolicitud = EstadosSolicitud.Cancelada;

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

public sealed record ObtenerMisNovedadesQuery : IRequest<Result<IReadOnlyList<NovedadDto>>>, IRequireAuthentication;

public sealed class ObtenerMisNovedadesQueryHandler
    : IRequestHandler<ObtenerMisNovedadesQuery, Result<IReadOnlyList<NovedadDto>>>
{
    private readonly GestionTurnosContext _context;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public ObtenerMisNovedadesQueryHandler(
        GestionTurnosContext context,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result<IReadOnlyList<NovedadDto>>> Handle(
        ObtenerMisNovedadesQuery request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<NovedadDto>>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        var items = await _context.Novedades
            .AsNoTracking()
            .Where(n => n.IdEmpleado == idEmpleado)
            .OrderByDescending(n => n.RegistradoEn)
            .Select(n => new NovedadDto(
                n.IdNovedad,
                n.IdEmpleado,
                n.Empleado.Nombres + " " + n.Empleado.Apellidos,
                n.CodTipoNovedad,
                n.TipoNovedad.Nombre,
                n.Inicio,
                n.Fin,
                n.Remunerado,
                n.AfectaHoras,
                n.CodEstadoSolicitud,
                n.EstadoSolicitud.Nombre,
                n.Observaciones,
                n.UrlSoporte,
                n.RegistradoPor,
                n.RegistradoEn,
                n.ResueltoPor,
                n.ResueltoEn,
                n.MotivoRechazo))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<NovedadDto>>(items);
    }
}

// =====================================================================
// 3.8 y 4. REGISTRO Y RESOLUCIÓN DE NOVEDADES POR ADMINISTRADOR (permiso: novedades.aprobar)
// =====================================================================

[RequirePermission(PermisosSistema.NovedadesAprobar)]
public sealed record RegistrarNovedadAdminCommand(
    long IdEmpleado,
    string CodTipoNovedad,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    bool? Remunerado,
    bool? AfectaHoras,
    string? Observaciones,
    string? UrlSoporte,
    bool ConfirmarAnulacionTurnos = false) : IRequest<Result<long>>;

public sealed class RegistrarNovedadAdminCommandValidator : AbstractValidator<RegistrarNovedadAdminCommand>
{
    public RegistrarNovedadAdminCommandValidator()
    {
        RuleFor(x => x.IdEmpleado).GreaterThan(0);
        RuleFor(x => x.CodTipoNovedad).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Inicio).NotEmpty();
        RuleFor(x => x.Fin).GreaterThan(x => x.Inicio);
        RuleFor(x => x.UrlSoporte).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.UrlSoporte));
    }
}

/// <summary>
/// Sección 12: registro directo de incapacidades y ausencias por el administrador. Nacen APROBADAS.
/// Turnos que se cruzan: los futuros siguen la regla de CONFLICTO_BLOQUEA; los pasados o en curso no se anulan
/// (quedan como turno sin asistencia explicado por la novedad). Si afecta horas, reliquida en la misma transacción.
/// </summary>
public sealed class RegistrarNovedadAdminCommandHandler : IRequestHandler<RegistrarNovedadAdminCommand, Result<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IParametroService _parametroService;
    private readonly ILiquidacionHorasCalculator _liquidacionCalculator;

    public RegistrarNovedadAdminCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IParametroService parametroService,
        ILiquidacionHorasCalculator liquidacionCalculator)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _parametroService = parametroService;
        _liquidacionCalculator = liquidacionCalculator;
    }

    public async Task<Result<long>> Handle(RegistrarNovedadAdminCommand request, CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.UserId!.Value;
        var codTipo = request.CodTipoNovedad.Trim().ToUpperInvariant();

        var tipoNovedad = await _context.TiposNovedad
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.CodTipoNovedad == codTipo && t.Activo, cancellationToken);

        if (tipoNovedad is null)
        {
            return Result.Failure<long>(new Error(
                "Novedades.TipoNovedadInvalido",
                "El tipo de novedad seleccionado no existe o fue desactivado.",
                ErrorType.NotFound));
        }

        if (tipoNovedad.RequiereSoporte && string.IsNullOrWhiteSpace(request.UrlSoporte))
        {
            return Result.Failure<long>(new Error(
                "Novedades.SoporteRequerido",
                $"El tipo de novedad '{tipoNovedad.Nombre}' exige adjuntar la URL del soporte.",
                ErrorType.Failure));
        }

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Mismo orden de bloqueo que la programación de turnos: primero el empleado.
            var empleado = await _context.BloquearPorIdAsync<Empleado>(request.IdEmpleado, cancellationToken);
            if (empleado is null || empleado.CodEstadoEmpleado != EstadosEmpleado.Activo)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<long>(new Error(
                    "Novedades.EmpleadoNoActivo",
                    "Solo se pueden registrar novedades para empleados activos.",
                    ErrorType.Failure));
            }

            var regla = await NovedadesComunes.AplicarReglaTurnosAsync(
                _context, _parametroService, request.IdEmpleado, request.Inicio, request.Fin,
                request.ConfirmarAnulacionTurnos, "Anulada por novedad registrada", cancellationToken);
            if (regla.IsFailure)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<long>(regla.Error);
            }

            var ahora = DateTimeOffset.UtcNow;
            var novedad = new Novedad
            {
                IdEmpleado = request.IdEmpleado,
                CodTipoNovedad = codTipo,
                Inicio = request.Inicio,
                Fin = request.Fin,
                Remunerado = request.Remunerado ?? tipoNovedad.RemuneradoDefecto,
                AfectaHoras = request.AfectaHoras ?? tipoNovedad.AfectaHorasDefecto,
                CodEstadoSolicitud = EstadosSolicitud.Aprobada,
                Observaciones = string.IsNullOrWhiteSpace(request.Observaciones) ? null : request.Observaciones.Trim(),
                UrlSoporte = string.IsNullOrWhiteSpace(request.UrlSoporte) ? null : request.UrlSoporte.Trim(),
                RegistradoPor = adminId,
                RegistradoEn = ahora,
                ResueltoPor = adminId,
                ResueltoEn = ahora
            };

            await _context.Novedades.AddAsync(novedad, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (novedad.AfectaHoras)
            {
                await NovedadesComunes.ReliquidarRegistrosAsync(
                    _context, _liquidacionCalculator, novedad.IdEmpleado, novedad.Inicio, novedad.Fin, cancellationToken);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success(novedad.IdNovedad);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<long>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

/// <summary>
/// Sección 12: resolución administrativa de novedad (APROBADA o RECHAZADA). Al aprobar se puede ajustar
/// si es remunerada y si afecta horas; se aplica la regla de turnos y, si afecta horas, se reliquida.
/// </summary>
[RequirePermission(PermisosSistema.NovedadesAprobar)]
public sealed record ResolverNovedadCommand(
    long IdNovedad,
    bool Aprobar,
    string? MotivoRechazo = null,
    bool ConfirmarAnulacionTurnos = false,
    bool? Remunerado = null,
    bool? AfectaHoras = null) : IRequest<Result>;

public sealed class ResolverNovedadCommandValidator : AbstractValidator<ResolverNovedadCommand>
{
    public ResolverNovedadCommandValidator()
    {
        RuleFor(x => x.IdNovedad).GreaterThan(0);
        RuleFor(x => x.MotivoRechazo)
            .NotEmpty().WithMessage("El motivo de rechazo es obligatorio al rechazar una novedad.")
            .MaximumLength(300)
            .When(x => !x.Aprobar);
    }
}

public sealed class ResolverNovedadCommandHandler : IRequestHandler<ResolverNovedadCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IParametroService _parametroService;
    private readonly ILiquidacionHorasCalculator _liquidacionCalculator;

    public ResolverNovedadCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IParametroService parametroService,
        ILiquidacionHorasCalculator liquidacionCalculator)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _parametroService = parametroService;
        _liquidacionCalculator = liquidacionCalculator;
    }

    public async Task<Result> Handle(ResolverNovedadCommand request, CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.UserId!.Value;

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Bloqueos: empleado y después la novedad.
            var idEmpleado = await _context.Novedades
                .AsNoTracking()
                .Where(n => n.IdNovedad == request.IdNovedad)
                .Select(n => (long?)n.IdEmpleado)
                .FirstOrDefaultAsync(cancellationToken);

            Novedad? novedad = null;
            if (idEmpleado.HasValue)
            {
                await _context.BloquearPorIdAsync<Empleado>(idEmpleado.Value, cancellationToken);
                novedad = await _context.BloquearPorIdAsync<Novedad>(request.IdNovedad, cancellationToken);
            }

            if (novedad is null || novedad.CodEstadoSolicitud != EstadosSolicitud.Pendiente)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(new Error(
                    "Novedades.NovedadNoPendiente",
                    "La novedad no existe o ya fue resuelta.",
                    ErrorType.NotFound));
            }

            if (request.Aprobar)
            {
                var regla = await NovedadesComunes.AplicarReglaTurnosAsync(
                    _context, _parametroService, novedad.IdEmpleado, novedad.Inicio, novedad.Fin,
                    request.ConfirmarAnulacionTurnos, $"Anulada por novedad aprobada (#{novedad.IdNovedad})", cancellationToken);
                if (regla.IsFailure)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return regla;
                }

                novedad.Remunerado = request.Remunerado ?? novedad.Remunerado;
                novedad.AfectaHoras = request.AfectaHoras ?? novedad.AfectaHoras;
                novedad.CodEstadoSolicitud = EstadosSolicitud.Aprobada;
                novedad.ResueltoPor = adminId;
                novedad.ResueltoEn = DateTimeOffset.UtcNow;
                novedad.MotivoRechazo = null;

                if (novedad.AfectaHoras)
                {
                    // Guardar primero: la liquidación lee las novedades APROBADAS desde la base.
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await NovedadesComunes.ReliquidarRegistrosAsync(
                        _context, _liquidacionCalculator, novedad.IdEmpleado, novedad.Inicio, novedad.Fin, cancellationToken);
                }
            }
            else
            {
                novedad.CodEstadoSolicitud = EstadosSolicitud.Rechazada;
                novedad.ResueltoPor = adminId;
                novedad.ResueltoEn = DateTimeOffset.UtcNow;
                novedad.MotivoRechazo = request.MotivoRechazo!.Trim();
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

/// <summary>
/// Reglas comunes de novedades del administrador (sección 12).
/// </summary>
internal static class NovedadesComunes
{
    /// <summary>
    /// Turnos que se cruzan con la novedad: los futuros (aún no inician) siguen la regla de CONFLICTO_BLOQUEA;
    /// los pasados o en curso no se tocan y quedan como turno sin asistencia explicado por la novedad.
    /// </summary>
    public static async Task<Result> AplicarReglaTurnosAsync(
        GestionTurnosContext context,
        IParametroService parametroService,
        long idEmpleado,
        DateTimeOffset inicio,
        DateTimeOffset fin,
        bool confirmarAnulacion,
        string observacionAnulacion,
        CancellationToken cancellationToken)
    {
        var ahora = DateTimeOffset.UtcNow;
        var turnos = await ConflictosTurnosNovedad.TurnosQueSeCruzanAsync(context, idEmpleado, inicio, fin, cancellationToken);
        var futuros = turnos.Where(t => t.Periodo.Inicio > ahora).ToList();

        return await ConflictosTurnosNovedad.AplicarReglaAsync(
            context,
            parametroService,
            futuros,
            DateOnly.FromDateTime(inicio.UtcDateTime),
            confirmarAnulacion,
            "Novedades",
            observacionAnulacion,
            cancellationToken);
    }

    /// <summary>
    /// Reliquida (sección 6.3) los registros cerrados del empleado que se cruzan con el intervalo de la novedad.
    /// La novedad ya debe estar guardada en la transacción en curso.
    /// </summary>
    public static async Task ReliquidarRegistrosAsync(
        GestionTurnosContext context,
        ILiquidacionHorasCalculator calculator,
        long idEmpleado,
        DateTimeOffset inicio,
        DateTimeOffset fin,
        CancellationToken cancellationToken)
    {
        var idsRegistros = await context.RegistrosAsistencia
            .AsNoTracking()
            .Where(r => r.IdEmpleado == idEmpleado &&
                        r.HoraSalida != null &&
                        r.HoraEntrada < fin &&
                        r.HoraSalida > inicio)
            .Select(r => r.IdRegistro)
            .ToListAsync(cancellationToken);

        foreach (var idRegistro in idsRegistros)
        {
            await calculator.LiquidarRegistroAsync(idRegistro, cancellationToken);
        }
    }
}

[RequirePermission(PermisosSistema.VacacionesAprobar)]
public sealed record ListarSolicitudesVacacionesAdminQuery(
    long? IdEmpleado = null,
    string? CodEstadoSolicitud = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<SolicitudVacacionesBandejaDto>>>;

/// <summary>
/// Valor de <c>CodEstadoSolicitud</c> para que las bandejas devuelvan todos los estados.
/// Sin estado, las bandejas muestran solo las PENDIENTE (secciones 11 y 12).
/// </summary>
public static class FiltroBandeja
{
    public const string Todos = "TODOS";

    public static string? Resolver(string? codEstado)
    {
        if (string.IsNullOrWhiteSpace(codEstado))
        {
            return EstadosSolicitud.Pendiente;
        }

        var estado = codEstado.Trim().ToUpperInvariant();
        return estado == Todos ? null : estado;
    }
}

/// <summary>
/// Bandeja de solicitudes de vacaciones (sección 11) con el saldo disponible de cada empleado,
/// leído siempre de novedades.v_saldo_vacaciones.
/// </summary>
public sealed class ListarSolicitudesVacacionesAdminQueryHandler
    : IRequestHandler<ListarSolicitudesVacacionesAdminQuery, Result<PagedResult<SolicitudVacacionesBandejaDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarSolicitudesVacacionesAdminQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<PagedResult<SolicitudVacacionesBandejaDto>>> Handle(
        ListarSolicitudesVacacionesAdminQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Paginacion.NormalizarPagina(request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        var query = _context.SolicitudesVacaciones.AsNoTracking();
        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(s => s.IdEmpleado == request.IdEmpleado.Value);
        }

        var estado = FiltroBandeja.Resolver(request.CodEstadoSolicitud);
        if (estado is not null)
        {
            query = query.Where(s => s.CodEstadoSolicitud == estado);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(s => s.SolicitadoEn)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SolicitudVacacionesDto(
                s.IdSolicitudVacaciones,
                s.IdEmpleado,
                s.Empleado.Nombres + " " + s.Empleado.Apellidos,
                s.FechaInicio,
                s.FechaFin,
                s.DiasSolicitados,
                s.CodEstadoSolicitud,
                s.EstadoSolicitud.Nombre,
                s.Observacion,
                s.SolicitadoPor,
                s.SolicitadoEn,
                s.ResueltoPor,
                s.ResueltoEn,
                s.MotivoRechazo))
            .ToListAsync(cancellationToken);

        var idsEmpleado = items.Select(i => i.IdEmpleado).Distinct().ToList();
        var saldos = await _context.VistaSaldosVacaciones
            .AsNoTracking()
            .Where(v => idsEmpleado.Contains(v.IdEmpleado))
            .ToDictionaryAsync(v => v.IdEmpleado, cancellationToken);

        var bandeja = items
            .Select(i =>
            {
                saldos.TryGetValue(i.IdEmpleado, out var saldo);
                decimal disponibles = saldo?.DiasDisponibles ?? 0m;
                decimal enSolicitud = saldo?.DiasEnSolicitud ?? 0m;
                return new SolicitudVacacionesBandejaDto(i, disponibles, enSolicitud, disponibles - enSolicitud);
            })
            .ToList();

        return Result.Success(new PagedResult<SolicitudVacacionesBandejaDto>(bandeja, totalCount, pageNumber, pageSize));
    }
}

[RequirePermission(PermisosSistema.NovedadesAprobar)]
public sealed record ListarNovedadesAdminQuery(
    long? IdEmpleado = null,
    string? CodEstadoSolicitud = null,
    string? CodTipoNovedad = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<NovedadDto>>>;

public sealed class ListarNovedadesAdminQueryHandler
    : IRequestHandler<ListarNovedadesAdminQuery, Result<PagedResult<NovedadDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarNovedadesAdminQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<PagedResult<NovedadDto>>> Handle(
        ListarNovedadesAdminQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Paginacion.NormalizarPagina(request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        var query = _context.Novedades.AsNoTracking();
        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(n => n.IdEmpleado == request.IdEmpleado.Value);
        }

        var estado = FiltroBandeja.Resolver(request.CodEstadoSolicitud);
        if (estado is not null)
        {
            query = query.Where(n => n.CodEstadoSolicitud == estado);
        }

        if (!string.IsNullOrWhiteSpace(request.CodTipoNovedad))
        {
            var tipo = request.CodTipoNovedad.Trim().ToUpperInvariant();
            query = query.Where(n => n.CodTipoNovedad == tipo);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(n => n.RegistradoEn)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NovedadDto(
                n.IdNovedad,
                n.IdEmpleado,
                n.Empleado.Nombres + " " + n.Empleado.Apellidos,
                n.CodTipoNovedad,
                n.TipoNovedad.Nombre,
                n.Inicio,
                n.Fin,
                n.Remunerado,
                n.AfectaHoras,
                n.CodEstadoSolicitud,
                n.EstadoSolicitud.Nombre,
                n.Observaciones,
                n.UrlSoporte,
                n.RegistradoPor,
                n.RegistradoEn,
                n.ResueltoPor,
                n.ResueltoEn,
                n.MotivoRechazo))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<NovedadDto>(items, totalCount, pageNumber, pageSize));
    }
}
