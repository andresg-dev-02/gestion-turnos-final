using FluentValidation;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Seguridad;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Application.Rules.Seguridad;
using GestionTurnos.Api.Domain.Entities.Auditoria;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Errors.Seguridad;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.CQRS.Seguridad.Usuarios;

// =====================================================================
// LISTAR Y OBTENER USUARIOS (RF-01, Sección 5)
// =====================================================================

[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record ListarUsuariosQuery(
    int? IdRol = null,
    string? CodEstadoUsuario = null,
    string? Busqueda = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<UsuarioDto>>>;

public sealed class ListarUsuariosQueryHandler : IRequestHandler<ListarUsuariosQuery, Result<PagedResult<UsuarioDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarUsuariosQueryHandler(GestionTurnosContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedResult<UsuarioDto>>> Handle(
        ListarUsuariosQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Paginacion.NormalizarPagina(request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        IQueryable<Usuario> query = _context.Usuarios.AsNoTracking();

        if (request.IdRol.HasValue)
        {
            query = query.Where(u => u.IdRol == request.IdRol.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CodEstadoUsuario))
        {
            var estado = request.CodEstadoUsuario.Trim().ToUpperInvariant();
            query = query.Where(u => u.CodEstadoUsuario == estado);
        }

        if (!string.IsNullOrWhiteSpace(request.Busqueda))
        {
            var term = request.Busqueda.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.Correo.ToLower().Contains(term) ||
                (u.Empleado != null && (u.Empleado.Nombres.ToLower().Contains(term) ||
                                        u.Empleado.Apellidos.ToLower().Contains(term) ||
                                        u.Empleado.NumeroDocumento.ToLower().Contains(term))));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(u => u.CreadoEn)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UsuarioDto(
                u.IdUsuario,
                u.Correo,
                u.IdRol,
                u.Rol.Nombre,
                u.CodEstadoUsuario,
                u.EstadoUsuario.Nombre,
                u.UltimoAcceso,
                u.CreadoEn,
                u.Empleado != null ? u.Empleado.IdEmpleado : null,
                u.Empleado != null ? u.Empleado.Nombres + " " + u.Empleado.Apellidos : null))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<UsuarioDto>(items, totalCount, pageNumber, pageSize));
    }
}

[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record ObtenerUsuarioPorIdQuery(long IdUsuario) : IRequest<Result<UsuarioDto>>;

public sealed class ObtenerUsuarioPorIdQueryHandler : IRequestHandler<ObtenerUsuarioPorIdQuery, Result<UsuarioDto>>
{
    private readonly GestionTurnosContext _context;

    public ObtenerUsuarioPorIdQueryHandler(GestionTurnosContext context)
    {
        _context = context;
    }

    public async Task<Result<UsuarioDto>> Handle(ObtenerUsuarioPorIdQuery request, CancellationToken cancellationToken)
    {
        var usuario = await _context.Usuarios
            .AsNoTracking()
            .Where(u => u.IdUsuario == request.IdUsuario)
            .Select(u => new UsuarioDto(
                u.IdUsuario,
                u.Correo,
                u.IdRol,
                u.Rol.Nombre,
                u.CodEstadoUsuario,
                u.EstadoUsuario.Nombre,
                u.UltimoAcceso,
                u.CreadoEn,
                u.Empleado != null ? u.Empleado.IdEmpleado : null,
                u.Empleado != null ? u.Empleado.Nombres + " " + u.Empleado.Apellidos : null))
            .FirstOrDefaultAsync(cancellationToken);

        if (usuario is null)
        {
            return Result.Failure<UsuarioDto>(AuthErrors.UsuarioNoEncontrado);
        }

        return Result.Success(usuario);
    }
}

// =====================================================================
// CREAR Y ACTUALIZAR USUARIO (RF-01, Sección 5 y 3.5)
// =====================================================================

[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record CrearUsuarioCommand(
    string Correo,
    string Contrasena,
    int IdRol) : IRequest<Result<long>>;

public sealed class CrearUsuarioCommandValidator : AbstractValidator<CrearUsuarioCommand>
{
    public CrearUsuarioCommandValidator()
    {
        RuleFor(x => x.Correo)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("El formato del correo electrónico no es válido.")
            .MaximumLength(150);

        RuleFor(x => x.Contrasena)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(72);

        RuleFor(x => x.IdRol).GreaterThan(0).WithMessage("El rol es obligatorio.");
    }
}

public sealed class CrearUsuarioCommandHandler : IRequestHandler<CrearUsuarioCommand, Result<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public CrearUsuarioCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<long>> Handle(CrearUsuarioCommand request, CancellationToken cancellationToken)
    {
        var correo = request.Correo.Trim().ToLowerInvariant();

        bool rolActivo = await _context.Roles
            .AsNoTracking()
            .AnyAsync(r => r.IdRol == request.IdRol && r.Activo, cancellationToken);

        if (!rolActivo)
        {
            return Result.Failure<long>(AuthErrors.RolNoEncontrado);
        }

        bool correoExiste = await _context.Usuarios
            .AsNoTracking()
            .AnyAsync(u => u.Correo.ToLower() == correo, cancellationToken);

        if (correoExiste)
        {
            return Result.Failure<long>(AuthErrors.CorreoYaRegistrado);
        }

        var nuevoUsuario = new Usuario
        {
            Correo = correo,
            HashContrasena = _passwordHasher.Hash(request.Contrasena),
            IdRol = request.IdRol,
            CodEstadoUsuario = EstadosUsuario.Activo,
            CreadoEn = DateTimeOffset.UtcNow
        };

        try
        {
            await _context.Usuarios.AddAsync(nuevoUsuario, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(nuevoUsuario.IdUsuario);
        }
        catch (Exception ex)
        {
            return Result.Failure<long>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

/// <summary>
/// Permite al administrador actualizar correo de acceso (Sección 3.5), rol y estado (ACTIVO, INACTIVO, BLOQUEADO - Sección 5).
/// </summary>
[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record ActualizarUsuarioCommand(
    long IdUsuario,
    string Correo,
    int IdRol,
    string CodEstadoUsuario) : IRequest<Result>;

public sealed class ActualizarUsuarioCommandValidator : AbstractValidator<ActualizarUsuarioCommand>
{
    public ActualizarUsuarioCommandValidator()
    {
        RuleFor(x => x.IdUsuario).GreaterThan(0);

        RuleFor(x => x.Correo)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("El formato del correo electrónico no es válido.")
            .MaximumLength(150);

        RuleFor(x => x.IdRol).GreaterThan(0);

        RuleFor(x => x.CodEstadoUsuario)
            .NotEmpty()
            .Must(e => e?.Trim().ToUpperInvariant() is EstadosUsuario.Activo or EstadosUsuario.Inactivo or EstadosUsuario.Bloqueado)
            .WithMessage("El estado del usuario debe ser ACTIVO, INACTIVO o BLOQUEADO.");
    }
}

public sealed class ActualizarUsuarioCommandHandler : IRequestHandler<ActualizarUsuarioCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public ActualizarUsuarioCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(ActualizarUsuarioCommand request, CancellationToken cancellationToken)
    {
        var nuevoCorreo = request.Correo.Trim().ToLowerInvariant();

        return await CambioUsuario.EjecutarAsync(
            _context,
            _unitOfWork,
            _currentUserService,
            request.IdUsuario,
            request.CodEstadoUsuario.Trim().ToUpperInvariant(),
            request.IdRol,
            async usuario =>
            {
                if (string.Equals(usuario.Correo, nuevoCorreo, StringComparison.OrdinalIgnoreCase))
                {
                    return Result.Success();
                }

                bool correoEnUso = await _context.Usuarios
                    .AsNoTracking()
                    .AnyAsync(u => u.IdUsuario != usuario.IdUsuario && u.Correo.ToLower() == nuevoCorreo, cancellationToken);

                if (correoEnUso)
                {
                    return Result.Failure(AuthErrors.CorreoYaRegistrado);
                }

                usuario.Correo = nuevoCorreo;
                return Result.Success();
            },
            cancellationToken);
    }
}

// =====================================================================
// CAMBIAR ESTADO Y ROL (FLUJO_BD_ADMIN sección 4)
// =====================================================================

/// <summary>
/// Activa, desactiva o bloquea un usuario (UPDATE cod_estado_usuario). El disparador lo audita.
/// </summary>
[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record CambiarEstadoUsuarioCommand(
    long IdUsuario,
    string CodEstadoUsuario) : IRequest<Result>;

public sealed class CambiarEstadoUsuarioCommandValidator : AbstractValidator<CambiarEstadoUsuarioCommand>
{
    public CambiarEstadoUsuarioCommandValidator()
    {
        RuleFor(x => x.IdUsuario).GreaterThan(0);

        RuleFor(x => x.CodEstadoUsuario)
            .NotEmpty()
            .Must(e => e?.Trim().ToUpperInvariant() is EstadosUsuario.Activo or EstadosUsuario.Inactivo or EstadosUsuario.Bloqueado)
            .WithMessage("El estado del usuario debe ser ACTIVO, INACTIVO o BLOQUEADO.");
    }
}

public sealed class CambiarEstadoUsuarioCommandHandler : IRequestHandler<CambiarEstadoUsuarioCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public CambiarEstadoUsuarioCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public Task<Result> Handle(CambiarEstadoUsuarioCommand request, CancellationToken cancellationToken)
    {
        return CambioUsuario.EjecutarAsync(
            _context,
            _unitOfWork,
            _currentUserService,
            request.IdUsuario,
            request.CodEstadoUsuario.Trim().ToUpperInvariant(),
            idRolNuevo: null,
            cambiosAdicionales: null,
            cancellationToken);
    }
}

/// <summary>
/// Cambia el rol de un usuario (UPDATE id_rol). No se asignan roles inactivos. El disparador lo audita.
/// </summary>
[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record CambiarRolUsuarioCommand(
    long IdUsuario,
    int IdRol) : IRequest<Result>;

public sealed class CambiarRolUsuarioCommandValidator : AbstractValidator<CambiarRolUsuarioCommand>
{
    public CambiarRolUsuarioCommandValidator()
    {
        RuleFor(x => x.IdUsuario).GreaterThan(0);
        RuleFor(x => x.IdRol).GreaterThan(0).WithMessage("El rol es obligatorio.");
    }
}

public sealed class CambiarRolUsuarioCommandHandler : IRequestHandler<CambiarRolUsuarioCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public CambiarRolUsuarioCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public Task<Result> Handle(CambiarRolUsuarioCommand request, CancellationToken cancellationToken)
    {
        return CambioUsuario.EjecutarAsync(
            _context,
            _unitOfWork,
            _currentUserService,
            request.IdUsuario,
            estadoNuevo: null,
            request.IdRol,
            cambiosAdicionales: null,
            cancellationToken);
    }
}

/// <summary>
/// Flujo común para cambiar estado y/o rol de un usuario dentro de una transacción,
/// con la fila del usuario bloqueada y las reglas de protección del administrador.
/// </summary>
internal static class CambioUsuario
{
    public static async Task<Result> EjecutarAsync(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        long idUsuario,
        string? estadoNuevo,
        int? idRolNuevo,
        Func<Usuario, Task<Result>>? cambiosAdicionales,
        CancellationToken cancellationToken)
    {
        var idUsuarioSesion = currentUserService.UserId!.Value;

        try
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken);

            // Primero el bloqueo global de la regla "último administrador", luego la fila del usuario.
            await ReglasAdministracionUsuarios.BloquearReglaAdministradoresAsync(context, cancellationToken);

            var usuario = await context.BloquearPorIdAsync<Usuario>(idUsuario, cancellationToken);
            if (usuario is null)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(AuthErrors.UsuarioNoEncontrado);
            }

            var estado = estadoNuevo ?? usuario.CodEstadoUsuario;
            var idRol = idRolNuevo ?? usuario.IdRol;

            if (idRol != usuario.IdRol)
            {
                bool rolActivo = await context.Roles
                    .AsNoTracking()
                    .AnyAsync(r => r.IdRol == idRol && r.Activo, cancellationToken);

                if (!rolActivo)
                {
                    await unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure(AuthErrors.RolNoEncontrado);
                }
            }

            var regla = await ReglasAdministracionUsuarios.ValidarCambioUsuarioAsync(
                context,
                idUsuarioSesion,
                usuario.IdUsuario,
                usuario.CodEstadoUsuario,
                usuario.IdRol,
                estado,
                idRol,
                cancellationToken);

            if (regla.IsFailure)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                return regla;
            }

            if (cambiosAdicionales is not null)
            {
                var adicional = await cambiosAdicionales(usuario);
                if (adicional.IsFailure)
                {
                    await unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return adicional;
                }
            }

            usuario.CodEstadoUsuario = estado;
            usuario.IdRol = idRol;

            // El disparador tg_aud_usuario registra el cambio en la bitácora.
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

// =====================================================================
// RESTABLECER CONTRASEÑA (FLUJO_BD_ADMIN sección 4)
// =====================================================================

/// <summary>
/// Restablece la contraseña de un usuario. El disparador omite hash_contrasena,
/// por eso la fila de bitácora (MODIFICAR) se inserta a mano en la misma transacción.
/// </summary>
[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record RestablecerContrasenaCommand(
    long IdUsuario,
    string NuevaContrasena) : IRequest<Result>;

public sealed class RestablecerContrasenaCommandValidator : AbstractValidator<RestablecerContrasenaCommand>
{
    public RestablecerContrasenaCommandValidator()
    {
        RuleFor(x => x.IdUsuario).GreaterThan(0);

        RuleFor(x => x.NuevaContrasena)
            .NotEmpty().WithMessage("La nueva contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(72);
    }
}

public sealed class RestablecerContrasenaCommandHandler : IRequestHandler<RestablecerContrasenaCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUserService;

    public RestablecerContrasenaCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(RestablecerContrasenaCommand request, CancellationToken cancellationToken)
    {
        var idAdmin = _currentUserService.UserId!.Value;

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var usuario = await _context.BloquearPorIdAsync<Usuario>(request.IdUsuario, cancellationToken);
            if (usuario is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(AuthErrors.UsuarioNoEncontrado);
            }

            var idEmpleado = await _context.Empleados
                .AsNoTracking()
                .Where(e => e.IdUsuario == usuario.IdUsuario)
                .Select(e => (long?)e.IdEmpleado)
                .FirstOrDefaultAsync(cancellationToken);

            usuario.HashContrasena = _passwordHasher.Hash(request.NuevaContrasena);

            // Un restablecimiento cierra la sesión del usuario en todos sus dispositivos.
            await Application.Services.Sesiones.SesionesService.RevocarTodasAsync(
                _context, usuario.IdUsuario, Domain.Entities.Seguridad.MotivosRevocacionSesion.CambioContrasena, cancellationToken);

            await _context.BitacoraNegocio.AddAsync(new BitacoraNegocio
            {
                FechaHora = DateTimeOffset.UtcNow,
                IdUsuario = idAdmin,
                CodTipoAccion = TiposAccionAuditoria.Modificar,
                Esquema = "seguridad",
                Tabla = "usuario",
                IdRegistro = usuario.IdUsuario.ToString(),
                IdEmpleado = idEmpleado,
                Descripcion = "Restablecimiento de contraseña",
                IpOrigen = _currentUserService.IpAddress
            }, cancellationToken);

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
