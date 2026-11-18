using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Errors.Seguridad;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Infrastructure.Identity;

/// <summary>
/// Implementa la resolución obligatoria de id_empleado para el usuario en sesión (Sección 2):
/// SELECT e.id_empleado FROM organizacion.empleado e WHERE e.id_usuario = $id_usuario_sesion AND e.cod_estado_empleado = 'ACTIVO';
/// </summary>
public sealed class EmpleadoSesionService : IEmpleadoSesionService
{
    private readonly ICurrentUserService _currentUserService;
    private readonly GestionTurnosContext _context;
    private long? _cachedEmpleadoId;
    private bool _resolved;

    public EmpleadoSesionService(ICurrentUserService currentUserService, GestionTurnosContext context)
    {
        _currentUserService = currentUserService;
        _context = context;
    }

    public async Task<Result<long>> ObtenerIdEmpleadoActivoAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<long>(AuthErrors.Unauthorized);
        }

        if (_resolved)
        {
            return _cachedEmpleadoId.HasValue
                ? Result.Success(_cachedEmpleadoId.Value)
                : Result.Failure<long>(AuthErrors.EmpleadoNoActivo);
        }

        var idUsuario = _currentUserService.UserId.Value;

        var idEmpleado = await _context.Empleados
            .AsNoTracking()
            .Where(e => e.IdUsuario == idUsuario && e.CodEstadoEmpleado == EstadosEmpleado.Activo)
            .Select(e => (long?)e.IdEmpleado)
            .FirstOrDefaultAsync(cancellationToken);

        _cachedEmpleadoId = idEmpleado;
        _resolved = true;

        if (!idEmpleado.HasValue)
        {
            return Result.Failure<long>(AuthErrors.EmpleadoNoActivo);
        }

        return Result.Success(idEmpleado.Value);
    }
}

