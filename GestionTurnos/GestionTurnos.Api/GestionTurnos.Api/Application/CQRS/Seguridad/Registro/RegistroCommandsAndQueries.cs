using FluentValidation;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Seguridad;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Entities.Organizacion;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Errors.Seguridad;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.CQRS.Seguridad.Registro;

// =====================================================================
// 3.1 SOLICITAR REGISTRO (PÚBLICO)
// =====================================================================

public sealed record SolicitarRegistroCommand(
    string CodTipoDocumento,
    string NumeroDocumento,
    string Nombres,
    string Apellidos,
    string Correo,
    string? Telefono,
    string Contrasena) : IRequest<Result<long>>;

public sealed class SolicitarRegistroCommandValidator : AbstractValidator<SolicitarRegistroCommand>
{
    public SolicitarRegistroCommandValidator()
    {
        RuleFor(x => x.CodTipoDocumento)
            .NotEmpty().WithMessage("El tipo de documento es obligatorio.")
            .MaximumLength(10);

        RuleFor(x => x.NumeroDocumento)
            .NotEmpty().WithMessage("El número de documento es obligatorio.")
            .MaximumLength(30);

        RuleFor(x => x.Nombres)
            .NotEmpty().WithMessage("Los nombres son obligatorios.")
            .MaximumLength(80);

        RuleFor(x => x.Apellidos)
            .NotEmpty().WithMessage("Los apellidos son obligatorios.")
            .MaximumLength(80);

        RuleFor(x => x.Correo)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("El formato del correo electrónico no es válido.")
            .MaximumLength(150);

        RuleFor(x => x.Telefono)
            .MaximumLength(30)
            .When(x => !string.IsNullOrWhiteSpace(x.Telefono));

        RuleFor(x => x.Contrasena)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(72);
    }
}

