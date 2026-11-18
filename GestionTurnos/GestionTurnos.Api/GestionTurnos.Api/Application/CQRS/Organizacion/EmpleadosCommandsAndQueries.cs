using FluentValidation;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Organizacion;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Entities.Organizacion;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Application.Rules.Seguridad;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Errors.Organizacion;
using GestionTurnos.Api.Domain.Errors.Seguridad;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.CQRS.Organizacion;

// =====================================================================
// GESTIÓN ADMINISTRATIVA DE EMPLEADOS (Sección 5 - RF-02, RG-06)
// =====================================================================

[RequirePermission(PermisosSistema.EmpleadosGestionar)]
public sealed record ListarEmpleadosQuery(
    int? IdArea = null,
    int? IdSucursal = null,
    int? IdCargo = null,
    string? CodEstadoEmpleado = null,
    string? Busqueda = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<EmpleadoDto>>>;

public sealed class ListarEmpleadosQueryHandler
    : IRequestHandler<ListarEmpleadosQuery, Result<PagedResult<EmpleadoDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarEmpleadosQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<PagedResult<EmpleadoDto>>> Handle(
        ListarEmpleadosQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Paginacion.NormalizarPagina(request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        IQueryable<Empleado> query = _context.Empleados.AsNoTracking();

        if (request.IdArea.HasValue)
        {
            query = query.Where(e => e.IdArea == request.IdArea.Value);
        }

        if (request.IdSucursal.HasValue)
        {
            query = query.Where(e => e.Area.IdSucursal == request.IdSucursal.Value);
        }

        if (request.IdCargo.HasValue)
        {
            query = query.Where(e => e.IdCargo == request.IdCargo.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CodEstadoEmpleado))
        {
            var estado = request.CodEstadoEmpleado.Trim().ToUpperInvariant();
            query = query.Where(e => e.CodEstadoEmpleado == estado);
        }

        if (!string.IsNullOrWhiteSpace(request.Busqueda))
        {
            var term = request.Busqueda.Trim().ToLowerInvariant();
            query = query.Where(e =>
                e.NumeroDocumento.ToLower().Contains(term) ||
                e.Nombres.ToLower().Contains(term) ||
                e.Apellidos.ToLower().Contains(term) ||
                (e.CorreoContacto != null && e.CorreoContacto.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.Apellidos)
            .ThenBy(e => e.Nombres)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EmpleadoDto(
                e.IdEmpleado,
                e.IdUsuario,
                e.Usuario != null ? e.Usuario.Correo : null,
                e.CodTipoDocumento,
                e.TipoDocumento.Nombre,
                e.NumeroDocumento,
                e.Nombres,
                e.Apellidos,
                e.CorreoContacto,
                e.Telefono,
                e.Direccion,
                e.IdArea,
                e.Area.Nombre,
                e.Area.IdSucursal,
                e.Area.Sucursal.Nombre,
                e.IdCargo,
                e.Cargo.Nombre,
                e.CodTipoContrato,
                e.TipoContrato != null ? e.TipoContrato.Nombre : null,
                e.FechaIngreso,
                e.FechaRetiro,
                e.CodEstadoEmpleado,
                e.EstadoEmpleado.Nombre,
                e.CreadoEn))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<EmpleadoDto>(items, totalCount, pageNumber, pageSize));
    }
}

[RequirePermission(PermisosSistema.EmpleadosGestionar)]
public sealed record ObtenerEmpleadoPorIdQuery(long IdEmpleado) : IRequest<Result<EmpleadoDto>>;

public sealed class ObtenerEmpleadoPorIdQueryHandler : IRequestHandler<ObtenerEmpleadoPorIdQuery, Result<EmpleadoDto>>
{
    private readonly GestionTurnosContext _context;

    public ObtenerEmpleadoPorIdQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<EmpleadoDto>> Handle(ObtenerEmpleadoPorIdQuery request, CancellationToken cancellationToken)
    {
        var empleado = await _context.Empleados
            .AsNoTracking()
            .Where(e => e.IdEmpleado == request.IdEmpleado)
            .Select(e => new EmpleadoDto(
                e.IdEmpleado,
                e.IdUsuario,
                e.Usuario != null ? e.Usuario.Correo : null,
                e.CodTipoDocumento,
                e.TipoDocumento.Nombre,
                e.NumeroDocumento,
                e.Nombres,
                e.Apellidos,
                e.CorreoContacto,
                e.Telefono,
                e.Direccion,
                e.IdArea,
                e.Area.Nombre,
                e.Area.IdSucursal,
                e.Area.Sucursal.Nombre,
                e.IdCargo,
                e.Cargo.Nombre,
                e.CodTipoContrato,
                e.TipoContrato != null ? e.TipoContrato.Nombre : null,
                e.FechaIngreso,
                e.FechaRetiro,
                e.CodEstadoEmpleado,
                e.EstadoEmpleado.Nombre,
                e.CreadoEn))
            .FirstOrDefaultAsync(cancellationToken);

        if (empleado is null)
        {
            return Result.Failure<EmpleadoDto>(new Error(
                "Organizacion.EmpleadoNoEncontrado",
                "El empleado solicitado no fue encontrado.",
                ErrorType.NotFound));
        }

        return Result.Success(empleado);
    }
}

[RequirePermission(PermisosSistema.EmpleadosGestionar)]
public sealed record CrearEmpleadoCommand(
    string CodTipoDocumento,
    string NumeroDocumento,
    string Nombres,
    string Apellidos,
    string? CorreoContacto,
    string? Telefono,
    string? Direccion,
    int IdArea,
    int IdCargo,
    string? CodTipoContrato,
    DateOnly FechaIngreso,
    long? IdUsuarioExistente = null,
    string? CorreoAccesoNuevoUsuario = null,
    string? ContrasenaNuevoUsuario = null) : IRequest<Result<long>>;

public sealed class CrearEmpleadoCommandValidator : AbstractValidator<CrearEmpleadoCommand>
{
    public CrearEmpleadoCommandValidator()
    {
        RuleFor(x => x.CodTipoDocumento).NotEmpty().MaximumLength(10);
        RuleFor(x => x.NumeroDocumento).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Nombres).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Apellidos).NotEmpty().MaximumLength(80);
        RuleFor(x => x.CorreoContacto).EmailAddress().MaximumLength(150).When(x => !string.IsNullOrWhiteSpace(x.CorreoContacto));
        RuleFor(x => x.Telefono).MaximumLength(30).When(x => !string.IsNullOrWhiteSpace(x.Telefono));
        RuleFor(x => x.Direccion).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Direccion));
        RuleFor(x => x.IdArea).GreaterThan(0);
        RuleFor(x => x.IdCargo).GreaterThan(0);
        RuleFor(x => x.CodTipoContrato).MaximumLength(20).When(x => !string.IsNullOrWhiteSpace(x.CodTipoContrato));
        RuleFor(x => x.FechaIngreso).NotEmpty();

        RuleFor(x => x.CorreoAccesoNuevoUsuario)
            .EmailAddress().MaximumLength(150)
            .When(x => !string.IsNullOrWhiteSpace(x.CorreoAccesoNuevoUsuario));

        // El correo de acceso y la contraseña temporal van juntos: antes, un correo sin contraseña
        // creaba el empleado sin usuario y sin avisar.
        RuleFor(x => x.ContrasenaNuevoUsuario)
            .NotEmpty().WithMessage("La contraseña temporal es obligatoria cuando se indica el correo de acceso.")
            .MinimumLength(8).MaximumLength(72)
            .When(x => !string.IsNullOrWhiteSpace(x.CorreoAccesoNuevoUsuario));

        RuleFor(x => x.CorreoAccesoNuevoUsuario)
            .NotEmpty().WithMessage("El correo de acceso es obligatorio cuando se indica la contraseña temporal.")
            .When(x => !string.IsNullOrWhiteSpace(x.ContrasenaNuevoUsuario));

        RuleFor(x => x.IdUsuarioExistente)
            .Null().WithMessage("Indique un usuario existente o los datos de un usuario nuevo, no ambos.")
            .When(x => !string.IsNullOrWhiteSpace(x.CorreoAccesoNuevoUsuario));
    }
}

