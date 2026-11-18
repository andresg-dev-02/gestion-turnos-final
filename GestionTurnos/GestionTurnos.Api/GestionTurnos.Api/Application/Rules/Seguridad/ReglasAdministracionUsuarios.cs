using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Errors.Seguridad;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.Rules.Seguridad;

/// <summary>
/// Reglas de FLUJO_BD_ADMIN sección 4 que protegen el acceso administrativo.
/// Deben evaluarse dentro de la misma transacción que aplica el cambio.
/// </summary>
public static class ReglasAdministracionUsuarios
{
    /// <summary>
    /// Serializa las operaciones que pueden reducir el número de administradores activos
    /// (cambio de estado o rol de un usuario, desactivar un rol, quitarle <c>usuarios.gestionar</c>).
    /// Bloquea la fila del permiso <c>usuarios.gestionar</c> hasta el fin de la transacción; sin esto,
    /// dos administradores que se desactivan entre sí al mismo tiempo pasarían ambos el conteo y el
    /// sistema quedaría sin administradores. Debe llamarse justo después de abrir la transacción,
    /// antes de bloquear cualquier otra fila, para mantener un orden de bloqueo único.
    /// </summary>
    public static async Task BloquearReglaAdministradoresAsync(
        GestionTurnosContext context,
        CancellationToken cancellationToken)
    {
        var idPermiso = await context.Permisos
            .AsNoTracking()
            .Where(p => p.Codigo == PermisosSistema.UsuariosGestionar)
            .Select(p => (int?)p.IdPermiso)
            .FirstOrDefaultAsync(cancellationToken);

        if (idPermiso.HasValue)
        {
            await context.BloquearPorIdAsync<Permiso>(idPermiso.Value, cancellationToken);
        }
    }

    /// <summary>
    /// Indica si el rol (activo) otorga el permiso <c>usuarios.gestionar</c>.
    /// </summary>
    public static Task<bool> RolGestionaUsuariosAsync(
        GestionTurnosContext context,
        int idRol,
        CancellationToken cancellationToken)
    {
        return context.RolesPermisos
            .AsNoTracking()
            .AnyAsync(rp => rp.IdRol == idRol &&
                            rp.Rol.Activo &&
                            rp.Permiso.Codigo == PermisosSistema.UsuariosGestionar,
                cancellationToken);
    }

    /// <summary>
    /// Cuenta los usuarios ACTIVO, con rol activo y permiso <c>usuarios.gestionar</c>,
    /// excluyendo opcionalmente un usuario o todos los usuarios de un rol.
    /// </summary>
    public static Task<int> ContarAdministradoresActivosAsync(
        GestionTurnosContext context,
        long? excluirIdUsuario,
        int? excluirIdRol,
        CancellationToken cancellationToken)
    {
        return context.Usuarios
            .AsNoTracking()
            .Where(u => u.CodEstadoUsuario == EstadosUsuario.Activo &&
                        u.Rol.Activo &&
                        u.Rol.RolPermisos.Any(rp => rp.Permiso.Codigo == PermisosSistema.UsuariosGestionar))
            .Where(u => excluirIdUsuario == null || u.IdUsuario != excluirIdUsuario)
            .Where(u => excluirIdRol == null || u.IdRol != excluirIdRol)
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// Valida un cambio de estado y/o rol sobre un usuario:
    /// el administrador no puede desactivarse, bloquearse ni cambiarse el rol a sí mismo,
    /// y siempre debe quedar al menos otro usuario activo con <c>usuarios.gestionar</c>.
    /// </summary>
    public static async Task<Result> ValidarCambioUsuarioAsync(
        GestionTurnosContext context,
        long idUsuarioSesion,
        long idUsuarioAfectado,
        string estadoActual,
        int idRolActual,
        string estadoNuevo,
        int idRolNuevo,
        CancellationToken cancellationToken)
    {
        bool cambiaEstado = !string.Equals(estadoActual, estadoNuevo, StringComparison.OrdinalIgnoreCase);
        bool cambiaRol = idRolActual != idRolNuevo;

        if (!cambiaEstado && !cambiaRol)
        {
            return Result.Success();
        }

        if (idUsuarioAfectado == idUsuarioSesion)
        {
            if (cambiaRol)
            {
                return Result.Failure(AuthErrors.NoPuedeCambiarsePropioRol);
            }

            if (estadoNuevo != EstadosUsuario.Activo)
            {
                return Result.Failure(AuthErrors.NoPuedeDesactivarseASiMismo);
            }
        }

        // Solo importa si el usuario afectado hoy cuenta como administrador activo.
        bool esAdministradorActivo = estadoActual == EstadosUsuario.Activo &&
                                     await RolGestionaUsuariosAsync(context, idRolActual, cancellationToken);
        if (!esAdministradorActivo)
        {
            return Result.Success();
        }

        bool seguiraSiendoAdministrador = estadoNuevo == EstadosUsuario.Activo &&
                                          (!cambiaRol || await RolGestionaUsuariosAsync(context, idRolNuevo, cancellationToken));
        if (seguiraSiendoAdministrador)
        {
            return Result.Success();
        }

        int otros = await ContarAdministradoresActivosAsync(context, idUsuarioAfectado, null, cancellationToken);
        return otros > 0 ? Result.Success() : Result.Failure(AuthErrors.UltimoAdministrador);
    }

    /// <summary>
    /// Valida que al desactivar un rol o quitarle <c>usuarios.gestionar</c>
    /// quede al menos un usuario activo con ese permiso en otro rol.
    /// </summary>
    public static async Task<Result> ValidarRolDejaDeGestionarUsuariosAsync(
        GestionTurnosContext context,
        int idRol,
        CancellationToken cancellationToken)
    {
        if (!await RolGestionaUsuariosAsync(context, idRol, cancellationToken))
        {
            return Result.Success();
        }

        int otros = await ContarAdministradoresActivosAsync(context, null, idRol, cancellationToken);
        return otros > 0 ? Result.Success() : Result.Failure(AuthErrors.UltimoAdministrador);
    }
}