public sealed class SolicitarRegistroCommandHandler : IRequestHandler<SolicitarRegistroCommand, Result<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public SolicitarRegistroCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<long>> Handle(SolicitarRegistroCommand request, CancellationToken cancellationToken)
    {
        var correoNormalizado = request.Correo.Trim().ToLowerInvariant();
        var codTipoDoc = request.CodTipoDocumento.Trim().ToUpperInvariant();
        var numDoc = request.NumeroDocumento.Trim();

        // Validar tipo de documento activo
        bool tipoDocActivo = await _context.TiposDocumento
            .AsNoTracking()
            .AnyAsync(t => t.CodTipoDocumento == codTipoDoc && t.Activo, cancellationToken);

        if (!tipoDocActivo)
        {
            return Result.Failure<long>(new Error(
                "Parametrizacion.TipoDocumentoInvalido",
                "El tipo de documento seleccionado no existe o fue desactivado.",
                ErrorType.Failure));
        }

        // 1. Valida que el correo no exista en seguridad.usuario (lower(correo)),
        //    y que el documento no exista en organizacion.empleado.
        bool correoExiste = await _context.Usuarios
            .AsNoTracking()
            .AnyAsync(u => u.Correo.ToLower() == correoNormalizado, cancellationToken);

        if (correoExiste)
        {
            return Result.Failure<long>(AuthErrors.CorreoYaRegistrado);
        }

        bool documentoExiste = await _context.Empleados
            .AsNoTracking()
            .AnyAsync(e => e.CodTipoDocumento == codTipoDoc && e.NumeroDocumento == numDoc, cancellationToken);

        if (documentoExiste)
        {
            return Result.Failure<long>(AuthErrors.DocumentoYaRegistrado);
        }

        bool solicitudPendienteExiste = await _context.SolicitudesRegistro
            .AsNoTracking()
            .AnyAsync(s => s.Correo.ToLower() == correoNormalizado &&
                           s.CodEstadoSolicitud == EstadosSolicitud.Pendiente, cancellationToken);

        if (solicitudPendienteExiste)
        {
            return Result.Failure<long>(AuthErrors.SolicitudPendienteExistente);
        }

        // 2. Genera el hash bcrypt (RNF-02) y guarda en seguridad.solicitud_registro
        var hashContrasena = _passwordHasher.Hash(request.Contrasena);

        var solicitud = new SolicitudRegistro
        {
            CodTipoDocumento = codTipoDoc,
            NumeroDocumento = numDoc,
            Nombres = request.Nombres.Trim(),
            Apellidos = request.Apellidos.Trim(),
            Correo = correoNormalizado,
            Telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim(),
            HashContrasena = hashContrasena,
            CodEstadoSolicitud = EstadosSolicitud.Pendiente,
            SolicitadoEn = DateTimeOffset.UtcNow
        };

        try
        {
            await _context.SolicitudesRegistro.AddAsync(solicitud, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(solicitud.IdSolicitudRegistro);
        }
        catch (Exception ex)
        {
            return Result.Failure<long>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

// =====================================================================
// 3.2 APROBACIÓN DEL REGISTRO (ADMINISTRADOR - permiso: registros.aprobar)
// =====================================================================

[RequirePermission(PermisosSistema.RegistrosAprobar)]
public sealed record AprobarSolicitudRegistroCommand(
    long IdSolicitudRegistro,
    int IdArea,
    int IdCargo,
    DateOnly FechaIngreso,
    string? CodTipoContrato) : IRequest<Result<AprobacionRegistroResultDto>>;

public sealed class AprobarSolicitudRegistroCommandValidator : AbstractValidator<AprobarSolicitudRegistroCommand>
{
    public AprobarSolicitudRegistroCommandValidator()
    {
        RuleFor(x => x.IdSolicitudRegistro).GreaterThan(0);
        RuleFor(x => x.IdArea).GreaterThan(0).WithMessage("El área es obligatoria.");
        RuleFor(x => x.IdCargo).GreaterThan(0).WithMessage("El cargo es obligatorio.");
        RuleFor(x => x.FechaIngreso).NotEmpty().WithMessage("La fecha de ingreso es obligatoria.");
        RuleFor(x => x.CodTipoContrato)
            .MaximumLength(20)
            .When(x => !string.IsNullOrWhiteSpace(x.CodTipoContrato));
    }
}

public sealed class AprobarSolicitudRegistroCommandHandler
    : IRequestHandler<AprobarSolicitudRegistroCommand, Result<AprobacionRegistroResultDto>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public AprobarSolicitudRegistroCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<AprobacionRegistroResultDto>> Handle(
        AprobarSolicitudRegistroCommand request,
        CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.UserId!.Value;

        // Validar catálogos activos (área, cargo y contrato si aplica)
        bool areaActiva = await _context.Areas
            .AsNoTracking()
            .AnyAsync(a => a.IdArea == request.IdArea && a.Activo, cancellationToken);
        if (!areaActiva)
        {
            return Result.Failure<AprobacionRegistroResultDto>(AuthErrors.AreaNoEncontrada);
        }

        bool cargoActivo = await _context.Cargos
            .AsNoTracking()
            .AnyAsync(c => c.IdCargo == request.IdCargo && c.Activo, cancellationToken);
        if (!cargoActivo)
        {
            return Result.Failure<AprobacionRegistroResultDto>(AuthErrors.CargoNoEncontrado);
        }

        string? codTipoContrato = string.IsNullOrWhiteSpace(request.CodTipoContrato)
            ? null
            : request.CodTipoContrato.Trim().ToUpperInvariant();

        if (codTipoContrato is not null)
        {
            bool contratoActivo = await _context.TiposContrato
                .AsNoTracking()
                .AnyAsync(tc => tc.CodTipoContrato == codTipoContrato && tc.Activo, cancellationToken);
            if (!contratoActivo)
            {
                return Result.Failure<AprobacionRegistroResultDto>(new Error(
                    "Parametrizacion.TipoContratoInvalido",
                    "El tipo de contrato seleccionado no existe o fue desactivado.",
                    ErrorType.Failure));
            }
        }

        var rolEmpleado = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Nombre == "Empleado" && r.Activo, cancellationToken);

        if (rolEmpleado is null)
        {
            return Result.Failure<AprobacionRegistroResultDto>(AuthErrors.RolEmpleadoNoConfigurado);
        }

        try
        {
            // En una sola transacción (Sección 3.2)
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // 1. Bloquear la solicitud (FOR UPDATE) y exigir que siga PENDIENTE
            var solicitud = await _context.BloquearPorIdAsync<SolicitudRegistro>(
                request.IdSolicitudRegistro, cancellationToken);

            if (solicitud is null || solicitud.CodEstadoSolicitud != EstadosSolicitud.Pendiente)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<AprobacionRegistroResultDto>(AuthErrors.SolicitudNoEncontradaOPendiente);
            }

            // Mensajes claros si el correo o el documento se registraron después de la solicitud
            // (la unicidad final la garantiza la base: 23505 se traduce en el catch).
            var correoSolicitud = solicitud.Correo.Trim().ToLowerInvariant();
            bool correoExiste = await _context.Usuarios
                .AsNoTracking()
                .AnyAsync(u => u.Correo.ToLower() == correoSolicitud, cancellationToken);
            if (correoExiste)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<AprobacionRegistroResultDto>(AuthErrors.CorreoYaRegistrado);
            }

            bool documentoExiste = await _context.Empleados
                .AsNoTracking()
                .AnyAsync(e => e.CodTipoDocumento == solicitud.CodTipoDocumento &&
                               e.NumeroDocumento == solicitud.NumeroDocumento, cancellationToken);
            if (documentoExiste)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<AprobacionRegistroResultDto>(AuthErrors.DocumentoYaRegistrado);
            }

            // 2. Crear el usuario con el hash que ya trae la solicitud
            var nuevoUsuario = new Usuario
            {
                Correo = correoSolicitud,
                HashContrasena = solicitud.HashContrasena,
                IdRol = rolEmpleado.IdRol,
                CodEstadoUsuario = EstadosUsuario.Activo,
                CreadoEn = DateTimeOffset.UtcNow
            };

            await _context.Usuarios.AddAsync(nuevoUsuario, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 3. Crear el empleado
            var nuevoEmpleado = new Empleado
            {
                IdUsuario = nuevoUsuario.IdUsuario,
                CodTipoDocumento = solicitud.CodTipoDocumento,
                NumeroDocumento = solicitud.NumeroDocumento,
                Nombres = solicitud.Nombres,
                Apellidos = solicitud.Apellidos,
                CorreoContacto = solicitud.Correo,
                Telefono = solicitud.Telefono,
                IdArea = request.IdArea,
                IdCargo = request.IdCargo,
                CodTipoContrato = codTipoContrato,
                FechaIngreso = request.FechaIngreso,
                CodEstadoEmpleado = EstadosEmpleado.Activo,
                CreadoEn = DateTimeOffset.UtcNow
            };

            await _context.Empleados.AddAsync(nuevoEmpleado, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 4. Cerrar la solicitud
            solicitud.CodEstadoSolicitud = EstadosSolicitud.Aprobada;
            solicitud.RevisadoPor = adminId;
            solicitud.RevisadoEn = DateTimeOffset.UtcNow;
            solicitud.IdUsuarioCreado = nuevoUsuario.IdUsuario;

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result.Success(new AprobacionRegistroResultDto(
                solicitud.IdSolicitudRegistro,
                nuevoUsuario.IdUsuario,
                nuevoEmpleado.IdEmpleado));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<AprobacionRegistroResultDto>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

// =====================================================================
// 3.2 RECHAZO DEL REGISTRO (ADMINISTRADOR - permiso: registros.aprobar)
// =====================================================================

[RequirePermission(PermisosSistema.RegistrosAprobar)]
public sealed record RechazarSolicitudRegistroCommand(
    long IdSolicitudRegistro,
    string MotivoRechazo) : IRequest<Result>;

public sealed class RechazarSolicitudRegistroCommandValidator : AbstractValidator<RechazarSolicitudRegistroCommand>
{
    public RechazarSolicitudRegistroCommandValidator()
    {
        RuleFor(x => x.IdSolicitudRegistro).GreaterThan(0);
        RuleFor(x => x.MotivoRechazo)
            .NotEmpty().WithMessage("El motivo de rechazo es obligatorio.")
            .MaximumLength(300).WithMessage("El motivo de rechazo no puede superar los 300 caracteres.");
    }
}

public sealed class RechazarSolicitudRegistroCommandHandler : IRequestHandler<RechazarSolicitudRegistroCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public RechazarSolicitudRegistroCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(RechazarSolicitudRegistroCommand request, CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.UserId!.Value;

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var solicitud = await _context.BloquearPorIdAsync<SolicitudRegistro>(
                request.IdSolicitudRegistro, cancellationToken);

            if (solicitud is null || solicitud.CodEstadoSolicitud != EstadosSolicitud.Pendiente)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(AuthErrors.SolicitudNoEncontradaOPendiente);
            }

            solicitud.CodEstadoSolicitud = EstadosSolicitud.Rechazada;
            solicitud.RevisadoPor = adminId;
            solicitud.RevisadoEn = DateTimeOffset.UtcNow;
            solicitud.MotivoRechazo = request.MotivoRechazo.Trim();

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

// =====================================================================
// CONSULTAR SOLICITUDES DE REGISTRO (ADMINISTRADOR - permiso: registros.aprobar)
// =====================================================================

[RequirePermission(PermisosSistema.RegistrosAprobar)]
public sealed record ListarSolicitudesRegistroQuery(
    string? CodEstadoSolicitud = null,
    string? Busqueda = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<SolicitudRegistroDto>>>;

public sealed class ListarSolicitudesRegistroQueryHandler
    : IRequestHandler<ListarSolicitudesRegistroQuery, Result<PagedResult<SolicitudRegistroDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarSolicitudesRegistroQueryHandler(GestionTurnosContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedResult<SolicitudRegistroDto>>> Handle(
        ListarSolicitudesRegistroQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Paginacion.NormalizarPagina(request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        IQueryable<SolicitudRegistro> query = _context.SolicitudesRegistro.AsNoTracking();

        // Bandeja por estado; por defecto solo las PENDIENTE (FLUJO_BD_ADMIN sección 5).
        var estado = string.IsNullOrWhiteSpace(request.CodEstadoSolicitud)
            ? EstadosSolicitud.Pendiente
            : request.CodEstadoSolicitud.Trim().ToUpperInvariant();
        query = query.Where(s => s.CodEstadoSolicitud == estado);

        if (!string.IsNullOrWhiteSpace(request.Busqueda))
        {
            var term = request.Busqueda.Trim().ToLowerInvariant();
            query = query.Where(s =>
                s.Correo.ToLower().Contains(term) ||
                s.NumeroDocumento.ToLower().Contains(term) ||
                s.Nombres.ToLower().Contains(term) ||
                s.Apellidos.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Proyección directa: jamás incluye hash_contrasena (Sección 3.1 punto 4 y Sección 9)
        var items = await query
            .OrderBy(s => s.SolicitadoEn)
            .ThenBy(s => s.IdSolicitudRegistro)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SolicitudRegistroDto(
                s.IdSolicitudRegistro,
                s.CodTipoDocumento,
                s.TipoDocumento.Nombre,
                s.NumeroDocumento,
                s.Nombres,
                s.Apellidos,
                s.Correo,
                s.Telefono,
                s.CodEstadoSolicitud,
                s.EstadoSolicitud.Nombre,
                s.SolicitadoEn,
                s.RevisadoPor,
                s.RevisadoEn,
                s.MotivoRechazo,
                s.IdUsuarioCreado))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<SolicitudRegistroDto>(items, totalCount, pageNumber, pageSize));
    }
}