public sealed class CrearEmpleadoCommandHandler : IRequestHandler<CrearEmpleadoCommand, Result<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public CrearEmpleadoCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<long>> Handle(CrearEmpleadoCommand request, CancellationToken cancellationToken)
    {
        var codTipoDoc = request.CodTipoDocumento.Trim().ToUpperInvariant();
        var numDoc = request.NumeroDocumento.Trim();
        var codTipoContrato = string.IsNullOrWhiteSpace(request.CodTipoContrato)
            ? null
            : request.CodTipoContrato.Trim().ToUpperInvariant();

        try
        {
            // Usuario (si aplica) y empleado en una sola transacción (Sección 7).
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var catalogos = await ValidacionesEmpleado.ValidarCatalogosAsync(
                _context, request.IdArea, request.IdCargo, codTipoDoc, codTipoContrato, cancellationToken);
            if (catalogos.IsFailure)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<long>(catalogos.Error);
            }

            bool documentoExiste = await _context.Empleados
                .AsNoTracking()
                .AnyAsync(e => e.CodTipoDocumento == codTipoDoc && e.NumeroDocumento == numDoc, cancellationToken);
            if (documentoExiste)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<long>(AuthErrors.DocumentoYaRegistrado);
            }

            long? idUsuario = null;

            if (request.IdUsuarioExistente.HasValue)
            {
                bool usuarioExiste = await _context.Usuarios
                    .AsNoTracking()
                    .AnyAsync(u => u.IdUsuario == request.IdUsuarioExistente.Value, cancellationToken);
                if (!usuarioExiste)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<long>(AuthErrors.UsuarioNoEncontrado);
                }

                bool yaVinculado = await _context.Empleados
                    .AsNoTracking()
                    .AnyAsync(e => e.IdUsuario == request.IdUsuarioExistente.Value, cancellationToken);
                if (yaVinculado)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<long>(OrganizacionErrors.UsuarioYaVinculado);
                }

                idUsuario = request.IdUsuarioExistente.Value;
            }
            else if (!string.IsNullOrWhiteSpace(request.CorreoAccesoNuevoUsuario))
            {
                if (string.IsNullOrWhiteSpace(request.ContrasenaNuevoUsuario))
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<long>(OrganizacionErrors.AccesoIncompleto);
                }

                var correo = request.CorreoAccesoNuevoUsuario.Trim().ToLowerInvariant();

                bool correoExiste = await _context.Usuarios
                    .AsNoTracking()
                    .AnyAsync(u => u.Correo.ToLower() == correo, cancellationToken);
                if (correoExiste)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<long>(AuthErrors.CorreoYaRegistrado);
                }

                var rolEmpleado = await _context.Roles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Nombre == "Empleado" && r.Activo, cancellationToken);

                if (rolEmpleado is null)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<long>(AuthErrors.RolEmpleadoNoConfigurado);
                }

                var nuevoUsuario = new Usuario
                {
                    Correo = correo,
                    HashContrasena = _passwordHasher.Hash(request.ContrasenaNuevoUsuario),
                    IdRol = rolEmpleado.IdRol,
                    CodEstadoUsuario = EstadosUsuario.Activo,
                    CreadoEn = DateTimeOffset.UtcNow
                };

                await _context.Usuarios.AddAsync(nuevoUsuario, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                idUsuario = nuevoUsuario.IdUsuario;
            }

            var empleado = new Empleado
            {
                IdUsuario = idUsuario,
                CodTipoDocumento = codTipoDoc,
                NumeroDocumento = numDoc,
                Nombres = request.Nombres.Trim(),
                Apellidos = request.Apellidos.Trim(),
                CorreoContacto = string.IsNullOrWhiteSpace(request.CorreoContacto) ? null : request.CorreoContacto.Trim(),
                Telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim(),
                Direccion = string.IsNullOrWhiteSpace(request.Direccion) ? null : request.Direccion.Trim(),
                IdArea = request.IdArea,
                IdCargo = request.IdCargo,
                CodTipoContrato = codTipoContrato,
                FechaIngreso = request.FechaIngreso,
                CodEstadoEmpleado = EstadosEmpleado.Activo,
                CreadoEn = DateTimeOffset.UtcNow
            };

            await _context.Empleados.AddAsync(empleado, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result.Success(empleado.IdEmpleado);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<long>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

[RequirePermission(PermisosSistema.EmpleadosGestionar)]
public sealed record ActualizarEmpleadoCommand(
    long IdEmpleado,
    string CodTipoDocumento,
    string NumeroDocumento,
    string Nombres,
    string Apellidos,
    string? CorreoContacto,
    string? Telefono,
    string? Direccion,
    int IdArea,
    int IdCargo,
    string? CodTipoContrato,
    DateOnly FechaIngreso) : IRequest<Result>;

public sealed class ActualizarEmpleadoCommandValidator : AbstractValidator<ActualizarEmpleadoCommand>
{
    public ActualizarEmpleadoCommandValidator()
    {
        RuleFor(x => x.IdEmpleado).GreaterThan(0);
        RuleFor(x => x.CodTipoDocumento).NotEmpty().MaximumLength(10);
        RuleFor(x => x.NumeroDocumento).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Nombres).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Apellidos).NotEmpty().MaximumLength(80);
        RuleFor(x => x.CorreoContacto).EmailAddress().MaximumLength(150).When(x => !string.IsNullOrWhiteSpace(x.CorreoContacto));
        RuleFor(x => x.Telefono).MaximumLength(30).When(x => !string.IsNullOrWhiteSpace(x.Telefono));
        RuleFor(x => x.Direccion).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Direccion));
        RuleFor(x => x.IdArea).GreaterThan(0);
        RuleFor(x => x.IdCargo).GreaterThan(0);
        RuleFor(x => x.CodTipoContrato).MaximumLength(20).When(x => !string.IsNullOrWhiteSpace(x.CodTipoContrato));
        RuleFor(x => x.FechaIngreso).NotEmpty();
    }
}

