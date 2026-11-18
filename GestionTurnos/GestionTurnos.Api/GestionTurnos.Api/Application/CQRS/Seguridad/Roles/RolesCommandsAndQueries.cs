using FluentValidation;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Seguridad;
using GestionTurnos.Api.Application.Rules.Seguridad;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Errors.Seguridad;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.CQRS.Seguridad.Roles;

// =====================================================================
// ROLES Y PERMISOS (RF-01, Sección 2 y 5)
// =====================================================================

[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record ListarRolesQuery(bool SoloActivos = false) : IRequest<Result<IReadOnlyList<RolDto>>>;

public sealed class ListarRolesQueryHandler : IRequestHandler<ListarRolesQuery, Result<IReadOnlyList<RolDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarRolesQueryHandler(GestionTurnosContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<RolDto>>> Handle(ListarRolesQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Rol> query = _context.Roles.AsNoTracking();

        if (request.SoloActivos)
        {
            query = query.Where(r => r.Activo);
        }

        var roles = await query
            .OrderBy(r => r.Nombre)
            .Select(r => new RolDto(
                r.IdRol,
                r.Nombre,
                r.Descripcion,
                r.Activo,
                r.RolPermisos.Select(rp => rp.Permiso.Codigo).ToList()))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<RolDto>>(roles);
    }
}

[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record ListarPermisosQuery : IRequest<Result<IReadOnlyList<PermisoDto>>>;

public sealed class ListarPermisosQueryHandler : IRequestHandler<ListarPermisosQuery, Result<IReadOnlyList<PermisoDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarPermisosQueryHandler(GestionTurnosContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<PermisoDto>>> Handle(ListarPermisosQuery request, CancellationToken cancellationToken)
    {
        var permisos = await _context.Permisos
            .AsNoTracking()
            .OrderBy(p => p.Codigo)
            .Select(p => new PermisoDto(p.IdPermiso, p.Codigo, p.Descripcion))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PermisoDto>>(permisos);
    }
}

[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record GuardarRolCommand(
    int? IdRol,
    string Nombre,
    string? Descripcion,
    bool Activo,
    IReadOnlyList<string> CodigosPermisos) : IRequest<Result<int>>;

public sealed class GuardarRolCommandValidator : AbstractValidator<GuardarRolCommand>
{
    public GuardarRolCommandValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre del rol es obligatorio.")
            .MaximumLength(50);

        RuleFor(x => x.Descripcion)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Descripcion));

        RuleFor(x => x.CodigosPermisos)
            .NotNull();
    }
}