/// <summary>
/// Actualiza todos los datos del empleado, incluidos los fijos (Sección 7).
/// El disparador tg_aud_empleado deja en la bitácora el valor anterior y el nuevo.
/// Los catálogos solo se exigen activos cuando cambian: un empleado puede conservar un valor
/// que se desactivó después (por ejemplo, un tipo de contrato retirado).
/// </summary>
public sealed class ActualizarEmpleadoCommandHandler : IRequestHandler<ActualizarEmpleadoCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public ActualizarEmpleadoCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ActualizarEmpleadoCommand request, CancellationToken cancellationToken)
    {
        var codTipoDoc = request.CodTipoDocumento.Trim().ToUpperInvariant();
        var numDoc = request.NumeroDocumento.Trim();
        var codTipoContrato = string.IsNullOrWhiteSpace(request.CodTipoContrato)
            ? null
            : request.CodTipoContrato.Trim().ToUpperInvariant();

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var actual = await _context.Empleados
                .AsNoTracking()
                .Where(e => e.IdEmpleado == request.IdEmpleado)
                .Select(e => new { e.IdArea, e.IdCargo, e.CodTipoDocumento, e.CodTipoContrato })
                .FirstOrDefaultAsync(cancellationToken);

            if (actual is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(OrganizacionErrors.EmpleadoNoEncontrado);
            }

            // Orden de bloqueo: área, cargo y después empleado (el mismo que la desactivación de área o cargo).
            var catalogos = await ValidacionesEmpleado.ValidarCatalogosAsync(
                _context,
                request.IdArea != actual.IdArea ? request.IdArea : null,
                request.IdCargo != actual.IdCargo ? request.IdCargo : null,
                codTipoDoc != actual.CodTipoDocumento ? codTipoDoc : null,
                codTipoContrato != actual.CodTipoContrato ? codTipoContrato : null,
                cancellationToken);
            if (catalogos.IsFailure)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return catalogos;
            }

            var empleado = await _context.BloquearPorIdAsync<Empleado>(request.IdEmpleado, cancellationToken);
            if (empleado is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(OrganizacionErrors.EmpleadoNoEncontrado);
            }

            bool documentoEnUso = await _context.Empleados
                .AsNoTracking()
                .AnyAsync(e => e.IdEmpleado != empleado.IdEmpleado &&
                               e.CodTipoDocumento == codTipoDoc &&
                               e.NumeroDocumento == numDoc, cancellationToken);
            if (documentoEnUso)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(AuthErrors.DocumentoYaRegistrado);
            }

            if (empleado.FechaRetiro.HasValue && empleado.FechaRetiro.Value < request.FechaIngreso)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(new Error(
                    "Organizacion.FechaIngresoInvalida",
                    "La fecha de ingreso no puede ser posterior a la fecha de retiro del empleado.",
                    ErrorType.Failure));
            }

            empleado.CodTipoDocumento = codTipoDoc;
            empleado.NumeroDocumento = numDoc;
            empleado.Nombres = request.Nombres.Trim();
            empleado.Apellidos = request.Apellidos.Trim();
            empleado.CorreoContacto = string.IsNullOrWhiteSpace(request.CorreoContacto) ? null : request.CorreoContacto.Trim();
            empleado.Telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim();
            empleado.Direccion = string.IsNullOrWhiteSpace(request.Direccion) ? null : request.Direccion.Trim();
            empleado.IdArea = request.IdArea;
            empleado.IdCargo = request.IdCargo;
            empleado.CodTipoContrato = codTipoContrato;
            empleado.FechaIngreso = request.FechaIngreso;

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
/// Sección 7 (RF-02, RG-06): Desactivar empleado. En una sola transacción:
/// 1. empleado INACTIVO con fecha_retiro y su usuario INACTIVO (deja de poder iniciar sesión);
/// 2. anula los turnos futuros que no tienen asistencia;
/// 3. cancela sus solicitudes pendientes (vacaciones, novedades y cambios de turno).
/// Nunca borra: el historial del empleado sigue consultable. El disparador audita cada cambio.
/// </summary>
[RequirePermission(PermisosSistema.EmpleadosGestionar)]
public sealed record DesactivarEmpleadoCommand(
    long IdEmpleado,
    DateOnly FechaRetiro) : IRequest<Result>;

public sealed class DesactivarEmpleadoCommandValidator : AbstractValidator<DesactivarEmpleadoCommand>
{
    public DesactivarEmpleadoCommandValidator()
    {
        RuleFor(x => x.IdEmpleado).GreaterThan(0);
        RuleFor(x => x.FechaRetiro).NotEmpty().WithMessage("La fecha de retiro es obligatoria al desactivar un empleado.");
    }
}

public sealed class DesactivarEmpleadoCommandHandler : IRequestHandler<DesactivarEmpleadoCommand, Result>
{
    private const string ObservacionAnulacion = "Anulada por retiro del empleado";

    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public DesactivarEmpleadoCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(DesactivarEmpleadoCommand request, CancellationToken cancellationToken)
    {
        var idUsuarioSesion = _currentUserService.UserId!.Value;
        var ahora = DateTimeOffset.UtcNow;

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Si el empleado tiene usuario, inactivarlo puede dejar el sistema sin administradores:
            // se toma primero el bloqueo de esa regla (mismo orden que en Usuarios).
            await ReglasAdministracionUsuarios.BloquearReglaAdministradoresAsync(_context, cancellationToken);

            var empleado = await _context.BloquearPorIdAsync<Empleado>(request.IdEmpleado, cancellationToken);
            if (empleado is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(OrganizacionErrors.EmpleadoNoEncontrado);
            }

            if (empleado.CodEstadoEmpleado == EstadosEmpleado.Inactivo)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(OrganizacionErrors.EmpleadoYaInactivo);
            }