public sealed class GuardarRolCommandHandler : IRequestHandler<GuardarRolCommand, Result<int>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public GuardarRolCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<int>> Handle(GuardarRolCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Mensaje claro antes de llegar a la restricción única de seguridad.rol (nombre).
            var nombreNormalizado = request.Nombre.Trim().ToLower();
            int idRolActual = request.IdRol ?? 0;
            bool nombreDuplicado = await _context.Roles
                .AsNoTracking()
                .AnyAsync(r => r.IdRol != idRolActual && r.Nombre.ToLower() == nombreNormalizado, cancellationToken);

            if (nombreDuplicado)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<int>(AuthErrors.RolNombreDuplicado);
            }

            Rol rol;
            if (request.IdRol.HasValue && request.IdRol.Value > 0)
            {
                // Bloqueo de la regla "último administrador" antes de la fila del rol (orden de bloqueo único).
                await ReglasAdministracionUsuarios.BloquearReglaAdministradoresAsync(_context, cancellationToken);

                var existente = await _context.BloquearPorIdAsync<Rol>(request.IdRol.Value, cancellationToken);

                if (existente is null)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<int>(AuthErrors.RolNoEncontrado);
                }

                bool conservaGestionUsuarios = request.Activo && request.CodigosPermisos.Any(c =>
                    string.Equals(c?.Trim(), PermisosSistema.UsuariosGestionar, StringComparison.OrdinalIgnoreCase));

                if (!conservaGestionUsuarios)
                {
                    var regla = await ReglasAdministracionUsuarios.ValidarRolDejaDeGestionarUsuariosAsync(
                        _context, existente.IdRol, cancellationToken);
                    if (regla.IsFailure)
                    {
                        await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                        return Result.Failure<int>(regla.Error);
                    }
                }

                existente.Nombre = request.Nombre.Trim();
                existente.Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
                existente.Activo = request.Activo;
                rol = existente;
            }
            else
            {
                rol = new Rol
                {
                    Nombre = request.Nombre.Trim(),
                    Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim(),
                    Activo = request.Activo
                };
                await _context.Roles.AddAsync(rol, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // Sincronizar permisos del rol en seguridad.rol_permiso
            var codigosSolicitados = request.CodigosPermisos
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            var permisosBd = await _context.Permisos
                .AsNoTracking()
                .Where(p => codigosSolicitados.Contains(p.Codigo.ToLower()))
                .ToListAsync(cancellationToken);

            if (permisosBd.Count != codigosSolicitados.Count)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<int>(AuthErrors.PermisoNoEncontrado);
            }

            var actuales = await _context.RolesPermisos
                .Where(rp => rp.IdRol == rol.IdRol)
                .ToListAsync(cancellationToken);

            var idsDeseados = permisosBd.Select(p => p.IdPermiso).ToHashSet();

            var aEliminar = actuales.Where(rp => !idsDeseados.Contains(rp.IdPermiso)).ToList();
            if (aEliminar.Count > 0)
            {
                _context.RolesPermisos.RemoveRange(aEliminar);
            }

            var idsActuales = actuales.Select(rp => rp.IdPermiso).ToHashSet();
            var aInsertar = idsDeseados
                .Where(id => !idsActuales.Contains(id))
                .Select(id => new RolPermiso { IdRol = rol.IdRol, IdPermiso = id })
                .ToList();

            if (aInsertar.Count > 0)
            {
                await _context.RolesPermisos.AddRangeAsync(aInsertar, cancellationToken);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success(rol.IdRol);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<int>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

// =====================================================================
// DESACTIVAR ROL, ASIGNAR Y QUITAR PERMISOS (FLUJO_BD_ADMIN sección 4)
// =====================================================================

/// <summary>
/// Desactiva un rol (activo = false). Los roles no se borran.
/// </summary>
[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record DesactivarRolCommand(int IdRol) : IRequest<Result>;

public sealed class DesactivarRolCommandValidator : AbstractValidator<DesactivarRolCommand>
{
    public DesactivarRolCommandValidator()
    {
        RuleFor(x => x.IdRol).GreaterThan(0);
    }
}

public sealed class DesactivarRolCommandHandler : IRequestHandler<DesactivarRolCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public DesactivarRolCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DesactivarRolCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Bloqueo de la regla "último administrador" antes de la fila del rol (orden de bloqueo único).
            await ReglasAdministracionUsuarios.BloquearReglaAdministradoresAsync(_context, cancellationToken);

            var rol = await _context.BloquearPorIdAsync<Rol>(request.IdRol, cancellationToken);
            if (rol is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(AuthErrors.RolNoEncontrado);
            }

            if (!rol.Activo)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Success();
            }

            var regla = await ReglasAdministracionUsuarios.ValidarRolDejaDeGestionarUsuariosAsync(
                _context, rol.IdRol, cancellationToken);
            if (regla.IsFailure)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return regla;
            }

            rol.Activo = false;

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
/// Asigna un permiso a un rol (INSERT en seguridad.rol_permiso; el disparador lo audita).
/// </summary>
[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record AsignarPermisoRolCommand(int IdRol, int IdPermiso) : IRequest<Result>;

public sealed class AsignarPermisoRolCommandValidator : AbstractValidator<AsignarPermisoRolCommand>
{
    public AsignarPermisoRolCommandValidator()
    {
        RuleFor(x => x.IdRol).GreaterThan(0);
        RuleFor(x => x.IdPermiso).GreaterThan(0);
    }
}

public sealed class AsignarPermisoRolCommandHandler : IRequestHandler<AsignarPermisoRolCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public AsignarPermisoRolCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(AsignarPermisoRolCommand request, CancellationToken cancellationToken)
    {
        bool rolExiste = await _context.Roles
            .AsNoTracking()
            .AnyAsync(r => r.IdRol == request.IdRol, cancellationToken);
        if (!rolExiste)
        {
            return Result.Failure(AuthErrors.RolNoEncontrado);
        }

        bool permisoExiste = await _context.Permisos
            .AsNoTracking()
            .AnyAsync(p => p.IdPermiso == request.IdPermiso, cancellationToken);
        if (!permisoExiste)
        {
            return Result.Failure(AuthErrors.PermisoNoEncontrado);
        }

        bool yaAsignado = await _context.RolesPermisos
            .AsNoTracking()
            .AnyAsync(rp => rp.IdRol == request.IdRol && rp.IdPermiso == request.IdPermiso, cancellationToken);
        if (yaAsignado)
        {
            return Result.Failure(AuthErrors.PermisoYaAsignado);
        }

        try
        {
            await _context.RolesPermisos.AddAsync(
                new RolPermiso { IdRol = request.IdRol, IdPermiso = request.IdPermiso },
                cancellationToken);
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
/// Quita un permiso a un rol. Es la única operación con DELETE de esta fase
/// (DELETE FROM seguridad.rol_permiso WHERE id_rol = $1 AND id_permiso = $2; queda auditado por el disparador).
/// </summary>
[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record QuitarPermisoRolCommand(int IdRol, int IdPermiso) : IRequest<Result>;

public sealed class QuitarPermisoRolCommandValidator : AbstractValidator<QuitarPermisoRolCommand>
{
    public QuitarPermisoRolCommandValidator()
    {
        RuleFor(x => x.IdRol).GreaterThan(0);
        RuleFor(x => x.IdPermiso).GreaterThan(0);
    }
}

public sealed class QuitarPermisoRolCommandHandler : IRequestHandler<QuitarPermisoRolCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public QuitarPermisoRolCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(QuitarPermisoRolCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Bloqueo de la regla "último administrador" antes de la fila del rol (orden de bloqueo único).
            await ReglasAdministracionUsuarios.BloquearReglaAdministradoresAsync(_context, cancellationToken);

            var rol = await _context.BloquearPorIdAsync<Rol>(request.IdRol, cancellationToken);
            if (rol is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(AuthErrors.RolNoEncontrado);
            }

            var asignacion = await _context.RolesPermisos
                .Include(rp => rp.Permiso)
                .FirstOrDefaultAsync(rp => rp.IdRol == request.IdRol && rp.IdPermiso == request.IdPermiso, cancellationToken);

            if (asignacion is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(AuthErrors.PermisoNoAsignado);
            }

            if (string.Equals(asignacion.Permiso.Codigo, PermisosSistema.UsuariosGestionar, StringComparison.OrdinalIgnoreCase))
            {
                var regla = await ReglasAdministracionUsuarios.ValidarRolDejaDeGestionarUsuariosAsync(
                    _context, request.IdRol, cancellationToken);
                if (regla.IsFailure)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return regla;
                }
            }

            _context.RolesPermisos.Remove(asignacion);

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
/// Sección 2: Carga en seguridad.permiso los códigos usados en el sistema y los asigna al rol Administrador en rol_permiso.
/// </summary>
[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record InicializarPermisosSistemaCommand : IRequest<Result<IReadOnlyList<PermisoDto>>>;

public sealed class InicializarPermisosSistemaCommandHandler
    : IRequestHandler<InicializarPermisosSistemaCommand, Result<IReadOnlyList<PermisoDto>>>
{
    private static readonly IReadOnlyList<(string Codigo, string Descripcion)> CatalogoPermisos = PermisosSistema.Catalogo;

    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public InicializarPermisosSistemaCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<PermisoDto>>> Handle(
        InicializarPermisosSistemaCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var existentes = await _context.Permisos.ToListAsync(cancellationToken);
            var codigosExistentes = existentes
                .Select(p => p.Codigo)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (codigo, descripcion) in CatalogoPermisos)
            {
                if (!codigosExistentes.Contains(codigo))
                {
                    var nuevo = new Permiso
                    {
                        Codigo = codigo,
                        Descripcion = descripcion
                    };
                    await _context.Permisos.AddAsync(nuevo, cancellationToken);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var todosPermisos = await _context.Permisos.AsNoTracking().ToListAsync(cancellationToken);
            var rolAdmin = await _context.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Nombre == "Administrador", cancellationToken);

            if (rolAdmin is not null)
            {
                var asignadosAdmin = await _context.RolesPermisos
                    .AsNoTracking()
                    .Where(rp => rp.IdRol == rolAdmin.IdRol)
                    .Select(rp => rp.IdPermiso)
                    .ToHashSetAsync(cancellationToken);

                foreach (var perm in todosPermisos)
                {
                    if (!asignadosAdmin.Contains(perm.IdPermiso))
                    {
                        await _context.RolesPermisos.AddAsync(new RolPermiso
                        {
                            IdRol = rolAdmin.IdRol,
                            IdPermiso = perm.IdPermiso
                        }, cancellationToken);
                    }
                }
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            var resultado = todosPermisos
                .OrderBy(p => p.Codigo)
                .Select(p => new PermisoDto(p.IdPermiso, p.Codigo, p.Descripcion))
                .ToList();

            return Result.Success<IReadOnlyList<PermisoDto>>(resultado);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<IReadOnlyList<PermisoDto>>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