            if (request.FechaRetiro < empleado.FechaIngreso)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(new Error(
                    "Organizacion.FechaRetiroInvalida",
                    "La fecha de retiro no puede ser anterior a la fecha de ingreso del empleado.",
                    ErrorType.Failure));
            }

            // 1. Empleado y usuario INACTIVO
            if (empleado.IdUsuario.HasValue)
            {
                var usuario = await _context.BloquearPorIdAsync<Usuario>(empleado.IdUsuario.Value, cancellationToken);
                if (usuario is not null)
                {
                    var regla = await ReglasAdministracionUsuarios.ValidarCambioUsuarioAsync(
                        _context,
                        idUsuarioSesion,
                        usuario.IdUsuario,
                        usuario.CodEstadoUsuario,
                        usuario.IdRol,
                        EstadosUsuario.Inactivo,
                        usuario.IdRol,
                        cancellationToken);

                    if (regla.IsFailure)
                    {
                        await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                        return regla;
                    }

                    usuario.CodEstadoUsuario = EstadosUsuario.Inactivo;
                }
            }

            empleado.CodEstadoEmpleado = EstadosEmpleado.Inactivo;
            empleado.FechaRetiro = request.FechaRetiro;

            // 2. Anular turnos futuros sin asistencia. El periodo se guarda como texto (tstzrange),
            //    así que se filtra por fecha en la base y por hora de inicio en memoria.
            var fechaDesde = DateOnly.FromDateTime(ahora.UtcDateTime).AddDays(-1);
            var candidatas = await _context.Asignaciones
                .Where(a => a.IdEmpleado == empleado.IdEmpleado &&
                            a.CodEstadoAsignacion != EstadosAsignacion.Anulada &&
                            a.Fecha >= fechaDesde &&
                            !_context.RegistrosAsistencia.Any(r => r.IdAsignacion == a.IdAsignacion))
                .ToListAsync(cancellationToken);

            var turnosFuturos = candidatas.Where(a => a.Periodo.Inicio > ahora).ToList();
            foreach (var asignacion in turnosFuturos)
            {
                asignacion.CodEstadoAsignacion = EstadosAsignacion.Anulada;
                asignacion.Observacion = ObservacionAnulacion;
            }

            // 3. Cancelar solicitudes pendientes
            var vacaciones = await _context.SolicitudesVacaciones
                .Where(v => v.IdEmpleado == empleado.IdEmpleado && v.CodEstadoSolicitud == EstadosSolicitud.Pendiente)
                .ToListAsync(cancellationToken);
            vacaciones.ForEach(v => v.CodEstadoSolicitud = EstadosSolicitud.Cancelada);

            var novedades = await _context.Novedades
                .Where(n => n.IdEmpleado == empleado.IdEmpleado && n.CodEstadoSolicitud == EstadosSolicitud.Pendiente)
                .ToListAsync(cancellationToken);
            novedades.ForEach(n => n.CodEstadoSolicitud = EstadosSolicitud.Cancelada);

            // Cambios de turno donde participa como solicitante o como compañero
            var cambios = await _context.SolicitudesCambioTurno
                .Where(c => c.CodEstadoSolicitud == EstadosSolicitud.Pendiente &&
                            (c.AsignacionSolicitante.IdEmpleado == empleado.IdEmpleado ||
                             c.AsignacionCompanero.IdEmpleado == empleado.IdEmpleado))
                .ToListAsync(cancellationToken);
            cambios.ForEach(c => c.CodEstadoSolicitud = EstadosSolicitud.Cancelada);

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
/// Reactiva un empleado inactivo fijando cod_estado_empleado = 'ACTIVO', fecha_retiro = null y,
/// en la misma transacción, usuario.cod_estado_usuario = 'ACTIVO' (permiso: empleados.gestionar).
/// Su área y su cargo deben seguir activos: no puede quedar un empleado activo en un área o cargo inactivo.
/// Los turnos anulados y las solicitudes canceladas al retirarlo no se restauran.
/// </summary>
[RequirePermission(PermisosSistema.EmpleadosGestionar)]
public sealed record ReactivarEmpleadoCommand(long IdEmpleado) : IRequest<Result>;

public sealed class ReactivarEmpleadoCommandHandler : IRequestHandler<ReactivarEmpleadoCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public ReactivarEmpleadoCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ReactivarEmpleadoCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var actual = await _context.Empleados
                .AsNoTracking()
                .Where(e => e.IdEmpleado == request.IdEmpleado)
                .Select(e => new { e.IdArea, e.IdCargo })
                .FirstOrDefaultAsync(cancellationToken);

            if (actual is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(OrganizacionErrors.EmpleadoNoEncontrado);
            }

            // Orden de bloqueo: área, cargo y después empleado.
            var catalogos = await ValidacionesEmpleado.ValidarCatalogosAsync(
                _context, actual.IdArea, actual.IdCargo, null, null, cancellationToken);
            if (catalogos.IsFailure)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return catalogos;
            }

            var empleado = await _context.BloquearPorIdAsync<Empleado>(request.IdEmpleado, cancellationToken);
            if (empleado is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(OrganizacionErrors.EmpleadoNoEncontrado);
            }

            if (empleado.CodEstadoEmpleado == EstadosEmpleado.Activo)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(OrganizacionErrors.EmpleadoYaActivo);
            }

            empleado.CodEstadoEmpleado = EstadosEmpleado.Activo;
            empleado.FechaRetiro = null;

            if (empleado.IdUsuario.HasValue)
            {
                var usuario = await _context.BloquearPorIdAsync<Usuario>(empleado.IdUsuario.Value, cancellationToken);
                if (usuario is not null)
                {
                    usuario.CodEstadoUsuario = EstadosUsuario.Activo;
                }
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
/// Validaciones compartidas de catálogos del empleado. Deben ejecutarse dentro de la transacción:
/// área y cargo se bloquean para que no se desactiven mientras se les asigna un empleado.
/// Un parámetro nulo significa "no cambia, no se valida".
/// </summary>
internal static class ValidacionesEmpleado
{
    public static async Task<Result> ValidarCatalogosAsync(
        GestionTurnosContext context,
        int? idArea,
        int? idCargo,
        string? codTipoDocumento,
        string? codTipoContrato,
        CancellationToken cancellationToken)
    {
        if (idArea.HasValue)
        {
            var area = await context.BloquearPorIdAsync<Area>(idArea.Value, cancellationToken);
            if (area is null || !area.Activo)
            {
                return Result.Failure(AuthErrors.AreaNoEncontrada);
            }
        }

        if (idCargo.HasValue)
        {
            var cargo = await context.BloquearPorIdAsync<Cargo>(idCargo.Value, cancellationToken);
            if (cargo is null || !cargo.Activo)
            {
                return Result.Failure(AuthErrors.CargoNoEncontrado);
            }
        }

        if (codTipoDocumento is not null)
        {
            bool tipoDocActivo = await context.TiposDocumento
                .AsNoTracking()
                .AnyAsync(t => t.CodTipoDocumento == codTipoDocumento && t.Activo, cancellationToken);
            if (!tipoDocActivo)
            {
                return Result.Failure(OrganizacionErrors.TipoDocumentoInvalido);
            }
        }

        if (codTipoContrato is not null)
        {
            bool contratoActivo = await context.TiposContrato
                .AsNoTracking()
                .AnyAsync(t => t.CodTipoContrato == codTipoContrato && t.Activo, cancellationToken);
            if (!contratoActivo)
            {
                return Result.Failure(OrganizacionErrors.TipoContratoInvalido);
            }
        }

        return Result.Success();
    }
}
